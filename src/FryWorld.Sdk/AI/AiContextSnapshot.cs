namespace FryWorld.Sdk.AI;

using System.Collections.Generic;

/// <summary>
/// Represents a structured contextual snapshot of a workspace, document, or plugin state
/// to provide grounding for AI models and external Model Context Protocol (MCP) clients.
/// </summary>
public record AiContextSnapshot
{
    /// <summary>
    /// Gets the unique identifier of the contributing plugin or workspace (e.g. "com.fryworld.circuit").
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets the human-readable title or headline for this context (e.g. "Inverting Op-Amp Schematic").
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the name of the active document or file, if applicable (e.g. "Amplifier.fcircuit").
    /// </summary>
    public string? ActiveDocumentName { get; init; }

    /// <summary>
    /// Gets the absolute or workspace-relative path to the active document, if applicable.
    /// </summary>
    public string? ActiveDocumentPath { get; init; }

    /// <summary>
    /// Gets the MIME or content type of the active content (e.g. "application/json; schema=fcircuit", "text/x-csharp").
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the primary payload or serialized state of the active document.
    /// </summary>
    public string? ActiveContent { get; init; }

    /// <summary>
    /// Gets the currently selected text, component, or AST node, if any.
    /// </summary>
    public string? SelectedContent { get; init; }

    /// <summary>
    /// Gets domain-specific system prompt guidelines, component descriptions, or schemas
    /// that should be injected into conversational LLM sessions when this workspace is active.
    /// </summary>
    public string? SystemPromptAugmentation { get; init; }

    /// <summary>
    /// Gets optional domain metadata key-value pairs (e.g. "SimulationState": "Running", "NodeCount": "12").
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
