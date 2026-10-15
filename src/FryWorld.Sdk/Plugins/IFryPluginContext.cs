using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Pipelines;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Capability context provided to plugins during their mount lifecycle.
/// Serves as the dynamic service bus, pipeline dispatcher, and capability registry.
/// </summary>
/// <remarks>
/// The context a plugin receives in <see cref="IFryPlugin.ApplyAsync"/> is scoped to that plugin: everything
/// registered through it (services, pipeline handlers, descriptors, effects) is removed automatically, in
/// reverse order, when the plugin unmounts. Each <c>Register*</c> method also returns an
/// <see cref="IDisposable"/> that removes the registration early. Reads (<c>GetService</c>,
/// <c>GetRegistered*</c>) see everything registered by every plugin and by the host.
/// </remarks>
public interface IFryPluginContext : IServiceProvider
{
    // --- 1. Dynamic Service Bus ---

    /// <summary>
    /// Makes <paramref name="implementation"/> available as <typeparamref name="TService"/> to the host and other
    /// plugins. A later registration of the same contract takes precedence until it is removed.
    /// </summary>
    void RegisterService<TService>(TService implementation) where TService : class;

    /// <summary>Makes <paramref name="implementation"/> available as <paramref name="serviceType"/>.</summary>
    void RegisterService(Type serviceType, object implementation);

    /// <summary>Gets a service registered by a plugin or provided by the host.</summary>
    /// <exception cref="InvalidOperationException">No such service is registered.</exception>
    TService GetService<TService>() where TService : class;

    /// <summary>Gets a service if one is registered.</summary>
    bool TryGetService<TService>([NotNullWhen(true)] out TService? service) where TService : class;

    /// <summary>Whether a <typeparamref name="TService"/> is registered or provided by the host.</summary>
    bool HasService<TService>() where TService : class;

    /// <summary>Whether a <paramref name="serviceType"/> is registered or provided by the host.</summary>
    bool HasService(Type serviceType);

    // --- 2. Reversible Effects (LIFO Rollback) ---

    /// <summary>
    /// Registers <paramref name="onDispose"/> to run when the plugin unmounts. Use it to undo anything the plugin
    /// did outside the context: event subscriptions, timers, files, processes. Disposing the returned handle
    /// runs the effect now instead.
    /// </summary>
    IDisposable RegisterEffect(Action onDispose);

    /// <summary>Disposes <paramref name="disposable"/> when the plugin unmounts.</summary>
    IDisposable RegisterDisposable(IDisposable disposable);

    // --- 3. Pipeline Dispatch ---

    /// <summary>
    /// The shared pipeline manager. Prefer the <c>Register*</c> pipeline methods on this context: they tie the
    /// handler to the plugin, while registering on <see cref="Pipelines"/> directly does not.
    /// </summary>
    IPipelineManager Pipelines { get; }

