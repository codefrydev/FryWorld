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
public class CommandPaletteHeadlessTests
{
    public CommandPaletteHeadlessTests()
    {
        TestHeadlessSession.EnsureInitialized();
    }

    [Fact]
    public void CommandPalette_OpensFiltersAndCloses()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            var host = App.Services.GetRequiredService<PluginHost>();

            // Register a test command palette item
            host.Context.RegisterCommand(new CommandPaletteDescriptor
            {
                Id = "test.palette.cmd",
                Title = "Run Test Palette Action",
                ShortcutKey = "Ctrl+Shift+T",
                Category = "Testing"
            });

            // Open palette
            mainVm.OpenCommandPaletteCommand.Execute(null);
            Assert.True(mainVm.IsCommandPaletteOpen);

            var paletteVm = mainVm.CommandPaletteVm;
            Assert.NotEmpty(paletteVm.FilteredCommands);

            // Search for the test command
            paletteVm.SearchQuery = "Palette Action";
            Assert.Contains(paletteVm.FilteredCommands, c => c.Title == "Run Test Palette Action");

            // Close palette
            paletteVm.SearchQuery = string.Empty;
            mainVm.IsCommandPaletteOpen = false;
            Assert.False(mainVm.IsCommandPaletteOpen);
        });
    }

    [Fact]
    public void CommandPaletteDialog_RendersOffscreen()
    {
        TestHeadlessSession.Run(() =>
        {
            var mainVm = App.Services.GetRequiredService<MainViewModel>();
            mainVm.OpenCommandPaletteCommand.Execute(null);

            var dialog = new CommandPaletteDialog { DataContext = mainVm.CommandPaletteVm };
            var window = TestHeadlessSession.ShowOffscreen(dialog, 700, 500);

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
}
