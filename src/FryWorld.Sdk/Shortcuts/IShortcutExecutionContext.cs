using System.Threading;
using Avalonia.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Execution parameters and context provided when a shortcut or command is triggered.
/// </summary>
public interface IShortcutExecutionContext : IShortcutContext
{
    /// <summary>
    /// The original Avalonia <see cref="KeyEventArgs"/> that triggered the shortcut.
    /// </summary>
    KeyEventArgs? OriginalEvent { get; }

    /// <summary>
    /// Optional command parameter passed to the command or action.
    /// </summary>
    object? Parameter { get; }

    /// <summary>
    /// Cancellation token for asynchronous operations.
    /// </summary>
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// Default implementation of <see cref="IShortcutExecutionContext"/>.
/// </summary>
public sealed class ShortcutExecutionContext : IShortcutExecutionContext
{
    private readonly IShortcutContext _innerContext;

    /// <inheritdoc />
    public string ActiveScope => _innerContext.ActiveScope;

    /// <inheritdoc />
    public System.Collections.Generic.IReadOnlyList<string> ActiveScopes => _innerContext.ActiveScopes;

    /// <inheritdoc />
    public object? FocusedControl => _innerContext.FocusedControl;

    /// <inheritdoc />
    public KeyEventArgs? OriginalEvent { get; init; }

    /// <inheritdoc />
    public object? Parameter { get; init; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; init; } = default;

    /// <summary>
    /// Initializes a new instance of <see cref="ShortcutExecutionContext"/>.
    /// </summary>
    /// <param name="innerContext">The underlying shortcut context.</param>
    /// <param name="originalEvent">Optional source key event.</param>
    /// <param name="parameter">Optional parameter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public ShortcutExecutionContext(
        IShortcutContext innerContext,
        KeyEventArgs? originalEvent = null,
        object? parameter = null,
        CancellationToken cancellationToken = default)
    {
        _innerContext = innerContext ?? throw new System.ArgumentNullException(nameof(innerContext));
        OriginalEvent = originalEvent;
        Parameter = parameter;
        CancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public bool HasContextKey(string key) => _innerContext.HasContextKey(key);

    /// <inheritdoc />
    public T? GetContextKey<T>(string key) => _innerContext.GetContextKey<T>(key);
}
