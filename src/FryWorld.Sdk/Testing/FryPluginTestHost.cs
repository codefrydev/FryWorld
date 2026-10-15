using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Pipelines;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;

namespace FryWorld.Sdk.Testing;

/// <summary>
/// Mounts a plugin in a unit test, with in-memory stand-ins for the services a host provides, so plugin
/// authors can test registrations, commands and clean unloading without starting a UI.
/// </summary>
/// <example>
/// <code>
/// await using var host = await FryPluginTestHost.MountAsync&lt;MyPlugin&gt;();
/// Assert.Contains(host.Context.GetRegisteredCommands(), c =&gt; c.Id == "cmd.my.open");
/// await host.RunCommandAsync("cmd.my.open");
/// Assert.Equal("my.page", host.Shell.LastNavigation);
/// await host.AssertCleanUnmountAsync();
/// </code>
/// </example>
public sealed class FryPluginTestHost : IAsyncDisposable
{
    private readonly FryPluginContext _context;
    private readonly PipelineManager _pipelines;
    private readonly IReadOnlyCollection<Type> _servicesBeforeMount;

    private FryPluginTestHost(
        IFryPlugin plugin, FryPluginContext context, PipelineManager pipelines, PluginHost host,
        RecordingShellService shell, RecordingNotificationService notifications, InMemorySettingsStore settings)
    {
        Plugin = plugin;
        _context = context;
        _pipelines = pipelines;
        Host = host;
        Shell = shell;
        Notifications = notifications;
        Settings = settings;

        // Services the host and the test registered are the baseline, not something the plugin leaked.
        _servicesBeforeMount = context.RegisteredServiceTypes;
    }

    /// <summary>The plugin under test.</summary>
    public IFryPlugin Plugin { get; }

    /// <summary>The host the plugin is mounted in.</summary>
    public PluginHost Host { get; }

    /// <summary>The root context, holding everything the plugin registered.</summary>
    public IFryPluginContext Context => _context;

    /// <summary>Records what the plugin asked the shell to do.</summary>
    public RecordingShellService Shell { get; }

    /// <summary>Records the toasts and errors the plugin showed.</summary>
    public RecordingNotificationService Notifications { get; }

    /// <summary>The settings store the plugin's <c>GetPluginSettings()</c> reads and writes.</summary>
    public InMemorySettingsStore Settings { get; }

    /// <summary>The plugin's current lifecycle state.</summary>
    public PluginLifecycleState? State => Host.GetPluginState(Plugin.Id);

    /// <summary>Creates <typeparamref name="TPlugin"/> and mounts it.</summary>
    /// <param name="configure">Registers extra services the plugin requires, before it mounts.</param>
    /// <param name="ct">Passed to the plugin's <c>ApplyAsync</c>.</param>
    /// <exception cref="InvalidOperationException">The plugin failed to mount; the inner exception says why.</exception>
    public static Task<FryPluginTestHost> MountAsync<TPlugin>(Action<IFryPluginContext>? configure = null, CancellationToken ct = default)
        where TPlugin : IFryPlugin, new() =>
        MountAsync(new TPlugin(), configure, ct);

    /// <summary>Mounts <paramref name="plugin"/>.</summary>
    /// <param name="plugin">The plugin to test.</param>
    /// <param name="configure">Registers extra services the plugin requires, before it mounts.</param>
    /// <param name="ct">Passed to the plugin's <c>ApplyAsync</c>.</param>
    /// <exception cref="InvalidOperationException">The plugin failed to mount; the inner exception says why.</exception>
    public static async Task<FryPluginTestHost> MountAsync(IFryPlugin plugin, Action<IFryPluginContext>? configure = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var pipelines = new PipelineManager();
        var context = new FryPluginContext(pipelines: pipelines);
        var host = new PluginHost(context);

        var shell = new RecordingShellService { Context = context };
        var notifications = new RecordingNotificationService();
        var settings = new InMemorySettingsStore();
        context.RegisterService<IShellService>(shell);
        context.RegisterService<INotificationService>(notifications);
        context.RegisterService<ISettingsManager>(settings);
        configure?.Invoke(context);

        var testHost = new FryPluginTestHost(plugin, context, pipelines, host, shell, notifications, settings);

        host.RegisterPlugin(plugin);
        await host.MountPluginAsync(plugin, ct);

        if (host.GetPluginState(plugin.Id) != PluginLifecycleState.Active)
        {
            var error = host.GetRegistration(plugin.Id)?.FailureError;
            await host.DisposeAsync();
            throw new InvalidOperationException($"Plugin '{plugin.Id}' failed to mount: {error?.Message}", error);
        }

        return testHost;
    }

