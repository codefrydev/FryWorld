using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Theming;
using FryWorld.Services;

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
    private readonly Action<ActiveOverlayViewModel>? _onClose;

    public OverlayDescriptor Descriptor { get; }
    public Control View { get; }

    public string Id => Descriptor.Id;
    public string Title => Descriptor.Title;
    public string? IconKind => Descriptor.IconKind ?? "WindowMaximize";

    [ObservableProperty]
    private double _x = 60;

    [ObservableProperty]
    private double _y = 60;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveHeight))]
    private double _height;

    [ObservableProperty]
    private int _zIndex = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveHeight))]
    [NotifyPropertyChangedFor(nameof(EffectiveMinHeight))]
    [NotifyPropertyChangedFor(nameof(CanResize))]
    private bool _isMinimized;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isVisible = true;

    public bool IsResizable => Descriptor.IsResizable;
    public bool CanResize => IsResizable && !IsMinimized;
    public double EffectiveHeight => IsMinimized ? 42 : Height;
    public double EffectiveMinHeight => IsMinimized ? 42 : Descriptor.MinHeight;

    public bool HasCustomChrome => Descriptor.HasCustomChrome;
    public bool HasStandardChrome => !HasCustomChrome;

    public ActiveOverlayViewModel(OverlayDescriptor descriptor, Action<ActiveOverlayViewModel>? onClose = null)
    {
        Descriptor = descriptor;
        View = descriptor.ViewFactory();
        Width = descriptor.DefaultWidth;
        Height = descriptor.DefaultHeight;
        _onClose = onClose;
    }

    public void BringToFront(IEnumerable<ActiveOverlayViewModel> allOverlays)
    {
        int maxZ = 0;
        foreach (var ov in allOverlays)
        {
            if (ov.ZIndex > maxZ)
                maxZ = ov.ZIndex;
        }
        ZIndex = maxZ + 1;
    }

    [RelayCommand]
    public void ToggleMinimize()
    {
        IsMinimized = !IsMinimized;
    }

    [RelayCommand]
    public void Close()
    {
        _onClose?.Invoke(this);
    }
}

