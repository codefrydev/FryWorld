using System;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Activation scopes for keyboard shortcuts and commands.
/// Shortcuts are only active when their declared scope matches the current application context.
/// </summary>
public static class ShortcutScope
{
    /// <summary>
    /// Global scope: active everywhere in the host application unless shadowed by a higher-priority focused scope.
    /// </summary>
    public const string Global = "global";

    /// <summary>
    /// Prefix used for workspace-level scopes (e.g. "plugin:circuit", "plugin:csharp").
    /// </summary>
    public const string WorkspacePrefix = "plugin:";

    /// <summary>
    /// Prefix used for floating shell overlay scopes (e.g. "overlay:circuit.overlay").
    /// </summary>
    public const string OverlayPrefix = "overlay:";

    /// <summary>
    /// Creates a scope identifier for a specific plugin workspace.
    /// </summary>
    /// <param name="pluginId">The unique plugin ID.</param>
    /// <returns>Scope string formatted as "plugin:{pluginId}".</returns>
    public static string Plugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return pluginId.StartsWith(WorkspacePrefix, StringComparison.OrdinalIgnoreCase)
            ? pluginId
            : $"{WorkspacePrefix}{pluginId}";
    }

    /// <summary>
    /// Creates a scope identifier for a floating shell overlay window.
    /// </summary>
    /// <param name="overlayId">The unique overlay ID.</param>
    /// <returns>Scope string formatted as "overlay:{overlayId}".</returns>
    public static string Overlay(string overlayId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overlayId);
        return overlayId.StartsWith(OverlayPrefix, StringComparison.OrdinalIgnoreCase)
            ? overlayId
            : $"{OverlayPrefix}{overlayId}";
    }

    /// <summary>
    /// Creates a scope identifier for a focused control or editor region.
    /// </summary>
    /// <param name="scopeId">The specific control scope name (e.g. "circuit.canvas").</param>
    /// <returns>The normalized scope string.</returns>
    public static string Focus(string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        return scopeId.Trim();
    }

    /// <summary>
    /// Normalizes a scope string by stripping known prefixes ("plugin:", "overlay:").
    /// </summary>
    /// <param name="scope">The scope string to normalize.</param>
    /// <returns>The normalized scope name without prefix.</returns>
    public static string Normalize(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return Global;

        var trimmed = scope.Trim();
        if (trimmed.StartsWith(WorkspacePrefix, StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(WorkspacePrefix.Length);
        if (trimmed.StartsWith(OverlayPrefix, StringComparison.OrdinalIgnoreCase))
            return trimmed.Substring(OverlayPrefix.Length);

        return trimmed;
    }

    /// <summary>
    /// Checks whether a declared binding scope matches any of the active scopes.
    /// Handles prefix normalization ("plugin:circuit" matches "circuit" and vice-versa).
    /// </summary>
    /// <param name="bindingScope">The scope declared on the shortcut binding.</param>
    /// <param name="activeScopes">The list of currently active scopes.</param>
    /// <returns>True if the scope is active or global.</returns>
    public static bool Matches(string bindingScope, System.Collections.Generic.IEnumerable<string> activeScopes)
    {
        ArgumentNullException.ThrowIfNull(activeScopes);

        if (string.IsNullOrWhiteSpace(bindingScope) || bindingScope.Equals(Global, StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedBinding = Normalize(bindingScope);

        foreach (var s in activeScopes)
        {
            if (s.Equals(Global, StringComparison.OrdinalIgnoreCase))
                continue;

            if (s.Equals(bindingScope, StringComparison.OrdinalIgnoreCase) ||
                Normalize(s).Equals(normalizedBinding, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
