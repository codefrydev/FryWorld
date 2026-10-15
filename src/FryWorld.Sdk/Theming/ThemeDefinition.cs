using System;
using System.Collections.Generic;

namespace FryWorld.Sdk.Theming;

/// <summary>
/// Serializable theme definition for dark/light themes, color tokens, and typography.
/// </summary>
public sealed class ThemeDefinition
{
    /// <summary>Unique identifier of the theme (e.g. 'dark-plus', 'dracula', 'cyberpunk').</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>User-facing display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Brief summary description of the theme's aesthetic.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Whether this is a dark-variant theme.</summary>
    public bool IsDark { get; init; } = true;

    /// <summary>Explicit color token mappings (e.g., 'M3PrimaryBrush' -> '#BD93F9').</summary>
    public Dictionary<string, string> Colors { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional font family overrides.</summary>
    public Dictionary<string, string> Fonts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional numeric token overrides.</summary>
    public Dictionary<string, double> Numbers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
