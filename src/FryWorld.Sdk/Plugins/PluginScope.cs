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
    // A List unwound in reverse rather than a Stack, so a handle returned by RegisterEffect can remove
    // its own effect without disturbing the LIFO order of the others.
    private readonly List<Action> _disposers = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// Receives exceptions thrown by effects while the scope unwinds. Disposal never throws, so that one
    /// failing effect cannot stop the others from running; the host uses this to report the failure.
    /// </summary>
    internal Action<Exception>? EffectErrorSink { get; set; }

    /// <summary>The plugin this scope belongs to, set by <see cref="PluginHost"/> when it mounts the plugin.</summary>
    internal IFryPlugin? Plugin { get; set; }

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
    /// Returns an <see cref="IDisposable"/> that runs the effect early and removes it from the scope,
    /// so it does not run a second time when the scope disposes.
    /// </summary>
    public IDisposable RegisterEffect(Action onDispose)
    {
        ArgumentNullException.ThrowIfNull(onDispose);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _disposers.Add(onDispose);
        }

        return new ScopeEffectHandle(this, onDispose);
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
    /// Every effect runs even if an earlier one throws.
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

        for (var i = actionsToRun.Count - 1; i >= 0; i--)
        {
            try
            {
                actionsToRun[i]();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginScope] Exception during reversible effect rollback: {ex.Message}");
                EffectErrorSink?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// Removes <paramref name="onDispose"/> from this scope without running it.
    /// </summary>
    /// <returns>True when the effect was still registered.</returns>
    private bool TryRemoveEffect(Action onDispose)
    {
        lock (_lock)
        {
            if (_isDisposed) return false;

            // Remove the most recent matching registration, mirroring LIFO unwinding.
            var index = _disposers.LastIndexOf(onDispose);
            if (index < 0) return false;
            _disposers.RemoveAt(index);
            return true;
        }
    }

    /// <summary>
    /// Handle returned by <see cref="RegisterEffect"/>. Disposing it runs that one effect now and
    /// removes it from the scope.
    /// </summary>
    /// <remarks>
    /// This used to run the effect but leave it registered, so the scope ran it a second time on
    /// unmount, and every early-disposed registration stayed in the scope for the plugin's lifetime.
    /// </remarks>
    private sealed class ScopeEffectHandle : IDisposable
    {
        private readonly PluginScope _scope;
        private Action? _action;

        public ScopeEffectHandle(PluginScope scope, Action action)
        {
            _scope = scope;
            _action = action;
        }

        public void Dispose()
        {
            var action = Interlocked.Exchange(ref _action, null);
            if (action == null) return;

            // If the scope already unwound it, running it again could double-unregister.
            if (_scope.TryRemoveEffect(action))
            {
                action();
            }
        }
    }
}
