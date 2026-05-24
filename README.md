<div align="center">
  <h1>ApexAuth</h1>
  <p><strong>A sleek encrypted Windows authenticator, built for the system tray.</strong></p>
  <p>
    <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-766CFF?style=for-the-badge">
    <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge">
    <img alt="Storage" src="https://img.shields.io/badge/vault-encrypted-111111?style=for-the-badge">
  </p>
</div>

## Overview

ApexAuth is a lightweight local-first TOTP authenticator for Windows. It lives in the system tray, opens into a compact darkmode desktop window, and stores secrets only inside an encrypted local vault.

The design leans into a premium black/violet glass aesthetic: rounded surfaces, glowing accents, large readable codes, and a custom ApexAuth identity across the window and tray.

## Highlights

| Area | Details |
| --- | --- |
| Tray workflow | Open, lock, or exit directly from the Windows system tray. |
| Local TOTP | Generates 6-digit TOTP codes locally from Base32 secrets. |
| Master lock | Requires a master password before decrypting the vault. |
| Key stretching | Uses `scrypt` with a random per-vault salt. |
| Vault encryption | Encrypts persisted account data with Fernet-style authenticated encryption. |
| Backups | Exports and imports password-protected `.vault-backup` files. |
| CRUD | Add, edit, delete, and copy account codes from a polished desktop UI. |

## Security Model

- Secrets are written to disk only inside `%APPDATA%\ApexAuth\data.vault`.
- The vault file contains KDF parameters, a salt, and encrypted ciphertext.
- TOTP secrets are decrypted only after unlock and kept in memory for the active session.
- Locking or exiting clears the in-memory vault state and session key material.
- Backups are independently encrypted with the password or backup key entered during export.

## Run Locally

```powershell
dotnet run
```

The vault is stored at:

```text
%APPDATA%\ApexAuth\data.vault
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
|-- Crypto/          scrypt + Fernet-style encryption
|-- Models/          vault and account models
|-- Services/        TOTP, vault storage, icon generation
|-- MainWindow.xaml  primary tray app interface
|-- App.xaml         shared darkmode theme
```

## Notes

ApexAuth is local desktop software. Protect your Windows account, use a strong master password, and be intentional about syncing the vault or backup files to cloud storage.
