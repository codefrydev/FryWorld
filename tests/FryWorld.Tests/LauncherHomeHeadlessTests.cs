using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Plugins;
using FryWorld.Tests.TestSupport;
using FryWorld.ViewModels;
using FryWorld.Views;
using Xunit;

namespace FryWorld.Tests;

[Collection("HeadlessTests")]
public class LauncherHomeHeadlessTests
{
    public LauncherHomeHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void LauncherHome_LoadsSeedApps_AndRendersUniformGrid()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var homeVm = mainVm.LauncherHomeVm;

            Assert.True(homeVm.AllCount > 0);
            Assert.Equal(homeVm.AllCount, homeVm.FilteredApps.Count);

            var view = new LauncherHomeView { DataContext = homeVm };
            var window = TestHeadlessSession.ShowOffscreen(view, 1200, 800);

            try
            {
                using var frame = TestHeadlessSession.Capture(window);
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width > 0);
            }
            finally
            {
                window.Close();
                TestHeadlessSession.Settle(2);
            }
        });
    }

    [Fact]
    public void FilterTabs_FiltersAppsCorrectly()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var homeVm = mainVm.LauncherHomeVm;

            // All filter
            homeVm.SetFilterCommand.Execute("All");
            Assert.Equal(homeVm.AllCount, homeVm.FilteredApps.Count);

            // NotInstalled filter
            homeVm.SetFilterCommand.Execute("NotInstalled");
            Assert.Equal(homeVm.NotInstalledCount, homeVm.FilteredApps.Count);

            // Installed filter
            homeVm.SetFilterCommand.Execute("Installed");
            Assert.Equal(homeVm.InstalledCount, homeVm.FilteredApps.Count);

            // Reset
            homeVm.SetFilterCommand.Execute("All");
        });
    }

    [Fact]
    public void SearchQuery_FiltersByTitleDescriptionAndId()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var homeVm = mainVm.LauncherHomeVm;

            homeVm.SearchQuery = "CSharp";
            Assert.NotEmpty(homeVm.FilteredApps);
            Assert.All(homeVm.FilteredApps, app =>
                Assert.True(app.Title.Contains("CSharp", StringComparison.OrdinalIgnoreCase) ||
                            app.Description.Contains("CSharp", StringComparison.OrdinalIgnoreCase) ||
                            app.Id.Contains("CSharp", StringComparison.OrdinalIgnoreCase)));

            homeVm.SearchQuery = "NonExistentAppTerm999";
            Assert.Empty(homeVm.FilteredApps);

            homeVm.SearchQuery = string.Empty;
            Assert.Equal(homeVm.AllCount, homeVm.FilteredApps.Count);
        });
    }

    [Fact]
    public void ViewMode_SwitchesBetweenGridAndTable()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var homeVm = mainVm.LauncherHomeVm;

            homeVm.SetViewModeCommand.Execute("Table");
            Assert.Equal(LauncherViewMode.Table, homeVm.ViewMode);
            Assert.True(homeVm.IsTableMode);
            Assert.False(homeVm.IsGridMode);

            homeVm.SetViewModeCommand.Execute("Grid");
            Assert.Equal(LauncherViewMode.Grid, homeVm.ViewMode);
            Assert.True(homeVm.IsGridMode);
            Assert.False(homeVm.IsTableMode);
        });
    }

    [Fact]
    public void LaunchButton_NavigatesToFrySharp()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var host = App.Services.GetRequiredService<PluginHost>();
            var frySharpCard = mainVm.LauncherHomeVm.FilteredApps.FirstOrDefault(a => a.Id == "com.fryworld.csharpstudio");
            Assert.NotNull(frySharpCard);

            var hasFrySharp = host.GetRegisteredPlugins().Any(p => p.Id == "com.fryworld.csharpstudio")
                || Directory.Exists(Path.Combine(FryWorldPaths.PluginsDirectory, "com.fryworld.csharpstudio"));

            if (!hasFrySharp)
            {
                // In clean CI environments where FrySharp is not pre-installed in AppData,
                // register an in-situ navigation item and ensure the card is in Installed status.
                host.Context.RegisterNavigationItem(new NavigationItemDescriptor
                {
                    Id = "com.fryworld.csharpstudio",
                    Title = "FrySharp",
                    ViewFactory = () => new Border { Child = new TextBlock { Text = "FrySharp Studio View" } }
                });
                frySharpCard.Status = LauncherAppStatus.Installed;
            }

            frySharpCard.ExecutePrimaryActionCommand.Execute(null);
            TestHeadlessSession.Settle(4);

            Assert.False(mainVm.IsLauncherActive);
            Assert.NotNull(mainVm.SelectedNavItem);
            Assert.Contains("com.fryworld.csharpstudio", mainVm.SelectedNavItem.Id);
            Assert.NotNull(mainVm.CurrentView);

            // Cleanup: restore launcher state
            var launcherItem = mainVm.NavigationItems.First(n => n.Id == "nav.launcher");
            mainVm.SelectNavigationItem(launcherItem);
            TestHeadlessSession.Settle(2);
        });
    }

    [Fact]
    public void UninstallButton_SetsStatusToNotInstalled_AndChangesActionToInstall()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var card = mainVm.LauncherHomeVm.FilteredApps.FirstOrDefault(a => a.Id == "com.fryworld.chess");
            Assert.NotNull(card);

            // Execute uninstall
            card.ExecuteUninstallCommand.Execute(null);

            // Verify state
            Assert.Equal(LauncherAppStatus.NotInstalled, card.Status);
            Assert.Equal("INSTALL", card.PrimaryActionText);
            Assert.Equal("AVAILABLE", card.StatusText);
            Assert.False(card.IsInstalled);
        });
    }
}
