using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Runtime input routing and state management service for keyboard shortcuts.
/// Handles key event routing, focus/input shielding, multi-key chords, and context evaluation.
/// </summary>
public sealed class ShortcutManager : IShortcutManager
{
    private readonly IShortcutRegistry _registry;
    private readonly Lock _scopeLock = new();
    private readonly Stack<string> _scopeStack = new();
    private readonly ConcurrentDictionary<string, object?> _contextKeys = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, List<object>> _commandHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _handlerLock = new();

    private string _activeScope = ShortcutScope.Global;

    // Chord state machine
    private KeyStroke? _pendingChordStroke;
    private long _chordTimestamp;
    private const long ChordTimeoutMs = 1500;

    /// <inheritdoc />
    public IShortcutRegistry Registry => _registry;

    /// <inheritdoc />
    public IShortcutContext CurrentContext => BuildCurrentContext();

    /// <summary>
    /// Initializes a new instance of <see cref="ShortcutManager"/>.
    /// </summary>
    /// <param name="registry">The shortcut registry.</param>
    public ShortcutManager(IShortcutRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public void SetActiveScope(string scope)
    {
        _activeScope = string.IsNullOrWhiteSpace(scope) ? ShortcutScope.Global : scope.Trim();
    }

    /// <inheritdoc />
    public void PushScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        lock (_scopeLock)
        {
            _scopeStack.Push(scope.Trim());
        }
    }

    /// <inheritdoc />
    public void PopScope(string scope)
    {
        lock (_scopeLock)
        {
            if (_scopeStack.Count > 0 && _scopeStack.Peek().Equals(scope, StringComparison.OrdinalIgnoreCase))
            {
                _scopeStack.Pop();
            }
        }
    }

    /// <inheritdoc />
    public void SetContextKey(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _contextKeys[key] = value;
    }

    /// <inheritdoc />
    public void RemoveContextKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _contextKeys.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public IDisposable RegisterCommandHandler(string commandId, System.Windows.Input.ICommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(command);
        return AddCommandHandler(commandId, command);
    }

    /// <inheritdoc />
    public IDisposable RegisterCommandHandler(string commandId, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(action);
        return AddCommandHandler(commandId, action);
    }

    /// <inheritdoc />
    public IDisposable RegisterCommandHandler(string commandId, Func<IShortcutExecutionContext, System.Threading.Tasks.Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(handler);
        return AddCommandHandler(commandId, handler);
    }

    private IDisposable AddCommandHandler(string commandId, object handler)
    {
        var normalizedId = commandId.Trim();
        lock (_handlerLock)
        {
            if (!_commandHandlers.TryGetValue(normalizedId, out var list))
            {
                list = new List<object>();
                _commandHandlers[normalizedId] = list;
            }
            list.Add(handler);
        }

        return new ActionDisposable(() =>
        {
            lock (_handlerLock)
            {
                if (_commandHandlers.TryGetValue(normalizedId, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _commandHandlers.TryRemove(normalizedId, out _);
                    }
                }
            }
        });
    }

