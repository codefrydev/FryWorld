using System;
using System.Collections.Generic;
using FryWorld.Sdk.Theming.Palette;

namespace FryWorld.Sdk.Theming;

/// <summary>
/// Allows a plugin, subsystem, or application to contribute domain-specific color and brush tokens
/// derived from a generated perceptual palette or active theme definition.
/// </summary>
public interface IThemeTokenContributor
{
    /// <summary>
    /// Unique identifier or domain prefix of the contributor (e.g. 'csharpstudio', 'imageeditor', 'musicplayer').
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Contributes domain-specific color tokens into the target dictionary when a perceptual palette is procedurally generated.
    /// </summary>
    /// <param name="palette">The active procedurally synthesized palette.</param>
    /// <param name="tokens">Mutable dictionary of color and brush token mappings.</param>
    void ContributeTokens(GeneratedPalette palette, IDictionary<string, string> tokens);

    /// <summary>
    /// Contributes or derives fallback domain-specific tokens when a static theme definition is activated.
    /// </summary>
    /// <param name="theme">The active theme definition.</param>
    /// <param name="tokens">Mutable dictionary of color and brush token mappings.</param>
    void ContributeTokens(ThemeDefinition theme, IDictionary<string, string> tokens);
}
