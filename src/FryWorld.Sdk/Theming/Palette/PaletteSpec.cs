using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FryWorld.Sdk.Theming.ColorMath;

namespace FryWorld.Sdk.Theming.Palette;

/// <summary>
/// The sections of a palette, each of which can be locked, set by hand or regenerated on its own:
/// the core roles, the syntax colours and the chart series.
/// </summary>
public static class PaletteSections
{
    /// <summary>Primary accent section.</summary>
    public const string Primary = "Primary";
    /// <summary>Secondary accent section.</summary>
    public const string Secondary = "Secondary";
    /// <summary>Tertiary accent section.</summary>
    public const string Tertiary = "Tertiary";
    /// <summary>Neutral surface section.</summary>
    public const string Neutral = "Neutral";
    /// <summary>Neutral variant section for borders and muted elements.</summary>
    public const string NeutralVariant = "NeutralVariant";
    /// <summary>Success status section.</summary>
    public const string Success = "Success";
    /// <summary>Warning status section.</summary>
    public const string Warning = "Warning";
    /// <summary>Error status section.</summary>
    public const string Error = "Error";
    /// <summary>Info status section.</summary>
    public const string Info = "Info";

    /// <summary>Prefix for syntax roles.</summary>
    public const string SyntaxPrefix = "Syntax.";
    /// <summary>Prefix for chart series.</summary>
    public const string ChartPrefix = "Chart.";

    /// <summary>Syntax roles, in the order the generator places them.</summary>
    public static readonly IReadOnlyList<string> SyntaxRoles =
        ["Keyword", "Type", "Function", "String", "Number", "Preprocessor", "Variable", "Comment", "Punctuation"];

    /// <summary>Number of chart series.</summary>
    public const int ChartSeriesCount = 8;

    /// <summary>Core sections list.</summary>
    public static readonly IReadOnlyList<string> Core = [Primary, Secondary, Tertiary, Neutral, NeutralVariant, Success, Warning, Error, Info];

    /// <summary>Syntax sections list.</summary>
    public static readonly IReadOnlyList<string> Syntax = SyntaxRoles.Select(r => SyntaxPrefix + r).ToArray();

    /// <summary>Chart sections list.</summary>
    public static readonly IReadOnlyList<string> Charts = Enumerable.Range(1, ChartSeriesCount).Select(i => ChartPrefix + i).ToArray();

    /// <summary>All known sections.</summary>
    public static readonly IReadOnlyList<string> All = [.. Core, .. Syntax, .. Charts];

    /// <summary>The roles harmony places around the colour wheel.</summary>
    public static readonly IReadOnlyList<string> Chromatic = [Primary, Secondary, Tertiary];

    /// <summary>Whether a section identifier is recognized.</summary>
    public static bool IsKnown(string sectionId) => All.Contains(sectionId, StringComparer.Ordinal);

    /// <summary>Section id for a syntax role.</summary>
    public static string SyntaxSection(string role) => SyntaxPrefix + role;

    /// <summary>Section id for a chart index.</summary>
    public static string Chart(int series) => ChartPrefix + series;
}

/// <summary>Where a section's colour comes from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionSource>))]
public enum SectionSource
{
    /// <summary>Placed exactly by the harmony mode.</summary>
    Harmony,

    /// <summary>Placed by the harmony mode with a little seeded variation.</summary>
    Random,

    /// <summary>Set by the user (a hex value, a picked colour).</summary>
    Manual,
}

/// <summary>One section of a palette.</summary>
public sealed record SectionSpec
{
    /// <summary>The section's colour when it is fixed (locked or set by hand), as #RRGGBB; <c>null</c> to generate it.</summary>
    public string? KeyColor { get; init; }

    /// <summary>A locked section never changes when the palette is regenerated.</summary>
    public bool Locked { get; init; }

    /// <summary>Source of section's color.</summary>
    public SectionSource Source { get; init; } = SectionSource.Harmony;
}

/// <summary>
/// Everything a palette is generated from. Immutable and deterministic.
/// </summary>
public sealed record PaletteSpec
{
    /// <summary>Current spec version.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Spec schema version.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Color engine kind.</summary>
    public ColorEngineKind Engine { get; init; } = ColorEngineKind.Oklch;

    /// <summary>Drives the variation of sections whose source is <see cref="SectionSource.Random"/>.</summary>
    public int Seed { get; init; }

    /// <summary>Color harmony relationship.</summary>
    public ColorHarmonyMode Harmony { get; init; } = ColorHarmonyMode.Complementary;

    /// <summary>The primary hue, in the engine's hue circle (degrees).</summary>
    public double BaseHue { get; init; } = 250;

    /// <summary>Whether the scheme is dark or light.</summary>
    public bool IsDark { get; init; } = true;

    /// <summary>Multiplies every role's chroma (1 = the engine's usual vivid level).</summary>
    public double ChromaBoost { get; init; } = 1.0;

    /// <summary>How much of the primary hue tints the greys, 0..35.</summary>
    public double NeutralTint { get; init; } = 10;

    /// <summary>How far success, warning, error and info lean towards the primary hue, 0..100.</summary>
    public double SemanticPull { get; init; } = 15;

    /// <summary>The WCAG ratio text and accents must reach on their surfaces: 4.5 (AA) or 7 (AAA).</summary>
    public double ContrastTarget { get; init; } = 4.5;

    /// <summary>Sections by id; a missing section is generated by harmony.</summary>
    public IReadOnlyDictionary<string, SectionSpec> Sections { get; init; } = new Dictionary<string, SectionSpec>(StringComparer.Ordinal);

    /// <summary>Gets section spec by section id.</summary>
    public SectionSpec Section(string sectionId) => Sections.TryGetValue(sectionId, out var section) ? section : Default;

    /// <summary>Checks whether a section is locked.</summary>
    public bool IsLocked(string sectionId) => Section(sectionId).Locked;

    /// <summary>This spec with one section replaced.</summary>
    public PaletteSpec WithSection(string sectionId, SectionSpec section)
    {
        if (!PaletteSections.IsKnown(sectionId)) throw new ArgumentException($"Unknown palette section '{sectionId}'.", nameof(sectionId));
        var sections = new Dictionary<string, SectionSpec>(Sections, StringComparer.Ordinal) { [sectionId] = section };
        return this with { Sections = sections };
    }

    /// <summary>Serializes spec to JsonElement.</summary>
    public JsonElement ToJson() => JsonSerializer.SerializeToElement(this, JsonOptions);

    /// <summary>A spec read back from JSON; <c>null</c> when invalid.</summary>
    public static PaletteSpec? FromJson(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(nameof(Version), out _)) return null;
        try
        {
            var spec = element.Deserialize<PaletteSpec>(JsonOptions);
            if (spec == null || spec.Version > CurrentVersion) return null;
            var sections = new Dictionary<string, SectionSpec>(StringComparer.Ordinal);
            foreach (var (id, section) in spec.Sections ?? new Dictionary<string, SectionSpec>())
            {
                if (PaletteSections.IsKnown(id) && section != null) sections[id] = section;
            }

            return spec with { Sections = sections };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly SectionSpec Default = new();
}
