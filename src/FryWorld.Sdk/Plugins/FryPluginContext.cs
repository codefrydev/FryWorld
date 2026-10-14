using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Pipelines;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Primary implementation of <see cref="IFryPluginContext"/>.
/// Manages services, effect rollbacks, and capability descriptors for an active plugin session.
/// </summary>
public class FryPluginContext : IFryPluginContext
{
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly IPipelineManager _pipelines;
    private readonly PluginScope? _scope;
    private readonly IServiceProvider? _fallbackServiceProvider;

    // Registries
    private readonly List<NavigationItemDescriptor> _navigationItems = new();
    private readonly List<OverlayDescriptor> _overlays = new();
    private readonly List<CommandPaletteDescriptor> _commands = new();
    private readonly List<StatusBarWidgetDescriptor> _statusBarWidgets = new();
    private readonly List<ToolDescriptor> _tools = new();
    private readonly List<RibbonTabDescriptor> _ribbonTabs = new();
    private readonly List<SidebarTabDescriptor> _sidebarTabs = new();
    private readonly List<InspectorSectionDescriptor> _inspectors = new();
    private readonly List<ShortcutDescriptor> _shortcuts = new();
    private readonly List<DialogDescriptor> _dialogs = new();
    private readonly object _registryLock = new();

    public IPipelineManager Pipelines => _pipelines;

    public event Action? NavigationItemsChanged;
    public event Action? OverlaysChanged;
    public event Action? CommandsChanged;
    public event Action? StatusBarWidgetsChanged;
    public event Action? ToolsChanged;
    public event Action? RibbonChanged;
    public event Action? SidebarsChanged;
    public event Action? InspectorsChanged;
    public event Action? ShortcutsChanged;
    public event Action? DialogsChanged;

    public FryPluginContext(
        PluginScope? scope = null,
        IPipelineManager? pipelines = null,
        IServiceProvider? fallbackServiceProvider = null)
    {
        _scope = scope;
        _pipelines = pipelines ?? new PipelineManager();
        _fallbackServiceProvider = fallbackServiceProvider;
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

    // --- 1. Service Bus ---

    public virtual void RegisterService<TService>(TService implementation) where TService : class
    {
        RegisterService(typeof(TService), implementation);
    }

    public virtual void RegisterService(Type serviceType, object implementation)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementation);

