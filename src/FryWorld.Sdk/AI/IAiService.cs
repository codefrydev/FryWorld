namespace FryWorld.Sdk.AI;

using Microsoft.Extensions.AI;

/// <summary>
/// Universal AI service provided by the FryWorld host to plugins, workspaces, and tools.
/// Supplies configured chat clients, context discovery, tool dispatch, and prompt templates.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Gets the default configured <see cref="IChatClient"/> with automatic tool invocation enabled.
    /// </summary>
    IChatClient ChatClient { get; }

    /// <summary>
    /// Gets the context manager for inspecting active workspace and document snapshots.
    /// </summary>
    IAiContextManager ContextManager { get; }

    /// <summary>
    /// Gets the central registry of callable AI tools.
    /// </summary>
    IAiToolRegistry Tools { get; }

    /// <summary>
    /// Gets the central registry of turnkey prompt templates.
    /// </summary>
    IAiPromptRegistry Prompts { get; }

    /// <summary>
    /// Creates a new <see cref="IChatClient"/> instance configured with the specified options.
    /// </summary>
    /// <param name="options">Optional configuration overrides; if null, uses host defaults.</param>
    /// <returns>A configured chat client.</returns>
    IChatClient CreateChatClient(AiClientOptions? options = null);
}
