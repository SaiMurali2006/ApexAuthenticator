using System.Windows;
using System.Windows.Controls;

namespace ApexAuth.UI;

public sealed class DarkMessageDialog : DialogBase
{
    private DarkMessageDialog(string title, string message, bool confirm)
        : base(title, confirm ? "DangerBrush" : "AccentBrush", width: 320)
    {
        Root.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Res("MutedBrush"),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var actions = new Grid { Margin = new Thickness(0, 2, 0, 0) };

        if (confirm)
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var cancel = new Button { Content = "Cancel", Style = GetStyle("GhostButton"), Margin = new Thickness(0, 0, 5, 0) };
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
            Style = GetStyle("PrimaryButton"),
            Background = confirm ? Res("DangerBrush") : Res("AccentBrush"),
            Margin = confirm ? new Thickness(5, 0, 0, 0) : new Thickness(0)
        };
        primary.Click += (_, _) => DialogResult = true;
        Grid.SetColumn(primary, confirm ? 1 : 0);
        actions.Children.Add(primary);
        Root.Children.Add(actions);
    }

    public static bool Confirm(Window owner, string title, string message)
    {
        var dialog = new DarkMessageDialog(title, message, true) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public static void Error(Window owner, string title, string message)
    {
        new DarkMessageDialog(title, message, false) { Owner = owner }.ShowDialog();
    }
}
