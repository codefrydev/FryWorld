using System;
using System.Collections.Generic;
using FryWorld.Sdk.Services;

namespace FryWorld.Sdk.Theming;

/// <summary>
/// Extended runtime theme and color management service for FryWorld and hosted plugins.
/// </summary>
public interface IThemeManager : IThemeService
{
    /// <summary>Unique identifier of the currently active theme preset or harmonic seed.</summary>
    string ActiveThemeId { get; }

    /// <summary>The currently active theme definition.</summary>
    ThemeDefinition? ActiveTheme { get; }

    /// <summary>All available built-in and registered theme presets.</summary>
    IReadOnlyList<ThemeDefinition> GetAvailableThemes();

    /// <summary>Applies a registered theme preset by its identifier (e.g. 'dracula', 'cyberpunk', 'dark-plus').</summary>
    bool ApplyTheme(string themeId);

    /// <summary>Registers a custom theme definition into the catalog.</summary>
    void RegisterTheme(ThemeDefinition theme);

    /// <summary>Generates and applies a deterministic color harmony theme.</summary>
    void ApplyHarmonicTheme(float hueDegrees, ColorHarmonyMode mode, bool isDark = true);

    /// <summary>Generates and applies a randomized, ergonomic color harmony theme.</summary>
    void ApplyRandomHarmonicTheme(bool isDark = true);

    /// <summary>Sets or overrides an individual color token at runtime (e.g. 'M3PrimaryBrush', '#FF5722').</summary>
    void SetColorOverride(string tokenName, string hexOrRgb);

    /// <summary>Gets the current value of a color token, or null if unset.</summary>
    string? GetColor(string tokenName);

    /// <summary>Resets all custom color overrides back to the active theme's defaults.</summary>
    void ResetToDefaults();

    /// <summary>Registers an extensible token contributor that generates domain-specific tokens when themes change.</summary>
    /// <param name="contributor">The token contributor instance.</param>
    /// <returns>A disposable handle that unregisters the contributor when disposed.</returns>
    IDisposable RegisterTokenContributor(IThemeTokenContributor contributor);

    /// <summary>Unregisters an extensible token contributor by its unique identifier.</summary>
    bool UnregisterTokenContributor(string contributorId);

    /// <summary>All currently registered token contributors.</summary>
    IReadOnlyList<IThemeTokenContributor> GetTokenContributors();

    /// <summary>
    /// Tracks an Avalonia styled element (such as a Window or top-level view) so dynamic theme tokens
    /// are attached directly to its resources, ensuring immediate single-notification recoloring of all child controls.
    /// </summary>
    void TrackResourceRoot(Avalonia.StyledElement element);

    /// <summary>Raised whenever the active theme definition or dynamic layer changes, passing the new theme id.</summary>
    event Action<string>? ThemeDefinitionChanged;
}
