namespace FryWorld.Services.AI;

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;

/// <summary>
/// Monitors and aggregates context from the currently focused plugin workspace.
/// </summary>
public class AiContextManager : IAiContextManager
{
    private readonly IFryPluginContext _context;
    private readonly Func<string?> _getActivePluginId;

    public event Action<AiContextSnapshot?>? ActiveContextChanged;

    public AiContextManager(IFryPluginContext context, Func<string?> getActivePluginId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _getActivePluginId = getActivePluginId ?? throw new ArgumentNullException(nameof(getActivePluginId));

        _context.AiContextProvidersChanged += () =>
        {
            _ = Task.Run(async () =>
            {
                var snapshot = await GetActiveContextAsync();
                ActiveContextChanged?.Invoke(snapshot);
            });
        };
    }

    /// <summary>
    /// Notifies listeners that the active navigation item or focused document has changed.
    /// </summary>
    public void NotifyActiveWorkspaceChanged()
    {
        _ = Task.Run(async () =>
        {
            var snapshot = await GetActiveContextAsync();
            ActiveContextChanged?.Invoke(snapshot);
        });
    }

    public async Task<AiContextSnapshot?> GetActiveContextAsync(CancellationToken cancellationToken = default)
    {
        var activePluginId = _getActivePluginId();
        if (!string.IsNullOrWhiteSpace(activePluginId))
        {
            // Normalize ID: e.g. "dyn.com.fryworld.circuit" -> "com.fryworld.circuit"
            var cleanId = activePluginId.StartsWith("dyn.") ? activePluginId["dyn.".Length..] : activePluginId;

            var providers = _context.GetRegisteredAiContextProviders();
            var matchedProvider = providers.FirstOrDefault(p =>
                string.Equals(p.PluginId, cleanId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.PluginId, activePluginId, StringComparison.OrdinalIgnoreCase) ||
                cleanId.EndsWith(p.PluginId, StringComparison.OrdinalIgnoreCase));

            if (matchedProvider != null)
            {
                try
                {
                    return await matchedProvider.GetCurrentContextAsync(cancellationToken);
                }
                catch
                {
                    // Fall back to general plugin snapshot if provider throws
                }
            }

            // Fallback for registered plugin without specialized context provider
            var nav = _context.GetRegisteredNavigationItems().FirstOrDefault(n =>
                string.Equals(n.Id, cleanId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n.Id, activePluginId, StringComparison.OrdinalIgnoreCase));

            return new AiContextSnapshot
            {
                PluginId = cleanId,
                Title = nav?.Title ?? cleanId,
                SystemPromptAugmentation = $"The user is currently focused on the '{nav?.Title ?? cleanId}' workspace."
            };
        }

        // Launcher / shell context
        return new AiContextSnapshot
        {
            PluginId = "core.launcher",
            Title = "Suite Launcher",
            SystemPromptAugmentation = "The user is currently at the FryWorld suite launcher home."
        };
    }

    public async Task<string> BuildSystemPromptAsync(string basePrompt, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(basePrompt);
        sb.AppendLine();

        var snapshot = await GetActiveContextAsync(cancellationToken);
        if (snapshot != null)
        {
            sb.AppendLine("# Active Workspace Context");
            sb.AppendLine($"- Workspace: {snapshot.Title} (`{snapshot.PluginId}`)");

            if (!string.IsNullOrWhiteSpace(snapshot.ActiveDocumentName))
            {
                sb.AppendLine($"- Active Document: `{snapshot.ActiveDocumentName}`");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ActiveDocumentPath))
            {
                sb.AppendLine($"- File Path: `{snapshot.ActiveDocumentPath}`");
            }

            if (snapshot.Metadata != null && snapshot.Metadata.Count > 0)
            {
                sb.AppendLine("- Workspace Metadata:");
                foreach (var (k, v) in snapshot.Metadata)
                {
                    sb.AppendLine($"  * {k}: {v}");
                }
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SystemPromptAugmentation))
            {
                sb.AppendLine();
                sb.AppendLine("## Workspace Specific Guidance & Schema");
                sb.AppendLine(snapshot.SystemPromptAugmentation);
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SelectedContent))
            {
                sb.AppendLine();
                sb.AppendLine("## User Selection");
                sb.AppendLine("```");
                sb.AppendLine(snapshot.SelectedContent);
                sb.AppendLine("```");
            }
        }

        return sb.ToString();
    }
}