        _services[serviceType] = implementation;
        _scope?.RegisterEffect(() => _services.TryRemove(serviceType, out _));
    }

    public virtual TService GetService<TService>() where TService : class
    {
        if (TryGetService<TService>(out var service))
        {
            return service;
        }

        throw new InvalidOperationException($"Required service of type '{typeof(TService).FullName}' is not registered.");
    }

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

    public virtual bool HasService<TService>() where TService : class => HasService(typeof(TService));

    public virtual bool HasService(Type serviceType)
    {
        return _services.ContainsKey(serviceType) || (_fallbackServiceProvider?.GetService(serviceType) != null);
    }

    public virtual object? GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var impl))
        {
            return impl;
        }

        return _fallbackServiceProvider?.GetService(serviceType);
    }

    // --- 2. Reversible Effects ---

    public virtual IDisposable RegisterEffect(Action onDispose)
    {
        if (_scope != null) return _scope.RegisterEffect(onDispose);
        return new DisposableAction(onDispose);
    }

    public virtual IDisposable RegisterDisposable(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        return RegisterEffect(disposable.Dispose);
    }

    // --- 3. Pipelines ---

    public virtual IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware) =>
        _pipelines.RegisterWaterfall(pipelineName, middleware);

    public virtual Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null) =>
        _pipelines.ExecuteWaterfallAsync(pipelineName, context, terminal);

    public virtual IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class =>
        _pipelines.RegisterBail(pipelineName, handler);

    public virtual Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class =>
        _pipelines.ExecuteBailAsync<TContext, TResult>(pipelineName, context);

    public virtual IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        _pipelines.RegisterParallel(pipelineName, handler);

    public virtual Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context) =>
        _pipelines.ExecuteParallelAsync(pipelineName, context);

    public virtual IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler) =>
        _pipelines.RegisterSerial(pipelineName, handler);

    public virtual Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context) =>
        _pipelines.ExecuteSerialAsync(pipelineName, context);

    // --- 4. Navigation Registry ---

    public virtual IDisposable RegisterNavigationItem(NavigationItemDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _navigationItems.Add(descriptor); }
        NavigationItemsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _navigationItems.Remove(descriptor); }
            NavigationItemsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<NavigationItemDescriptor> GetRegisteredNavigationItems()
    {
        lock (_registryLock) { return _navigationItems.OrderBy(n => n.Order).ToList(); }
    }

    // --- 5. Overlay Registry ---

    public virtual IDisposable RegisterOverlay(OverlayDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _overlays.Add(descriptor); }
        OverlaysChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _overlays.Remove(descriptor); }
            OverlaysChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<OverlayDescriptor> GetRegisteredOverlays()
    {
        lock (_registryLock) { return _overlays.ToList(); }
    }

    // --- 6. Command Palette Registry ---

    public virtual IDisposable RegisterCommand(CommandPaletteDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _commands.Add(descriptor); }
        CommandsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _commands.Remove(descriptor); }
            CommandsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<CommandPaletteDescriptor> GetRegisteredCommands()
    {
        lock (_registryLock) { return _commands.ToList(); }
    }

    // --- 7. Status Bar Registry ---

    public virtual IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _statusBarWidgets.Add(descriptor); }
        StatusBarWidgetsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _statusBarWidgets.Remove(descriptor); }
            StatusBarWidgetsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<StatusBarWidgetDescriptor> GetRegisteredStatusBarWidgets()
    {
        lock (_registryLock) { return _statusBarWidgets.OrderBy(w => w.Order).ToList(); }
    }

    // --- 8. Tool Registry ---

    public virtual IDisposable RegisterTool(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _tools.Add(descriptor); }
        ToolsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _tools.Remove(descriptor); }
            ToolsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<ToolDescriptor> GetRegisteredTools()
    {
        lock (_registryLock) { return _tools.ToList(); }
    }

    // --- 9. Ribbon Registry ---

    public virtual IDisposable RegisterRibbonTab(RibbonTabDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _ribbonTabs.Add(descriptor); }
        RibbonChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _ribbonTabs.Remove(descriptor); }
            RibbonChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<RibbonTabDescriptor> GetRegisteredRibbonTabs()
    {
        lock (_registryLock) { return _ribbonTabs.OrderBy(r => r.Order).ToList(); }
    }

    // --- 10. Sidebars & Inspectors ---

    public virtual IDisposable RegisterSidebarTab(SidebarTabDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _sidebarTabs.Add(descriptor); }
        SidebarsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _sidebarTabs.Remove(descriptor); }
            SidebarsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<SidebarTabDescriptor> GetRegisteredSidebarTabs()
    {
        lock (_registryLock) { return _sidebarTabs.OrderBy(s => s.Order).ToList(); }
    }

    public virtual IDisposable RegisterInspectorSection(InspectorSectionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _inspectors.Add(descriptor); }
        InspectorsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _inspectors.Remove(descriptor); }
            InspectorsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<InspectorSectionDescriptor> GetRegisteredInspectorSections()
    {
        lock (_registryLock) { return _inspectors.OrderBy(i => i.Order).ToList(); }
    }

    // --- 11. Shortcuts & Dialogs ---

    public virtual IDisposable RegisterShortcut(ShortcutDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _shortcuts.Add(descriptor); }
        ShortcutsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _shortcuts.Remove(descriptor); }
            ShortcutsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts()
    {
        lock (_registryLock) { return _shortcuts.ToList(); }
    }

    public virtual IDisposable RegisterDialog(DialogDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_registryLock) { _dialogs.Add(descriptor); }
        DialogsChanged?.Invoke();

        return RegisterEffect(() =>
        {
            lock (_registryLock) { _dialogs.Remove(descriptor); }
            DialogsChanged?.Invoke();
        });
    }

    public virtual IReadOnlyList<DialogDescriptor> GetRegisteredDialogs()
    {
        lock (_registryLock) { return _dialogs.ToList(); }
    }

    // --- Inner Scoped Context ---

    private sealed class ScopedPluginContext : IFryPluginContext
    {
        private readonly FryPluginContext _parent;
        private readonly PluginScope _scope;

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
            _scope.RegisterEffect(() => _parent._services.TryRemove(serviceType, out _));
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

        public IDisposable RegisterShortcut(ShortcutDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterShortcut(descriptor));

        public IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts() => _parent.GetRegisteredShortcuts();

        public IDisposable RegisterDialog(DialogDescriptor descriptor) =>
            _scope.RegisterDisposable(_parent.RegisterDialog(descriptor));

        public IReadOnlyList<DialogDescriptor> GetRegisteredDialogs() => _parent.GetRegisteredDialogs();
    }

    private sealed class DisposableAction : IDisposable
    {
        private Action? _action;

        public DisposableAction(Action action) => _action = action;

        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
