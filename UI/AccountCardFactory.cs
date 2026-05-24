using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ApexAuth.Models;
using ApexAuth.Services;

namespace ApexAuth.UI;

public static class AccountCardFactory
{
    public static Border Create(
        AuthAccount account,
        Action<AuthAccount> onCopy,
        Action<AuthAccount> onEdit,
        Action<AuthAccount> onDelete,
        out TextBlock codeBlock)
    {
        var card = new Border
        {
            Tag = account,
            Background = Res("CardBrush"),
            BorderBrush = Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 0, 9),
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, 8),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(61, 56, 232),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.2
            }
        };
        card.Loaded += (_, _) => AnimateIn(card);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = account.Label,
            Foreground = Res("TextBrush"),
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(0, 0, 0, 2)
        });

        var code = new TextBlock
        {
            Text = FormatCode(TotpService.GetCode(account.Secret)),
            Foreground = Res("AccentBrush"),
            FontFamily = new FontFamily("Cascadia Code, Cascadia Mono, Consolas"),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        codeBlock = code;
        left.Children.Add(code);
        grid.Children.Add(left);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(MakeButton("Copy", () => onCopy(account)));
        actions.Children.Add(MakeButton("Edit", () => onEdit(account)));
        actions.Children.Add(MakeButton("Del",  () => onDelete(account), danger: true));
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    public static string FormatCode(string code) => $"{code[..3]} {code[3..]}";

    private static Button MakeButton(string text, Action handler, bool danger = false)
    {
        var btn = new Button
        {
            Content = text,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 5, 8, 5),
            Background = danger
                ? new SolidColorBrush(Color.FromRgb(58, 22, 35))
                : Res("AccentSoftBrush"),
            BorderBrush = danger ? Res("DangerBrush") : Res("LineBrush"),
            Foreground = Res("TextBrush"),
            Style = (Style)Application.Current.FindResource("GhostButton")
        };
        btn.Click += (_, _) => handler();
        return btn;
    }

    private static void AnimateIn(UIElement element)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        if (element.RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
    }

    private static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
}
