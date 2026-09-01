using System.Diagnostics;

namespace CursorUsage;

sealed class TrayApplicationContext : ApplicationContext
{
    private const int ActivePollMs = 60_000;
    private const int IdlePollMs = 300_000;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Form _sync;
    private readonly System.Windows.Forms.Timer _pollTimer;

    private UsageSnapshot _snapshot = UsageSnapshot.Unavailable();
    private Icon? _icon;
    private Form? _about;
    private DashboardForm? _dashboard;
    private bool _refreshing;
    private DateTime _dashboardClosedUtc = DateTime.MinValue;

    public TrayApplicationContext()
    {
        Autostart.ApplyOnLaunch();
        _snapshot = UsageCache.LoadSnapshot() ?? UsageSnapshot.Unavailable();

        _sync = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1),
            Opacity = 0,
            ShowIcon = false
        };
        _ = _sync.Handle;

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, e) =>
        {
            RebuildMenu();
            // WinForms cancels Opening when the strip is still empty. The first
            // right-click after start hits that path and is swallowed.
            e.Cancel = _menu.Items.Count == 0;
        };

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = _menu,
            Text = Strings.AppName
        };
        _notifyIcon.MouseClick += OnTrayMouseClick;

        _pollTimer = new System.Windows.Forms.Timer { Interval = ActivePollMs };
        _pollTimer.Tick += (_, _) => _ = RefreshAsync();
        _pollTimer.Start();

        ApplyPresentation();
        _ = RefreshAsync();
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        ToggleDashboard();
    }

    private void ToggleDashboard()
    {
        if (_dashboard is { IsDisposed: false })
        {
            _dashboard.Close();
            return;
        }

        // The same tray click that deactivated the dashboard would otherwise
        // close it (Deactivate) and immediately open it again (this click).
        if (DateTime.UtcNow - _dashboardClosedUtc < TimeSpan.FromMilliseconds(250))
            return;

        var form = new DashboardForm();
        form.Apply(_snapshot);
        _dashboard = form;
        form.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_dashboard, form))
                _dashboard = null;
            _dashboardClosedUtc = DateTime.UtcNow;
            form.Dispose();
            SetPollInterval();
        };
        _ = form.Handle;
        form.PlaceNearPointer();
        form.Show();
        form.Activate();
        _sync.BeginInvoke(() => _ = RefreshAsync());
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;
        try
        {
            bool dashboardOpen = _dashboard is { IsDisposed: false };
            UsageSnapshot next = await UsageClient.FetchAsync(dashboardOpen, CancellationToken.None);
            if (next.Status == UsageStatus.Unavailable && _snapshot.Status == UsageStatus.Ok)
            {
                _snapshot = new UsageSnapshot
                {
                    Status = UsageStatus.Unavailable,
                    TotalPercent = _snapshot.TotalPercent,
                    AutoPercent = _snapshot.AutoPercent,
                    ApiPercent = _snapshot.ApiPercent,
                    MembershipType = _snapshot.MembershipType,
                    CycleStart = _snapshot.CycleStart,
                    CycleEnd = _snapshot.CycleEnd,
                    OnDemandEnabled = _snapshot.OnDemandEnabled,
                    Buckets = _snapshot.Buckets,
                    InputTokens = _snapshot.InputTokens,
                    OutputTokens = _snapshot.OutputTokens,
                    CacheTokens = _snapshot.CacheTokens,
                    FetchedAt = _snapshot.FetchedAt,
                    HadRecentActivity = false
                };
            }
            else
            {
                _snapshot = next;
                if (next.Status == UsageStatus.Ok)
                    UsageCache.SaveSnapshot(next);
            }

            if (_sync.IsHandleCreated)
                _sync.BeginInvoke(ApplyPresentation);
            else
                ApplyPresentation();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (_snapshot.Status != UsageStatus.Ok)
            {
                _snapshot = UsageSnapshot.Unavailable();
                ApplyPresentation();
            }
        }
        finally
        {
            _refreshing = false;
            SetPollInterval();
        }
    }

    private void SetPollInterval()
    {
        bool active = _snapshot.HadRecentActivity || _dashboard is { IsDisposed: false };
        _pollTimer.Interval = active ? ActivePollMs : IdlePollMs;
    }

    private void ApplyPresentation()
    {
        Icon created = TrayIcons.Create(_snapshot);
        Icon? previous = _icon;
        _icon = created;
        _notifyIcon.Icon = created;
        previous?.Dispose();
        _notifyIcon.Text = BuildTooltip();
        _dashboard?.Apply(_snapshot);
    }

    private string BuildTooltip()
    {
        if (_snapshot.Status == UsageStatus.SignedOut)
            return Strings.TruncateTooltip(Strings.SignInToCursor);

        if (_snapshot.Status != UsageStatus.Ok || !_snapshot.HasIncludedPercent)
            return Strings.TruncateTooltip(Strings.UsageUnavailable);

        double total = _snapshot.TotalPercent!.Value;
        if (total >= 100)
            return Strings.TooltipRateLimited(total);

        if (total >= 90 && _snapshot.CycleEnd is DateTimeOffset end)
            return Strings.TooltipIncludedResets(total, end);

        return Strings.TooltipIncluded(total, _snapshot.AutoPercent, _snapshot.ApiPercent);
    }

    private void RebuildMenu()
    {
        _menu.Items.Clear();

        var refresh = new ToolStripMenuItem(Strings.Refresh, null, (_, _) => _ = RefreshAsync());
        refresh.Enabled = !_refreshing;
        _menu.Items.Add(refresh);

        _menu.Items.Add(Strings.OpenCursorSpending, null, (_, _) => OpenSpending());
        _menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem(Strings.StartWithWindows)
        {
            Checked = Autostart.IsEnabled,
            CheckOnClick = false
        };
        autostart.Click += (_, _) => ToggleAutostart();
        _menu.Items.Add(autostart);

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Strings.About, null, (_, _) => ShowAbout());
        _menu.Items.Add(Strings.Exit, null, (_, _) => ExitApp());
    }

    private void ToggleAutostart()
    {
        try
        {
            if (Autostart.IsEnabled)
                Autostart.Disable();
            else
                Autostart.Enable();
        }
        catch
        {
            _notifyIcon.ShowBalloonTip(4000, Strings.AppName, Strings.AutostartFailed, ToolTipIcon.None);
        }
    }

    private void ShowAbout()
    {
        TrayDialog.ShowOnce(ref _about, _sync, () => new AboutForm());
    }

    private void ExitApp()
    {
        _pollTimer.Stop();
        _notifyIcon.Visible = false;
        Autostart.DeleteInstalledExeAfterThisProcessExits();
        ExitThread();
    }

    private static void OpenSpending()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Strings.SpendingUrl,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Dispose();
            _notifyIcon.Dispose();
            _icon?.Dispose();
            _sync.Dispose();
        }

        base.Dispose(disposing);
    }
}
