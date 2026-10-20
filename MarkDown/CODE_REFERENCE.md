# ProxyAI · Code Reference (API per file)

> Versione: 1.0
> Namespace unico: **`OneProviderProxy`**
> Target: `net10.0-windows` · WinForms · nullable abilitato · implicit usings attivi
> Documento complementare: `ARCHITECTURE.md` (flusso e vincoli), `.github/copilot-instructions.md` (sintesi).

Legenda: ✅ = usato nel flusso · ⚠️ = usato ma con problema noto · ❌ = codice morto

---

## Mappa rapida: dove intervenire

| Se devi… | Tocca |
| :--- | :--- |
| Aggiungere un campo di configurazione globale | `Settings.cs` → `SettingForm.InitializeUI/LoadValues/BtnSave_Click/ApplyLocalization` |
| Aggiungere un campo per agente | `AgentProfile.cs` → `SettingForm` (controllo + `OnFeaturesChanged`/`OnLimitsChanged`) → `ProxyServer.ApplyAgent` |
| Cambiare come viene costruito il system prompt | `ProxyServer.ApplyAgent` (punto unico) |
| Aggiungere un endpoint locale servito dal proxy | `ProxyServer.ProcessRequestAsync` (parte alta, prima del forwarding) |
| Cambiare la formula del costo | `ProxyServer.ProcessRequestAsync` (blocco `estCost`) |
| Persistere nuovi dati | `UsageTracker` (pattern: file JSON in `BaseDirectory`, `lock`, `Load`/`Save`) |
| Aggiungere stringhe UI | `Localization.cs` (`Strings`) + `SettingForm.ApplyLocalization` |
| Aggiungere una finestra | `Program.TrayApplicationContext` (campo + `Show…` + voce menu) |

---

## `ProxyAI.csproj`

`WinExe`, `net10.0-windows`, `UseWindowsForms`, `Nullable=enable`, `ImplicitUsings=enable`, `RootNamespace=OneProviderProxy`.
Nessun pacchetto NuGet esterno: **solo BCL**. Un'eventuale integrazione esterna (es. Obsidian/MCP) aggiungerebbe la prima dipendenza.

---

## `Program.cs` ✅

### `static class Program`
| Membro | Firma | Note |
| :--- | :--- | :--- |
| `AppGuid` | `const string` | `Global\OneProviderProxy_SingleInstance_Mutex_B6A8E2` |
| `ShowAppMessageName` | `const string` | `WM_SHOW_ONEPROVIDER_PROXY_SETTINGS` |
| `WmShowApp` | `static readonly int` | Da `RegisterWindowMessage` |
| `RegisterWindowMessage` | `[DllImport] int(string)` | user32 |
| `PostMessage` | `[DllImport] bool(IntPtr,int,IntPtr,IntPtr)` | user32 |
| `HWND_BROADCAST` | `const int` | `0xFFFF` |
| `Main()` | `[STAThread] static void` | Mutex → se seconda istanza, broadcast e `return`; altrimenti `Application.Run(new TrayApplicationContext())` |

### `class TrayApplicationContext : ApplicationContext`
Campi: `_trayIcon` (`NotifyIcon`, icona `SystemIcons.Shield`), `_settings`, `_server`, `_settingsForm?`, `_logForm` (creato subito), `_messageReceiver`.

| Membro | Firma | Note |
| :--- | :--- | :--- |
| `.ctor()` | — | Carica settings, crea server, menu (`Configurazione…`, `Mostra Log…`, separatore, `Esci`), avvia server, refresh saldo in background |
| `UpdateTrayToolTip` | `void` | **Tronca a 60+`...`** per il limite di 63 caratteri di `NotifyIcon.Text` |
| `RefreshBalanceInBackgroundAsync` | `async Task` | `GET {TargetEndpoint}/v1/dashboard/balance`, timeout 6 s, `catch {}` silenzioso |
| `CreateProxyServer` | `ProxyServer` | Aggancia `OnLog` → `_logForm.AppendLog` |
| `StartServer` | `void(bool isInitialStart = false)` | Mostra balloon solo al primo avvio; `MessageBox` in caso di errore |
| `ShowSettings` | `void` | Ricrea il form solo se `null`/disposed; callback: dispose server → nuovo server → `StartServer(false)` → refresh saldo |
| `ShowLog` | `void` | Ripristina da minimizzato e porta in primo piano |
| `Exit` | `void` | Dispose server, nasconde icona, `DestroyHandle`, `Application.Exit` |
| `MessageReceiverWindow` | `private class : NativeWindow` | Riceve il messaggio broadcast della seconda istanza → `ShowSettings` |

