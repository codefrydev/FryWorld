using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Entry containing runtime metadata and tracking for a mounted plugin instance.
/// </summary>
public sealed class PluginRegistrationEntry
{
    public required IFryPlugin Plugin { get; init; }
    public PluginLifecycleState State { get; set; } = PluginLifecycleState.Registered;
    public PluginScope Scope { get; } = new();
    public Exception? FailureError { get; set; }
}

/// <summary>
/// The central microkernel engine orchestrating plugin lifecycles, DAG dependency resolution,
/// and dynamic capability dispatch.
/// </summary>
public sealed class PluginHost : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, PluginRegistrationEntry> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly FryPluginContext _rootContext;
    private readonly object _lifecycleLock = new();
    private bool _isDisposed;

    public IFryPluginContext Context => _rootContext;

    public event Action<IFryPlugin, PluginLifecycleState>? PluginStateChanged;

    public PluginHost(FryPluginContext? rootContext = null)
    {
        _rootContext = rootContext ?? new FryPluginContext();
    }

    /// <summary>
    /// Registers a plugin into the host registry.
    /// </summary>
    public void RegisterPlugin(IFryPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentException.ThrowIfNullOrWhiteSpace(plugin.Id);

        var entry = new PluginRegistrationEntry { Plugin = plugin, State = PluginLifecycleState.Registered };
        if (!_plugins.TryAdd(plugin.Id, entry))
        {
            if (_plugins.TryGetValue(plugin.Id, out var existing) && (existing.State == PluginLifecycleState.Unloaded || existing.State == PluginLifecycleState.Failed))
            {
                _plugins[plugin.Id] = entry;
            }
            else
            {
                throw new InvalidOperationException($"A plugin with identifier '{plugin.Id}' has already been registered.");
            }
        }

        PluginStateChanged?.Invoke(plugin, PluginLifecycleState.Registered);
    }

    /// <summary>
    /// Unregisters and removes a plugin from the host registry.
    /// </summary>
    public bool UnregisterPlugin(string pluginId)
    {
        if (_plugins.TryRemove(pluginId, out var entry))
        {
            if (entry.State == PluginLifecycleState.Active)
            {
                try { entry.Scope.Dispose(); } catch { }
            }
            entry.State = PluginLifecycleState.Unloaded;
            PluginStateChanged?.Invoke(entry.Plugin, PluginLifecycleState.Unloaded);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    public IReadOnlyList<IFryPlugin> GetRegisteredPlugins() => _plugins.Values.Select(e => e.Plugin).ToList();

    /// <summary>
    /// Gets the current lifecycle state of a registered plugin.
    /// </summary>
    public PluginLifecycleState? GetPluginState(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var entry) ? entry.State : null;
    }

    /// <summary>
    /// Sorts all currently registered plugins topologically based on their declarative service requirements,
    /// and mounts them asynchronously into the context.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        List<IFryPlugin> sortedPlugins;
        lock (_lifecycleLock)
        {
            sortedPlugins = TopologicalSort(_plugins.Values.Select(e => e.Plugin).ToList());
        }

        foreach (var plugin in sortedPlugins)
        {
            ct.ThrowIfCancellationRequested();
            await MountPluginAsync(plugin, ct);
        }
    }

    /// <summary>
    /// Mounts an individual plugin.
    /// </summary>
    public async Task MountPluginAsync(IFryPlugin plugin, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(plugin.Id, out var entry))
        {
            entry = new PluginRegistrationEntry { Plugin = plugin };
            _plugins[plugin.Id] = entry;
        }

        if (entry.State == PluginLifecycleState.Active)
        {
            return;
        }

        entry.State = PluginLifecycleState.Mounting;
        PluginStateChanged?.Invoke(plugin, entry.State);

        try
        {
            // Scoped context forwarding registrations and effects to the plugin's dedicated scope
            var scopedContext = _rootContext.CreateScopedContext(entry.Scope);
            await plugin.ApplyAsync(scopedContext, ct);

            entry.State = PluginLifecycleState.Active;
            PluginStateChanged?.Invoke(plugin, entry.State);
        }
        catch (Exception ex)
        {
            entry.State = PluginLifecycleState.Failed;
            entry.FailureError = ex;
            PluginStateChanged?.Invoke(plugin, entry.State);
            Debug.WriteLine($"[PluginHost] Failed to mount plugin '{plugin.Id}': {ex}");
        }
    }

    /// <summary>
    /// Unmounts an individual plugin, rolling back its reversible effects in LIFO order.
    /// </summary>
    public Task UnmountPluginAsync(string pluginId)
    {
        if (!_plugins.TryGetValue(pluginId, out var entry) || entry.State != PluginLifecycleState.Active)
        {
            return Task.CompletedTask;
        }

        entry.State = PluginLifecycleState.Unmounting;
        PluginStateChanged?.Invoke(entry.Plugin, entry.State);

        try
        {
            entry.Scope.Dispose();
            entry.State = PluginLifecycleState.Unloaded;
            PluginStateChanged?.Invoke(entry.Plugin, entry.State);
        }
        catch (Exception ex)
        {
            entry.State = PluginLifecycleState.Failed;
            entry.FailureError = ex;
            PluginStateChanged?.Invoke(entry.Plugin, entry.State);
        }

        _plugins.TryRemove(pluginId, out _);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Solves dependency ordering via Kahn's Directed Acyclic Graph (DAG) algorithm.
    /// </summary>
    public static List<IFryPlugin> TopologicalSort(IReadOnlyList<IFryPlugin> plugins)
    {
        var result = new List<IFryPlugin>();
        var inDegree = new Dictionary<IFryPlugin, int>();
        var graph = new Dictionary<IFryPlugin, List<IFryPlugin>>();

        // Map provided service types to providing plugins
        var serviceProviders = new Dictionary<Type, List<IFryPlugin>>();
        foreach (var p in plugins)
        {
            inDegree[p] = 0;
            graph[p] = new List<IFryPlugin>();

            foreach (var svc in p.ProvidedServices)
            {
                if (!serviceProviders.TryGetValue(svc, out var list))
                {
                    list = new List<IFryPlugin>();
                    serviceProviders[svc] = list;
                }
                list.Add(p);
            }
        }

        // Build edges: provider -> consumer
        foreach (var consumer in plugins)
        {
            foreach (var req in consumer.RequiredServices)
            {
                if (serviceProviders.TryGetValue(req, out var providers))
                {
                    foreach (var provider in providers.Where(p => !ReferenceEquals(p, consumer)))
                    {
                        graph[provider].Add(consumer);
                        inDegree[consumer]++;
                    }
                }
            }
        }

        // Queue all nodes with 0 dependencies
        var queue = new Queue<IFryPlugin>(plugins.Where(p => inDegree[p] == 0));
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            foreach (var dependent in graph[node])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // If cycle detected, append remaining nodes to allow graceful degradation
        if (result.Count < plugins.Count)
        {
            var unvisited = plugins.Except(result).ToList();
            Debug.WriteLine($"[PluginHost] Warning: Cyclic or unresolved dependency graph among {unvisited.Count} plugins.");
            result.AddRange(unvisited);
        }

        return result;
    }

    private static void BindScopedRegistries(FryPluginContext source, FryPluginContext target, PluginScope scope)
    {
        // When source registers items, forward to target and register cleanup on scope
        source.NavigationItemsChanged += () => { };
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        foreach (var entry in _plugins.Values.Reverse())
        {
            if (entry.State == PluginLifecycleState.Active)
            {
                try
                {
                    entry.Scope.Dispose();
                    entry.State = PluginLifecycleState.Unloaded;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginHost] Error during shutdown of plugin '{entry.Plugin.Id}': {ex.Message}");
                }
            }
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
