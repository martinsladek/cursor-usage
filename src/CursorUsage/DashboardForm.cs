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
    private readonly Label _tokens;
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
        ClientSize = new Size(320, 390);
        KeyPreview = true;
        BackColor = SystemColors.Window;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
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
            Margin = new Padding(0, 0, 0, 12)
        };

        _autoLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        _autoBar = new PercentBar { Height = 10, Margin = new Padding(0, 0, 0, 10), Dock = DockStyle.Top };
        _apiLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
        _apiBar = new PercentBar { Height = 10, Margin = new Padding(0, 0, 0, 12), Dock = DockStyle.Top };

        _sparkCaption = new Label
        {
            Text = Strings.RecentIncluded,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        _sparkline = new SparklineBox { Height = 48, Margin = new Padding(0, 0, 0, 4), Dock = DockStyle.Top };
        _lag = new Label
        {
            Text = Strings.LagNote,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 12)
        };

        _tokens = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(280, 0),
            Margin = new Padding(0, 0, 0, 10)
        };

        _onDemand = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(280, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 10)
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

        var bottom = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        bottom.Controls.Add(_tokens, 0, 0);
        bottom.Controls.Add(_onDemand, 0, 1);
        bottom.Controls.Add(link, 0, 2);
        layout.Controls.Add(bottom, 0, 10);

        Controls.Add(layout);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };

        Shown += (_, _) =>
        {
            PositionNearCursor();
            BeginInvoke(() =>
            {
                _closeOnDeactivate = true;
                Activate();
            });
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
            _tokens.Text = "";
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
            _tokens.Text = "";
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

        _tokens.Text =
            $"{Strings.TokensRecent}: {Strings.TokenInput} {FormatCount(snapshot.InputTokens)} · " +
            $"{Strings.TokenOutput} {FormatCount(snapshot.OutputTokens)} · " +
            $"{Strings.TokenCache} {FormatCount(snapshot.CacheTokens)}";

        _onDemand.Text = snapshot.OnDemandEnabled is true ? Strings.OnDemandOn : Strings.OnDemandOff;
    }

    private void PositionNearCursor()
    {
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