---

## `ProxyServer.cs` ✅ (cuore del programma, ~709 righe)

### `class ProxyServer : IDisposable`
Campi: `_listener` (`HttpListener?`), `_client` (`HttpClient`, creato nel ctor senza timeout), `_cts`, `_settings` (riferimento **condiviso** con `TrayApplicationContext`: le modifiche agli agenti si vedono subito, la porta no).
Evento: `Action<string, Color>? OnLog`. Proprietà: `bool IsRunning`.

| Membro | Firma | Comportamento |
| :--- | :--- | :--- |
| `.ctor` | `(AppSettings settings)` | — |
| `Start` | `void` | `Prefixes.Add("http://localhost:{Port}/")`, `Start()`, log verde, `Task.Run(ListenLoop)` |
| `Stop` | `void` | Cancel + Stop + Close + `null` |
| `ListenLoop` | `private async Task(CancellationToken)` | Accetta e lancia `ProcessRequestAsync` in un `Task` per contesto; esce su `HttpListenerException`/`ObjectDisposedException` |
| `ProcessRequestAsync` | `private async Task(HttpListenerContext, CancellationToken)` | Vedi `ARCHITECTURE.md` §3. Contiene anche retry 400, gestione 429, streaming, metriche, gestione errori |
| `ApplyAgent` | `private byte[](byte[] body, out string? log, out bool cachingRequested, bool allowCaching = true, bool anthropicNative = false)` | **Punto unico** della logica agenti (`ARCHITECTURE.md` §4). Non lancia mai: in caso di errore restituisce `body` |
| `GetInt` | `private static int?(JsonNode?)` | Supporta `int` e `long` (entro `int.MaxValue`) |
| `EstimateTokens` | `private static long(string?)` | `(length + 3) / 4` |
| `IsAnthropicNativePath` | `private static bool(string)` | `path.Contains("/v1/messages")` |
| `TrimMessagesToBudget` | `private static int(JsonArray, int budgetTokens)` | Ritorna il numero di messaggi rimossi; non tocca `system` né l'ultimo turno |
| `RemoveImageParts` | `private static int(JsonArray)` | Rimuove `content` part con `type == "image_url"`; ritorna il conteggio |
| `WriteShowResponseAsync` | `private async Task(HttpListenerContext, CancellationToken)` | Risposta `/api/show` in stile Ollama |
| `WriteAgentListAsync` | `private async Task(HttpListenerContext, bool ollamaFormat, CancellationToken)` | `/v1/models` ([OI]) o `/api/tags` (Ollama) |
| `IsRestrictedResponseHeader` | `private static bool(string)` | Filtro header hop-by-hop / gestiti da HTTP.sys |
| `ExtractTokenCounts` | `private static (long inTokens, long outTokens)(string)` | Regex su `prompt_tokens`/`completion_tokens`, fallback `input_tokens`/`output_tokens` |
| `ExtractCacheTokens` | `private static (long cacheRead, long cacheWrite)(string)` | `cache_read_input_tokens`/`cache_creation_input_tokens`, fallback `cached_tokens` |
| `Dispose` | `void` | `Stop()` + dispose client e cts |

**Colori dei log**: `DeepSkyBlue` = applicazione agente · `LimeGreen` = risposta OK · `LightGreen` = avvio · `Orange` = avviso/fallback/429 · `Red` = errore/saldo esaurito.

---

## `Settings.cs` ✅

### `class AppSettings`
`ApiKey`, `TargetEndpoint` (`https://api.oneprovider.dev`), `Port` (`11435`), `Language` (`""`), `Agents` (`List<AgentProfile>`).
`ConfigPath` = `BaseDirectory/appsettings.json` (statico, privato).

