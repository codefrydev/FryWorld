namespace FryWorld.Sdk.AI;

using Microsoft.Extensions.AI;

/// <summary>
/// Describes a callable tool or action exposed by a plugin or the host to in-app AI models
/// and external Model Context Protocol (MCP) clients.
/// </summary>
public class AiToolDescriptor
{
    /// <summary>
    /// Gets the unique programmatic name of the tool (e.g. "circuit_add_component", "pdf_extract_text").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a clear, functional description of what the tool accomplishes and when the LLM should invoke it.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets an optional category grouping for the tool (e.g. "Circuit CAD", "File Operations").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the ID of the plugin that registered this tool, or null if contributed by the host.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Gets whether executing this tool performs destructive mutations, file writes, or shell commands
    /// requiring explicit user approval before execution.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Gets the executable <see cref="AIFunction"/> instance representing this tool.
    /// </summary>
    public required AIFunction Function { get; init; }
}
