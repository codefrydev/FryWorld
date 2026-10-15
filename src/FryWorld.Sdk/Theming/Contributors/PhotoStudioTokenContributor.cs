using System;
using System.Collections.Generic;
using FryWorld.Sdk.Theming.ColorMath;
using FryWorld.Sdk.Theming.Palette;

namespace FryWorld.Sdk.Theming.Contributors;

/// <summary>
/// Contributes Photo / Image Studio tokens (Ps...) used by FryImage.
/// </summary>
public sealed class PhotoStudioTokenContributor : IThemeTokenContributor
{
    /// <inheritdoc />
    public string Id => "imageeditor";

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

        var warningKey = palette.Keys[PaletteSections.Warning];
        var warningParts = palette.Parts[PaletteSections.Warning];
        var warning = ContrastSolver.Meets(warningKey, surface, target, 45f)
            ? warningKey
            : ContrastSolver.SolveTone(engine, warningParts.Hue, warningParts.Chroma, warningParts.Tone, surface, target, 45f).Color;

        SetColorAndBrush(tokens, "PsBg", bg);
        SetColorAndBrush(tokens, "PsSurface", surface);
        SetColorAndBrush(tokens, "PsSurfaceLow", surfaceLowest);
        SetColorAndBrush(tokens, "PsSurfaceHigh", surfaceHigh);
        SetColorAndBrush(tokens, "PsBorder", border);
        SetColorAndBrush(tokens, "PsBorderSubtle", borderSubtle);
        SetColorAndBrush(tokens, "PsBorderLight", surfaceHover);
        SetColorAndBrush(tokens, "PsText", heading);
        SetColorAndBrush(tokens, "PsTextDim", text);
        SetColorAndBrush(tokens, "PsTextFaint", muted);
        SetColorAndBrush(tokens, "PsAccent", primary);
        SetColorAndBrush(tokens, "PsAccentHover", primaryHover);
        tokens["PsSelectionBrush"] = primary.ToHexWithAlpha(0.30f);
        tokens["PsSelectionColor"] = primary.ToHexWithAlpha(0.30f);
        SetColorAndBrush(tokens, "PsRowSelected", surfaceHover);
        SetColorAndBrush(tokens, "PsHover", surfaceHover);
        SetColorAndBrush(tokens, "PsWarning", warning);
    }

    /// <inheritdoc />
    public void ContributeTokens(ThemeDefinition theme, IDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(tokens);

        string primary = GetFirst(tokens, "M3PrimaryBrush", "M3PrimaryColor", "#38BDF8");
        string bg = GetFirst(tokens, "M3SurfaceBrush", "M3SurfaceColor", theme.IsDark ? "#0F172A" : "#FFFFFF");
        string surface = GetFirst(tokens, "M3SurfaceContainerLowBrush", "M3SurfaceContainerLowColor", theme.IsDark ? "#1E293B" : "#F8FAFC");
        string border = GetFirst(tokens, "M3OutlineBrush", "M3OutlineColor", theme.IsDark ? "#334155" : "#E2E8F0");
        string text = GetFirst(tokens, "M3OnSurfaceBrush", "M3OnSurfaceColor", theme.IsDark ? "#F8FAFC" : "#0F172A");
        string muted = GetFirst(tokens, "M3OnSurfaceVariantBrush", "M3OnSurfaceVariantColor", theme.IsDark ? "#94A3B8" : "#64748B");

        SetColorAndBrush(tokens, "PsBg", bg);
        SetColorAndBrush(tokens, "PsSurface", surface);
        SetColorAndBrush(tokens, "PsBorder", border);
        SetColorAndBrush(tokens, "PsText", text);
        SetColorAndBrush(tokens, "PsTextDim", muted);
        SetColorAndBrush(tokens, "PsAccent", primary);
    }

    private static void SetColorAndBrush(IDictionary<string, string> tokens, string prefix, ColorRgb color)
    {
        tokens[$"{prefix}Brush"] = color.ToHex();
        tokens[$"{prefix}Color"] = color.ToHex();
    }

    private static void SetColorAndBrush(IDictionary<string, string> tokens, string prefix, string hex)
    {
        if (!tokens.ContainsKey($"{prefix}Brush")) tokens[$"{prefix}Brush"] = hex;
        if (!tokens.ContainsKey($"{prefix}Color")) tokens[$"{prefix}Color"] = hex;
    }

    private static string GetFirst(IDictionary<string, string> dict, string key1, string key2, string fallback) =>
        dict.TryGetValue(key1, out var v1) ? v1 : (dict.TryGetValue(key2, out var v2) ? v2 : fallback);
}
