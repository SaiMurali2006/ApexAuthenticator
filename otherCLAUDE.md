# Apex Design Language — otherCLAUDE.md

> **Purpose.** This document is the canonical design specification for the *Apex* family of Windows desktop apps (ApexAuth, ApexPass, and any sibling apps). It exists so any new app — including [[ApexPass]] — can reproduce ApexAuth's visual identity verbatim. Treat it as a contract: if you ship an Apex app that doesn't match these rules, you've drifted.
>
> Maintainer note: keep this file up to date whenever ApexAuth's design system evolves. The "Changelog" section at the bottom must reflect every meaningful change.

---

## 1. Philosophy

Three rules govern everything visual:

1. **Surfaces are neutral; the accent threads through.**
   Backgrounds, panels, cards, inputs, borders, and button chrome are derived as a *mix of the user's accent color with a neutral base* — never a hardcoded hue. If the user picks teal as their accent, the entire UI subtly leans teal; if they pick coral, it leans coral. The accent itself (the unmixed color) is reserved for *interactive* elements only: primary buttons, monospace code/value text, progress bars, the logo badge, focus borders, and shadow halos.

2. **Light/Dark/System with a user-configurable accent.**
   Three modes (`Light`, `Dark`, `System`) plus a 6-digit hex accent picker, all persisted to `%APPDATA%\<AppName>\theme.json`. System mode tracks `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` and listens to `SystemEvents.UserPreferenceChanged`. The theme entry point is **the app's logo badge in the top-left corner** — clicking it opens a popover with segmented mode control + preset swatches + hex input.

3. **Quiet by default, alive on interaction.**
   Resting state is calm: subtle borders, no glow, no shadow noise. Hovering, pressing, copying, and refreshing all earn springy animations — short `CubicEase` on press-down, then `ElasticEase EaseOut` (`Oscillations=2`, `Springiness≈2.5–4`) or `BackEase Amplitude≈1.0` on release. Bounce is the Apex signature; nothing twitches or shouts but everything settles with a tiny overshoot.

---

## 2. Theme System

### 2.1 Architecture

Singleton `ThemeService.Current`, initialized in `App.OnStartup` *before* any window is constructed. Apps must:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    ThemeService.Current.Initialize();   // load theme.json + subscribe to OS events
    new MainWindow().Show();
}
```

The service:
- Owns the live palette and writes brushes/colors into `Application.Current.Resources`.
- Listens to `SystemEvents.UserPreferenceChanged` to re-apply when the OS theme flips (only in `System` mode).
- Fires a `ThemeChanged` event so persistent UI (account cards, tray icon, window icon) can re-render on swap.

### 2.2 Resource pattern

- **XAML consumers always use `{DynamicResource ...}`**, never `{StaticResource ...}`. Static references freeze on first parse and won't follow theme switches.
- **C# code that builds long-lived UI** (cards, persistent toasts) should either rebuild on `ThemeChanged` or use `SetResourceReference(...)`. Short-lived modal dialogs may use `Application.Current.FindResource(...)` at construction.
- **Adding a new color token**: add to `App.xaml` (defaults) *and* `ThemeService.Apply()` (live mapping). Both. If either is missing, the designer preview or the live theme breaks.

### 2.3 Persistence

`%APPDATA%\<AppName>\theme.json`:

```json
{
  "Mode": "System",
  "Accent": "#7C73FF"
}
```

`Mode ∈ {Light, Dark, System}`. `Accent` is a 6-digit hex with or without `#`. Invalid values fall back to the default (`#7C73FF` for ApexAuth; pick a brand default per app).

---

## 3. Color Palette

### 3.1 Derivation formula

All surfaces are computed via `Surface(accentPct, lift)`:

```
Surface(accentPct, lift) =
    let c = mix(accent, base, 1 - accentPct)    // bleed in accent
    if lift > 0: c = mix(inverse, c, 1 - lift)  // pull toward inverse (lighten in dark, darken in light)
    return c
```

Where:
- `base` = `#0A0B10` (dark mode) or `#FFFFFF` (light mode)
- `inverse` = `#FFFFFF` (dark mode) or `#14151F` (light mode)
- `mix(a, b, bWeight)` is a per-channel linear blend; `0.96` means 96% of `b`, 4% of `a`.

### 3.2 Token map

