using System;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace ApexAuth.Services;

public static class IconFactory
{
    private static readonly ConcurrentDictionary<float, Drawing.Font> FontCache = new();

    private static Drawing.Font GetLogoFont(float pixelSize) =>
        FontCache.GetOrAdd(pixelSize, px => new Drawing.Font(
            "Segoe UI Variable Display", px, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel));

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

    // Flat, modern: solid accent fill, thin alt-accent ring, centered bold "A".
    // Mirrors the in-app logo badge so the tray and window read as the same mark.
    private static Drawing.Bitmap CreateLogoBitmap(int size, Color accent)
    {
        var bitmap = new Drawing.Bitmap(size, size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Drawing.Color.Transparent);

        var scale     = size / 64f;
        var altAccent = Lighten(accent, 0.20);
        var onAccent  = ContrastText(accent);

        // Body: solid accent rounded square. 5px outer margin on a 64px canvas.
        var body       = ScaleRect(5, 5, 54, 54, scale);
        var bodyRadius = (int)Math.Round(13 * scale);

        using var fill = new Drawing.SolidBrush(ToGdi(accent));
        graphics.FillRoundedRectangle(fill, body, bodyRadius);

        // Faintest top-down highlight to suggest dimension — no glossy ring or shine.
        using var highlight = new Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle(0, 0, size, (int)(size * 0.55f)),
            Drawing.Color.FromArgb(36, 255, 255, 255),
            Drawing.Color.FromArgb(0, 255, 255, 255),
            90f);
        graphics.FillRoundedRectangle(highlight, body, bodyRadius);

        // Thin alt-accent outline; 1px minimum so it always reads at 16/32px.
        var penWidth = Math.Max(1.0f, 1.4f * scale);
        using var ring = new Drawing.Pen(
            Drawing.Color.FromArgb(230, altAccent.R, altAccent.G, altAccent.B),
            penWidth);
        graphics.DrawRoundedRectangle(ring, body, bodyRadius);

        // Centered bold "A".
        using var textBrush = new Drawing.SolidBrush(ToGdi(onAccent));
        var font = GetLogoFont(36f * scale);
        using var fmt = new Drawing.StringFormat
        {
            Alignment = Drawing.StringAlignment.Center,
            LineAlignment = Drawing.StringAlignment.Center
        };
        var textRect = new Drawing.RectangleF(0, 1.5f * scale, size, 60 * scale);
        graphics.DrawString("A", font, textBrush, textRect, fmt);

        return bitmap;
    }

    private static Drawing.Color ToGdi(Color c) =>
        Drawing.Color.FromArgb(255, c.R, c.G, c.B);

    private static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));

    private static Color ContrastText(Color bg)
    {
        var luminance = bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114;
        return luminance >= 150
            ? Color.FromRgb(0x10, 0x11, 0x1A)
            : Color.FromRgb(0xFF, 0xFF, 0xFF);
    }

    private static Drawing.Rectangle ScaleRect(int x, int y, int width, int height, float scale) =>
        new(
            (int)Math.Round(x * scale),
            (int)Math.Round(y * scale),
            (int)Math.Round(width * scale),
            (int)Math.Round(height * scale));

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