    /// <inheritdoc />
    public bool ProcessKeyDown(KeyEventArgs e, IShortcutContext? overrideContext = null)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled)
            return false;

        var context = overrideContext ?? BuildCurrentContext(e.Source);

        // 1. INPUT SHIELDING: If focused element is a text input, don't hijack plain characters
        if (IsInputShielded(context.FocusedControl, e))
        {
            return false;
        }

        var activeBindings = _registry.GetActiveBindings(context);
        long now = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

        // 2. CHORD RESOLUTION: If a first stroke is pending, check for second stroke match
        if (_pendingChordStroke.HasValue)
        {
            var firstStroke = _pendingChordStroke.Value;
            _pendingChordStroke = null;

            if (now - _chordTimestamp <= ChordTimeoutMs)
            {
                foreach (var b in activeBindings)
                {
                    if (b.ParsedGesture.IsChord &&
                        b.ParsedGesture.Strokes.Count == 2 &&
                        b.ParsedGesture.Strokes[0].Equals(firstStroke) &&
                        b.ParsedGesture.Strokes[1].Matches(e))
                    {
                        if (TryExecute(b, context, e))
                        {
                            e.Handled = true;
                            return true;
                        }
                    }
                }
            }
        }

        // 3. FIRST STROKE OF CHORD CHECK
        // If this key matches the first stroke of any active chord, enter chord pending state
        bool isChordPrefix = activeBindings.Any(b => b.ParsedGesture.IsChord && b.ParsedGesture.Strokes[0].Matches(e));
        if (isChordPrefix)
        {
            _pendingChordStroke = new KeyStroke(e.Key, e.KeyModifiers);
            _chordTimestamp = now;
            e.Handled = true;
            return true;
        }

        // 4. STANDARD SINGLE-STROKE SHORTCUT EVALUATION
        foreach (var binding in activeBindings)
        {
            if (!binding.ParsedGesture.IsChord && binding.ParsedGesture.Matches(e))
            {
                if (TryExecute(binding, context, e))
                {
                    e.Handled = true;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryExecute(ShortcutBinding binding, IShortcutContext context, KeyEventArgs e)
    {
        if (binding.Command != null)
        {
            if (binding.Command.CanExecute(null))
            {
                binding.Command.Execute(null);
                return true;
            }
            return false;
        }

        if (binding.AsyncAction != null)
        {
            var execContext = new ShortcutExecutionContext(context, e);
            _ = binding.AsyncAction(execContext);
            return true;
        }

        // Check for registered command handler by CommandId or Id
        var targetId = !string.IsNullOrWhiteSpace(binding.CommandId) ? binding.CommandId : binding.Id;
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            object? handler = null;
            lock (_handlerLock)
            {
                if (_commandHandlers.TryGetValue(targetId, out var list) && list.Count > 0)
                {
                    handler = list[^1];
                }
                else if (binding.CommandId != null && _commandHandlers.TryGetValue(binding.Id, out var fallbackList) && fallbackList.Count > 0)
                {
                    handler = fallbackList[^1];
                }
            }

            if (handler is System.Windows.Input.ICommand cmd)
            {
                if (cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                    return true;
                }
                return false;
            }

            if (handler is Action act)
            {
                act();
                return true;
            }

            if (handler is Func<IShortcutExecutionContext, System.Threading.Tasks.Task> asyncFn)
            {
                var execContext = new ShortcutExecutionContext(context, e);
                _ = asyncFn(execContext);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether keyboard focus is currently inside an editable text input and the pressed key
    /// represents plain text that should be typed rather than intercepted as a shortcut.
    /// </summary>
    public static bool IsInputShielded(object? focusedControl, KeyEventArgs e)
    {
        if (focusedControl == null)
            return false;

        // If a modifier key is pressed (Ctrl, Alt, Cmd / Meta), allow shortcuts like Ctrl+S, Cmd+K, Alt+W through
        bool hasModifier = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        if (hasModifier)
            return false;

        // Function keys (F1-F12), Escape, Tab are usually allowed through
        if (e.Key >= Key.F1 && e.Key <= Key.F12)
            return false;
        if (e.Key is Key.Escape or Key.Tab)
            return false;

        // Check if focused element is a text input
        if (focusedControl is TextBox or AutoCompleteBox)
            return true;

        var type = focusedControl.GetType();
        string typeName = type.Name;

        return typeName.Contains("TextBox", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("TextEditor", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("TextPresenter", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("NumericUpDown", StringComparison.OrdinalIgnoreCase);
    }

    private IShortcutContext BuildCurrentContext(object? sourceElement = null)
    {
        var scopes = new List<string>(4);

        lock (_scopeLock)
        {
            foreach (var s in _scopeStack)
            {
                scopes.Add(s);
            }
        }

        if (!string.IsNullOrWhiteSpace(_activeScope))
        {
            scopes.Add(_activeScope);
        }

        scopes.Add(ShortcutScope.Global);

        object? focusedControl = sourceElement;
        if (focusedControl is Visual visual)
        {
            focusedControl = TopLevel.GetTopLevel(visual)?.FocusManager?.GetFocusedElement() ?? focusedControl;
        }

        return new ShortcutContext(scopes, _contextKeys, focusedControl);
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;

        public ActionDisposable(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _action, null)?.Invoke();
        }
    }
}
