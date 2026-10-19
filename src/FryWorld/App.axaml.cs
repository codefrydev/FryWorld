using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Hosting.Mcp;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Services;
using FryWorld.Services.AI;
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

        // Initialize Theme persistence
        var settingsMgr = Services.GetService<ISettingsManager>();
        if (settingsMgr != null)
        {
            Services.GetRequiredService<ThemeService>().SetSettingsManager(settingsMgr);
        }

        // Register host-provided AI service to plugin context
        var host = Services.GetRequiredService<PluginHost>();
        var aiService = Services.GetRequiredService<IAiService>();
        host.Context.RegisterService<IAiService>(aiService);

        // Start MCP server (HTTP/SSE)
        var aiSettings = Services.GetRequiredService<AiSettings>();
        if (aiSettings.McpEnabled)
        {
            var mcpTransport = Services.GetRequiredService<McpHttpSseTransport>();
            mcpTransport.Start(aiSettings.McpPort);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            desktop.MainWindow = mainWindow;
            Services.GetRequiredService<ThemeService>().TrackResourceRoot(mainWindow);

            desktop.Exit += (s, e) =>
            {
                Services.GetService<McpHttpSseTransport>()?.Dispose();
            };
        }

        // Plugins load in the background, after the window exists, so it appears immediately.
        Services.GetRequiredService<PluginStartup>().Start();

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Foundation Services
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IThemeService>(sp => sp.GetRequiredService<ThemeService>());
        services.AddSingleton<FryWorld.Sdk.Theming.IThemeManager>(sp => sp.GetRequiredService<ThemeService>());
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IShellService, ShellService>();

        // AI & MCP Subsystems
        services.AddSingleton<AiSettings>();
        services.AddSingleton<AiContextManager>(sp =>
        {
            var host = sp.GetRequiredService<PluginHost>();
            return new AiContextManager(host.Context, () =>
            {
                var mainVm = sp.GetService<MainViewModel>();
                return mainVm?.SelectedNavItem?.Id;
            });
        });
        services.AddSingleton<IAiContextManager>(sp => sp.GetRequiredService<AiContextManager>());
        services.AddSingleton<AiToolRegistry>(sp =>
        {
            var host = sp.GetRequiredService<PluginHost>();
            return new AiToolRegistry(host.Context, () =>
            {
                var mainVm = sp.GetService<MainViewModel>();
                return mainVm?.SelectedNavItem?.Id;
            });
        });
        services.AddSingleton<IAiToolRegistry>(sp => sp.GetRequiredService<AiToolRegistry>());
        services.AddSingleton<AiPromptRegistry>(sp =>
        {
            var host = sp.GetRequiredService<PluginHost>();
            return new AiPromptRegistry(host.Context);
        });
        services.AddSingleton<IAiPromptRegistry>(sp => sp.GetRequiredService<AiPromptRegistry>());
        services.AddSingleton<AiService>();
        services.AddSingleton<IAiService>(sp => sp.GetRequiredService<AiService>());
        services.AddSingleton<McpServer>();
        services.AddSingleton<McpHttpSseTransport>();

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
        services.AddSingleton<AiAssistantViewModel>();
    }
}

