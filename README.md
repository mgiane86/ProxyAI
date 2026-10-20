# ProxyAI

A local HTTP proxy that exposes **your own prompt presets as if they were models**, so Visual Studio (or any OpenAI/Ollama-compatible client) can use them with *Bring Your Own Model*.

ProxyAI sits between your client and the **OneProvider** API. Each *agent* you define is advertised to the client as a model; when the client picks it, ProxyAI rewrites the request to the real upstream model and injects the agent's Markdown text as the `system` prompt.

```
Visual Studio (Bring Your Own Model → provider: Ollama)
		│  POST http://localhost:11435/v1/chat/completions   model = "coder"
		▼
   ProxyAI (system tray, HttpListener)
		│  1. model "coder"            →  "claude-…"  (real upstream model)
		│  2. injects agent Markdown   →  messages[0] = { role: "system" }
		│  3. applies token limits, strips tools/images if not supported
		│  4. Authorization: Bearer <your API key>
		▼
   https://api.oneprovider.dev
		│
		▼  streamed back chunk by chunk (SSE)
```

## Requirements

| | |
| :--- | :--- |
| OS | Windows (x64) |
| Runtime | **.NET 10** — either the SDK (to build) or nothing at all if you use the self-contained zip from [Releases](../../releases) |
| Network | outbound HTTPS to `https://api.oneprovider.dev` |
| Account | a OneProvider API key (`sk-…`) with credit |

> ⚠️ This is a **.NET 10** project (`net10.0-windows`). With an older SDK `dotnet build` fails with `NETSDK1045`. If you don't want to install the preview SDK, grab the self-contained `win-x64` zip from the Releases page.

## Quick start

```powershell
git clone https://github.com/mgiane86/ProxyAI.git
cd ProxyAI
dotnet build
dotnet run
```

Or, without installing the SDK:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
.\publish\ProxyAI.exe
```

The app has **no window on startup**: it lives in the system tray (shield icon).

1. Double-click the tray icon → **Configuration…**
2. Paste your **OneProvider API key** and press **Check Balance** to verify it.
3. Pick an agent, press **Select Model** → **Refresh from API**, choose the real upstream model.
4. Press **Save & Restart**.
5. Press **Copy Preset** — it puts the exact values to paste into Visual Studio on the clipboard.

### Connecting Visual Studio

1. *Copilot → Manage models → Bring Your Own Model*
2. Provider: **Ollama**
3. Resource endpoint: `http://localhost:11435/v1`
4. Model ID: the **agent name** (e.g. `coder`), not the upstream model
5. Token limits: use the values shown by **Copy Preset** (context on the left, output on the right)
6. Enable tool calling / image support only if the underlying model supports them

The same guide is built into the settings window, so it follows your configured port.

## Configuration

Everything lives in `appsettings.json`, **next to the executable** (not in `%AppData%`). It is created on first save.

```json
{
  "ApiKey": "sk-...",
  "TargetEndpoint": "https://api.oneprovider.dev",
  "Port": 11435,
  "Language": "it",
  "Agents": [
	{
	  "Name": "coder",
	  "ModelId": "claude-…",
	  "Markdown": "Your instructions here, injected as the system prompt.",
	  "MaxInputTokens": 200000,
	  "MaxOutputTokens": 8000,
	  "SupportsTools": true,
	  "SupportsVision": true,
	  "UsePromptCaching": false
	}
  ]
}
```

| Agent field | Meaning |
| :--- | :--- |
| `Name` | the model ID the client sees |
| `ModelId` | the real OneProvider model the request is rewritten to |
| `Markdown` | injected as`system`; empty = nothing injected |
| `MaxInputTokens` | `0` = no limit. Oldest whole turns are trimmed to fit (~4 chars/token estimate) |
| `MaxOutputTokens` | `0` = no limit. Applied only if the client didn't ask for something lower |
| `SupportsTools` | `false` → `tools`, `tool_choice`, `parallel_tool_calls` are stripped from the request |
| `SupportsVision` | `false` → `image_url` content parts are stripped |
| `UsePromptCaching` | sends the system prompt as a `cache_control` block (only on `/v1/messages`) |

## Local endpoints

ProxyAI serves these itself, without contacting upstream:

| Method | Path | Purpose |
| :--- | :--- | :--- |
| `GET` | `/v1/models` | agent list, OpenAI format |
| `GET` | `/api/tags` | agent list, Ollama format |
| `POST` | `/api/show` | fake-but-valid model card, so Visual Studio enables tools/vision |

Everything else is forwarded to `TargetEndpoint` with the agent transformation applied. Streaming responses are passed through chunk by chunk.

## Things a tester should know

- **Port 11435 by default** — deliberately *not* 11434, which is Ollama's default and would prevent the proxy from starting if Ollama is running. You can change it, but the proxy cannot switch port at runtime: it is torn down and recreated on *Save & Restart*.
- **The API key is stored in clear text** in `appsettings.json`. The file is in `.gitignore`; keep it out of synced folders.
- **The client's `Authorization` header is always discarded** and replaced with the configured key.
- **Cost figures are estimates.** Rates are hardcoded in the proxy (1.5 $/M input, 5 $/M output) and don't vary per model — treat the tray tooltip as an indication, not an invoice.
- **Requests are fully buffered** before being forwarded. They must be, to be rewritten.
- **`cache_control` is only sent on `/v1/messages`** (Anthropic native format). On `chat/completions` the system message must stay a plain string. If the upstream rejects it with a 400, the proxy retries once without caching.
- **No automated tests.** Verification is `dotnet build` plus a manual request to `http://localhost:11435/v1/models`.
- The UI and the source comments are in **Italian**.

## Troubleshooting

| Symptom | Cause |
| :--- | :--- |
| "Impossibile avviare il proxy" at startup | port already in use — change `Port` in Configuration |
| `HTTP 401` / `403` | invalid API key (`Check Balance` tells you immediately) |
| `HTTP 429` with `insufficient_balance` | OneProvider credit exhausted (logged as `[SALDO ESAURITO]`) |
| Requests succeed but the agent prompt is ignored | the `model` value doesn't match the agent `Name` exactly (case-insensitive) |
| Empty model list in *Select Model* | `ModelCatalog` cache is empty on first run: press **Refresh from API** |

## Project layout

| File | Role |
| :--- | :--- |
| `Program.cs` | entry point, single-instance mutex, tray icon and menu |
| `ProxyServer.cs` | the core: `HttpListener`, forwarding, body transformation, streaming, metrics |
| `AgentProfile.cs` / `Settings.cs` | data model and `appsettings.json` persistence |
| `SettingForm.cs` | configuration UI |
| `ModelCatalog.cs` / `ModelSelectorForm.cs` | upstream model list and picker |
| `UsageTracker.cs` | daily tokens/cost and balance |
| `Localization.cs` | IT/EN strings (`Loc.T`) |
| `LogForm.cs` | colored activity log |
| `StartupHelper.cs` | run-at-startup registry entry |
| `MarkDown/ARCHITECTURE.md` | request flow, invariants, known debt |
| `MarkDown/CODE_REFERENCE.md` | per-class API reference |

No NuGet packages: BCL only.

## License

MIT — see [LICENSE](LICENSE).
