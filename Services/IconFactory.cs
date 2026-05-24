using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace ApexAuth.Services;

public static class IconFactory
{
    public static Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = CreateLogoBitmap(64, AccentColor());
        var handle = bitmap.GetHicon();
        var icon = (Drawing.Icon)Drawing.Icon.FromHandle(handle).Clone();
        NativeMethods.DestroyIcon(handle);
        return icon;
    }

    public static BitmapSource CreateWindowIcon()
    {
        using var bitmap = CreateLogoBitmap(128, AccentColor());
        var handle = bitmap.GetHicon();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(128, 128));
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static Color AccentColor()
    {
        if (System.Windows.Application.Current?.Resources["AccentColor"] is Color c)
            return c;
        return Color.FromRgb(0x7C, 0x73, 0xFF);
    }

    private static Drawing.Bitmap CreateLogoBitmap(int size, Color accent)
    {
        var bitmap = new Drawing.Bitmap(size, size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);

        var lighter = Lighten(accent, 0.10);
        var darker  = Darken(accent, 0.40);

        var scale = size / 64f;
        using var bg = new Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle(0, 0, size, size),
            Drawing.Color.FromArgb(255, lighter.R, lighter.G, lighter.B),
            Drawing.Color.FromArgb(255, darker.R, darker.G, darker.B),
            45f);
        using var shine = new Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle(0, 0, size, size / 2),
            Drawing.Color.FromArgb(125, 255, 255, 255),
            Drawing.Color.FromArgb(0, 255, 255, 255),
            90f);
        using var ring = new Drawing.Pen(Drawing.Color.FromArgb(210, 230, 233, 255), 2.5f * scale);
        using var glow = new Drawing.Pen(Drawing.Color.FromArgb(120, accent.R, accent.G, accent.B), 7f * scale);
        using var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
        using var font = new Drawing.Font("Segoe UI", 34f * scale, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
        using var textFormat = new Drawing.StringFormat
        {
            Alignment = Drawing.StringAlignment.Center,
            LineAlignment = Drawing.StringAlignment.Center
        };

        var outer = ScaleRect(5, 5, 54, 54, scale);
        var inner = ScaleRect(9, 9, 46, 46, scale);
        graphics.FillRoundedRectangle(bg, outer, (int)(15 * scale));
        graphics.FillRoundedRectangle(shine, ScaleRect(9, 8, 46, 24, scale), (int)(12 * scale));
        graphics.DrawRoundedRectangle(glow, ScaleRect(8, 8, 48, 48, scale), (int)(12 * scale));
        graphics.DrawRoundedRectangle(ring, inner, (int)(12 * scale));
        graphics.DrawString("A", font, textBrush, new Drawing.RectangleF(0, 2 * scale, size, 58 * scale), textFormat);
        return bitmap;
    }

    private static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));

    private static Color Darken(Color c, double amount) => Color.FromRgb(
        (byte)(c.R * (1 - amount)),
        (byte)(c.G * (1 - amount)),
        (byte)(c.B * (1 - amount)));

    private static Drawing.Rectangle ScaleRect(int x, int y, int width, int height, float scale)
    {
        return new Drawing.Rectangle(
            (int)(x * scale),
            (int)(y * scale),
            (int)(width * scale),
            (int)(height * scale));
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyIcon(IntPtr hIcon);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Drawing.Graphics graphics, Drawing.Brush brush, Drawing.Rectangle bounds, int radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Drawing.Graphics graphics, Drawing.Pen pen, Drawing.Rectangle bounds, int radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static Drawing2D.GraphicsPath CreateRoundedPath(Drawing.Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