public partial class MainViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IThemeService _themeService;
    private readonly IPluginMarketplaceService _marketplace;
    private readonly PluginStartup _startup;
    private readonly Dictionary<string, Control> _viewCache = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private NavigationRailItem? _selectedNavItem;

    [ObservableProperty]
    private Control? _currentView;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private bool _isNavRailVisible = true;

    [ObservableProperty]
    private bool _isStatusBarVisible = true;

    [ObservableProperty]
    private bool _isLauncherActive = true;

    [ObservableProperty]
    private bool _isPluginsActive;

    [ObservableProperty]
    private bool _isSettingsActive;

    [ObservableProperty]
    private int _loadedPluginCount;

    [ObservableProperty]
    private string _memoryUsageText = "12 MB";

    [ObservableProperty]
    private string _statusText = "Ready";

    public event Action? NavigationItemsUpdated;

    public LauncherHomeViewModel LauncherHomeVm { get; }
    public PluginsManagerViewModel PluginsManagerVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public CommandPaletteViewModel CommandPaletteVm { get; }

    public ObservableCollection<NavigationRailItem> NavigationItems { get; } = new();
    public ObservableCollection<ActiveOverlayViewModel> ActiveOverlays { get; } = new();

    public MainViewModel(
        PluginHost host,
        IThemeService themeService,
        IPluginMarketplaceService marketplace,
        PluginStartup startup)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));

        LauncherHomeVm = new LauncherHomeViewModel(_host, _marketplace, NavigateToPlugin);
        PluginsManagerVm = new PluginsManagerViewModel(_host, _marketplace);
        SettingsVm = new SettingsViewModel((IThemeManager)_themeService, _host);
        CommandPaletteVm = new CommandPaletteViewModel(_host.Context);
        CommandPaletteVm.RequestClose += () => IsCommandPaletteOpen = false;

        InitializeNavigation();
        OnDynamicNavigationItemsChanged();
        OnDynamicOverlaysChanged();

        // Listen for dynamic capability registrations from plugins
        _host.Context.NavigationItemsChanged += OnDynamicNavigationItemsChanged;
        _host.Context.OverlaysChanged += OnDynamicOverlaysChanged;
        _host.PluginStateChanged += (plugin, state) =>
        {
            UpdateMetrics();
            if (state == PluginLifecycleState.Unloaded || state == PluginLifecycleState.Failed)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    OnDynamicNavigationItemsChanged();
                    var toClose = ActiveOverlays.Where(o => string.Equals(o.Descriptor.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var ov in toClose)
                    {
                        ActiveOverlays.Remove(ov);
                    }
                });
            }
        };

        RegisterBuiltInCommands();
        UpdateMetrics();
    }

    private void RegisterBuiltInCommands()
    {
        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.toggleNavRail",
            Title = "Toggle Left Navigation Rail",
            Subtitle = "Show or hide the left navigation rail",
            Category = "View",
            IconKind = "DockLeft",
            ShortcutKey = "⌘B",
            Command = ToggleNavRailCommand
        });

        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.toggleStatusBar",
            Title = "Toggle Bottom Status Bar",
            Subtitle = "Show or hide the bottom status and diagnostic bar",
            Category = "View",
            IconKind = "DockBottom",
            ShortcutKey = "⌘J",
            Command = ToggleStatusBarCommand
        });

        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.openLauncher",
            Title = "Open Suite Launcher",
            Subtitle = "Switch to FryWorld Launcher Home",
            Category = "Navigation",
            IconKind = "ViewGridOutline",
            ShortcutKey = "⌘1",
            Command = OpenLauncherCommand
        });

        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.openPlugins",
            Title = "Open Plugins & Extensions",
            Subtitle = "Browse and manage installed plugins",
            Category = "Navigation",
            IconKind = "ToyBrickOutline",
            ShortcutKey = "⌘2",
            Command = OpenPluginsManagerCommand
        });

        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.toggleTheme",
            Title = "Toggle Theme",
            Subtitle = "Switch between Dark and Light theme",
            Category = "View",
            IconKind = "ThemeLightDark",
            ShortcutKey = "⌘T",
            Command = ToggleThemeCommand
        });

        _host.Context.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "core.openSettings",
            Title = "Open Settings & Appearance",
            Subtitle = "Customize theme presets, procedural color harmonies, and diagnostics",
            Category = "Preferences",
            IconKind = "CogOutline",
            ShortcutKey = "⌘,",
            Command = OpenSettingsCommand
        });

        if (_themeService is IThemeManager tm)
        {
            foreach (var theme in tm.GetAvailableThemes())
            {
                var themeId = theme.Id;
                _host.Context.RegisterCommand(new CommandPaletteDescriptor
                {
                    Id = $"core.theme.{themeId}",
                    Title = $"Theme: {theme.Name}",
                    Subtitle = theme.Description,
                    Category = "Themes",
                    IconKind = "PaletteOutline",
                    Command = new RelayCommand(() => ApplyTheme(themeId))
                });
            }

            _host.Context.RegisterCommand(new CommandPaletteDescriptor
            {
                Id = "core.theme.randomHarmonic",
                Title = "Theme: Random Color Harmony",
                Subtitle = "Procedurally generate a balanced ergonomic color harmony",
                Category = "Themes",
                IconKind = "DiceMultipleOutline",
                Command = new RelayCommand(() => ApplyRandomHarmonic())
            });
        }
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

        // 3. Built-in Settings & Appearance
        var settingsItem = new NavigationRailItem
        {
            Id = "nav.settings",
            Title = "Settings & Appearance",
            IconKind = "CogOutline"
        };
        NavigationItems.Add(settingsItem);

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

            NavigationItemsUpdated?.Invoke();
        }


        // The plugin host raises registry events on the UI thread (PluginHostOptions.EventSynchronizationContext),
        // and the shell's other callers are on it too.
        Update();
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
                    int offsetIndex = ActiveOverlays.Count % 6;
                    var vm = new ActiveOverlayViewModel(overlay, CloseOverlay)
                    {
                        X = 80 + (offsetIndex * 36),
                        Y = 60 + (offsetIndex * 32),
                        ZIndex = ActiveOverlays.Count + 1
                    };
                    ActiveOverlays.Add(vm);
                }
            }
        }


        // The plugin host raises registry events on the UI thread (PluginHostOptions.EventSynchronizationContext),
        // and the shell's other callers are on it too.
        Update();
    }

    public async void NavigateToPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) return;

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => NavigateToPlugin(pluginId));
            return;
        }

        // Installed plugins load in the background at startup. Wait for that rather than loading a second
        // copy of the plugin from disk below.
        if (!_startup.Completion.IsCompleted)
        {
            await _startup.Completion;
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
        var existing = ActiveOverlays.FirstOrDefault(o => o.Descriptor.Id == descriptor.Id);
        if (existing != null)
        {
            existing.IsVisible = true;
            existing.IsMinimized = false;
            existing.BringToFront(ActiveOverlays);
            return;
        }

        int offsetIndex = ActiveOverlays.Count % 6;
        var vm = new ActiveOverlayViewModel(descriptor, CloseOverlay)
        {
            X = 80 + (offsetIndex * 36),
            Y = 60 + (offsetIndex * 32),
            ZIndex = ActiveOverlays.Count + 1
        };
        ActiveOverlays.Add(vm);
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

    [RelayCommand]
    public void ApplyTheme(string? themeId)
    {
        if (!string.IsNullOrWhiteSpace(themeId) && _themeService is IThemeManager tm)
        {
            tm.ApplyTheme(themeId);
        }
    }

    [RelayCommand]
    public void ApplyRandomHarmonic()
    {
        if (_themeService is IThemeManager tm)
        {
            tm.ApplyRandomHarmonicTheme(_themeService.CurrentTheme == AppTheme.Dark);
        }
    }

    public IReadOnlyList<ThemeDefinition> AvailableThemes =>
        _themeService is IThemeManager tm ? tm.GetAvailableThemes() : Array.Empty<ThemeDefinition>();

    [RelayCommand]
    public void ToggleNavRail()
    {
        IsNavRailVisible = !IsNavRailVisible;
        IsStatusBarVisible = IsNavRailVisible;
    }

    [RelayCommand]
    public void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    [RelayCommand]
    public void OpenLauncher()
    {
        var item = NavigationItems.FirstOrDefault(n => n.Id == "nav.launcher") ?? (NavigationItems.Count > 0 ? NavigationItems[0] : null);
        if (item != null) SelectNavigationItem(item);
    }

    [RelayCommand]
    public void OpenPluginsManager()
    {
        var item = NavigationItems.FirstOrDefault(n => n.Id == "nav.plugins") ?? (NavigationItems.Count > 1 ? NavigationItems[1] : null);
        if (item != null) SelectNavigationItem(item);
    }

    [RelayCommand]
    public void OpenSettings()
    {
        var item = NavigationItems.FirstOrDefault(n => n.Id == "nav.settings") ?? (NavigationItems.Count > 2 ? NavigationItems[2] : null);
        if (item != null) SelectNavigationItem(item);
        else IsSettingsActive = true;
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
        IsSettingsActive = value.Id == "nav.settings";

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
