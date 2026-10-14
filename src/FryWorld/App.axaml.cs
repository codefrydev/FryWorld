using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Services;
using FryWorld.ViewModels;
using FryWorld.Views;

namespace FryWorld;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        Name = "FryWorld";
        AvaloniaXamlLoader.Load(this);

        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
        {
            if (e.Exception is NullReferenceException nre && nre.StackTrace?.Contains("AvaloniaEdit.CodeCompletion") == true)
            {
                e.Handled = true;
                return;
            }

            if (e.Exception is System.Reflection.TargetInvocationException tie &&
                tie.InnerException is NullReferenceException inre &&
                inre.StackTrace?.Contains("AvaloniaEdit.CodeCompletion") == true)
            {
                e.Handled = true;
                return;
            }
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        InitializePlugins();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Microkernel Host
        services.AddSingleton<PluginHost>(sp => new PluginHost(new FryPluginContext(fallbackServiceProvider: sp)));

        // Foundation Services
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ISettingsManager, SettingsManager>();

        // Marketplace Service
        services.AddSingleton<IPluginMarketplaceService>(sp =>
            new PluginMarketplaceService(
                sp.GetRequiredService<PluginHost>(),
                FryWorldPaths.PluginsDirectory));

        // Master ViewModels
        services.AddSingleton<MainViewModel>();
    }

    private static void InitializePlugins()
    {
        var host = Services.GetRequiredService<PluginHost>();
        var pluginsDir = FryWorldPaths.PluginsDirectory;

        try
        {
            if (Directory.Exists(pluginsDir))
            {
                var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(pluginsDir);
                foreach (var pkg in packages)
                {
                    foreach (var plugin in pkg.Plugins)
                    {
                        try
                        {
                            host.RegisterPlugin(plugin);
                        }
                        catch { }
                    }
                }
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await host.StartAsync();
                }
                catch (Exception ex)
                {
                    AppLogService.Instance.LogWarning("PluginHost", "Background mount error", ex);
                }
            });
        }
        catch (Exception ex)
        {
            AppLogService.Instance.LogError("App", "Error during plugin initialization", ex);
        }
    }
}
