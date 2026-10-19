namespace FryWorld.Sdk.AI;

using System.Collections.Generic;

/// <summary>
/// Describes a turnkey prompt template contributed by a plugin or the host.
/// </summary>
public class AiPromptDescriptor
{
    /// <summary>
    /// Gets the unique identifier of the prompt template (e.g. "circuit.design_filter").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the user-facing title of the prompt template.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets a short description explaining what this prompt accomplishes.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets an optional category (e.g. "Analog Filters", "Roslyn Refactoring").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the ID of the plugin that registered this prompt, or null if host-provided.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Gets the raw prompt template text, optionally containing placeholder tokens (e.g. "{{cutoff_freq}}").
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// Gets the optional list of argument or variable names expected by the template.
    /// </summary>
    public IReadOnlyList<string>? Arguments { get; init; }
}
