using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Tests.TestSupport;
using FryWorld.Services;
using FryWorld.ViewModels;
using FryWorld.Views;
using Xunit;

namespace FryWorld.Tests;

[Collection("HeadlessTests")]
public class MainWindowHeadlessTests
{
    public MainWindowHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void MainWindow_RendersOffscreen_CapturesValidBitmap()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var window = new MainWindow
            {
                DataContext = mainVm,
                Width = 1280,
                Height = 820
            };

            window.Show();
            TestHeadlessSession.Settle(10);

            try
            {
                using var frame = TestHeadlessSession.Capture(window);
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width > 0, "Rendered bitmap width must be greater than zero.");
                Assert.True(frame.PixelSize.Height > 0, "Rendered bitmap height must be greater than zero.");
            }
            finally
            {
                window.Close();
                TestHeadlessSession.Settle(2);
            }
        });
    }

    [Fact]
    public void NavigationRail_SwitchesBetweenLauncherAndPlugins()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();

            var launcherItem = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
            mainVm.SelectNavigationItem(launcherItem);
            TestHeadlessSession.Settle(4);

            Assert.True(mainVm.IsLauncherActive);
            Assert.False(mainVm.IsPluginsActive);

            // Switch to Plugins Manager
            var pluginsItem = mainVm.NavigationItems.First(n => n.Id == "nav.plugins");
            mainVm.SelectNavigationItem(pluginsItem);
            TestHeadlessSession.Settle(4);

            Assert.False(mainVm.IsLauncherActive);
            Assert.True(mainVm.IsPluginsActive);
            Assert.Equal(pluginsItem, mainVm.SelectedNavItem);

            // Switch back to Launcher
            mainVm.SelectNavigationItem(launcherItem);
            TestHeadlessSession.Settle(4);

            Assert.True(mainVm.IsLauncherActive);
            Assert.False(mainVm.IsPluginsActive);
        });
    }

    [Fact]
    public void DynamicCapabilityRegistration_AddsDynamicNavItemAndRendersView()
    {
        TestHeadlessSession.Run(() =>
        {
            var host = App.Services.GetRequiredService<PluginHost>();
            var mainVm = App.Services.GetRequiredService<MainViewModel>();

            bool viewFactoryCalled = false;
            var navItem = new NavigationItemDescriptor
            {
                Id = "test.dynamic.editor",
                Title = "Test Editor Studio",
                IconKind = "CodeBraces",
                ViewFactory = () =>
                {
                    viewFactoryCalled = true;
                    return new Border
                    {
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.DarkSlateBlue),
                        Child = new TextBlock { Text = "Dynamic Studio In-Situ" }
                    };
                }
            };

            host.Context.RegisterNavigationItem(navItem);
            TestHeadlessSession.Settle(4);

            // Verify dynamic item is added to navigation
            var dynItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id.Contains("test.dynamic.editor"));
            Assert.NotNull(dynItem);

            // Select it
            mainVm.SelectNavigationItem(dynItem);
            TestHeadlessSession.Settle(4);

            Assert.True(viewFactoryCalled);
            Assert.NotNull(mainVm.CurrentView);
            Assert.IsType<Border>(mainVm.CurrentView);

            // Cleanup: reset navigation back to launcher
            var launcherItem = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
            mainVm.SelectNavigationItem(launcherItem);
            TestHeadlessSession.Settle(2);
        });
    }

    [Fact]
    public void NavigateToPlugin_ImageEditor_LoadsAndDisplaysView()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var host = App.Services.GetRequiredService<PluginHost>();

            // Wait for background loading and mounting to finish
            var startup = App.Services.GetRequiredService<PluginStartup>().Completion;
            for (int i = 0; i < 400 && !startup.IsCompleted; i++)
            {
                TestHeadlessSession.Settle(1);
            }
            Assert.True(startup.IsCompleted, "Plugins did not finish loading within the test timeout.");

            var hasImageEditor = host.GetRegisteredPlugins().Any(p => p.Id == "com.fryworld.imageeditor")
                || Directory.Exists(Path.Combine(FryWorldPaths.PluginsDirectory, "com.fryworld.imageeditor"));

            if (!hasImageEditor)
            {
                // In headless CI or clean environments without pre-sideloaded plugins in AppData,
                // register an in-situ descriptor so navigation logic can still be verified.
                host.Context.RegisterNavigationItem(new NavigationItemDescriptor
                {
                    Id = "com.fryworld.imageeditor",
                    Title = "Image Editor",
                    ViewFactory = () => new Border { Child = new TextBlock { Text = "Image Editor View" } }
                });
            }

            mainVm.NavigateToPlugin("com.fryworld.imageeditor");
            TestHeadlessSession.Settle(10);

            Assert.NotNull(mainVm.CurrentView);
            if (hasImageEditor)
            {
                Assert.Equal("PdfEditorApp.Plugins.ImageEditor.Views.ImageEditorView", mainVm.CurrentView.GetType().FullName);
            }
            Assert.Contains("com.fryworld.imageeditor", mainVm.SelectedNavItem?.Id);

            // Cleanup: reset navigation back to launcher
            var launcherItem = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
            mainVm.SelectNavigationItem(launcherItem);
            TestHeadlessSession.Settle(4);
        });
    }

    [Fact]
    public void NavigateToPlugin_Circuit_LoadsAndDisplaysView()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var host = App.Services.GetRequiredService<PluginHost>();

            var startup = App.Services.GetRequiredService<PluginStartup>().Completion;
            for (int i = 0; i < 400 && !startup.IsCompleted; i++)
            {
                TestHeadlessSession.Settle(1);
            }
            Assert.True(startup.IsCompleted, "Plugins did not finish loading within the test timeout.");

            var registered = host.GetRegisteredPlugins();
            Console.WriteLine($"Registered plugins: {string.Join(", ", registered.Select(p => p.Id))}");

            var logs = FryWorld.Sdk.Services.AppLogService.Instance.GetRecentEntries(100);
            foreach (var log in logs.Where(l => l.Level >= FryWorld.Sdk.Services.AppLogLevel.Warning))
            {
                Console.WriteLine($"[LOG {log.Level}] {log.Category}: {log.Message}\nException: {log.Exception}");
            }

            Console.WriteLine($"CurrentView type: {mainVm.CurrentView?.GetType().FullName}");
            var window = new MainWindow
            {
                DataContext = mainVm,
                Width = 1280,
                Height = 820
            };
            window.Show();
            TestHeadlessSession.Settle(10);

            Console.WriteLine($"Before navigate - SelectedNavItem: {mainVm.SelectedNavItem?.Id}, CurrentView: {mainVm.CurrentView?.GetType().FullName}");

            var hasCircuit = host.GetRegisteredPlugins().Any(p => p.Id == "com.fryworld.circuit")
                || Directory.Exists(Path.Combine(FryWorldPaths.PluginsDirectory, "com.fryworld.circuit"));

            if (!hasCircuit)
            {
                // In headless CI or clean environments without pre-sideloaded plugins in AppData,
                // register an in-situ descriptor so navigation logic can still be verified.
                host.Context.RegisterNavigationItem(new NavigationItemDescriptor
                {
                    Id = "com.fryworld.circuit",
                    Title = "Circuit Studio",
                    ViewFactory = () => new Border { Child = new TextBlock { Text = "Circuit Studio View" } }
                });
            }

            mainVm.NavigateToPlugin("com.fryworld.circuit");
            TestHeadlessSession.Settle(20);

            Assert.Contains("com.fryworld.circuit", mainVm.SelectedNavItem?.Id);
            Assert.NotNull(mainVm.CurrentView);
            if (hasCircuit)
            {
                Assert.Equal("FryCircuit.UI.Views.CircuitStudioView", mainVm.CurrentView.GetType().FullName);
            }

            var studioCC = window.FindControl<Avalonia.Controls.ContentControl>("DynamicStudioHost");
            Assert.NotNull(studioCC);
            Assert.Same(mainVm.CurrentView, studioCC.Content);
            Assert.True(studioCC.Bounds.Width > 0, "Studio viewport width must be greater than zero.");
            Assert.True(studioCC.Bounds.Height > 0, "Studio viewport height must be greater than zero.");

            try
            {
                using var frame = TestHeadlessSession.Capture(window);
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width > 0);
                Assert.True(frame.PixelSize.Height > 0);
            }
            finally
            {
                var launcherItem = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
                mainVm.SelectNavigationItem(launcherItem);
                TestHeadlessSession.Settle(2);
                window.Close();
                TestHeadlessSession.Settle(2);
            }
        });
    }

    [Fact]
    public void Marketplace_InstallCircuitPlugin_DownloadsVerifiesAndMounts()
    {
        TestHeadlessSession.Run(() =>
        {
            var marketplace = App.Services.GetRequiredService<IPluginMarketplaceService>();
            var host = App.Services.GetRequiredService<PluginHost>();

            var catalogTask = marketplace.FetchCatalogAsync(forceRefresh: true);
            for (int i = 0; i < 300 && !catalogTask.IsCompleted; i++)
            {
                TestHeadlessSession.Settle(1);
            }
            Assert.True(catalogTask.IsCompleted, "Catalog fetch timed out.");
            var catalog = catalogTask.GetAwaiter().GetResult();

            var circuitItem = catalog.FirstOrDefault(c => c.Id == "com.fryworld.circuit");
            Assert.NotNull(circuitItem);
            Assert.True(Version.TryParse(circuitItem.Version, out var v) && v >= new Version(1, 0, 0));
            Assert.Contains("FryStore", circuitItem.DownloadUrl);

            var downloadTask = marketplace.DownloadPackageAsync(circuitItem);
            for (int i = 0; i < 300 && !downloadTask.IsCompleted; i++)
            {
                TestHeadlessSession.Settle(1);
            }
            Assert.True(downloadTask.IsCompleted, "Package download timed out.");
            var downloadedPath = downloadTask.GetAwaiter().GetResult();
            Assert.True(File.Exists(downloadedPath));

            var installTask = marketplace.InstallPackageAsync(downloadedPath);
            for (int i = 0; i < 500 && !installTask.IsCompleted; i++)
            {
                TestHeadlessSession.Settle(1);
            }
            Assert.True(installTask.IsCompleted, "Plugin installation timed out.");
            installTask.GetAwaiter().GetResult();

            Assert.Contains(host.GetRegisteredPlugins(), p => p.Id == "com.fryworld.circuit");
        });
    }

    [Fact]
    public void NavigationRail_ToggleNavRail_TogglesVisibilityAndUpdatesNativeMenu()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var window = new MainWindow
            {
                DataContext = mainVm
            };
            window.Show();
            TestHeadlessSession.Settle(4);

            try
            {
                Assert.True(mainVm.IsNavRailVisible);

                // Toggle rail off
                mainVm.ToggleNavRailCommand.Execute(null);
                TestHeadlessSession.Settle(2);
                Assert.False(mainVm.IsNavRailVisible);

                // Toggle rail back on
                mainVm.ToggleNavRailCommand.Execute(null);
                TestHeadlessSession.Settle(2);
                Assert.True(mainVm.IsNavRailVisible);

                // Verify native menu was populated
                var nativeMenu = NativeMenu.GetMenu(window);
                Assert.NotNull(nativeMenu);
                var workspaces = nativeMenu.Items.OfType<NativeMenuItem>().FirstOrDefault(m => m.Header == "Workspaces");
                Assert.NotNull(workspaces);
                Assert.NotNull(workspaces.Menu);
                Assert.True(workspaces.Menu.Items.Count >= 3, "Workspaces menu should contain launcher, plugins, and toggle rail items.");
            }
            finally
            {
                window.Close();
                TestHeadlessSession.Settle(2);
            }
        });
    }
}