| Membro | Note |
| :--- | :--- |
| `Load()` | `static`; se il file esiste deserializza, normalizza `Agents ??= new()`, chiama `SeedDefaultAgentsIfEmpty()`. `catch {}` silenzioso |
| `SeedDefaultAgentsIfEmpty()` | `private`; crea `architect`/`coder`/`assistant` con `ModelId` **vuoto** |
| `Save()` | `WriteIndented`, `catch {}` silenzioso. **Non atomico** |

---

## `AgentProfile.cs` ✅

`Name`, `ModelId`, `Markdown`, `MaxInputTokens` (0 = nessun limite), `MaxOutputTokens` (0 = nessun limite), `SupportsTools` (`true`), `SupportsVision` (`true`), `UsePromptCaching` (`false`).
`ToString()` → `Name` (usato dalla `ComboBox`). `Clone()` → `MemberwiseClone()` (memberwise: adatto perché tutti i campi sono value/string).

---

## `Localization.cs` ✅

`static class Loc`
- `CurrentLanguage` (`"it"` se `CurrentUICulture` è italiano, altrimenti `"en"`), **statico mutabile globale**.
- `Strings` : `Dictionary<string, (string En, string It)>`.
- `T(string key, params object[] args)` → se la chiave non esiste **restituisce la chiave stessa** (utile per scoprire i buchi); applica `string.Format` solo se ci sono argomenti.

Chiavi suddivise per area: connessione (`Title`, `GrpConn`, `ApiKey`, `ShowKey`, `TargetBaseUrl`, `LocalPort`, `RunStartup`, `CheckBalance`, `BalanceChecking`, `BalanceAvailable`, `StatusActive`, `StatusDisabled`, `KeyExpiry`, `NoExpiry`, `InvalidKey`, `RateLimit`, `ConnError`, `Language`) · agenti (`GrpModels`, `Agent`, `NewAgent`, `DeleteAgent`, `AgentName`, `Markdown`, `LoadFile`, `ClearMd`, `AgentNameInvalid`, `ConfirmDelete`, `Model`, `CopyId`, `CopyPreset`, `ContextLimit`, `OutputLimit`, `Features`, `MaxInput`, `MaxOutput`, `SupportsTools`, `SupportsVision`, `UsePromptCaching`) · selezione modello (`Refresh`, `SelectModel`, `SearchModel`, `SelectedModel`, `NoModelSelected`, `RefreshModels`, `Cancel`, `OtherModels`, `Yes`, `No`) · guida (`GrpGuide`, `GuideStep1`…`GuideStep6`, `GuideCache`) · azioni (`SaveRestart`, `SavedMsg`, `IdCopied`, `PresetCopied`, `CopiedTitle`) · tray (`TraySettings`, `TrayLog`, `TrayExit`, `BalloonTitle`, `BalloonText`, `TipBalance`, `TipToday`) · log (`LogTitle`, `LogClear`).

`GuideStep3` contiene `{0}` = porta locale. `BalanceAvailable` contiene `{0:F2}` = saldo, `{1}` = stato. `ConfirmDelete` `{0}` = nome agente.

---

## `SettingForm.cs` ✅ (~658 righe)

### `class SettingsForm : Form`
Campi principali: `_settings`, `_onSettingsSaved`, `_agents` (**copie** via `Clone()`), `_currentIndex`, `_loadingAgent` (guardia anti-ricorsione: **indispensabile**, gli handler riscrivono i dati).
Gruppi UI: `_grpConn` (connessione + saldo + lingua + avvio automatico), `_grpModels` (agenti), `_grpGuide` (guida statica), `_btnSave`.
`ClientSize` = 520×878, `FormBorderStyle.FixedDialog`.

