using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace OneProviderProxy;

/// <summary>Modello così come lo restituisce /v1/models di OneProvider.</summary>
public class OneProviderModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

/// <summary>Risposta di /v1/models (formato [OI]: {"object":"list","data":[...]}).</summary>
public class OneProviderModelList
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("data")]
    public List<OneProviderModel> Data { get; set; } = new();
}

/// <summary>
/// Recupera l'elenco dei modelli da OneProvider e lo tiene in cache su disco,
/// così la finestra delle impostazioni resta usabile anche senza rete.
/// </summary>
public static class ModelCatalog
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly string CachePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "models_cache.json");

    private static readonly JsonSerializerOptions CacheOptions = new() { WriteIndented = true };

    /// <summary>Ultimo elenco ottenuto (memoria o cache su disco).</summary>
    public static List<OneProviderModel> Cached { get; private set; } = new();

    static ModelCatalog() => LoadCache();

    /// <summary>
    /// Scarica l'elenco dei modelli dall'endpoint. In caso di errore restituisce
    /// l'ultima cache disponibile (che può essere vuota).
    /// </summary>
    public static async Task<List<OneProviderModel>> FetchAsync(
        string baseUrl,
        string apiKey,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Cached;

        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/v1/models";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey.Trim()}");

            using var res = await Client.SendAsync(req, token);
            if (!res.IsSuccessStatusCode)
                return Cached;

            var json = await res.Content.ReadAsStringAsync(token);
            var parsed = JsonSerializer.Deserialize<OneProviderModelList>(json);

            var models = parsed?.Data?
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .ToList() ?? new List<OneProviderModel>();

            if (models.Count == 0)
                return Cached;

            Cached = models;
            SaveCache(models);
            return models;
        }
        catch
        {
            return Cached;
        }
    }

    private static void LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return;
            var json = File.ReadAllText(CachePath);
            Cached = JsonSerializer.Deserialize<List<OneProviderModel>>(json) ?? new();
        }
        catch
        {
            Cached = new();
        }
    }

    private static void SaveCache(List<OneProviderModel> models)
    {
        try
        {
            File.WriteAllText(CachePath, JsonSerializer.Serialize(models, CacheOptions));
        }
        catch { }
    }
}
