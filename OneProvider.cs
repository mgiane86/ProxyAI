using System;
using System.Text.Json.Serialization;

namespace OneProviderProxy;

public class OneProviderBalance
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "";

    [JsonPropertyName("balance_usd")]
    public decimal BalanceUsd { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }
}