using System.Drawing.Drawing2D;

namespace CursorUsage;

static class Sparkline
{
    public const int BucketCount = 16;

    public static Color QuotaColor(double? totalPercent)
    {
        if (totalPercent is not double value || double.IsNaN(value))
            return Color.FromArgb(255, 112, 112, 112);

        if (value >= 100)
            return Color.FromArgb(255, 196, 43, 28);

        if (value >= 90)
            return Color.FromArgb(255, 232, 80, 28);

        if (value >= 70)
            return Color.FromArgb(255, 218, 164, 0);

        return Color.FromArgb(255, 0, 120, 215);
    }

    public static void Draw(
        Graphics g,
        Rectangle bounds,
        IReadOnlyList<double> buckets,
        Color barColor,
        bool fillFull)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        int count = Math.Min(BucketCount, buckets.Count);
        if (count <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        double max = 0;
        for (int i = 0; i < count; i++)
            max = Math.Max(max, buckets[i]);

        float gap = bounds.Width >= count * 3 ? 1f : 0f;
        float barWidth = (bounds.Width - gap * (count - 1)) / count;
        if (barWidth < 1f)
            barWidth = 1f;

        using var brush = new SolidBrush(barColor);
        for (int i = 0; i < count; i++)
        {
            float height;
            if (fillFull)
            {
                height = bounds.Height;
            }
            else if (max <= 0 || buckets[i] <= 0)
            {
                height = 0;
            }
            else
            {
                height = (float)(buckets[i] / max) * bounds.Height;
                if (height > 0 && height < 1f)
                    height = 1f;
            }

            if (height <= 0)
                continue;

            float x = bounds.X + i * (barWidth + gap);
            float y = bounds.Bottom - height;
            g.FillRectangle(brush, x, y, Math.Max(1f, barWidth - 0.2f), height);
        }
    }
}
