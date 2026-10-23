# ProxyAI v1.1.0 - UI Temi Light/Dark e Robustezza Migliorata

Questa release introduce un sistema completo di temi per l'interfaccia utente, migliora significativamente la robustezza del sistema e risolve diversi problemi di stabilità.

## 🎨 Nuove Funzionalità UI

### Sistema Temi Light/Dark
- Nuovo sistema completo di gestione temi con palette Light/Dark
- Applicazione automatica ai controlli e menu contestuali
- Nuovi controlli custom: `ThemedForm`, `CardPanel`, `AccentButton`, `NavButton`, `LogConsoleBox`
- Persistenza preferenza tema in `appsettings.json`

### LogForm Migliorato
- Toolbar con filtro ricerca in tempo reale
- Pulsante "Copia Tutto" per esportare i log
- Checkbox auto-scroll configurabile
- **Limite 1000 righe** con rimozione automatica delle più vecchie (risolto memory leak)

### ModelSelectorForm Riorganizzato
- Toolbar con ricerca e pulsante aggiornamento modelli
- Contatore modelli visibili/totali
- Layout completamente ridisegnato con barre superiore e inferiore

### SettingsForm Riprogettato
- Navigazione laterale con 3 sezioni: Connection, Agents & Presets, Setup Guide
- Layout moderno con card panels e TableLayoutPanel
- Interfaccia più intuitiva e accessibile

## 🔧 Fix Critici e Robustezza

### HttpClient Timeout
- **Aggiunto timeout di 60 secondi** all'HttpClient per prevenire blocchi indefiniti nelle richieste verso OneProvider
- Previene hang del proxy in caso di problemi di rete

### Inizializzazione LogForm
- **Fix critico**: `LogForm.Handle` ora viene creato all'avvio anche se la finestra è nascosta
- Risolve il problema di Visual Studio che non riceveva risposte fino all'apertura manuale del log
- `BeginInvoke` funziona correttamente sin dal primo avvio

### Scrittura Atomica File
- **Implementata scrittura atomica** per `appsettings.json` e `daily_stats.json`
- Pattern temp file + rename per prevenire corruzione dati in caso di crash
- Maggiore affidabilità del salvataggio configurazioni e statistiche

### Supporto DPI Multipli
- Aggiunto `ApplicationHighDpiMode=PerMonitorV2` al progetto
- Font di default impostato a `Segoe UI, 9pt`
- Layout responsive migliorato per tutti i form

## 🌍 Localizzazione

- Estesa localizzazione con **~40 nuove chiavi IT/EN**
- Copertura completa per tutte le nuove funzionalità UI
- Chiavi per: navigazione, temi, stato applicazione, toolbar log, selettore modelli

## 📝 Modifiche Tecniche

### ProxyAI.csproj
- Aggiornata versione a `1.1.0`
- Aggiunti `ApplicationHighDpiMode` e `ApplicationDefaultFont`

### Nuovi File
- `Theme.cs`: gestione completa del sistema temi
- `CHANGELOG.md`: storico completo versioni con formato Keep a Changelog

### Settings.cs
- Aggiunto campo `Theme` per persistere la preferenza utente

## 🐛 Bug Risolti

1. **Memory leak in LogForm**: righe accumulate indefinitamente → limite 1000 con trim automatico
2. **HttpClient senza timeout**: richieste bloccate indefinitamente → timeout 60s
3. **Corruzione file stato**: crash durante salvataggio → scrittura atomica
4. **Visual Studio non risponde**: BeginInvoke prima di Handle creato → inizializzazione anticipata

## 📦 Download e Installazione

Scarica il file `ProxyAI-v1.1.0-win-x64.zip` dagli Assets qui sotto.

**Requisiti:**
- Windows x64
- .NET 10 Runtime (o usa la versione self-contained)
- OneProvider API key

**Quick Start:**
1. Estrai `ProxyAI.exe`
2. Avvia l'applicazione (apparirà nel system tray)
3. Click destro sull'icona → Configurazione
4. Inserisci API key e configura gli agenti
5. Connetti Visual Studio a `http://localhost:11435/v1`

## 🔗 Documentazione

- [README.md](README.md) - Guida introduttiva
- [ARCHITECTURE.md](MarkDown/ARCHITECTURE.md) - Architettura tecnica
- [CODE_REFERENCE.md](MarkDown/CODE_REFERENCE.md) - Reference API
- [CHANGELOG.md](CHANGELOG.md) - Storico modifiche completo

## ⚠️ Note di Upgrade da v1.0.0

Questa versione è **retrocompatibile** con la v1.0.0:
- Il file `appsettings.json` esistente viene letto correttamente
- Aggiunto automaticamente il campo `Theme` (default: "light")
- Nessuna modifica breaking agli agenti esistenti

## 🙏 Contributori

Marco Gianesin (@mgiane86)

---

**Full Changelog**: https://github.com/mgiane86/ProxyAI/compare/v1.0.0...v1.1.0