| Token | Dark `Surface(a, l)` | Light `Surface(a, l)` | Purpose |
|---|---|---|---|
| `BgBrush`              | `0.03, 0.00` | `0.07, 0.03` | Window background. Light mode = visible tinted wash so pure-white panels can pop against it. |
| `BgAltBrush`           | `0.02, 0.00` | `0.05, 0.02` | Outer/alt surface |
| `PanelBrush`           | `0.05, 0.03` | `0.00, 0.00` | Header, vault control, dialog shell. Pure white in light mode for max contrast against `BgBrush`. |
| `CardBrush`            | `0.04, 0.04` | `0.00, 0.00` | Cards at rest. Pure white in light mode. |
| `CardHoverBrush`       | `0.08, 0.08` | `0.05, 0.04` | Card hover. Light mode = visible gray tint against white card. |
| `InputBrush`           | `0.03, 0.00` | `0.04, 0.025` | TextBox/PasswordBox interior. Light mode = subtle wash to read inside a white panel. |
| `ButtonBgBrush`        | `0.04, 0.05` | `0.03, 0.02` | Ghost button chrome |
| `ButtonBorderBrush`    | `0.08, 0.10` | `0.09, 0.09` | Ghost button border |
| `ButtonHoverBgBrush`   | `0.10, 0.10` | `0.06, 0.05` | Ghost button hover |
| `ControlBgBrush`       | `0.05, 0.06` | `0.04, 0.03` | Window-control + small icon-button chrome |
| `ControlBorderBrush`   | `0.08, 0.10` | `0.09, 0.09` | Same border |
| `LineBrush`            | `0.08, 0.08` | `0.10, 0.10` | Main definition border (window outer, dialog shell) |
| `LineSoftBrush`        | `0.05, 0.05` | `0.07, 0.07` | Subtle separator. Bumped in both modes vs. v1 — was disappearing on white. |
| `ProgressTrackBrush`   | `0.05, 0.00` | `0.06, 0.06` | TOTP progress track |
| `ToastBgBrush`         | `0.08, 0.10` | `0.00, 0.00` | Toast notification surface (pure white in light) |

### 3.3 Accent-derived tokens (not via `Surface`)

| Token | Dark | Light | Purpose |
|---|---|---|---|
| `AccentBrush`              | `accent` | `accent` | Primary accent (interactive elements) |
| `AccentAltBrush`           | `Lighten(accent, 0.18)` | same | Lifted accent for outlines/glow |
| `AccentSoftBrush`          | `Surface(0.18, 0.04)`   | `Surface(0.18, 0.02)` | Accent-tinted chip background (eyebrow pill, dialog accent-bar bg) |
| `AccentSubtleBrush`        | `Surface(0.08, 0.02)`   | `Surface(0.08, 0.01)` | Quiet accent wash |
| `PrimaryButtonBgBrush`     | `accent` | `accent` | Primary CTA background |
| `PrimaryButtonHoverBrush`  | `Lighten(accent, 0.10)` | same | Primary CTA hover |
| `PrimaryButtonBorderBrush` | `Lighten(accent, 0.22)` | same | Primary CTA border |

### 3.4 Fixed-per-theme tokens

| Token | Dark | Light |
|---|---|---|
| `TextBrush`        | `#F1F3F8` | `#16171F` |
| `MutedBrush`       | `#8C90A2` | `#6E7188` |
| `DangerBrush`      | `#FF5C78` | `#D63255` |
| `DangerSoftBrush`  | `#361822` | `#FBE3E9` |

### 3.5 Auto-contrast

`OnAccentBrush` is computed *per accent* via standard luminance:

```
luminance = r·0.299 + g·0.587 + b·0.114
OnAccent  = (luminance ≥ 150) ? #10111A : #FFFFFF
```

Apply to: primary button text, logo glyph, active mode-segment text, anything else drawn directly on the accent color. **Never hardcode `Foreground="White"` on a primary CTA** — it breaks when the user picks a light accent (amber, mint).

Equivalent `OnDangerBrush` is computed from the per-theme `DangerBrush`.

### 3.6 Color resources (not brushes)

For `Color="..."` attributes on `SolidColorBrush.Color`, `DropShadowEffect.Color`, and gradient stops, define raw `Color` keys (not `SolidColorBrush`):

| Key | Default |
|---|---|
| `AccentColor`        | the live accent |
| `AccentAltColor`     | lightened accent |
| `AccentShadowColor`  | `Argb(160 dark / 70 light, accent)` |
| `WindowShadowColor`  | dark: near-black opaque · light: `Argb(70, #1E2136)` |
| `ToastShadowColor`   | dark: `Argb(140, accent)` · light: `Argb(45, accent)` |

---

## 4. Typography

### 4.1 Font

`Segoe UI Variable Text, Segoe UI` (fallback chain). For monospace values (codes, hex, secrets) use `Cascadia Code, Cascadia Mono, Consolas`. **Do not introduce new fonts.**

### 4.2 Weight scale

Body text is **Bold** by default — the app should never feel thin. Hierarchy is built by stepping *up* in weight, not down.

