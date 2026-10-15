using System;
using System.Collections.Generic;
using FryWorld.Sdk.Theming.ColorMath;
using FryWorld.Sdk.Theming.Palette;

namespace FryWorld.Sdk.Theming.Contributors;

/// <summary>
/// Contributes Developer Studio tokens (Ds... and Syntax... and Chart...) used by C# Code Studio (FrySharp).
/// </summary>
public sealed class DeveloperStudioTokenContributor : IThemeTokenContributor
{
    /// <inheritdoc />
    public string Id => "csharpstudio";

    /// <inheritdoc />
    public void ContributeTokens(GeneratedPalette palette, IDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(tokens);

        var spec = palette.Spec;
        var engine = palette.Engine;
        var dark = spec.IsDark;
        var target = (float)Math.Clamp(spec.ContrastTarget, 3, 21);
        var textLc = target >= 7 ? 90f : 75f;

        var neutral = palette.Parts[PaletteSections.Neutral];
        var neutralVariant = palette.Parts[PaletteSections.NeutralVariant];
        ColorRgb Neutral(double lstar) => engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(lstar));

        var bg = palette.Background;
        var surface = palette.Surface;
        var surfaceHover = Neutral(dark ? 16 : 93);
        var surfaceHigh = dark ? Neutral(14) : ColorRgb.White;
        var surfaceLowest = dark ? Neutral(3) : ColorRgb.White;
        var border = Neutral(dark ? 22 : 86);
        var borderSubtle = Neutral(dark ? 14 : 92);

        var text = ContrastSolver.SolveTone(engine, neutral.Hue, neutral.Chroma, engine.ToneFromLstar(dark ? 90 : 15), surface, Math.Max(target, 7f), textLc).Color;
        var muted = ContrastSolver.SolveTone(engine, neutralVariant.Hue, neutralVariant.Chroma, engine.ToneFromLstar(dark ? 60 : 45), surface, target, 60f).Color;
        var heading = dark ? ColorRgb.White : ColorRgb.FromHex("#1F2328");

        var primaryKey = palette.Keys[PaletteSections.Primary];
        var primaryParts = palette.Parts[PaletteSections.Primary];
        var primary = ContrastSolver.Meets(primaryKey, surface, target, 45f)
            ? primaryKey
            : ContrastSolver.SolveTone(engine, primaryParts.Hue, primaryParts.Chroma, primaryParts.Tone, surface, target, 45f).Color;

        var primaryHover = engine.Compose(primaryParts.Hue, primaryParts.Chroma, engine.Decompose(primary).Tone + (dark ? 8 : -8));
        var onPrimaryLight = ColorRgb.White;
        var onPrimaryDark = engine.Compose(primaryParts.Hue, Math.Min(primaryParts.Chroma, engine.VividChroma * 0.3), engine.ToneFromLstar(12));
        var onAccent = onPrimaryLight.ContrastRatio(primary) >= onPrimaryDark.ContrastRatio(primary) ? onPrimaryLight : onPrimaryDark;

        tokens["DsBgBrush"] = bg.ToHex();
        tokens["DsSurfaceBrush"] = surface.ToHex();
        tokens["DsSurfaceHoverBrush"] = surfaceHover.ToHex();
        tokens["DsSurfaceHighBrush"] = surfaceHigh.ToHex();
        tokens["DsSurfaceContainerLowestBrush"] = surfaceLowest.ToHex();
        tokens["DsGlassBgBrush"] = surface.ToHexWithAlpha(0.90f);
        tokens["DsHoverOverlayBrush"] = dark ? "#1AFFFFFF" : "#14000000";
        tokens["DsScrimBrush"] = dark ? "#85000000" : "#50000000";
        tokens["VisCanvasBgBrush"] = bg.ToHex();

        tokens["DsBorderBrush"] = border.ToHex();
        tokens["DsBorderSubtleBrush"] = borderSubtle.ToHex();
        tokens["DsFocusBorderBrush"] = primary.ToHexWithAlpha(0.65f);

        tokens["DsPrimaryBrush"] = primary.ToHex();
        tokens["DsPrimaryHoverBrush"] = primaryHover.ToHex();
        tokens["DsPrimarySubtleBrush"] = primary.ToHexWithAlpha(0.15f);
        tokens["DsSelectionBrush"] = primary.ToHexWithAlpha(dark ? 0.40f : 0.30f);
        tokens["DsPrimaryBorderSubtleBrush"] = primary.ToHexWithAlpha(0.25f);
        tokens["DsOnAccentBrush"] = onAccent.ToHex();

        tokens["DsTextBrush"] = text.ToHex();
        tokens["DsTextWhiteBrush"] = heading.ToHex();
        tokens["DsMutedBrush"] = muted.ToHex();

        // Editor tokens
        var editorBg = palette.EditorBackground;
        var editorFg = ContrastSolver.SolveTone(engine, neutral.Hue, neutral.Chroma, engine.ToneFromLstar(dark ? 86 : 18), editorBg, Math.Max(target, 7f), textLc).Color;
        tokens["EditorBgBrush"] = editorBg.ToHex();
        tokens["EditorFgBrush"] = editorFg.ToHex();
        tokens["EditorLineNumberBrush"] = Neutral(dark ? 30 : 68).ToHex();
        tokens["EditorLineNumberActiveBrush"] = primary.ToHex();
        tokens["EditorCurrentLineBrush"] = (dark ? Neutral(11) : Neutral(96)).ToHex();
        tokens["EditorSelectionBrush"] = primary.ToHexWithAlpha(dark ? 0.35f : 0.22f);
        tokens["EditorWhitespaceBrush"] = Neutral(dark ? 24 : 80).ToHex();

