using System;
using System.IO;
using System.Text.Json;

namespace OneProviderProxy;

public class AppSettings
{
    public string ApiKey { get; set; } = "";
    public string TargetEndpoint { get; set; } = "https://api.oneprovider.dev";
    public int Port { get; set; } = 11434;
    public string Language { get; set; } = "";

    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
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