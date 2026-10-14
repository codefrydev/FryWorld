using System;
using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Services;
using FryWorld.Tests.TestSupport;
using FryWorld.ViewModels;
using Xunit;

namespace FryWorld.Tests;

[Collection("HeadlessTests")]
public class ThemingHeadlessTests
{
    public ThemingHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void ThemeService_TogglesThemeWithoutCrash()
    {
        TestHeadlessSession.Run(() =>
        {
            var themeService = App.Services.GetRequiredService<IThemeService>();
            var initialTheme = themeService.CurrentTheme;

            themeService.ToggleTheme();
            TestHeadlessSession.Settle(4);

            var toggledTheme = themeService.CurrentTheme;
            Assert.NotEqual(initialTheme, toggledTheme);

            if (Application.Current != null)
            {
                var expectedVariant = toggledTheme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
                Assert.Equal(expectedVariant, Application.Current.RequestedThemeVariant);
            }

            // Toggle back
            themeService.ToggleTheme();
            TestHeadlessSession.Settle(4);
            Assert.Equal(initialTheme, themeService.CurrentTheme);
        });
    }
}