| Role | Weight | Where |
|---|---|---|
| Window default (body)        | `Bold`        | App.xaml Window style — inherited everywhere |
| Buttons (ghost + primary)    | `Bold`        | All button styles |
| Inputs (TextBox/PasswordBox) | `Bold`        | Both input styles |
| Section/eyebrow labels       | `ExtraBold`   | "SECURE SESSION", "LABEL", "BASE32 SECRET" — uppercased |
| Card primary label           | `ExtraBold`   | Account name / entry title |
| Card primary value (code)    | `ExtraBold`   | TOTP code, password preview |
| Dialog title                 | `Black`       | "Add account", "Delete account" |
| Lock/empty-state heading     | `Black`       | "Unlock vault", "No accounts yet" |
| App title (header)           | `Black`       | "ApexAuth", "ApexPass" |
| Tagline (header subtitle)    | `Bold`        | "Encrypted local authenticator" |
| Body explanation             | `Bold`        | Dialog message bodies, lock subtitle |

Avoid `Normal` and `Light` — they're never used in Apex.

### 4.3 Size scale

| Size | Use |
|---|---|
| `10pt`  | Eyebrow chips (`SECURE SESSION`, section labels) |
| `11pt`  | Tagline under app title; very-small secondary text |
| `12pt`  | Card primary label; popup section labels; segmented control |
| `13pt`  | Toast body; dialog message body; icon-button glyph; cycle text |
| `14pt`  | Inputs; ghost button content |
| `18pt`  | App title in header |
| `19pt`  | Dialog title |
| `22pt`  | Lock-screen / empty-state heading |
| `26pt`  | Card primary monospace value (TOTP code, password) |

---

## 5. Shape (Corner Radius Scale)

A strict 4-step scale. Pick the closest tier; never invent new radii.

| Radius | Use |
|---|---|
| **18** | Window outer border |
| **14** | All panels, cards (lock card, empty-state), dialog shell, header panel, vault control panel |
| **12** | Buttons (ghost + primary), inputs (TextBox/PasswordBox), action-bar inner container, account card, logo badge, toast |
| **10** | Window-control buttons (minimize/close), account-card icon buttons (Copy/Edit/Delete), theme popup segmented control container, theme popup hex preview |
| **8**  | Pills/chips (eyebrow labels), theme popup segment buttons, theme popup color swatches |
| **6**  | Progress-bar outer track (with inner indicator radius `5`, 1px inset, 1px `LineSoftBrush` border) |

**Nesting rule.** Inner radii should always be smaller than the parent's. A `r=14` panel containing an action bar should use `r=12`, whose buttons use `r=12` (matching, since they extend to the inner edge with `Padding=4`).

---

## 6. Border Hierarchy

Borders are visual weight — too many stacked borders create noise. Apex uses a 3-tier hierarchy:

| Tier | Token | Where |
|---|---|---|
| **Defining** | `LineBrush`   | Window outer · dialog shell (standalone windows) · input focus state (`IsKeyboardFocusWithin → AccentBrush`) |
| **Subtle**   | `LineSoftBrush` | Cards · header panel · vault control panel · lock card · any nested panel inside the window |
| **None**     | `BorderThickness="0"` | Action-bar inner container — relies on `InputBrush` background contrast against parent panel. Don't nest borders three levels deep. |
| **Accent**   | `AccentBrush` | Toast (notification — meant to draw attention); inputs on focus |
| **Accent-soft** | `AccentSoftBrush` | Card hover state |
| **Decorative** | `AccentAltBrush` | Logo badge outline |

**Rule of thumb.** A child border should always be *softer* than its parent's border (or have none). If the parent has `LineBrush`, the child has at most `LineSoftBrush`.

---

## 7. Spacing

Margins/paddings in `(left, top, right, bottom)` or uniform.

| Token | Px | Use |
|---|---|---|
| `2`     | Hair separator (between label and value inside a card) |
| `4`     | Action-bar internal padding around segmented buttons |
| `6`     | Vertical gap between dialog title and accent bar |
| `8`     | Card vertical gap; eyebrow→title gap; small icon-button left margin |
| `12`    | Window outer margin from edge to header; header internal padding |
| `14`    | Header padding (`14,12`); vault control panel padding; action-bar nested margins |
| `16`    | Toast padding (`16,10`); dialog title→content gap |
| `18`    | Lock panel outer margin; toast bottom offset |
| `20`    | Dialog shell padding |
| `22`    | Lock card internal padding |

The general rule: **outer surfaces use 12–14px**, **content padding uses 18–22px**, **micro-gaps stay 2–8px**.

---

## 8. Component Patterns

### 8.1 Window chrome

- `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`.
- A single root `Border` (`CornerRadius=18`, `BorderBrush=LineBrush`) with a `DropShadowEffect` (`BlurRadius=38`, `Color=WindowShadowColor`, `Opacity=1`).
- Drag via a `MouseLeftButtonDown="Header_MouseLeftButtonDown"` handler on the header `Grid`; double-click minimizes.
- Two `WindowControlButton`s (minimize `−`, close `×`) in the top-right of the header. Close hides to tray (`Hide()`); only tray's Exit item really closes.

### 8.2 Header panel

