using System;
using System.Collections.Generic;
using FryWorld.Sdk.Descriptors;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Centralized registry for keyboard shortcuts, commands, and keybinding overrides in FryWorld.
/// Supports multi-tier precedence (Host, Plugin, Keymap, User) and context-aware resolution.
/// </summary>
public interface IShortcutRegistry
{
    /// <summary>
    /// Raised whenever a shortcut binding is added, modified, or removed, or when user overrides change.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Registers a rich shortcut binding.
    /// </summary>
    /// <param name="binding">The shortcut binding.</param>
    /// <returns>A disposable handle that unregisters the shortcut when disposed.</returns>
    IDisposable Register(ShortcutBinding binding);

    /// <summary>
    /// Registers a shortcut from a <see cref="ShortcutDescriptor"/> for compatibility with existing plugins.
    /// </summary>
    /// <param name="descriptor">The shortcut descriptor.</param>
    /// <returns>A disposable handle that unregisters the shortcut when disposed.</returns>
    IDisposable Register(ShortcutDescriptor descriptor);

    /// <summary>
    /// Gets an immutable snapshot of all registered shortcut bindings across all tiers.
    /// </summary>
    IReadOnlyList<ShortcutBinding> GetRegisteredBindings();

    /// <summary>
    /// Gets all shortcut bindings currently active and applicable for the specified context.
    /// Ordered by priority and precedence tier (highest first).
    /// </summary>
    /// <param name="context">The active context to match against.</param>
    /// <returns>List of active bindings.</returns>
    IReadOnlyList<ShortcutBinding> GetActiveBindings(IShortcutContext context);

    /// <summary>
    /// Resolves the effective, platform-adapted display gesture string for the given command ID within an optional scope.
    /// Returns the highest-precedence binding's display string (e.g. "⌘S" or "Ctrl+S").
    /// </summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="scope">Optional scope filter; if null, matches active or global bindings.</param>
    /// <returns>The formatted gesture string, or null if no binding is found.</returns>
    string? ResolveDisplayGesture(string commandId, string? scope = null);

    /// <summary>
    /// Registers or updates a user keybinding override (Tier 3).
    /// Overrides persist and shadow default plugin and host bindings.
    /// </summary>
    /// <param name="userOverride">The user override binding.</param>
    void RegisterUserOverride(ShortcutBinding userOverride);

    /// <summary>
    /// Clears all user keybinding overrides, restoring default host and plugin keybindings.
    /// </summary>
    void ResetUserOverrides();
}
