# ProxyAI · Architettura e Flusso di Esecuzione

> Versione: 1.0
> Target: **.NET 10 (`net10.0-windows`), WinForms, tray application**
> Scopo: descrivere *come funziona* il programma, per poter intervenire sul codice senza rileggere tutti i file.
> Documenti collegati: `CODE_REFERENCE.md` (API per file), `.github/copilot-instructions.md` (sintesi operativa).

---

## 1. Che cos'è

ProxyAI (assembly/progetto `ProxyAI`, namespace **`OneProviderProxy`**) è un piccolo proxy HTTP locale con interfaccia a icona di sistema (system tray) che si frappone tra un client compatibile [OI]/Ollama (tipicamente Visual Studio → *Bring Your Own Model*) e l'API cloud **OneProvider**.

Il suo valore aggiunto non è il forwarding (banale), ma la **finzione degli "agenti"**: ogni `AgentProfile` viene presentato al client come se fosse un modello, e il suo testo Markdown viene iniettato come *system prompt* della richiesta. Il client vede N modelli specializzati; l'upstream vede sempre e solo un modello reale scelto dall'agente.

```
Visual Studio (provider "Ollama")
		│  POST http://localhost:11434/v1/chat/completions
		▼
   ProxyServer (HttpListener, 1 thread accept + Task per richiesta)
		│  1. riscrive "model": "<nome agente>"  →  "<modello OneProvider>"
		│  2. inserisce/antepone il messaggio "system" con il Markdown dell'agente
		│  3. applica limiti token, rimuove tools/immagini se non supportati
		│  4. aggiunge Authorization: Bearer <ApiKey>
		▼
   https://api.oneprovider.dev  (TargetEndpoint)
		│  risposta SSE in streaming
		▼
   streaming verso il client + cattura primi 32 KB → metriche token/costo
```

---

## 2. Ciclo di vita del processo

| Fase | Dove | Note |
| :--- | :--- | :--- |
| Mutex globale `Global\OneProviderProxy_SingleInstance_Mutex_B6A8E2` | `Program.Main` | Una sola istanza. Se già attiva, invia `RegisterWindowMessage` broadcast e termina. |
| `TrayApplicationContext` | `Program.cs` | Costruisce `NotifyIcon`, carica `AppSettings`, crea e avvia il proxy. |
| `ProxyServer.Start()` | `ProxyServer.cs` | `HttpListener` su `http://localhost:{Port}/` (nessun URL ACL necessario per `localhost`), poi `ListenLoop` in background. |
| Salvataggio impostazioni | `SettingsForm.BtnSave_Click` | Callback `onSettingsSaved` → `_server.Dispose()` + nuovo `ProxyServer` + `Start()`. **Il server non supporta il cambio di porta a caldo: viene ricreato.** |
| Uscita | `TrayApplicationContext.Exit` | Dispose server, `NotifyIcon.Visible = false`, `Application.Exit()`. |

Le finestre `SettingsForm` e `LogForm` non vengono mai distrutte: `OnFormClosing` annulla la chiusura dell'utente e chiama `Hide()` (la X nasconde, non chiude).

---

## 3. Flusso di una richiesta (`ProxyServer.ProcessRequestAsync`)

Ordine esatto delle decisioni. Ogni ramo che ritorna prima del forwarding **non** contatta OneProvider.

1. **`GET /v1/models` o `GET /api/tags`** (solo se `Agents.Count > 0`)
   → `WriteAgentListAsync`: elenca gli agenti. Formato [OI] per `/v1/models`, formato Ollama per `/api/tags`. Nessuna chiamata upstream.
2. **`POST /api/show`**
   → `WriteShowResponseAsync`: scheda tecnica finta ma valida dell'agente (capabilities derivate da `SupportsTools`/`SupportsVision`, `context_length` = `MaxInputTokens` o 128000). 404 se il nome non esiste. Serve a Visual Studio per abilitare strumenti/immagini.
3. **Tutto il resto** → forwarding verso `new Uri(new Uri(TargetEndpoint), path)`.
   - Copia gli header della richiesta **tranne** `Host`, `Authorization`, `Content-*`.
   - Se `ApiKey` è valorizzata, aggiunge `Authorization: Bearer <ApiKey>` (sovrascrive sempre quella del client).
   - Il body viene **letto interamente in memoria** (`rawBodyBytes`): serve per poterlo riscrivere e per il retry.
   - `ApplyAgent(...)` produce il body definitivo.