Layout:
```
[ Logo (clickable) ]  [ Title / Subtitle ]  [ Min ][ Close ]
[          Action bar: Import / Export / Lock          ]
```
- Logo is a clickable `Button` (transparent template) whose content is a `36×36` accent-filled rounded `Border` with the app's letter (`A`). Clicking opens the `ThemePopup` anchored below.
- App title is `Black 18pt`; tagline below is `Bold 11pt MutedBrush`.

### 8.3 Action bar

A 3-column equal-weight `Grid` of `GhostButton`s inside an `InputBrush`-tinted container with **no border** and `CornerRadius=12`. Inter-button gap: `3px`.

### 8.4 Account/entry cards

Every card has:
- `CardBrush` background, `LineSoftBrush` border, `r=12`, `Padding=14,12,12,12`, `Margin=0,0,0,8`.
- Left: small `MutedBrush` (or `TextBrush`) label + large monospace value below.
- Right: three icon buttons (`30×30`, `r=10`) — Copy, Edit, Delete (danger).
- Click anywhere outside a button → copy primary value (with border flash).

Animations: AnimateIn on `Loaded` (opacity + 14px slide-up with `ElasticEase` bounce, 560ms); hover lifts `-3px` via `BackEase` (260ms); press scales to `0.94` then springs back via `ElasticEase` (480ms); copy flashes border to `AccentBrush` for `280ms`; primary value pulses (`opacity 1→0.25→1`, `scale 1→1.14→1`) when its data refreshes, with the return arc on `ElasticEase` (560ms total).

### 8.5 Buttons

Four styles. All spring-animated on press — quick scale-down on `CubicEase EaseOut` (~70–80ms), then settle to `1.0` on `ElasticEase EaseOut Oscillations=2, Springiness≈2.5–3` (~360–460ms):

| Style | Background | Border | Foreground | Use |
|---|---|---|---|---|
| `GhostButton`         | `ButtonBgBrush`        | `ButtonBorderBrush` (hover → `AccentBrush`) | `TextBrush` | Secondary/neutral actions |
| `PrimaryButton`       | `PrimaryButtonBgBrush` (= accent) | `PrimaryButtonBorderBrush` | `OnAccentBrush` (auto-contrast) | Single primary CTA per surface |
| `WindowControlButton` | `ControlBgBrush`       | `ControlBorderBrush`       | `TextBrush` | Min/close + small icon actions; presses to `scale 0.85` for tactile feel |
| `InlineIconButton`    | `Transparent`          | none                       | `MutedBrush` (hover → `AccentBrush`) | Chromeless in-field actions (e.g., password reveal eye). Sits *inside* an input's right-padding zone, never gets its own border or background. Presses to `scale 0.8`. |
| Card icon button (in-line)   | `ButtonBgBrush` (or `DangerSoftBrush` for destructive) | `ButtonBorderBrush` (or `DangerBrush`) | `TextBrush` (or `DangerBrush`) | Card-row actions (Copy/Edit/Delete); presses to `scale 0.82` |

All have `r=12` (or `r=10` for window/card-icon buttons), `Cursor=Hand`, hover changes `Background` (and border to accent for ghost). **Every button is keyboard-focusable with a custom `ApexFocusVisualStyle`** — a 1.5px dashed accent ring at `-3px` offset, `r=14`, opacity `0.85`. Never use WPF's default dotted black focus outline.

### 8.6 Inputs (`TextBox` / `PasswordBox`)

`InputBrush` background, `LineSoftBrush` border at rest, `r=12`, `Padding=12,9`, `Bold 14pt`. On `IsKeyboardFocusWithin=True` the border swaps to `AccentBrush`. Caret + selection brushes use `AccentBrush`.

**In-field action icons (e.g., the password reveal eye)** must be `InlineIconButton`-styled and live *inside* the input's right-padding zone. Rules so the icon reads as part of the field and never as a stuck-on button:

- Input right-padding ≥ icon width + `~16px` clearance (`Padding="12,9,40,9"` for a 28px icon).
- Button: no fixed Width/Height — let `InlineIconButton` provide `28×28`. Transparent background, `BorderThickness=0`, `HorizontalAlignment=Right`, `VerticalAlignment=Center`, `Margin="0,0,8,0"`.
- Icon is a `Viewbox Width=18 Height=18` over an internal `Canvas Width=16 Height=16`. The closed-eye path is `M2,4.5 C5,9 11,9 14,4.5 …` (geometric center ≈ y=8, matching the canvas center — avoids the optical-low look the old `y=5→12` path had).
- `Stroke` binds to the button's `Foreground` (`{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}`) so hover swaps the glyph color to accent without re-templating.

### 8.7 Dialogs

