using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ApexAuth.Services;

public enum ThemeMode { Light, Dark, System }

public sealed class ThemeService
{
    public static ThemeService Current { get; } = new();

    private readonly string _configPath;
    private bool _initialized;
    private Color _accentBase = Color.FromRgb(0x7C, 0x73, 0xFF);

    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public string AccentHex { get; private set; } = "#7C73FF";
    public bool IsEffectiveDark => Mode == ThemeMode.Dark || (Mode == ThemeMode.System && SystemIsDark());

    public event Action? ThemeChanged;

    private ThemeService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexAuth");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "theme.json");
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Load();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && Mode == ThemeMode.System)
                Application.Current?.Dispatcher.Invoke(Apply);
        };
        Apply();
    }

    public void SetMode(ThemeMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Save();
        Apply();
    }

    public bool TrySetAccent(string hex)
    {
        if (!TryParseHex(hex, out var color)) return false;
        AccentHex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        _accentBase = color;
        Save();
        Apply();
        return true;
    }

    public static bool TryParseHex(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim().TrimStart('#');
        if (!Regex.IsMatch(hex, "^[0-9A-Fa-f]{6}$")) return false;
        try
        {
            color = Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
            return true;
        }
        catch { return false; }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void Load()
    {
        if (!File.Exists(_configPath)) return;
        try
        {
            var json = File.ReadAllText(_configPath);
            var cfg = JsonSerializer.Deserialize<Config>(json);
            if (cfg is null) return;
            if (Enum.TryParse<ThemeMode>(cfg.Mode, true, out var mode)) Mode = mode;
            if (TryParseHex(cfg.Accent ?? "", out var color))
            {
                _accentBase = color;
                AccentHex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            }
        }
        catch { /* corrupt config — fall back to defaults */ }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new Config { Mode = Mode.ToString(), Accent = AccentHex },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { /* non-fatal */ }
    }

    private sealed class Config
    {
        public string Mode { get; set; } = "System";
        public string Accent { get; set; } = "#7C73FF";
    }

    // ── Apply palette to Application.Resources ────────────────────────────────

    public void Apply()
    {
        if (Application.Current is null) return;
        var dark = IsEffectiveDark;
        var p = BuildPalette(_accentBase, dark);

        var r = Application.Current.Resources;
        Set(r, "BgBrush",                 p.Bg);
        Set(r, "BgAltBrush",              p.BgAlt);
        Set(r, "PanelBrush",              p.Panel);
        Set(r, "CardBrush",               p.Card);
        Set(r, "CardHoverBrush",          p.CardHover);
        Set(r, "InputBrush",              p.Input);
        Set(r, "TextBrush",               p.Text);
        Set(r, "MutedBrush",              p.Muted);
        Set(r, "LineBrush",               p.Line);
        Set(r, "LineSoftBrush",           p.LineSoft);
        Set(r, "DangerBrush",             p.Danger);
        Set(r, "DangerSoftBrush",         p.DangerSoft);
        Set(r, "AccentBrush",             p.Accent);
        Set(r, "AccentAltBrush",          p.AccentAlt);
        Set(r, "AccentSoftBrush",         p.AccentSoft);
        Set(r, "AccentSubtleBrush",       p.AccentSubtle);
        Set(r, "ButtonBgBrush",           p.ButtonBg);
        Set(r, "ButtonBorderBrush",       p.ButtonBorder);
        Set(r, "ButtonHoverBgBrush",      p.ButtonHover);
        Set(r, "ControlBgBrush",          p.ControlBg);
        Set(r, "ControlBorderBrush",      p.ControlBorder);
        Set(r, "PrimaryButtonBgBrush",    p.PrimaryButtonBg);
        Set(r, "PrimaryButtonHoverBrush", p.PrimaryButtonHover);
        Set(r, "PrimaryButtonBorderBrush",p.PrimaryButtonBorder);
        Set(r, "ProgressTrackBrush",      p.ProgressTrack);
        Set(r, "ToastBgBrush",            p.ToastBg);
        Set(r, "OnAccentBrush",           p.OnAccent);
        Set(r, "OnDangerBrush",           p.OnDanger);

        r["AccentColor"]       = p.Accent;
        r["AccentAltColor"]    = p.AccentAlt;
        r["AccentShadowColor"] = p.AccentShadow;
        r["WindowShadowColor"] = p.WindowShadow;
        r["ToastShadowColor"]  = p.ToastShadow;
        r["IsDarkTheme"]       = dark;

        ThemeChanged?.Invoke();
    }

    private static Palette BuildPalette(Color accent, bool dark)
    {
        // The trick: every surface is derived as a mix of a neutral base + a tiny
        // amount of the user's accent. The accent then "tints" the whole UI
        // without the surfaces themselves looking like the accent.
        var baseColor = dark ? Color.FromRgb(0x0A, 0x0B, 0x10) : Color.FromRgb(0xFF, 0xFF, 0xFF);
        var inverse   = dark ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x14, 0x15, 0x1F);

        Color Surface(double accentPct, double lift = 0.0)
        {
            // accentPct = how much accent to bleed in (0..1)
            // lift = move toward inverse (lighten in dark, darken in light)
            var c = Mix(accent, baseColor, 1.0 - accentPct);
            if (lift > 0) c = Mix(inverse, c, 1.0 - lift);
            return c;
        }

        var p = new Palette
        {
            Accent       = accent,
            AccentAlt    = Lighten(accent, 0.18),
            AccentSoft   = Surface(0.18, dark ? 0.04 : 0.02),
            AccentSubtle = Surface(0.08, dark ? 0.02 : 0.01),
            AccentShadow = Color.FromArgb(dark ? (byte)160 : (byte)70, accent.R, accent.G, accent.B),

            Bg            = Surface(0.03),
            BgAlt         = Surface(0.02),
            Panel         = Surface(0.05, dark ? 0.03 : 0.0),
            Card          = Surface(0.04, dark ? 0.04 : 0.0),
            CardHover     = Surface(0.08, dark ? 0.08 : 0.04),
            Input         = Surface(0.03, dark ? 0.0  : 0.02),

            Line          = Surface(0.12, dark ? 0.10 : 0.10),
            LineSoft      = Surface(0.06, dark ? 0.06 : 0.05),

            ButtonBg      = Surface(0.04, dark ? 0.05 : 0.02),
            ButtonBorder  = Surface(0.10, dark ? 0.12 : 0.10),
            ButtonHover   = Surface(0.10, dark ? 0.10 : 0.06),
            ControlBg     = Surface(0.05, dark ? 0.06 : 0.03),
            ControlBorder = Surface(0.10, dark ? 0.12 : 0.10),
            ProgressTrack = Surface(0.05, dark ? 0.0  : 0.06),
            ToastBg       = Surface(0.08, dark ? 0.10 : 0.0),

            Text          = dark ? Color.FromRgb(0xF1, 0xF3, 0xF8) : Color.FromRgb(0x16, 0x17, 0x23),
            Muted         = dark ? Color.FromRgb(0x8C, 0x90, 0xA2) : Color.FromRgb(0x6E, 0x71, 0x88),

            Danger        = dark ? Color.FromRgb(0xFF, 0x5C, 0x78) : Color.FromRgb(0xD6, 0x32, 0x55),
            DangerSoft    = dark ? Color.FromRgb(0x36, 0x18, 0x22) : Color.FromRgb(0xFB, 0xE3, 0xE9),

            PrimaryButtonBg     = accent,
            PrimaryButtonHover  = Lighten(accent, 0.10),
            PrimaryButtonBorder = Lighten(accent, 0.22),

            OnAccent = ContrastText(accent),
            OnDanger = ContrastText(dark ? Color.FromRgb(0xFF, 0x5C, 0x78) : Color.FromRgb(0xD6, 0x32, 0x55)),

            WindowShadow = dark
                ? Color.FromArgb(220, 0, 0, 4)
                : Color.FromArgb(70, 0x1E, 0x21, 0x36),
            ToastShadow = dark
                ? Color.FromArgb(140, accent.R, accent.G, accent.B)
                : Color.FromArgb(45,  accent.R, accent.G, accent.B)
        };
        return p;
    }

    private sealed class Palette
    {
        public Color Bg, BgAlt, Panel, Card, CardHover, Input;
        public Color Text, Muted, Line, LineSoft, Danger, DangerSoft;
        public Color ButtonBg, ButtonBorder, ButtonHover, ControlBg, ControlBorder, ProgressTrack, ToastBg;
        public Color Accent, AccentAlt, AccentSoft, AccentSubtle, AccentShadow;
        public Color PrimaryButtonBg, PrimaryButtonHover, PrimaryButtonBorder;
        public Color WindowShadow, ToastShadow;
        public Color OnAccent, OnDanger;
    }

    private static void Set(ResourceDictionary r, string key, Color color)
    {
        if (r[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = color;
            return;
        }
        r[key] = Freeze(new SolidColorBrush(color));
    }

    private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

    // ── Color helpers ─────────────────────────────────────────────────────────

    // Standard luminance heuristic — picks near-white text on dark backgrounds,
    // near-black text on light ones. Keeps primary buttons readable regardless of accent.
    public static Color ContrastText(Color bg)
    {
        var luminance = bg.R * 0.299 + bg.G * 0.587 + bg.B * 0.114;
        return luminance >= 150
            ? Color.FromRgb(0x10, 0x11, 0x1A)
            : Color.FromRgb(0xFF, 0xFF, 0xFF);
    }

    private static Color Lighten(Color c, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return Color.FromRgb(
            (byte)(c.R + (255 - c.R) * amount),
            (byte)(c.G + (255 - c.G) * amount),
            (byte)(c.B + (255 - c.B) * amount));
    }

    // bWeight = weight of `b`; 0.96 means 96% b + 4% a.
    private static Color Mix(Color a, Color b, double bWeight)
    {
        bWeight = Math.Clamp(bWeight, 0.0, 1.0);
        var aWeight = 1.0 - bWeight;
        return Color.FromRgb(
            (byte)(a.R * aWeight + b.R * bWeight),
            (byte)(a.G * aWeight + b.G * bWeight),
            (byte)(a.B * aWeight + b.B * bWeight));
    }

    private static bool SystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i) return i == 0;
        }
        catch { /* ignore */ }
        return true;
    }
}