4. **Retry per prompt caching**: se `ApplyAgent` ha inserito `cache_control` e l'upstream risponde **400**:
   - se il messaggio d'errore **non** contiene `cache_control` → il 400 è reale, viene restituito così com'è;
   - altrimenti si riparte dal body **originale** con `allowCaching: false` (evita di applicare due volte il Markdown).
5. **429**: body letto, ricodificato e riscritto; log `[SALDO ESAURITO]` se contiene `insufficient_balance`/`insufficient_quota`, altrimenti `[RATE LIMIT]`.
6. **Streaming**: `HttpCompletionOption.ResponseHeadersRead`, loop `ReadAsync` su buffer 8192 byte con `FlushAsync` per ogni blocco (necessario per SSE), cattura dei **primi 32 KB** in un `MemoryStream` per estrarre le metriche.
7. **Metriche** (solo se token > 0): `ExtractTokenCounts` + `ExtractCacheTokens` → costo stimato → `UsageTracker.RecordUsage`.
8. **Errore**: `502` con corpo `Proxy Error: <messaggio>`; log in rosso. `finally` → `context.Response.Close()`.

### Formula del costo stimato

```
costo = (in * 1.5 + out * 5.0) / 1e6
	  - (cacheRead  * 1.5 * 0.90) / 1e6      // lettura cache ≈ 10% della tariffa
	  + (cacheWrite * 1.5 * 0.25) / 1e6      // scrittura cache ≈ 125% della tariffa
```
Tariffe hardcoded in `ProxyServer` (1.5 $/M input, 5.0 $/M output): sono una **stima**, non un dato dell'API.

---

## 4. `ApplyAgent`: la trasformazione del body

Unico punto in cui la semantica degli agenti viene applicata. Passi, in ordine:

