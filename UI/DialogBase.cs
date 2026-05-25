using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ApexAuth.UI;

public abstract class DialogBase : Window
{
    protected readonly StackPanel Root = new();

    protected DialogBase(string title, string accentKey = "AccentBrush", double width = 340)
    {
        Width = width;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Foreground = Res("TextBrush");

        var shell = new Border
        {
            Background = Res("PanelBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 28,
                ShadowDepth = 0,
                Opacity = 0.4
            }
        };
        shell.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        Root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = FontWeights.Black,
            Foreground = Res("TextBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        Root.Children.Add(new Border
        {
            Width = 38,
            Height = 3,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Res(accentKey),
            Margin = new Thickness(0, 0, 0, 16)
        });

        shell.Child = Root;
        Content = shell;
    }

    protected static Brush Res(string key) =>
        (Brush)Application.Current.FindResource(key);

    protected static Style GetStyle(string key) =>
        (Style)Application.Current.FindResource(key);
}
