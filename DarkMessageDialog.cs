using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexAuth;

public sealed class DarkMessageDialog : Window
{
    private DarkMessageDialog(string title, string message, bool confirm)
    {
        Title = title;
        Width = 320;
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
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = FontWeights.Black,
            Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        root.Children.Add(new Border
        {
            Width = 42,
            Height = 3,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (System.Windows.Media.Brush)Application.Current.FindResource(confirm ? "DangerBrush" : "AccentBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        root.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("MutedBrush"),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var actions = new Grid { Margin = new Thickness(0, 2, 0, 0) };

        if (confirm)
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cancel = new Button
            {
                Content = "Cancel",
                Style = (Style)Application.Current.FindResource("GhostButton"),
                Margin = new Thickness(0, 0, 5, 0)
            };
            cancel.Click += (_, _) => DialogResult = false;
            actions.Children.Add(cancel);
        }
        else
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var primary = new Button
        {
            Content = confirm ? "Delete" : "OK",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Background = confirm
                ? (System.Windows.Media.Brush)Application.Current.FindResource("DangerBrush")
                : (System.Windows.Media.Brush)Application.Current.FindResource("AccentBrush"),
            Margin = confirm ? new Thickness(5, 0, 0, 0) : new Thickness(0)
        };
        primary.Click += (_, _) => DialogResult = true;
        Grid.SetColumn(primary, confirm ? 1 : 0);
        actions.Children.Add(primary);
        root.Children.Add(actions);
        shell.Child = root;
        Content = shell;
    }

    public static bool Confirm(Window owner, string title, string message)
    {
        var dialog = new DarkMessageDialog(title, message, true) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public static void Error(Window owner, string title, string message)
    {
        var dialog = new DarkMessageDialog(title, message, false) { Owner = owner };
        dialog.ShowDialog();
    }
}
