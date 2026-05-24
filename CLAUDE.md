# ApexAuth — CLAUDE.md

## What This Is
ApexAuth is a Windows WPF/.NET 8.0 TOTP authenticator that lives in the system tray. It encrypts all secrets locally with a master password and has no network dependencies. The executable is published as a single self-contained file.

## Build & Run

```powershell
# Debug run
dotnet run --project ApexAuth.csproj

# Release publish (self-contained single EXE)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Vault file location:** `%APPDATA%\ApexAuth\data.vault`

## Tech Stack
- .NET 8.0 + WPF (Windows-only)
- No external NuGet dependencies — all crypto is hand-implemented from specs
- `System.Security.Cryptography` for AES, HMAC, PBKDF2 primitives
- `System.Text.Json` for serialization

## Architecture

```
Crypto/         Fernet.cs (AES-256-CBC + HMAC-SHA256), Scrypt.cs (RFC 7914)
Models/         AuthAccount, VaultPayload (plaintext), VaultEnvelope (encrypted JSON on disk)
Services/       VaultService (unlock/save/export/import), TotpService (RFC 6238), IconFactory (procedural GDI+)
*.xaml/.cs      WPF UI — no MVVM framework, all UI built imperatively in C#
App.xaml        Global color resources and button/input styles (single source of truth for theming)
```

**No MVVM.** All UI is built imperatively — `new Button()`, `new TextBlock()`, etc.

## Cryptography Summary
| Layer | Implementation |
|-------|---------------|
| Key derivation | Scrypt (N=32768, R=8, P=1) — `Crypto/Scrypt.cs` |
| Encryption | Fernet-style AES-256-CBC + HMAC-SHA256 — `Crypto/Fernet.cs` |
| TOTP | RFC 6238 / HOTP with 30s window, HMAC-SHA1 — `Services/TotpService.cs` |
| Memory | `CryptographicOperations.ZeroMemory` on all sensitive buffers after use |

Session key held in `VaultService._sessionKey` (byte[]); zeroed on lock/exit. HMAC verification uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.

## Color / Theme System
All colors live in `App.xaml` as static `SolidColorBrush` resources:

| Resource Key | Current Value | Role |
|---|---|---|
| `BgBrush` | `#000000` | Window background |
| `PanelBrush` | `#111032` | Header/panels |
| `CardBrush` | `#15143A` | Account cards |
| `AccentBrush` | `#7C73FF` | Primary buttons, progress bar |
| `DangerBrush` | `#FF5470` | Delete actions |
| `TextBrush` | `#F7FBFF` | Primary text |
| `MutedBrush` | `#A4A0B8` | Secondary text |
| `LineBrush` | `#343060` | Borders |

**To change the color scheme: only edit `App.xaml`.** All UI components reference these resources — nothing is hardcoded elsewhere.

## Vault File Format
On disk (`data.vault`) is a JSON `VaultEnvelope`:
```json
{
  "App": "ApexAuth", "Version": 1, "Kdf": "scrypt",
  "Salt": "<base64-16-bytes>", "N": 32768, "R": 8, "P": 1,
  "Token": "<base64url Fernet ciphertext>"
}
```
The `Token` decrypts to a JSON `VaultPayload` containing the list of `AuthAccount` objects.

## Coding Conventions (from existing code)
- Sealed classes for all models and services
- File-scoped namespaces (`namespace ApexAuth.X;`)
- No dependency injection — objects created directly
- No comments unless explaining a non-obvious crypto detail
- WPF animations use `DoubleAnimation` with `CubicEase(EaseOut)` for consistency
- Toast notifications: call `ShowToast("message")` — handles its own timer

## Project Goals (Active Roadmap)

### 1. Visual / Theme Flexibility
- Expose color scheme as a user-editable JSON config (`%APPDATA%\ApexAuth\theme.json`)
- Load into `App.xaml` resources at startup; allow hot-swap from settings UI
- Keep `App.xaml` as the source of truth at compile time (fallback defaults)