    /// <summary>
    /// Runs the command palette entry <paramref name="commandId"/> the way the host would: its
    /// <c>AsyncAction</c> if it has one, otherwise its <c>Command</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No such command is registered, or it cannot execute.</exception>
    public async Task RunCommandAsync(string commandId, object? parameter = null)
    {
        var command = Context.GetRegisteredCommands().FirstOrDefault(c => c.Id == commandId)
            ?? throw new InvalidOperationException(
                $"No command '{commandId}' is registered. Registered: {string.Join(", ", Context.GetRegisteredCommands().Select(c => c.Id))}.");

        if (command.AsyncAction != null)
        {
            await command.AsyncAction();
        }
        else if (command.Command != null)
        {
            if (!command.Command.CanExecute(parameter))
            {
                throw new InvalidOperationException($"Command '{commandId}' cannot execute right now.");
            }
            command.Command.Execute(parameter);
        }
        else
        {
            throw new InvalidOperationException($"Command '{commandId}' has neither an AsyncAction nor a Command.");
        }
    }

    /// <summary>Unmounts the plugin.</summary>
    public Task UnmountAsync() => Host.UnmountPluginAsync(Plugin.Id);

    /// <summary>
    /// Unmounts the plugin and checks it left nothing behind: no registrations in any registry, no
    /// services, and no pipeline handlers. Anything left over keeps the plugin's assemblies in memory
    /// after the host unloads it.
    /// </summary>
    /// <exception cref="FryPluginLeakException">Something the plugin registered was still there after unmount.</exception>
    public async Task AssertCleanUnmountAsync()
    {
        await UnmountAsync();

        var leftovers = new List<string>();
        void Check<T>(string registry, IReadOnlyList<T> items, Func<T, string> id)
        {
            foreach (var item in items) leftovers.Add($"{registry}: {id(item)}");
        }

        Check("navigation item", Context.GetRegisteredNavigationItems(), n => n.Id);
        Check("overlay", Context.GetRegisteredOverlays(), o => o.Id);
        Check("command", Context.GetRegisteredCommands(), c => c.Id);
        Check("status bar widget", Context.GetRegisteredStatusBarWidgets(), w => w.Id);
        Check("tool", Context.GetRegisteredTools(), t => t.Id);
        Check("ribbon tab", Context.GetRegisteredRibbonTabs(), r => r.Id);
        Check("sidebar tab", Context.GetRegisteredSidebarTabs(), s => s.Id);
        Check("inspector section", Context.GetRegisteredInspectorSections(), i => i.Id);
        Check("shortcut", Context.GetRegisteredShortcuts(), s => s.Id);
        Check("dialog", Context.GetRegisteredDialogs(), d => d.Id);

        foreach (var service in _context.RegisteredServiceTypes.Except(_servicesBeforeMount))
        {
            leftovers.Add($"service: {service.FullName}");
        }

        foreach (var pipeline in _pipelines.RegisteredPipelineNames)
        {
            leftovers.Add($"pipeline handler: {pipeline}");
        }

        if (leftovers.Count > 0)
        {
            throw new FryPluginLeakException(Plugin.Id, leftovers);
        }
    }

    /// <summary>Unmounts the plugin if it is still mounted.</summary>
    public ValueTask DisposeAsync() => Host.DisposeAsync();
}

