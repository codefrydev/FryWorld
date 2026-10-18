using System;
using System.Collections.Generic;
using System.Linq;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Immutable snapshot implementation of <see cref="IShortcutContext"/>.
/// </summary>
public sealed class ShortcutContext : IShortcutContext
{
    private readonly Dictionary<string, object?> _contextKeys;
    private readonly string[] _activeScopes;

    /// <inheritdoc />
    public string ActiveScope => _activeScopes.Length > 0 ? _activeScopes[0] : ShortcutScope.Global;

    /// <inheritdoc />
    public IReadOnlyList<string> ActiveScopes => _activeScopes;

    /// <inheritdoc />
    public object? FocusedControl { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="ShortcutContext"/>.
    /// </summary>
    /// <param name="scopes">Active scopes in order of specificity.</param>
    /// <param name="contextKeys">Optional key/value state pairs.</param>
    /// <param name="focusedControl">Optional focused visual element.</param>
    public ShortcutContext(
        IEnumerable<string>? scopes = null,
        IDictionary<string, object?>? contextKeys = null,
        object? focusedControl = null)
    {
        var rawScopes = scopes?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (rawScopes.Length == 0)
        {
            _activeScopes = [ShortcutScope.Global];
        }
        else if (!rawScopes.Contains(ShortcutScope.Global, StringComparer.OrdinalIgnoreCase))
        {
            _activeScopes = [.. rawScopes, ShortcutScope.Global];
        }
        else
        {
            _activeScopes = rawScopes;
        }

        _contextKeys = contextKeys != null
            ? new Dictionary<string, object?>(contextKeys, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        FocusedControl = focusedControl;
    }

    /// <inheritdoc />
    public bool HasContextKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_contextKeys.TryGetValue(key, out var val) && val != null)
        {
            if (val is bool b) return b;
            if (val is string s) return !string.IsNullOrWhiteSpace(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
            if (val is int i) return i != 0;
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public T? GetContextKey<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_contextKeys.TryGetValue(key, out var val) && val is T typed)
        {
            return typed;
        }
        return default;
    }
}
