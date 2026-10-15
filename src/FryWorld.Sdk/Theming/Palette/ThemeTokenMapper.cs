using System;
using System.Collections.Generic;
using FryWorld.Sdk.Theming.ColorMath;

namespace FryWorld.Sdk.Theming.Palette;

/// <summary>
/// Turns a <see cref="GeneratedPalette"/> into core application colour tokens:
/// Material 3 Expressive tokens for the FryWorld shell, tonal scales, and navigation colors.
/// Additional domain tokens (e.g. for C# Studio or Image Editor) are contributed via <see cref="IThemeTokenContributor"/>.
/// </summary>
public static class ThemeTokenMapper
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly (string Key, string Reference)[] NavigationHues =
    [
        ("Home", "#38BDF8"), ("Projects", "#818CF8"), ("Notebooks", "#FB923C"), ("Scripts", "#C084FC"), ("Pinned", "#FB7185"),
        ("Templates", "#FBBF24"), ("Open", "#FACC15"), ("Blind75", "#F59E0B"), ("Docs", "#22D3EE"), ("Theme", "#F472B6"),
        ("Toolchains", "#34D399"), ("Kernels", "#A78BFA"), ("Settings", "#94A3B8"), ("FrySharp", "#38BDF8"), ("Servers", "#2DD4BF"),
    ];

    /// <summary>Maps a generated palette to a dictionary of core hex color and brush token values.</summary>
    public static Dictionary<string, string> Map(GeneratedPalette palette) =>
        Map(palette, null);

    /// <summary>Maps a generated palette to a dictionary of hex color and brush token values, executing any registered contributors.</summary>
    public static Dictionary<string, string> Map(GeneratedPalette palette, IEnumerable<IThemeTokenContributor>? contributors)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var spec = palette.Spec;
        var engine = palette.Engine;
        var dark = spec.IsDark;
        var target = (float)Math.Clamp(spec.ContrastTarget, 3, 21);
        var aaa = target >= 7;
        var textLc = aaa ? 90f : 75f;
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var neutral = palette.Parts[PaletteSections.Neutral];
        var neutralVariant = palette.Parts[PaletteSections.NeutralVariant];
        ColorRgb Neutral(double lstar) => engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(lstar));

        // 1. Tonal scales
        foreach (var (prefix, role) in new[]
                 {
                     ("TonalP", PaletteSections.Primary), ("TonalA", PaletteSections.Tertiary), ("TonalS", PaletteSections.Secondary),
                     ("TonalN", PaletteSections.Neutral), ("TonalSuccess", PaletteSections.Success), ("TonalWarning", PaletteSections.Warning),
                     ("TonalError", PaletteSections.Error), ("TonalInfo", PaletteSections.Info),
                 })
        {
            var scale = palette.Scales[role];
            foreach (var stop in Stops) colors[$"{prefix}{stop}"] = scale[stop].ToHex();
        }

        // 2. Surfaces
        var bg = palette.Background;
        var surface = palette.Surface;
        var surfaceHover = Neutral(dark ? 16 : 93);
        var surfaceHigh = dark ? Neutral(14) : ColorRgb.White;
        var surfaceLowest = dark ? Neutral(3) : ColorRgb.White;
        var border = Neutral(dark ? 22 : 86);
        var borderSubtle = Neutral(dark ? 14 : 92);

        // 3. Text
        var text = Solve(neutral, dark ? 90 : 15, surface, Math.Max(target, 7f), textLc);
        var muted = Solve(neutralVariant, dark ? 60 : 45, surface, target, 60f);
        var heading = dark ? ColorRgb.White : ColorRgb.FromHex("#1F2328");

        // 4. Accents
        var primary = Accent(PaletteSections.Primary, surface);
        var primaryParts = palette.Parts[PaletteSections.Primary];
        var primaryHover = engine.Compose(primaryParts.Hue, primaryParts.Chroma, engine.Decompose(primary).Tone + (dark ? 8 : -8));
        var onPrimaryLight = ColorRgb.White;
        var onPrimaryDark = engine.Compose(primaryParts.Hue, Math.Min(primaryParts.Chroma, engine.VividChroma * 0.3), engine.ToneFromLstar(12));
        var onAccent = onPrimaryLight.ContrastRatio(primary) >= onPrimaryDark.ContrastRatio(primary) ? onPrimaryLight : onPrimaryDark;
        var secondary = Accent(PaletteSections.Secondary, surface);
        var tertiary = Accent(PaletteSections.Tertiary, surface);

        var error = Accent(PaletteSections.Error, surface);
        var warning = Accent(PaletteSections.Warning, surface);
        var success = Accent(PaletteSections.Success, surface);
        var info = Accent(PaletteSections.Info, surface);

        // 5. Material 3 Expressive Tokens (FryWorld Host & Shell)
        SetColorAndBrush("M3Surface", bg);
        SetColorAndBrush("M3Background", bg);
        SetColorAndBrush("M3SurfaceContainerLowest", surfaceLowest);
        SetColorAndBrush("M3SurfaceContainerLow", surface);
        SetColorAndBrush("M3SurfaceContainer", surface);
        SetColorAndBrush("M3SurfaceContainerHigh", surfaceHigh);
        SetColorAndBrush("M3SurfaceContainerHighest", surfaceHover);
        SetColorAndBrush("M3SurfaceVariant", surfaceHover);

        SetColorAndBrush("M3Primary", primary);
        colors["M3PrimaryContainerBrush"] = primary.ToHexWithAlpha(0.15f);
        colors["M3PrimaryContainerColor"] = primary.ToHexWithAlpha(0.15f);
        SetColorAndBrush("M3OnPrimary", onAccent);
        SetColorAndBrush("M3OnPrimaryContainer", primary);

        SetColorAndBrush("M3Secondary", secondary);
        colors["M3SecondaryContainerBrush"] = secondary.ToHexWithAlpha(0.18f);
        colors["M3SecondaryContainerColor"] = secondary.ToHexWithAlpha(0.18f);
        SetColorAndBrush("M3OnSecondary", onAccent);

        SetColorAndBrush("M3Tertiary", tertiary);
        SetColorAndBrush("M3Outline", engine.Compose(neutralVariant.Hue, neutralVariant.Chroma, engine.ToneFromLstar(dark ? 32 : 60)));
        SetColorAndBrush("M3OutlineVariant", border);

        SetColorAndBrush("M3OnSurface", heading);
        SetColorAndBrush("M3OnSurfaceVariant", muted);
        SetColorAndBrush("M3Error", error);
        SetColorAndBrush("M3Warning", warning);

        // 6. Navigation categories
        foreach (var (key, reference) in NavigationHues)
        {
            var hue = engine.Decompose(ColorRgb.FromHex(reference)).Hue;
            var chroma = key == "Settings" ? palette.Parts[PaletteSections.NeutralVariant].Chroma * 2 : engine.VividChroma * Math.Clamp(spec.ChromaBoost, 0.3, 1.6) * 0.9;
            var fg = ContrastSolver.SolveTone(engine, hue, chroma, engine.ToneFromLstar(dark ? 72 : 45), surface, target, 45f).Color;
            Set($"Nav{key}FgBrush", fg);
            colors[$"Nav{key}BgBrush"] = fg.ToHexWithAlpha(dark ? 0.10f : 0.08f);
        }

        // 7. Execute extensible plugin token contributors
        if (contributors != null)
        {
            foreach (var contributor in contributors)
            {
                contributor.ContributeTokens(palette, colors);
            }
        }

        return colors;

        void Set(string key, ColorRgb color) => colors[key] = color.ToHex();

        void SetColorAndBrush(string prefix, ColorRgb color)
        {
            colors[$"{prefix}Brush"] = color.ToHex();
            colors[$"{prefix}Color"] = color.ToHex();
        }

        ColorRgb Solve(PerceptualColor role, double lstar, ColorRgb against, float minRatio, float minLc) =>
            ContrastSolver.SolveTone(engine, role.Hue, role.Chroma, engine.ToneFromLstar(lstar), against, minRatio, minLc).Color;

        ColorRgb Accent(string role, ColorRgb against)
        {
            var key = palette.Keys[role];
            if (ContrastSolver.Meets(key, against, target, 45f)) return key;
            var parts = engine.Decompose(key);
            return ContrastSolver.SolveTone(engine, parts.Hue, palette.Parts[role].Chroma, parts.Tone, against, target, 45f).Color;
        }
    }
}
