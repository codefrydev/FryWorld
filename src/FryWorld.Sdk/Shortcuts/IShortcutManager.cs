using Avalonia.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Runtime input routing and state management service for keyboard shortcuts.
/// Handles key event routing, focus/input shielding, multi-key chords, and context evaluation.
/// </summary>
public interface IShortcutManager
{
    /// <summary>
    /// Gets the underlying shortcut registry.
    /// </summary>
    IShortcutRegistry Registry { get; }

    /// <summary>
    /// Gets the current live context snapshot.
    /// </summary>
    IShortcutContext CurrentContext { get; }

    /// <summary>
    /// Processes an incoming Avalonia <see cref="KeyEventArgs"/>.
    /// Resolves active scopes, shields focused text inputs, matches gestures/chords, and dispatches the winning command.
    /// </summary>
    /// <param name="e">The key event arguments.</param>
    /// <param name="overrideContext">Optional explicit context; if null, uses the dynamic current context.</param>
    /// <returns>True if a shortcut matched and was executed (the event is marked Handled).</returns>
    bool ProcessKeyDown(KeyEventArgs e, IShortcutContext? overrideContext = null);

    /// <summary>
    /// Sets the current primary workspace or overlay scope.
    /// </summary>
    /// <param name="scope">The active scope identifier (e.g. "plugin:circuit" or "global").</param>
    void SetActiveScope(string scope);

    /// <summary>
    /// Pushes a temporary local scope onto the active scope stack (e.g. entering modal tool or sub-canvas).
    /// </summary>
    /// <param name="scope">The scope to push.</param>
    void PushScope(string scope);

    /// <summary>
    /// Pops a temporary local scope from the active scope stack.
    /// </summary>
    /// <param name="scope">The scope to pop.</param>
    void PopScope(string scope);

    /// <summary>
    /// Sets a dynamic context key state used by "When" expressions (e.g. "circuit.hasSelection" = true).
    /// </summary>
    /// <param name="key">The key name.</param>
    /// <param name="value">The key value.</param>
    void SetContextKey(string key, object? value);

    /// <summary>
    /// Removes a dynamic context key state.
    /// </summary>
    /// <param name="key">The key name to remove.</param>
    void RemoveContextKey(string key);

    /// <summary>
    /// Registers an active execution command for a specific command ID.
    /// </summary>
    /// <param name="commandId">The semantic command ID (e.g. "circuit.tool.wire").</param>
    /// <param name="command">The command implementation.</param>
    /// <returns>A disposable handle that unregisters the command handler when disposed.</returns>
    System.IDisposable RegisterCommandHandler(string commandId, System.Windows.Input.ICommand command);

    /// <summary>
    /// Registers an active execution action for a specific command ID.
    /// </summary>
    /// <param name="commandId">The semantic command ID.</param>
    /// <param name="action">The action delegate.</param>
    /// <returns>A disposable handle that unregisters the command handler when disposed.</returns>
    System.IDisposable RegisterCommandHandler(string commandId, System.Action action);

    /// <summary>
    /// Registers an active asynchronous execution handler for a specific command ID.
    /// </summary>
    /// <param name="commandId">The semantic command ID.</param>
    /// <param name="handler">The asynchronous handler delegate.</param>
    /// <returns>A disposable handle that unregisters the command handler when disposed.</returns>
    System.IDisposable RegisterCommandHandler(string commandId, System.Func<IShortcutExecutionContext, System.Threading.Tasks.Task> handler);
}
