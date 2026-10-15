using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Runner;

/// <summary>
/// The standalone runner's main window. Shows the plugin's pages as tabs; a plugin with no pages but an
/// overlay (a game, a widget) gets its first overlay as the window's content. Other overlays open as their
/// own windows from the toolbar, commands run from the Commands menu, and status bar widgets sit along the bottom.
/// </summary>
public sealed class FryRunnerWindow : Window
{
    private readonly IFryPlugin _plugin;
    private readonly FryPluginContext _context;
    private readonly FryRunnerOptions _options;
    private readonly ContentControl _body = new();
    private readonly StackPanel _toolbar = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly StackPanel _statusLeft = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private readonly StackPanel _statusRight = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private readonly TextBlock _statusMessage = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 };
    private readonly Dictionary<string, Window> _overlayWindows = new(StringComparer.OrdinalIgnoreCase);
    private TabControl? _tabs;
    private OverlayDescriptor? _mainOverlay;
    private string? _pendingNavigation;
    private bool _isMounted;
    private DispatcherTimer? _messageTimer;

    internal FryRunnerWindow(IFryPlugin plugin, FryPluginContext context, FryRunnerOptions options)
    {
        _plugin = plugin;
        _context = context;
        _options = options;

        Shell = new RunnerShellService(this, context);
        Notifications = new RunnerNotificationService(this);

        Title = options.Title ?? $"{plugin.Name} — FryWorld Runner";
        Width = options.Width ?? 1100;
        Height = options.Height ?? 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _body.Content = Centered(new TextBlock { Text = $"Loading {plugin.Name}…" });

        var toolbar = new Border
        {
            Padding = new Thickness(12, 8),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            Child = new DockPanel
            {
                Children =
                {
                    Docked(new TextBlock
                    {
                        Text = $"{plugin.Name}  ·  {plugin.Id} {plugin.Version}",
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 16, 0)
                    }, Dock.Left),
                    _toolbar
                }
            }
        };

        var statusBar = new Border
        {
            Padding = new Thickness(12, 4),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
            Child = new DockPanel
            {
                Children =
                {
                    Docked(_statusRight, Dock.Right),
                    Docked(_statusLeft, Dock.Left),
                    _statusMessage
                }
            }
        };

        Content = new DockPanel
        {
            Children =
            {
                Docked(toolbar, Dock.Top),
                Docked(statusBar, Dock.Bottom),
                _body
            }
        };

        Closed += (_, _) =>
        {
            foreach (var overlay in _overlayWindows.Values.ToList()) overlay.Close();
        };
    }

    internal RunnerShellService Shell { get; }

    internal RunnerNotificationService Notifications { get; }

    /// <summary>The page (navigation item) id currently shown, if the plugin has pages.</summary>
    public string? SelectedNavigationItemId => (_tabs?.SelectedItem as TabItem)?.Tag as string;

    /// <summary>The overlay shown as the window's content, for plugins with no pages.</summary>
    public string? MainOverlayId => _mainOverlay?.Id;

    /// <summary>Ids of overlays open in their own windows.</summary>
    public IReadOnlyCollection<string> OpenOverlayWindowIds => _overlayWindows.Keys.ToList();

    /// <summary>Called once the plugin has mounted, or failed to.</summary>
    internal void ShowMounted(Exception? failure, IReadOnlyList<PluginLoadDiagnostic> diagnostics)
    {
        if (failure != null)
        {
            _body.Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(24),
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = $"{_plugin.Name} failed to mount", FontSize = 18, FontWeight = FontWeight.SemiBold },
                        new SelectableTextBlock { Text = failure.ToString(), TextWrapping = TextWrapping.Wrap },
                        new SelectableTextBlock
                        {
                            Text = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString())),
                            TextWrapping = TextWrapping.Wrap,
                            Opacity = 0.7
                        }
                    }
                }
            };
            return;
        }

        _isMounted = true;
        Rebuild();

        foreach (var overlay in _context.GetRegisteredOverlays().Where(o => o.AutoOpenOnMount && o != _mainOverlay))
        {
            OpenOverlay(overlay);
        }

        if (_pendingNavigation != null) SelectNavigationItem(_pendingNavigation);

        // Keep up with registrations made after mount (plugins may add pages or commands later).
        _context.NavigationItemsChanged += () => Dispatcher.UIThread.Post(Rebuild);
        _context.OverlaysChanged += () => Dispatcher.UIThread.Post(Rebuild);
        _context.CommandsChanged += () => Dispatcher.UIThread.Post(BuildToolbar);
        _context.StatusBarWidgetsChanged += () => Dispatcher.UIThread.Post(BuildStatusBar);
    }

    private void Rebuild()
    {
        BuildBody();
        BuildToolbar();
        BuildStatusBar();
    }

    private void BuildBody()
    {
        var pages = _context.GetRegisteredNavigationItems();
        var overlays = _context.GetRegisteredOverlays();

        if (pages.Count > 0)
        {
            _mainOverlay = null;
            var selected = SelectedNavigationItemId;
            _tabs = new TabControl { Padding = new Thickness(0) };
            foreach (var page in pages)
            {
                var tab = new TabItem { Header = page.Title, Tag = page.Id };
                if (!string.IsNullOrEmpty(page.Tooltip)) ToolTip.SetTip(tab, page.Tooltip);
                _tabs.Items.Add(tab);
            }

            _tabs.SelectionChanged += (_, _) => LoadSelectedTab();
            _body.Content = _tabs;
            SelectNavigationItem(selected ?? pages[0].Id);
            return;
        }

        if (overlays.Count > 0)
        {
            _tabs = null;
            _mainOverlay = overlays[0];
            var view = CreateView(_mainOverlay.Title, _mainOverlay.ViewFactory, _mainOverlay.ViewModelFactory);
            _body.Content = view;

            // Games and other keyboard-driven overlays need focus to receive keys.
            Dispatcher.UIThread.Post(() => view.Focus(), DispatcherPriority.Loaded);

            if (_options.Width == null && _options.Height == null)
            {
                Width = Math.Max(_mainOverlay.DefaultWidth, _mainOverlay.MinWidth) + 48;
                Height = Math.Max(_mainOverlay.DefaultHeight, _mainOverlay.MinHeight) + 110;
            }
            return;
        }

        _tabs = null;
        _mainOverlay = null;
        _body.Content = Centered(new TextBlock
        {
            Text = "This plugin registers no pages or overlays. Its commands are in the Commands menu.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Opacity = 0.7
        });
    }

    private void LoadSelectedTab()
    {
        if (_tabs?.SelectedItem is not TabItem { Content: null, Tag: string id } tab) return;
        var page = _context.GetRegisteredNavigationItems().FirstOrDefault(p => p.Id == id);
        if (page != null) tab.Content = CreateView(page.Title, page.ViewFactory, page.ViewModelFactory);
    }

    private void BuildToolbar()
    {
        _toolbar.Children.Clear();

        var commands = _context.GetRegisteredCommands();
        if (commands.Count > 0)
        {
            var menu = new MenuFlyout();
            foreach (var command in commands)
            {
                var item = new MenuItem
                {
                    Header = string.IsNullOrEmpty(command.ShortcutKey) ? command.Title : $"{command.Title}    {command.ShortcutKey}",
                };
                item.Click += async (_, _) =>
                {
                    try
                    {
                        if (command.AsyncAction != null) await command.AsyncAction();
                        else if (command.Command?.CanExecute(null) == true) command.Command.Execute(null);
                    }
                    catch (Exception ex)
                    {
                        Notifications.ShowError(ex.Message, command.Title);
                    }
                };
                menu.Items.Add(item);
            }
            _toolbar.Children.Add(new Button { Content = $"Commands ({commands.Count})", Flyout = menu });
        }

        foreach (var overlay in _context.GetRegisteredOverlays().Where(o => o != _mainOverlay))
        {
            var button = new Button { Content = overlay.Title };
            button.Click += (_, _) => ToggleOverlay(overlay);
            _toolbar.Children.Add(button);
        }

        var dialogs = _context.GetRegisteredDialogs();
        foreach (var dialog in dialogs)
        {
            var button = new Button { Content = dialog.Title };
            button.Click += async (_, _) => await Shell.ShowDialogAsync(dialog.Id);
            _toolbar.Children.Add(button);
        }
    }

    private void BuildStatusBar()
    {
        _statusLeft.Children.Clear();
        _statusRight.Children.Clear();

        foreach (var widget in _context.GetRegisteredStatusBarWidgets())
        {
            Control control;
            try
            {
                control = widget.CustomControlFactory?.Invoke()
                    ?? (widget.ClickCommand != null
                        ? new Button { Content = widget.Label ?? widget.Id, Command = widget.ClickCommand, Padding = new Thickness(8, 2) }
                        : new TextBlock { Text = widget.Label ?? widget.Id, VerticalAlignment = VerticalAlignment.Center });
            }
            catch (Exception ex)
            {
                control = new TextBlock { Text = $"{widget.Id}: {ex.Message}", Foreground = Brushes.IndianRed };
            }

            if (!string.IsNullOrEmpty(widget.Tooltip)) ToolTip.SetTip(control, widget.Tooltip);
            (widget.AlignRight ? _statusRight : _statusLeft).Children.Add(control);
        }
    }

    internal bool SelectNavigationItem(string id)
    {
        if (!_isMounted)
        {
            _pendingNavigation = id;
            return true;
        }

        var tab = _tabs?.Items.OfType<TabItem>().FirstOrDefault(t => string.Equals(t.Tag as string, id, StringComparison.OrdinalIgnoreCase));
        if (tab == null) return false;

        _tabs!.SelectedItem = tab;
        LoadSelectedTab();
        Activate();
        return true;
    }

    internal bool IsOverlayOpen(string id) =>
        _overlayWindows.ContainsKey(id) || string.Equals(_mainOverlay?.Id, id, StringComparison.OrdinalIgnoreCase);

    internal void OpenOverlay(OverlayDescriptor overlay)
    {
        if (string.Equals(_mainOverlay?.Id, overlay.Id, StringComparison.OrdinalIgnoreCase))
        {
            Activate();
            return;
        }

        if (_overlayWindows.TryGetValue(overlay.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = new Window
        {
            Title = overlay.Title,
            Width = overlay.DefaultWidth,
            Height = overlay.DefaultHeight,
            MinWidth = overlay.MinWidth,
            MinHeight = overlay.MinHeight,
            CanResize = overlay.IsResizable,
            Content = CreateView(overlay.Title, overlay.ViewFactory, overlay.ViewModelFactory)
        };
        window.Closed += (_, _) => _overlayWindows.Remove(overlay.Id);
        _overlayWindows[overlay.Id] = window;
        window.Show(this);
    }

    internal void CloseOverlay(string id)
    {
        if (_overlayWindows.TryGetValue(id, out var window)) window.Close();
    }

    internal void ToggleOverlay(OverlayDescriptor overlay)
    {
        if (_overlayWindows.ContainsKey(overlay.Id)) CloseOverlay(overlay.Id);
        else OpenOverlay(overlay);
    }

    internal System.Threading.Tasks.Task ShowDialogWindowAsync(DialogDescriptor dialog)
    {
        var window = new Window
        {
            Title = dialog.Title,
            Width = dialog.DefaultWidth,
            Height = dialog.DefaultHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = CreateView(dialog.Title, dialog.ViewFactory, null)
        };
        return window.ShowDialog(this);
    }

    internal void ShowMessage(string text, bool isError)
    {
        _statusMessage.Text = text;
        _statusMessage.Foreground = isError ? Brushes.IndianRed : null;

        _messageTimer?.Stop();
        _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _messageTimer.Tick += (_, _) =>
        {
            _statusMessage.Text = string.Empty;
            _messageTimer?.Stop();
        };
        _messageTimer.Start();
    }

    /// <summary>Creates a plugin view, showing the error in its place if the factory throws.</summary>
    private static Control CreateView(string title, Func<Control> viewFactory, Func<object?>? viewModelFactory)
    {
        try
        {
            var view = viewFactory();
            if (viewModelFactory != null && view.DataContext == null)
            {
                view.DataContext = viewModelFactory();
            }
            return view;
        }
        catch (Exception ex)
        {
            return Centered(new SelectableTextBlock
            {
                Text = $"'{title}' failed to create its view:{Environment.NewLine}{ex}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.IndianRed,
                Margin = new Thickness(24)
            });
        }
    }

    private static Control Centered(Control child)
    {
        child.HorizontalAlignment = HorizontalAlignment.Center;
        child.VerticalAlignment = VerticalAlignment.Center;
        return child;
    }

    private static Control Docked(Control child, Dock dock)
    {
        DockPanel.SetDock(child, dock);
        return child;
    }
}