1. Parse JSON; se il body non è JSON o non ha `model` → **body invariato**.
2. Rimozione del suffisso `:latest` (Ollama lo aggiunge).
3. Match dell'agente per `Name` **case-insensitive**; se nessun agente corrisponde → body invariato (comportamento pass-through puro).
4. `obj["model"] = agent.ModelId`.
5. **Markdown → system prompt**:
   - se `messages[0]` è già un `system` con content testuale → `agent.Markdown + "\n\n" + esistente` (il Markdown dell'agente **vince** in testa);
   - altrimenti inserisce un nuovo `system` in posizione 0.
6. **Prompt caching** (solo se `UsePromptCaching` **e** path `/v1/messages` **e** `allowCaching`): il content del system diventa un array `[{type:"text", text, cache_control:{type:"ephemeral"}}]`. Nel formato [OI] `chat/completions` il campo non viene mai inviato perché lì `content` deve essere una stringa.
7. **`MaxOutputTokens`**: imposta `max_tokens` solo se il client non ne ha già chiesto uno più basso.
8. **`MaxInputTokens`**: `TrimMessagesToBudget` rimuove i **turni più vecchi interi** (da un `user` fino al `user` successivo). Mai il `system`, mai l'ultimo turno. Stima token = `caratteri / 4`. Rete di sicurezza: il primo messaggio non-system deve essere `user`.
9. **`SupportsTools == false`** → rimuove `tools`, `tool_choice`, `parallel_tool_calls`.
10. **`SupportsVision == false`** → rimuove i content part `type: "image_url"`.
11. Ogni ritorno è accompagnato da un `log` leggibile (mostrato in `LogForm` con colore `DeepSkyBlue`), es. `Agente 'coder' -> claude-3-5-sonnet (+markdown) (cache) [max_tokens=8000, -tools]`.

In caso di qualunque eccezione, `ApplyAgent` restituisce il body originale: **il proxy non deve mai rompere una richiesta per un errore di trasformazione**.

---

## 5. Stato persistente

Tutti i file stanno in `AppDomain.CurrentDomain.BaseDirectory` (in debug: `bin\Debug\net10.0-windows\`), **non** in `%AppData%`.

| File | Classe | Contenuto |
| :--- | :--- | :--- |
| `appsettings.json` | `AppSettings` | `ApiKey`, `TargetEndpoint`, `Port`, `Language`, `Agents[]`. **Contiene la chiave API in chiaro.** |
| `daily_stats.json` | `UsageTracker` | Token e costo del giorno corrente + ultimo saldo noto. Si azzera automaticamente al cambio data (conservando il saldo). |
| `models_cache.json` | `ModelCatalog` | Ultimo elenco di modelli OneProvider, per usare la finestra impostazioni offline. |

Nessuno dei tre file è versionato: `appsettings.json` non deve mai finire in un repository o in una cartella sincronizzata.

---

## 6. Modello dati

```
AppSettings
 ├─ ApiKey, TargetEndpoint, Port, Language
 └─ Agents : List<AgentProfile>
				├─ Name              // ID modello visto dal client
				├─ ModelId           // modello reale OneProvider
				├─ Markdown          // system prompt iniettato
				├─ MaxInputTokens    // 0 = nessun limite
				├─ MaxOutputTokens   // 0 = nessun limite
				├─ SupportsTools     // default true
				├─ SupportsVision    // default true
				└─ UsePromptCaching  // default false

DailyUsageData            // UsageTracker
 ├─ Date, InputTokens, OutputTokens, EstimatedCostUsd, LastKnownBalanceUsd

OneProviderModel          // ModelCatalog
 └─ Id, DisplayName, Type

OneProviderBalance        // /v1/dashboard/balance
 └─ Object, BalanceUsd, ExpiresAt, IsActive, LastSyncedAt
```

Al primo avvio (o se `Agents` è vuoto) `AppSettings.SeedDefaultAgentsIfEmpty()` crea `architect`, `coder`, `assistant` con `ModelId` vuoto: il client li vede, ma le richieste falliscono finché non si assegna un modello reale.

---

## 7. UI

| Form | Ruolo | Note architetturali |
| :--- | :--- | :--- |
| `TrayApplicationContext` | Icona tray, menu, tooltip | Tooltip limitato a **63 caratteri** (limite `NotifyIcon.Text`). Doppio click → impostazioni. |
| `SettingsForm` | Configurazione completa | Lavora su **copie** degli agenti (`Clone()`); nulla viene applicato prima di *Salva e Riavvia*. Flag `_loadingAgent` impedisce che il caricamento dei controlli riscriva i dati. |
| `LogForm` | Log colorato in tempo reale | `AppendLog` è thread-safe (`InvokeRequired` → `BeginInvoke`). |
| `ModelSelectorForm` | Scelta del modello reale | `TreeView` raggruppato per famiglia, filtro testuale, refresh da API. Riceve `AppSettings` tramite `Tag`. |
| `Form1` | **Residuo del template** | Namespace `ProxyAI`, mai istanziato. Non fa parte del flusso. |

---

## 8. Vincoli tecnici da non violare

Sono tutte regole nate da bug reali già risolti: reintrodurle significa ricrearli.

1. **`Content-Length` va impostato tramite `context.Response.ContentLength64`**, mai via `Headers["Content-Length"]`: le due viste restano desincronizzate e la scrittura fallisce con `HttpListenerException (87) "Parametro non corretto"`.
2. **Non copiare gli header di risposta in `IsRestrictedResponseHeader`** (`Content-Length`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Server`, `Date`, `Trailer`, `Upgrade`): li gestisce HTTP.sys.
3. **Flush dopo ogni blocco** nello streaming, altrimenti il client non riceve i token in tempo reale.
4. **Body bufferizzato**: qualunque modifica al JSON richiede di leggere tutto il body prima di creare il `ByteArrayContent`.
5. **`Content-Type` va ricopiato** dal client al nuovo contenuto, altrimenti l'upstream non interpreta il body.
6. **Fallback 400 su `cache_control`** deve ripartire dal body **originale**, non da quello già trasformato.
7. **`Authorization` del client viene sempre scartato** e sostituito con la `ApiKey` delle impostazioni.
8. **Ogni eccezione in `ApplyAgent` deve restituire il body intatto**: meglio un agente non applicato che una richiesta rotta.

---

## 9. Debito tecnico noto

Utile da sapere prima di intervenire, per non confonderlo con un bug appena introdotto.

- `Form1.cs` / `Form1.Designer.cs` / `Form1.resx`: residui del template, namespace `ProxyAI` diverso dal resto. Rimovibili.
- `ModelCatalog.Cached` viene letto da `SettingsForm.OpenModelSelector` ma **non** viene popolato all'avvio: il primo `Select Model` mostra una lista vuota finché non si preme *Refresh from API*. `ModelCatalog.FetchAsync` esiste e funziona, ma nessuno lo chiama all'apertura del form.
- `UsageTracker` e `AppSettings` scrivono con `File.WriteAllText` non atomico e `catch {}` silenzioso: un crash durante il salvataggio può corrompere il file senza alcun segnale.
- Costi e tariffe sono hardcoded in `ProxyServer`; il calcolo non distingue modello né listino.
- `LogForm` non ha limite di righe: sessioni lunghe con molte richieste accumulano memoria.
- Nessun test automatico nel repository.
- `Loc.CurrentLanguage` è statico mutabile globale: viene impostato dal combo lingua e persiste per il processo.