    /// <summary>Adds around-middleware to the waterfall pipeline <paramref name="pipelineName"/>; call <c>next()</c> to continue the chain.</summary>
    IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware);

    /// <summary>Runs the waterfall pipeline <paramref name="pipelineName"/>, then <paramref name="terminal"/> at the end of the chain.</summary>
    Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null);

    /// <summary>Adds a handler to the bail pipeline <paramref name="pipelineName"/>; return null to let the next handler try.</summary>
    IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class;

    /// <summary>Runs the bail pipeline <paramref name="pipelineName"/>, returning the first non-null result, or null.</summary>
    Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class;

    /// <summary>Adds a handler to the parallel pipeline <paramref name="pipelineName"/>.</summary>
    IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler);

    /// <summary>Runs every handler of the parallel pipeline <paramref name="pipelineName"/> at once and waits for all of them.</summary>
    Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context);

    /// <summary>Adds a handler to the serial pipeline <paramref name="pipelineName"/>.</summary>
    IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler);

    /// <summary>Runs the handlers of the serial pipeline <paramref name="pipelineName"/> one after another, in registration order.</summary>
    Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context);

    // --- 4. Dynamic Workspace Navigation Registry ---

    /// <summary>Raised when a page is registered or removed.</summary>
    event Action? NavigationItemsChanged;

    /// <summary>Adds a full-viewport page (workspace) to the host's navigation.</summary>
    IDisposable RegisterNavigationItem(NavigationItemDescriptor descriptor);

    /// <summary>All registered pages, ordered by <see cref="NavigationItemDescriptor.Order"/>.</summary>
    IReadOnlyList<NavigationItemDescriptor> GetRegisteredNavigationItems();

    // --- 5. Floating Shell Overlay Registry ---

    /// <summary>Raised when an overlay is registered or removed.</summary>
    event Action? OverlaysChanged;

    /// <summary>Adds a floating overlay (a game, a widget, a mini-app) that the user or the plugin can open.</summary>
    IDisposable RegisterOverlay(OverlayDescriptor descriptor);

    /// <summary>All registered overlays, in registration order.</summary>
    IReadOnlyList<OverlayDescriptor> GetRegisteredOverlays();

    // --- 6. Command Palette Registry (⌘K / Ctrl+K) ---

    /// <summary>Raised when a command is registered or removed.</summary>
    event Action? CommandsChanged;

    /// <summary>Adds an action to the host's command palette.</summary>
    IDisposable RegisterCommand(CommandPaletteDescriptor descriptor);

    /// <summary>All registered commands, in registration order.</summary>
    IReadOnlyList<CommandPaletteDescriptor> GetRegisteredCommands();

    // --- 7. Status Bar Registry ---

    /// <summary>Raised when a status bar widget is registered or removed.</summary>
    event Action? StatusBarWidgetsChanged;

    /// <summary>Adds a widget to the host's status bar.</summary>
    IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor);

    /// <summary>All registered status bar widgets, ordered by <see cref="StatusBarWidgetDescriptor.Order"/>.</summary>
    IReadOnlyList<StatusBarWidgetDescriptor> GetRegisteredStatusBarWidgets();

    // --- 8. Tool Registry ---

    /// <summary>Raised when a tool is registered or removed.</summary>
    event Action? ToolsChanged;

    /// <summary>Adds a discrete tool or action.</summary>
    IDisposable RegisterTool(ToolDescriptor descriptor);

    /// <summary>All registered tools, in registration order.</summary>
    IReadOnlyList<ToolDescriptor> GetRegisteredTools();

    // --- 9. Dynamic Ribbon & Toolbar Registry ---

    /// <summary>Raised when a ribbon tab is registered or removed.</summary>
    event Action? RibbonChanged;

    /// <summary>Adds a ribbon or toolbar tab.</summary>
    IDisposable RegisterRibbonTab(RibbonTabDescriptor descriptor);

    /// <summary>All registered ribbon tabs, ordered by <see cref="RibbonTabDescriptor.Order"/>.</summary>
    IReadOnlyList<RibbonTabDescriptor> GetRegisteredRibbonTabs();

    // --- 10. Sidebar & Inspector Registries ---

    /// <summary>Raised when a sidebar tab is registered or removed.</summary>
    event Action? SidebarsChanged;

    /// <summary>Adds a sidebar tab.</summary>
    IDisposable RegisterSidebarTab(SidebarTabDescriptor descriptor);

    /// <summary>All registered sidebar tabs, ordered by <see cref="SidebarTabDescriptor.Order"/>.</summary>
    IReadOnlyList<SidebarTabDescriptor> GetRegisteredSidebarTabs();

    /// <summary>Raised when an inspector section is registered or removed.</summary>
    event Action? InspectorsChanged;

    /// <summary>Adds a section to the host's inspector panel.</summary>
    IDisposable RegisterInspectorSection(InspectorSectionDescriptor descriptor);

    /// <summary>All registered inspector sections, ordered by <see cref="InspectorSectionDescriptor.Order"/>.</summary>
    IReadOnlyList<InspectorSectionDescriptor> GetRegisteredInspectorSections();

    // --- 11. Shortcuts & Dialogs ---

    /// <summary>Raised when a keyboard shortcut is registered or removed.</summary>
    event Action? ShortcutsChanged;

    /// <summary>Adds a keyboard shortcut.</summary>
    IDisposable RegisterShortcut(ShortcutDescriptor descriptor);

    /// <summary>All registered keyboard shortcuts, in registration order.</summary>
    IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts();

    /// <summary>Raised when a dialog is registered or removed.</summary>
    event Action? DialogsChanged;

    /// <summary>Adds a modal dialog the plugin can open with <c>IShellService.ShowDialogAsync</c>.</summary>
    IDisposable RegisterDialog(DialogDescriptor descriptor);

    /// <summary>All registered dialogs, in registration order.</summary>
    IReadOnlyList<DialogDescriptor> GetRegisteredDialogs();
}
