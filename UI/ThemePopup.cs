using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ApexAuth.Services;

namespace ApexAuth.UI;

public sealed class ThemePopup
{
    private static readonly string[] PresetAccents =
    {
        "#7C73FF", // signature violet
        "#3D8BFF", // azure
        "#1FB6A8", // teal
        "#27C065", // green
        "#FFB347", // amber
        "#FF6B81", // coral
        "#D26AFF", // magenta
        "#9C9C9C"  // neutral
    };

    public Popup Popup { get; }
    private readonly TextBox _hexBox;
    private readonly Border _hexPreview;
    private readonly Button _lightBtn;
    private readonly Button _darkBtn;
    private readonly Button _systemBtn;
    private readonly TextBlock _hexError;
    private readonly WrapPanel _presetRow;

    public ThemePopup(UIElement placement)
    {
        var shell = new Border
        {
            Background = Res("PanelBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            Width = 286,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 26,
                ShadowDepth = 0,
                Opacity = 0.35
            }
        };

        var stack = new StackPanel();
        shell.Child = stack;

        stack.Children.Add(SectionLabel("APPEARANCE"));

        var modeRow = new Border
        {
            Background = Res("InputBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 6, 0, 14)
        };
        var modeGrid = new Grid();
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _lightBtn  = SegmentButton("Light");
        _darkBtn   = SegmentButton("Dark");
        _systemBtn = SegmentButton("System");
        _lightBtn.Click  += (_, _) => { ThemeService.Current.SetMode(ThemeMode.Light);  Sync(); };
        _darkBtn.Click   += (_, _) => { ThemeService.Current.SetMode(ThemeMode.Dark);   Sync(); };
        _systemBtn.Click += (_, _) => { ThemeService.Current.SetMode(ThemeMode.System); Sync(); };
        Grid.SetColumn(_lightBtn,  0);
        Grid.SetColumn(_darkBtn,   1);
        Grid.SetColumn(_systemBtn, 2);
        modeGrid.Children.Add(_lightBtn);
        modeGrid.Children.Add(_darkBtn);
        modeGrid.Children.Add(_systemBtn);
        modeRow.Child = modeGrid;
        stack.Children.Add(modeRow);

        stack.Children.Add(SectionLabel("ACCENT"));

        _presetRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 10) };
        foreach (var hex in PresetAccents)
            _presetRow.Children.Add(SwatchButton(hex));
        stack.Children.Add(_presetRow);

        var hexRow = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _hexPreview = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(10),
            Background = Res("AccentBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _hexBox = new TextBox
        {
            Text = ThemeService.Current.AccentHex,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _hexBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) TryApplyHex();
        };
        _hexBox.LostFocus += (_, _) => TryApplyHex();
        Grid.SetColumn(_hexPreview, 0);
        Grid.SetColumn(_hexBox, 1);
        hexRow.Children.Add(_hexPreview);
        hexRow.Children.Add(_hexBox);
        stack.Children.Add(hexRow);

        _hexError = new TextBlock
        {
            Foreground = Res("DangerBrush"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed
        };
        stack.Children.Add(_hexError);

        Popup = new Popup
        {
            Child = shell,
            PlacementTarget = placement,
            Placement = PlacementMode.Bottom,
            VerticalOffset = 8,
            HorizontalOffset = -2,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Slide
        };
        Popup.Opened += (_, _) =>
        {
            Sync();
            // Pop-in with a tiny spring once the slide finishes.
            shell.RenderTransformOrigin = new Point(0, 0);
            var scale = new ScaleTransform(0.96, 0.96);
            shell.RenderTransform = scale;
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
            var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        };

        ThemeService.Current.ThemeChanged += () =>
        {
            _hexPreview.Background = Res("AccentBrush");
            _hexBox.Text = ThemeService.Current.AccentHex;
            Sync();
        };
    }

    public void Toggle() => Popup.IsOpen = !Popup.IsOpen;

    private void TryApplyHex()
    {
        if (ThemeService.Current.TrySetAccent(_hexBox.Text))
        {
            _hexError.Visibility = Visibility.Collapsed;
            _hexBox.Text = ThemeService.Current.AccentHex;
        }
        else
        {
            _hexError.Text = "Use a 6-digit hex like #7C73FF.";
            _hexError.Visibility = Visibility.Visible;
        }
    }

    private void Sync()
    {
        MarkActive(_lightBtn,  ThemeService.Current.Mode == ThemeMode.Light);
        MarkActive(_darkBtn,   ThemeService.Current.Mode == ThemeMode.Dark);
        MarkActive(_systemBtn, ThemeService.Current.Mode == ThemeMode.System);
        _hexBox.Text = ThemeService.Current.AccentHex;
        _hexPreview.Background = Res("AccentBrush");
    }

    private static void MarkActive(Button b, bool active)
    {
        b.Background = active ? Res("AccentBrush") : System.Windows.Media.Brushes.Transparent;
        b.Foreground = active ? Res("OnAccentBrush") : Res("MutedBrush");
        b.BorderBrush = System.Windows.Media.Brushes.Transparent;
    }

    private static Button SegmentButton(string text)
    {
        var btn = new Button
        {
            Content = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Padding = new Thickness(4, 6, 4, 6),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
            MinHeight = 28
        };
        btn.Template = SegmentTemplate();
        return btn;
    }

    private static ControlTemplate SegmentTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty,
            new System.Windows.Data.Binding("Foreground") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        content.SetValue(System.Windows.Documents.TextElement.FontWeightProperty, FontWeights.SemiBold);
        border.AppendChild(content);
        tpl.VisualTree = border;
        return tpl;
    }

    private Button SwatchButton(string hex)
    {
        var ok = ThemeService.TryParseHex(hex, out var color);
        var brush = ok ? new SolidColorBrush(color) : (Brush)Res("AccentBrush");
        brush.Freeze();

        var swatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = brush,
            Margin = new Thickness(2)
        };
        var btn = new Button
        {
            Content = swatch,
            ToolTip = hex,
            Cursor = Cursors.Hand,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 2, 2),
            MinHeight = 28,
            Width = 32,
            Height = 32
        };
        btn.Template = SegmentTemplate();
        btn.Click += (_, _) =>
        {
            ThemeService.Current.TrySetAccent(hex);
            Sync();
        };
        return btn;
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 10,
        FontWeight = FontWeights.Bold,
        Foreground = Res("MutedBrush"),
        Margin = new Thickness(2, 0, 0, 0)
    };
}
