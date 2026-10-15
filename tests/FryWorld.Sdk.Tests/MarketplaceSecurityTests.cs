using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Tests.TestSupport;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// Download verification, compatibility gates, updates and uninstall in the marketplace service.
/// </summary>
public sealed class MarketplaceSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryWorldSdkMarket_" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginsDir;

    public MarketplaceSecurityTests()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(_pluginsDir);
    }

    public void Dispose()
    {
        foreach (var package in PluginAssemblyLoader.ActivePackages.Where(p => p.AssemblyPath.StartsWith(_root)))
        {
            package.Dispose();
        }
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    /// <summary>Serves fixed responses by URL, or throws, without a network.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Responses { get; } = new();
        public Exception? Throw { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Throw != null) throw Throw;

            return Task.FromResult(Responses.TryGetValue(request.RequestUri!.ToString(), out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>Builds a .fryplugin of the fixture with an edited manifest.</summary>
    private string BuildPackage(string name, Action<JsonNode>? editManifest = null)
    {
        var staging = Path.Combine(_root, "staging-" + name);
        Directory.CreateDirectory(staging);
        foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory).Where(f => !f.EndsWith(".fryplugin")))
        {
            File.Copy(file, Path.Combine(staging, Path.GetFileName(file)), overwrite: true);
        }

        var manifestPath = Path.Combine(staging, "plugin.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        editManifest?.Invoke(manifest);
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var package = Path.Combine(_root, name + ".fryplugin");
        ZipFile.CreateFromDirectory(staging, package);
        return package;
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    // --- PluginVersion ---

    [Fact]
    public void PluginVersion_OrdersBySemanticVersioningRules()
    {
        var ordered = new[] { "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0", "1.0.1", "1.10.0", "2.0.0" };
        var parsed = ordered.Select(PluginVersion.Parse).ToList();

        for (var i = 1; i < parsed.Count; i++)
        {
            Assert.True(parsed[i - 1] < parsed[i], $"{ordered[i - 1]} should be older than {ordered[i]}");
        }
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1", "1.0.0")]
    [InlineData("1.2.3.4", "1.2.3")]
    [InlineData("2.0.0-beta.1+sha.abc", "2.0.0-beta.1+sha.abc")]
    public void PluginVersion_Parses(string text, string expected)
    {
        Assert.Equal(expected, PluginVersion.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("one.two")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3.4.5")]
    public void PluginVersion_RejectsNonVersions(string text)
    {
        Assert.False(PluginVersion.TryParse(text, out _));
    }

    [Fact]
    public void PluginVersion_BuildMetadataIsIgnoredWhenComparing()
    {
        Assert.Equal(PluginVersion.Parse("1.0.0+a"), PluginVersion.Parse("1.0.0+b"));
    }

    // --- Compatibility ---

    [Theory]
    [InlineData("1.0.0", null, null, true, PluginCompatibilityStatus.Compatible)]
    [InlineData("2.5.0", null, null, false, PluginCompatibilityStatus.HostUpgradeRequired)]
    [InlineData(null, "1.9.0", null, false, PluginCompatibilityStatus.HostTooNew)]
    [InlineData(null, null, "net99.0", false, PluginCompatibilityStatus.IncompatibleRuntime)]
    [InlineData(null, null, "net8.0", true, PluginCompatibilityStatus.Compatible)]
    [InlineData(null, null, "netstandard2.0", true, PluginCompatibilityStatus.Compatible)]
    [InlineData(null, null, "net10.0-windows", true, PluginCompatibilityStatus.Compatible)]
    [InlineData("2.0.0-beta", null, null, true, PluginCompatibilityStatus.Compatible)]
    public void Compatibility_ChecksHostVersionAndRuntime(string? min, string? max, string? tfm, bool compatible, PluginCompatibilityStatus status)
    {
        var result = PluginCompatibility.Check(min, max, tfm, hostVersion: new Version(2, 0, 0), hostTargetFramework: "net10.0");

        Assert.Equal(compatible, result.IsCompatible);
        Assert.Equal(status, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Theory]
    [InlineData("0.1.0", "0.1.0", true)]
    [InlineData("0.1.5", "0.1.0", true)]   // patch releases add no API
    [InlineData("0.0.2", "0.1.0", true)]   // older SDK: additive guarantee
    [InlineData("0.2.0", "0.1.3", false)]  // newer minor may use APIs the host lacks
    [InlineData("1.0.0", "0.9.0", false)]
    [InlineData(null, "0.1.0", true)]      // not stamped (project references, old builds)
    public void Compatibility_ChecksTheSdkThePluginWasBuiltAgainst(string? built, string host, bool compatible)
    {
        var result = PluginCompatibility.CheckSdkVersion(built, PluginVersion.Parse(host));

        Assert.Equal(compatible, result == null);
        if (!compatible) Assert.Equal(PluginCompatibilityStatus.HostSdkTooOld, result!.Value.Status);
    }

    // --- Downloads ---

    private (PluginMarketplaceService Service, FakeHandler Handler, PluginHost Host) CreateService(bool requireChecksum = false)
    {
        var handler = new FakeHandler();
        var host = new PluginHost();
        var service = new PluginMarketplaceService(host, _pluginsDir, new PluginMarketplaceOptions
        {
            CatalogUrl = "https://catalog.test/catalog.json",
            HttpClient = new HttpClient(handler),
            RequireChecksum = requireChecksum,
            HostVersion = new Version(1, 0, 0)
        });
        return (service, handler, host);
    }

    [Fact]
    public async Task Download_MatchingChecksum_IsKept()
    {
        var (service, handler, _) = CreateService();
        var body = Encoding.UTF8.GetBytes("package bytes");
        handler.Responses["https://cdn.test/p.fryplugin"] = body;

        var item = new MarketplacePluginItem
        {
            Id = "com.test.download", Version = "1.0.0", DownloadUrl = "https://cdn.test/p.fryplugin",
            Versions = { new MarketplacePluginVersion { Version = "1.0.0", DownloadUrl = "https://cdn.test/p.fryplugin", Sha256 = Sha256(body).ToLowerInvariant() } }
        };

        var progress = new List<double>();
        var path = await service.DownloadPackageAsync(item, progress.Add);

        Assert.Equal(body, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".partial"));
    }

    [Fact]
    public async Task Download_WrongChecksum_IsDeletedAndRefused()
    {
        var (service, handler, _) = CreateService();
        handler.Responses["https://cdn.test/evil.fryplugin"] = Encoding.UTF8.GetBytes("tampered");

        var item = new MarketplacePluginItem
        {
            Id = "com.test.tampered", Version = "1.0.0", DownloadUrl = "https://cdn.test/evil.fryplugin",
            Versions = { new MarketplacePluginVersion { Version = "1.0.0", Sha256 = Sha256(Encoding.UTF8.GetBytes("original")) } }
        };

        var error = await Assert.ThrowsAsync<PluginException>(() => service.DownloadPackageAsync(item));
        Assert.Contains("checksum", error.Message);

        var expectedPath = Path.Combine(Path.GetTempPath(), "FryWorld", "Downloads", "com.test.tampered_1.0.0.fryplugin");
        Assert.False(File.Exists(expectedPath));
        Assert.False(File.Exists(expectedPath + ".partial"));
    }

    [Fact]
    public async Task Download_BareItem_IsVerifiedAgainstTheCachedCatalog()
    {
        // Hosts build a bare item (no versions) for the release the user picked; the checksum comes from the catalog.
        var (service, handler, _) = CreateService();
        var body = Encoding.UTF8.GetBytes("release 2");
        handler.Responses["https://cdn.test/r2.fryplugin"] = body;
        handler.Responses["https://catalog.test/catalog.json"] = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new MarketplacePluginItem
            {
                Id = "com.test.catalog", Version = "2.0.0",
                Versions = { new MarketplacePluginVersion { Version = "2.0.0", DownloadUrl = "https://cdn.test/r2.fryplugin", Sha256 = Sha256(Encoding.UTF8.GetBytes("something else")) } }
            }
        });
        await service.FetchCatalogAsync();

        var bare = new MarketplacePluginItem { Id = "com.test.catalog", Version = "2.0.0", DownloadUrl = "https://cdn.test/r2.fryplugin" };
        await Assert.ThrowsAsync<PluginException>(() => service.DownloadPackageAsync(bare));
    }

    [Fact]
    public async Task Download_NoChecksum_IsRefusedWhenRequired()
    {
        var (service, handler, _) = CreateService(requireChecksum: true);
        handler.Responses["https://cdn.test/x.fryplugin"] = new byte[] { 1 };

        var item = new MarketplacePluginItem { Id = "com.test.nochecksum", Version = "1.0.0", DownloadUrl = "https://cdn.test/x.fryplugin" };
        await Assert.ThrowsAsync<PluginException>(() => service.DownloadPackageAsync(item));
    }

    [Theory]
    [InlineData("../../escape", "1.0.0")]
    [InlineData("com.test.ok", "../1.0.0")]
    public async Task Download_IdOrVersionThatEscapesTheFolder_IsRejected(string id, string version)
    {
        var (service, _, _) = CreateService();
        var item = new MarketplacePluginItem { Id = id, Version = version, DownloadUrl = "https://cdn.test/x.fryplugin" };

        await Assert.ThrowsAsync<ArgumentException>(() => service.DownloadPackageAsync(item));
    }

    [Fact]
    public async Task Catalog_Offline_FallsBackToTheCache_ButCancellationPropagates()
    {
        var (service, handler, _) = CreateService();
        handler.Throw = new HttpRequestException("offline");

        Assert.Empty(await service.FetchCatalogAsync(forceRefresh: true));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        handler.Throw = null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.FetchCatalogAsync(forceRefresh: true, cts.Token));
    }

    // --- Install, update, uninstall ---

    [Fact]
    public async Task Install_IncompatiblePlugin_IsRefusedBeforeExtracting()
    {
        var (service, _, host) = CreateService();
        var package = BuildPackage("future", m => m["minHostVersion"] = "99.0.0");

        var error = await Assert.ThrowsAsync<PluginIncompatibleException>(() => service.InstallPackageAsync(package));

        Assert.Equal(PluginCompatibilityStatus.HostUpgradeRequired, error.Result.Status);
        Assert.False(Directory.Exists(Path.Combine(_pluginsDir, TestPaths.FixturePluginId)));
        Assert.Null(host.GetPluginState(TestPaths.FixturePluginId));
    }

    [Fact]
    public async Task Install_NewVersion_ReplacesTheLoadedOne()
    {
        var (service, _, host) = CreateService();

        await service.InstallPackageAsync(BuildPackage("v1", m => m["version"] = "1.2.3"));
        var first = host.GetRegistration(TestPaths.FixturePluginId)!.Plugin;
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(TestPaths.FixturePluginId));
        Assert.Equal("1.2.3", service.GetInstalledVersion(TestPaths.FixturePluginId));

        await service.InstallPackageAsync(BuildPackage("v2", m => m["version"] = "1.3.0"));
        var second = host.GetRegistration(TestPaths.FixturePluginId)!.Plugin;

        Assert.NotSame(first, second);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(TestPaths.FixturePluginId));
        Assert.False(PluginAssemblyLoader.TryGetPackage(first, out _)); // old copy unloaded
        Assert.Single(host.Context.GetRegisteredNavigationItems(), n => n.Id == "fixture.page"); // not registered twice
        Assert.Equal("1.3.0", service.GetInstalledVersion(TestPaths.FixturePluginId));

        var oldVersionDir = Path.Combine(_pluginsDir, TestPaths.FixturePluginId, "1.2.3");
        Assert.True(!Directory.Exists(oldVersionDir) || PluginInstallLayout.IsRemoved(oldVersionDir));
    }

    [Fact]
    public async Task Uninstall_RemovesTheFolderAndTheSideloadedPackage()
    {
        var (service, _, host) = CreateService();
        File.Copy(BuildPackage("sideload"), Path.Combine(_pluginsDir, "Sideloaded.fryplugin"));

        foreach (var package in PluginAssemblyLoader.DiscoverAndLoadDirectory(_pluginsDir))
        {
            foreach (var plugin in package.Plugins) host.RegisterPlugin(plugin);
        }
        await host.StartAsync();
        Assert.True(service.IsPluginInstalled(TestPaths.FixturePluginId));

        // A package for a different plugin, dropped in later.
        File.Copy(BuildPackage("other", m => m["id"] = "com.test.other"), Path.Combine(_pluginsDir, "Other.fryplugin"));

        await service.UninstallPluginAsync(TestPaths.FixturePluginId);

        Assert.False(service.IsPluginInstalled(TestPaths.FixturePluginId));
        Assert.Null(service.GetInstalledVersion(TestPaths.FixturePluginId));
        Assert.False(File.Exists(Path.Combine(_pluginsDir, "Sideloaded.fryplugin")));
        Assert.True(File.Exists(Path.Combine(_pluginsDir, "Other.fryplugin"))); // another plugin's package is left alone
    }

    [Fact]
    public void Discovery_TwoPackagesForTheSamePlugin_LoadsOneAndReportsTheOther()
    {
        File.Copy(BuildPackage("first"), Path.Combine(_pluginsDir, "Fixture.fryplugin"));
        File.Copy(BuildPackage("again"), Path.Combine(_pluginsDir, "Fixture (1).fryplugin"));

        var diagnostics = new List<PluginLoadDiagnostic>();
        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(_pluginsDir, diagnostics);

        Assert.Single(packages);
        Assert.Contains(diagnostics, d => d.Message.Contains("already provides") && d.Path!.EndsWith("Fixture.fryplugin"));
        foreach (var p in packages) p.Dispose();
    }

    [Fact]
    public void Load_ManifestIdThatDiffersFromThePluginsId_IsReported()
    {
        var installDir = Path.Combine(_pluginsDir, "com.test.mismatch", "1.0.0");
        ZipFile.ExtractToDirectory(BuildPackage("mismatch", m => m["id"] = "com.test.mismatch"), installDir);

        var diagnostics = new List<PluginLoadDiagnostic>();
        using var package = PluginAssemblyLoader.LoadAssembly(Path.Combine(installDir, TestPaths.FixturePluginAssemblyName + ".dll"), null, diagnostics);

        Assert.Contains(diagnostics, d => d.Message.Contains("com.test.mismatch") && d.Message.Contains(TestPaths.FixturePluginId));
    }

    [Fact]
    public void Discovery_SkipsAndCleansUpWhatWasMarkedForRemoval()
    {
        var pluginDir = Path.Combine(_pluginsDir, TestPaths.FixturePluginId);
        foreach (var version in new[] { "1.2.3", "1.5.0" })
        {
            var dir = Path.Combine(pluginDir, version);
            Directory.CreateDirectory(dir);
            foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory).Where(f => !f.EndsWith(".fryplugin")))
            {
                File.Copy(file, Path.Combine(dir, Path.GetFileName(file)));
            }
        }

        // 1.5.0 was superseded (say, by a downgrade): it must not load even though it is the highest.
        File.WriteAllText(Path.Combine(pluginDir, "1.5.0", PluginInstallLayout.RemoveMarkerFileName), "");
        Assert.EndsWith(Path.Combine("1.2.3", TestPaths.FixturePluginAssemblyName + ".dll"), PluginAssemblyLoader.ResolvePluginDirectoryEntryDll(pluginDir));

        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(_pluginsDir);
        Assert.Contains(Path.Combine("1.2.3", ""), Assert.Single(packages).AssemblyPath);
        Assert.False(Directory.Exists(Path.Combine(pluginDir, "1.5.0")));
        foreach (var p in packages) p.Dispose();

        // An uninstalled plugin is neither loaded nor left on disk.
        File.WriteAllText(Path.Combine(pluginDir, PluginInstallLayout.UninstallMarkerFileName), "");
        Assert.Empty(PluginAssemblyLoader.DiscoverAndLoadDirectory(_pluginsDir));
        Assert.False(Directory.Exists(pluginDir));
    }

    [Fact]
    public async Task Startup_SkipsPluginsThatNeedANewerHost()
    {
        var installDir = Path.Combine(_pluginsDir, TestPaths.FixturePluginId, "1.2.3");
        ZipFile.ExtractToDirectory(BuildPackage("needs-new-host", m => m["minHostVersion"] = "5.0.0"), installDir);

        await using var host = new PluginHost();
        var startup = new PluginStartup(host, _pluginsDir, hostVersion: new Version(1, 0, 0));
        startup.Start();
        await startup.Completion.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Null(host.GetPluginState(TestPaths.FixturePluginId));
        var diagnostic = Assert.Single(host.Diagnostics, d => d.PluginId == TestPaths.FixturePluginId);
        Assert.Equal(PluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("5.0.0", diagnostic.Message);
    }

    [Fact]
    public async Task HasUpdateAvailable_UnderstandsPrereleases()
    {
        var (service, handler, _) = CreateService();
        handler.Responses["https://catalog.test/catalog.json"] = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new MarketplacePluginItem { Id = "com.test.update", Version = "2.0.0" }
        });
        await service.FetchCatalogAsync();

        Assert.True(service.HasUpdateAvailable("com.test.update", "2.0.0-rc.1"));
        Assert.True(service.HasUpdateAvailable("com.test.update", "1.10.0"));
        Assert.False(service.HasUpdateAvailable("com.test.update", "2.0.0"));
        Assert.False(service.HasUpdateAvailable("com.test.update", "v2.0.0"));
        Assert.False(service.HasUpdateAvailable("com.test.unknown", "1.0.0"));
    }
}
