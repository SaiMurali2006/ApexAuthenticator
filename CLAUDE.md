# ApexAuth — CLAUDE.md

> **Sibling spec.** [otherCLAUDE.md](otherCLAUDE.md) is the canonical *design language* spec for the Apex family of apps (ApexAuth, ApexPass, …). When you change typography, color, shape, border hierarchy, or animation feel in this repo, update `otherCLAUDE.md`'s changelog so the sibling apps stay in sync.

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
**Theme settings location:** `%APPDATA%\ApexAuth\theme.json`

## Tech Stack
- .NET 8.0 + WPF (Windows-only)
- No external NuGet dependencies — all crypto is hand-implemented from specs
- `System.Security.Cryptography` for AES, HMAC, PBKDF2 primitives
- `System.Text.Json` for serialization
- `Microsoft.Win32.SystemEvents` for system theme change detection

## Architecture

```
Crypto/         Fernet.cs (AES-256-CBC + HMAC-SHA256), Scrypt.cs (RFC 7914)
Models/         AuthAccount, VaultPayload (plaintext), VaultEnvelope (encrypted JSON on disk)
Services/       VaultService (unlock/save/export/import), TotpService (RFC 6238),
                IconFactory (procedural GDI+, accent-aware), TrayService (tray icon + menu lifecycle),
                ThemeService (singleton — palette, light/dark/system, accent override, persistence)
UI/             DialogBase (shared dialog chrome), AccountCardFactory (card builder + AnimateIn),
                AccountDialog, PasswordPrompt, DarkMessageDialog (all extend DialogBase),
                ThemePopup (light/dark/system + accent hex picker, opened by the logo)
*.xaml/.cs      WPF main window — no MVVM framework, all UI built imperatively in C#
App.xaml        Default palette + button/input styles (overridden at runtime by ThemeService)
```

**No MVVM.** All UI is built imperatively — `new Button()`, `new TextBlock()`, etc.

### Module responsibilities
| File | Does one thing |
|---|---|
| `Services/ThemeService.cs` | Holds the live palette, derives shades from the accent, persists `theme.json`, watches the OS theme |
| `Services/TrayService.cs` | Owns `NotifyIcon` + `Drawing.Icon`; dispatches callbacks to WPF thread |
| `Services/IconFactory.cs` | Builds the tray + window icons; samples the current accent color from `Application.Resources` |
| `UI/DialogBase.cs` | Shared window chrome (border, title, accent bar, drag-move); base for all dialogs |
| `UI/AccountCardFactory.cs` | Builds account cards; click-to-copy on the card, hover swap, icon-button actions |
| `UI/ThemePopup.cs` | Logo popover — Light/Dark/System segmented control, preset swatches, hex input |
| `UI/AccountDialog.cs` | Add/edit dialog — extends `DialogBase` |
| `UI/PasswordPrompt.cs` | Password input dialog — extends `DialogBase` |
| `UI/DarkMessageDialog.cs` | Confirm/error dialog — extends `DialogBase` |
| `MainWindow.xaml.cs` | Orchestration only: timer, unlock flow, account CRUD, theme/tray/window plumbing |

## Cryptography Summary
| Layer | Implementation |
|-------|---------------|
| Key derivation | Scrypt (N=32768, R=8, P=1) — `Crypto/Scrypt.cs` |
| Encryption | Fernet-style AES-256-CBC + HMAC-SHA256 — `Crypto/Fernet.cs` |
| TOTP | RFC 6238 / HOTP with 30s window, HMAC-SHA1 — `Services/TotpService.cs` |
| Memory | `CryptographicOperations.ZeroMemory` on all sensitive buffers after use |

Session key held in `VaultService._sessionKey` (byte[]); zeroed on lock/exit. HMAC verification uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.

## Theme System

`ThemeService.Current` is a singleton that owns the palette. It's initialized in `App.OnStartup` before the main window is constructed.

