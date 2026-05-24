using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ApexAuth.Models;
using ApexAuth.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ApexAuth;

public partial class MainWindow : Window
{
    private readonly VaultService _vault = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Drawing.Icon _appIcon;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _reallyClose;
    private bool _syncingPasswordFields;

    public MainWindow()
    {
        InitializeComponent();
        Icon = IconFactory.CreateWindowIcon();
        ConfigureLockScreen();
        Opacity = 0;
        RenderTransformOrigin = new Point(0.5, 1);

        _timer.Tick += (_, _) => RefreshCodes();
        _toastTimer.Tick += (_, _) =>
        {
            AnimateToastOut();
            _toastTimer.Stop();
        };

        _appIcon = IconFactory.CreateTrayIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "ApexAuth",
            Icon = _appIcon,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open ApexAuth", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Lock", null, (_, _) => Dispatcher.Invoke(LockVault));
        menu.Items.Add("Exit", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _reallyClose = true;
                Close();
            });
        });
        return menu;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAboveTray();
        AnimateWindowIn();
    }

    private void ConfigureLockScreen()
    {
        var isNew = !_vault.Exists;
        LockTitle.Text = isNew ? "Create master lock" : "Unlock vault";
        LockSubtitle.Text = isNew
            ? "Create a master password. It will stretch through scrypt before anything touches disk."
            : "Enter your master password to decrypt codes in memory.";
        ConfirmPasswordGrid.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
        UnlockButton.Content = isNew ? "Create Vault" : "Unlock";
        MasterPasswordBox.Focus();
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => UnlockOrCreate();

    private void MasterPasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) UnlockOrCreate();
    }

    private void UnlockOrCreate()
    {
        LockError.Text = "";
        var password = GetMasterPassword();
        if (password.Length < 10)
        {
            LockError.Text = "Use at least 10 characters for the master password.";
            return;
        }

        try
        {
            if (!_vault.Exists)
            {
                if (password != GetConfirmPassword())
                {
                    LockError.Text = "The confirmation password does not match.";
                    return;
                }
                _vault.Create(password);
            }
            else
            {
                _vault.Unlock(password);
            }

            MasterPasswordBox.Clear();
            MasterPasswordTextBox.Clear();
            ConfirmPasswordBox.Clear();
            ConfirmPasswordTextBox.Clear();
            LockedPanel.Visibility = Visibility.Collapsed;
            VaultPanel.Visibility = Visibility.Visible;
            _timer.Start();
            RenderAccounts();
            RefreshCodes();
            ShowToast("Vault unlocked");
        }
        catch (Exception ex)
        {
            LockError.Text = ex.Message;
        }
    }

    private void RefreshCodes()
    {
        if (VaultPanel.Visibility != Visibility.Visible) return;

        CycleProgress.Value = TotpService.CycleProgress() * 100;
        CycleText.Text = $"{TotpService.SecondsRemaining()} seconds until refresh";

        foreach (var child in AccountsPanel.Children.OfType<Border>())
        {
            if (child.Tag is not AuthAccount account) continue;
            if (child.FindName("CodeText") is TextBlock codeText)
                codeText.Text = FormatCode(TotpService.GetCode(account.Secret));
        }
    }

    private string GetMasterPassword()
    {
        return MasterPasswordTextBox.Visibility == Visibility.Visible
            ? MasterPasswordTextBox.Text
            : MasterPasswordBox.Password;
    }

    private string GetConfirmPassword()
    {
        return ConfirmPasswordTextBox.Visibility == Visibility.Visible
            ? ConfirmPasswordTextBox.Text
            : ConfirmPasswordBox.Password;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPasswordFields) return;
        _syncingPasswordFields = true;
        MasterPasswordTextBox.Text = MasterPasswordBox.Password;
        ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
        _syncingPasswordFields = false;
    }

    private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingPasswordFields) return;
        _syncingPasswordFields = true;
        MasterPasswordBox.Password = MasterPasswordTextBox.Text;
        ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
        _syncingPasswordFields = false;
    }

    private void MasterRevealButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordReveal(
            MasterPasswordBox,
            MasterPasswordTextBox,
            MasterEyeClosed,
            MasterEyeOpen,
            MasterEyePupil,
            MasterEyeGlint);
    }

    private void ConfirmRevealButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePasswordReveal(
            ConfirmPasswordBox,
            ConfirmPasswordTextBox,
            ConfirmEyeClosed,
            ConfirmEyeOpen,
            ConfirmEyePupil,
            ConfirmEyeGlint);
    }

    private static void TogglePasswordReveal(
        PasswordBox passwordBox,
        TextBox textBox,
        UIElement closedEye,
        UIElement openEye,
        UIElement pupil,
        UIElement glint)
    {
        var showing = textBox.Visibility == Visibility.Visible;
        if (showing)
        {
            passwordBox.Password = textBox.Text;
            textBox.Visibility = Visibility.Collapsed;
            passwordBox.Visibility = Visibility.Visible;
            closedEye.Visibility = Visibility.Visible;
            openEye.Visibility = Visibility.Collapsed;
            pupil.Visibility = Visibility.Collapsed;
            glint.Visibility = Visibility.Collapsed;
            passwordBox.Focus();
            return;
        }

        textBox.Text = passwordBox.Password;
        passwordBox.Visibility = Visibility.Collapsed;
        textBox.Visibility = Visibility.Visible;
        closedEye.Visibility = Visibility.Collapsed;
        openEye.Visibility = Visibility.Visible;
        pupil.Visibility = Visibility.Visible;
        glint.Visibility = Visibility.Visible;
        textBox.Focus();
        textBox.CaretIndex = textBox.Text.Length;
    }

    private void RenderAccounts()
    {
        AccountsPanel.Children.Clear();
        if (_vault.Payload.Accounts.Count == 0)
        {
            AccountsPanel.Children.Add(new TextBlock
            {
                Text = "No accounts yet. Add your first TOTP secret.",
                Foreground = (Brush)FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
                Margin = new Thickness(0, 42, 0, 0)
            });
            return;
        }

        foreach (var account in _vault.Payload.Accounts.OrderBy(x => x.Label))
            AccountsPanel.Children.Add(CreateAccountCard(account));
    }

    private Border CreateAccountCard(AuthAccount account)
    {
        var card = new Border
        {
            Tag = account,
            Background = (Brush)FindResource("CardBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 0, 9),
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, 8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(61, 56, 232),
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.2
            }
        };
        card.Loaded += (_, _) => AnimateElementIn(card);
        NameScope.SetNameScope(card, new NameScope());

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = account.Label,
            Foreground = (Brush)FindResource("TextBrush"),
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(0, 0, 0, 2)
        });
        var code = new TextBlock
        {
            Name = "CodeText",
            Text = FormatCode(TotpService.GetCode(account.Secret)),
            Foreground = (Brush)FindResource("AccentBrush"),
            FontFamily = new FontFamily("Cascadia Code, Cascadia Mono, Consolas"),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        card.RegisterName(code.Name, code);
        left.Children.Add(code);
        grid.Children.Add(left);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(ActionButton("Copy", (_, _) => CopyCode(account)));
        actions.Children.Add(ActionButton("Edit", (_, _) => EditAccount(account)));
        actions.Children.Add(ActionButton("Del", (_, _) => DeleteAccount(account), true));
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        card.Child = grid;
        return card;
    }

    private System.Windows.Controls.Button ActionButton(string text, RoutedEventHandler handler, bool danger = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 5, 8, 5),
            Background = danger ? new SolidColorBrush(Color.FromRgb(58, 22, 35)) : (Brush)FindResource("AccentSoftBrush"),
            BorderBrush = danger ? (Brush)FindResource("DangerBrush") : (Brush)FindResource("LineBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            Style = (Style)FindResource("GhostButton")
        };
        button.Click += handler;
        return button;
    }

    private static string FormatCode(string code) => $"{code[..3]} {code[3..]}";

    private void CopyCode(AuthAccount account)
    {
        Clipboard.SetText(TotpService.GetCode(account.Secret));
        ShowToast($"Copied {account.Label}");
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var account = AccountDialog.Show(this);
        if (account is null) return;
        _vault.Payload.Accounts.Add(account);
        _vault.Save();
        RenderAccounts();
        RefreshCodes();
        ShowToast("Account added");
    }

    private void EditAccount(AuthAccount account)
    {
        var edited = AccountDialog.Show(this, account);
        if (edited is null) return;
        account.Label = edited.Label;
        account.Secret = edited.Secret;
        _vault.Save();
        RenderAccounts();
        RefreshCodes();
        ShowToast("Account updated");
    }

    private void DeleteAccount(AuthAccount account)
    {
        if (!DarkMessageDialog.Confirm(this, "Delete account", $"Delete {account.Label}? This cannot be undone."))
            return;

        _vault.Payload.Accounts.Remove(account);
        _vault.Save();
        RenderAccounts();
        ShowToast("Account deleted");
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordPrompt.Show(this, "Backup key", "Use your master password or enter a new backup key.");
        if (password is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "ApexAuth backup (*.vault-backup)|*.vault-backup",
            FileName = $"apexauth-{DateTime.Now:yyyyMMdd-HHmm}.vault-backup"
        };
        if (dialog.ShowDialog(this) != true) return;

        _vault.ExportBackup(dialog.FileName, password);
        ShowToast("Backup exported");
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "ApexAuth backup (*.vault-backup)|*.vault-backup|Vault files (*.vault)|*.vault" };
        if (dialog.ShowDialog(this) != true) return;

        var password = PasswordPrompt.Show(this, "Import backup", "Enter the password used to encrypt this backup.");
        if (password is null) return;

        try
        {
            var count = _vault.ImportBackup(dialog.FileName, password);
            RenderAccounts();
            RefreshCodes();
            ShowToast($"Imported {count} account(s)");
        }
        catch (Exception ex)
        {
            DarkMessageDialog.Error(this, "Import failed", ex.Message);
        }
    }

    private void LockButton_Click(object sender, RoutedEventArgs e) => LockVault();

    private void LockVault()
    {
        _timer.Stop();
        _vault.Lock();
        AccountsPanel.Children.Clear();
        VaultPanel.Visibility = Visibility.Collapsed;
        LockedPanel.Visibility = Visibility.Visible;
        ConfigureLockScreen();
        ShowFromTray();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Visibility = Visibility.Visible;
        Toast.Opacity = 0;
        Toast.RenderTransformOrigin = new Point(0.5, 1);
        Toast.RenderTransform = new TranslateTransform(0, 8);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
        ((TranslateTransform)Toast.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ShowFromTray()
    {
        PositionAboveTray();
        Show();
        WindowState = WindowState.Normal;
        AnimateWindowIn();
        Activate();
    }

    private void PositionAboveTray()
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Max(work.Left + 12, work.Right - ActualWidth - 18);
        Top = Math.Max(work.Top + 12, work.Bottom - ActualHeight - 18);
    }

    private void AnimateWindowIn()
    {
        WindowScale.ScaleX = 0.96;
        WindowScale.ScaleY = 0.96;
        Opacity = 0;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(210)) { EasingFunction = ease });
        WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(210)) { EasingFunction = ease });
    }

    private void AnimateToastOut()
    {
        if (Toast.Visibility != Visibility.Visible) return;

        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
        Toast.BeginAnimation(OpacityProperty, animation);
    }

    private static void AnimateElementIn(UIElement element)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });

        if (element.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Minimized : WindowState.Normal;
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _appIcon.Dispose();
        _vault.Lock();
        Application.Current.Shutdown();
    }
}
