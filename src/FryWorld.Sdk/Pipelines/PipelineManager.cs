using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FryWorld.Sdk.Pipelines;

/// <summary>
/// Thread-safe implementation of <see cref="IPipelineManager"/> providing Cordis-style dispatch modes.
/// </summary>
/// <remarks>
/// Handlers are kept per pipeline name <em>and</em> handler type, as immutable arrays replaced on every
/// registration change. Running a pipeline is a single dictionary lookup: no lock, no filtering, no copy.
/// A handler only runs when the pipeline is executed with the context type it was registered for; mixing
/// context types under one name is reported to the logger. <see cref="PipelineKey{TContext}"/> makes the
/// context type part of the pipeline's identity so that mistake becomes a compile error.
/// </remarks>
public sealed class PipelineManager : IPipelineManager
{
    private readonly ConcurrentDictionary<HandlerKey, Delegate[]> _handlers = new();
    private readonly object _syncLock = new();
    private readonly ILogger _logger;

    /// <summary>Creates a pipeline manager that does not log.</summary>
    public PipelineManager() : this(NullLogger.Instance)
    {
    }

    /// <summary>Creates a pipeline manager that reports misuse, such as mismatched context types, to <paramref name="logger"/>.</summary>
    public PipelineManager(ILogger logger)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    private enum Mode
    {
        Waterfall,
        Bail,
        Parallel,
        Serial
    }

    private readonly record struct HandlerKey(Mode Mode, string Name, Type HandlerType)
    {
        public bool Equals(HandlerKey other) =>
            Mode == other.Mode && HandlerType == other.HandlerType &&
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(Mode, HandlerType, StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
    }

    private IDisposable Register<THandler>(Mode mode, string pipelineName, THandler handler) where THandler : Delegate
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(handler);

        var key = new HandlerKey(mode, pipelineName, typeof(THandler));
        lock (_syncLock)
        {
            WarnOnContextTypeMismatch(key);
            var current = _handlers.TryGetValue(key, out var existing) ? (THandler[])existing : Array.Empty<THandler>();

            // Stored as THandler[] (not Delegate[]) so execution can cast the array back without copying.
            THandler[] updated = [.. current, handler];
            _handlers[key] = updated;
        }

        return new PipelineUnsubscriber(() =>
        {
            lock (_syncLock)
            {
                if (!_handlers.TryGetValue(key, out var existing)) return;

                var handlers = (THandler[])existing;
                var index = Array.LastIndexOf(handlers, handler);
                if (index < 0) return;

                if (handlers.Length == 1)
                {
                    _handlers.TryRemove(key, out _);
                }
                else
                {
                    THandler[] remaining = [.. handlers[..index], .. handlers[(index + 1)..]];
                    _handlers[key] = remaining;
                }
            }
        });
    }

    /// <summary>"Mode pipeline-name" for every pipeline that has handlers, for leak checks in tests.</summary>
    internal IReadOnlyList<string> RegisteredPipelineNames =>
        _handlers.Keys.Select(k => $"{k.Mode} {k.Name}").Distinct().ToList();

    private THandler[]? GetHandlers<THandler>(Mode mode, string pipelineName) where THandler : Delegate =>
        _handlers.TryGetValue(new HandlerKey(mode, pipelineName, typeof(THandler)), out var handlers)
            ? (THandler[])handlers
            : null;

    /// <summary>
    /// A handler registered for a different context type than the one the pipeline is executed with never
    /// runs. Warn at registration time instead of letting it be skipped silently.
    /// </summary>
    private void WarnOnContextTypeMismatch(HandlerKey added)
    {
        foreach (var key in _handlers.Keys)
        {
            if (key.Mode == added.Mode && key.HandlerType != added.HandlerType &&
                string.Equals(key.Name, added.Name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "{Mode} pipeline '{Pipeline}' has handlers of different types ({Existing} and {Added}). " +
                    "Executing it runs only the handlers whose context type matches, so some will never run.",
                    added.Mode, added.Name, key.HandlerType, added.HandlerType);
                return;
            }
        }
    }

