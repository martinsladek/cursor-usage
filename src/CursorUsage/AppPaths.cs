namespace CursorUsage;

static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Strings.ConfigFolderName);

    public static string ConfigFile => Path.Combine(DataDirectory, "config.json");

    public static string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public static string EventCacheFile => Path.Combine(CacheDirectory, "events.json");

    public static string SnapshotCacheFile => Path.Combine(CacheDirectory, "snapshot.json");

    public static string InstalledExe => Path.Combine(DataDirectory, "CursorUsage.exe");

    public static string CurrentExe =>
        Environment.ProcessPath
        ?? throw new InvalidOperationException("The current executable path is unknown.");

    public static bool IsRunningFromInstallLocation =>
        PathsEqual(CurrentExe, InstalledExe);

    public static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
