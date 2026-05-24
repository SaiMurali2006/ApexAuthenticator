using System;
using System.Windows;
using System.Windows.Controls;
using ApexAuth.Models;
using ApexAuth.Services;

namespace ApexAuth;

public sealed class AccountDialog : Window
{
    private readonly System.Windows.Controls.TextBox _label = new();
    private readonly System.Windows.Controls.TextBox _secret = new();
    private readonly TextBlock _error = new();

    private AccountDialog(AuthAccount? account)
    {
        Title = account is null ? "Add account" : "Edit account";
        Width = 330;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextBrush");

        _label.Text = account?.Label ?? "";
        _secret.Text = account?.Secret ?? "";

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
            Text = Title,
            FontSize = 19,
            FontWeight = FontWeights.Black,
            Margin = new Thickness(0, 0, 0, 5)
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
        root.Children.Add(new TextBlock { Text = "Label", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("MutedBrush"), Margin = new Thickness(0, 0, 0, 5) });
        root.Children.Add(_label);
        root.Children.Add(new TextBlock { Text = "Base32 secret", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("MutedBrush"), Margin = new Thickness(0, 10, 0, 5) });
        root.Children.Add(_secret);
        _error.Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("DangerBrush");
        _error.FontSize = 12;
        _error.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(_error);

        var actions = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cancel = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("GhostButton"), Margin = new Thickness(0, 0, 5, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = account is null ? "Add" : "Finish", Style = (Style)Application.Current.FindResource("PrimaryButton"), Margin = new Thickness(5, 0, 0, 0) };
        save.Click += (_, _) => Save();
        Grid.SetColumn(save, 1);
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        root.Children.Add(actions);
        shell.Child = root;
        Content = shell;
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
