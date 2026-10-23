using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OneProviderProxy;

public class ProxyServer : IDisposable
{
    private HttpListener? _listener;
    private readonly HttpClient _client;
    private CancellationTokenSource? _cts;
    private readonly AppSettings _settings;

    public event Action<string, Color>? OnLog;

    public bool IsRunning => _listener?.IsListening ?? false;

    public ProxyServer(AppSettings settings)
    {
        _settings = settings;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_settings.Port}/");
        _listener.Start();

        OnLog?.Invoke($"Server avviato su http://localhost:{_settings.Port}/", Color.LightGreen);

        Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        OnLog?.Invoke("Server proxy arrestato.", Color.Orange);
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context, token), token);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.HttpMethod;
        var path = context.Request.RawUrl ?? "/";

        try
        {
            // Se Visual Studio chiede l'elenco dei modelli, rispondiamo noi con gli AGENTI
            // (senza interrogare OneProvider)
            if (method == "GET" && _settings.Agents.Count > 0)
            {
                var cleanPath = (context.Request.Url?.AbsolutePath ?? "/").TrimEnd('/');
                if (cleanPath.Equals("/v1/models", StringComparison.OrdinalIgnoreCase) ||
                    cleanPath.Equals("/api/tags", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteAgentListAsync(context, ollamaFormat: cleanPath.EndsWith("/api/tags", StringComparison.OrdinalIgnoreCase), token);
                    OnLog?.Invoke($"{method} {path} -> elenco agenti ({_settings.Agents.Count})", Color.LimeGreen);
                    return;
                }
            }

            // /api/show: Visual Studio (provider Ollama) chiede i dettagli di un modello.
            // OneProvider non ha questo endpoint, quindi rispondiamo noi.
            if (method == "POST" &&
                (context.Request.Url?.AbsolutePath ?? "").TrimEnd('/').Equals("/api/show", StringComparison.OrdinalIgnoreCase))
            {
                await WriteShowResponseAsync(context, token);
                OnLog?.Invoke($"{method} {path} -> dettagli agente (risposta locale)", Color.LimeGreen);
                return;
            }

            var targetUri = new Uri(new Uri(_settings.TargetEndpoint), path);
            var req = new HttpRequestMessage(new HttpMethod(method), targetUri);

            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ApiKey}");
            }

            foreach (string headerName in context.Request.Headers)
            {
                if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                    headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                    headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    continue;

                req.Headers.TryAddWithoutValidation(headerName, context.Request.Headers[headerName]);
            }

            string? agentLog = null;
            bool cachingRequested = false;
            byte[]? rawBodyBytes = null;

            if (context.Request.HasEntityBody)
            {
                // Leggiamo tutto il body in memoria: ci serve per poter modificare il JSON
                using var bodyMs = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(bodyMs, token);
                rawBodyBytes = bodyMs.ToArray();

                // Sostituisce il nome dell'agente col modello reale e aggiunge il markdown
                var bodyBytes = ApplyAgent(rawBodyBytes, out agentLog, out cachingRequested,
                                           anthropicNative: IsAnthropicNativePath(path));

                var content = new ByteArrayContent(bodyBytes);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                    content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
                req.Content = content;
            }

            if (agentLog != null)
                OnLog?.Invoke(agentLog, Color.DeepSkyBlue);

            HttpResponseMessage? response = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
            HttpRequestMessage? retryReq = null;

            // Se il modello non accetta cache_control risponde 400: riproviamo senza caching.
            if (cachingRequested && (int)response.StatusCode == 400 && rawBodyBytes != null)
            {
                var probe = await response.Content.ReadAsStringAsync(token);
                response.Dispose();

                if (!probe.Contains("cache_control", StringComparison.OrdinalIgnoreCase))
                {
                    // 400 non legato al caching: lo restituiamo così com'è
                    context.Response.StatusCode = 400;
                    var errBytes = Encoding.UTF8.GetBytes(probe);
                    context.Response.ContentLength64 = errBytes.Length;
                    await context.Response.OutputStream.WriteAsync(errBytes, token);
                    return;
                }

                OnLog?.Invoke("[CACHE] Modello senza supporto cache_control: riprovo senza caching.", Color.Orange);

                // Si riparte dal body ORIGINALE del client, così il markdown
                // non viene applicato due volte e cache_control non c'è.
                var plainBody = ApplyAgent(rawBodyBytes, out _, out _, allowCaching: false,
                                           anthropicNative: IsAnthropicNativePath(path));

                retryReq = new HttpRequestMessage(new HttpMethod(method), targetUri);
                if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                    retryReq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ApiKey}");

                foreach (string headerName in context.Request.Headers)
                {
                    if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                        headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                        headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                        continue;

                    retryReq.Headers.TryAddWithoutValidation(headerName, context.Request.Headers[headerName]);
                }

                var retryContent = new ByteArrayContent(plainBody);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                    retryContent.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
                retryReq.Content = retryContent;

                response = await _client.SendAsync(retryReq, HttpCompletionOption.ResponseHeadersRead, token);
            }

            // Dispose differito: response e l'eventuale richiesta di retry vivono fino a fine metodo
            using var _finalResponse = response;
            using var _finalRetryReq = retryReq;

            context.Response.StatusCode = (int)response.StatusCode;

            if (response.Content.Headers.ContentType != null)
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();

            foreach (var header in response.Headers)
            {
                if (IsRestrictedResponseHeader(header.Key)) continue;
                context.Response.Headers[header.Key] = string.Join(",", header.Value);
            }

            foreach (var header in response.Content.Headers)
            {
                if (IsRestrictedResponseHeader(header.Key)) continue;
                context.Response.Headers[header.Key] = string.Join(",", header.Value);
            }

            // Rilevamento esaurimento saldo OneProvider (429 con insufficient_balance / quota)
            if ((int)response.StatusCode == 429)
            {
                var bodyText = await response.Content.ReadAsStringAsync(token);

                if (bodyText.Contains("insufficient_balance") || bodyText.Contains("insufficient_quota"))
                {
                    OnLog?.Invoke("[SALDO ESAURITO] Credito terminato su OneProvider! (429 Insufficient Quota). Ricarica il conto.", Color.Red);
                }
                else
                {
                    OnLog?.Invoke("[RATE LIMIT] 429 Troppe richieste. Attendi il retry.", Color.Orange);
                }

                // Il body viene ricodificato: la lunghezza va ricalcolata, non copiata dall'upstream
                var errorBytes = Encoding.UTF8.GetBytes(bodyText);
                context.Response.ContentLength64 = errorBytes.Length;
                await context.Response.OutputStream.WriteAsync(errorBytes, token);
                return;
            }

            // La lunghezza va impostata tramite la proprietà: assegnarla a Headers["Content-Length"]
            // lascerebbe ContentLength64 non sincronizzato e la scrittura fallirebbe con
            // HttpListenerException (87) "Parametro non corretto". Se l'upstream non la fornisce
            // (risposta chunked) HttpListener applica il chunked encoding da sé.
            if (response.Content.Headers.ContentLength is long upstreamLength)
                context.Response.ContentLength64 = upstreamLength;

            // Inoltro streaming verso il client con cattura payload per metriche token
            await using var responseStream = await response.Content.ReadAsStreamAsync(token);
            using var capturedStream = new MemoryStream();

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await responseStream.ReadAsync(buffer, token)) > 0)
            {
                await context.Response.OutputStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                await context.Response.OutputStream.FlushAsync(token);

                if (capturedStream.Length < 32768)
                {
                    await capturedStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                }
            }

            sw.Stop();

            // Calcolo e memorizzazione delle metriche
            var responseSnippet = Encoding.UTF8.GetString(capturedStream.ToArray());
            var (inTok, outTok) = ExtractTokenCounts(responseSnippet);
            var (cacheRead, cacheWrite) = ExtractCacheTokens(responseSnippet);

            if (inTok > 0 || outTok > 0)
            {
                // I token letti dalla cache costano ~10% della tariffa piena,
                // quelli scritti in cache ~125%.
                decimal estCost = ((inTok * 1.5m) + (outTok * 5.0m)) / 1_000_000m;
                if (cacheRead > 0)
                    estCost -= (cacheRead * 1.5m * 0.9m) / 1_000_000m;
                if (cacheWrite > 0)
                    estCost += (cacheWrite * 1.5m * 0.25m) / 1_000_000m;

                UsageTracker.RecordUsage(inTok, outTok, estCost);
            }

            var logMsg = $"{method} {path} -> {(int)response.StatusCode} {response.StatusCode} ({sw.ElapsedMilliseconds} ms)";
            if (inTok > 0 || outTok > 0)
            {
                logMsg += $" | Tokens: In {inTok}, Out {outTok}";
            }
            if (cacheRead > 0 || cacheWrite > 0)
            {
                logMsg += $" | Cache: read {cacheRead}, write {cacheWrite}";
            }

            OnLog?.Invoke(logMsg, Color.LimeGreen);
        }
        catch (Exception ex)
        {
            sw.Stop();
            context.Response.StatusCode = 502;
            using var writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync($"Proxy Error: {ex.Message}");

            OnLog?.Invoke($"ERRORE {method} {path}: {ex.Message} ({sw.ElapsedMilliseconds} ms)", Color.Red);
        }
        finally
        {
            context.Response.Close();
        }
    }

    /// <summary>
    /// Se il campo "model" del JSON corrisponde a un agente:
    ///  1) lo sostituisce con il modello reale
    ///  2) se l'agente ha un markdown, lo inserisce come messaggio di sistema
    /// Se non c'è corrispondenza (o il body non è JSON) restituisce il body invariato.
    /// </summary>
    /// <param name="cachingRequested">true se è stato inviato cache_control (serve per il fallback su 400).</param>
    /// <param name="allowCaching">false per forzare un nuovo tentativo senza prompt caching.</param>
    /// <param name="anthropicNative">
    /// true solo per /v1/messages: è l'unico formato in cui "content" può essere un array
    /// di blocchi con cache_control. Nel formato [OI] chat/completions il system vuole una stringa.
    /// </param>
    private byte[] ApplyAgent(byte[] body, out string? log, out bool cachingRequested,
                              bool allowCaching = true, bool anthropicNative = false)
    {
        log = null;
        cachingRequested = false;
        try
        {
            if (JsonNode.Parse(body) is not JsonObject obj) return body;
            if (obj["model"] is not JsonValue modelValue || !modelValue.TryGetValue<string>(out var requested))
                return body;

            // Ollama a volte aggiunge ":latest" al nome: lo ignoriamo
            var name = requested.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
                ? requested[..^7]
                : requested;

            var agent = _settings.Agents.FirstOrDefault(a =>
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (agent == null) return body; // nessun agente: passa com'è (comportamento di prima)

            obj["model"] = agent.ModelId;

            bool markdownApplied = false;
            if (!string.IsNullOrWhiteSpace(agent.Markdown) && obj["messages"] is JsonArray messages)
            {
                // Se esiste già un messaggio "system" testuale in testa, ci anteponiamo il nostro markdown;
                // altrimenti creiamo un nuovo messaggio "system" in prima posizione.
                if (messages.Count > 0 &&
                    messages[0] is JsonObject first &&
                    (string?)first["role"] == "system" &&
                    first["content"] is JsonValue contentValue &&
                    contentValue.TryGetValue<string>(out var existing))
                {
                    first["content"] = agent.Markdown + "\n\n" + existing;
                }
                else
                {
                    messages.Insert(0, new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = agent.Markdown
                    });
                }
                markdownApplied = true;
            }

            // Prompt caching: il system prompt identico viene riusato tra le richieste,
            // pagando la tariffa di cache (~10%) invece di quella piena.
            // Il markdown va inviato come blocco di contenuto con cache_control.
            if (allowCaching && anthropicNative && agent.UsePromptCaching &&
                obj["messages"] is JsonArray cacheMessages && cacheMessages.Count > 0 &&
                cacheMessages[0] is JsonObject systemMsg &&
                (string?)systemMsg["role"] == "system" &&
                systemMsg["content"] is JsonValue sysValue &&
                sysValue.TryGetValue<string>(out var systemText) &&
                !string.IsNullOrWhiteSpace(systemText))
            {
                systemMsg["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = systemText,
                        ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
                    }
                };
                cachingRequested = true;
            }

            var adjustments = new List<string>();

            // Limite di output: applicato solo se il client non ne ha già chiesto uno più basso
            if (agent.MaxOutputTokens > 0)
            {
                var requestedMax = GetInt(obj["max_tokens"]) ?? GetInt(obj["max_completion_tokens"]);
                if (requestedMax is null || requestedMax > agent.MaxOutputTokens)
                {
                    obj["max_tokens"] = agent.MaxOutputTokens;
                    adjustments.Add($"max_tokens={agent.MaxOutputTokens}");
                }
            }

            // Limite di input: i token non sono misurabili senza tokenizer, quindi tronchiamo
            // i messaggi più vecchi finché la stima (caratteri / 4) rientra nel budget.
            if (agent.MaxInputTokens > 0 && obj["messages"] is JsonArray msgArray)
            {
                var trimmed = TrimMessagesToBudget(msgArray, agent.MaxInputTokens);
                if (trimmed > 0) adjustments.Add($"-{trimmed} msg (budget {agent.MaxInputTokens})");
            }

            // Strumenti: se l'agente non li supporta, il campo viene rimosso dal body
            if (!agent.SupportsTools)
            {
                if (obj.Remove("tools")) adjustments.Add("-tools");
                if (obj.Remove("tool_choice")) adjustments.Add("-tool_choice");
                if (obj.Remove("parallel_tool_calls")) adjustments.Add("-parallel_tool_calls");
            }

            // Immagini: se l'agente non le supporta, i content part "image_url" vengono rimossi
            if (!agent.SupportsVision && obj["messages"] is JsonArray visionMessages)
            {
                int removed = RemoveImageParts(visionMessages);
                if (removed > 0) adjustments.Add($"-{removed} img");
            }

            log = $"Agente '{agent.Name}' -> {agent.ModelId}" +
                  (markdownApplied ? " (+markdown)" : "") +
                  (cachingRequested ? " (cache)" : "") +
                  (adjustments.Count > 0 ? $" [{string.Join(", ", adjustments)}]" : "");
            return JsonSerializer.SerializeToUtf8Bytes(obj);
        }
        catch
        {
            return body; // in caso di dubbio non tocchiamo nulla
        }
    }

    private static int? GetInt(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<int>(out var i)) return i;
        if (value.TryGetValue<long>(out var l) && l <= int.MaxValue) return (int)l;
        return null;
    }

    /// <summary>Stima grezza dei token: ~4 caratteri per token.</summary>
    private static long EstimateTokens(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    /// <summary>
    /// true solo per /v1/messages (formato Anthropic nativo), l'unico in cui
    /// "content" può essere un array di blocchi con cache_control.
    /// </summary>
    private static bool IsAnthropicNativePath(string path) =>
        path.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rimuove i turni più vecchi finché la stima dei token rientra nel budget.
    /// Restituisce quanti messaggi ha rimosso.
    ///
    /// Un "turno" parte da un messaggio "user" e comprende tutto ciò che segue fino
    /// al "user" successivo (assistant, tool_calls, risposte "tool"). Si rimuovono
    /// turni interi perché tagliare a metà lascerebbe messaggi "tool" orfani o
    /// risposte senza la domanda, e l'upstream risponde 400.
    ///
    /// Il messaggio "system" (prompt dell'agente) e l'ultimo turno non vengono mai toccati.
    /// </summary>
    private static int TrimMessagesToBudget(JsonArray messages, int budgetTokens)
    {
        long Total()
        {
            long sum = 0;
            foreach (var m in messages)
                if (m is JsonObject o)
                    sum += EstimateTokens(o.ToJsonString());
            return sum;
        }

        static bool IsUser(JsonNode? n) => n is JsonObject o && (string?)o["role"] == "user";

        int removed = 0;

        while (Total() > budgetTokens)
        {
            // Primo "user" dopo l'eventuale "system" in testa
            int start = -1;
            for (int i = 0; i < messages.Count; i++)
            {
                if (IsUser(messages[i])) { start = i; break; }
            }
            if (start < 0) break;

            // Fine del turno = "user" successivo (o fine array)
            int end = messages.Count;
            for (int i = start + 1; i < messages.Count; i++)
            {
                if (IsUser(messages[i])) { end = i; break; }
            }

            // Non rimuovere l'ultimo turno: serve per formulare la risposta
            if (end >= messages.Count) break;

            for (int i = end - 1; i >= start; i--)
            {
                messages.RemoveAt(i);
                removed++;
            }
        }

        // Rete di sicurezza: il primo messaggio non-system deve essere "user"
        while (messages.Count > 1 && messages[0] is JsonObject head &&
               (string?)head["role"] is not ("system" or "user"))
        {
            messages.RemoveAt(0);
            removed++;
        }

        return removed;
    }

    /// <summary>Rimuove i content part di tipo immagine. Restituisce quanti ne ha rimossi.</summary>
    private static int RemoveImageParts(JsonArray messages)
    {
        int removed = 0;
        foreach (var m in messages)
        {
            if (m is not JsonObject o || o["content"] is not JsonArray parts) continue;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i] is JsonObject part &&
                    string.Equals((string?)part["type"], "image_url", StringComparison.OrdinalIgnoreCase))
                {
                    parts.RemoveAt(i);
                    removed++;
                }
            }
        }
        return removed;
    }

    /// <summary>Risponde a POST /api/show con i dettagli (finti ma validi) dell'agente richiesto.</summary>
    private async Task WriteShowResponseAsync(HttpListenerContext context, CancellationToken token)
    {
        // Leggiamo il body per sapere quale agente viene chiesto ({"model":"..."} oppure {"name":"..."})
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var bodyText = await reader.ReadToEndAsync(token);

        string? requested = null;
        try
        {
            if (JsonNode.Parse(bodyText) is JsonObject obj)
                requested = (string?)(obj["model"] ?? obj["name"]);
        }
        catch { }

        var name = requested ?? "";
        if (name.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)) name = name[..^7];

        var agent = _settings.Agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        object payload;
        int status;
        if (agent == null)
        {
            status = 404;
            payload = new { error = $"model '{requested}' not found" };
        }
        else
        {
            status = 200;

            // Le capabilities rispecchiano le spunte dell'agente, così Visual Studio
            // abilita/disabilita strumenti e immagini di conseguenza.
            var capabilities = new List<string> { "completion" };
            if (agent.SupportsTools) capabilities.Add("tools");
            if (agent.SupportsVision) capabilities.Add("vision");

            var contextLength = agent.MaxInputTokens > 0 ? agent.MaxInputTokens : 128000;

            payload = new
            {
                modelfile = "",
                parameters = "",
                template = "",
                details = new
                {
                    format = "gguf",
                    family = "proxy",
                    families = new[] { "proxy" },
                    parameter_size = "unknown",
                    quantization_level = "none"
                },
                model_info = new Dictionary<string, object>
                {
                    ["general.architecture"] = "proxy",
                    ["proxy.context_length"] = contextLength
                },
                capabilities
            };
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, token);
    }

    /// <summary>Risponde a /v1/models (formato OpenAI) o /api/tags (formato Ollama) elencando gli agenti.</summary>
    private async Task WriteAgentListAsync(HttpListenerContext context, bool ollamaFormat, CancellationToken token)
    {
        object payload = ollamaFormat
            ? new
            {
                models = _settings.Agents.Select(a => new
                {
                    name = a.Name,
                    model = a.Name,
                    modified_at = DateTime.UtcNow.ToString("o"),
                    size = 0,
                    digest = "",
                    details = new { }
                })
            }
            : new
            {
                @object = "list",
                data = _settings.Agents.Select(a => new
                {
                    id = a.Name,
                    @object = "model",
                    created = 0,
                    owned_by = "oneprovider-proxy"
                })
            };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, token);
    }

    /// <summary>
    /// Header che HttpListener/HTTP.sys gestiscono autonomamente: copiarli dalla risposta
    /// upstream produce uno stato incoerente (es. Content-Length in Headers con
    /// ContentLength64 non aggiornato) e la scrittura fallisce con errore 87.
    /// </summary>
    private static bool IsRestrictedResponseHeader(string name) =>
        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);

    private static (long inTokens, long outTokens) ExtractTokenCounts(string text)
    {
        long inT = 0, outT = 0;

        var pMatch = Regex.Match(text, "\"prompt_tokens\"\\s*:\\s*(\\d+)");
        var cMatch = Regex.Match(text, "\"completion_tokens\"\\s*:\\s*(\\d+)");
        if (pMatch.Success) long.TryParse(pMatch.Groups[1].Value, out inT);
        if (cMatch.Success) long.TryParse(cMatch.Groups[1].Value, out outT);

        if (inT == 0 && outT == 0)
        {
            var inMatch = Regex.Match(text, "\"input_tokens\"\\s*:\\s*(\\d+)");
            var outMatch = Regex.Match(text, "\"output_tokens\"\\s*:\\s*(\\d+)");
            if (inMatch.Success) long.TryParse(inMatch.Groups[1].Value, out inT);
            if (outMatch.Success) long.TryParse(outMatch.Groups[1].Value, out outT);
        }

        return (inT, outT);
    }

    /// <summary>
    /// Legge dalla risposta i token serviti dalla cache e quelli scritti in cache.
    /// Anthropic usa cache_read_input_tokens / cache_creation_input_tokens,
    /// [OI] usa prompt_tokens_details.cached_tokens.
    /// </summary>
    private static (long cacheRead, long cacheWrite) ExtractCacheTokens(string text)
    {
        long read = 0, write = 0;

        var r = Regex.Match(text, "\"cache_read_input_tokens\"\\s*:\\s*(\\d+)");
        if (r.Success) long.TryParse(r.Groups[1].Value, out read);

        var w = Regex.Match(text, "\"cache_creation_input_tokens\"\\s*:\\s*(\\d+)");
        if (w.Success) long.TryParse(w.Groups[1].Value, out write);

        if (read == 0)
        {
            var c = Regex.Match(text, "\"cached_tokens\"\\s*:\\s*(\\d+)");
            if (c.Success) long.TryParse(c.Groups[1].Value, out read);
        }

        return (read, write);
    }

    public void Dispose()
    {
        Stop();
        _client.Dispose();
        _cts?.Dispose();
    }
}