using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ApexAuth.UI;

// Modern stroke-based icon set. Every glyph is sized to a 16×16 canvas,
// rendered inside a Viewbox so callers can pick the final visual size.
public static class Icons
{
    public static UIElement Minimize(string strokeKey = "TextBrush", double display = 12)
        => Glyph("M3.5,8 H12.5", strokeKey, display, thickness: 1.6);

    public static UIElement Close(string strokeKey = "TextBrush", double display = 12)
        => Glyph("M4,4 L12,12 M12,4 L4,12", strokeKey, display, thickness: 1.6);

    // Two overlapping rounded rectangles — the universal "copy" affordance.
    public static UIElement Copy(string strokeKey = "TextBrush", double display = 14)
        => Glyph("M4,5 H10 V11 H4 Z M6,3 H12 V9", strokeKey, display, thickness: 1.5);

    // A clean pencil: tip at upper-right, body slanting to lower-left, small ferrule line.
    public static UIElement Edit(string strokeKey = "TextBrush", double display = 14)
        => Glyph("M3,13 L3,11 L11,3 L13,5 L5,13 Z M9.5,4.5 L11.5,6.5", strokeKey, display, thickness: 1.5);

    // A trash can: lid line, handle bump, body sides + bottom, two slat marks.
    public static UIElement Delete(string strokeKey = "DangerBrush", double display = 14)
        => Glyph("M3,5 H13 M5,5 L5.5,13 L10.5,13 L11,5 M6.5,3 H9.5 V5 M7,7.5 V11 M9,7.5 V11",
                 strokeKey, display, thickness: 1.5);

    // Subtle dot for "no accounts yet" empty state — purely decorative.
    public static UIElement EmptyState(string strokeKey = "MutedBrush", double display = 28)
        => Glyph("M4,7 H12 M4,11 H10 M3,3 H13 V13 H3 Z", strokeKey, display, thickness: 1.4);

    private static UIElement Glyph(string data, string strokeKey, double display, double thickness)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        path.SetResourceReference(Shape.StrokeProperty, strokeKey);

        var canvas = new Canvas { Width = 16, Height = 16 };
        canvas.Children.Add(path);

        return new Viewbox
        {
            Width = display,
            Height = display,
            Stretch = Stretch.Uniform,
            Child = canvas,
            IsHitTestVisible = false
        };
    }
}