    // --- 1. Waterfall Pipeline ---

    /// <inheritdoc />
    public IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware) =>
        Register(Mode.Waterfall, pipelineName, middleware);

    /// <inheritdoc />
    public Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null)
    {
        var middlewares = GetHandlers<Func<TContext, Func<Task>, Task>>(Mode.Waterfall, pipelineName);
        if (middlewares == null)
        {
            return terminal?.Invoke() ?? Task.CompletedTask;
        }

        return InvokeAsync(0);

        // Each middleware gets its own next(). Calling it more than once is a no-op, so a middleware cannot
        // re-run the rest of the chain (or the terminal) by mistake.
        Task InvokeAsync(int index)
        {
            try
            {
                if (index >= middlewares.Length)
                {
                    return terminal?.Invoke() ?? Task.CompletedTask;
                }

                var called = 0;
                return middlewares[index](context, () =>
                {
                    if (Interlocked.Exchange(ref called, 1) != 0)
                    {
                        _logger.LogWarning("A middleware in waterfall pipeline '{Pipeline}' called next() more than once; the extra call was ignored.", pipelineName);
                        return Task.CompletedTask;
                    }
                    return InvokeAsync(index + 1);
                });
            }
            catch (Exception ex)
            {
                // A handler that throws before returning a task surfaces as a faulted task, as with await.
                return Task.FromException(ex);
            }
        }
    }

    // --- 2. Bail Pipeline ---

    /// <inheritdoc />
    public IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class =>
        Register(Mode.Bail, pipelineName, handler);

    /// <inheritdoc />
    public async Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class
    {
        var handlers = GetHandlers<Func<TContext, Task<TResult?>>>(Mode.Bail, pipelineName);
        if (handlers == null) return null;

        foreach (var handler in handlers)
        {
            var result = await handler(context);
            if (result != null)
            {
                return result; // Short-circuits on first handled result
            }
        }

        return null;
    }

    // --- 3. Parallel Pipeline ---

    /// <inheritdoc />
    public IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        Register(Mode.Parallel, pipelineName, handler);

    /// <inheritdoc />
    public Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context)
    {
        var handlers = GetHandlers<Func<TContext, Task>>(Mode.Parallel, pipelineName);
        if (handlers == null) return Task.CompletedTask;

        var tasks = new Task[handlers.Length];
        var allCompleted = true;
        try
        {
            for (var i = 0; i < handlers.Length; i++)
            {
                tasks[i] = handlers[i](context);
                allCompleted &= tasks[i].IsCompletedSuccessfully;
            }
        }
        catch (Exception ex)
        {
            // A handler that throws before returning a task surfaces as a faulted task, as with await.
            return Task.FromException(ex);
        }

        // Most handlers finish synchronously; skip Task.WhenAll's allocation when they all did.
        return allCompleted ? Task.CompletedTask : Task.WhenAll(tasks);
    }

    // --- 4. Serial Pipeline ---

    /// <inheritdoc />
    public IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        Register(Mode.Serial, pipelineName, handler);

    /// <inheritdoc />
    public Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context)
    {
        var handlers = GetHandlers<Func<TContext, Task>>(Mode.Serial, pipelineName);
        if (handlers == null) return Task.CompletedTask;

        // Run synchronously-completing handlers without an async state machine; switch to awaiting at the
        // first one that is still running.
        try
        {
            for (var i = 0; i < handlers.Length; i++)
            {
                var task = handlers[i](context);
                if (!task.IsCompletedSuccessfully)
                {
                    return ContinueAsync(task, i + 1);
                }
            }
        }
        catch (Exception ex)
        {
            // A handler that throws before returning a task surfaces as a faulted task, as with await.
            return Task.FromException(ex);
        }

        return Task.CompletedTask;

        async Task ContinueAsync(Task pending, int next)
        {
            await pending;
            for (var i = next; i < handlers.Length; i++)
            {
                await handlers[i](context);
            }
        }
    }

    private sealed class PipelineUnsubscriber : IDisposable
    {
        private Action? _unsubscribe;

        public PipelineUnsubscriber(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
