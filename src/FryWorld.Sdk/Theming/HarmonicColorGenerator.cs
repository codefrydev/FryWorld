using System;
using System.Collections.Generic;
using System.Linq;
using FryWorld.Sdk.Theming.ColorMath;
using FryWorld.Sdk.Theming.Palette;

namespace FryWorld.Sdk.Theming;

/// <summary>
/// Fine-tuning parameters for the procedural harmonic design system generator.
/// </summary>
public sealed record HarmonicConfiguration
{
    /// <summary>Base hue in degrees (0..360).</summary>
    public float BaseHue { get; init; } = 210f;
    /// <summary>Harmony relationship mode.</summary>
    public ColorHarmonyMode Mode { get; init; } = ColorHarmonyMode.Analogous;
    /// <summary>Saturation multiplier.</summary>
    public float SaturationBoost { get; init; } = 1.0f;
    /// <summary>Accent shift in degrees.</summary>
    public float AccentShift { get; init; } = 0f;
    /// <summary>Neutral tint percentage (0..35).</summary>
    public float NeutralTint { get; init; } = 10f;
    /// <summary>Neutral hue offset in degrees.</summary>
    public float NeutralHueOffset { get; init; } = 0f;
    /// <summary>Semantic pull toward brand hue (0..100).</summary>
    public float SemanticPull { get; init; } = 15f;
    /// <summary>Target WCAG contrast ratio (3..21).</summary>
    public float ContrastTarget { get; init; } = 4.5f;

    /// <summary>The colour model the palette is built in.</summary>
    public ColorEngineKind Engine { get; init; } = ColorEngineKind.Oklch;
}

/// <summary>
/// Procedural color harmony wheel and theme synthesizer.
/// </summary>
public static class HarmonicColorGenerator
{
    private static readonly Random Rnd = new();

    /// <summary>
    /// Generates a randomized, ergonomically balanced harmonic theme.
    /// </summary>
    public static ThemeDefinition GenerateRandomHarmonicTheme(bool isDark = true) =>
        GenerateRandomHarmonicTheme(isDark, null);

    /// <summary>
    /// Generates a randomized, ergonomically balanced harmonic theme with extensible plugin token contributors.
    /// </summary>
    public static ThemeDefinition GenerateRandomHarmonicTheme(bool isDark, IEnumerable<IThemeTokenContributor>? contributors)
    {
        float baseHue = (float)(Rnd.NextDouble() * 360.0);
        var modes = Enum.GetValues<ColorHarmonyMode>();
        var mode = modes[Rnd.Next(modes.Length)];
        return GenerateHarmonicTheme(baseHue, mode, isDark, contributors);
    }

