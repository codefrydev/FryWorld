using System;
using System.Linq;
using FryWorld.Sdk.Theming;
using FryWorld.Sdk.Theming.ColorMath;
using FryWorld.Sdk.Theming.Palette;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class ThemingScienceTests
{
    [Fact]
    public void ColorRgb_HexParsing_And_Contrast()
    {
        Assert.True(ColorRgb.TryParseHex("#FF5722", out var c1));
        Assert.Equal(255, c1.R);
        Assert.Equal(87, c1.G);
        Assert.Equal(34, c1.B);

        var white = ColorRgb.White;
        var black = ColorRgb.Black;

        float ratio = white.ContrastRatio(black);
        Assert.InRange(ratio, 20.9f, 21.1f);
    }

    [Fact]
    public void Oklab_Roundtrip_PrecisionMatches()
    {
        var original = new ColorRgb(47, 129, 247); // Blue
        var oklch = Oklch.FromRgb(original);
        var restored = oklch.ToRgb();

        Assert.True(Math.Abs(original.R - restored.R) <= 1);
        Assert.True(Math.Abs(original.G - restored.G) <= 1);
        Assert.True(Math.Abs(original.B - restored.B) <= 1);
    }

    [Fact]
    public void MaterialHct_Roundtrip_PreservesTone()
    {
        var original = new ColorRgb(189, 147, 249); // Dracula Purple
        var hct = Hct.FromRgb(original);
        var restored = hct.ToRgb();

        Assert.InRange(hct.Tone, 60.0, 75.0);
        Assert.True(Math.Abs(original.R - restored.R) <= 2);
        Assert.True(Math.Abs(original.G - restored.G) <= 2);
        Assert.True(Math.Abs(original.B - restored.B) <= 2);
    }

    [Fact]
    public void ContrastSolver_EnsuresTargetContrastRatio()
    {
        var surface = new ColorRgb(22, 27, 34); // Dark surface
        var engine = ColorEngines.Oklch;

        var solved = ContrastSolver.SolveTone(engine, 210, 0.15, 70, surface, minRatio: 4.5f);

        Assert.True(solved.IsSatisfied);
        Assert.True(solved.Ratio >= 4.5f);
        Assert.True(solved.Color.ContrastRatio(surface) >= 4.5f);
    }

    [Fact]
    public void ApcaEngine_CalculatesContrastLc()
    {
        var white = ColorRgb.White;
        var black = ColorRgb.Black;

        float darkOnLight = ApcaEngine.ContrastLc(black, white);
        Assert.True(darkOnLight > 100f);

        float lightOnDark = ApcaEngine.ContrastLc(white, black);
        Assert.True(lightOnDark < -100f);

        Assert.True(ApcaEngine.IsBodyTextCompliant(darkOnLight));
        Assert.True(ApcaEngine.IsBodyTextCompliant(lightOnDark));
    }

    [Fact]
    public void ColorBlindnessEngine_SimulateAndDistinct()
    {
        var green = new ColorRgb(46, 160, 67);
        var red = new ColorRgb(248, 81, 73);

        var deuteranopiaGreen = ColorBlindnessEngine.Simulate(green, VisionDeficiency.Deuteranopia);
        var deuteranopiaRed = ColorBlindnessEngine.Simulate(red, VisionDeficiency.Deuteranopia);

        Assert.NotEqual(green, deuteranopiaGreen);
        Assert.NotEqual(red, deuteranopiaRed);
    }

    [Fact]
    public void PaletteGenerator_ProducesCompleteTonalRamps()
    {
        var spec = new PaletteSpec
        {
            BaseHue = 220,
            IsDark = true,
            Harmony = ColorHarmonyMode.Complementary
        };

        var palette = PaletteGenerator.Generate(spec);
        Assert.NotNull(palette);
        Assert.Equal(PaletteSections.Core.Count, palette.Scales.Count);

        foreach (var (role, scale) in palette.Scales)
        {
            Assert.Equal(11, scale.Count);
            foreach (var stop in TonalScaleEngine.StandardStops)
            {
                Assert.True(scale.ContainsKey(stop));
            }
        }
    }

    [Theory]
    [InlineData(ColorHarmonyMode.Analogous)]
    [InlineData(ColorHarmonyMode.Complementary)]
    [InlineData(ColorHarmonyMode.SplitComplementary)]
    [InlineData(ColorHarmonyMode.Triadic)]
    [InlineData(ColorHarmonyMode.Tetradic)]
    [InlineData(ColorHarmonyMode.Monochromatic)]
    public void HarmonicColorGenerator_GeneratesAllModes(ColorHarmonyMode mode)
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(210f, mode, isDark: true);

        Assert.NotNull(theme);
        Assert.NotEmpty(theme.Id);
        Assert.True(theme.IsDark);
        Assert.True(theme.Colors.ContainsKey("M3SurfaceBrush"));
        Assert.True(theme.Colors.ContainsKey("M3PrimaryBrush"));
        Assert.True(theme.Colors.ContainsKey("M3OnSurfaceBrush"));
    }

    [Fact]
    public void ThemeTokenMapper_WithoutContributors_ContainsCoreTokensOnly()
    {
        var spec = new PaletteSpec { BaseHue = 200, IsDark = true };
        var palette = PaletteGenerator.Generate(spec);
        var tokens = ThemeTokenMapper.Map(palette);

        Assert.True(tokens.ContainsKey("M3PrimaryBrush"));
        Assert.True(tokens.ContainsKey("M3SurfaceBrush"));
        Assert.True(tokens.ContainsKey("TonalP500"));

        // Domain tokens are not present when no contributors are registered
        Assert.False(tokens.ContainsKey("DsPrimaryBrush"));
        Assert.False(tokens.ContainsKey("PsAccentBrush"));
    }

    [Fact]
    public void ThemeTokenMapper_WithCustomPluginContributor_AddsDomainTokens()
    {
        var spec = new PaletteSpec { BaseHue = 200, IsDark = true };
        var palette = PaletteGenerator.Generate(spec);

        var contributor = new TestPluginTokenContributor();
        var tokens = ThemeTokenMapper.Map(palette, [contributor, new FryWorld.Sdk.Theming.Contributors.DeveloperStudioTokenContributor()]);

        Assert.True(tokens.ContainsKey("M3PrimaryBrush"));
        Assert.True(tokens.ContainsKey("DsPrimaryBrush"));
        Assert.True(tokens.ContainsKey("TestPluginWaveformBrush"));
        Assert.Equal("#112233", tokens["TestPluginWaveformBrush"]);
    }

    [Fact]
    public void BuiltInThemes_HaveCompleteTokenCoverage()
    {
        Assert.True(BuiltInThemes.All.Count >= 7);

        foreach (var theme in BuiltInThemes.All)
        {
            Assert.NotEmpty(theme.Id);
            Assert.NotEmpty(theme.Name);
            Assert.True(theme.Colors.ContainsKey("M3SurfaceBrush"), $"Theme {theme.Id} missing M3SurfaceBrush");
            Assert.True(theme.Colors.ContainsKey("M3PrimaryBrush"), $"Theme {theme.Id} missing M3PrimaryBrush");
            Assert.True(theme.Colors.ContainsKey("M3OnSurfaceBrush"), $"Theme {theme.Id} missing M3OnSurfaceBrush");
        }
    }

    private sealed class TestPluginTokenContributor : IThemeTokenContributor
    {
        public string Id => "test.musicplayer";

        public void ContributeTokens(GeneratedPalette palette, IDictionary<string, string> tokens)
        {
            tokens["TestPluginWaveformBrush"] = "#112233";
        }

        public void ContributeTokens(ThemeDefinition theme, IDictionary<string, string> tokens)
        {
            tokens["TestPluginWaveformBrush"] = "#112233";
        }
    }
}