        // Syntax tokens
        foreach (var role in PaletteSections.SyntaxRoles)
        {
            var key = PaletteSections.SyntaxPrefix + role;
            if (palette.Keys.TryGetValue(key, out var _))
            {
                var fg = ContrastSolver.SolveTone(engine, palette.Parts[key].Hue, palette.Parts[key].Chroma, palette.Parts[key].Tone, editorBg, target, 45f).Color;
                tokens[$"Syntax{role}Brush"] = fg.ToHex();
            }
        }
        if (!tokens.ContainsKey("SyntaxFunctionBrush"))
        {
            var tertKey = palette.Keys[PaletteSections.Tertiary];
            var tertParts = palette.Parts[PaletteSections.Tertiary];
            var tert = ContrastSolver.Meets(tertKey, editorBg, target, 45f) ? tertKey : ContrastSolver.SolveTone(engine, tertParts.Hue, tertParts.Chroma, tertParts.Tone, editorBg, target, 45f).Color;
            tokens["SyntaxFunctionBrush"] = tert.ToHex();
        }
        if (!tokens.ContainsKey("SyntaxTypeBrush"))
        {
            var secKey = palette.Keys[PaletteSections.Secondary];
            var secParts = palette.Parts[PaletteSections.Secondary];
            var sec = ContrastSolver.Meets(secKey, editorBg, target, 45f) ? secKey : ContrastSolver.SolveTone(engine, secParts.Hue, secParts.Chroma, secParts.Tone, editorBg, target, 45f).Color;
            tokens["SyntaxTypeBrush"] = sec.ToHex();
        }
        if (!tokens.ContainsKey("SyntaxVariableBrush")) tokens["SyntaxVariableBrush"] = text.ToHex();
        if (!tokens.ContainsKey("SyntaxPunctuationBrush")) tokens["SyntaxPunctuationBrush"] = muted.ToHex();

        // Chart series tokens
        for (int i = 1; i <= PaletteSections.ChartSeriesCount; i++)
        {
            var key = PaletteSections.ChartPrefix + i;
            if (palette.Keys.TryGetValue(key, out var chart)) tokens[$"ChartSeries{i}Brush"] = chart.ToHex();
        }
    }

    /// <inheritdoc />
    public void ContributeTokens(ThemeDefinition theme, IDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(tokens);

        // Fallback derivation from core M3 tokens if not explicitly present
        string primary = GetFirst(tokens, "M3PrimaryBrush", "M3PrimaryColor", "#2F81F7");
        string bg = GetFirst(tokens, "M3SurfaceBrush", "M3SurfaceColor", theme.IsDark ? "#0D1117" : "#F8FAFD");
        string surface = GetFirst(tokens, "M3SurfaceContainerLowBrush", "M3SurfaceContainerLowColor", theme.IsDark ? "#161B22" : "#F1F5F9");
        string border = GetFirst(tokens, "M3OutlineBrush", "M3OutlineColor", theme.IsDark ? "#30363D" : "#D0D7DE");
        string text = GetFirst(tokens, "M3OnSurfaceBrush", "M3OnSurfaceColor", theme.IsDark ? "#C9D1D9" : "#1F2328");
        string muted = GetFirst(tokens, "M3OnSurfaceVariantBrush", "M3OnSurfaceVariantColor", theme.IsDark ? "#8B949E" : "#656D76");

        SetDefault(tokens, "DsBgBrush", bg);
        SetDefault(tokens, "DsSurfaceBrush", surface);
        SetDefault(tokens, "DsSurfaceHoverBrush", GetFirst(tokens, "M3SurfaceContainerHighestBrush", theme.IsDark ? "#21262D" : "#E2E8F0"));
        SetDefault(tokens, "DsSurfaceHighBrush", GetFirst(tokens, "M3SurfaceContainerHighBrush", theme.IsDark ? "#1C2128" : "#EAEFF7"));
        SetDefault(tokens, "DsSurfaceContainerLowestBrush", GetFirst(tokens, "M3SurfaceContainerLowestBrush", theme.IsDark ? "#080C11" : "#FFFFFF"));
        SetDefault(tokens, "DsBorderBrush", border);
        SetDefault(tokens, "DsBorderSubtleBrush", GetFirst(tokens, "M3OutlineVariantBrush", theme.IsDark ? "#21262D" : "#E2E8F0"));
        SetDefault(tokens, "DsPrimaryBrush", primary);
        SetDefault(tokens, "DsTextBrush", text);
        SetDefault(tokens, "DsMutedBrush", muted);
        SetDefault(tokens, "EditorBgBrush", bg);
        SetDefault(tokens, "EditorFgBrush", text);
    }

    private static string GetFirst(IDictionary<string, string> dict, string key, string fallback) =>
        dict.TryGetValue(key, out var v) ? v : fallback;

    private static string GetFirst(IDictionary<string, string> dict, string key1, string key2, string fallback) =>
        dict.TryGetValue(key1, out var v1) ? v1 : (dict.TryGetValue(key2, out var v2) ? v2 : fallback);

    private static void SetDefault(IDictionary<string, string> dict, string key, string value)
    {
        if (!dict.ContainsKey(key)) dict[key] = value;
    }
}
