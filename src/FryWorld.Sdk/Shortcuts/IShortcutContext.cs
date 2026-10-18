using System.Collections.Generic;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Provides contextual information about the active window, focused control, scopes, and state variables
/// for evaluating shortcut activation and "When" conditions.
/// </summary>
public interface IShortcutContext
{
    /// <summary>
    /// Gets the primary active scope identifier (e.g. "plugin:circuit", "overlay:circuit.overlay", or "global").
    /// </summary>
    string ActiveScope { get; }

    /// <summary>
    /// Gets all active scopes in priority order, starting from the most specific (focused element)
    /// up to the least specific (global shell).
    /// </summary>
    IReadOnlyList<string> ActiveScopes { get; }

    /// <summary>
    /// Gets the currently focused visual control or element, if any.
    /// </summary>
    object? FocusedControl { get; }

    /// <summary>
    /// Checks whether a specific context key exists and is truthy.
    /// </summary>
    /// <param name="key">The context key name (e.g. "circuit.hasSelection").</param>
    /// <returns>True if the key is present and truthy.</returns>
    bool HasContextKey(string key);

    /// <summary>
    /// Gets the strongly-typed value associated with a context key.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The context key name.</param>
    /// <returns>The value if present and convertible; otherwise, default.</returns>
    T? GetContextKey<T>(string key);
}
