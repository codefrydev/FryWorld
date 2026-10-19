namespace FryWorld.Sdk.AI;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

/// <summary>
/// Dynamic central registry of AI tools contributed by plugins and the host.
/// </summary>
public interface IAiToolRegistry
{
    /// <summary>
    /// Raised whenever a tool is registered or unregistered.
    /// </summary>
    event Action? ToolsChanged;

    /// <summary>
    /// Gets all currently registered tool descriptors.
    /// </summary>
    /// <returns>A read-only list of tool descriptors.</returns>
    IReadOnlyList<AiToolDescriptor> GetRegisteredTools();

    /// <summary>
    /// Gets the list of <see cref="AITool"/> instances suitable for passing to
    /// <see cref="ChatOptions.Tools"/>, optionally filtered by plugin scope.
    /// </summary>
    /// <param name="pluginScope">Optional plugin ID to filter tools, or null for all tools.</param>
    /// <returns>A list of AI tools.</returns>
    IReadOnlyList<AITool> GetAiTools(string? pluginScope = null);

    /// <summary>
    /// Attempts to find a registered tool by its name.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="tool">The retrieved tool descriptor if found.</param>
    /// <returns>True if the tool is registered; otherwise false.</returns>
    bool TryGetTool(string toolName, [NotNullWhen(true)] out AiToolDescriptor? tool);
}