/// <summary>
/// Thrown by <see cref="FryPluginTestHost.AssertCleanUnmountAsync"/> when a plugin's registrations outlive it.
/// </summary>
public sealed class FryPluginLeakException : Exception
{
    /// <summary>Creates the exception listing what <paramref name="pluginId"/> left behind.</summary>
    public FryPluginLeakException(string pluginId, IReadOnlyList<string> leftovers)
        : base($"Plugin '{pluginId}' left {leftovers.Count} registration(s) behind after unmount:\n  " +
               string.Join("\n  ", leftovers) +
               "\nRegister everything through the context passed to ApplyAsync (or with ctx.RegisterEffect) so it is removed on unmount.")
    {
        PluginId = pluginId;
        Leftovers = leftovers;
    }

    /// <summary>The plugin that leaked.</summary>
    public string PluginId { get; }

    /// <summary>One line per leftover registration.</summary>
    public IReadOnlyList<string> Leftovers { get; }
}

/// <summary>
/// <see cref="IShellService"/> that records calls instead of changing any UI, for tests.
/// It answers whether ids exist from the registries, like a real host.
/// </summary>
public sealed class RecordingShellService : IShellService
{
    private readonly ConcurrentDictionary<string, byte> _openOverlays = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The context whose registries are checked; set by <see cref="FryPluginTestHost"/>.</summary>
    public IFryPluginContext? Context { get; set; }

    /// <summary>Every call, in order, as "NavigateTo:id", "ShowOverlay:id" and so on.</summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>The id of the last page navigated to.</summary>
    public string? LastNavigation { get; private set; }

    /// <summary>Overlays currently open.</summary>
    public IReadOnlyCollection<string> OpenOverlays => _openOverlays.Keys.ToList();

    private bool HasNavigation(string id) => Context?.GetRegisteredNavigationItems().Any(n => n.Id == id) ?? true;
    private bool HasOverlay(string id) => Context?.GetRegisteredOverlays().Any(o => o.Id == id) ?? true;
    private bool HasDialog(string id) => Context?.GetRegisteredDialogs().Any(d => d.Id == id) ?? true;

    /// <inheritdoc />
    public bool NavigateTo(string navigationItemId)
    {
        Calls.Enqueue($"NavigateTo:{navigationItemId}");
        if (!HasNavigation(navigationItemId)) return false;
        LastNavigation = navigationItemId;
        return true;
    }

    /// <inheritdoc />
    public bool ShowOverlay(string overlayId)
    {
        Calls.Enqueue($"ShowOverlay:{overlayId}");
        if (!HasOverlay(overlayId)) return false;
        _openOverlays[overlayId] = 0;
        return true;
    }

    /// <inheritdoc />
    public bool HideOverlay(string overlayId)
    {
        Calls.Enqueue($"HideOverlay:{overlayId}");
        if (!HasOverlay(overlayId)) return false;
        _openOverlays.TryRemove(overlayId, out _);
        return true;
    }

    /// <inheritdoc />
    public bool ToggleOverlay(string overlayId)
    {
        Calls.Enqueue($"ToggleOverlay:{overlayId}");
        if (!HasOverlay(overlayId)) return false;
        if (!_openOverlays.TryRemove(overlayId, out _)) _openOverlays[overlayId] = 0;
        return true;
    }

    /// <inheritdoc />
    public bool IsOverlayVisible(string overlayId) => _openOverlays.ContainsKey(overlayId);

    /// <inheritdoc />
    public Task<bool> ShowDialogAsync(string dialogId)
    {
        Calls.Enqueue($"ShowDialog:{dialogId}");
        return Task.FromResult(HasDialog(dialogId));
    }
}

/// <summary>
/// <see cref="INotificationService"/> that records messages instead of showing them, for tests.
/// </summary>
public sealed class RecordingNotificationService : INotificationService
{
    /// <summary>Every toast and error, in order, as (Title, Message, IsError).</summary>
    public ConcurrentQueue<(string Title, string Message, bool IsError)> Messages { get; } = new();

    /// <inheritdoc />
    public void ShowToast(string message, string title = "Information", string? iconKind = null) =>
        Messages.Enqueue((title, message, false));

    /// <inheritdoc />
    public void ShowError(string message, string title = "Error") =>
        Messages.Enqueue((title, message, true));
}