All extend a `DialogBase` (Window with `WindowStyle=None`, `AllowsTransparency=true`):
- Shell `Border` = `PanelBrush` bg, `LineBrush` border, `r=14`, `Padding=20`, `DropShadow(BlurRadius=28, Black, Opacity=0.4)`.
- Header: `Black 19pt` title + a `38×3` accent-color "underline bar" with `r=3`.
- Two-button footer: cancel (Ghost, left) + primary (Primary, right), each `Margin=0,0,4,0` / `4,0,0,0` — `8px` total gap between them.
- For a destructive primary (e.g., "Delete" in confirm dialogs), background switches to `DangerBrush` and foreground to `OnDangerBrush` (auto-contrasted from the danger color).
- Drag the dialog by mousing down on the shell.
- **Entry animation** (handled in `DialogBase`): `Opacity 0→1` (CubicEase, 180ms) + `Scale 0.86→1` on `ElasticEase EaseOut Oscillations=2 Springiness=3.5` (500ms). Origin `(0.5,0.5)`.

### 8.8 Toast

Bottom-center, `ToastBgBrush` background, `AccentBrush` border, `r=12`, `Padding=16,10`, `Margin=...,22`, `DropShadow(BlurRadius=22, ToastShadowColor)`.

Entry animation:
- `Opacity 0→1` (CubicEase, 180ms)
- `TranslateY 22→0` (`ElasticEase EaseOut Oscillations=2 Springiness=3.5`, 520ms)
- `Scale 0.82→1` (same ElasticEase, 520ms)
- Origin `(0.5, 1)` so it grows up from the bottom.

Auto-hides after `1.6s` with `Opacity→0` + `TranslateY→8` (CubicEase EaseIn, 180ms).

### 8.9 Theme popup (logo popover)

Anchored to the logo button, `Placement=Bottom`, `VerticalOffset=8`, `StaysOpen=false`, `PopupAnimation=Slide`. After open, the shell springs from `scale 0.96→1` with `BackEase Amplitude=0.4`.

Contents (in order):
1. `APPEARANCE` section label (`ExtraBold 10pt MutedBrush`).
2. Mode segmented control — `InputBrush` background, `LineSoftBrush` border, `r=10`, `Padding=3`, three equal `r=8` segments (Light/Dark/System). Active segment = `AccentBrush` bg + `OnAccentBrush` fg; inactive = transparent + `MutedBrush` fg.
3. `ACCENT` section label.
4. Eight color swatches (`24×24`, `r=8`, presets: violet, azure, teal, green, amber, coral, magenta, neutral).
5. Hex input row: `34×34` accent preview + monospace `TextBox` (Enter or focus-loss to commit; invalid → inline `DangerBrush` error below).

Shell: `PanelBrush` bg, `LineBrush` border, `r=14`, `Padding=14`, width `286`, `DropShadow(BlurRadius=26, Black, Opacity=0.35)`.

### 8.10 Lock/empty-state

A single centered `CardBrush` panel with:
- `AccentSoftBrush` eyebrow pill (`r=8`, `Padding=9,4`) with `ExtraBold 10pt` accent-colored uppercase label (e.g., "SECURE SESSION").
- `Black 22pt` heading (e.g., "Unlock vault").
- `Bold 13pt MutedBrush` body explanation.
- Inputs / primary button below.

---

## 9. Animations

Apex's signature is **bouncy release**. Press-down is short and snappy on `CubicEase EaseOut`; release uses `ElasticEase EaseOut` (or high-amplitude `BackEase`) so things settle with a tiny overshoot. No linear easing, no overshoot longer than ~520ms.

| Where | Easing | Duration | Effect |
|---|---|---|---|
| Window open                  | `ElasticEase EaseOut Osc=2 Spring=4` | 520ms (scale), 220ms (opacity) | `Scale 0.86→1` + fade in |
| Dialog open (`DialogBase`)   | `ElasticEase EaseOut Osc=2 Spring=3.5` | 500ms (scale), 180ms (opacity) | `Scale 0.86→1` + fade |
| Toast in                     | `ElasticEase EaseOut Osc=2 Spring=3.5` | 520ms | Translate `22→0` + scale `0.82→1` + fade |
| Toast out                    | `CubicEase EaseIn`          | 180ms | Translate + fade |
| Popup pop                    | `BackEase EaseOut Amp=0.4`  | 260ms | Scale `0.96→1` |
| Button press                 | `CubicEase EaseOut` (down) → `ElasticEase EaseOut Osc=2 Spring=3` (up) | 70ms / 360–400ms | `Scale 1↔0.9` (GhostButton/PrimaryButton); window controls: `0.85`; inline icon: `0.8` |
| Card hover lift              | `BackEase Amp=1.1` (in) / `ElasticEase EaseOut Osc=2 Spring=3.2` (out) | 260ms / 320ms | `TranslateY 0↔-3` |
| Card press                   | `CubicEase EaseOut` → `ElasticEase EaseOut Osc=2 Spring=3.2` | 80ms / 480ms | `Scale 1↔0.94` |
| Card icon-button press       | `CubicEase EaseOut` → `ElasticEase EaseOut Osc=2 Spring=2.8` | 80ms / 460ms | `Scale 1↔0.82` |
| Logo hover                   | `BackEase EaseOut Amp=0.4`  | 180ms | `Scale 1↔1.06` (the only "grow" hover in Apex) |
| Card copy flash              | DispatcherTimer reset       | 280ms hold | Border → `AccentBrush` then back |
| Card AnimateIn (on Loaded)   | `ElasticEase EaseOut Osc=2 Spring=3.2` | 560ms (translate), 260ms (opacity) | Opacity + 14px translate up |
| Primary value refresh pulse  | KeyFrame (CubicEase up + ElasticEase down) | 560ms total | `Opacity 1→0.25→1`, `Scale 1→1.14→1` |
| Secure-session badge pulse   | `SineEase EaseInOut`, `AutoReverse=True`, `RepeatBehavior=Forever` | 1600ms half-cycle | `Opacity 1↔0.55` (lock-screen "SECURE SESSION" eyebrow) |

