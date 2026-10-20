namespace OneProviderProxy;

/// <summary>
/// Un "agente" è ciò che Visual Studio vede come modello.
/// Name     = l'ID che scrivi in Visual Studio (es. "revisore-codice")
/// ModelId  = il modello reale di OneProvider a cui l'agente è collegato (es. "claude-opus-5-5")
/// Markdown = istruzioni tue, iniettate come system prompt. Vuoto = nessuna istruzione aggiunta.
/// </summary>
public class AgentProfile
{
    public string Name { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Markdown { get; set; } = "";

    // 0 = nessun limite applicato
    public int MaxInputTokens { get; set; }
    public int MaxOutputTokens { get; set; }

    // true = il campo viene lasciato passare, false = viene rimosso dal body
    public bool SupportsTools { get; set; } = true;
    public bool SupportsVision { get; set; } = true;

    // true = il markdown viene inviato come blocco con cache_control (prompt caching).
    // Riduce il costo del system prompt ripetuto a ogni richiesta, ma non tutti i
    // modelli accettano il campo: in caso di 400 il proxy riprova senza.
    public bool UsePromptCaching { get; set; }

    // Serve alla ComboBox: mostra il nome dell'agente nell'elenco
    public override string ToString() => Name;

    // Copia indipendente: la finestra delle impostazioni lavora su copie
    // e le applica solo quando premi "Salva e Riavvia"
    public AgentProfile Clone() => (AgentProfile)MemberwiseClone();
}