### 2. Cloud Sync (E2E Encrypted)
- The vault file is already encrypted — sync just needs to replicate `data.vault`
- Target: OneDrive (already on user's machine), with opt-in Google Drive / iCloud
- Sync strategy: "last-write-wins" with conflict detection via `VaultEnvelope.Version`
- **Never upload plaintext or the master password** — only the encrypted envelope
- Consider bumping scrypt N for cloud-exposed vaults (attacker gets the ciphertext)
- Sync path configurable in settings; default remains local `%APPDATA%`

### 3. Lightweight / Performance
- Replace 250ms `_timer` with a smarter scheduler: sleep until the next 30s boundary, then refresh
- Avoid `RenderAccounts()` on every code refresh — only rebuild the list on account changes
- Lazy-load GDI+ icon resources; release them after the tray icon is created
- Startup time: profile and reduce allocations in Scrypt during unlock

### 4. Security Hardening (Priority)
See "Security Suggestions" section below.

## Security Suggestions (Ranked by Impact)

### Critical / High
1. **Auto-lock on idle** — lock the vault after N minutes of inactivity (configurable). Currently an unlocked vault stays open indefinitely.
2. **Clipboard auto-clear** — after copying a TOTP code, schedule a 30s `DispatcherTimer` to clear the clipboard. Codes copied and forgotten persist until overwritten.
3. **Password strength enforcement** — minimum 10 chars is enforced but "aaaaaaaaaa" passes. Add entropy estimation (zxcvbn algorithm or a lightweight equivalent).
4. **Screen capture exclusion** — apply `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (Windows 10 2004+) so the window and TOTP codes are invisible to screenshots and screen-share. Opt-out in settings for accessibility.

### Medium
5. **Fernet timestamp validation** — `Fernet.Decrypt` currently ignores the embedded 8-byte timestamp. Validate that the token is not older than ~60 seconds to harden against replay of captured vault tokens.
6. **Scrypt N upgrade path** — N=32768 was a good parameter circa 2017. On modern hardware consider N=65536 or N=131072 for new vaults. Store N in the envelope (already done) so old vaults still open correctly.
7. **DPAPI double-wrap for session key** — wrap `_sessionKey` with `ProtectedMemory.Protect` (DPAPI) while the vault is unlocked. This prevents cold-boot / hibernation file exposure of the in-memory key.
8. **Strict Base32 decoding** — `TotpService.DecodeBase32` silently maps unknown chars to zero bits. Reject invalid characters explicitly to surface bad secrets at import time.

### Low / Nice-to-Have
9. **SHA-256/SHA-512 TOTP support** — some services (Steam, some enterprise SSO) use non-SHA1 HMAC. Store hash algorithm per account in `AuthAccount`.
10. **TOTP period configurability** — most use 30s but a few use 60s. Store period per account.
11. **Anti-debugging / process hollowing guard** — low priority unless threat model includes targeted attacks; Windows Defender handles most malware.

## Key Files Quick-Reference

| File | Responsibility |
|---|---|
| [App.xaml](App.xaml) | All colors, button styles, global resources |
| [MainWindow.xaml.cs](MainWindow.xaml.cs) | Tray, timer, all top-level UI logic |
| [Services/VaultService.cs](Services/VaultService.cs) | Unlock / Save / Export / Import / Lock |
| [Services/TotpService.cs](Services/TotpService.cs) | TOTP generation, Base32, timing |
| [Crypto/Fernet.cs](Crypto/Fernet.cs) | AES-256-CBC + HMAC-SHA256 encrypt/decrypt |
| [Crypto/Scrypt.cs](Crypto/Scrypt.cs) | scrypt key derivation (RFC 7914) |
| [Models/VaultEnvelope.cs](Models/VaultEnvelope.cs) | On-disk format |
| [Models/VaultPayload.cs](Models/VaultPayload.cs) | In-memory decrypted structure |
