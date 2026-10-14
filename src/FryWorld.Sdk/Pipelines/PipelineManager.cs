using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Pipelines;

/// <summary>
/// Thread-safe implementation of <see cref="IPipelineManager"/> providing Cordis-style dispatch modes.
/// </summary>
public sealed class PipelineManager : IPipelineManager
{
    private readonly ConcurrentDictionary<string, List<object>> _waterfalls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<object>> _bails = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<object>> _parallels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<object>> _serials = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    // --- 1. Waterfall Pipeline ---

    public IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(middleware);

        lock (_syncLock)
        {
            var list = _waterfalls.GetOrAdd(pipelineName, _ => new List<object>());
            list.Add(middleware);
        }

        return new PipelineUnsubscriber(() =>
        {
            lock (_syncLock)
            {
                if (_waterfalls.TryGetValue(pipelineName, out var list))
                {
                    list.Remove(middleware);
                }
            }
        });
    }

    public async Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null)
    {
        List<Func<TContext, Func<Task>, Task>> middlewares;
        lock (_syncLock)
        {
            if (!_waterfalls.TryGetValue(pipelineName, out var rawList) || rawList.Count == 0)
            {
                middlewares = new List<Func<TContext, Func<Task>, Task>>();
            }
            else
            {
                middlewares = rawList.OfType<Func<TContext, Func<Task>, Task>>().ToList();
            }
        }

        if (middlewares.Count == 0)
        {
            if (terminal != null) await terminal();
            return;
        }

        int index = 0;
        async Task NextAsync()
        {
            if (index < middlewares.Count)
            {
                var current = middlewares[index++];
                await current(context, NextAsync);
            }
            else if (terminal != null)
            {
                await terminal();
            }
        }

        await NextAsync();
    }

    // --- 2. Bail Pipeline ---

    public IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncLock)
        {
            var list = _bails.GetOrAdd(pipelineName, _ => new List<object>());
            list.Add(handler);
        }

        return new PipelineUnsubscriber(() =>
        {
            lock (_syncLock)
            {
                if (_bails.TryGetValue(pipelineName, out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    public async Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class
    {
        List<Func<TContext, Task<TResult?>>> handlers;
        lock (_syncLock)
        {
            if (!_bails.TryGetValue(pipelineName, out var rawList) || rawList.Count == 0)
            {
                return null;
            }

            handlers = rawList.OfType<Func<TContext, Task<TResult?>>>().ToList();
        }

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

    public IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncLock)
        {
            var list = _parallels.GetOrAdd(pipelineName, _ => new List<object>());
            list.Add(handler);
        }

        return new PipelineUnsubscriber(() =>
        {
            lock (_syncLock)
            {
                if (_parallels.TryGetValue(pipelineName, out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    public async Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context)
    {
        List<Func<TContext, Task>> handlers;
        lock (_syncLock)
        {
            if (!_parallels.TryGetValue(pipelineName, out var rawList) || rawList.Count == 0)
            {
                return;
            }

            handlers = rawList.OfType<Func<TContext, Task>>().ToList();
        }

        await Task.WhenAll(handlers.Select(h => h(context)));
    }

    // --- 4. Serial Pipeline ---

    public IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_syncLock)
        {
            var list = _serials.GetOrAdd(pipelineName, _ => new List<object>());
            list.Add(handler);
        }

        return new PipelineUnsubscriber(() =>
        {
            lock (_syncLock)
            {
                if (_serials.TryGetValue(pipelineName, out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    public async Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context)
    {
        List<Func<TContext, Task>> handlers;
        lock (_syncLock)
        {
            if (!_serials.TryGetValue(pipelineName, out var rawList) || rawList.Count == 0)
            {
                return;
            }

            handlers = rawList.OfType<Func<TContext, Task>>().ToList();
        }

        foreach (var handler in handlers)
        {
            await handler(context);
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
