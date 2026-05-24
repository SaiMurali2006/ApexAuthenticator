using System;
using System.Windows;
using System.Windows.Controls;

namespace ApexAuth;

public sealed class PasswordPrompt : Window
{
    private readonly PasswordBox _password = new();

    private PasswordPrompt(string title, string message)
    {
        Title = title;
        Width = 330;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextBrush");

        var shell = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("CardBrush"),
            BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18)
        };
        shell.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        };

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = FontWeights.Black,
            Margin = new Thickness(0, 0, 0, 6)
        });
        root.Children.Add(new Border
        {
            Width = 42,
            Height = 3,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (System.Windows.Media.Brush)Application.Current.FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        root.Children.Add(new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("MutedBrush"), Margin = new Thickness(0, 0, 0, 12) });
        root.Children.Add(_password);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cancel = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("GhostButton"), Margin = new Thickness(0, 0, 5, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "Continue", Style = (Style)Application.Current.FindResource("PrimaryButton"), Margin = new Thickness(5, 0, 0, 0) };
        ok.Click += (_, _) => DialogResult = _password.Password.Length > 0;
        Grid.SetColumn(ok, 1);
        actions.Children.Add(cancel);
        actions.Children.Add(ok);
        root.Children.Add(actions);
        shell.Child = root;
        Content = shell;
    }

    public static string? Show(Window owner, string title, string message)
    {
        var prompt = new PasswordPrompt(title, message) { Owner = owner };
        return prompt.ShowDialog() == true ? prompt._password.Password : null;
    }
}