| Membro | Firma | Note |
| :--- | :--- | :--- |
| `.ctor` | `(AppSettings settings, Action onSettingsSaved)` | Imposta `Loc.CurrentLanguage`, `InitializeUI` → `LoadValues` → `RefreshAgentList(0)` → `ApplyLocalization` |
| `InitializeUI` | `void` | Costruzione manuale dei controlli (nessun designer) |
| `ApplyLocalization` | `void` | Rilegge tutte le stringhe; chiamata anche al cambio lingua |
| `OpenModelSelector` | `void` | ⚠️ usa `ModelCatalog.Cached`, che all'avvio è vuoto → serve *Refresh from API* dentro il selettore |
| `RefreshAgentList` | `void(int selectIndex)` | Ricostruisce la `ComboBox`, clampa l'indice, poi `LoadAgentToEditor` |
| `LoadAgentToEditor` | `void` | Agente → controlli; `Markdown.ReplaceLineEndings()` (il `TextBox` vuole `\r\n`) |
| `SetEditorEnabled` | `void(bool)` | Abilita/disabilita tutto l'editor; usato quando non ci sono agenti |
| `OnAgentNameChanged` | `void` | Aggiorna il modello in-place nella `ComboBox` |
| `OnLimitsChanged` | `void` | `MaxInput/MaxOutput` |
| `OnFeaturesChanged` | `void` | `SupportsTools`/`SupportsVision`/`UsePromptCaching` |
| `NewAgent` | `void` | Nome univoco `agent-N`, `ModelId` vuoto |
| `DeleteCurrentAgent` | `void` | Conferma con `ConfirmDelete` |
| `LoadMarkdownFromFile` | `void` | `OpenFileDialog` `*.md;*.markdown;*.txt`; salva con `\n` |
| `UpdateSelectedPresetDisplay` | `void` | Mostra `ContextLimit` |
| `CopyFullPreset` | `void` | Copia negli appunti endpoint/limiti/flag per la configurazione manuale in Visual Studio |
| `UpdateGuideText` | `void` | Ricostruisce la guida con la porta corrente |
| `CheckBalanceAsync` | `private async Task` | `GET {baseUrl}/v1/dashboard/balance`, timeout 10 s, aggiorna `UsageTracker.UpdateBalance`, gestisce 401/403/429/altro |
| `LoadValues` | `void` | Popola da `_settings` + `StartupHelper.IsRunAtStartupEnabled()` |
| `BtnSave_Click` | `void` | **Validazione nomi** (trim, non vuoti, unici case-insensitive) → copia in `_settings` → `_settings.Save()` → `StartupHelper.SetRunAtStartup` → `_onSettingsSaved()` → `Hide()` |
| `OnFormClosing` | `override void` | La X nasconde, non chiude |
| `ModelPreset` | `private record(string DisplayName, string Id)` | ❌ non referenziato |

**Convenzioni da rispettare in questo file**: `_loadingAgent = true` prima di scrivere sui controlli; ogni nuovo controllo va aggiunto a `_grpModels.Controls`, a `SetEditorEnabled` e ad `ApplyLocalization`.

---

## `LogForm.cs` ✅

`class LogForm : Form` — `RichTextBox` readonly, `Consolas 9.5`, sfondo `FromArgb(28,28,28)`, pulsante `LogClear`.
`AppendLog(string message, Color color)` thread-safe (`InvokeRequired` → `BeginInvoke`), prefissa `[HH:mm:ss]`, `ScrollToCaret()`.
`OnFormClosing` → `Hide()`.
⚠️ `Text` viene assegnato due volte: `Loc.T("LogTitle")` e subito dopo `"OneProvider Proxy"` → la localizzazione del titolo è inefficace.

---

## `ModelCatalog.cs` ✅

`OneProviderModel` : `Id`, `DisplayName`, `Type` (JSON `id`, `display_name`, `type`).
`OneProviderModelList` : `Object`, `Data`.
`static class ModelCatalog` : `Client` (timeout 15 s), `CachePath` = `models_cache.json`, `Cached` (`List<OneProviderModel>`, popolata dal costruttore statico via `LoadCache`).

| Membro | Firma | Note |
| :--- | :--- | :--- |
| `FetchAsync` | `static async Task<List<OneProviderModel>>(string baseUrl, string apiKey, CancellationToken token = default)` | `GET {baseUrl}/v1/models` con Bearer; su qualunque errore **restituisce la cache** |
| `LoadCache` / `SaveCache` | `private static void` | `catch {}` silenzioso |

---

## `ModelSelectorForm.cs` ✅

