using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;

namespace FryWorld.ViewModels;

public partial class NavigationRailItem : ViewModelBase
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string IconKind { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    public Func<Control>? ViewFactory { get; init; }
}

public partial class ActiveOverlayViewModel : ViewModelBase
{
    public OverlayDescriptor Descriptor { get; }
    public Control View { get; }

    public string Title => Descriptor.Title;
    public string? IconKind => Descriptor.IconKind ?? "WindowMaximize";

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    public ActiveOverlayViewModel(OverlayDescriptor descriptor)
    {
        Descriptor = descriptor;
        View = descriptor.ViewFactory();
        Width = descriptor.DefaultWidth;
        Height = descriptor.DefaultHeight;
    }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IThemeService _themeService;
    private readonly IPluginMarketplaceService _marketplace;
    private readonly Dictionary<string, Control> _viewCache = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private NavigationRailItem? _selectedNavItem;

    [ObservableProperty]
    private Control? _currentView;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private bool _isLauncherActive = true;

    [ObservableProperty]
    private bool _isPluginsActive;

    [ObservableProperty]
    private int _loadedPluginCount;

    [ObservableProperty]
    private string _memoryUsageText = "12 MB";

    [ObservableProperty]
    private string _statusText = "Ready";

    public LauncherHomeViewModel LauncherHomeVm { get; }
    public PluginsManagerViewModel PluginsManagerVm { get; }
    public CommandPaletteViewModel CommandPaletteVm { get; }

    public ObservableCollection<NavigationRailItem> NavigationItems { get; } = new();
    public ObservableCollection<ActiveOverlayViewModel> ActiveOverlays { get; } = new();