    /// <summary>
    /// Generates a deterministic harmonic theme from a specified base hue and harmony relationship.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(float hueDegrees, ColorHarmonyMode mode, bool isDark = true) =>
        GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = hueDegrees, Mode = mode }, isDark, null);

    /// <summary>
    /// Generates a deterministic harmonic theme from a specified base hue and harmony relationship with token contributors.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(float hueDegrees, ColorHarmonyMode mode, bool isDark, IEnumerable<IThemeTokenContributor>? contributors) =>
        GenerateHarmonicTheme(new HarmonicConfiguration { BaseHue = hueDegrees, Mode = mode }, isDark, contributors);

    /// <summary>
    /// Generates a deterministic harmonic theme from an extended procedural configuration.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(HarmonicConfiguration config, bool isDark = true) =>
        GenerateHarmonicTheme(config, isDark, null);

    /// <summary>
    /// Generates a deterministic harmonic theme from an extended procedural configuration with token contributors.
    /// </summary>
    public static ThemeDefinition GenerateHarmonicTheme(HarmonicConfiguration config, bool isDark, IEnumerable<IThemeTokenContributor>? contributors)
    {
        ArgumentNullException.ThrowIfNull(config);
        float accentHue = AccentHue(config);
        return ThemeFromPalette(PaletteGenerator.Generate(ToPaletteSpec(config, accentHue, isDark)), accentHue, contributors);
    }

    /// <summary>Creates a ThemeDefinition from a generated palette.</summary>
    public static ThemeDefinition ThemeFromPalette(GeneratedPalette palette, float accentHue) =>
        ThemeFromPalette(palette, accentHue, null);

    /// <summary>Creates a ThemeDefinition from a generated palette, executing any registered contributors.</summary>
    public static ThemeDefinition ThemeFromPalette(GeneratedPalette palette, float accentHue, IEnumerable<IThemeTokenContributor>? contributors)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var mode = palette.Spec.Harmony;
        var colors = ThemeTokenMapper.Map(palette, contributors);

        string modeName = mode switch
        {
            ColorHarmonyMode.Analogous => "Analogous",
            ColorHarmonyMode.Complementary => "Complementary",
            ColorHarmonyMode.SplitComplementary => "Split-Complementary",
            ColorHarmonyMode.Triadic => "Triadic",
            ColorHarmonyMode.Tetradic => "Tetradic",
            ColorHarmonyMode.Monochromatic => "Monochromatic",
            _ => "Harmonic"
        };

        string hueName = GetHueDescriptor(accentHue);
        string themeId = $"harmonic-{mode.ToString().ToLowerInvariant()}-{(int)accentHue}";
        string themeName = $"Harmonic {modeName} ({hueName})";

        return new ThemeDefinition
        {
            Id = themeId,
            Name = themeName,
            Description = $"{modeName} harmony built in {palette.Engine.DisplayName}, centred at {hueName} ({accentHue:0}°).",
            IsDark = palette.Spec.IsDark,
            Colors = colors
        };
    }

    /// <summary>The wheel hue of the primary accent.</summary>
    public static float AccentHue(HarmonicConfiguration config)
    {
        float hueDegrees = (config.BaseHue % 360f + 360f) % 360f;
        float rawAccent = config.Mode switch
        {
            ColorHarmonyMode.Analogous => hueDegrees + 30f,
            ColorHarmonyMode.Complementary => hueDegrees + 180f,
            ColorHarmonyMode.SplitComplementary => hueDegrees + 150f,
            ColorHarmonyMode.Triadic => hueDegrees + 120f,
            ColorHarmonyMode.Tetradic => hueDegrees + 90f,
            ColorHarmonyMode.Monochromatic => hueDegrees,
            _ => hueDegrees + 180f
        };
        return ((rawAccent + config.AccentShift) % 360f + 360f) % 360f;
    }

    /// <summary>Converts configuration to PaletteSpec.</summary>
    public static PaletteSpec ToPaletteSpec(HarmonicConfiguration config, float accentHue, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(config);
        var engine = ColorEngines.Get(config.Engine);
        return new PaletteSpec
        {
            Engine = config.Engine,
            Harmony = config.Mode,
            BaseHue = EngineHue(engine, accentHue),
            IsDark = isDark,
            ChromaBoost = Math.Clamp(config.SaturationBoost, 0.2f, 2.0f),
            NeutralTint = Math.Clamp(config.NeutralTint, 0f, 35f),
            SemanticPull = Math.Clamp(config.SemanticPull, 0f, 100f),
            ContrastTarget = Math.Clamp(config.ContrastTarget, 3f, 21f),
        };
    }

    /// <summary>The engine hue of a colour-wheel hue.</summary>
    public static double EngineHue(IColorEngine engine, float wheelHue) =>
        engine.Decompose(ColorRgb.FromHex(HslToHex(wheelHue, 0.85f, 0.55f))).Hue;

    /// <summary>
    /// Guarantees that any theme definition has complete coverage of all tokens by backfilling missing entries.
    /// </summary>
    public static void EnsureCompleteTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var fallback = theme.IsDark ? DarkFallback.Value : LightFallback.Value;
        foreach (var (k, v) in fallback)
        {
            if (!theme.Colors.ContainsKey(k))
            {
                theme.Colors[k] = v;
            }
        }
    }

    private static readonly Lazy<Dictionary<string, string>> DarkFallback = new(() => Fallback(isDark: true));
    private static readonly Lazy<Dictionary<string, string>> LightFallback = new(() => Fallback(isDark: false));

    private static Dictionary<string, string> Fallback(bool isDark)
    {
        var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var palette = PaletteGenerator.Generate(new PaletteSpec
        {
            BaseHue = 215f,
            IsDark = isDark,
            Harmony = ColorHarmonyMode.Analogous
        });
        foreach (var (k, v) in ThemeTokenMapper.Map(palette))
        {
            fallback[k] = v;
        }

        return fallback;
    }

    /// <summary>Converts HSL to hex format.</summary>
    public static string HslToHex(float h, float s, float l)
    {
        h = (h % 360f + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        l = Math.Clamp(l, 0f, 1f);

        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r = 0f, g = 0f, b = 0f;
        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        byte rByte = (byte)Math.Clamp((int)Math.Round((r + m) * 255f), 0, 255);
        byte gByte = (byte)Math.Clamp((int)Math.Round((g + m) * 255f), 0, 255);
        byte bByte = (byte)Math.Clamp((int)Math.Round((b + m) * 255f), 0, 255);

        return $"#{rByte:X2}{gByte:X2}{bByte:X2}";
    }

    /// <summary>Gets descriptive title for a hue.</summary>
    public static string GetHueDescriptor(float h) => h switch
    {
        >= 345 or < 15 => "Ruby Crimson",
        >= 15 and < 45 => "Amber Coral",
        >= 45 and < 70 => "Golden Sun",
        >= 70 and < 150 => "Emerald Mint",
        >= 150 and < 190 => "Cyan Glacier",
        >= 190 and < 255 => "Sapphire Ocean",
        >= 255 and < 290 => "Violet Iris",
        >= 290 and < 345 => "Magenta Orchid",
        _ => "Spectrum"
    };
}
