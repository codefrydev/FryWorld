using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Tests.TestSupport;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// Loads the fixture plugin from disk into its own load context, the same way the host loads an
/// installed plugin.
/// </summary>
public sealed class FixturePluginLoadingTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FryWorldSdkTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void FixturePlugin_IsBuiltNextToTheTests()
    {
        Assert.True(File.Exists(TestPaths.FixturePluginAssembly), $"Missing fixture plugin at {TestPaths.FixturePluginAssembly}");
        Assert.False(File.Exists(Path.Combine(TestPaths.FixturePluginDirectory, "FryWorld.Sdk.dll")),
            "The fixture must not ship FryWorld.Sdk.dll; the host provides it.");
    }

    [Fact]
    public async Task LoadAssembly_MountsAndUnmountsFixtureCleanly()
    {
        using var package = PluginAssemblyLoader.LoadAssembly(TestPaths.FixturePluginAssembly);

        var plugin = Assert.Single(package.Plugins);
        Assert.Equal(TestPaths.FixturePluginId, plugin.Id);
        Assert.True(package.IsCollectible);

        // The plugin type comes from the isolated context; IFryPlugin comes from the host's copy of the SDK.
        Assert.NotSame(System.Runtime.Loader.AssemblyLoadContext.Default,
            System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly));

        await using var host = new PluginHost();
        host.RegisterPlugin(plugin);
        await host.StartAsync();

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(plugin.Id));
        Assert.Contains(host.Context.GetRegisteredNavigationItems(), n => n.Id == "fixture.page");
        Assert.Contains(host.Context.GetRegisteredOverlays(), o => o.Id == "fixture.overlay");
        Assert.Contains(host.Context.GetRegisteredCommands(), c => c.Id == "fixture.command");

        var greeterType = plugin.GetType().Assembly.GetType("FryWorld.Sdk.FixturePlugin.IFixtureGreeter", throwOnError: true)!;
        Assert.True(host.Context.HasService(greeterType));

        var log = new List<string>();
        await host.Context.ExecuteSerialAsync("fixture.pipeline", log);
        Assert.Equal(new[] { TestPaths.FixturePluginId }, log);

        await host.UnmountPluginAsync(plugin.Id);

        Assert.Empty(host.Context.GetRegisteredNavigationItems());
        Assert.Empty(host.Context.GetRegisteredOverlays());
        Assert.Empty(host.Context.GetRegisteredCommands());
        Assert.False(host.Context.HasService(greeterType));

        log.Clear();
        await host.Context.ExecuteSerialAsync("fixture.pipeline", log);
        Assert.Empty(log);
    }

    [Fact]
    public void DiscoverAndLoadDirectory_FindsVersionedPluginFolder()
    {
        // Installed layout: <plugins>/<id>/<version>/plugin.json + assemblies
        var installDir = Path.Combine(_tempDir, TestPaths.FixturePluginId, "1.2.3");
        CopyDirectory(TestPaths.FixturePluginDirectory, installDir);

        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(_tempDir);
        try
        {
            var package = Assert.Single(packages);
            Assert.Equal(TestPaths.FixturePluginId, Assert.Single(package.Plugins).Id);
        }
        finally
        {
            foreach (var p in packages) p.Dispose();
        }
    }

    [Fact]
    public void UnpackAndLoad_ExtractsFryPluginArchive()
    {
        var packageFile = Path.Combine(_tempDir, "Fixture.fryplugin");
        Directory.CreateDirectory(_tempDir);
        ZipFile.CreateFromDirectory(TestPaths.FixturePluginDirectory, packageFile);

        var pluginsDir = Path.Combine(_tempDir, "plugins");
        using var result = FryPluginPackageLoader.UnpackAndLoad(packageFile, pluginsDir);

        Assert.Equal(TestPaths.FixturePluginId, result.Manifest.Id);
        Assert.Equal("1.2.3", result.Manifest.Version);
        Assert.Equal(Path.Combine(pluginsDir, TestPaths.FixturePluginId, "1.2.3"), result.InstallDirectory);
        Assert.Equal(TestPaths.FixturePluginId, Assert.Single(result.AssemblyPackage.Plugins).Id);
    }

    [Fact]
    public void CollectiblePlugin_IsUnloadedAfterUnmountAndDispose()
    {
        var contextRef = LoadMountUnmountAndDispose(TestPaths.FixturePluginAssembly);

        for (var i = 0; contextRef.IsAlive && i < 20; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(contextRef.IsAlive, "The plugin's load context is still referenced after unload, so its assemblies stay in memory.");
    }

    // Kept out of line so no local in the test method keeps the plugin reachable.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadMountUnmountAndDispose(string assemblyPath)
    {
        var package = PluginAssemblyLoader.LoadAssembly(assemblyPath, isCollectibleOverride: true);
        var contextRef = new WeakReference(package.Context);

        using (var host = new PluginHost())
        {
            foreach (var plugin in package.Plugins) host.RegisterPlugin(plugin);
            host.StartAsync().GetAwaiter().GetResult();
            host.UnmountPluginAsync(TestPaths.FixturePluginId).GetAwaiter().GetResult();
        }

        package.Dispose();
        return contextRef;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
    }
}
