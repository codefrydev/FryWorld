using System;
using System.Collections.Generic;

namespace FryWorld.Sdk.Theming.ColorMath;

/// <summary>The colour models palettes can be built in.</summary>
public enum ColorEngineKind
{
    /// <summary>OKLCH (CSS Color 4; Tailwind 4 and Radix palettes): perceptually even lightness and hue.</summary>
    Oklch,

    /// <summary>HCT (Material Design 3): CAM16 hue and chroma with L* tone, so tone differences predict contrast.</summary>
    Hct,
}

/// <summary>A colour as an engine sees it: hue in degrees, chroma in the engine's units, tone 0 (black) .. 100 (white).</summary>
public readonly record struct PerceptualColor(double Hue, double Chroma, double Tone);

/// <summary>
/// A perceptual colour model: takes colours apart into hue, chroma and tone and puts them back together.
/// </summary>
public interface IColorEngine
{
    /// <summary>Engine kind (Oklch or Hct).</summary>
    ColorEngineKind Kind { get; }

    /// <summary>Display name of the color model.</summary>
    string DisplayName { get; }

    /// <summary>The chroma of a typical vivid brand colour, in this engine's units (OKLCH about 0.17, HCT about 48).</summary>
    double VividChroma { get; }

    /// <summary>Decomposes an RGB color into perceptual coordinates.</summary>
    PerceptualColor Decompose(ColorRgb color);

    /// <summary>Composes perceptual coordinates into displayable sRGB.</summary>
    ColorRgb Compose(double hue, double chroma, double tone);

    /// <summary>The tone (0..100) of a scale stop (50 lightest .. 950 darkest).</summary>
    double ToneOfStop(int stop);

    /// <summary>The 11 stops 50..950 of one hue and chroma, lightest first.</summary>
    IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma);

    /// <summary>The hue turned by <paramref name="degrees"/> in this engine's hue circle, 0..360.</summary>
    double RotateHue(double hue, double degrees);

    /// <summary>
    /// This engine's tone for a CIE L* lightness (exact for greys).
    /// </summary>
    double ToneFromLstar(double lstar);
}

/// <summary>The engines, by kind.</summary>
public static class ColorEngines
{
    /// <summary>OKLCH perceptual engine.</summary>
    public static IColorEngine Oklch { get; } = new OklchEngine();

    /// <summary>Material Design 3 HCT engine.</summary>
    public static IColorEngine Hct { get; } = new HctEngine();

    /// <summary>Gets engine by kind.</summary>
    public static IColorEngine Get(ColorEngineKind kind) => kind == ColorEngineKind.Hct ? Hct : Oklch;

    internal static double SanitizeHue(double hue)
    {
        hue %= 360;
        return hue < 0 ? hue + 360 : hue;
    }
}

/// <summary>
/// OKLCH engine. Tone is OKLab lightness × 100.
/// </summary>
public sealed class OklchEngine : IColorEngine
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Dictionary<int, (double Lightness, double ChromaShare)> Curve = new()
    {
        [50] = (0.975, 0.14),
        [100] = (0.936, 0.28),
        [200] = (0.885, 0.48),
        [300] = (0.810, 0.72),
        [400] = (0.715, 0.92),
        [500] = (0.635, 1.00),
        [600] = (0.555, 0.98),
        [700] = (0.480, 0.88),
        [800] = (0.410, 0.74),
        [900] = (0.345, 0.60),
        [950] = (0.265, 0.46),
    };

    /// <inheritdoc />
    public ColorEngineKind Kind => ColorEngineKind.Oklch;

    /// <inheritdoc />
    public string DisplayName => "OKLCH";

    /// <inheritdoc />
    public double VividChroma => 0.17;

    /// <inheritdoc />
    public PerceptualColor Decompose(ColorRgb color)
    {
        var lch = ColorMath.Oklch.FromRgb(color);
        return new PerceptualColor(lch.H, lch.C, lch.L * 100);
    }

    /// <inheritdoc />
    public ColorRgb Compose(double hue, double chroma, double tone) =>
        new Oklch(Math.Clamp(tone, 0, 100) / 100, Math.Max(0, chroma), ColorEngines.SanitizeHue(hue)).ToRgb();

    /// <inheritdoc />
    public double ToneOfStop(int stop) => (Curve.TryGetValue(stop, out var point) ? point.Lightness : 0.635) * 100;

    /// <inheritdoc />
    public IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma)
    {
        var scale = new Dictionary<int, ColorRgb>(Stops.Length);
        foreach (var stop in Stops)
        {
            var (lightness, share) = Curve[stop];
            scale[stop] = Compose(hue, chroma * share, lightness * 100);
        }

        return scale;
    }

    /// <inheritdoc />
    public double RotateHue(double hue, double degrees) => ColorEngines.SanitizeHue(hue + degrees);

    /// <inheritdoc />
    public double ToneFromLstar(double lstar)
    {
        var y = MaterialColorUtils.YFromLstar(Math.Clamp(lstar, 0, 100)) / 100.0;
        return Math.Cbrt(y) * 100;
    }
}

/// <summary>
/// Material Design 3 HCT engine. Tone is CIE L*.
/// </summary>
public sealed class HctEngine : IColorEngine
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Dictionary<int, double> Tones = new()
    {
        [50] = 95, [100] = 90, [200] = 80, [300] = 70, [400] = 60, [500] = 50,
        [600] = 40, [700] = 30, [800] = 20, [900] = 10, [950] = 5,
    };

    /// <inheritdoc />
    public ColorEngineKind Kind => ColorEngineKind.Hct;

    /// <inheritdoc />
    public string DisplayName => "Material HCT";

    /// <inheritdoc />
    public double VividChroma => 48;

    /// <inheritdoc />
    public PerceptualColor Decompose(ColorRgb color)
    {
        var hct = ColorMath.Hct.FromRgb(color);
        return new PerceptualColor(hct.Hue, hct.Chroma, hct.Tone);
    }

    /// <inheritdoc />
    public ColorRgb Compose(double hue, double chroma, double tone) =>
        new Hct(ColorEngines.SanitizeHue(hue), Math.Max(0, chroma), Math.Clamp(tone, 0, 100)).ToRgb();

    /// <inheritdoc />
    public double ToneOfStop(int stop) => Tones.TryGetValue(stop, out var tone) ? tone : 50;

    /// <inheritdoc />
    public IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma)
    {
        var scale = new Dictionary<int, ColorRgb>(Stops.Length);
        foreach (var stop in Stops) scale[stop] = Compose(hue, chroma, Tones[stop]);
        return scale;
    }

    /// <inheritdoc />
    public double RotateHue(double hue, double degrees) => ColorEngines.SanitizeHue(hue + degrees);

    /// <inheritdoc />
    public double ToneFromLstar(double lstar) => Math.Clamp(lstar, 0, 100);
}