    public MainViewModel(
        PluginHost host,
        IThemeService themeService,
        IPluginMarketplaceService marketplace)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));

        LauncherHomeVm = new LauncherHomeViewModel(_host, _marketplace, NavigateToPlugin);
        PluginsManagerVm = new PluginsManagerViewModel(_host, _marketplace);
        CommandPaletteVm = new CommandPaletteViewModel(_host.Context);
        CommandPaletteVm.RequestClose += () => IsCommandPaletteOpen = false;

        InitializeNavigation();
        OnDynamicNavigationItemsChanged();
        OnDynamicOverlaysChanged();

        // Listen for dynamic capability registrations from plugins
        _host.Context.NavigationItemsChanged += OnDynamicNavigationItemsChanged;
        _host.Context.OverlaysChanged += OnDynamicOverlaysChanged;
        _host.PluginStateChanged += (_, _) => UpdateMetrics();

        UpdateMetrics();
    }

    private void InitializeNavigation()
    {
        NavigationItems.Clear();

        // 1. Built-in Launcher Home
        var homeItem = new NavigationRailItem
        {
            Id = "nav.launcher",
            Title = "Suite Launcher",
            IconKind = "ViewGridOutline"
        };
        NavigationItems.Add(homeItem);

        // 2. Built-in Plugins Manager
        var pluginsItem = new NavigationRailItem
        {
            Id = "nav.plugins",
            Title = "Plugins & Store",
            IconKind = "ToyBrickOutline"
        };
        NavigationItems.Add(pluginsItem);

        SelectedNavItem = homeItem;
    }

    private void OnDynamicNavigationItemsChanged()
    {
        void Update()
        {
            var registered = _host.Context.GetRegisteredNavigationItems();
            var existingDynamic = NavigationItems.Where(n => n.Id.StartsWith("dyn.")).ToList();
            foreach (var item in existingDynamic) NavigationItems.Remove(item);

            foreach (var nav in registered)
            {
                NavigationItems.Add(new NavigationRailItem
                {
                    Id = $"dyn.{nav.Id}",
                    Title = nav.Title,
                    IconKind = nav.IconKind ?? "Application",
                    ViewFactory = nav.ViewFactory
                });
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    private void OnDynamicOverlaysChanged()
    {
        void Update()
        {
            var registered = _host.Context.GetRegisteredOverlays();
            foreach (var overlay in registered)
            {
                if (overlay.AutoOpenOnMount && !ActiveOverlays.Any(o => o.Descriptor.Id == overlay.Id))
                {
                    ActiveOverlays.Add(new ActiveOverlayViewModel(overlay));
                }
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }

    public async void NavigateToPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) return;

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => NavigateToPlugin(pluginId));
            return;
        }

        // 1. Check if already in NavigationItems
        var dynItem = NavigationItems.FirstOrDefault(n =>
            n.Id == $"dyn.{pluginId}" ||
            n.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase) ||
            n.Id.EndsWith(pluginId, StringComparison.OrdinalIgnoreCase));

        if (dynItem != null)
        {
            SelectedNavItem = dynItem;
            return;
        }

        // 2. Check if registered in context but not yet synced
        var registeredNav = _host.Context.GetRegisteredNavigationItems().FirstOrDefault(n =>
            n.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase) ||
            n.Id.EndsWith(pluginId, StringComparison.OrdinalIgnoreCase));

        if (registeredNav != null)
        {
            OnDynamicNavigationItemsChanged();
            dynItem = NavigationItems.FirstOrDefault(n =>
                n.Id == $"dyn.{registeredNav.Id}" ||
                n.Id.Equals(registeredNav.Id, StringComparison.OrdinalIgnoreCase));

            if (dynItem != null)
            {
                SelectedNavItem = dynItem;
                return;
            }
        }

        // 3. Check if plugin is registered in host but inactive
        var registeredPlugin = _host.GetRegisteredPlugins().FirstOrDefault(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

        if (registeredPlugin != null)
        {
            try
            {
                await _host.MountPluginAsync(registeredPlugin);
                OnDynamicNavigationItemsChanged();
                dynItem = NavigationItems.FirstOrDefault(n =>
                    n.Id == $"dyn.{registeredPlugin.Id}" ||
                    n.Id.Equals(registeredPlugin.Id, StringComparison.OrdinalIgnoreCase));

                if (dynItem != null)
                {
                    SelectedNavItem = dynItem;
                    return;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Instance.LogError("MainViewModel", $"Failed to mount plugin '{pluginId}' during launch", ex);
            }
        }

        // 4. Check if plugin is installed in plugins directory
        var pluginsDir = FryWorldPaths.PluginsDirectory;
        var pluginFolder = System.IO.Path.Combine(pluginsDir, pluginId);
        if (System.IO.Directory.Exists(pluginFolder))
        {
            try
            {
                var entryDll = FryWorld.Sdk.Loading.PluginAssemblyLoader.ResolvePluginDirectoryEntryDll(pluginFolder);
                if (entryDll != null)
                {
                    var package = FryWorld.Sdk.Loading.PluginAssemblyLoader.LoadAssembly(entryDll);
                    foreach (var p in package.Plugins)
                    {
                        try { _host.RegisterPlugin(p); } catch { }
                        await _host.MountPluginAsync(p);
                    }
                    OnDynamicNavigationItemsChanged();
                    dynItem = NavigationItems.FirstOrDefault(n =>
                        n.Id == $"dyn.{pluginId}" ||
                        n.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase) ||
                        n.Id.EndsWith(pluginId, StringComparison.OrdinalIgnoreCase));

                    if (dynItem != null)
                    {
                        SelectedNavItem = dynItem;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Instance.LogError("MainViewModel", $"Failed to load and launch plugin '{pluginId}' from disk", ex);
            }
        }

        // 5. Check if it's an overlay
        var overlay = _host.Context.GetRegisteredOverlays().FirstOrDefault(o => o.Id == pluginId);
        if (overlay != null)
        {
            OpenOverlay(overlay);
        }
    }

    public void OpenOverlay(OverlayDescriptor descriptor)
    {
        if (!ActiveOverlays.Any(o => o.Descriptor.Id == descriptor.Id))
        {
            ActiveOverlays.Add(new ActiveOverlayViewModel(descriptor));
        }
    }

    [RelayCommand]
    public void CloseOverlay(ActiveOverlayViewModel overlay)
    {
        ActiveOverlays.Remove(overlay);
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        CommandPaletteVm.RefreshList();
        IsCommandPaletteOpen = true;
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }

    public void SelectNavigationItem(NavigationRailItem item)
    {
        SelectedNavItem = item;
    }

    partial void OnSelectedNavItemChanged(NavigationRailItem? value)
    {
        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        if (value == null) return;

        IsLauncherActive = value.Id == "nav.launcher";
        IsPluginsActive = value.Id == "nav.plugins";

        if (value.ViewFactory != null)
        {
            try
            {
                if (!_viewCache.TryGetValue(value.Id, out var cachedView))
                {
                    cachedView = value.ViewFactory();
                    _viewCache[value.Id] = cachedView;
                }
                CurrentView = cachedView;
            }
            catch (Exception ex)
            {
                AppLogService.Instance.LogError("MainViewModel", $"Failed to instantiate view for navigation item '{value.Id}'", ex);
                CurrentView = null;
            }
        }
        else
        {
            CurrentView = null;
        }
    }

    private void UpdateMetrics()
    {
        void Update()
        {
            LoadedPluginCount = _host.GetRegisteredPlugins().Count;
            var bytes = GC.GetTotalMemory(false);
            MemoryUsageText = $"{bytes / (1024 * 1024)} MB";
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
        }
    }
}
