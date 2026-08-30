using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CursorUsage;

static class UsageClient
{
    private static readonly HttpClient Http = CreateClient();
    private static double? _lastTotalPercent;

    public static async Task<UsageSnapshot> FetchAsync(bool dashboardOpen, CancellationToken token)
    {
        string? cookie = CursorAuth.TryReadSessionCookie();
        if (cookie is null)
            return UsageSnapshot.SignedOut();

        try
        {
            JsonDocument? summaryDoc = await GetJsonAsync("https://cursor.com/api/usage-summary", cookie, origin: false, token);
            PlanPercents percents = ReadPercents(summaryDoc?.RootElement);

            if (!percents.HasTotal)
            {
                JsonDocument? periodDoc = await PostJsonAsync(
                    "https://cursor.com/api/dashboard/get-current-period-usage",
                    cookie,
                    "{}",
                    token);
                percents = Merge(percents, ReadPeriodPercents(periodDoc?.RootElement));
            }

            if (!percents.HasTotal)
            {
                summaryDoc?.Dispose();
                return UsageSnapshot.Unavailable();
            }

            EventCacheState cache = UsageCache.LoadEvents();
            List<UsageEvent> fresh = await FetchNewEventsAsync(cookie, cache.WatermarkMs, token);
            bool activity = fresh.Count > 0;
            long newest = fresh.Count > 0 ? fresh.Max(e => e.TimestampMs) : cache.WatermarkMs;
            UsageCache.Merge(cache, fresh, newest);
            UsageCache.SaveEvents(cache);

            double[] buckets = BuildBuckets(cache.Events, percents.Total);
            TokenTotals tokens = SumRecentTokens(cache.Events);

            var snapshot = new UsageSnapshot
            {
                Status = UsageStatus.Ok,
                TotalPercent = percents.Total,
                AutoPercent = percents.Auto,
                ApiPercent = percents.Api,
                MembershipType = percents.MembershipType,
                CycleStart = percents.CycleStart,
                CycleEnd = percents.CycleEnd,
                OnDemandEnabled = percents.OnDemandEnabled,
                Buckets = buckets,
                InputTokens = tokens.Input,
                OutputTokens = tokens.Output,
                CacheTokens = tokens.Cache,
                FetchedAt = DateTimeOffset.UtcNow,
                HadRecentActivity = activity || dashboardOpen
            };

            _lastTotalPercent = percents.Total;
            summaryDoc?.Dispose();
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return UsageSnapshot.Unavailable();
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CursorUsage/1.0");
        return client;
    }

    private static async Task<JsonDocument?> GetJsonAsync(string url, string cookie, bool origin, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cookie", "WorkosCursorSessionToken=" + cookie);
        if (origin)
            request.Headers.TryAddWithoutValidation("Origin", "https://cursor.com");

        using HttpResponseMessage response = await Http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            return null;

        string body = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument?> PostJsonAsync(string url, string cookie, string json, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Cookie", "WorkosCursorSessionToken=" + cookie);
        request.Headers.TryAddWithoutValidation("Origin", "https://cursor.com");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await Http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            return null;

        string body = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return JsonDocument.Parse(body);
    }

    private static async Task<List<UsageEvent>> FetchNewEventsAsync(string cookie, long watermarkMs, CancellationToken token)
    {
        var found = new List<UsageEvent>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long startMs = watermarkMs > 0
            ? watermarkMs
            : now.AddHours(-2).ToUnixTimeMilliseconds();
        long endMs = now.ToUnixTimeMilliseconds();

        const int pageSize = 100;
        int page = 1;
        bool crossedWatermark = false;

        while (page <= 20)
        {
            string body = JsonSerializer.Serialize(new
            {
                startDate = startMs.ToString(CultureInfo.InvariantCulture),
                endDate = endMs.ToString(CultureInfo.InvariantCulture),
                page,
                pageSize
            });

            using JsonDocument? doc = await PostJsonAsync(
                "https://cursor.com/api/dashboard/get-filtered-usage-events",
                cookie,
                body,
                token);

            if (doc is null)
                break;

            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("usageEventsDisplay", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
                break;

            int count = 0;
            bool sawOlderThanWindow = false;
            foreach (JsonElement el in list.EnumerateArray())
            {
                count++;
                UsageEvent? ev = ParseEvent(el);
                if (ev is null)
                    continue;

                if (ev.TimestampMs < startMs)
                {
                    sawOlderThanWindow = true;
                    continue;
                }

                if (watermarkMs > 0 && ev.TimestampMs <= watermarkMs)
                {
                    crossedWatermark = true;
                    continue;
                }

                found.Add(ev);
            }

            if (count == 0 || sawOlderThanWindow || crossedWatermark || count < pageSize)
                break;

            page++;
        }

        return found;
    }

    private static UsageEvent? ParseEvent(JsonElement el)
    {
        if (!JsonUtil.TryGetInt64(el, "timestamp", out long timestamp) &&
            !TryParseTimestampString(el, out timestamp))
        {
            return null;
        }

        var ev = new UsageEvent
        {
            TimestampMs = timestamp,
            Model = JsonUtil.GetString(el, "model") ?? ""
        };

        JsonUtil.TryGetDouble(el, "requestsCosts", out double requests);
        ev.RequestsCosts = requests;
        JsonUtil.TryGetDouble(el, "chargedCents", out double charged);
        ev.ChargedCents = charged;

        if (el.TryGetProperty("tokenUsage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object)
        {
            JsonUtil.TryGetInt64(usage, "inputTokens", out long input);
            JsonUtil.TryGetInt64(usage, "outputTokens", out long output);
            JsonUtil.TryGetInt64(usage, "cacheReadTokens", out long cacheRead);
            JsonUtil.TryGetInt64(usage, "cacheWriteTokens", out long cacheWrite);
            JsonUtil.TryGetDouble(usage, "totalCents", out double cents);
            ev.InputTokens = input;
            ev.OutputTokens = output;
            ev.CacheReadTokens = cacheRead;
            ev.CacheWriteTokens = cacheWrite;
            if (ev.ChargedCents <= 0)
                ev.ChargedCents = cents;
        }

        return ev;
    }

    private static bool TryParseTimestampString(JsonElement el, out long timestamp)
    {
        timestamp = 0;
        string? raw = JsonUtil.GetString(el, "timestamp");
        return raw is not null && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp);
    }

    private static double[] BuildBuckets(List<UsageEvent> events, double? totalPercent)
    {
        var buckets = new double[Sparkline.BucketCount];
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long windowMs = Sparkline.BucketCount * 60_000L;
        long startMs = now.ToUnixTimeMilliseconds() - windowMs;

        foreach (UsageEvent ev in events)
        {
            if (ev.TimestampMs < startMs)
                continue;

            int index = (int)((ev.TimestampMs - startMs) / 60_000L);
            if (index < 0 || index >= Sparkline.BucketCount)
                continue;

            buckets[index] += ev.Burn;
        }

        if (buckets.All(b => b <= 0) && totalPercent is double current && _lastTotalPercent is double previous)
        {
            double delta = Math.Max(0, current - previous);
            if (delta > 0)
                buckets[^1] = delta;
        }

        return buckets;
    }

    private static TokenTotals SumRecentTokens(List<UsageEvent> events)
    {
        long startMs = DateTimeOffset.UtcNow.AddMinutes(-Sparkline.BucketCount).ToUnixTimeMilliseconds();
        long input = 0, output = 0, cache = 0;
        foreach (UsageEvent ev in events)
        {
            if (ev.TimestampMs < startMs)
                continue;
            input += ev.InputTokens;
            output += ev.OutputTokens;
            cache += ev.CacheReadTokens + ev.CacheWriteTokens;
        }

        return new TokenTotals(input, output, cache);
    }

    private readonly record struct TokenTotals(long Input, long Output, long Cache);

    private readonly record struct PlanPercents(
        double? Total,
        double? Auto,
        double? Api,
        string? MembershipType,
        DateTimeOffset? CycleStart,
        DateTimeOffset? CycleEnd,
        bool? OnDemandEnabled)
    {
        public bool HasTotal => Total is double t && !double.IsNaN(t);
    }

    private static PlanPercents ReadPercents(JsonElement? root)
    {
        if (root is not JsonElement obj || obj.ValueKind != JsonValueKind.Object)
            return default;

        string? membership = JsonUtil.GetString(obj, "membershipType");
        DateTimeOffset? start = JsonUtil.GetDate(obj, "billingCycleStart");
        DateTimeOffset? end = JsonUtil.GetDate(obj, "billingCycleEnd");
        bool? onDemand = null;
        double? total = null, auto = null, api = null;

        if (obj.TryGetProperty("individualUsage", out JsonElement individual) && individual.ValueKind == JsonValueKind.Object)
        {
            if (individual.TryGetProperty("plan", out JsonElement plan) && plan.ValueKind == JsonValueKind.Object)
            {
                if (JsonUtil.TryGetDouble(plan, "totalPercentUsed", out double t)) total = t;
                if (JsonUtil.TryGetDouble(plan, "autoPercentUsed", out double a)) auto = a;
                if (JsonUtil.TryGetDouble(plan, "apiPercentUsed", out double p)) api = p;
            }

            if (individual.TryGetProperty("onDemand", out JsonElement od) && od.ValueKind == JsonValueKind.Object
                && JsonUtil.TryGetBool(od, "enabled", out bool enabled))
            {
                onDemand = enabled;
            }
        }

        if (total is null)
        {
            string? totalMessage = JsonUtil.GetString(obj, "autoModelSelectedDisplayMessage");
            if (totalMessage is not null
                && totalMessage.Contains("included total", StringComparison.OrdinalIgnoreCase))
            {
                total = ParsePercentMessage(totalMessage);
            }
        }

        return new PlanPercents(total, auto, api, membership, start, end, onDemand);
    }

    private static PlanPercents ReadPeriodPercents(JsonElement? root)
    {
        if (root is not JsonElement obj || obj.ValueKind != JsonValueKind.Object)
            return default;

        JsonElement plan = obj;
        if (obj.TryGetProperty("planUsage", out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
            plan = nested;

        double? total = JsonUtil.TryGetDouble(plan, "totalPercentUsed", out double t) ? t : null;
        double? auto = JsonUtil.TryGetDouble(plan, "autoPercentUsed", out double a) ? a : null;
        double? api = JsonUtil.TryGetDouble(plan, "apiPercentUsed", out double p) ? p : null;
        DateTimeOffset? start = JsonUtil.GetDate(obj, "billingCycleStart");
        DateTimeOffset? end = JsonUtil.GetDate(obj, "billingCycleEnd");
        return new PlanPercents(total, auto, api, null, start, end, null);
    }

    private static PlanPercents Merge(PlanPercents a, PlanPercents b) => new(
        a.Total ?? b.Total,
        a.Auto ?? b.Auto,
        a.Api ?? b.Api,
        a.MembershipType ?? b.MembershipType,
        a.CycleStart ?? b.CycleStart,
        a.CycleEnd ?? b.CycleEnd,
        a.OnDemandEnabled ?? b.OnDemandEnabled);

    private static double? ParsePercentMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        Match match = Regex.Match(message, @"(\d+(?:\.\d+)?)\s*%");
        if (!match.Success)
            return null;

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }
}