If you add a new interactive element, animate it. Static interactive elements feel broken in Apex.

### 9.1 Bounce profile cheat-sheet

When picking the release easing, use this scale:

| Feeling | Easing | Notes |
|---|---|---|
| Tight nudge (hover lifts, small spring-back) | `BackEase EaseOut Amp=0.8–1.2` | Single soft overshoot; 180–280ms |
| Default bounce (most press releases) | `ElasticEase EaseOut Osc=2 Springiness=3` | Two visible oscillations; 360–500ms |
| Loose bounce (cards, dialogs, toast, window) | `ElasticEase EaseOut Osc=2 Springiness=2.5–3.5` | Looser springs for larger UI; 460–560ms |

`Springiness` is inverse: lower number = bouncier. Don't go below `2.0` — it gets jelly.

---

## 10. Iconography

### 10.1 App identity badge

Single-letter rounded badge. The tray icon, window icon, and in-app logo all use the same recipe so they read as one mark:
- Outer rounded square at `r≈13` (scaled per icon size), filled solid with the live accent — no gradient, no gloss.
- A 1.4px alt-accent (`Lighten(accent, 0.20)`) outline.
- A *very faint* top-down white highlight (alpha 36, top half only) to suggest dimension. No glow ring, no shine band.
- The app's first letter (`A` for ApexAuth, `P` for ApexPass) centered in `OnAccentBrush`, `Segoe UI Variable Display` Bold at ~`36px` (scaled).

`Services/IconFactory.cs` generates the tray (`64px`) and window (`128px`) bitmaps via GDI+. The tray service must call `RefreshIcon()` from the `ThemeChanged` event so the tray glyph follows the accent.

In-window, the logo badge is a `36×36` `Border` (`r=12`) with `Background = AccentColor`, `BorderBrush = AccentAltBrush`, a soft `ToastShadowColor` drop shadow, and the letter in `OnAccentBrush`.

### 10.2 Inline UI icons

Every interactive control glyph (window controls, card row actions, password reveal, empty states) uses **stroke-based vector paths**, never Unicode text glyphs. Unicode characters render inconsistently across fonts and dpi.

Two delivery patterns:
- **In XAML** (window-controls, eye reveal): inline `<Viewbox><Canvas Width="16" Height="16"><Path .../></Canvas></Viewbox>` with `IsHitTestVisible="False"`. Stroke is `{DynamicResource <BrushKey>}` so it follows theme changes.
- **In C#** (account-card actions, empty states): use `UI/Icons.cs` — static factory methods that return a `Viewbox` containing the same shape. The stroke brush is wired via `SetResourceReference` so re-theming is automatic.

Authoring rules for new icons:
- Design on a `16×16` canvas. Use `StrokeThickness` between `1.4` and `1.6` so the line weight matches across the set.
- `StrokeStartLineCap` / `StrokeEndLineCap` / `StrokeLineJoin` = `Round` for every path.
- Never fill — strokes only.
- Display size is `12–14px` for chrome buttons, `28px+` for empty-state badges; pick the viewbox `display` value accordingly.

The canonical set in `Icons.cs`: `Minimize`, `Close`, `Copy`, `Edit`, `Delete` (defaults to `DangerBrush`), `EmptyState`. Add domain-specific glyphs (key, lock, eye, etc.) following the same recipe.

---

## 11. Project Layout

