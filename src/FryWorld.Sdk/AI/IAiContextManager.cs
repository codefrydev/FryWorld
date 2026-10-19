namespace FryWorld.Sdk.AI;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Aggregates and monitors active workspace context across the host application and all mounted plugins.
/// </summary>
public interface IAiContextManager
{
    /// <summary>
    /// Raised whenever the active workspace or document context changes.
    /// </summary>
    event Action<AiContextSnapshot?>? ActiveContextChanged;

    /// <summary>
    /// Gets the current active workspace snapshot from the currently focused plugin or workspace.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The active snapshot, or null if no plugin context is active.</returns>
    Task<AiContextSnapshot?> GetActiveContextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Assembles an augmented system prompt combining base instructions with the active workspace guidelines and schema.
    /// </summary>
    /// <param name="basePrompt">The base system prompt.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The fully aggregated system prompt.</returns>
    Task<string> BuildSystemPromptAsync(string basePrompt, CancellationToken cancellationToken = default);
}
