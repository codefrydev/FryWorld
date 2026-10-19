using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Pipelines;
using FryWorld.Sdk.Shortcuts;
using Microsoft.Extensions.AI;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Primary implementation of <see cref="IFryPluginContext"/>.
/// Manages services, effect rollbacks, and capability descriptors for an active plugin session.
/// </summary>
/// <remarks>
/// Registry reads (<c>GetRegistered*</c>) are lock-free and allocation-free: each registry keeps an
/// immutable, already-sorted snapshot that is rebuilt only when something registers or unregisters.
/// Change events can be deferred with <see cref="DeferChangeNotifications"/> and delivered on a chosen
/// thread with <see cref="NotificationContext"/>.
/// </remarks>
public class FryPluginContext : IFryPluginContext
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    // Every live registration per contract, oldest first. _services holds the newest for lock-free reads;
    // when it is unregistered, the one before it becomes current again.
    private readonly Dictionary<Type, List<object>> _serviceRegistrations = new();
    private readonly IPipelineManager _pipelines;
    private readonly PluginScope? _scope;
    private readonly IServiceProvider? _fallbackServiceProvider;

    // Registries
    private readonly Registry<NavigationItemDescriptor> _navigationItems = new(RegistryKind.Navigation, static n => n.Order);
    private readonly Registry<OverlayDescriptor> _overlays = new(RegistryKind.Overlays);
    private readonly Registry<CommandPaletteDescriptor> _commands = new(RegistryKind.Commands);
    private readonly Registry<StatusBarWidgetDescriptor> _statusBarWidgets = new(RegistryKind.StatusBar, static w => w.Order);
    private readonly Registry<ToolDescriptor> _tools = new(RegistryKind.Tools);
    private readonly Registry<RibbonTabDescriptor> _ribbonTabs = new(RegistryKind.Ribbon, static r => r.Order);
    private readonly Registry<SidebarTabDescriptor> _sidebarTabs = new(RegistryKind.Sidebars, static s => s.Order);
    private readonly Registry<InspectorSectionDescriptor> _inspectors = new(RegistryKind.Inspectors, static i => i.Order);
    private readonly Registry<ShortcutDescriptor> _shortcuts = new(RegistryKind.Shortcuts);
    private readonly Registry<DialogDescriptor> _dialogs = new(RegistryKind.Dialogs);
    private readonly Registry<IAiContextProvider> _aiContextProviders = new(RegistryKind.AiContextProviders);
    private readonly Registry<AiToolDescriptor> _aiTools = new(RegistryKind.AiTools);
    private readonly Registry<AiPromptDescriptor> _aiPrompts = new(RegistryKind.AiPrompts);
    private readonly IShortcutRegistry _shortcutRegistry;

    // Change notification batching: while _deferDepth > 0, changed registries are recorded as bits in
    // _pendingChanges and each fires once when the last deferral ends.
    private int _deferDepth;
    private int _pendingChanges;

    /// <inheritdoc />
    public IPipelineManager Pipelines => _pipelines;

    /// <summary>
    /// Where registry change events are raised. When null (the default) they are raised synchronously on the
    /// thread that registered or unregistered. Set it to the UI thread's context so handlers can update
    /// bound collections without marshalling themselves.
    /// </summary>
    public SynchronizationContext? NotificationContext { get; set; }

    /// <inheritdoc />
    public event Action? NavigationItemsChanged;
    /// <inheritdoc />
    public event Action? OverlaysChanged;
    /// <inheritdoc />
    public event Action? CommandsChanged;
    /// <inheritdoc />
    public event Action? StatusBarWidgetsChanged;
    /// <inheritdoc />
    public event Action? ToolsChanged;
    /// <inheritdoc />
    public event Action? RibbonChanged;
    /// <inheritdoc />
    public event Action? SidebarsChanged;
    /// <inheritdoc />
    public event Action? InspectorsChanged;
    /// <inheritdoc />
    public event Action? ShortcutsChanged;
    /// <inheritdoc />
    public event Action? DialogsChanged;
    /// <inheritdoc />
    public event Action? AiContextProvidersChanged;
    /// <inheritdoc />
    public event Action? AiToolsChanged;
    /// <inheritdoc />
    public event Action? AiPromptsChanged;

    /// <inheritdoc />
    public virtual IShortcutRegistry Shortcuts => _shortcutRegistry;

    /// <summary>
    /// Creates a context. Registrations made directly on it are bound to <paramref name="scope"/> when one is given.
    /// </summary>
    public FryPluginContext(
        PluginScope? scope = null,
        IPipelineManager? pipelines = null,
        IServiceProvider? fallbackServiceProvider = null)
        : this(scope, pipelines, fallbackServiceProvider, null)
    {
    }

    /// <summary>
    /// Creates a context with a custom shortcut registry.
    /// </summary>
    public FryPluginContext(
        PluginScope? scope,
        IPipelineManager? pipelines,
        IServiceProvider? fallbackServiceProvider,
        IShortcutRegistry? shortcutRegistry)
    {
        _scope = scope;
        _pipelines = pipelines ?? new PipelineManager();
        _fallbackServiceProvider = fallbackServiceProvider;
        _shortcutRegistry = shortcutRegistry ?? new ShortcutRegistry();
        _shortcutRegistry.Changed += () => NotifyChanged(RegistryKind.Shortcuts);
        RegisterService<IShortcutRegistry>(_shortcutRegistry);
        RegisterService<IShortcutManager>(new ShortcutManager(_shortcutRegistry));
    }

    /// <summary>
    /// Creates a scoped child context attached to a plugin's lifecycle scope.
    /// Registrations automatically bind their unregister action to the scope.
    /// </summary>
    public virtual IFryPluginContext CreateScopedContext(PluginScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new ScopedPluginContext(this, scope);
    }

    /// <summary>
    /// Holds back registry change events until the returned handle is disposed, then raises each changed
    /// registry's event once. Use it around a batch of registrations, such as a plugin mounting, so the UI
    /// rebuilds once instead of once per registration. Deferrals nest.
    /// </summary>
    public IDisposable DeferChangeNotifications()
    {
        Interlocked.Increment(ref _deferDepth);
        return new DisposableAction(EndDeferral);
    }

    private void EndDeferral()
    {
        if (Interlocked.Decrement(ref _deferDepth) == 0)
        {
            FlushPendingChanges();
        }
    }

    private void NotifyChanged(RegistryKind kind)
    {
        if (Volatile.Read(ref _deferDepth) > 0)
        {
            Interlocked.Or(ref _pendingChanges, 1 << (int)kind);

            // The last deferral may have ended between the check and recording the change; flush it here
            // so it is not lost.
            if (Volatile.Read(ref _deferDepth) == 0)
            {
                FlushPendingChanges();
            }
            return;
        }

        Raise(kind);
    }

    private void FlushPendingChanges()
    {
        var pending = Interlocked.Exchange(ref _pendingChanges, 0);
        for (var kind = 0; pending != 0; kind++, pending >>= 1)
        {
            if ((pending & 1) != 0) Raise((RegistryKind)kind);
        }
    }

    private void Raise(RegistryKind kind)
    {
        var handler = kind switch
        {
            RegistryKind.Navigation => NavigationItemsChanged,
            RegistryKind.Overlays => OverlaysChanged,
            RegistryKind.Commands => CommandsChanged,
            RegistryKind.StatusBar => StatusBarWidgetsChanged,
            RegistryKind.Tools => ToolsChanged,
            RegistryKind.Ribbon => RibbonChanged,
            RegistryKind.Sidebars => SidebarsChanged,
            RegistryKind.Inspectors => InspectorsChanged,
            RegistryKind.Shortcuts => ShortcutsChanged,
            RegistryKind.Dialogs => DialogsChanged,
            RegistryKind.AiContextProviders => AiContextProvidersChanged,
            RegistryKind.AiTools => AiToolsChanged,
            RegistryKind.AiPrompts => AiPromptsChanged,
            _ => null
        };

        if (handler == null) return;

        var context = NotificationContext;
        if (context == null || ReferenceEquals(SynchronizationContext.Current, context))
        {
            handler();
        }
        else
        {
            context.Post(static state => ((Action)state!)(), handler);
        }
    }

    // --- 1. Service Bus ---

    /// <inheritdoc />
    public virtual void RegisterService<TService>(TService implementation) where TService : class
    {
        RegisterService(typeof(TService), implementation);
    }

    /// <inheritdoc />
    public virtual void RegisterService(Type serviceType, object implementation)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementation);

        lock (_serviceRegistrations)
        {
            if (!_serviceRegistrations.TryGetValue(serviceType, out var registrations))
            {
                registrations = new List<object>();
                _serviceRegistrations[serviceType] = registrations;
            }
            registrations.Add(implementation);
            _services[serviceType] = implementation;
        }

        _scope?.RegisterEffect(() => RemoveServiceRegistration(serviceType, implementation));
    }

    /// <summary>
    /// Removes one registration of <paramref name="serviceType"/>. If it was the current one, the previous
    /// registration (for example, the one a plugin overrode) becomes current again.
    /// </summary>
    private void RemoveServiceRegistration(Type serviceType, object implementation)
    {
        lock (_serviceRegistrations)
        {
            if (!_serviceRegistrations.TryGetValue(serviceType, out var registrations)) return;

            var index = registrations.LastIndexOf(implementation);
            if (index < 0) return;
            registrations.RemoveAt(index);

            if (registrations.Count == 0)
            {
                _serviceRegistrations.Remove(serviceType);
                _services.TryRemove(serviceType, out _);
            }
            else
            {
                _services[serviceType] = registrations[^1];
            }
        }
    }

    /// <inheritdoc />
    public virtual TService GetService<TService>() where TService : class
    {
        if (TryGetService<TService>(out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"Required service of type '{typeof(TService).FullName}' is not registered.");
    }

    /// <inheritdoc />
    public virtual bool TryGetService<TService>([NotNullWhen(true)] out TService? service) where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out var impl) && impl is TService typed)
        {
            service = typed;
            return true;
        }

        if (_fallbackServiceProvider?.GetService(typeof(TService)) is TService fallback)
        {
            service = fallback;
            return true;
        }

        service = null;
        return false;
    }

    /// <inheritdoc />
    public virtual bool HasService<TService>() where TService : class => HasService(typeof(TService));

    /// <inheritdoc />
    public virtual bool HasService(Type serviceType)
    {
        return _services.ContainsKey(serviceType) || (_fallbackServiceProvider?.GetService(serviceType) != null);
    }

    /// <inheritdoc />
    public virtual object? GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var impl))
        {
            return impl;
        }

        return _fallbackServiceProvider?.GetService(serviceType);
    }

    // --- 2. Reversible Effects ---

    /// <inheritdoc />
    public virtual IDisposable RegisterEffect(Action onDispose)
    {
        if (_scope != null) return _scope.RegisterEffect(onDispose);
        return new DisposableAction(onDispose);
    }

    /// <inheritdoc />
    public virtual IDisposable RegisterDisposable(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        return RegisterEffect(disposable.Dispose);
    }

    // --- 3. Pipelines ---

    /// <inheritdoc />
    public virtual IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware) =>
        _pipelines.RegisterWaterfall(pipelineName, middleware);

    /// <inheritdoc />
    public virtual Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null) =>
        _pipelines.ExecuteWaterfallAsync(pipelineName, context, terminal);

    /// <inheritdoc />
    public virtual IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class =>
        _pipelines.RegisterBail(pipelineName, handler);

    /// <inheritdoc />
    public virtual Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class =>
        _pipelines.ExecuteBailAsync<TContext, TResult>(pipelineName, context);

    /// <inheritdoc />
    public virtual IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        _pipelines.RegisterParallel(pipelineName, handler);

    /// <inheritdoc />
    public virtual Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context) =>
        _pipelines.ExecuteParallelAsync(pipelineName, context);

    /// <inheritdoc />
    public virtual IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        _pipelines.RegisterSerial(pipelineName, handler);

    /// <inheritdoc />
    public virtual Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context) =>
        _pipelines.ExecuteSerialAsync(pipelineName, context);

    // --- 4-11. Capability registries ---

    private IDisposable Register<T>(Registry<T> registry, T descriptor) where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        registry.Add(descriptor);
        NotifyChanged(registry.Kind);

        return RegisterEffect(() =>
        {
            if (registry.Remove(descriptor))
            {
                NotifyChanged(registry.Kind);
            }
        });
    }

    /// <inheritdoc />
    public virtual IDisposable RegisterNavigationItem(NavigationItemDescriptor descriptor) => Register(_navigationItems, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<NavigationItemDescriptor> GetRegisteredNavigationItems() => _navigationItems.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterOverlay(OverlayDescriptor descriptor) => Register(_overlays, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<OverlayDescriptor> GetRegisteredOverlays() => _overlays.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterCommand(CommandPaletteDescriptor descriptor) => Register(_commands, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<CommandPaletteDescriptor> GetRegisteredCommands() => _commands.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor) => Register(_statusBarWidgets, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<StatusBarWidgetDescriptor> GetRegisteredStatusBarWidgets() => _statusBarWidgets.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterTool(ToolDescriptor descriptor) => Register(_tools, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<ToolDescriptor> GetRegisteredTools() => _tools.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterRibbonTab(RibbonTabDescriptor descriptor) => Register(_ribbonTabs, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<RibbonTabDescriptor> GetRegisteredRibbonTabs() => _ribbonTabs.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterSidebarTab(SidebarTabDescriptor descriptor) => Register(_sidebarTabs, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<SidebarTabDescriptor> GetRegisteredSidebarTabs() => _sidebarTabs.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterInspectorSection(InspectorSectionDescriptor descriptor) => Register(_inspectors, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<InspectorSectionDescriptor> GetRegisteredInspectorSections() => _inspectors.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterShortcut(ShortcutDescriptor descriptor)
    {
        var legacyHandle = Register(_shortcuts, descriptor);
        var registryHandle = _shortcutRegistry.Register(descriptor);
        return new DisposableAction(() =>
        {
            registryHandle.Dispose();
            legacyHandle.Dispose();
        });
    }

    /// <inheritdoc />
    public virtual IDisposable RegisterShortcut(ShortcutBinding binding)
    {
        var registryHandle = _shortcutRegistry.Register(binding);
        var desc = new ShortcutDescriptor
        {
            Id = binding.Id,
            Description = binding.Description ?? binding.Title ?? binding.Id,
            KeyGesture = binding.KeyGesture,
            Command = binding.Command,
            Scope = binding.Scope,
            When = binding.When,
            WhenPredicate = binding.WhenPredicate,
            Priority = binding.Priority,
            Category = binding.Category,
            Title = binding.Title,
            AsyncAction = binding.AsyncAction != null ? () => binding.AsyncAction(new ShortcutExecutionContext(new ShortcutContext())) : null,
            IsEnabled = binding.IsEnabled
        };
        var legacyHandle = Register(_shortcuts, desc);
        return new DisposableAction(() =>
        {
            registryHandle.Dispose();
            legacyHandle.Dispose();
        });
    }

    /// <inheritdoc />
    public virtual IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts() => _shortcuts.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterDialog(DialogDescriptor descriptor) => Register(_dialogs, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<DialogDescriptor> GetRegisteredDialogs() => _dialogs.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterAiContextProvider(IAiContextProvider provider) => Register(_aiContextProviders, provider);

    /// <inheritdoc />
    public virtual IReadOnlyList<IAiContextProvider> GetRegisteredAiContextProviders() => _aiContextProviders.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterAiTool(AiToolDescriptor descriptor) => Register(_aiTools, descriptor);

    /// <inheritdoc />
    public virtual IDisposable RegisterAiTool(AIFunction function, bool requiresConfirmation = false, string? category = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        var descriptor = new AiToolDescriptor
        {
            Name = function.Name,
            Description = function.Description ?? function.Name,
            Category = category,
            RequiresConfirmation = requiresConfirmation,
            Function = function
        };
        return RegisterAiTool(descriptor);
    }

    /// <inheritdoc />
    public virtual IReadOnlyList<AiToolDescriptor> GetRegisteredAiTools() => _aiTools.Snapshot;

    /// <inheritdoc />
    public virtual IDisposable RegisterAiPrompt(AiPromptDescriptor descriptor) => Register(_aiPrompts, descriptor);

    /// <inheritdoc />
    public virtual IReadOnlyList<AiPromptDescriptor> GetRegisteredAiPrompts() => _aiPrompts.Snapshot;

    // --- Registry storage ---

    private enum RegistryKind
    {
        Navigation,
        Overlays,
        Commands,
        StatusBar,
        Tools,
        Ribbon,
        Sidebars,
        Inspectors,
        Shortcuts,
        Dialogs,
        AiContextProviders,
        AiTools,
        AiPrompts
    }

    /// <summary>
    /// Descriptors of one kind. Writes take a lock and rebuild an immutable snapshot (sorted by the order
    /// key, if any, stably so equal orders keep registration order); reads just return the snapshot.
    /// </summary>
    private sealed class Registry<T> where T : class
    {
        private readonly object _lock = new();
        private readonly List<T> _items = new();
        private readonly Func<T, int>? _orderKey;
        private IReadOnlyList<T> _snapshot = Array.Empty<T>();

        public Registry(RegistryKind kind, Func<T, int>? orderKey = null)
        {
            Kind = kind;
            _orderKey = orderKey;
        }

        public RegistryKind Kind { get; }

        public IReadOnlyList<T> Snapshot => Volatile.Read(ref _snapshot);

        public void Add(T item)
        {
            lock (_lock)
            {
                _items.Add(item);
                Rebuild();
            }
        }

        public bool Remove(T item)
        {
            lock (_lock)
            {
                if (!_items.Remove(item)) return false;
                Rebuild();
                return true;
            }
        }

        private void Rebuild()
        {
            var items = _orderKey == null ? _items.ToArray() : _items.OrderBy(_orderKey).ToArray();
            Volatile.Write(ref _snapshot, new ReadOnlyCollection<T>(items));
        }
    }

    /// <summary>
    /// The plugin a scoped context was created for, or null for any other context.
    /// </summary>
    internal static IFryPlugin? GetOwningPlugin(IFryPluginContext ctx) =>
        ctx is ScopedPluginContext scoped ? scoped.Scope.Plugin : null;

    /// <summary>Contract types of every service currently registered, for leak checks in tests.</summary>
    internal IReadOnlyCollection<Type> RegisteredServiceTypes => _services.Keys.ToList();

    // --- Inner Scoped Context ---

    private sealed class ScopedPluginContext : IFryPluginContext
    {
        private readonly FryPluginContext _parent;
        private readonly PluginScope _scope;

        public PluginScope Scope => _scope;

        public IPipelineManager Pipelines => _parent.Pipelines;

        public event Action? NavigationItemsChanged { add => _parent.NavigationItemsChanged += value; remove => _parent.NavigationItemsChanged -= value; }
        public event Action? OverlaysChanged { add => _parent.OverlaysChanged += value; remove => _parent.OverlaysChanged -= value; }
        public event Action? CommandsChanged { add => _parent.CommandsChanged += value; remove => _parent.CommandsChanged -= value; }
        public event Action? StatusBarWidgetsChanged { add => _parent.StatusBarWidgetsChanged += value; remove => _parent.StatusBarWidgetsChanged -= value; }
        public event Action? ToolsChanged { add => _parent.ToolsChanged += value; remove => _parent.ToolsChanged -= value; }
        public event Action? RibbonChanged { add => _parent.RibbonChanged += value; remove => _parent.RibbonChanged -= value; }
        public event Action? SidebarsChanged { add => _parent.SidebarsChanged += value; remove => _parent.SidebarsChanged -= value; }
        public event Action? InspectorsChanged { add => _parent.InspectorsChanged += value; remove => _parent.InspectorsChanged -= value; }
        public event Action? ShortcutsChanged { add => _parent.ShortcutsChanged += value; remove => _parent.ShortcutsChanged -= value; }
        public event Action? DialogsChanged { add => _parent.DialogsChanged += value; remove => _parent.DialogsChanged -= value; }
        public event Action? AiContextProvidersChanged { add => _parent.AiContextProvidersChanged += value; remove => _parent.AiContextProvidersChanged -= value; }
        public event Action? AiToolsChanged { add => _parent.AiToolsChanged += value; remove => _parent.AiToolsChanged -= value; }
        public event Action? AiPromptsChanged { add => _parent.AiPromptsChanged += value; remove => _parent.AiPromptsChanged -= value; }

        public ScopedPluginContext(FryPluginContext parent, PluginScope scope)
        {
            _parent = parent;
            _scope = scope;
        }

        public void RegisterService<TService>(TService implementation) where TService : class =>
            RegisterService(typeof(TService), implementation);

        public void RegisterService(Type serviceType, object implementation)
        {
            _parent.RegisterService(serviceType, implementation);
            _scope.RegisterEffect(() => _parent.RemoveServiceRegistration(serviceType, implementation));
        }

        public TService GetService<TService>() where TService : class => _parent.GetService<TService>();
        public bool TryGetService<TService>([NotNullWhen(true)] out TService? service) where TService : class => _parent.TryGetService(out service);
        public bool HasService<TService>() where TService : class => _parent.HasService<TService>();
        public bool HasService(Type serviceType) => _parent.HasService(serviceType);
        public object? GetService(Type serviceType) => _parent.GetService(serviceType);

        public IDisposable RegisterEffect(Action onDispose) => _scope.RegisterEffect(onDispose);
        public IDisposable RegisterDisposable(IDisposable disposable) => _scope.RegisterDisposable(disposable);

        public IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware) =>
            _scope.RegisterDisposable(_parent.RegisterWaterfall(pipelineName, middleware));

        public Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null) =>
            _parent.ExecuteWaterfallAsync(pipelineName, context, terminal);

        public IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class =>
            _scope.RegisterDisposable(_parent.RegisterBail(pipelineName, handler));

        public Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class =>
            _parent.ExecuteBailAsync<TContext, TResult>(pipelineName, context);

        public IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler) =>
            _scope.RegisterDisposable(_parent.RegisterParallel(pipelineName, handler));

        public Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context) =>
            _parent.ExecuteParallelAsync(pipelineName, context);

        public IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler) =>
            _scope.RegisterDisposable(_parent.RegisterSerial(pipelineName, handler));

        public Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context) =>
            _parent.ExecuteSerialAsync(pipelineName, context);

        public IDisposable RegisterNavigationItem(NavigationItemDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterNavigationItem(descriptor));

        public IReadOnlyList<NavigationItemDescriptor> GetRegisteredNavigationItems() => _parent.GetRegisteredNavigationItems();

        public IDisposable RegisterOverlay(OverlayDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterOverlay(descriptor));

        public IReadOnlyList<OverlayDescriptor> GetRegisteredOverlays() => _parent.GetRegisteredOverlays();

        public IDisposable RegisterCommand(CommandPaletteDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterCommand(descriptor));

        public IReadOnlyList<CommandPaletteDescriptor> GetRegisteredCommands() => _parent.GetRegisteredCommands();

        public IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterStatusBarWidget(descriptor));

        public IReadOnlyList<StatusBarWidgetDescriptor> GetRegisteredStatusBarWidgets() => _parent.GetRegisteredStatusBarWidgets();

        public IDisposable RegisterTool(ToolDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterTool(descriptor));

        public IReadOnlyList<ToolDescriptor> GetRegisteredTools() => _parent.GetRegisteredTools();

        public IDisposable RegisterRibbonTab(RibbonTabDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterRibbonTab(descriptor));

        public IReadOnlyList<RibbonTabDescriptor> GetRegisteredRibbonTabs() => _parent.GetRegisteredRibbonTabs();

        public IDisposable RegisterSidebarTab(SidebarTabDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterSidebarTab(descriptor));

        public IReadOnlyList<SidebarTabDescriptor> GetRegisteredSidebarTabs() => _parent.GetRegisteredSidebarTabs();

        public IDisposable RegisterInspectorSection(InspectorSectionDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterInspectorSection(descriptor));

        public IReadOnlyList<InspectorSectionDescriptor> GetRegisteredInspectorSections() => _parent.GetRegisteredInspectorSections();

        public IShortcutRegistry Shortcuts => _parent.Shortcuts;

        public IDisposable RegisterShortcut(ShortcutDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterShortcut(descriptor));

        public IDisposable RegisterShortcut(ShortcutBinding binding) =>
            _scope.RegisterDisposable(_parent.RegisterShortcut(binding));

        public IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts() => _parent.GetRegisteredShortcuts();

        public IDisposable RegisterDialog(DialogDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterDialog(descriptor));

        public IReadOnlyList<DialogDescriptor> GetRegisteredDialogs() => _parent.GetRegisteredDialogs();

        public IDisposable RegisterAiContextProvider(IAiContextProvider provider) =>
            _scope.RegisterDisposable(_parent.RegisterAiContextProvider(provider));

        public IReadOnlyList<IAiContextProvider> GetRegisteredAiContextProviders() => _parent.GetRegisteredAiContextProviders();

        public IDisposable RegisterAiTool(AiToolDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterAiTool(descriptor));

        public IDisposable RegisterAiTool(AIFunction function, bool requiresConfirmation = false, string? category = null) =>
            _scope.RegisterDisposable(_parent.RegisterAiTool(function, requiresConfirmation, category));

        public IReadOnlyList<AiToolDescriptor> GetRegisteredAiTools() => _parent.GetRegisteredAiTools();

        public IDisposable RegisterAiPrompt(AiPromptDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterAiPrompt(descriptor));

        public IReadOnlyList<AiPromptDescriptor> GetRegisteredAiPrompts() => _parent.GetRegisteredAiPrompts();
    }

    private sealed class DisposableAction : IDisposable
    {
        private Action? _action;

        public DisposableAction(Action action) => _action = action;

        public void Dispose()
        {
            var act = Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
