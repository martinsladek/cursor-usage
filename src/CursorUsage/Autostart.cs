using System.Diagnostics;
using Microsoft.Win32;

namespace CursorUsage;

static class Autostart
{
    public const string RunValueName = "CursorUsage";

    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static bool IsEnabled => HasRunValue && !IsDisabledByWindows;

    /// <summary>
    /// The running image cannot be deleted. Remove it after Exit instead.
    /// </summary>
    public static bool DeleteInstalledExeOnExit { get; private set; }

    public static void Enable()
    {
        DeleteInstalledExeOnExit = false;
        InstallCurrentExe();

        using (RegistryKey run = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true)
               ?? throw new InvalidOperationException("Could not open the Run registry key."))
        {
            run.SetValue(RunValueName, Quoted(AppPaths.InstalledExe));
        }

        SetApproved(enabled: true);
    }

    public static void Disable()
    {
        using (RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true))
        {
            run?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        using (RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedSubKey, writable: true))
        {
            approved?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        if (AppPaths.IsRunningFromInstallLocation)
            DeleteInstalledExeOnExit = true;
        else
            TryDeleteInstalledExe();
    }

    public static void ApplyOnLaunch()
    {
        if (HasRunValue)
        {
            RefreshInstalledCopyIfRegistered();
            return;
        }

        if (!AppPaths.IsRunningFromInstallLocation)
            TryDeleteInstalledExe();
    }

    public static void DeleteInstalledExeAfterThisProcessExits()
    {
        if (!DeleteInstalledExeOnExit)
            return;

        string path = AppPaths.InstalledExe;
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c timeout /t 1 /nobreak >nul & del /f /q \"{path}\"",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        });
    }

    public static void RefreshInstalledCopyIfRegistered()
    {
        if (!HasRunValue)
            return;

        if (AppPaths.IsRunningFromInstallLocation)
            return;

        if (!File.Exists(AppPaths.InstalledExe) || InstalledCopyLooksStale())
            InstallCurrentExe();
    }

    private static bool HasRunValue
    {
        get
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunSubKey);
            return run?.GetValue(RunValueName) is string { Length: > 0 };
        }
    }

    private static bool IsDisabledByWindows
    {
        get
        {
            using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedSubKey);
            if (approved?.GetValue(RunValueName) is not byte[] data || data.Length == 0)
                return false;

            byte flag = data[0];
            return flag is 0x03 or 0x07;
        }
    }

    private static void SetApproved(bool enabled)
    {
        using RegistryKey approved = Registry.CurrentUser.CreateSubKey(ApprovedSubKey, writable: true)
            ?? throw new InvalidOperationException("Could not open the StartupApproved registry key.");

        byte flag = enabled ? (byte)0x02 : (byte)0x03;
        var data = new byte[12];
        data[0] = flag;
        approved.SetValue(RunValueName, data, RegistryValueKind.Binary);
    }

    private static void InstallCurrentExe()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        if (AppPaths.IsRunningFromInstallLocation)
            return;

        File.Copy(AppPaths.CurrentExe, AppPaths.InstalledExe, overwrite: true);
    }

    private static bool InstalledCopyLooksStale()
    {
        var current = new FileInfo(AppPaths.CurrentExe);
        var installed = new FileInfo(AppPaths.InstalledExe);
        return current.Length != installed.Length
            || current.LastWriteTimeUtc != installed.LastWriteTimeUtc;
    }

    private static void TryDeleteInstalledExe()
    {
        try
        {
            if (File.Exists(AppPaths.InstalledExe))
                File.Delete(AppPaths.InstalledExe);
        }
        catch
        {
        }
    }

    private static string Quoted(string path) => $"\"{path}\"";
}
