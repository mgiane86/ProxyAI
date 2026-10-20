# Istruzioni per Copilot — ProxyAI

Sintesi operativa del progetto. **Leggi questo file prima di esplorare il codice**; per i dettagli usa i riferimenti indicati invece di rileggere i sorgenti.

## Cos'è

App WinForms .NET 10 (`net10.0-windows`) che gira come icona di sistema (tray). Espone un proxy HTTP locale (`http://localhost:11435`) compatibile con i provider **[OI]** e **Ollama**, e inoltra le richieste a **OneProvider** (`https://api.oneprovider.dev`). Ogni `AgentProfile` è presentato al client (Visual Studio → *Bring Your Own Model*) come se fosse un modello; il suo testo `Markdown` viene iniettato come messaggio `system`.

- Namespace: **`OneProviderProxy`** (unico in tutto il progetto).
- Soluzione: `ProxyAI.slnx` · Progetto: `ProxyAI.csproj` · **nessun pacchetto NuGet**, solo BCL.
- Interfaccia e commenti del codice sono **in italiano**: mantenerli tali.

## Documentazione (leggila invece dei sorgenti)

| File | Contenuto |
| :--- | :--- |
| `README.md` | Presentazione per chi arriva da fuori (in inglese): requisiti, quick start, guida Visual Studio, limiti noti |
| `MarkDown/ARCHITECTURE.md` | Flusso di una richiesta, trasformazione del body, stato persistente, vincoli tecnici, debito tecnico |
| `MarkDown/CODE_REFERENCE.md` | Reference di ogni classe e metodo, con firme e "dove intervenire" |

## Mappa dei file (una riga ciascuno)

| File | Ruolo |
| :--- | :--- |
| `README.md` / `LICENSE` | Documento di ingresso per un tester esterno; licenza MIT |
| `Program.cs` | Entry point, mutex singola istanza, `TrayApplicationContext` (icona, menu, tooltip, saldo) |
| `ProxyServer.cs` | **Cuore**: `HttpListener`, forwarding, `ApplyAgent` (agente→modello, markdown→system, limiti token, tools/vision, prompt caching), streaming SSE, metriche |
| `Settings.cs` | `AppSettings` + persistenza `appsettings.json` |
| `AgentProfile.cs` | Modello dell'agente (Name, ModelId, Markdown, limiti, flag) |
| `SettingForm.cs` | UI di configurazione (lavora su copie degli agenti, applica solo con *Salva e Riavvia*) |
| `ModelCatalog.cs` | Elenco modelli OneProvider + cache `models_cache.json` |
| `ModelSelectorForm.cs` | Selettore modello (TreeView per famiglia + ricerca) |
| `UsageTracker.cs` | Token/costo giornalieri + saldo, `daily_stats.json` |
| `Localization.cs` | `Loc.T("Chiave")`, dizionario IT/EN |
| `LogForm.cs` | Log colorato thread-safe |
| `StartupHelper.cs` | Avvio automatico (chiave `Run` di HKCU) |
| `OneProvider.cs` | DTO `/v1/dashboard/balance` |

## Regole di modifica (non negoziabili)

1. **Punto unico della logica agenti**: `ProxyServer.ApplyAgent`. Non duplicare trasformazioni del body altrove.
2. **`ApplyAgent` non deve mai lanciare**: in caso di errore restituisce il body originale. Meglio un agente non applicato che una richiesta rotta.
3. **`Content-Length` solo via `context.Response.ContentLength64`**, mai via `Headers["Content-Length"]` → altrimenti `HttpListenerException (87)`.
4. **Non copiare gli header elencati in `IsRestrictedResponseHeader`** (li gestisce HTTP.sys).
5. **Flush dopo ogni chunk** nello streaming SSE.
6. **Nuovo campo di configurazione** = 4 punti: `AppSettings`/`AgentProfile` → controllo in `SettingForm.InitializeUI` (aggiunto a `_grpModels.Controls`) → `SetEditorEnabled` → `ApplyLocalization`, più la chiave in `Localization.cs` e l'uso in `ProxyServer`. In `SettingForm` usa sempre la guardia `_loadingAgent` e il pattern *copia-e-applica-su-Salva*.
7. **Stringhe UI** solo tramite `Loc.T`, mai hardcoded (eccezione: messaggi tecnici di errore in `MessageBox`/log).
8. **Log** tramite `OnLog?.Invoke(msg, Color)` con i colori convenzionali: `DeepSkyBlue` agente, `LimeGreen` OK, `Orange` avviso, `Red` errore.
9. **Denaro in `decimal`**, mai `double`/`float` (costo stimato compreso).
10. **Persistenza**: file JSON in `AppDomain.CurrentDomain.BaseDirectory`, `catch {}` silenzioso è lo stile esistente; se aggiungi scritture critiche usa scrittura atomica.
11. **Non introdurre dipendenze NuGet** senza necessità esplicita.
12. Il server **non cambia porta a caldo**: viene distrutto e ricreato dal callback di `TrayApplicationContext`.

## Comportamenti da non "correggere" per errore

- Il body della richiesta viene **interamente bufferizzato**: serve per riscriverlo e per il retry sul 400 di `cache_control`.
- L'header `Authorization` del client viene **sempre scartato** e sostituito con la `ApiKey` delle impostazioni.
- `cache_control` viene inviato **solo** su path `/v1/messages` (formato Anthropic nativo); in `chat/completions` il `content` del system deve restare una stringa.
- `/api/tags` e `/api/show` sono risposte finte: servono solo al provider "Ollama" di Visual Studio.
- Il troncamento dei messaggi rimuove **turni interi** (mai `system`, mai l'ultimo turno): tagliare a metà lascerebbe messaggi `tool` orfani e l'upstream risponde 400.
- I file di stato stanno accanto all'eseguibile (`bin\…`), **non** in `%AppData%`.
- `appsettings.json` contiene la **API key in chiaro**: è in `.gitignore` insieme a `daily_stats.json` e `models_cache.json`; non rimuovere quelle regole, non copiare i file in cartelle sincronizzate.

## Debito tecnico noto (non reintrodurlo, non segnalarlo come nuovo)

`ModelCatalog.Cached` vuota all'avvio

## Versioning

La versione vive in due posti che devono restare allineati: `<Version>` in `ProxyAI.csproj` e il tag Git `vMAJOR.MINOR.PATCH`. Il versioning parte da **v1.0.0**. `AssemblyVersion`/`FileVersion`/`ProductVersion` sono derivati automaticamente da `<Version>`: non impostarli a mano. Per una nuova release: incrementa `<Version>`, committa, poi `git tag -a vX.Y.Z -m "..."` e push del tag.

## Build

`dotnet build` dalla root (`ProxyAI.slnx`). Non esistono test: la verifica è la compilazione più una prova manuale su `http://localhost:{Port}/v1/models`.

Pacchetto per l'esterno: `dotnet publish ProxyAI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish`, poi `Compress-Archive` del solo `ProxyAI.exe`. `publish/` è in `.gitignore`: il binario va allegato a mano alla GitHub Release.
