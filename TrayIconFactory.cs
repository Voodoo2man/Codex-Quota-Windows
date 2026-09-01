using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;

namespace CodexQuota;

internal static class TrayIconFactory
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        using (var path = RoundedRectangle(3, 3, 58, 58, 15))
        using (var brush = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Color.FromArgb(70, 125, 255), Color.FromArgb(145, 75, 220), 135))
        using (var border = new Pen(Color.FromArgb(210, Color.White), 3))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPath(brush, path);
            g.DrawPath(border, path);
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, 17, 17, 30, 30);
            using var cutout = new SolidBrush(Color.FromArgb(100, 90, 225));
            g.FillEllipse(cutout, 23, 23, 18, 18);
            using var bar = new Pen(Color.White, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(bar, 32, 12, 32, 22);
            g.DrawLine(bar, 32, 42, 32, 52);
            g.DrawLine(bar, 12, 32, 22, 32);
            g.DrawLine(bar, 42, 32, 52, 32);
        }

        var handle = bitmap.GetHicon();
        try { using var source = Icon.FromHandle(handle); return (Icon)source.Clone(); }
        finally { DestroyIcon(handle); }
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90); path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}
