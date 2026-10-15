using System;
using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Theming;
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

    [Fact]
    public void ThemeManager_AppliesBuiltInTheme_InjectsTokensAndUpdatesState()
    {
        TestHeadlessSession.Run(() =>
        {
            var themeManager = App.Services.GetRequiredService<IThemeManager>();
            var available = themeManager.GetAvailableThemes();
            Assert.NotEmpty(available);
            Assert.True(available.Count >= 7);

            bool eventFired = false;
            string? notifiedThemeId = null;
            void OnThemeDefChanged(string newId)
            {
                eventFired = true;
                notifiedThemeId = newId;
            }

            themeManager.ThemeDefinitionChanged += OnThemeDefChanged;
            try
            {
                // Apply Dracula theme
                var success = themeManager.ApplyTheme("dracula");
                Assert.True(success);
                TestHeadlessSession.Settle(4);

                Assert.True(eventFired);
                Assert.Equal("dracula", notifiedThemeId);
                Assert.Equal("dracula", themeManager.ActiveThemeId);
                Assert.NotNull(themeManager.ActiveTheme);
                Assert.Equal("Dracula Pro", themeManager.ActiveTheme.Name);
                Assert.Equal(AppTheme.Dark, themeManager.CurrentTheme);

                // Verify Avalonia application tokens are injected
                if (Application.Current != null)
                {
                    Assert.True(Application.Current.Resources.TryGetResource("M3PrimaryBrush", null, out var m3Primary));
                    Assert.NotNull(m3Primary);

                    Assert.True(Application.Current.Resources.TryGetResource("DsPrimaryBrush", null, out var dsPrimary));
                    Assert.NotNull(dsPrimary);

                    Assert.True(Application.Current.Resources.TryGetResource("PsAccentBrush", null, out var psAccent));
                    Assert.NotNull(psAccent);

                    Assert.True(Application.Current.Resources.TryGetResource("M3SurfaceBrush", ThemeVariant.Dark, out var surfaceBrush));
                    Assert.NotNull(surfaceBrush);
                    var brushColor = ((Avalonia.Media.ISolidColorBrush)surfaceBrush).Color;
                    Assert.Equal(Avalonia.Media.Color.Parse("#21222C"), brushColor);

                    // Test control-level resolution in a window
                    var border = new Avalonia.Controls.Border();
                    var window = TestHeadlessSession.ShowOffscreen(border, 200, 200);
                    themeManager.TrackResourceRoot(window);
                    TestHeadlessSession.Settle(2);
                    try
                    {
                        Assert.True(window.Resources.TryGetResource("M3SurfaceBrush", null, out var winResolved));
                        Assert.NotNull(winResolved);
                        var winColor = ((Avalonia.Media.ISolidColorBrush)winResolved).Color;
                        Assert.Equal(Avalonia.Media.Color.Parse("#21222C"), winColor);
                    }
                    finally
                    {
                        window.Close();
                    }
                }

                // Apply Cyberpunk theme
                eventFired = false;
                success = themeManager.ApplyTheme("cyberpunk");
                Assert.True(success);
                TestHeadlessSession.Settle(4);

                Assert.True(eventFired);
                Assert.Equal("cyberpunk", themeManager.ActiveThemeId);
                Assert.Equal(AppTheme.Dark, themeManager.CurrentTheme);
            }
            finally
            {
                themeManager.ThemeDefinitionChanged -= OnThemeDefChanged;
                // Restore to default Dark theme
                themeManager.ApplyTheme("dark-plus");
                TestHeadlessSession.Settle(4);
            }
        });
    }

    [Fact]
    public void ThemeManager_ApplyRandomHarmonicTheme_GeneratesAndApplies()
    {
        TestHeadlessSession.Run(() =>
        {
            var themeManager = App.Services.GetRequiredService<IThemeManager>();
            var initialThemeId = themeManager.ActiveThemeId;

            themeManager.ApplyRandomHarmonicTheme(isDark: true);
            TestHeadlessSession.Settle(4);

            var active = themeManager.ActiveTheme;
            Assert.NotNull(active);
            Assert.StartsWith("harmonic-", active.Id);
            Assert.Equal(active.Id, themeManager.ActiveThemeId);
            Assert.NotEmpty(active.Colors);

            if (Application.Current != null)
            {
                Assert.True(Application.Current.Resources.TryGetResource("M3PrimaryBrush", null, out var primary));
                Assert.NotNull(primary);
            }

            // Restore
            themeManager.ApplyTheme(initialThemeId);
            TestHeadlessSession.Settle(4);
        });
    }

    [Fact]
    public void MainViewModel_AvailableThemesAndCommands_Exposed()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            Assert.NotEmpty(mainVm.AvailableThemes);
            Assert.True(mainVm.AvailableThemes.Count >= 7);

            // Execute ApplyThemeCommand
            mainVm.ApplyThemeCommand.Execute("nord");
            TestHeadlessSession.Settle(4);

            var themeManager = App.Services.GetRequiredService<IThemeManager>();
            Assert.Equal("nord", themeManager.ActiveThemeId);

            // Restore
            mainVm.ApplyThemeCommand.Execute("dark-plus");
            TestHeadlessSession.Settle(4);
        });
    }

    [Fact]
    public void ThemeManager_RegisterTokenContributor_InjectsCustomTokensAtRuntime()
    {
        TestHeadlessSession.Run(() =>
        {
            var themeManager = App.Services.GetRequiredService<IThemeManager>();
            var contributor = new DynamicCustomContributor();

            // Register contributor
            using var registration = themeManager.RegisterTokenContributor(contributor);
            TestHeadlessSession.Settle(4);

            if (Application.Current != null)
            {
                Assert.True(Application.Current.Resources.TryGetResource("DynamicPluginCustomBrush", null, out var brush));
                Assert.NotNull(brush);
            }

            // Unregister via Dispose
            registration.Dispose();
            TestHeadlessSession.Settle(4);

            Assert.DoesNotContain(themeManager.GetTokenContributors(), c => c.Id == contributor.Id);
        });
    }

    [Fact]
    public void SettingsViewModel_And_Navigation_SwitchesView_And_AppliesThemes()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var themeManager = App.Services.GetRequiredService<IThemeManager>();

            // 1. Verify nav.settings item exists
            var settingsItem = mainVm.NavigationItems.FirstOrDefault(n => n.Id == "nav.settings");
            Assert.NotNull(settingsItem);
            Assert.Equal("Settings & Appearance", settingsItem.Title);

            // 2. Open settings
            mainVm.OpenSettingsCommand.Execute(null);
            TestHeadlessSession.Settle(4);
            Assert.True(mainVm.IsSettingsActive);
            Assert.False(mainVm.IsLauncherActive);
            Assert.False(mainVm.IsPluginsActive);
            Assert.Equal(settingsItem, mainVm.SelectedNavItem);

            // 3. Apply Cyberpunk theme via SettingsVm
            mainVm.SettingsVm.ApplyThemeCommand.Execute("cyberpunk");
            TestHeadlessSession.Settle(4);
            Assert.Equal("cyberpunk", themeManager.ActiveThemeId);
            Assert.Equal("cyberpunk", mainVm.SettingsVm.ActiveThemeId);

            // 4. Test procedural custom harmony via SettingsVm
            mainVm.SettingsVm.BaseHue = 180f;
            mainVm.SettingsVm.SelectedHarmonyMode = Sdk.Theming.ColorHarmonyMode.Triadic;
            mainVm.SettingsVm.ApplyCustomHarmonicCommand.Execute(null);
            TestHeadlessSession.Settle(4);
            Assert.StartsWith("harmonic-", themeManager.ActiveThemeId);

            // 5. Reset to defaults
            mainVm.SettingsVm.ResetDefaultsCommand.Execute(null);
            TestHeadlessSession.Settle(4);
            Assert.Equal("dark-plus", themeManager.ActiveThemeId);

            // 6. Return to launcher
            mainVm.OpenLauncherCommand.Execute(null);
            TestHeadlessSession.Settle(4);
            Assert.True(mainVm.IsLauncherActive);
            Assert.False(mainVm.IsSettingsActive);
        });
    }

    private sealed class DynamicCustomContributor : IThemeTokenContributor
    {
        public string Id => "dynamic.custom";

        public void ContributeTokens(FryWorld.Sdk.Theming.Palette.GeneratedPalette palette, System.Collections.Generic.IDictionary<string, string> tokens)
        {
            tokens["DynamicPluginCustomBrush"] = "#445566";
        }

        public void ContributeTokens(ThemeDefinition theme, System.Collections.Generic.IDictionary<string, string> tokens)
        {
            tokens["DynamicPluginCustomBrush"] = "#445566";
        }
    }
}
