using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
using FryWorld.Tests.TestSupport;
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
}
