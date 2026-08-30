using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CursorUsage;

static class TrayIcons
{
    public static Icon Create(UsageSnapshot snapshot)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.Transparent);

            bool unknown = snapshot.Status != UsageStatus.Ok || !snapshot.HasIncludedPercent;
            bool exhausted = snapshot.TotalPercent is >= 100;
            Color color = unknown
                ? Color.FromArgb(255, 112, 112, 112)
                : Sparkline.QuotaColor(snapshot.TotalPercent);

            Color background = exhausted && !unknown
                ? Color.FromArgb(255, 196, 43, 28)
                : Color.FromArgb(255, 32, 32, 32);

            using (var fill = new SolidBrush(background))
                g.FillRectangle(fill, 1, 1, size - 2, size - 2);

            var chart = new Rectangle(2, 3, size - 4, size - 6);
            Color bars = exhausted && !unknown ? Color.White : color;
            Sparkline.Draw(g, chart, snapshot.Buckets, bars, fillFull: exhausted && !unknown);
        }

        return BitmapToIcon(bmp);
    }

    private static Icon BitmapToIcon(Bitmap bmp)
    {
        IntPtr handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeUser32.DestroyIcon(handle);
        }
    }
}

static class NativeUser32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
