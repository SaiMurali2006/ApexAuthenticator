<div align="center">
  <h1>ApexAuth</h1>
  <p><strong>A sleek, encrypted Windows authenticator built for the system tray.</strong></p>
  <p>
    <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-7C73FF?style=for-the-badge">
    <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge">
    <img alt="Theme" src="https://img.shields.io/badge/theme-light%20%7C%20dark%20%7C%20system-9DB0FF?style=for-the-badge">
    <img alt="Storage" src="https://img.shields.io/badge/vault-encrypted-111111?style=for-the-badge">
  </p>
</div>

## Overview

ApexAuth is a lightweight, local-first TOTP authenticator for Windows. It lives in the system tray, opens into a compact desktop window, and stores every secret inside an encrypted local vault. There are no accounts, no cloud calls, and no network dependencies.

The interface is built for taste: rounded surfaces, glowing accents, large readable codes, click-to-copy cards, and a customisable accent that follows your mood — or your system's theme.

## Highlights

| Area | Details |
| --- | --- |
| Tray workflow | Open, lock, or exit directly from the Windows system tray. |
| Local TOTP | Generates 6-digit TOTP codes locally from Base32 secrets (RFC 6238). |
| Master lock | Requires a master password before decrypting the vault. |
| Key stretching | Uses `scrypt` (N=32768, R=8, P=1) with a random per-vault salt. |
| Vault encryption | Encrypts persisted account data with Fernet-style AES-256-CBC + HMAC-SHA256. |
| Backups | Exports and imports password-protected `.vault-backup` files. |
| CRUD | Add, edit, delete, copy account codes from a refined desktop UI. |
| Theming | Light, Dark, or Follow System — with a user-defined accent (hex or preset). |
| Live updates | Theme changes apply instantly; even the tray icon recolours to your accent. |

## Theming

Click the **A** badge in the top-left corner of the window. A popover appears with:

- **Appearance** — segmented control for *Light*, *Dark*, or *System* (follows the Windows `AppsUseLightTheme` setting in real time).
- **Accent** — eight curated preset swatches plus a hex input. Type `#7C73FF`, hit Enter, and every button, progress bar, badge, glow, drop-shadow, and tray icon updates instantly.

Settings persist to `%APPDATA%\ApexAuth\theme.json`:

```json
{
  "Mode": "System",
  "Accent": "#7C73FF"
}
```

Defaults fall back to dark + signature violet on first run.

## Security Model

- Secrets are written to disk only inside `%APPDATA%\ApexAuth\data.vault`.
- The vault file contains only the KDF parameters, a per-vault salt, and authenticated ciphertext.
- TOTP secrets are decrypted only after unlock and held in memory for the active session.
- Locking, hiding to tray, or exiting clears the in-memory vault state and zeros the session key.
- Backups are independently encrypted with the password or backup key entered during export.
- The master password is **never** persisted in any form.

## Run Locally

```powershell
dotnet run
```

The vault is stored at:

```text
%APPDATA%\ApexAuth\data.vault
```

The theme settings are stored at:

```text
%APPDATA%\ApexAuth\theme.json
```

## Package

Framework-dependent build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Self-contained single-file build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The packaged app will be under:

```text
bin\Release\net8.0-windows\win-x64\publish
```

## Project Structure

```text
ApexAuth
|-- Crypto/                scrypt + Fernet-style encryption
|-- Models/                vault and account models
|-- Services/              TOTP, vault storage, icon generation, theme service
|   |-- ThemeService.cs    palette, light/dark/system, accent, theme.json
|   |-- IconFactory.cs     accent-aware procedural icons
|   |-- TrayService.cs     system tray integration
|   |-- TotpService.cs     RFC 6238 TOTP + Base32
|   `-- VaultService.cs    unlock / save / export / import
|-- UI/                    dialogs, account cards, theme popup
|   |-- DialogBase.cs      shared chrome
|   |-- AccountCardFactory build account cards
|   |-- ThemePopup.cs      logo popover (mode + accent)
|   |-- AccountDialog.cs   add/edit account
|   |-- DarkMessageDialog  confirm / error
|   `-- PasswordPrompt.cs  password input
|-- App.xaml               default palette + button & input styles
|-- App.xaml.cs            startup; initialises ThemeService
|-- MainWindow.xaml        primary tray app interface
`-- MainWindow.xaml.cs     orchestration (timer, vault, theme plumbing)
```

## Notes

ApexAuth is local desktop software. Protect your Windows account, use a strong master password, and be intentional about syncing the vault or backup files to cloud storage.