`class ModelSelectorForm : Form` — `TreeView` (480×420) raggruppato per famiglia, `TextBox` di ricerca, `_btnRefresh`, `_btnOk`, `_btnCancel`.
`.ctor(List<OneProviderModel> models, string? currentModelId)` · proprietà `SelectedModelId`.

| Membro | Note |
| :--- | :--- |
| `PopulateTree` | Nodi padre = famiglie (`GetModelFamily`), figli = modelli con `Tag = OneProviderModel` |
| `FilterTree` | Filtro su `Id` e `DisplayName`; a filtro vuoto ripopola tutto |
| `SelectNodeByModelId` | Cerca negli attuali nodi e fa `EnsureVisible` |
| `RefreshModelsAsync` | Legge `this.Tag as AppSettings` (assegnato da `SettingsForm`), chiama `ModelCatalog.FetchAsync` |
| `GetModelFamily` | `claude`, `gpt`, `gemini`, `deepseek`, `grok`, `glm`, `qwen`, `kimi`, `kling`, `other` |
| `FormatFamilyName` | Nome commerciale per famiglia; default `Loc.T("OtherModels")` |
| `FormatModelDisplay` | `"DisplayName (Id)"` se differiscono, altrimenti `Id` |

---

## `UsageTracker.cs` ✅

`class DailyUsageData` : `Date` (`yyyy-MM-dd`), `InputTokens`, `OutputTokens`, `EstimatedCostUsd`, `LastKnownBalanceUsd` (`decimal?`).

`static class UsageTracker` — `StatsPath` = `daily_stats.json`, `_lock`, `_current`.

| Membro | Note |
| :--- | :--- |
| `.cctor` | `Load()` + `EnsureToday()` |
| `EnsureToday` | `private static`; cambio data → nuovo record **conservando il saldo** |
| `RecordUsage(long, long, decimal costEstimate = 0m)` | Somma e salva sotto `lock` |
| `UpdateBalance(decimal)` | Sovrascrive l'ultimo saldo noto |
| `GetSnapshot()` | `(long inTokens, long outTokens, decimal? balance, decimal cost)` |
| `FormatCompactNumber(long)` | `1.2M` / `45.6k` / numero pieno |

---

## `StartupHelper.cs` ✅

`static class StartupHelper` — `AppName = "OneProviderProxy"`, `RunKeyPath = HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
`IsRunAtStartupEnabled()` → `bool` (false su eccezione). `SetRunAtStartup(bool)` → scrive/elimina il valore con percorso eseguibile tra virgolette (`Environment.ProcessPath`).

---

## `OneProvider.cs` ✅

`class OneProviderBalance` : `Object`, `BalanceUsd` (`balance_usd`, `decimal`), `ExpiresAt` (`expires_at`, `DateTime?`), `IsActive` (`is_active`), `LastSyncedAt` (`last_synced_at`, `DateTime?`). Usato da `Program.RefreshBalanceInBackgroundAsync` e `SettingsForm.CheckBalanceAsync`.

---

## Contratti esterni

### Endpoint *serviti* (in ingresso)
| Metodo | Path | Risposta |
| :--- | :--- | :--- |
| `GET` | `/v1/models` | `{object:"list", data:[{id, object:"model", created, owned_by:"oneprovider-proxy"}]}` |
| `GET` | `/api/tags` | `{models:[{name, model, modified_at, size, digest, details}]}` |
| `POST` | `/api/show` | `{modelfile, parameters, template, details{format:"gguf",family:"proxy",…}, model_info{"proxy.context_length"}, capabilities[]}` — 404 se l'agente non esiste |
| * | tutto il resto | Forwarding verso `TargetEndpoint` con body trasformato |

`/api/tags` e `/api/show` esistono solo per soddisfare il provider "Ollama" di Visual Studio.

### Endpoint *consumati* (in uscita)
| Metodo | URL | Usato da |
| :--- | :--- | :--- |
| `GET` | `{TargetEndpoint}/v1/models` | `ModelCatalog.FetchAsync` |
| `GET` | `{TargetEndpoint}/v1/dashboard/balance` | `Program` (6 s) e `SettingForm` (10 s) |
| * | `{TargetEndpoint}{path}` | `ProxyServer` (forwarding) |
