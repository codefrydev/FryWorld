namespace FryWorld.Sdk.AI;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Dynamic central registry of prompt templates contributed by plugins and the host.
/// </summary>
public interface IAiPromptRegistry
{
    /// <summary>
    /// Raised whenever a prompt is registered or unregistered.
    /// </summary>
    event Action? PromptsChanged;

    /// <summary>
    /// Gets all currently registered prompt templates.
    /// </summary>
    /// <returns>A read-only list of prompt descriptors.</returns>
    IReadOnlyList<AiPromptDescriptor> GetRegisteredPrompts();

    /// <summary>
    /// Attempts to find a registered prompt template by its identifier.
    /// </summary>
    /// <param name="promptId">The prompt ID.</param>
    /// <param name="prompt">The prompt descriptor if found.</param>
    /// <returns>True if found; otherwise false.</returns>
    bool TryGetPrompt(string promptId, [NotNullWhen(true)] out AiPromptDescriptor? prompt);
}