### How theming works
1. `App.xaml` defines default brush + Color resources (dark palette + signature violet accent). These let the designer preview and provide a fallback before the service runs.
2. `ThemeService.Initialize()` loads `%APPDATA%\ApexAuth\theme.json` (Mode + AccentHex), subscribes to `SystemEvents.UserPreferenceChanged`, and calls `Apply()`.
3. `Apply()` picks the dark or light palette (resolving `System` mode by reading `HKCU\…\Personalize\AppsUseLightTheme`), derives `AccentAlt`, `AccentSoft`, `AccentSubtle`, and `AccentShadow` from the user's accent, then mutates `Application.Current.Resources[...]` in place.
4. All consumers reference brushes via **`DynamicResource`** (XAML) or `FindResource` at construction time (C# dialogs/cards which are short-lived). The `ThemeChanged` event lets `MainWindow` re-render account cards and regenerate the window icon.

### The logo as theme entry point
Clicking the top-left "A" badge in the header opens `ThemePopup` — a popover with:
- Light / Dark / System segmented control
- 8 preset accent swatches
- 6-digit hex text input (Enter or focus-loss to commit; invalid values surface an inline error)

### Adding a new themed surface
- Reference the brush key via `{DynamicResource …}` in XAML, or `FindResource(...)` in code that runs each time the surface is built.
- If you persist a UI element across theme switches (e.g., a non-rebuilt control), use `SetResourceReference(...)` so it follows live updates.
- For *new* color tokens, add the key to both `App.xaml` (defaults) and `ThemeService.Apply()` (palette mapping).

### Color tokens
| Resource Key | Purpose |
|---|---|
| `BgBrush` / `BgAltBrush` | Window background; alt surface |
| `PanelBrush` | Header, vault control panel, dialogs |
| `CardBrush` / `CardHoverBrush` | Account cards (rest and hover) |
| `InputBrush` | TextBox / PasswordBox interior |
| `TextBrush` / `MutedBrush` | Primary and secondary type |
| `LineBrush` | Borders, dividers |
| `AccentBrush` | Primary accent (user-configurable) |
| `AccentAltBrush` / `AccentSoftBrush` / `AccentSubtleBrush` | Lighter and washed shades derived from accent |
| `ButtonBgBrush` / `ButtonBorderBrush` / `ButtonHoverBgBrush` | Ghost button states |
| `ControlBgBrush` / `ControlBorderBrush` | Window-control and small icon buttons |
| `PrimaryButtonBgBrush` / `PrimaryButtonHoverBrush` / `PrimaryButtonBorderBrush` | Primary call-to-action |
| `DangerBrush` / `DangerSoftBrush` | Destructive actions |
| `ProgressTrackBrush` | TOTP cycle progress bar background |
| `AccentColor` / `AccentShadowColor` / `ShadowColor` | Color (not Brush) keys consumed by gradients and drop-shadow effects |

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

## Theme File Format
`%APPDATA%\ApexAuth\theme.json`:
```json
{
  "Mode": "System",
  "Accent": "#7C73FF"
}
```
`Mode` is one of `Light`, `Dark`, `System`. `Accent` must be a 6-digit hex (with or without `#`); invalid values fall back to the default.

## Coding Conventions (from existing code)
- Sealed classes for all models and services
- File-scoped namespaces (`namespace ApexAuth.X;`)
- No dependency injection — objects created directly
- No comments unless explaining a non-obvious crypto detail
- WPF animations use `DoubleAnimation` with `CubicEase(EaseOut)` for consistency
- Toast notifications: call `ShowToast("message")` — handles its own timer
- All visible colors come from `ThemeService` — never hardcode hex outside `App.xaml` defaults or `ThemeService.Apply()`

## Project Goals (Active Roadmap)

### 1. Visual / Theme Flexibility (shipped)
- `ThemeService` with Light / Dark / System modes
- User-configurable accent (hex input + presets)
- Persisted to `%APPDATA%\ApexAuth\theme.json`
- Logo popup as in-app entry point
- Live updates without restart (window icon, account cards, progress bar all re-skin)
- Future: per-account tinting; high-contrast mode

### 2. Cloud Sync (E2E Encrypted)
- The vault file is already encrypted — sync just needs to replicate `data.vault`
- Target: OneDrive (already on user's machine), with opt-in Google Drive / iCloud
- Sync strategy: "last-write-wins" with conflict detection via `VaultEnvelope.Version`
- **Never upload plaintext or the master password** — only the encrypted envelope
- Consider bumping scrypt N for cloud-exposed vaults (attacker gets the ciphertext)
- Sync path configurable in settings; default remains local `%APPDATA%`

### 3. Lightweight / Performance
- Replace 250ms `_timer` with a smarter scheduler: sleep until the next 30s boundary, then refresh
- Avoid `RenderAccounts()` on every code refresh — only rebuild the list on account changes or theme changes
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
| [otherCLAUDE.md](otherCLAUDE.md) | Canonical design language spec — colors, shapes, type, animation; reused by ApexPass and any sibling app |
| [App.xaml](App.xaml) | Default palette + button/input styles (theme fallback) |
| [App.xaml.cs](App.xaml.cs) | Initializes `ThemeService` before window construction |
| [Services/ThemeService.cs](Services/ThemeService.cs) | Owns palette, light/dark/system, accent hex, theme.json |
| [MainWindow.xaml.cs](MainWindow.xaml.cs) | Orchestration: timer, vault flow, CRUD, window/tray/theme plumbing |
| [Services/TrayService.cs](Services/TrayService.cs) | Tray icon + menu; owns lifetime of `NotifyIcon` and `Drawing.Icon` |
| [UI/ThemePopup.cs](UI/ThemePopup.cs) | Logo popover for mode + accent |
| [UI/DialogBase.cs](UI/DialogBase.cs) | Shared chrome for all dialogs; extend this for any new dialog |
| [UI/AccountCardFactory.cs](UI/AccountCardFactory.cs) | Creates account cards; click-to-copy and icon-button actions |
| [Services/VaultService.cs](Services/VaultService.cs) | Unlock / Save / Export / Import / Lock |
| [Services/TotpService.cs](Services/TotpService.cs) | TOTP generation, Base32, timing helpers |
| [Crypto/Fernet.cs](Crypto/Fernet.cs) | AES-256-CBC + HMAC-SHA256 encrypt/decrypt |
| [Crypto/Scrypt.cs](Crypto/Scrypt.cs) | scrypt key derivation (RFC 7914) |
| [Models/VaultEnvelope.cs](Models/VaultEnvelope.cs) | On-disk JSON format |
| [Models/VaultPayload.cs](Models/VaultPayload.cs) | In-memory decrypted structure |
