namespace FryWorld.Sdk.AI;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implemented by plugins and workspaces to provide live, domain-specific contextual snapshots
/// to the host's AI subsystem and external MCP clients.
/// </summary>
public interface IAiContextProvider
{
    /// <summary>
    /// Gets the unique plugin or workspace identifier (e.g. "com.fryworld.circuit").
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Retrieves a fresh contextual snapshot representing the current active document, selection, and domain state.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A snapshot of the active context.</returns>
    Task<AiContextSnapshot> GetCurrentContextAsync(CancellationToken cancellationToken = default);
}
