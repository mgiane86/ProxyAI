# Changelog

Tutte le modifiche rilevanti a ProxyAI saranno documentate in questo file.

Il formato è basato su [Keep a Changelog](https://keepachangelog.com/it/1.0.0/),
e questo progetto aderisce al [Semantic Versioning](https://semver.org/lang/it/).

---

## [1.1.0] - 2025-01-XX

### Aggiunto
- **Sistema di temi Light/Dark**: scelta tra tema chiaro e scuro nell'interfaccia (non influisce sul proxy)
- **LogForm migliorato**: aggiunti toolbar con filtro ricerca, pulsante copia tutto, checkbox auto-scroll
- **ModelSelectorForm migliorato**: toolbar con ricerca e aggiornamento modelli, contatore modelli visibili/totali, layout riorganizzato con barre superiore e inferiore
- **SettingsForm riorganizzato**: navigazione laterale con 3 sezioni (Connection, Agents & Presets, Setup Guide), layout completamente ridisegnato con card panels e TableLayoutPanel
- **Theme.cs**: nuovo sistema completo di gestione temi con palette Light/Dark, applicazione automatica ai controlli, supporto menu contestuali
- **Nuovi controlli custom**: `ThemedForm`, `CardPanel`, `AccentButton`, `NavButton`, `LogConsoleBox` per un'interfaccia coerente e moderna
- **Localizzazione estesa**: ~40 nuove chiavi di traduzione per tutte le nuove funzionalità UI (EN/IT)

### Modificato
- **Program.cs**: inizializzazione anticipata di `LogForm.Handle` per evitare problemi con `BeginInvoke` prima dell'apertura della finestra (fix: Visual Studio riceve correttamente le risposte anche senza aprire il log)
- **ProxyAI.csproj**: aggiunti `ApplicationHighDpiMode=PerMonitorV2` e `ApplicationDefaultFont=Segoe UI, 9pt` per supporto corretto DPI monitor multipli
- **Settings.cs**: aggiunto campo `Theme` per persistere la preferenza tema utente
- **Localization.cs**: espansa con chiavi per navigazione, temi, stato applicazione, toolbar log, selettore modelli
- Tutti i form ora ereditano da `ThemedForm` e rispondono automaticamente ai cambi tema

### Corretto
- **Fix critico**: `LogForm.Handle` ora viene creato all'avvio anche se la finestra è nascosta, risolvendo il problema di Visual Studio che non riceveva risposte fino all'apertura manuale del log
- Layout responsive migliorato per tutte le finestre con supporto DPI multipli

---

## [1.0.0] - 2025-01-XX

### Aggiunto
- Rilascio iniziale di ProxyAI
- Proxy HTTP locale (`http://localhost:11435`) compatibile con provider OpenAI e Ollama
- Sistema di agenti: ogni `AgentProfile` esposto come modello indipendente
- Iniezione automatica del prompt Markdown dell'agente come messaggio `system`
- Supporto prompt caching Anthropic (riduzione costi ~90% per token cachati)
- Gestione automatica limiti token (max input/output configurabili per agente)
- Rilevamento e strip automatico di tools/vision per modelli che non li supportano
- Interfaccia tray con menu contestuale, tooltip con saldo e statistiche giornaliere
- Form di configurazione con gestione completa agenti (nome, modello, markdown, limiti, features)
- Selettore modelli OneProvider con cache locale e raggruppamento per famiglia
- Sistema di localizzazione IT/EN completo
- Tracciamento token e costo giornaliero con persistenza
- Log colorato thread-safe con timestamp
- Supporto avvio automatico Windows (chiave registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- Gestione errori 429 con distinzione `insufficient_balance` vs rate limit
- Retry automatico su 400 quando `cache_control` non è supportato dal modello
- Streaming SSE completo verso il client
- Conversione automatica tra formati Ollama `/api/tags` e OpenAI `/v1/models`
- Emulazione endpoint `/api/show` per compatibilità provider Ollama
- Persistenza configurazione in `appsettings.json` (escluso da Git)
- Cache modelli in `models_cache.json` (24h validità)
- Statistiche giornaliere in `daily_stats.json` con reset automatico a mezzanotte

### Dettagli tecnici
- Target: .NET 10 (`net10.0-windows`)
- Framework: WinForms
- Nessuna dipendenza NuGet, solo BCL
- Single instance enforcement via mutex globale
- HttpListener su localhost (nessun URL ACL necessario)
- Task asincroni per ogni richiesta HTTP
- Buffering completo body richiesta per riscrittura JSON e retry
- Metriche estratte dai primi 32KB della risposta streaming

[1.1.0]: https://github.com/mgiane86/ProxyAI/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/mgiane86/ProxyAI/releases/tag/v1.0.0
