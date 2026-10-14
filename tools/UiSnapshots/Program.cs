using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.ViewModels;
using FryWorld.Views;

namespace FryWorld.Tools.UiSnapshots;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("   FryWorld Headless UI Snapshots Runner");
        Console.WriteLine("==================================================");

        if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        bool isLight = args.Contains("--light");
        string? outDir = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--out") outDir = args[i + 1];
        }

        try
        {
            Snapshot.Start(outDir, isLight);
            var cmd = args[0].ToLowerInvariant();

            return cmd switch
            {
                "all" => RunAllSnapshots(isLight),
                "main" => RunMainSnapshot(isLight),
                "launcher" => RunLauncherSnapshot(isLight),
                "table" => RunLauncherTableSnapshot(isLight),
                "plugins" => RunPluginsSnapshot(isLight),
                "palette" => RunPaletteSnapshot(isLight),
                "overlay" => RunOverlaySnapshot(isLight),
                "launch" => RunLaunchFrySharpSnapshot(isLight),
                "icons" => GenerateBrandIcons(),
                "plugin" => args.Length > 1 ? RunPluginSnapshot(args[1], isLight) : Error("Missing plugin path argument."),
                _ => Error($"Unknown command '{cmd}'. Run 'help' for usage.")
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[FATAL ERROR] {ex.GetType().FullName}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                Console.Error.WriteLine(ex.InnerException.StackTrace);
            }
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Usage:
              dotnet run --project tools/UiSnapshots -- <command> [options]

            Commands:
              all                 Render all primary views and states (dark + light).
              main                Render MainWindow shell.
              launcher            Render LauncherHomeView in Grid mode.
              table               Render LauncherHomeView in Table mode.
              plugins             Render PluginsManagerPageView.
              palette             Render CommandPaletteDialog (⌘K).
              overlay             Render ShellOverlayHost with active overlay.
              plugin <path>       Load a .fryplugin or .dll and render its views.

            Options:
              --light             Render using Light theme variant (default is Dark).
              --out <dir>         Output directory for snapshots (default: test-artifacts/snapshots).
            """);
    }

    private static int RunAllSnapshots(bool lightRequested)
    {
        Console.WriteLine("\n[1/7] Capturing MainWindow (Dark)...");
        RunMainSnapshot(light: false, nameSuffix: "_dark");

        Console.WriteLine("\n[2/7] Capturing MainWindow (Light)...");
        RunMainSnapshot(light: true, nameSuffix: "_light");

        Console.WriteLine("\n[3/7] Capturing LauncherHomeView (Grid)...");
        RunLauncherSnapshot(lightRequested, nameSuffix: "_grid");

        Console.WriteLine("\n[4/7] Capturing LauncherHomeView (Table)...");
        RunLauncherTableSnapshot(lightRequested, nameSuffix: "_table");

        Console.WriteLine("\n[5/7] Capturing PluginsManagerPageView...");
        RunPluginsSnapshot(lightRequested);

        Console.WriteLine("\n[6/7] Capturing CommandPaletteDialog (⌘K)...");
        RunPaletteSnapshot(lightRequested);

        Console.WriteLine("\n[7/7] Capturing ShellOverlayHost with active card...");
        RunOverlaySnapshot(lightRequested);

        Console.WriteLine("\n==================================================");
        Console.WriteLine(" All UI snapshots captured successfully!");
        Console.WriteLine($" Output Directory: {Snapshot.OutputFolder}");
        Console.WriteLine("==================================================");
        return 0;
    }

    private static int RunMainSnapshot(bool light, string nameSuffix = "")
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm, Width = 1280, Height = 820 };
        window.Show();
        Snapshot.Settle(12);
        try
        {
            var path = Snapshot.Save(window, $"fryworld_main{nameSuffix}");
            return 0;
        }
        finally
        {
            window.Close();
            Snapshot.Settle(2);
        }
    }

    private static int RunLauncherSnapshot(bool light, string nameSuffix = "")
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var homeVm = mainVm.LauncherHomeVm;
        homeVm.SetViewModeCommand.Execute("Grid");

        var view = new LauncherHomeView { DataContext = homeVm };
        Snapshot.RenderAndSave(view, 1200, 800, $"fryworld_launcher{nameSuffix}");
        return 0;
    }

    private static int RunLauncherTableSnapshot(bool light, string nameSuffix = "")
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var homeVm = mainVm.LauncherHomeVm;
        homeVm.SetViewModeCommand.Execute("Table");

        var view = new LauncherHomeView { DataContext = homeVm };
        Snapshot.RenderAndSave(view, 1200, 800, $"fryworld_launcher{nameSuffix}");
        return 0;
    }

    private static int RunPluginsSnapshot(bool light)
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var view = new PluginsManagerPageView { DataContext = mainVm.PluginsManagerVm };
        Snapshot.RenderAndSave(view, 1200, 800, "fryworld_plugins_manager");
        return 0;
    }

    private static int RunPaletteSnapshot(bool light)
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        mainVm.OpenCommandPaletteCommand.Execute(null);

        var view = new CommandPaletteDialog { DataContext = mainVm.CommandPaletteVm };
        Snapshot.RenderAndSave(view, 800, 600, "fryworld_command_palette");
        return 0;
    }

    private static int RunOverlaySnapshot(bool light)
    {
        Snapshot.Start(lightTheme: light);
        var mainVm = App.Services.GetRequiredService<MainViewModel>();

        // Register sample overlay descriptor
        var descriptor = new OverlayDescriptor
        {
            Id = "sample.quick.scratchpad",
            Title = "Quick Scratchpad",
            ViewFactory = () => new Border
            {
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#161D2A")),
                Padding = new Avalonia.Thickness(16),
                Child = new TextBlock
                {
                    Text = "FryWorld In-Situ Floating Overlay\nRendered off-screen with headless Skia.",
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#38BDF8")),
                    FontSize = 14,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold
                }
            },
            IconKind = "NotebookEdit",
            DefaultWidth = 420,
            DefaultHeight = 260
        };

        mainVm.OpenOverlay(descriptor);

        var host = new ShellOverlayHost { DataContext = mainVm };
        Snapshot.RenderAndSave(host, 1280, 820, "fryworld_shell_overlay");
        return 0;
    }

    private static int RunPluginSnapshot(string pluginPath, bool light)
    {
        if (!File.Exists(pluginPath))
        {
            return Error($"Plugin file not found: {pluginPath}");
        }

        Console.WriteLine($"Loading plugin from: {pluginPath}");
        Snapshot.Start(lightTheme: light);

        var host = App.Services.GetRequiredService<PluginHost>();
        var package = PluginAssemblyLoader.LoadAssembly(pluginPath);

        Console.WriteLine($"Discovered {package.Plugins.Count} plugin(s) in package.");
        foreach (var plugin in package.Plugins)
        {
            Console.WriteLine($"Mounting plugin '{plugin.Name}' ({plugin.Id} v{plugin.Version})...");
            host.RegisterPlugin(plugin);
            plugin.ApplyAsync(host.Context).GetAwaiter().GetResult();
        }

        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm, Width = 1280, Height = 820 };
        window.Show();
        Snapshot.Settle(15);
        try
        {
            var fileName = $"fryworld_insitu_plugin_{Path.GetFileNameWithoutExtension(pluginPath)}";
            Snapshot.Save(window, fileName);
            return 0;
        }
        finally
        {
            window.Close();
            Snapshot.Settle(2);
        }
    }

    private static int RunLaunchFrySharpSnapshot(bool light)
    {
        Snapshot.Start(lightTheme: light);

        var mainVm = App.Services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainVm, Width = 1280, Height = 820 };
        window.Show();
        Snapshot.Settle(10);

        var frySharpCard = mainVm.LauncherHomeVm.FilteredApps.FirstOrDefault(a => a.Id == "com.fryworld.csharpstudio");
        if (frySharpCard != null)
        {
            frySharpCard.ExecutePrimaryActionCommand.Execute(null);
            Snapshot.Settle(15);
        }

        try
        {
            var fileName = light ? "fryworld_launched_frysharp_light" : "fryworld_launched_frysharp_dark";
            Snapshot.Save(window, fileName);
            return 0;
        }
        finally
        {
            window.Close();
            Snapshot.Settle(2);
        }
    }

    private static int GenerateBrandIcons()
    {
        var targetDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FryWorld", "Assets", "plugins");
        targetDir = Path.GetFullPath(targetDir);
        IconGenerator.GenerateAllBrandIcons(targetDir);
        return 0;
    }

    private static int Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {message}");
        Console.ResetColor();
        return 1;
    }
}