```
<App>/
├─ App.xaml                    # Default brushes + button/input styles (DynamicResource consumers)
├─ App.xaml.cs                 # ThemeService.Initialize() then show MainWindow
├─ MainWindow.xaml(.cs)        # Header + content + toast
├─ Services/
│   ├─ ThemeService.cs         # Singleton palette owner + theme.json
│   ├─ IconFactory.cs          # Procedural accent-aware logo
│   ├─ TrayService.cs          # NotifyIcon + RefreshIcon hook
│   └─ <DomainService>.cs      # App-specific (TotpService, VaultService, PasswordService, etc.)
├─ UI/
│   ├─ DialogBase.cs           # Shared chrome
│   ├─ <Entity>CardFactory.cs  # The repeated list-item builder
│   ├─ ThemePopup.cs           # Logo popover
│   ├─ <Entity>Dialog.cs       # Add/edit
│   ├─ PasswordPrompt.cs       # Reused as-is for backup/import keys
│   └─ DarkMessageDialog.cs    # Confirm/error dialog
├─ Crypto/                     # If the app is security-critical
└─ Models/                     # POCOs only
```

Conventions:
- File-scoped namespaces (`namespace ApexPass.X;`).
- `sealed` classes for services and models.
- No dependency injection — construct directly.
- No MVVM framework — UI is built imperatively in C# where dynamic; in XAML where static.
- No comments unless explaining a non-obvious detail (crypto edge case, theme propagation quirk).

---

## 12. ApexPass-specific Adaptation Notes

When you build [[ApexPass]], reuse this design language as-is. The mapping is mostly mechanical:

