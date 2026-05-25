using System.Windows;
using System.Windows.Controls;
using ApexAuth.Models;
using ApexAuth.Services;

namespace ApexAuth.UI;

public sealed class AccountDialog : DialogBase
{
    private readonly TextBox _label = new();
    private readonly TextBox _secret = new();
    private readonly TextBlock _error = new();

    private AccountDialog(AuthAccount? account)
        : base(account is null ? "Add account" : "Edit account")
    {
        _label.Text = account?.Label ?? "";
        _secret.Text = account?.Secret ?? "";

        Root.Children.Add(new TextBlock
        {
            Text = "LABEL",
            FontSize = 10,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Res("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        Root.Children.Add(_label);
        Root.Children.Add(new TextBlock
        {
            Text = "BASE32 SECRET",
            FontSize = 10,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Res("MutedBrush"),
            Margin = new Thickness(0, 12, 0, 6)
        });
        Root.Children.Add(_secret);

        _error.Foreground = Res("DangerBrush");
        _error.FontSize = 12;
        _error.Margin = new Thickness(0, 8, 0, 0);
        Root.Children.Add(_error);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cancel = new Button { Content = "Cancel", Style = GetStyle("GhostButton"), Margin = new Thickness(0, 0, 4, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button
        {
            Content = account is null ? "Add" : "Save",
            Style = GetStyle("PrimaryButton"),
            Margin = new Thickness(4, 0, 0, 0)
        };
        save.Click += (_, _) => Save();
        Grid.SetColumn(save, 1);
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Root.Children.Add(actions);
    }

    public AuthAccount? Account { get; private set; }

    public static AuthAccount? Show(Window owner, AuthAccount? account = null)
    {
        var dialog = new AccountDialog(account) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Account : null;
    }

    private void Save()
    {
        var label = _label.Text.Trim();
        var secret = TotpService.NormalizeSecret(_secret.Text);
        if (string.IsNullOrWhiteSpace(label))
        {
            _error.Text = "Label is required.";
            return;
        }
        if (!TotpService.IsValidSecret(secret))
        {
            _error.Text = "Secret must be valid Base32.";
            return;
        }

        Account = new AuthAccount { Label = label, Secret = secret };
        DialogResult = true;
    }
}
