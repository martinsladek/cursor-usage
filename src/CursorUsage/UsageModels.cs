using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CursorUsage;

enum UsageStatus
{
    Ok,
    SignedOut,
    Unavailable
}

sealed class UsageSnapshot
{
    public UsageStatus Status { get; init; } = UsageStatus.Unavailable;
    public double? TotalPercent { get; init; }
    public double? AutoPercent { get; init; }
    public double? ApiPercent { get; init; }
    public string? MembershipType { get; init; }
    public DateTimeOffset? CycleStart { get; init; }
    public DateTimeOffset? CycleEnd { get; init; }
    public bool? OnDemandEnabled { get; init; }
    public double[] Buckets { get; init; } = new double[Sparkline.BucketCount];
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long CacheTokens { get; init; }
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool HadRecentActivity { get; init; }

    [JsonIgnore]
    public bool HasIncludedPercent => TotalPercent is double t && !double.IsNaN(t);

    public static UsageSnapshot SignedOut() => new() { Status = UsageStatus.SignedOut };

    public static UsageSnapshot Unavailable() => new() { Status = UsageStatus.Unavailable };
}

sealed class UsageEvent
{
    public long TimestampMs { get; set; }
    public string Model { get; set; } = "";
    public double RequestsCosts { get; set; }
    public double ChargedCents { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long CacheWriteTokens { get; set; }

    public string Identity =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{TimestampMs}|{Model}|{InputTokens}|{OutputTokens}|{CacheReadTokens}|{CacheWriteTokens}|{ChargedCents:G17}|{RequestsCosts:G17}");

    public double Burn =>
        RequestsCosts > 0 ? RequestsCosts
        : ChargedCents > 0 ? ChargedCents
        : 1;
}

sealed class EventCacheState
{
    public long WatermarkMs { get; set; }
    public List<UsageEvent> Events { get; set; } = [];
}

static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static bool TryGetDouble(JsonElement obj, string name, out double value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out JsonElement el))
            return false;

        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetDouble(out value);
            case JsonValueKind.String:
                return double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    public static bool TryGetInt64(JsonElement obj, string name, out long value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out JsonElement el))
            return false;

        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetInt64(out value);
            case JsonValueKind.String:
                return long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    public static bool TryGetBool(JsonElement obj, string name, out bool value)
    {
        value = false;
        if (!obj.TryGetProperty(name, out JsonElement el))
            return false;

        if (el.ValueKind == JsonValueKind.True) { value = true; return true; }
        if (el.ValueKind == JsonValueKind.False) { value = false; return true; }
        return false;
    }

    public static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    public static DateTimeOffset? GetDate(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement el))
            return null;

        if (el.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(el.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dto))
        {
            return dto;
        }

        if (TryGetInt64(obj, name, out long ms) && ms > 1_000_000_000_000)
            return DateTimeOffset.FromUnixTimeMilliseconds(ms);

        if (TryGetInt64(obj, name, out long sec) && sec > 1_000_000_000)
            return DateTimeOffset.FromUnixTimeSeconds(sec);

        return null;
    }
}
