using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.FixturePlugin;

/// <summary>
/// Service contract that lives inside the plugin's own assembly, so it is only reachable through the
/// plugin's load context.
/// </summary>
public interface IFixtureGreeter
{
    string Greet(string name);
}

public sealed class FixtureGreeter : IFixtureGreeter
{
    public string Greet(string name) => $"Hello, {name}";
}

/// <summary>
/// Registers one of each common capability so tests can check that mounting adds them and unmounting
/// removes every one of them.
/// </summary>
public sealed class FixturePlugin : FryPluginBase
{
    public const string PluginId = "com.fryworld.tests.fixture";
    public const string PipelineName = "fixture.pipeline";

    public override string Id => PluginId;
    public override string Name => "Fixture Plugin";
    public override Version Version => new(1, 2, 3);

    public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        ctx.RegisterService<IFixtureGreeter>(new FixtureGreeter());

        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "fixture.page",
            Title = "Fixture Page",
            ViewFactory = () => new TextBlock { Text = "Fixture" }
        });

        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = "fixture.overlay",
            Title = "Fixture Overlay",
            ViewFactory = () => new TextBlock { Text = "Fixture overlay" }
        });

        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "fixture.command",
            Title = "Fixture Command"
        });

        // Host-owned context type, so the host can run this pipeline without referencing the plugin.
        ctx.RegisterSerial<List<string>>(PipelineName, log =>
        {
            log.Add(PluginId);
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }
}
