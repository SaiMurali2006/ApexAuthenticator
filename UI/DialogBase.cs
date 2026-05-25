using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ApexAuth.UI;

public abstract class DialogBase : Window
{
    protected readonly StackPanel Root = new();
    private Border _shell = null!;

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
        Loaded += (_, _) => AnimateIn();

        var shell = new Border
        {
            Background = Res("PanelBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 28,
                ShadowDepth = 0,
                Opacity = 0.4
            }
        };
        _shell = shell;
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

    private void AnimateIn()
    {
        var scale = new ScaleTransform(0.88, 0.88);
        _shell.RenderTransform = scale;
        _shell.Opacity = 0;

        var pop  = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 3.5 };
        var fade = new CubicEase   { EasingMode = EasingMode.EaseOut };

        _shell.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = fade });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(500)) { EasingFunction = pop });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(500)) { EasingFunction = pop });
    }

    protected static Brush Res(string key) =>
        (Brush)Application.Current.FindResource(key);

    protected static Style GetStyle(string key) =>
        (Style)Application.Current.FindResource(key);
}
