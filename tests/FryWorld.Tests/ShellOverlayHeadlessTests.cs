using System;
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
public class ShellOverlayHeadlessTests
{
    public ShellOverlayHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void ShellOverlayHost_DisplaysAndClosesActiveOverlays()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();

            var descriptor = new OverlayDescriptor
            {
                Id = "test.overlay.hud",
                Title = "Resource Monitor HUD",
                ViewFactory = () => new TextBlock { Text = "CPU 12% | RAM 48MB" },
                IconKind = "Speedometer",
                DefaultWidth = 320,
                DefaultHeight = 180
            };

            mainVm.OpenOverlay(descriptor);
            Assert.Contains(mainVm.ActiveOverlays, o => o.Descriptor.Id == "test.overlay.hud");

            var overlayVm = mainVm.ActiveOverlays.First(o => o.Descriptor.Id == "test.overlay.hud");
            Assert.Equal("Resource Monitor HUD", overlayVm.Title);

            var host = new ShellOverlayHost { DataContext = mainVm };
            var window = TestHeadlessSession.ShowOffscreen(host, 1280, 820);

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

            // Close overlay
            mainVm.CloseOverlay(overlayVm);
            Assert.DoesNotContain(mainVm.ActiveOverlays, o => o.Descriptor.Id == "test.overlay.hud");
        });
    }
}
