namespace FryWorld;

using System;
using System.Threading.Tasks;
using Avalonia;
using FryWorld.Hosting.Mcp;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;
using FryWorld.Services.AI;

using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Hosting;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--mcp" || args[0] == "--mcp-stdio" || args[0] == "--mcp-bridge"))
        {
            RunMcpStdioAsync().GetAwaiter().GetResult();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunMcpStdioAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AiSettings>();
        services.AddSingleton<AiContextManager>(sp =>
        {
            var host = sp.GetRequiredService<PluginHost>();
            return new AiContextManager(host.Context, () => null);
        });
        services.AddSingleton<IAiContextManager>(sp => sp.GetRequiredService<AiContextManager>());
        services.AddSingleton<AiToolRegistry>(sp =>
        {
            var host = sp.GetRequiredService<PluginHost>();
            return new AiToolRegistry(host.Context, () => null);
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

        services.AddFryWorldPluginHost(o =>
        {
            o.PluginsDirectory = FryWorldPaths.PluginsDirectory;
            o.SettingsFilePath = FryWorldPaths.SettingsFilePath;
            o.HostVersion = typeof(Program).Assembly.GetName().Version;
        });

        var sp = services.BuildServiceProvider();
        var host = sp.GetRequiredService<PluginHost>();
        var aiService = sp.GetRequiredService<IAiService>();
        host.Context.RegisterService<IAiService>(aiService);

        var startup = sp.GetService<PluginStartup>();
        if (startup != null)
        {
            startup.Start();
            await startup.Completion;

            foreach (var diag in host.Diagnostics)
            {
                Console.Error.WriteLine($"[PluginDiagnostic] {diag.Stage} {diag.Severity} ({diag.PluginId}): {diag.Message} - {diag.Exception}");
            }
        }

        var server = new McpServer(aiService, host.Context);
        var stdioTransport = new McpStdioTransport(server);
        await stdioTransport.RunAsync(Console.In, Console.Out);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                ShowInDock = true,
                DisableSetProcessName = false
            })
            .WithInterFont()
            .LogToTrace();
}
