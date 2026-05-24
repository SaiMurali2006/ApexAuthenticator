using System.Windows;
using System.Windows.Controls;

namespace ApexAuth.UI;

public sealed class PasswordPrompt : DialogBase
{
    private readonly PasswordBox _password = new();

    private PasswordPrompt(string title, string message) : base(title)
    {
        Root.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Res("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
        Root.Children.Add(_password);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cancel = new Button { Content = "Cancel", Style = GetStyle("GhostButton"), Margin = new Thickness(0, 0, 5, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "Continue", Style = GetStyle("PrimaryButton"), Margin = new Thickness(5, 0, 0, 0) };
        ok.Click += (_, _) => DialogResult = _password.Password.Length > 0;
        Grid.SetColumn(ok, 1);
        actions.Children.Add(cancel);
        actions.Children.Add(ok);
        Root.Children.Add(actions);
    }

    public static string? Show(Window owner, string title, string message)
    {
        var prompt = new PasswordPrompt(title, message) { Owner = owner };
        return prompt.ShowDialog() == true ? prompt._password.Password : null;
    }
}
