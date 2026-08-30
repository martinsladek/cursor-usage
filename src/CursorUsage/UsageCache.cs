using System.Text.Json;

namespace CursorUsage;

static class UsageCache
{
    public static EventCacheState LoadEvents()
    {
        try
        {
            string path = AppPaths.EventCacheFile;
            if (!File.Exists(path))
                return new EventCacheState();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<EventCacheState>(json, JsonUtil.Options) ?? new EventCacheState();
        }
        catch
        {
            return new EventCacheState();
        }
    }

    public static void SaveEvents(EventCacheState state)
    {
        Directory.CreateDirectory(AppPaths.CacheDirectory);
        File.WriteAllText(AppPaths.EventCacheFile, JsonSerializer.Serialize(state, JsonUtil.Options));
    }

    public static UsageSnapshot? LoadSnapshot()
    {
        try
        {
            string path = AppPaths.SnapshotCacheFile;
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UsageSnapshot>(json, JsonUtil.Options);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveSnapshot(UsageSnapshot snapshot)
    {
        if (snapshot.Status != UsageStatus.Ok)
            return;

        Directory.CreateDirectory(AppPaths.CacheDirectory);
        File.WriteAllText(AppPaths.SnapshotCacheFile, JsonSerializer.Serialize(snapshot, JsonUtil.Options));
    }

    public static void Merge(EventCacheState state, IEnumerable<UsageEvent> incoming, long newestMs)
    {
        var seen = new HashSet<string>(state.Events.Select(e => e.Identity), StringComparer.Ordinal);
        foreach (UsageEvent ev in incoming)
        {
            if (seen.Add(ev.Identity))
                state.Events.Add(ev);
        }

        long keepAfter = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds();
        state.Events = state.Events.Where(e => e.TimestampMs >= keepAfter).ToList();
        if (newestMs > state.WatermarkMs)
            state.WatermarkMs = newestMs;
    }
}
