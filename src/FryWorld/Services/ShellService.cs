using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FryWorld.Services;

/// <summary>
/// The FryWorld shell's <see cref="IShellService"/>: pages are workspaces in the navigation rail, overlays float
/// over the canvas, dialogs are modal windows over the main window.
/// </summary>
public sealed class ShellService : IShellService
{
    private readonly IServiceProvider _services;
    private readonly PluginHost _host;

    public ShellService(IServiceProvider services, PluginHost host)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    // Resolved on use: the view model is created after the plugin host, and plugins may ask for the shell first.
    private MainViewModel Shell => _services.GetRequiredService<MainViewModel>();

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public bool NavigateTo(string navigationItemId)
    {
        var page = _host.Context.GetRegisteredNavigationItems()
            .FirstOrDefault(n => string.Equals(n.Id, navigationItemId, StringComparison.OrdinalIgnoreCase));
        if (page == null) return false;

        OnUiThread(() => Shell.NavigateToPlugin(page.Id));
        return true;
    }

    public bool ShowOverlay(string overlayId)
    {
        var overlay = _host.Context.GetRegisteredOverlays()
            .FirstOrDefault(o => string.Equals(o.Id, overlayId, StringComparison.OrdinalIgnoreCase));
        if (overlay == null) return false;

        OnUiThread(() => Shell.OpenOverlay(overlay));
        return true;
    }

    public bool HideOverlay(string overlayId)
    {
        if (!_host.Context.GetRegisteredOverlays().Any(o => string.Equals(o.Id, overlayId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        OnUiThread(() =>
        {
            var open = Shell.ActiveOverlays.FirstOrDefault(o => string.Equals(o.Descriptor.Id, overlayId, StringComparison.OrdinalIgnoreCase));
            if (open != null) Shell.CloseOverlay(open);
        });
        return true;
    }

    public bool ToggleOverlay(string overlayId) =>
        IsOverlayVisible(overlayId) ? HideOverlay(overlayId) : ShowOverlay(overlayId);

    public bool IsOverlayVisible(string overlayId)
    {
        bool Check() => Shell.ActiveOverlays.Any(o =>
            o.IsVisible && string.Equals(o.Descriptor.Id, overlayId, StringComparison.OrdinalIgnoreCase));

        return Dispatcher.UIThread.CheckAccess() ? Check() : Dispatcher.UIThread.Invoke(Check);
    }

    public async Task<bool> ShowDialogAsync(string dialogId)
    {
        var dialog = _host.Context.GetRegisteredDialogs()
            .FirstOrDefault(d => string.Equals(d.Id, dialogId, StringComparison.OrdinalIgnoreCase));
        if (dialog == null) return false;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var window = new Window
            {
                Title = dialog.Title,
                Width = dialog.DefaultWidth,
                Height = dialog.DefaultHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = dialog.ViewFactory()
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            {
                await window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        });
        return true;
    }
}

/// <summary>
/// Posts to Avalonia's UI thread, so the plugin host raises registry change events where the shell's bound
/// collections live.
/// </summary>
public sealed class UiThreadSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => Dispatcher.UIThread.Post(() => d(state));

    public override void Send(SendOrPostCallback d, object? state) => Dispatcher.UIThread.Invoke(() => d(state));

    public override SynchronizationContext CreateCopy() => this;
}
