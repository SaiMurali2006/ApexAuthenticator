using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ApexAuth.Models;
using ApexAuth.Services;
using ApexAuth.UI;
using Microsoft.Win32;

namespace ApexAuth;

public partial class MainWindow : Window
{
    private const int AutoLockMinutes = 5;
    private const int ClipboardClearSeconds = 30;
    private const int UnlockBackoffStartFailures = 5;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private readonly VaultService _vault = new();
    private readonly TrayService _tray;
    private readonly DispatcherTimer _timer              = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _toastTimer         = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly DispatcherTimer _idleTimer          = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer _clipboardTimer    = new() { Interval = TimeSpan.FromSeconds(ClipboardClearSeconds) };
    private readonly DispatcherTimer _unlockBackoffTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<Guid, (AuthAccount Account, TextBlock Block)> _codeBlocks = new();
    private ThemePopup? _themePopup;
    private long _lastTotpCounter = -1;
    private bool _reallyClose;
    private bool _syncingPasswordFields;
    private DateTimeOffset _lastInteraction = DateTimeOffset.UtcNow;
    private string? _clipboardCookie;
    private int _failedUnlockAttempts;
    private int _backoffSecondsRemaining;

    public MainWindow()
    {
        InitializeComponent();
        RefreshWindowIcon();
        ConfigureLockScreen();
        Opacity = 0;
        RenderTransformOrigin = new Point(0.5, 1);

        _timer.Tick               += (_, _) => RefreshCodes();
        _toastTimer.Tick          += (_, _) => { AnimateToastOut(); _toastTimer.Stop(); };
        _idleTimer.Tick           += (_, _) => CheckIdleAutoLock();
        _clipboardTimer.Tick      += (_, _) => ClearClipboardIfOurs();
        _unlockBackoffTimer.Tick  += (_, _) => TickUnlockBackoff();

        PreviewMouseMove   += (_, _) => MarkInteraction();
        PreviewMouseDown   += (_, _) => MarkInteraction();
        PreviewKeyDown     += (_, _) => MarkInteraction();

        SystemEvents.SessionSwitch += OnSessionSwitch;

        _tray = new TrayService(
            onShow: ShowFromTray,
            onLock: LockVault,
            onExit: () => { _reallyClose = true; Close(); });

        ThemeService.Current.ThemeChanged += OnThemeChanged;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        };
    }

    private void MarkInteraction() => _lastInteraction = DateTimeOffset.UtcNow;

    private void CheckIdleAutoLock()
    {
        if (VaultPanel.Visibility != Visibility.Visible) return;
        var idle = DateTimeOffset.UtcNow - _lastInteraction;
        if (idle.TotalMinutes >= AutoLockMinutes) LockVault();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.SessionLogoff)
        {
            Dispatcher.Invoke(() =>
            {
                if (VaultPanel.Visibility == Visibility.Visible) LockVault();
            });
        }
    }

    // ── Startup ──────────────────────────────────────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAboveTray();
        AnimateWindowIn();
    }

    private void ConfigureLockScreen()
    {
        var isNew = !_vault.Exists;
        LockTitle.Text    = isNew ? "Create master lock" : "Unlock vault";
        LockSubtitle.Text = isNew
            ? "Create a master password. It will stretch through scrypt before anything touches disk."
            : "Enter your master password to decrypt codes in memory.";
        ConfirmPasswordGrid.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
        UnlockButton.Content           = isNew ? "Create Vault" : "Unlock";
        MasterPasswordBox.Focus();
    }

    // ── Theme ────────────────────────────────────────────────────────────────

    private void LogoButton_Click(object sender, RoutedEventArgs e)
    {
        _themePopup ??= new ThemePopup(LogoButton);
        _themePopup.Toggle();
    }

    private void OnThemeChanged()
    {
        RefreshWindowIcon();
        _tray.RefreshIcon();
    }

    private void RefreshWindowIcon() => Icon = IconFactory.CreateWindowIcon();

    // ── Lock / Unlock ─────────────────────────────────────────────────────────

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => UnlockOrCreate();
    private void MasterPasswordBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) UnlockOrCreate(); }

    private void UnlockOrCreate()
    {
        LockError.Text = "";
        if (_backoffSecondsRemaining > 0) return;

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
            _failedUnlockAttempts = 0;
            ClearPasswordFields();
            LockedPanel.Visibility = Visibility.Collapsed;
            VaultPanel.Visibility  = Visibility.Visible;
            MarkInteraction();
            _timer.Start();
            _idleTimer.Start();
            RenderAccounts();
            RefreshCodes();
            ShowToast("Vault unlocked");
        }
        catch (CryptographicException)
        {
            HandleUnlockFailure("Incorrect password or corrupted vault.");
        }
        catch (InvalidDataException)
        {
            HandleUnlockFailure("Incorrect password or corrupted vault.");
        }
        catch (Exception ex)
        {
            LockError.Text = ex.Message;
        }
    }

    private void HandleUnlockFailure(string message)
    {
        _failedUnlockAttempts++;
        LockError.Text = message;
        if (_failedUnlockAttempts >= UnlockBackoffStartFailures)
        {
            var penalty = Math.Min(60, 1 << Math.Min(6, _failedUnlockAttempts - UnlockBackoffStartFailures));
            StartUnlockBackoff(penalty);
        }
    }

    private void StartUnlockBackoff(int seconds)
    {
        _backoffSecondsRemaining = seconds;
        UnlockButton.IsEnabled = false;
        UpdateBackoffButtonText();
        _unlockBackoffTimer.Start();
    }

    private void TickUnlockBackoff()
    {
        _backoffSecondsRemaining--;
        if (_backoffSecondsRemaining <= 0)
        {
            _unlockBackoffTimer.Stop();
            UnlockButton.IsEnabled = true;
            UnlockButton.Content = _vault.Exists ? "Unlock" : "Create Vault";
            return;
        }
        UpdateBackoffButtonText();
    }

    private void UpdateBackoffButtonText() =>
        UnlockButton.Content = $"Wait {_backoffSecondsRemaining}s";

    private void LockButton_Click(object sender, RoutedEventArgs e) => LockVault();

    private void LockVault()
    {
        _timer.Stop();
        _idleTimer.Stop();
        _clipboardTimer.Stop();
        ClearClipboardIfOurs();
        _codeBlocks.Clear();
        _lastTotpCounter = -1;
        _vault.Lock();
        AccountsPanel.Children.Clear();
        VaultPanel.Visibility  = Visibility.Collapsed;
        LockedPanel.Visibility = Visibility.Visible;
        ConfigureLockScreen();
        ShowFromTray();
    }

    // ── Account list ──────────────────────────────────────────────────────────

    private void RenderAccounts()
    {
        _codeBlocks.Clear();
        _lastTotpCounter = -1;
        AccountsPanel.Children.Clear();

        if (_vault.Payload.Accounts.Count == 0)
        {
            AccountsPanel.Children.Add(BuildEmptyState());
            return;
        }

        foreach (var account in _vault.Payload.Accounts.OrderBy(x => x.Label))
        {
            var card = AccountCardFactory.Create(account, CopyCode, EditAccount, DeleteAccount, out var block);
            _codeBlocks[account.Id] = (account, block);
            AccountsPanel.Children.Add(card);
        }
    }

    private static UIElement BuildEmptyState()
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 56, 0, 0)
        };

        var badge = new Border
        {
            Width = 56, Height = 56,
            CornerRadius = new CornerRadius(16),
            Background = (Brush)Application.Current.FindResource("AccentSubtleBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("LineSoftBrush"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var glyph = UI.Icons.EmptyState("AccentBrush", display: 28);
        if (glyph is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Center;
            fe.VerticalAlignment = VerticalAlignment.Center;
        }
        badge.Child = glyph;
        stack.Children.Add(badge);

        stack.Children.Add(new TextBlock
        {
            Text = "No accounts yet",
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            FontSize = 16,
            FontWeight = FontWeights.Black,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Tap + Add to import your first TOTP secret.",
            Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        });
        return stack;
    }

    // Only recomputes TOTP codes at 30-second boundaries; progress bar updates every tick.
    private void RefreshCodes()
    {
        if (VaultPanel.Visibility != Visibility.Visible) return;
        if (!IsVisible) return;

        var now = DateTimeOffset.UtcNow;
        CycleProgress.Value = TotpService.CycleProgress(now) * 100;
        CycleText.Text      = $"{TotpService.SecondsRemaining(now)} seconds until refresh";

        var counter = now.ToUnixTimeSeconds() / 30;
        if (counter == _lastTotpCounter) return;
        var firstRender = _lastTotpCounter == -1;
        _lastTotpCounter = counter;

        foreach (var (account, block) in _codeBlocks.Values)
        {
            block.Text = AccountCardFactory.FormatCode(TotpService.GetCode(account.Secret, now));
            if (!firstRender) AccountCardFactory.AnimateCodeRefresh(block);
        }
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
        account.Label  = edited.Label;
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

    private void CopyCode(AuthAccount account)
    {
        var code = TotpService.GetCode(account.Secret);
        SetClipboardSensitive(code);
        ShowToast($"Copied {account.Label}");
    }

    private void SetClipboardSensitive(string text)
    {
        var data = new DataObject();
        data.SetText(text);
        data.SetData("ExcludeClipboardContentFromMonitors", true);
        data.SetData("CanIncludeInClipboardHistory", false);
        try
        {
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (COMException)
        {
            Clipboard.SetText(text);
        }
        _clipboardCookie = text;
        _clipboardTimer.Stop();
        _clipboardTimer.Start();
    }

    private void ClearClipboardIfOurs()
    {
        _clipboardTimer.Stop();
        if (_clipboardCookie is null) return;
        try
        {
            if (Clipboard.ContainsText() && Clipboard.GetText() == _clipboardCookie)
                Clipboard.Clear();
        }
        catch (COMException) { /* clipboard locked by another app — give up */ }
        _clipboardCookie = null;
    }

    // ── Import / Export ───────────────────────────────────────────────────────

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordPrompt.Show(this, "Backup key", "Use your master password or enter a new backup key.");
        if (password is null) return;
        var dialog = new SaveFileDialog
        {
            Filter   = "ApexAuth backup (*.vault-backup)|*.vault-backup",
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

    // ── Window / tray plumbing ────────────────────────────────────────────────

    private void ShowFromTray()
    {
        PositionAboveTray();
        Show();
        WindowState = WindowState.Normal;
        AnimateWindowIn();
        Activate();
        if (VaultPanel.Visibility == Visibility.Visible && !_timer.IsEnabled)
        {
            MarkInteraction();
            _timer.Start();
            RefreshCodes();
        }
    }

    private void PositionAboveTray()
    {
        var work = SystemParameters.WorkArea;
        Left = Math.Max(work.Left + 12, work.Right  - ActualWidth  - 18);
        Top  = Math.Max(work.Top  + 12, work.Bottom - ActualHeight - 18);
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Hide();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose) { e.Cancel = true; _timer.Stop(); Hide(); return; }
        _idleTimer.Stop();
        _clipboardTimer.Stop();
        _unlockBackoffTimer.Stop();
        ClearClipboardIfOurs();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _tray.Dispose();
        _vault.Lock();
        Application.Current.Shutdown();
    }

    // ── Animations ────────────────────────────────────────────────────────────

    private void AnimateWindowIn()
    {
        WindowScale.ScaleX = WindowScale.ScaleY = 0.92;
        Opacity = 0;
        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
        var fade = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)) { EasingFunction = fade });
        WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(320)) { EasingFunction = pop });
        WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(320)) { EasingFunction = pop });
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Visibility = Visibility.Visible;
        Toast.Opacity = 0;

        // Build a fresh transform every call so animations start from a known state.
        var translate = new TranslateTransform(0, 14);
        var scale     = new ScaleTransform(0.92, 0.92);
        var group     = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        Toast.RenderTransform = group;

        var pop  = new BackEase  { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
        var fade = new CubicEase { EasingMode = EasingMode.EaseOut };

        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = fade });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { EasingFunction = pop });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(320)) { EasingFunction = pop });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(320)) { EasingFunction = pop });

        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void AnimateToastOut()
    {
        if (Toast.Visibility != Visibility.Visible) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fadeAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
        fadeAnim.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
        Toast.BeginAnimation(OpacityProperty, fadeAnim);

        if (Toast.RenderTransform is TransformGroup tg)
        {
            var translate = tg.Children[1] as TranslateTransform;
            translate?.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        }
    }

    // ── Password field helpers ────────────────────────────────────────────────

    private string GetMasterPassword() =>
        MasterPasswordTextBox.Visibility == Visibility.Visible
            ? MasterPasswordTextBox.Text
            : MasterPasswordBox.Password;

    private string GetConfirmPassword() =>
        ConfirmPasswordTextBox.Visibility == Visibility.Visible
            ? ConfirmPasswordTextBox.Text
            : ConfirmPasswordBox.Password;

    private void ClearPasswordFields()
    {
        _syncingPasswordFields = true;
        MasterPasswordBox.Clear(); MasterPasswordTextBox.Clear();
        ConfirmPasswordBox.Clear(); ConfirmPasswordTextBox.Clear();
        _syncingPasswordFields = false;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPasswordFields) return;
        _syncingPasswordFields = true;
        if (MasterPasswordTextBox.Visibility == Visibility.Visible)
            MasterPasswordTextBox.Text = MasterPasswordBox.Password;
        if (ConfirmPasswordTextBox.Visibility == Visibility.Visible)
            ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
        _syncingPasswordFields = false;
    }

    private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingPasswordFields) return;
        _syncingPasswordFields = true;
        if (MasterPasswordTextBox.Visibility == Visibility.Visible)
            MasterPasswordBox.Password = MasterPasswordTextBox.Text;
        if (ConfirmPasswordTextBox.Visibility == Visibility.Visible)
            ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
        _syncingPasswordFields = false;
    }

    private void MasterRevealButton_Click(object sender, RoutedEventArgs e) =>
        ToggleReveal(MasterPasswordBox, MasterPasswordTextBox, MasterEyeClosed, MasterEyeOpen, MasterEyePupil);

    private void ConfirmRevealButton_Click(object sender, RoutedEventArgs e) =>
        ToggleReveal(ConfirmPasswordBox, ConfirmPasswordTextBox, ConfirmEyeClosed, ConfirmEyeOpen, ConfirmEyePupil);

    private void ToggleReveal(PasswordBox box, TextBox text, UIElement closed, UIElement open, UIElement pupil)
    {
        if (text.Visibility == Visibility.Visible)
        {
            _syncingPasswordFields = true;
            box.Password = text.Text;
            text.Clear();
            _syncingPasswordFields = false;
            text.Visibility = Visibility.Collapsed;
            box.Visibility = Visibility.Visible;
            closed.Visibility = Visibility.Visible;
            open.Visibility = pupil.Visibility = Visibility.Collapsed;
            box.Focus();
        }
        else
        {
            _syncingPasswordFields = true;
            text.Text = box.Password;
            _syncingPasswordFields = false;
            box.Visibility = Visibility.Collapsed;
            text.Visibility = Visibility.Visible;
            closed.Visibility = Visibility.Collapsed;
            open.Visibility = pupil.Visibility = Visibility.Visible;
            text.Focus();
            text.CaretIndex = text.Text.Length;
        }
    }
}
