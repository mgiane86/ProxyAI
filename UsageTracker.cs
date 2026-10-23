using System;
using System.IO;
using System.Text.Json;

namespace OneProviderProxy;

public class DailyUsageData
{
    public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal? LastKnownBalanceUsd { get; set; }
}

public static class UsageTracker
{
    private static readonly object _lock = new();
    private static DailyUsageData _current = new();
    private static readonly string StatsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_stats.json");

    static UsageTracker()
    {
        Load();
        EnsureToday();
    }

    private static void EnsureToday()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (_current.Date != today)
        {
            var oldBalance = _current.LastKnownBalanceUsd;
            _current = new DailyUsageData
            {
                Date = today,
                LastKnownBalanceUsd = oldBalance
            };
            Save();
        }
    }

    public static void RecordUsage(long inputTokens, long outputTokens, decimal costEstimate = 0m)
    {
        lock (_lock)
        {
            EnsureToday();
            _current.InputTokens += inputTokens;
            _current.OutputTokens += outputTokens;
            _current.EstimatedCostUsd += costEstimate;
            Save();
        }
    }

    public static void UpdateBalance(decimal balanceUsd)
    {
        lock (_lock)
        {
            _current.LastKnownBalanceUsd = balanceUsd;
            Save();
        }
    }

    public static (long inTokens, long outTokens, decimal? balance, decimal cost) GetSnapshot()
    {
        lock (_lock)
        {
            EnsureToday();
            return (_current.InputTokens, _current.OutputTokens, _current.LastKnownBalanceUsd, _current.EstimatedCostUsd);
        }
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(StatsPath))
            {
                var json = File.ReadAllText(StatsPath);
                _current = JsonSerializer.Deserialize<DailyUsageData>(json) ?? new DailyUsageData();
            }
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });

            // Scrittura atomica: temp file + rename per evitare corruzione in caso di crash
            var tempPath = StatsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, StatsPath, overwrite: true);
        }
        catch { }
    }

    public static string FormatCompactNumber(long num)
    {
        if (num >= 1_000_000) return $"{(num / 1_000_000.0):0.#}M";
        if (num >= 1_000) return $"{(num / 1_000.0):0.#}k";
        return num.ToString();
    }
}