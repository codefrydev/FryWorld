using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Testing;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// The compatibility promise, tested with real binaries: plugin packages published before this SDK release, never
/// rebuilt, still load, mount and unload. Package validation checks the API surface at pack time; this checks the
/// runtime behaviour those plugins depend on.
/// </summary>
public sealed class LegacyPluginCompatibilityTests : IDisposable
{
    private static readonly string LegacyDir = Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy");
    private readonly string _pluginsDir = Path.Combine(Path.GetTempPath(), "FryWorldSdkLegacy_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        foreach (var package in PluginAssemblyLoader.ActivePackages.Where(p => p.AssemblyPath.StartsWith(_pluginsDir)))
        {
            package.Dispose();
        }
        try { Directory.Delete(_pluginsDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task Snake_BuiltWithSdk002_LoadsMountsAndUnmountsCleanly()
    {
        using var result = FryPluginPackageLoader.UnpackAndLoad(Path.Combine(LegacyDir, "Snake-built-with-sdk-0.0.2.fryplugin"), _pluginsDir);
        var plugin = Assert.Single(result.AssemblyPackage.Plugins);
        Assert.Equal("com.fryworld.snake", plugin.Id);

        // Mounted the way a host does, with the test host's stand-in services; the leak check covers unmount.
        await using var host = await FryPluginTestHost.MountAsync(plugin);

        Assert.Contains(host.Context.GetRegisteredOverlays(), o => o.Id == "com.fryworld.snake");
        Assert.Contains(host.Context.GetRegisteredCommands(), c => c.Id == "cmd.overlay.snake");
        Assert.Contains(host.Context.GetRegisteredStatusBarWidgets(), w => w.Id == "fryworld.status.snake");
        Assert.Empty(host.Host.Diagnostics);

        // Its old command navigates by reflecting into the FryWorld host; without that host it must simply do nothing.
        await host.RunCommandAsync("cmd.overlay.snake");

        await host.AssertCleanUnmountAsync();
    }
}
