using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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
        _client = new HttpClient();
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

            if (context.Request.HasEntityBody)
            {
                var streamContent = new StreamContent(context.Request.InputStream);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                    streamContent.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
                req.Content = streamContent;
            }

            using var response = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);

            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = string.Join(",", header.Value);

            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = string.Join(",", header.Value);

            context.Response.Headers.Remove("Transfer-Encoding");

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

                var errorBytes = Encoding.UTF8.GetBytes(bodyText);
                await context.Response.OutputStream.WriteAsync(errorBytes, token);
                return;
            }

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

            if (inTok > 0 || outTok > 0)
            {
                decimal estCost = ((inTok * 1.5m) + (outTok * 5.0m)) / 1_000_000m;
                UsageTracker.RecordUsage(inTok, outTok, estCost);
            }

            var logMsg = $"{method} {path} -> {(int)response.StatusCode} {response.StatusCode} ({sw.ElapsedMilliseconds} ms)";
            if (inTok > 0 || outTok > 0)
            {
                logMsg += $" | Tokens: In {inTok}, Out {outTok}";
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

    public void Dispose()
    {
        Stop();
        _client.Dispose();
        _cts?.Dispose();
    }
}