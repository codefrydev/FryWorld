namespace FryWorld.Sdk.Theming;

/// <summary>
/// Classical color wheel harmony relationships.
/// </summary>
public enum ColorHarmonyMode
{
    /// <summary>Adjacent hues on the color wheel (30° offset).</summary>
    Analogous,
    /// <summary>Directly opposite hues on the color wheel (180° offset).</summary>
    Complementary,
    /// <summary>Split opposite hues (150° and 210° offset).</summary>
    SplitComplementary,
    /// <summary>Equilateral triangular placement (120° offsets).</summary>
    Triadic,
    /// <summary>Rectangular placement (90° offsets).</summary>
    Tetradic,
    /// <summary>Single hue with varying saturation and tonal contrast.</summary>
    Monochromatic
}
