using System.Diagnostics;
using System.Globalization;

namespace CursorUsage;

sealed class DashboardForm : Form
{
    private readonly Label _headline;
    private readonly Label _subline;
    private readonly Label _autoLabel;
    private readonly Label _apiLabel;
    private readonly PercentBar _autoBar;
    private readonly PercentBar _apiBar;
    private readonly SparklineBox _sparkline;
    private readonly Label _sparkCaption;
    private readonly Label _lag;
    private readonly Label _tokenCaption;
    private readonly Label _tokenInput;
    private readonly Label _tokenOutput;
    private readonly Label _tokenCache;
    private readonly Label _onDemand;
    private bool _closeOnDeactivate;

    public DashboardForm()
    {
        Text = Strings.ProductName;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        ClientSize = new Size(320, 470);
        KeyPreview = true;
        BackColor = SystemColors.Window;
        Location = new Point(-32000, -32000);

        int chapterGap = Math.Max(16, Font.Height);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 16,
            Padding = new Padding(16, 14, 16, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _headline = new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 22f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2)
        };

        var included = new Label
        {
            Text = Strings.IncludedInPro,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 2)
        };

        _subline = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 4)
        };

        _autoLabel = new Label { AutoSize = true, Margin = new Padding(0, chapterGap, 0, 2) };
        _autoBar = new PercentBar { Height = 10, Margin = new Padding(0, 0, 0, 8), Dock = DockStyle.Top };
        _apiLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        _apiBar = new PercentBar { Height = 10, Margin = new Padding(0, 0, 0, 0), Dock = DockStyle.Top };

        _sparkCaption = new Label
        {
            Text = Strings.RecentIncluded,
            AutoSize = true,
            Margin = new Padding(0, chapterGap, 0, 4)
        };
        _sparkline = new SparklineBox { Height = 48, Margin = new Padding(0, 0, 0, 4), Dock = DockStyle.Top };
        _lag = new Label
        {
            Text = Strings.LagNote,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 0)
        };

        _tokenCaption = new Label
        {
            Text = Strings.TokensRecent,
            AutoSize = true,
            Margin = new Padding(0, chapterGap, 0, 4)
        };
        _tokenInput = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        _tokenOutput = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        _tokenCache = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 4) };

        _onDemand = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 8, 0, 10)
        };

        var link = new LinkLabel
        {
            Text = Strings.OpenSpendingDashboard,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        link.LinkClicked += (_, _) => OpenSpending();

        layout.Controls.Add(included, 0, 0);
        layout.Controls.Add(_headline, 0, 1);
        layout.Controls.Add(_subline, 0, 2);
        layout.Controls.Add(_autoLabel, 0, 3);
        layout.Controls.Add(_autoBar, 0, 4);
        layout.Controls.Add(_apiLabel, 0, 5);
        layout.Controls.Add(_apiBar, 0, 6);
        layout.Controls.Add(_sparkCaption, 0, 7);
        layout.Controls.Add(_sparkline, 0, 8);
        layout.Controls.Add(_lag, 0, 9);
        layout.Controls.Add(_tokenCaption, 0, 10);
        layout.Controls.Add(_tokenInput, 0, 11);
        layout.Controls.Add(_tokenOutput, 0, 12);
        layout.Controls.Add(_tokenCache, 0, 13);
        layout.Controls.Add(_onDemand, 0, 14);
        layout.Controls.Add(link, 0, 15);

        Controls.Add(layout);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };

        Shown += (_, _) =>
        {
            BeginInvoke(() => _closeOnDeactivate = true);
        };

        Deactivate += (_, _) =>
        {
            if (_closeOnDeactivate)
                Close();
        };
    }

    public void Apply(UsageSnapshot snapshot)
    {
        if (snapshot.Status == UsageStatus.SignedOut)
        {
            _headline.Text = "—";
            _headline.ForeColor = Color.FromArgb(112, 112, 112);
            _subline.Text = Strings.SignInToCursor;
            _autoLabel.Text = Strings.AutoPool;
            _apiLabel.Text = Strings.ApiPool;
            _autoBar.Value = null;
            _apiBar.Value = null;
            _sparkline.Apply(snapshot);
            ClearTokenLines();
            _onDemand.Text = "";
            return;
        }

        if (snapshot.Status != UsageStatus.Ok || !snapshot.HasIncludedPercent)
        {
            _headline.Text = "—";
            _headline.ForeColor = Color.FromArgb(112, 112, 112);
            _subline.Text = Strings.UsageUnavailable;
            _autoLabel.Text = Strings.AutoPool;
            _apiLabel.Text = Strings.ApiPool;
            _autoBar.Value = null;
            _apiBar.Value = null;
            _sparkline.Apply(snapshot);
            ClearTokenLines();
            _onDemand.Text = "";
            return;
        }

        _headline.Text = Strings.FormatPercent(snapshot.TotalPercent!.Value);
        _headline.ForeColor = Sparkline.QuotaColor(snapshot.TotalPercent);

        string plan = Strings.FormatPlanName(snapshot.MembershipType);
        if (snapshot.CycleEnd is DateTimeOffset end)
        {
            string date = end.ToLocalTime().ToString("d MMMM", CultureInfo.CurrentCulture);
            _subline.Text = $"{plan} · {Strings.Resets} {date}";
        }
        else
        {
            _subline.Text = plan;
        }

        _autoLabel.Text = $"{Strings.AutoPool}  {FormatPool(snapshot.AutoPercent)}";
        _apiLabel.Text = $"{Strings.ApiPool}  {FormatPool(snapshot.ApiPercent)}";
        _autoBar.Value = snapshot.AutoPercent;
        _apiBar.Value = snapshot.ApiPercent;
        _sparkline.Apply(snapshot);

        _tokenInput.Text = $"{Strings.TokenInput}  {FormatCount(snapshot.InputTokens)}";
        _tokenOutput.Text = $"{Strings.TokenOutput}  {FormatCount(snapshot.OutputTokens)}";
        _tokenCache.Text = $"{Strings.TokenCache}  {FormatCount(snapshot.CacheTokens)}";

        _onDemand.Text = snapshot.OnDemandEnabled is true ? Strings.OnDemandOn : Strings.OnDemandOff;
    }

    private void ClearTokenLines()
    {
        _tokenInput.Text = $"{Strings.TokenInput}  —";
        _tokenOutput.Text = $"{Strings.TokenOutput}  —";
        _tokenCache.Text = $"{Strings.TokenCache}  —";
    }

    public void PlaceNearPointer()
    {
        if (!IsHandleCreated)
            _ = Handle;

        Point pos = Cursor.Position;
        Rectangle area = Screen.FromPoint(pos).WorkingArea;
        int x = pos.X - Width / 2;
        int y = pos.Y - Height - 8;
        if (y < area.Top)
            y = pos.Y + 8;
        if (x < area.Left)
            x = area.Left + 8;
        if (x + Width > area.Right)
            x = area.Right - Width - 8;
        if (y + Height > area.Bottom)
            y = area.Bottom - Height - 8;
        Location = new Point(x, y);
    }

    private static string FormatPool(double? value) =>
        value is double v ? Strings.FormatPercent(v) : "—";

    private static string FormatCount(long value)
    {
        if (value >= 1_000_000)
            return (value / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture) + "M";
        if (value >= 1_000)
            return (value / 1_000d).ToString("0.0", CultureInfo.CurrentCulture) + "k";
        return value.ToString("N0", CultureInfo.CurrentCulture);
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
}

