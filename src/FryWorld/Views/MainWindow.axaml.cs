using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FryWorld.ViewModels;

namespace FryWorld.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        if (App.Services?.GetService(typeof(FryWorld.Sdk.Theming.IThemeManager)) is FryWorld.Sdk.Theming.IThemeManager themeManager)
        {
            themeManager.TrackResourceRoot(this);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
        {
            _vm.NavigationItemsUpdated -= RebuildWorkspacesMenu;
        }

        _vm = DataContext as MainViewModel;
        if (_vm != null)
        {
            _vm.NavigationItemsUpdated += RebuildWorkspacesMenu;
            RebuildWorkspacesMenu();
        }
    }

    private void RebuildWorkspacesMenu()
    {
        if (_vm == null) return;

        var rootMenu = NativeMenu.GetMenu(this);
        if (rootMenu == null) return;

        var workspacesItem = rootMenu.Items.OfType<NativeMenuItem>().FirstOrDefault(m => m.Header == "Workspaces");
        if (workspacesItem?.Menu == null) return;

        var menu = workspacesItem.Menu;
        menu.Items.Clear();

        menu.Items.Add(new NativeMenuItem("Suite Launcher")
        {
            Gesture = new KeyGesture(Key.D1, KeyModifiers.Meta),
            Command = _vm.OpenLauncherCommand
        });

        menu.Items.Add(new NativeMenuItem("Plugins & Extensions")
        {
            Gesture = new KeyGesture(Key.D2, KeyModifiers.Meta),
            Command = _vm.OpenPluginsManagerCommand
        });

        var dynamicItems = _vm.NavigationItems.Where(n => n.Id.StartsWith("dyn.")).ToList();
        if (dynamicItems.Count > 0)
        {
            menu.Items.Add(new NativeMenuItemSeparator());
            int shortcutNum = 3;
            foreach (var nav in dynamicItems)
            {
                var dynamicMenuItem = new NativeMenuItem(nav.Title)
                {
                    Command = new RelayCommand(() => _vm.SelectNavigationItem(nav))
                };

                if (shortcutNum <= 9)
                {
                    dynamicMenuItem.Gesture = new KeyGesture((Key)(Key.D0 + shortcutNum), KeyModifiers.Meta);
                }

                menu.Items.Add(dynamicMenuItem);
                shortcutNum++;
            }
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Settings & Appearance")
        {
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta),
            Command = _vm.OpenSettingsCommand
        });
        menu.Items.Add(new NativeMenuItem("Toggle Left Navigation Rail")
        {
            Gesture = new KeyGesture(Key.B, KeyModifiers.Meta),
            Command = _vm.ToggleNavRailCommand
        });
    }
}

