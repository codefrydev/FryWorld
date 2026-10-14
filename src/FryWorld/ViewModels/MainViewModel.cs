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

    private void OnDynamicOverlaysChanged()
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

    public void NavigateToPlugin(string pluginId)
    {
        var dynItem = NavigationItems.FirstOrDefault(n => n.Id == $"dyn.{pluginId}");
        if (dynItem != null)
        {
            SelectedNavItem = dynItem;
            return;
        }

        // Check if it's an overlay
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
            if (!_viewCache.TryGetValue(value.Id, out var cachedView))
            {
                cachedView = value.ViewFactory();
                _viewCache[value.Id] = cachedView;
            }
            CurrentView = cachedView;
        }
        else
        {
            CurrentView = null;
        }
    }

    private void UpdateMetrics()
    {
        LoadedPluginCount = _host.GetRegisteredPlugins().Count;
        var bytes = GC.GetTotalMemory(false);
        MemoryUsageText = $"{bytes / (1024 * 1024)} MB";
    }
}
