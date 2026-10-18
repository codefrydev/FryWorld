using System;
using System.Collections.Generic;
using FryWorld.Sdk.Theming.ColorMath;
using FryWorld.Sdk.Theming.Palette;

namespace FryWorld.Sdk.Theming.Contributors;

/// <summary>
/// Contributes Circuit Studio tokens (Circuit... and M3 compatibility) to FryWorld host
/// and generated color harmony palettes.
/// </summary>
public sealed class CircuitStudioTokenContributor : IThemeTokenContributor
{
    /// <inheritdoc />
    public string Id => "circuitstudio";

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
        var surfaceHover = Neutral(dark ? 18 : 92);
        var surfaceHigh = dark ? Neutral(16) : ColorRgb.White;
        var surfaceLow = dark ? Neutral(8) : Neutral(97);
        var surfaceLowest = dark ? Neutral(4) : ColorRgb.White;
        var border = Neutral(dark ? 24 : 84);
        var borderSubtle = Neutral(dark ? 15 : 92);
        var borderLight = Neutral(dark ? 30 : 78);

        var text = ContrastSolver.SolveTone(engine, neutral.Hue, neutral.Chroma, engine.ToneFromLstar(dark ? 90 : 15), surface, Math.Max(target, 7f), textLc).Color;
        var muted = ContrastSolver.SolveTone(engine, neutralVariant.Hue, neutralVariant.Chroma, engine.ToneFromLstar(dark ? 60 : 45), surface, target, 60f).Color;
        var heading = dark ? ColorRgb.White : ColorRgb.FromHex("#0F172A");

        var primaryKey = palette.Keys[PaletteSections.Primary];
        var primaryParts = palette.Parts[PaletteSections.Primary];
        var primary = ContrastSolver.Meets(primaryKey, surface, target, 45f)
            ? primaryKey
            : ContrastSolver.SolveTone(engine, primaryParts.Hue, primaryParts.Chroma, primaryParts.Tone, surface, target, 45f).Color;
        var primaryHover = engine.Compose(primaryParts.Hue, primaryParts.Chroma, engine.Decompose(primary).Tone + (dark ? 10 : -10));

        var secondaryKey = palette.Keys[PaletteSections.Secondary];
        var secondaryParts = palette.Parts[PaletteSections.Secondary];
        var secondary = ContrastSolver.Meets(secondaryKey, surface, target, 40f)
            ? secondaryKey
            : ContrastSolver.SolveTone(engine, secondaryParts.Hue, secondaryParts.Chroma, secondaryParts.Tone, surface, target, 40f).Color;

        var warningKey = palette.Keys[PaletteSections.Warning];
        var warningParts = palette.Parts[PaletteSections.Warning];
        var warning = ContrastSolver.Meets(warningKey, surface, target, 40f)
            ? warningKey
            : ContrastSolver.SolveTone(engine, warningParts.Hue, warningParts.Chroma, warningParts.Tone, surface, target, 40f).Color;

        var errorKey = palette.Keys[PaletteSections.Error];
        var errorParts = palette.Parts[PaletteSections.Error];
        var danger = ContrastSolver.Meets(errorKey, surface, target, 45f)
            ? errorKey
            : ContrastSolver.SolveTone(engine, errorParts.Hue, errorParts.Chroma, errorParts.Tone, surface, target, 45f).Color;

        SetColorAndBrush(tokens, "CircuitBg", bg);
        SetColorAndBrush(tokens, "CircuitSurface", surface);
        SetColorAndBrush(tokens, "CircuitSurfaceHover", surfaceHover);
        SetColorAndBrush(tokens, "CircuitSurfaceHigh", surfaceHigh);
        SetColorAndBrush(tokens, "CircuitSurfaceLow", surfaceLow);
        SetColorAndBrush(tokens, "CircuitSurfaceLowest", surfaceLowest);
        SetColorAndBrush(tokens, "CircuitHeaderBg", dark ? surfaceLow : ColorRgb.White);
        SetColorAndBrush(tokens, "CircuitPanelBg", surface);
        SetColorAndBrush(tokens, "CircuitPanelHeaderBg", surfaceLow);
        tokens["CircuitGlassBgBrush"] = surface.ToHexWithAlpha(0.92f);
        tokens["CircuitHoverOverlayBrush"] = dark ? "#18FFFFFF" : "#0D000000";
        tokens["CircuitScrimBrush"] = dark ? "#B305070B" : "#66000000";

        SetColorAndBrush(tokens, "CircuitBorder", border);
        SetColorAndBrush(tokens, "CircuitBorderSubtle", borderSubtle);
        SetColorAndBrush(tokens, "CircuitBorderLight", borderLight);
        tokens["CircuitFocusBorderBrush"] = primary.ToHexWithAlpha(0.65f);

