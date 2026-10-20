using System;
using System.Collections.Generic;
using System.Globalization;

namespace OneProviderProxy;

public static class Loc
{
    public static string CurrentLanguage { get; set; } = "en";

    static Loc()
    {
        // Se la lingua di sistema è l'italiano imposta 'it', altrimenti default 'en'
        var sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        CurrentLanguage = (sysLang == "it") ? "it" : "en";
    }

    private static readonly Dictionary<string, (string En, string It)> Strings = new()
    {
        // Settings Form - Connection Group
        ["Title"] = ("OneProvider Proxy - Configuration & Presets", "OneProvider Proxy - Configurazione & Preset Modelli"),
        ["GrpConn"] = ("Connection Parameters & Balance", "Parametri di Connessione & Saldo OneProvider"),
        ["ApiKey"] = ("OneProvider API Key (sk-...):", "OneProvider API Key (sk-...):"),
        ["ShowKey"] = ("Show key", "Mostra chiave"),
        ["TargetBaseUrl"] = ("Target Base URL:", "Target Base URL:"),
        ["LocalPort"] = ("Local Port:", "Porta Locale:"),
        ["RunStartup"] = ("Start automatically with Windows", "Avvia automaticamente con Windows"),
        ["CheckBalance"] = ("Check Balance", "Verifica Saldo"),
        ["BalanceChecking"] = ("Checking balance...", "Interrogazione saldo..."),
        ["BalanceAvailable"] = ("Available: ${0:F2} USD ({1})", "Disponibile: ${0:F2} USD ({1})"),
        ["StatusActive"] = ("Active", "Attiva"),
        ["StatusDisabled"] = ("Disabled", "Disattivata"),
        ["KeyExpiry"] = ("Key Expiry: {0}", "Scadenza chiave: {0}"),
        ["NoExpiry"] = ("No expiration", "Nessuna scadenza"),
        ["InvalidKey"] = ("Invalid API Key (401/403)", "Chiave API non valida (401/403)"),
        ["RateLimit"] = ("Balance rate limit exceeded (429)", "Rate limit superato per il saldo (429)"),
        ["ConnError"] = ("Connection error", "Errore di connessione"),
        ["Language"] = ("Language / Lingua:", "Lingua / Language:"),

        // Settings Form - Presets Group
        ["GrpModels"] = ("Agents (shown as models in Visual Studio)", "Agenti (mostrati come modelli in Visual Studio)"),
        ["Agent"] = ("Agent:", "Agente:"),
        ["NewAgent"] = ("New", "Nuovo"),
        ["DeleteAgent"] = ("Delete", "Elimina"),
        ["AgentName"] = ("Agent name (this is the Model ID used in Visual Studio):", "Nome agente (è l'ID modello da usare in Visual Studio):"),
        ["Markdown"] = ("Instructions (Markdown) - empty = none:", "Istruzioni (Markdown) - vuoto = nessuna:"),
        ["LoadFile"] = ("Load .md...", "Carica .md..."),
        ["ClearMd"] = ("Clear", "Svuota"),
        ["AgentNameInvalid"] = ("Every agent needs a unique, non-empty name.", "Ogni agente deve avere un nome unico e non vuoto."),
        ["ConfirmDelete"] = ("Delete agent '{0}'?", "Eliminare l'agente '{0}'?"),
        ["Model"] = ("Model:", "Modello:"),
        ["CopyId"] = ("Copy ID", "Copia ID"),
        ["CopyPreset"] = ("Copy Preset", "Copia Preset"),
        ["ContextLimit"] = ("• Context Window (Left): {0:N0} tokens", "• Limite Contesto (Sinistra): {0:N0} token"),
        ["OutputLimit"] = ("• Max Output (Right): {0:N0} tokens", "• Limite Max Output (Destra): {0:N0} token"),
        ["Features"] = ("• Flags: [{0}] Tool calling  |  [{1}] Image support", "• Spunte: [{0}] Chiamata strumenti  |  [{1}] Supporto immagini"),
        ["MaxInput"] = ("Max input tokens", "Token input max"),
        ["MaxOutput"] = ("Max output tokens", "Token output max"),
        ["SupportsTools"] = ("Tool calling", "Chiamata strumenti"),
        ["SupportsVision"] = ("Image support", "Supporto immagini"),
        ["UsePromptCaching"] = ("Prompt caching (cheaper repeated prompt)", "Prompt caching (prompt ripetuto più economico)"),
        ["Refresh"] = ("Refresh", "Aggiorna"),
        ["SelectModel"] = ("Select Model", "Seleziona Modello"),
        ["SearchModel"] = ("Search (name or ID):", "Cerca (nome o ID):"),
        ["SelectedModel"] = ("Selected: {0}", "Selezionato: {0}"),
        ["NoModelSelected"] = ("(no model selected)", "(nessun modello selezionato)"),
        ["RefreshModels"] = ("Refresh from API", "Aggiorna da API"),
        ["Cancel"] = ("Cancel", "Annulla"),
        ["OtherModels"] = ("Other Models", "Altri Modelli"),
        ["Yes"] = ("Yes", "Sì"),
        ["No"] = ("No", "No"),

        // Guide Group
        ["GrpGuide"] = ("Visual Studio Setup Guide (Bring Your Own Model)", "Come configurare in Visual Studio (Bring Your Own Model)"),
        ["GuideStep1"] = ("1. In Visual Studio open: Copilot > Manage models > Bring Your Own Model.", "1. In Visual Studio vai su: Copilot > Gestisci modelli > Bring Your Own Model."),
        ["GuideStep2"] = ("2. Provider: select 'Ollama'.", "2. Provider: seleziona 'Ollama'."),
        ["GuideStep3"] = ("3. Resource endpoint: http://localhost:{0}/v1", "3. Endpoint risorsa: http://localhost:{0}/v1"),
        ["GuideStep4"] = ("4. Model ID: pick an agent above and click 'Copy ID'.", "4. ID modello: seleziona l'agente sopra e clicca 'Copia ID'."),
        ["GuideStep5"] = ("5. Token limits: set values suggested above (Left: Context, Right: Output).", "5. Limite token: imposta i valori suggeriti sopra (Sinistra: Contesto, Destra: Output)."),
        ["GuideStep6"] = ("6. Check recommended feature boxes, click 'Add' and 'Save'.", "6. Spunta le funzionalità consigliate (Strumenti/Immagini), clicca 'Aggiungi' e poi 'Salva'."),
        ["GuideCache"] = ("Tip: enable 'Prompt caching' to pay the big markdown prompt once, then ~10% on every reuse.", "Suggerimento: attiva 'Prompt caching' per pagare il markdown una volta sola e poi ~10% a ogni riuso."),

        // Buttons & Messages
        ["SaveRestart"] = ("Save & Restart", "Salva e Riavvia"),
        ["SavedMsg"] = ("Configuration saved successfully!", "Configurazione salvata con successo!"),
        ["IdCopied"] = ("Model ID copied to clipboard:\n\n{0}", "ID Modello copiato negli appunti:\n\n{0}"),
        ["PresetCopied"] = ("Recommended parameters copied to clipboard:\n\n{0}", "Parametri consigliati copiati negli appunti:\n\n{0}"),
        ["CopiedTitle"] = ("Copied", "Copiato"),

        // Tray & Balloon
        ["TraySettings"] = ("Configuration...", "Configurazione..."),
        ["TrayLog"] = ("Show Log...", "Mostra Log..."),
        ["TrayExit"] = ("Exit", "Esci"),
        ["BalloonTitle"] = ("OneProvider Proxy Active", "OneProvider Proxy Attivo"),
        ["BalloonText"] = ("Listening on http://localhost:{0}/\nHover over the icon to view tokens and balance.", "In ascolto su http://localhost:{0}/\nPassa sull'icona per visualizzare token e saldo."),
        ["TipBalance"] = ("Bal", "Saldo"),
        ["TipToday"] = ("Today", "Oggi"),

        // Log Form
        ["LogTitle"] = ("OneProvider Proxy - Activity Log", "OneProvider Proxy - Monitor Chiamate"),
        ["LogClear"] = ("Clear Log", "Cancella Log")
    };

    public static string T(string key, params object[] args)
    {
        if (!Strings.TryGetValue(key, out var val))
            return key;

        var pattern = (CurrentLanguage == "it") ? val.It : val.En;
        return args.Length > 0 ? string.Format(pattern, args) : pattern;
    }
}