namespace FryWorld;

using System;
using System.Threading.Tasks;
using Avalonia;
using FryWorld.Hosting.Mcp;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Plugins;
using FryWorld.Services.AI;

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
        var ctx = new FryPluginContext();
        var settings = new AiSettings();
        var contextManager = new AiContextManager(ctx, () => null);
        var toolRegistry = new AiToolRegistry(ctx, () => null);
        var promptRegistry = new AiPromptRegistry(ctx);
        var aiService = new AiService(settings, contextManager, toolRegistry, promptRegistry);
        ctx.RegisterService<IAiService>(aiService);

        var server = new McpServer(aiService, ctx);
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
