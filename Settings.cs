using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OneProviderProxy;

public class AppSettings
{
    public string ApiKey { get; set; } = "";
    public string TargetEndpoint { get; set; } = "https://api.oneprovider.dev";
    public int Port { get; set; } = 11434;
    public string Language { get; set; } = "";
    public List<AgentProfile> Agents { get; set; } = new();

    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                loaded.Agents ??= new();
                loaded.SeedDefaultAgentsIfEmpty();
                return loaded;
            }
        }
        catch { }
        var fresh = new AppSettings();
        fresh.SeedDefaultAgentsIfEmpty();
        return fresh;
    }

    // Al primo avvio dopo l'aggiornamento (appsettings.json senza "Agents")
    // crea tre agenti d'esempio, così Visual Studio ha subito qualcosa da usare.
    private void SeedDefaultAgentsIfEmpty()
    {
        if (Agents.Count > 0) return;
        Agents.Add(new AgentProfile { Name = "architect", ModelId = "claude-fable-5-1" });
        Agents.Add(new AgentProfile { Name = "coder", ModelId = "claude-opus-5-5" });
        Agents.Add(new AgentProfile { Name = "assistant", ModelId = "claude-sonnet-5-5" });
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}