using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ApexAuth.UI;

public abstract class DialogBase : Window
{
    protected readonly StackPanel Root = new();

    protected DialogBase(string title, string accentKey = "AccentBrush", double width = 330)
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
            Background = Res("CardBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18)
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
            Width = 42,
            Height = 3,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Res(accentKey),
            Margin = new Thickness(0, 0, 0, 12)
        });

        shell.Child = Root;
        Content = shell;
    }

    protected static Brush Res(string key) =>
        (Brush)Application.Current.FindResource(key);

    protected static Style GetStyle(string key) =>
        (Style)Application.Current.FindResource(key);
}
