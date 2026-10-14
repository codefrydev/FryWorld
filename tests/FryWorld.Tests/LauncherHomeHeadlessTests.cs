using System;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
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

            Assert.Equal(12, homeVm.AllCount);
            Assert.Equal(12, homeVm.FilteredApps.Count);

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
            Assert.Equal(12, homeVm.FilteredApps.Count);
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
            var frySharpCard = mainVm.LauncherHomeVm.FilteredApps.FirstOrDefault(a => a.Id == "com.fryworld.csharpstudio");
            Assert.NotNull(frySharpCard);
            
            frySharpCard.ExecutePrimaryActionCommand.Execute(null);

            Assert.False(mainVm.IsLauncherActive);
            Assert.NotNull(mainVm.SelectedNavItem);
            Assert.Contains("com.fryworld.csharpstudio", mainVm.SelectedNavItem.Id);
            Assert.NotNull(mainVm.CurrentView);
        });
    }
}