        SetColorAndBrush(tokens, "CircuitText", text);
        SetColorAndBrush(tokens, "CircuitTextWhite", heading);
        SetColorAndBrush(tokens, "CircuitTextDim", muted);
        SetColorAndBrush(tokens, "CircuitTextMuted", muted);
        SetColorAndBrush(tokens, "CircuitTextFaint", borderLight);

        SetColorAndBrush(tokens, "circuitstudio.surface", surface);
        SetColorAndBrush(tokens, "circuitstudio.primary", primary);
        SetColorAndBrush(tokens, "CircuitPrimary", primary);
        SetColorAndBrush(tokens, "CircuitPrimaryHover", primaryHover);
        SetColorAndBrush(tokens, "CircuitAccent", primary);
        SetColorAndBrush(tokens, "CircuitAccentHover", primaryHover);
        tokens["CircuitAccentSubtleBrush"] = primary.ToHexWithAlpha(0.15f);
        tokens["CircuitAccentGlowBrush"] = primary.ToHexWithAlpha(0.25f);
        tokens["CircuitOnAccentBrush"] = dark ? "#05080E" : "#FFFFFF";

        SetColorAndBrush(tokens, "CircuitSecondary", secondary);
        tokens["CircuitSecondarySubtleBrush"] = secondary.ToHexWithAlpha(0.15f);

        SetColorAndBrush(tokens, "CircuitWarning", warning);
        tokens["CircuitWarningSubtleBrush"] = warning.ToHexWithAlpha(0.15f);

        SetColorAndBrush(tokens, "CircuitDanger", danger);
        tokens["CircuitDangerSubtleBrush"] = danger.ToHexWithAlpha(0.15f);

        tokens["CircuitSimRunningBrush"] = secondary.ToHex();
        tokens["CircuitSimRunningBgBrush"] = dark ? "#0E2319" : "#ECFDF5";
        tokens["CircuitSimRunningBorderBrush"] = secondary.ToHex();
        tokens["CircuitSimPausedBrush"] = warning.ToHex();
        tokens["CircuitSimPausedBgBrush"] = dark ? "#2A200F" : "#FFFBEB";

        tokens["CircuitSpiceBrush"] = primary.ToHex();
        tokens["CircuitSpiceBgBrush"] = dark ? "#0F202D" : "#F0F9FF";
        tokens["CircuitSpiceHoverBgBrush"] = dark ? "#172F42" : "#E0F2FE";

        tokens["CircuitBomBrush"] = secondary.ToHex();
        tokens["CircuitBomBgBrush"] = dark ? "#0D2419" : "#ECFDF5";
        tokens["CircuitBomHoverBgBrush"] = dark ? "#143625" : "#D1FAE5";

        SetColorAndBrush(tokens, "CircuitCanvasBg", dark ? bg : ColorRgb.FromHex("#F9FBFC"));
        tokens["CircuitCanvasDotMinorBrush"] = dark ? Neutral(18).ToHex() : Neutral(80).ToHex();
        tokens["CircuitCanvasDotMajorBrush"] = primary.ToHexWithAlpha(0.30f);

        tokens["CircuitScopeBgBrush"] = dark ? "#070B10" : "#0D1520";
        tokens["CircuitScopeGridBrush"] = dark ? "#182A3A" : "#1C2A3A";
        tokens["CircuitScopeCenterBrush"] = dark ? "#243D54" : "#2A3F56";
        tokens["CircuitScopeTickBrush"] = dark ? "#2E4D68" : "#385472";

        tokens["CircuitScopeCh1Brush"] = warning.ToHex();
        tokens["CircuitScopeCh2Brush"] = primary.ToHex();
        tokens["CircuitScopeCh3Brush"] = secondary.ToHex();
        tokens["CircuitScopeCh4Brush"] = danger.ToHex();

        tokens["CircuitProbeCh1Brush"] = warning.ToHex();
        tokens["CircuitProbeCh2Brush"] = primary.ToHex();

        tokens["CircuitWireBrush"] = muted.ToHex();
        tokens["CircuitWireNormalBrush"] = muted.ToHex();
        tokens["CircuitWireActiveBrush"] = secondary.ToHex();
        tokens["CircuitWireSelectedBrush"] = primary.ToHex();
        tokens["CircuitWireGlowBrush"] = primary.ToHexWithAlpha(0.35f);
        tokens["CircuitPinBrush"] = text.ToHex();