| ApexAuth concept | ApexPass equivalent |
|---|---|
| Account (TOTP secret + label) | Vault entry (label + URL + username + password + notes) |
| TOTP code (refreshes every 30s) | Password value (revealed on hover/click) |
| `AccountCardFactory` (card with code + Copy/Edit/Del) | `EntryCardFactory` (card with masked password + Reveal/Copy/Edit/Del) |
| Cycle progress bar (30s window) | Password-strength bar (entropy 0–100%) — same `ProgressBar` style, color via strength gradient |
| Logo `A` | Logo `P` |
| `data.vault` (Fernet-encrypted JSON of `AuthAccount[]`) | `passwords.vault` (Fernet-encrypted JSON of `PasswordEntry[]`) |
| `theme.json` | `theme.json` (same schema, in `%APPDATA%\ApexPass\`) |

Reuse verbatim (copy these files; do not refactor the design system):
- `Services/ThemeService.cs`
- `Services/IconFactory.cs` (change the letter)
- `Services/TrayService.cs`
- `Crypto/Fernet.cs`, `Crypto/Scrypt.cs`
- `UI/DialogBase.cs`, `UI/PasswordPrompt.cs`, `UI/DarkMessageDialog.cs`, `UI/ThemePopup.cs`
- `App.xaml` (verbatim)

Different by definition:
- `MainWindow.xaml(.cs)` — different domain
- `UI/EntryCardFactory.cs` — analogous to `AccountCardFactory`, same patterns (hover-lift, press-scale, copy-flash, reveal animation instead of code-refresh pulse)
- `Models/PasswordEntry.cs` — new POCO
- `Services/PasswordService.cs` — new domain logic (password generation, strength scoring)

Suggested ApexPass-only additions, all following these rules:
- **Reveal animation**: tapping the password swaps the masked dots for the real value with a 180ms opacity crossfade + brief accent-flash on the value.
- **Strength meter**: reuse the `ProgressBar` style; color the foreground via a gradient from `DangerBrush` (low) → `accent` (high).
- **Generator dialog**: a `DialogBase` subclass with length slider, character-class toggles, and a copy button — reuse the input styles verbatim.

---

## 13. Anti-patterns (Don't Ship Apex Apps That Do These)

- ❌ Hardcoding a brand color in XAML (`Background="#7C73FF"`). Always reference `{DynamicResource AccentBrush}` or `{DynamicResource AccentColor}`.
- ❌ Using `Foreground="White"` on an accent-colored surface. Use `{DynamicResource OnAccentBrush}` — the user's accent might be light.
- ❌ Three nested visible borders. The middle one should be `BorderThickness="0"` or `LineSoftBrush`.
- ❌ A custom corner radius like `r=11` or `r=16`. Stick to `{18, 14, 12, 10, 8}`.
- ❌ A new font family (Inter, SF Pro, Roboto). Use the Segoe UI Variable / Cascadia Code stack.
- ❌ `FontWeight="Normal"` or `"Light"`. Apex body text is `Bold`; hierarchy goes up from there.
- ❌ `StaticResource` for any themed brush in XAML. Always `DynamicResource`.
- ❌ Linear-eased animations. Use the bounce profile in §9.1 — `ElasticEase EaseOut` for release, `CubicEase` for press-down. Releases should overshoot.
- ❌ Animations slower than ~600ms. The bouncy releases run to ~520ms; press-down stays under ~80ms.
- ❌ An in-field action icon (eye/clear/search) rendered as a bordered button. Use `InlineIconButton` — transparent, no border, hover-tints to accent. The field's right padding is the icon's home.
- ❌ Forgetting to subscribe to `SystemEvents.UserPreferenceChanged` — `System` mode won't follow OS theme changes.
- ❌ Storing the master key on disk or sending the vault unencrypted anywhere. (Apex apps are security-critical; see [[feedback_apexauth_security]].)

---

## 14. Changelog

| Date (UTC) | Change |
|---|---|
| 2026-05-25 | Initial design language extraction from ApexAuth (post-typography + border rework). Defines palette derivation, radius scale, weight scale, animation feel, and ApexPass adaptation guide. |
| 2026-05-25 | **Polish pass.** Progress bar reshaped to `r=6` outer / `r=5` indicator with 1px `LineSoftBrush` outline and 1px inset (height bumped to `10px` for substance). Standardized outer panel margin at `14px` everywhere; lock-card padding `22→20` (matches dialog); lock subtitle/button vertical rhythm tightened (`16/16` instead of `18/18`); confirm-password gap `10→8`. Dialog footer gap unified to `8px` total (cancel `4`, primary `4`). Destructive primary buttons now use `OnDangerBrush` for proper red-button text contrast. Logo button gets a `1.06×` `BackEase` scale on hover (only "grow" hover in Apex) + the `ApexFocusVisualStyle`. Card icon buttons (Copy/Edit/Delete) now press-animate to `scale 0.88` like the rest of the button family. Hex preview swatch in popup dropped to `LineSoftBrush` border (was the harsher `LineBrush`). Added `ApexFocusVisualStyle` — a 1.5px dashed accent ring at `-3px` offset — wired into `RoundedButtonBase`, replacing WPF's dotted black outline. Account dialog "Finish" relabelled to "Save" for verb consistency. |
| 2026-05-25 | **Icon system + light-mode separation overhaul.** Introduced `UI/Icons.cs`: stroke-based vector icon set (`Minimize`, `Close`, `Copy`, `Edit`, `Delete`, `EmptyState`) drawn on a 16×16 canvas with `1.4–1.6px` round-cap strokes. **Replaced every Unicode glyph in the UI** — window minimize/close, account card Copy/Edit/Delete, and the password reveal eye (also redesigned, dropped its decorative glint dot). Rewrote `IconFactory` tray logo: flat solid-accent fill + thin alt-accent ring + clean "A" — matches the in-app badge instead of the old glossy gradient/glow look. Tray icon already refreshes on accent change via `TrayService.RefreshIcon()`. **Light-mode hierarchy redone**: `BgBrush` now a visible tinted wash (`0.07, 0.03`), `PanelBrush` and `CardBrush` switch to pure white so they pop against the wash. `LineSoftBrush` bumped (`0.04→0.05` dark, `0.025→0.07` light) — was invisible on white. `CardHoverBrush` light reworked to a gray tint that reads against white. Empty state gets a 56px accent-tinted badge with an `EmptyState` glyph, and its heading goes `Bold→Black`. Toast now wraps (`MaxWidth=320`, `TextAlignment=Center`) so long messages don't overflow. Popup hex-error label gets `TextWrapping=Wrap`. |
| 2026-05-25 | **Dialog crash fix + eye realignment.** `DialogBase.AnimateIn` was setting `RenderTransform` + `Opacity=0` on the `Window` itself; combined with `AllowsTransparency=true` and `SizeToContent.Height` this crashed Edit/Export (any DialogBase subclass). Moved the entry animation to the inner shell `Border` (its own `RenderTransformOrigin=(0.5,0.5)`, scale start `0.88`). Eye icon shifted further inside the field — input right-padding `40→46`, button right margin `8→14` — so it no longer sits flush against the field's right edge. |
| 2026-05-25 | **Bounce pass + in-field icon convention.** Switched the release-half of every press/open animation from `BackEase Amp≈0.4–0.5` to `ElasticEase EaseOut Osc=2 Springiness≈2.5–3.5` so window-in, dialog-in, toast-in, card-AnimateIn, card press/hover, button presses, and the TOTP code-refresh pulse all settle with a tactile overshoot. Larger start deltas (window/toast `Scale 0.92→0.86`/`0.82`, toast `Y 14→22`, card AnimateIn `Y 6→14`) make the bounce readable. New `DialogBase.AnimateIn()` gives every dialog the same bouncy entry. Added §9.1 bounce-profile cheat-sheet. **Password reveal eye realigned**: introduced new `InlineIconButton` style (transparent, no border, `28×28`, hover-tints `MutedBrush→AccentBrush`, press `Scale 0.8`); the eye now lives inside the input's right-padding zone instead of looking like a stuck-on bordered button. Closed-eye path rebalanced to a canvas-centered geometry (`M2,4.5 C5,9 11,9 14,4.5 …`). Input right-padding tuned `42→40` to match new icon footprint. Updated §3 philosophy + §13 anti-patterns. |

> When you update ApexAuth's design system, add a line here describing the change. If a change affects ApexPass too, also bump the sibling app to match.
