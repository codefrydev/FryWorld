// The code samples in src/FryWorld.Sdk/README.md, compiled and run here so the README cannot drift from the API.
// ReadmeSampleTests checks that each block between "README:<name>" markers appears in the README verbatim.
#pragma warning disable CS1591
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Testing;
using Xunit;

namespace FryWorld.Sdk.Tests.Samples;

// README:quickstart:start
public sealed class MyStudioPlugin : FryPluginBase
{
    public override string Id => "com.example.mystudio";
    public override string Name => "My Studio";

    public override IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => new Dictionary<string, PluginSettingDefinition>
    {
        ["FontSize"] = new() { Type = PluginSettingType.Integer, Label = "Font size", DefaultValue = 14 }
    };

    public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        var settings = ctx.GetPluginSettings();
        var shell = ctx.GetService<IShellService>();

        // A full-viewport page in the host's navigation rail
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "mystudio.page",
            Title = "My Studio",
            IconKind = "Wrench",
            ViewFactory = () => new TextBlock { Text = "Hello", FontSize = settings.Get<int>("FontSize") }
        });

        // A floating overlay, opened on demand
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = "mystudio.notes",
            Title = "Notes",
            ViewFactory = () => new TextBox { AcceptsReturn = true }
        });

        // A command palette entry (⌘K / Ctrl+K) that opens it
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "mystudio.open-notes",
            Title = "Open Notes",
            Command = new RelayCommand(() => shell.ShowOverlay("mystudio.notes"))
        });

        // Everything registered through ctx is removed automatically when the plugin unmounts.
        return Task.CompletedTask;
    }
}
// README:quickstart:end

// README:testing:start
public class MyStudioPluginTests
{
    [Fact]
    public async Task OpenNotes_ShowsTheNotesOverlay()
    {
        await using var host = await FryPluginTestHost.MountAsync<MyStudioPlugin>();

        await host.RunCommandAsync("mystudio.open-notes");

        Assert.Contains("mystudio.notes", host.Shell.OpenOverlays);
        await host.AssertCleanUnmountAsync(); // fails if anything outlives the plugin
    }
}
// README:testing:end
