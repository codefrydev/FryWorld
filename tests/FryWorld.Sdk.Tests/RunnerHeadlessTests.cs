using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Runner;
using FryWorld.Sdk.Services;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// Drives the standalone runner off-screen. One Avalonia app per process, so everything runs in one test.
/// </summary>
public sealed class RunnerHeadlessTests
{
    private sealed class StudioPlugin : FryPluginBase
    {
        public override string Id => "test.runner.studio";
        public override string Name => "Runner Studio";

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            var settings = ctx.GetPluginSettings();
            ctx.RegisterNavigationItem(new NavigationItemDescriptor
            {
                Id = "studio.editor",
                Title = "Editor",
                ViewFactory = () => new Border
                {
                    Background = Brushes.SteelBlue,
                    Child = new TextBlock { Text = $"Editor page — opened {settings.Get("Opened", 0)} times", Margin = new Thickness(24), FontSize = 20 }
                }
            });
            ctx.RegisterNavigationItem(new NavigationItemDescriptor
            {
                Id = "studio.preview",
                Title = "Preview",
                Order = 200,
                ViewFactory = () => new TextBlock { Text = "Preview page", Margin = new Thickness(24) }
            });
            ctx.RegisterOverlay(new OverlayDescriptor
            {
                Id = "studio.inspector",
                Title = "Inspector",
                DefaultWidth = 300,
                DefaultHeight = 200,
                ViewFactory = () => new TextBlock { Text = "Inspector overlay", Margin = new Thickness(16) }
            });
            ctx.RegisterCommand(new CommandPaletteDescriptor
            {
                Id = "studio.open.inspector",
                Title = "Open Inspector",
                ShortcutKey = "Ctrl+I",
                Command = new RelayCommand(() =>
                {
                    settings.Set("Opened", settings.Get("Opened", 0) + 1);
                    ctx.GetService<IShellService>().ShowOverlay("studio.inspector");
                })
            });
            ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor { Id = "studio.status", Label = "● Ready" });
            return Task.CompletedTask;
        }
    }

    private sealed class GamePlugin : FryPluginBase
    {
        public override string Id => "test.runner.game";
        public override string Name => "Runner Game";

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            ctx.RegisterOverlay(new OverlayDescriptor
            {
                Id = "game.board",
                Title = "Game",
                DefaultWidth = 320,
                DefaultHeight = 420,
                ViewFactory = () => new Border { Background = Brushes.DarkGreen, Child = new TextBlock { Text = "Game board", Margin = new Thickness(16) } }
            });
            return Task.CompletedTask;
        }
    }

    private sealed class BrokenPlugin : FryPluginBase
    {
        public override string Id => "test.runner.broken";
        public override string Name => "Runner Broken";
        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default) =>
            throw new InvalidOperationException("Broken on purpose");
    }

    private static void Pump(Func<bool> until, int maxFrames = 200)
    {
        for (var i = 0; i < maxFrames && !until(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Snapshot(Window window, string name)
    {
        Pump(() => false, 10);
        var frame = window.CaptureRenderedFrame();
        var dir = Path.Combine(AppContext.BaseDirectory, "runner-snapshots");
        Directory.CreateDirectory(dir);
        frame?.Save(Path.Combine(dir, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public void Runner_ShowsPagesOverlaysCommands_AndGivesPluginsTheShell()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"runner-{Guid.NewGuid():N}.json");
        FryRunnerApp? app = null;

        AppBuilder.Configure(() => app = new FryRunnerApp(
                () => new StudioPlugin(),
                a => a.Styles.Add(new FluentTheme()),
                new FryRunnerOptions { SettingsFilePath = settingsPath }))
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .SetupWithoutStarting();

        try
        {
            // --- A plugin with pages: tabs, commands, overlays as windows ---
            var window = app!.CreateMainWindow();
            window.Show();
            Pump(() => app.MountCompletion!.IsCompleted);

            Assert.Equal(PluginLifecycleState.Active, app.Host!.GetPluginState("test.runner.studio"));
            Assert.Equal("studio.editor", window.SelectedNavigationItemId);
            Assert.Null(window.MainOverlayId);
            Snapshot(window, "studio-editor");

            var shell = app.Host.Context.GetService<IShellService>();
            Assert.True(shell.NavigateTo("studio.preview"));
            Pump(() => window.SelectedNavigationItemId == "studio.preview");
            Assert.Equal("studio.preview", window.SelectedNavigationItemId);
            Assert.False(shell.NavigateTo("no.such.page"));

            // The command opens the overlay through IShellService, as a plugin would.
            var command = app.Host.Context.GetRegisteredCommands().Single(c => c.Id == "studio.open.inspector");
            command.Command!.Execute(null);
            Pump(() => shell.IsOverlayVisible("studio.inspector"));
            Assert.Contains("studio.inspector", window.OpenOverlayWindowIds);
            Assert.True(shell.IsOverlayVisible("studio.inspector"));

            Assert.True(shell.ToggleOverlay("studio.inspector"));
            Pump(() => !shell.IsOverlayVisible("studio.inspector"));
            Assert.False(shell.IsOverlayVisible("studio.inspector"));

            // Settings go to the runner's settings file.
            app.Shutdown();
            Assert.Contains("test.runner.studio:Opened", File.ReadAllText(settingsPath));
            window.Close();

            // --- An overlay-only plugin (a game): its overlay is the window ---
            var gameHost = new PluginHost();
            var gameContext = (FryPluginContext)gameHost.Context;
            var game = new GamePlugin();
            var gameWindow = new FryRunnerWindow(game, gameContext, new FryRunnerOptions());
            gameContext.RegisterService<IShellService>(gameWindow.Shell);
            gameWindow.Show();
            var gameMount = gameHost.MountPluginAsync(game);
            Pump(() => gameMount.IsCompleted);
            gameWindow.ShowMounted(null, gameHost.Diagnostics);
            Pump(() => false, 5);

            Assert.Equal("game.board", gameWindow.MainOverlayId);
            Assert.True(gameHost.Context.GetService<IShellService>().IsOverlayVisible("game.board"));
            Assert.Equal(320 + 48, gameWindow.Width);
            Snapshot(gameWindow, "game-overlay-only");
            gameWindow.Close();

            // --- A plugin that fails to mount: the error is shown instead of a blank window ---
            var brokenHost = new PluginHost();
            var brokenWindow = new FryRunnerWindow(new BrokenPlugin(), (FryPluginContext)brokenHost.Context, new FryRunnerOptions());
            brokenWindow.Show();
            var brokenMount = brokenHost.MountPluginAsync(new BrokenPlugin());
            Pump(() => brokenMount.IsCompleted);
            var failure = brokenHost.GetRegistration("test.runner.broken")!.FailureError;
            brokenWindow.ShowMounted(failure, brokenHost.Diagnostics);
            Pump(() => false, 5);
            Snapshot(brokenWindow, "broken-plugin");
            brokenWindow.Close();
        }
        finally
        {
            try { File.Delete(settingsPath); } catch { }
        }
    }
}