        tokens["CircuitKnobTrackBrush"] = surfaceLow.ToHex();
        tokens["CircuitKnobBezelBrush"] = border.ToHex();
    }

    /// <inheritdoc />
    public void ContributeTokens(ThemeDefinition theme, IDictionary<string, string> tokens)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(tokens);

        var dark = theme.IsDark;
        string primary = GetFirst(tokens, "M3PrimaryBrush", "M3PrimaryColor", dark ? "#00F0FF" : "#0088CC");
        string secondary = GetFirst(tokens, "M3SecondaryBrush", "M3SecondaryColor", dark ? "#00E676" : "#059669");
        string bg = GetFirst(tokens, "M3SurfaceBrush", "M3SurfaceColor", dark ? "#0A0D14" : "#F4F7FA");
        string surface = GetFirst(tokens, "M3SurfaceContainerBrush", "M3SurfaceContainerLowColor", dark ? "#111622" : "#FFFFFF");
        string border = GetFirst(tokens, "M3OutlineBrush", "M3OutlineColor", dark ? "#1F293D" : "#D6DFE8");
        string text = GetFirst(tokens, "M3OnSurfaceBrush", "M3OnSurfaceColor", dark ? "#E1E8F2" : "#1E293B");
        string muted = GetFirst(tokens, "M3OnSurfaceVariantBrush", "M3OnSurfaceVariantColor", dark ? "#94A3B8" : "#64748B");
        string danger = GetFirst(tokens, "M3ErrorBrush", "M3ErrorColor", dark ? "#FF5252" : "#DC2626");

        SetDefaultColorAndBrush(tokens, "CircuitBg", bg);
        SetDefaultColorAndBrush(tokens, "CircuitSurface", surface);
        SetDefaultColorAndBrush(tokens, "CircuitSurfaceHover", dark ? "#1A2234" : "#EAF0F6");
        SetDefaultColorAndBrush(tokens, "CircuitSurfaceHigh", dark ? "#18202F" : "#F0F4F9");
        SetDefaultColorAndBrush(tokens, "CircuitSurfaceLow", dark ? "#0D111A" : "#F8FAFC");
        SetDefaultColorAndBrush(tokens, "CircuitSurfaceLowest", dark ? "#070A0F" : "#E9EFF6");
        SetDefaultColorAndBrush(tokens, "CircuitHeaderBg", dark ? "#0D1119" : "#FFFFFF");
        SetDefaultColorAndBrush(tokens, "CircuitPanelBg", surface);
        SetDefaultColorAndBrush(tokens, "CircuitPanelHeaderBg", dark ? "#131926" : "#F3F7FA");

        SetDefaultColorAndBrush(tokens, "CircuitBorder", border);
        SetDefaultColorAndBrush(tokens, "CircuitBorderSubtle", dark ? "#161F2E" : "#E6ECF2");
        SetDefaultColorAndBrush(tokens, "CircuitBorderLight", dark ? "#28364F" : "#CBD5E1");

        SetDefaultColorAndBrush(tokens, "CircuitText", text);
        SetDefaultColorAndBrush(tokens, "CircuitTextWhite", dark ? "#FFFFFF" : "#0F172A");
        SetDefaultColorAndBrush(tokens, "CircuitTextDim", muted);
        SetDefaultColorAndBrush(tokens, "CircuitTextMuted", muted);

        SetDefaultColorAndBrush(tokens, "circuitstudio.surface", surface);
        SetDefaultColorAndBrush(tokens, "circuitstudio.primary", primary);
        SetDefaultColorAndBrush(tokens, "CircuitPrimary", primary);
        SetDefaultColorAndBrush(tokens, "CircuitAccent", primary);
        SetDefaultColorAndBrush(tokens, "CircuitAccentHover", dark ? "#5CFAFF" : "#0073B0");
        SetDefaultColorAndBrush(tokens, "CircuitSecondary", secondary);
        SetDefaultColorAndBrush(tokens, "CircuitWarning", dark ? "#FFD700" : "#D97706");
        SetDefaultColorAndBrush(tokens, "CircuitDanger", danger);

        SetDefaultColorAndBrush(tokens, "CircuitCanvasBg", dark ? bg : "#F9FBFC");
        SetDefaultColorAndBrush(tokens, "CircuitWire", muted);
        SetDefaultColorAndBrush(tokens, "CircuitWireNormal", muted);
        SetDefaultColorAndBrush(tokens, "CircuitWireActive", secondary);
        SetDefaultColorAndBrush(tokens, "CircuitWireSelected", primary);
    }

    private static void SetColorAndBrush(IDictionary<string, string> tokens, string prefix, ColorRgb color)
    {
        tokens[$"{prefix}Brush"] = color.ToHex();
        tokens[$"{prefix}Color"] = color.ToHex();
    }

    private static void SetDefaultColorAndBrush(IDictionary<string, string> tokens, string prefix, string hex)
    {
        if (!tokens.ContainsKey($"{prefix}Brush")) tokens[$"{prefix}Brush"] = hex;
        if (!tokens.ContainsKey($"{prefix}Color")) tokens[$"{prefix}Color"] = hex;
    }

    private static string GetFirst(IDictionary<string, string> dict, string key1, string key2, string fallback) =>
        dict.TryGetValue(key1, out var v1) ? v1 : (dict.TryGetValue(key2, out var v2) ? v2 : fallback);
}
