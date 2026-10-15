using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;

namespace FryWorld.Sdk.Runner;

/// <summary>
/// <see cref="IShellService"/> for the standalone runner: pages are tabs, overlays are windows.
/// </summary>
internal sealed class RunnerShellService : IShellService
{
    private readonly FryRunnerWindow _window;
    private readonly IFryPluginContext _context;

    public RunnerShellService(FryRunnerWindow window, IFryPluginContext context)
    {
        _window = window;
        _context = context;
    }

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public bool NavigateTo(string navigationItemId)
    {
        if (!_context.GetRegisteredNavigationItems().Any(n => string.Equals(n.Id, navigationItemId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        OnUiThread(() => _window.SelectNavigationItem(navigationItemId));
        return true;
    }

    public bool ShowOverlay(string overlayId)
    {
        var overlay = _context.GetRegisteredOverlays().FirstOrDefault(o => string.Equals(o.Id, overlayId, StringComparison.OrdinalIgnoreCase));
        if (overlay == null) return false;

        OnUiThread(() => _window.OpenOverlay(overlay));
        return true;
    }

    public bool HideOverlay(string overlayId)
    {
        if (!_context.GetRegisteredOverlays().Any(o => string.Equals(o.Id, overlayId, StringComparison.OrdinalIgnoreCase))) return false;

        OnUiThread(() => _window.CloseOverlay(overlayId));
        return true;
    }

    public bool ToggleOverlay(string overlayId)
    {
        var overlay = _context.GetRegisteredOverlays().FirstOrDefault(o => string.Equals(o.Id, overlayId, StringComparison.OrdinalIgnoreCase));
        if (overlay == null) return false;

        OnUiThread(() => _window.ToggleOverlay(overlay));
        return true;
    }

    public bool IsOverlayVisible(string overlayId) => _window.IsOverlayOpen(overlayId);

    public async Task<bool> ShowDialogAsync(string dialogId)
    {
        var dialog = _context.GetRegisteredDialogs().FirstOrDefault(d => string.Equals(d.Id, dialogId, StringComparison.OrdinalIgnoreCase));
        if (dialog == null) return false;

        await Dispatcher.UIThread.InvokeAsync(() => _window.ShowDialogWindowAsync(dialog));
        return true;
    }
}

/// <summary>
/// <see cref="INotificationService"/> for the standalone runner: messages appear in the status bar.
/// </summary>
internal sealed class RunnerNotificationService : INotificationService
{
    private readonly FryRunnerWindow _window;

    public RunnerNotificationService(FryRunnerWindow window) => _window = window;

    public void ShowToast(string message, string title = "Information", string? iconKind = null) =>
        Dispatcher.UIThread.Post(() => _window.ShowMessage($"{title}: {message}", isError: false));

    public void ShowError(string message, string title = "Error") =>
        Dispatcher.UIThread.Post(() => _window.ShowMessage($"{title}: {message}", isError: true));
}

/// <summary>
/// <see cref="IThemeService"/> for the standalone runner, switching the app's light/dark theme variant.
/// </summary>
internal sealed class RunnerThemeService : IThemeService
{
    private readonly Application _app;

    public RunnerThemeService(Application app) => _app = app;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public event Action<AppTheme>? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        Dispatcher.UIThread.Post(() =>
        {
            _app.RequestedThemeVariant = theme switch
            {
                AppTheme.Dark => ThemeVariant.Dark,
                AppTheme.Light => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
        });
        ThemeChanged?.Invoke(theme);
    }

    public void ToggleTheme()
    {
        var isDark = CurrentTheme == AppTheme.Dark ||
                     (CurrentTheme == AppTheme.System && _app.ActualThemeVariant == ThemeVariant.Dark);
        SetTheme(isDark ? AppTheme.Light : AppTheme.Dark);
    }
}
