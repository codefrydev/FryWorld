using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;

namespace FryWorld.Sdk.Runner;

/// <summary>
/// The Avalonia application behind <see cref="FryRunner"/>: mounts one plugin into a <see cref="PluginHost"/>
/// and shows it in a <see cref="FryRunnerWindow"/>.
/// </summary>
public class FryRunnerApp : Application
{
    private readonly Func<IFryPlugin> _pluginFactory;
    private readonly Action<Application>? _configureApp;
    private FileSettingsStore? _settings;

    /// <summary>Creates the app; <see cref="FryRunner.Configure{TPlugin}"/> is the usual way in.</summary>
    public FryRunnerApp(Func<IFryPlugin> pluginFactory, Action<Application>? configureApp = null, FryRunnerOptions? options = null)
    {
        _pluginFactory = pluginFactory ?? throw new ArgumentNullException(nameof(pluginFactory));
        _configureApp = configureApp;
        Options = options ?? new FryRunnerOptions();
    }

    /// <summary>The runner's options.</summary>
    public FryRunnerOptions Options { get; }

    /// <summary>The host the plugin is mounted in; available once <see cref="CreateMainWindow"/> has run.</summary>
    public PluginHost? Host { get; private set; }

    /// <summary>The plugin being run; available once <see cref="CreateMainWindow"/> has run.</summary>
    public IFryPlugin? Plugin { get; private set; }

    /// <summary>Completes when the plugin has mounted (or failed to); available once <see cref="CreateMainWindow"/> has run.</summary>
    public Task? MountCompletion { get; private set; }

    /// <inheritdoc />
    public override void Initialize()
    {
        Name = "FryWorld Runner";
        _configureApp?.Invoke(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
            desktop.Exit += (_, _) => Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Creates the plugin and its host, starts mounting it, and returns the window that shows it. The window
    /// fills in once mounting completes, so a plugin that awaits the UI thread in <c>ApplyAsync</c> cannot deadlock.
    /// </summary>
    public FryRunnerWindow CreateMainWindow()
    {
        if (Host != null) throw new InvalidOperationException("The runner's main window has already been created.");

        var plugin = _pluginFactory();
        Plugin = plugin;

        var context = new FryPluginContext();
        Host = new PluginHost(context);

        var settingsPath = Options.SettingsFilePath
            ?? Path.Combine(AppContext.BaseDirectory, $"{plugin.Id}.runner.settings.json");
        _settings = new FileSettingsStore(settingsPath);

        var window = new FryRunnerWindow(plugin, context, Options);
        context.RegisterService<IShellService>(window.Shell);
        context.RegisterService<INotificationService>(window.Notifications);
        context.RegisterService<ISettingsManager>(_settings);
        context.RegisterService<IThemeService>(new RunnerThemeService(this));
        Options.ConfigureServices?.Invoke(context);

        MountCompletion = MountAndShowAsync(plugin, window);
        return window;
    }

    private async Task MountAndShowAsync(IFryPlugin plugin, FryRunnerWindow window)
    {
        Host!.RegisterPlugin(plugin);
        await Host.MountPluginAsync(plugin);

        var registration = Host.GetRegistration(plugin.Id);
        window.ShowMounted(registration?.State == PluginLifecycleState.Active ? null : registration?.FailureError, Host.Diagnostics);
    }

    /// <summary>Unmounts the plugin and saves its settings.</summary>
    public void Shutdown()
    {
        Host?.Dispose();
        _settings?.Dispose();
    }
}
