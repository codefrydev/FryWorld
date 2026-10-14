using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Manages reversible effects and resource subscriptions for a plugin.
/// When the plugin is unloaded or unmounted, the scope executes all cleanup actions in reverse order (LIFO).
/// </summary>
public sealed class PluginScope : IDisposable
{
    private readonly Stack<Action> _disposers = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// Gets whether this scope has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock) return _isDisposed;
        }
    }

    /// <summary>
    /// Gets the count of registered active cleanup actions.
    /// </summary>
    public int ActiveEffectCount
    {
        get
        {
            lock (_lock) return _disposers.Count;
        }
    }

    /// <summary>
    /// Registers a reversible cleanup effect action that will be executed when this scope disposes.
    /// Returns an <see cref="IDisposable"/> that can be used to cancel or execute the effect early.
    /// </summary>
    public IDisposable RegisterEffect(Action onDispose)
    {
        ArgumentNullException.ThrowIfNull(onDispose);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _disposers.Push(onDispose);
        }

        return new ScopeEffectHandle(onDispose);
    }

    /// <summary>
    /// Registers an <see cref="IDisposable"/> resource to be disposed when this scope disposes.
    /// </summary>
    public IDisposable RegisterDisposable(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        return RegisterEffect(disposable.Dispose);
    }

    /// <summary>
    /// Disposes all registered effects in LIFO (Last-In-First-Out) order.
    /// </summary>
    public void Dispose()
    {
        List<Action> actionsToRun;
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            actionsToRun = new List<Action>(_disposers);
            _disposers.Clear();
        }

        foreach (var action in actionsToRun)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginScope] Exception during reversible effect rollback: {ex.Message}");
            }
        }
    }

    private sealed class ScopeEffectHandle : IDisposable
    {
        private Action? _action;

        public ScopeEffectHandle(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _action, null)?.Invoke();
        }
    }
}
