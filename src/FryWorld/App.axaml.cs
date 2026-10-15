using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Hosting;
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        // Plugins load in the background, after the window exists, so it appears immediately.
        Services.GetRequiredService<PluginStartup>().Start();

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Foundation Services
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IShellService, ShellService>();

        // Plugin runtime: host, marketplace, settings (same settings.json as before) and background startup
        services.AddFryWorldPluginHost(o =>
        {
            o.PluginsDirectory = FryWorldPaths.PluginsDirectory;
            o.SettingsFilePath = FryWorldPaths.SettingsFilePath;
            o.HostVersion = typeof(App).Assembly.GetName().Version;
            o.Host.Logger = AppLogService.Instance.CreateLogger("Plugins");
            o.Host.EventSynchronizationContext = new UiThreadSynchronizationContext();
        });

        // Master ViewModels
        services.AddSingleton<MainViewModel>();
    }
}
