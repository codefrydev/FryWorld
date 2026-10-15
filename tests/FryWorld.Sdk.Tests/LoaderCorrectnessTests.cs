using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Tests.TestSupport;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// How the loader finds, picks and reports on plugins on disk, and how uninstall stays inside the plugins folder.
/// </summary>
public sealed class LoaderCorrectnessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryWorldSdkLoader_" + Guid.NewGuid().ToString("N"));

    public LoaderCorrectnessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    /// <summary>Copies the fixture plugin into <paramref name="destination"/>, optionally editing its manifest.</summary>
    private static void CopyFixture(string destination, Action<JsonNode>? editManifest = null)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        if (editManifest != null)
        {
            var path = Path.Combine(destination, "plugin.json");
            var manifest = JsonNode.Parse(File.ReadAllText(path))!;
            editManifest(manifest);
            File.WriteAllText(path, manifest.ToJsonString());
        }
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    public void PluginIdValidator_RejectsIdsThatAreNotPlainFolderNames(string id)
    {
        Assert.Throws<ArgumentException>(() => PluginIdValidator.ValidateIdentifier(id));
    }

    [Theory]
    [InlineData("../victim")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task Uninstall_IdOutsideThePluginsFolder_IsRejectedAndDeletesNothing(string pluginId)
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        var victim = Path.Combine(_root, "victim");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(victim);
        File.WriteAllText(Path.Combine(pluginsDir, "keep.txt"), "keep");

        await using var host = new PluginHost();
        var marketplace = new PluginMarketplaceService(host, pluginsDir);

        await Assert.ThrowsAsync<ArgumentException>(() => marketplace.UninstallPluginAsync(pluginId));

        Assert.True(Directory.Exists(victim));
        Assert.True(File.Exists(Path.Combine(pluginsDir, "keep.txt")));
    }

    [Fact]
    public async Task Uninstall_UnloadsThePluginsAssemblies()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        CopyFixture(Path.Combine(pluginsDir, TestPaths.FixturePluginId, "1.2.3"));

        var package = Assert.Single(PluginAssemblyLoader.DiscoverAndLoadDirectory(pluginsDir));
        await using var host = new PluginHost();
        host.RegisterPlugin(package.Plugins[0]);
        await host.StartAsync();

        await new PluginMarketplaceService(host, pluginsDir).UninstallPluginAsync(TestPaths.FixturePluginId);

        // Other tests may have their own copy of the fixture loaded, so check this package specifically.
        Assert.False(PluginAssemblyLoader.TryGetPackage(package.Plugins[0], out _));
        Assert.DoesNotContain(package, PluginAssemblyLoader.ActivePackages);
        Assert.False(Directory.Exists(Path.Combine(pluginsDir, TestPaths.FixturePluginId)));
    }

    [Fact]
    public void EntryPoint_NotDeclared_PicksTheAssemblyThatReferencesTheSdk()
    {
        var dir = Path.Combine(_root, "guess");
        CopyFixture(dir, m => m["entryPoint"] = "");

        // Sorts before the fixture and is managed, but is not a plugin: the old "first *.dll" rule picked it.
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Microsoft.Extensions.Logging.Abstractions.dll"),
            Path.Combine(dir, "AAA.Decoy.dll"));

        var diagnostics = new List<PluginLoadDiagnostic>();
        var entry = PluginAssemblyLoader.FindEntryDllInDirectory(dir, diagnostics);

        Assert.Equal(TestPaths.FixturePluginAssemblyName + ".dll", Path.GetFileName(entry));
        Assert.Contains(diagnostics, d => d.Severity == PluginDiagnosticSeverity.Warning && d.Message.Contains("entryPoint"));
    }

    [Fact]
    public void VersionFolders_HighestVersionIsLoaded()
    {
        var pluginDir = Path.Combine(_root, "plugins", TestPaths.FixturePluginId);
        CopyFixture(Path.Combine(pluginDir, "1.9.0"));
        CopyFixture(Path.Combine(pluginDir, "1.10.0"));
        CopyFixture(Path.Combine(pluginDir, "2.0.0-beta"));

        var entry = PluginAssemblyLoader.ResolvePluginDirectoryEntryDll(pluginDir);

        Assert.Equal("2.0.0-beta", Path.GetFileName(Path.GetDirectoryName(entry)));

        Directory.Delete(Path.Combine(pluginDir, "2.0.0-beta"), recursive: true);
        entry = PluginAssemblyLoader.ResolvePluginDirectoryEntryDll(pluginDir);
        Assert.Equal("1.10.0", Path.GetFileName(Path.GetDirectoryName(entry)));
    }

    [Fact]
    public void Discovery_PackageAndStaleFolderOfSameId_LoadsThePluginOnce()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        CopyFixture(Path.Combine(pluginsDir, TestPaths.FixturePluginId, "0.9.0"));

        var staging = Path.Combine(_root, "staging");
        CopyFixture(staging);
        ZipFile.CreateFromDirectory(staging, Path.Combine(pluginsDir, "Fixture.fryplugin"));

        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(pluginsDir);
        try
        {
            var package = Assert.Single(packages);
            Assert.Contains(Path.Combine(TestPaths.FixturePluginId, "1.2.3"), package.AssemblyPath);
        }
        finally
        {
            foreach (var p in packages) p.Dispose();
        }
    }

    [Fact]
    public void Discovery_CorruptPackage_IsReportedNotSwallowed()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "Broken.fryplugin"), "this is not a zip archive");

        var diagnostics = new List<PluginLoadDiagnostic>();
        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(pluginsDir, diagnostics);

        Assert.Empty(packages);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(PluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.EndsWith("Broken.fryplugin", diagnostic.Path);
        Assert.NotNull(diagnostic.Exception);
    }

    [Fact]
    public void LoadFailure_ForAHostAssemblyVersion_BecomesAnAbiMismatch()
    {
        var missing = new FileNotFoundException("Could not load file or assembly.",
            "FryWorld.Sdk, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null");

        var translated = Assert.IsType<PluginAbiMismatchException>(PluginAssemblyLoader.TranslateLoadFailure(missing));

        Assert.Equal("FryWorld.Sdk", translated.AssemblyName);
        Assert.Equal(new Version(9, 0, 0, 0), translated.RequestedVersion);
        Assert.Equal(typeof(PluginHost).Assembly.GetName().Version, translated.HostVersion);
        Assert.Same(missing, translated.InnerException);
    }

    [Theory]
    [InlineData("Avalonia.Base", true)]
    [InlineData("Avalonia.Controls", true)]
    [InlineData("AvaloniaEdit", false)]               // only some hosts ship it: plugins carry it
    [InlineData("SkiaSharp", true)]
    [InlineData("SkiaSharp.NativeAssets.macOS", true)]
    [InlineData("CommunityToolkit.Mvvm", true)]
    [InlineData("FryWorld.Sdk", true)]
    [InlineData("Avalonia.Controls.WebView", false)]  // an extension package hosts do not ship: plugins carry it
    [InlineData("Avalonia.Controls.DataGrid", false)]
    [InlineData("Markdig", false)]
    public void HostProvidedList_NamesWhatHostsShip(string assembly, bool hostProvided)
    {
        Assert.Equal(hostProvided, CollectiblePluginLoadContext.IsHostProvided(assembly));
    }

    [Fact]
    public void LoadFailure_ForAPluginsOwnDependency_IsLeftAlone()
    {
        var missing = new FileNotFoundException("Could not load file or assembly.", "SomePluginDependency, Version=1.0.0.0");

        Assert.Same(missing, PluginAssemblyLoader.TranslateLoadFailure(missing));
    }
}
