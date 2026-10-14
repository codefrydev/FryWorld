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
public interface IFryPluginContext : IServiceProvider
{
    // --- 1. Dynamic Service Bus ---
    void RegisterService<TService>(TService implementation) where TService : class;
    void RegisterService(Type serviceType, object implementation);
    TService GetService<TService>() where TService : class;
    bool TryGetService<TService>([NotNullWhen(true)] out TService? service) where TService : class;
    bool HasService<TService>() where TService : class;
    bool HasService(Type serviceType);

    // --- 2. Reversible Effects (LIFO Rollback) ---
    IDisposable RegisterEffect(Action onDispose);
    IDisposable RegisterDisposable(IDisposable disposable);

    // --- 3. Pipeline Dispatch ---
    IPipelineManager Pipelines { get; }
    IDisposable RegisterWaterfall<TContext>(string pipelineName, Func<TContext, Func<Task>, Task> middleware);
    Task ExecuteWaterfallAsync<TContext>(string pipelineName, TContext context, Func<Task>? terminal = null);
    IDisposable RegisterBail<TContext, TResult>(string pipelineName, Func<TContext, Task<TResult?>> handler) where TResult : class;
    Task<TResult?> ExecuteBailAsync<TContext, TResult>(string pipelineName, TContext context) where TResult : class;
    IDisposable RegisterParallel<TContext>(string pipelineName, Func<TContext, Task> handler);
    Task ExecuteParallelAsync<TContext>(string pipelineName, TContext context);
    IDisposable RegisterSerial<TContext>(string pipelineName, Func<TContext, Task> handler);
    Task ExecuteSerialAsync<TContext>(string pipelineName, TContext context);

    // --- 4. Dynamic Workspace Navigation Registry ---
    event Action? NavigationItemsChanged;
    IDisposable RegisterNavigationItem(NavigationItemDescriptor descriptor);
    IReadOnlyList<NavigationItemDescriptor> GetRegisteredNavigationItems();

    // --- 5. Floating Shell Overlay Registry ---
    event Action? OverlaysChanged;
    IDisposable RegisterOverlay(OverlayDescriptor descriptor);
    IReadOnlyList<OverlayDescriptor> GetRegisteredOverlays();

    // --- 6. Command Palette Registry (⌘K / Ctrl+K) ---
    event Action? CommandsChanged;
    IDisposable RegisterCommand(CommandPaletteDescriptor descriptor);
    IReadOnlyList<CommandPaletteDescriptor> GetRegisteredCommands();

    // --- 7. Status Bar Registry ---
    event Action? StatusBarWidgetsChanged;
    IDisposable RegisterStatusBarWidget(StatusBarWidgetDescriptor descriptor);
    IReadOnlyList<StatusBarWidgetDescriptor> GetRegisteredStatusBarWidgets();

    // --- 8. Tool Registry ---
    event Action? ToolsChanged;
    IDisposable RegisterTool(ToolDescriptor descriptor);
    IReadOnlyList<ToolDescriptor> GetRegisteredTools();

    // --- 9. Dynamic Ribbon & Toolbar Registry ---
    event Action? RibbonChanged;
    IDisposable RegisterRibbonTab(RibbonTabDescriptor descriptor);
    IReadOnlyList<RibbonTabDescriptor> GetRegisteredRibbonTabs();

    // --- 10. Sidebar & Inspector Registries ---
    event Action? SidebarsChanged;
    IDisposable RegisterSidebarTab(SidebarTabDescriptor descriptor);
    IReadOnlyList<SidebarTabDescriptor> GetRegisteredSidebarTabs();

    event Action? InspectorsChanged;
    IDisposable RegisterInspectorSection(InspectorSectionDescriptor descriptor);
    IReadOnlyList<InspectorSectionDescriptor> GetRegisteredInspectorSections();

    // --- 11. Shortcuts & Dialogs ---
    event Action? ShortcutsChanged;
    IDisposable RegisterShortcut(ShortcutDescriptor descriptor);
    IReadOnlyList<ShortcutDescriptor> GetRegisteredShortcuts();

    event Action? DialogsChanged;
    IDisposable RegisterDialog(DialogDescriptor descriptor);
    IReadOnlyList<DialogDescriptor> GetRegisteredDialogs();
}
