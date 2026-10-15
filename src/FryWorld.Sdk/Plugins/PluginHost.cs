using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Pipelines;
using Microsoft.Extensions.Logging;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Entry containing runtime metadata and tracking for a mounted plugin instance.
/// </summary>
public sealed class PluginRegistrationEntry
{
    /// <summary>The plugin instance.</summary>
    public required IFryPlugin Plugin { get; init; }

    /// <summary>Where the plugin is in its lifecycle.</summary>
    public PluginLifecycleState State { get; set; } = PluginLifecycleState.Registered;

    /// <summary>
    /// The scope holding the plugin's registrations for its current mount. A new scope is created for
    /// every mount, so a plugin can be unmounted and mounted again.
    /// </summary>
    public PluginScope Scope { get; internal set; } = new();

    /// <summary>Why the plugin last failed to mount or unmount, if it did.</summary>
    public Exception? FailureError { get; set; }

    /// <summary>How long the plugin's last <see cref="IFryPlugin.ApplyAsync"/> took, or null if it has not mounted.</summary>
    public TimeSpan? MountDuration { get; internal set; }
}

/// <summary>
/// The central microkernel engine orchestrating plugin lifecycles, DAG dependency resolution,
/// and dynamic capability dispatch.
/// </summary>
public sealed class PluginHost : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, PluginRegistrationEntry> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly FryPluginContext _rootContext;
    private readonly ILogger _logger;
    private readonly PluginHostOptions _options;
    private readonly object _lifecycleLock = new();

    // Plugin ids in the order they became active, so teardown runs in reverse.
    private readonly List<string> _mountOrder = new();
    private readonly List<PluginLoadDiagnostic> _diagnostics = new();
    private bool _isDisposed;

    /// <summary>The root context every plugin's registrations land in.</summary>
    public IFryPluginContext Context => _rootContext;

    /// <summary>Raised whenever a plugin changes lifecycle state.</summary>
    public event Action<IFryPlugin, PluginLifecycleState>? PluginStateChanged;

    /// <summary>Raised for every problem found while ordering, mounting or unmounting plugins.</summary>
    public event Action<PluginLoadDiagnostic>? DiagnosticReported;

    /// <summary>Creates a host. When <paramref name="rootContext"/> is null, the host creates its own.</summary>
    public PluginHost(FryPluginContext? rootContext = null)
        : this(rootContext, new PluginHostOptions())
    {
    }

    /// <summary>Creates a host with the given options. When <paramref name="rootContext"/> is null, the host creates its own.</summary>
    public PluginHost(FryPluginContext? rootContext, PluginHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MountConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MountConcurrency, "MountConcurrency must be at least 1.");
        }

        _options = options;
        _logger = options.Logger;
        _rootContext = rootContext ?? new FryPluginContext(pipelines: new PipelineManager(_logger));

        if (options.EventSynchronizationContext != null)
        {
            _rootContext.NotificationContext = options.EventSynchronizationContext;
        }
    }

    /// <summary>
    /// Everything that went wrong, or nearly did, while this host ordered, mounted and unmounted plugins.
    /// </summary>
    public IReadOnlyList<PluginLoadDiagnostic> Diagnostics
    {
        get
        {
            lock (_diagnostics) return _diagnostics.ToList();
        }
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
                entry.Scope.Dispose();
                lock (_lifecycleLock) _mountOrder.Remove(entry.Plugin.Id);
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
    /// Gets the host's record of a registered plugin, including why it failed if it did.
    /// </summary>
    public PluginRegistrationEntry? GetRegistration(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Sorts all currently registered plugins topologically based on their declarative service requirements,
    /// and mounts them asynchronously into the context.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        List<List<IFryPlugin>> layers;
        IReadOnlyList<IFryPlugin> unresolved;
        lock (_lifecycleLock)
        {
            layers = TopologicalLayers(_plugins.Values.Select(e => e.Plugin).ToList(), out unresolved);
        }

        if (unresolved.Count > 0)
        {
            var ids = string.Join(", ", unresolved.Select(p => p.Id));
            Report(new PluginLoadDiagnostic(
                PluginLoadStage.DependencyResolution,
                PluginDiagnosticSeverity.Warning,
                $"The service requirements of these plugins form a cycle, so they mount in an arbitrary order and may fail: {ids}.",
                Exception: new PluginCircularDependencyException($"Cyclic service requirements among: {ids}.")));
        }

        // Every plugin in a layer depends only on plugins in earlier layers, so a layer's plugins can mount
        // together once the layer before it has finished.
        foreach (var layer in layers)
        {
            ct.ThrowIfCancellationRequested();

            if (_options.MountConcurrency == 1 || layer.Count == 1)
            {
                foreach (var plugin in layer)
                {
                    ct.ThrowIfCancellationRequested();
                    await MountPluginAsync(plugin, ct);
                }
            }
            else
            {
                await Parallel.ForEachAsync(
                    layer,
                    new ParallelOptions { MaxDegreeOfParallelism = _options.MountConcurrency, CancellationToken = ct },
                    async (plugin, token) => await MountPluginAsync(plugin, token));
            }
        }
    }

    /// <summary>
    /// Mounts an individual plugin. If the plugin fails, every registration it made before failing is
    /// rolled back and the plugin is left in <see cref="PluginLifecycleState.Failed"/>.
    /// </summary>
    public async Task MountPluginAsync(IFryPlugin plugin, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        PluginRegistrationEntry entry;
        lock (_lifecycleLock)
        {
            entry = _plugins.GetOrAdd(plugin.Id, _ => new PluginRegistrationEntry { Plugin = plugin });

            if (entry.State is PluginLifecycleState.Active or PluginLifecycleState.Mounting)
            {
                return;
            }

            // A scope is single-use: one that an earlier mount or failure disposed cannot hold new effects.
            if (entry.Scope.IsDisposed)
            {
                entry.Scope = new PluginScope();
            }

            entry.Scope.Plugin = plugin;
            var pluginId = plugin.Id;
            entry.Scope.EffectErrorSink = ex => Report(new PluginLoadDiagnostic(
                PluginLoadStage.Unmount, PluginDiagnosticSeverity.Error,
                "A cleanup effect threw while the plugin's registrations were being rolled back.",
                PluginId: pluginId, Exception: ex));

            entry.State = PluginLifecycleState.Mounting;
            entry.FailureError = null;
        }

        PluginStateChanged?.Invoke(plugin, PluginLifecycleState.Mounting);

        try
        {
            foreach (var required in plugin.RequiredServices)
            {
                if (!_rootContext.HasService(required))
                {
                    throw new PluginMissingDependencyException(plugin.Id, required);
                }
            }

            // Scoped context forwarding registrations and effects to the plugin's dedicated scope
            var scopedContext = _rootContext.CreateScopedContext(entry.Scope);
            var stopwatch = Stopwatch.StartNew();

            // One change event per registry once the plugin is in, not one per registration.
            using (_rootContext.DeferChangeNotifications())
            {
                await plugin.ApplyAsync(scopedContext, ct);
            }

            entry.MountDuration = stopwatch.Elapsed;
            if (stopwatch.Elapsed > _options.SlowMountThreshold)
            {
                Report(new PluginLoadDiagnostic(
                    PluginLoadStage.Mount, PluginDiagnosticSeverity.Warning,
                    $"Took {stopwatch.Elapsed.TotalMilliseconds:F0} ms to mount, which delays every plugin after it. " +
                    "Move slow work out of ApplyAsync, for example into the view factory or a background task.",
                    PluginId: plugin.Id));
            }

            lock (_lifecycleLock)
            {
                entry.State = PluginLifecycleState.Active;
                _mountOrder.Remove(plugin.Id);
                _mountOrder.Add(plugin.Id);
            }
            PluginStateChanged?.Invoke(plugin, PluginLifecycleState.Active);
        }
        catch (Exception ex)
        {
            // Roll back whatever the plugin registered before it failed, so it leaves nothing behind.
            using (_rootContext.DeferChangeNotifications())
            {
                entry.Scope.Dispose();
            }

            entry.State = PluginLifecycleState.Failed;
            entry.FailureError = ex;
            Report(new PluginLoadDiagnostic(
                PluginLoadStage.Mount, PluginDiagnosticSeverity.Error,
                ex is PluginMissingDependencyException ? ex.Message : $"The plugin failed to mount: {ex.Message}",
                PluginId: plugin.Id, Exception: ex));
            PluginStateChanged?.Invoke(plugin, PluginLifecycleState.Failed);
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
            using (_rootContext.DeferChangeNotifications())
            {
                entry.Scope.Dispose();
            }
            entry.State = PluginLifecycleState.Unloaded;
            PluginStateChanged?.Invoke(entry.Plugin, entry.State);
        }
        catch (Exception ex)
        {
            entry.State = PluginLifecycleState.Failed;
            entry.FailureError = ex;
            Report(new PluginLoadDiagnostic(
                PluginLoadStage.Unmount, PluginDiagnosticSeverity.Error,
                $"The plugin failed to unmount: {ex.Message}", PluginId: pluginId, Exception: ex));
            PluginStateChanged?.Invoke(entry.Plugin, entry.State);
        }

        lock (_lifecycleLock) _mountOrder.Remove(entry.Plugin.Id);
        _plugins.TryRemove(pluginId, out _);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Solves dependency ordering via Kahn's Directed Acyclic Graph (DAG) algorithm.
    /// Plugins caught in a dependency cycle are appended at the end in their original order.
    /// </summary>
    public static List<IFryPlugin> TopologicalSort(IReadOnlyList<IFryPlugin> plugins) =>
        TopologicalLayers(plugins, out _).SelectMany(layer => layer).ToList();

    /// <summary>
    /// Groups plugins into layers: each plugin's providers are all in earlier layers. Plugins caught in a
    /// cycle form a final layer, in their original order, and are returned in <paramref name="unresolved"/>.
    /// </summary>
    private static List<List<IFryPlugin>> TopologicalLayers(IReadOnlyList<IFryPlugin> plugins, out IReadOnlyList<IFryPlugin> unresolved)
    {
        var layers = new List<List<IFryPlugin>>();
        var placed = 0;
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

        // Kahn's algorithm, one layer at a time: a layer is every plugin whose providers have all been placed.
        var current = plugins.Where(p => inDegree[p] == 0).ToList();
        while (current.Count > 0)
        {
            layers.Add(current);
            placed += current.Count;

            var next = new List<IFryPlugin>();
            foreach (var node in current)
            {
                foreach (var dependent in graph[node])
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                    {
                        next.Add(dependent);
                    }
                }
            }

            // Keep registration order within a layer.
            current = plugins.Where(next.Contains).ToList();
        }

        // If cycle detected, append remaining nodes to allow graceful degradation
        if (placed < plugins.Count)
        {
            var placedSet = layers.SelectMany(l => l).ToHashSet();
            var remaining = plugins.Where(p => !placedSet.Contains(p)).ToList();
            Debug.WriteLine($"[PluginHost] Warning: Cyclic or unresolved dependency graph among {remaining.Count} plugins.");
            layers.Add(remaining);
            unresolved = remaining;
        }
        else
        {
            unresolved = Array.Empty<IFryPlugin>();
        }

        return layers;
    }

    /// <summary>
    /// Adds a diagnostic found outside the host, such as a package that failed to load from disk, so that
    /// <see cref="Diagnostics"/> is the one place a UI needs to look. Raises <see cref="DiagnosticReported"/>.
    /// </summary>
    public void ReportDiagnostic(PluginLoadDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        Report(diagnostic);
    }

    private void Report(PluginLoadDiagnostic diagnostic)
    {
        lock (_diagnostics) _diagnostics.Add(diagnostic);

        var level = diagnostic.Severity switch
        {
            PluginDiagnosticSeverity.Error => LogLevel.Error,
            PluginDiagnosticSeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };
        _logger.Log(level, diagnostic.Exception, "{Diagnostic}", diagnostic.ToString());
        Debug.WriteLine($"[PluginHost] {diagnostic}");

        DiagnosticReported?.Invoke(diagnostic);
    }

    /// <summary>Unmounts every active plugin, most recently mounted first.</summary>
    public ValueTask DisposeAsync()
    {
        string[] teardownOrder;
        lock (_lifecycleLock)
        {
            if (_isDisposed) return ValueTask.CompletedTask;
            _isDisposed = true;
            teardownOrder = _mountOrder.AsEnumerable().Reverse().ToArray();
            _mountOrder.Clear();
        }

        foreach (var pluginId in teardownOrder)
        {
            if (_plugins.TryGetValue(pluginId, out var entry) && entry.State == PluginLifecycleState.Active)
            {
                try
                {
                    entry.Scope.Dispose();
                    entry.State = PluginLifecycleState.Unloaded;
                }
                catch (Exception ex)
                {
                    Report(new PluginLoadDiagnostic(
                        PluginLoadStage.Unmount, PluginDiagnosticSeverity.Error,
                        $"Error during shutdown: {ex.Message}", PluginId: pluginId, Exception: ex));
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Unmounts every active plugin, most recently mounted first.</summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