sealed class PercentBar : Control
{
    private double? _value;

    public double? Value
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    public PercentBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = 10;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var bg = new SolidBrush(Color.FromArgb(230, 230, 230)))
            e.Graphics.FillRectangle(bg, bounds);

        if (_value is double v && v > 0)
        {
            int w = (int)Math.Round(Math.Clamp(v, 0, 100) / 100.0 * bounds.Width);
            using var fg = new SolidBrush(Sparkline.QuotaColor(v));
            e.Graphics.FillRectangle(fg, 0, 0, Math.Max(1, w), bounds.Height);
        }

        using var pen = new Pen(Color.FromArgb(200, 200, 200));
        e.Graphics.DrawRectangle(pen, bounds);
    }
}

sealed class SparklineBox : Control
{
    private UsageSnapshot _snapshot = UsageSnapshot.Unavailable();

    public SparklineBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = 48;
    }

    public void Apply(UsageSnapshot snapshot)
    {
        _snapshot = snapshot;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        bool unknown = _snapshot.Status != UsageStatus.Ok || !_snapshot.HasIncludedPercent;
        bool exhausted = _snapshot.TotalPercent is >= 100;
        Color bg = exhausted && !unknown ? Color.FromArgb(196, 43, 28) : Color.FromArgb(245, 245, 245);
        using (var brush = new SolidBrush(bg))
            e.Graphics.FillRectangle(brush, bounds);

        Color bars = unknown
            ? Color.FromArgb(112, 112, 112)
            : exhausted
                ? Color.White
                : Sparkline.QuotaColor(_snapshot.TotalPercent);

        var chart = Rectangle.Inflate(bounds, -4, -4);
        Sparkline.Draw(e.Graphics, chart, _snapshot.Buckets, bars, fillFull: exhausted && !unknown);

        using var pen = new Pen(Color.FromArgb(200, 200, 200));
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
