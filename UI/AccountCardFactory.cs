using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ApexAuth.Models;
using ApexAuth.Services;
using static ApexAuth.UI.ResourceHelpers;

namespace ApexAuth.UI;

public static class AccountCardFactory
{
    private static readonly CubicEase   EaseOut = new() { EasingMode = EasingMode.EaseOut };
    private static readonly BackEase    Spring  = new() { EasingMode = EasingMode.EaseOut, Amplitude = 1.1 };
    private static readonly ElasticEase Bounce  = new() { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 3.2 };

    public static Border Create(
        AuthAccount account,
        Action<AuthAccount> onCopy,
        Action<AuthAccount> onEdit,
        Action<AuthAccount> onDelete,
        out TextBlock codeBlock)
    {
        var translate = new TranslateTransform(0, 14);
        var scale     = new ScaleTransform(1, 1);
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scale);
        transformGroup.Children.Add(translate);

        var card = new Border
        {
            Tag = account,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 12, 12, 12),
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = transformGroup,
            Cursor = Cursors.Hand
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "LineSoftBrush");
        card.Loaded += (_, _) => AnimateIn(card, translate);
        card.MouseEnter += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "CardHoverBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "AccentSoftBrush");
            AnimateTo(translate, TranslateTransform.YProperty, -3, 260, Spring);
        };
        card.MouseLeave += (_, _) =>
        {
            card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "LineSoftBrush");
            AnimateTo(translate, TranslateTransform.YProperty, 0, 320, Bounce);
        };
        card.PreviewMouseLeftButtonDown += (_, _) =>
        {
            AnimateTo(scale, ScaleTransform.ScaleXProperty, 0.94, 80, EaseOut);
            AnimateTo(scale, ScaleTransform.ScaleYProperty, 0.94, 80, EaseOut);
        };
        card.PreviewMouseLeftButtonUp += (_, _) =>
        {
            AnimateTo(scale, ScaleTransform.ScaleXProperty, 1, 480, Bounce);
            AnimateTo(scale, ScaleTransform.ScaleYProperty, 1, 480, Bounce);
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject src && IsButtonAncestor(src)) return;
            FlashBorder(card);
            onCopy(account);
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        var label = new TextBlock
        {
            Text = account.Label,
            FontSize = 12,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(0, 0, 0, 2)
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        left.Children.Add(label);

        var code = new TextBlock
        {
            Text = FormatCode(TotpService.GetCode(account.Secret)),
            FontFamily = new FontFamily("Cascadia Code, Cascadia Mono, Consolas"),
            FontSize = 26,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(0, 4, 0, 0),
            RenderTransformOrigin = new Point(0, 0.5)
        };
        code.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        codeBlock = code;
        left.Children.Add(code);
        grid.Children.Add(left);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(MakeIconButton(Icons.Copy(),                  "Copy",   () => { FlashBorder(card); onCopy(account); }));
        actions.Children.Add(MakeIconButton(Icons.Edit(),                  "Edit",   () => onEdit(account)));
        actions.Children.Add(MakeIconButton(Icons.Delete("DangerBrush"),   "Delete", () => onDelete(account), danger: true));
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    public static string FormatCode(string code) => $"{code[..3]} {code[3..]}";

    // Subtle pulse on the code text whenever a new TOTP cycle arrives.
    public static void AnimateCodeRefresh(TextBlock code)
    {
        var fade = new DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)),
            new CubicEase { EasingMode = EasingMode.EaseIn }));
        code.BeginAnimation(UIElement.OpacityProperty, fade);

        if (code.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            code.RenderTransform = scale;
        }
        var pulse = new DoubleAnimationUsingKeyFrames();
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(560)),
            new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 3.0 }));
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    private static void FlashBorder(Border card)
    {
        card.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            card.SetResourceReference(Border.BorderBrushProperty,
                card.IsMouseOver ? "AccentSoftBrush" : "LineSoftBrush");
        };
        timer.Start();
    }

    private static Button MakeIconButton(UIElement glyph, string tip, Action handler, bool danger = false)
    {
        var btn = new Button
        {
            Content = glyph,
            ToolTip = tip,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(0),
            Width = 30,
            Height = 30,
            MinHeight = 30,
            Cursor = Cursors.Hand,
            Template = IconButtonTemplate()
        };
        btn.SetResourceReference(Button.BackgroundProperty, danger ? "DangerSoftBrush" : "ButtonBgBrush");
        btn.SetResourceReference(Button.BorderBrushProperty, danger ? "DangerBrush" : "ButtonBorderBrush");
        btn.Click += (_, _) => handler();
        return btn;
    }

    private static ControlTemplate IconButtonTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chrome";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(FrameworkElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
        border.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1, 1));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty,
            new System.Windows.Data.Binding("Foreground") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.AppendChild(content);
        tpl.VisualTree = border;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82, "Chrome"));
        tpl.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.EnterActions.Add(BuildScaleStoryboard(0.82, 80, new CubicEase { EasingMode = EasingMode.EaseOut }));
        pressedTrigger.ExitActions.Add(BuildScaleStoryboard(1.0, 460, new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 2.8 }));
        tpl.Triggers.Add(pressedTrigger);
        return tpl;
    }

    private static BeginStoryboard BuildScaleStoryboard(double to, double ms, IEasingFunction ease)
    {
        var sb = new Storyboard();
        foreach (var prop in new[] { "(UIElement.RenderTransform).(ScaleTransform.ScaleX)",
                                     "(UIElement.RenderTransform).(ScaleTransform.ScaleY)" })
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            Storyboard.SetTargetName(anim, "Chrome");
            Storyboard.SetTargetProperty(anim, new PropertyPath(prop));
            sb.Children.Add(anim);
        }
        return new BeginStoryboard { Storyboard = sb };
    }

    private static bool IsButtonAncestor(DependencyObject element)
    {
        var node = element;
        while (node is not null)
        {
            if (node is Button) return true;
            node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    private static void AnimateIn(UIElement element, TranslateTransform translate)
    {
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut });
        translate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(560)) { EasingFunction = Bounce });
    }

    private static void AnimateTo(IAnimatable target, DependencyProperty property, double to, double ms, IEasingFunction ease)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        target.BeginAnimation(property, anim);
    }

}
