using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Testing;
using FryWorld.Sdk.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// The plugin-author surface added in Phase 3: shell service, plugin settings, the test host, the DI
/// extension, logging, packaging targets and the default plugin version.
/// </summary>
public sealed class DeveloperExperienceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryWorldSdkDx_" + Guid.NewGuid().ToString("N"));

    public DeveloperExperienceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    /// <summary>A small but complete plugin: a page, an overlay, a command that opens the overlay, settings.</summary>
    private sealed class SamplePlugin : FryPluginBase
    {
        public const string PluginId = "test.sample";
        public IPluginSettings? Settings { get; private set; }
        public List<string> ChangedKeys { get; } = new();

        public override string Id => PluginId;
        public override string Name => "Sample";

        public override IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => new Dictionary<string, PluginSettingDefinition>
        {
            ["Speed"] = new() { Type = PluginSettingType.Select, DefaultValue = "Normal", Options = new() { "Normal", "Fast" } },
            ["Volume"] = new() { Type = PluginSettingType.Integer, DefaultValue = 7L },
            ["FromJson"] = new() { Type = PluginSettingType.Boolean, DefaultValue = JsonDocument.Parse("true").RootElement }
        };

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            Settings = ctx.GetPluginSettings();
            Settings.Changed += key => ChangedKeys.Add(key);

            ctx.RegisterNavigationItem(new NavigationItemDescriptor { Id = "sample.page", Title = "Sample", ViewFactory = () => new Avalonia.Controls.TextBlock() });
            ctx.RegisterOverlay(new OverlayDescriptor { Id = "sample.overlay", Title = "Overlay", ViewFactory = () => new Avalonia.Controls.TextBlock() });
            ctx.RegisterCommand(new CommandPaletteDescriptor
            {
                Id = "sample.open",
                Title = "Open overlay",
                Command = new RelayCommand(() => ctx.GetService<IShellService>().ShowOverlay("sample.overlay"))
            });
            ctx.RegisterSerial<string>("sample.pipeline", _ => Task.CompletedTask);
            return Task.CompletedTask;
        }
    }

    /// <summary>Registers a pipeline handler straight on the pipeline manager, bypassing the plugin's scope.</summary>
    private sealed class LeakyPlugin : FryPluginBase
    {
        public override string Id => "test.leaky";
        public override string Name => "Leaky";

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            ctx.Pipelines.RegisterSerial<string>("leaky.pipeline", _ => Task.CompletedTask);
            return Task.CompletedTask;
        }
    }

    private sealed class VersionlessPlugin : FryPluginBase
    {
        public override string Id => "test.versionless";
        public override string Name => "Versionless";
        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task TestHost_RunsCommands_AndRecordsShellCalls()
    {
        await using var host = await FryPluginTestHost.MountAsync<SamplePlugin>();

        Assert.Equal(PluginLifecycleState.Active, host.State);
        await host.RunCommandAsync("sample.open");

        Assert.Contains("sample.overlay", host.Shell.OpenOverlays);
        Assert.Equal(new[] { "ShowOverlay:sample.overlay" }, host.Shell.Calls);
        Assert.False(host.Shell.ShowOverlay("not.registered"));
    }

    [Fact]
    public async Task TestHost_CleanPlugin_PassesTheLeakCheck()
    {
        await using var host = await FryPluginTestHost.MountAsync<SamplePlugin>();
        await host.AssertCleanUnmountAsync();
    }

    [Fact]
    public async Task TestHost_RegistrationOutsideTheScope_IsReportedAsALeak()
    {
        await using var host = await FryPluginTestHost.MountAsync<LeakyPlugin>();

        var leak = await Assert.ThrowsAsync<FryPluginLeakException>(() => host.AssertCleanUnmountAsync());
        Assert.Contains(leak.Leftovers, l => l.Contains("leaky.pipeline"));
    }

    [Fact]
    public async Task TestHost_UnknownCommand_ListsWhatIsRegistered()
    {
        await using var host = await FryPluginTestHost.MountAsync<SamplePlugin>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunCommandAsync("nope"));
        Assert.Contains("sample.open", error.Message);
    }

    [Fact]
    public async Task PluginSettings_UseSchemaDefaults_StoreValues_AndReportChanges()
    {
        await using var host = await FryPluginTestHost.MountAsync<SamplePlugin>();
        var plugin = (SamplePlugin)host.Plugin;
        var settings = plugin.Settings!;

        Assert.Equal(SamplePlugin.PluginId, settings.PluginId);
        Assert.Equal("Normal", settings.Get<string>("Speed"));
        Assert.Equal(7, settings.Get<int>("Volume"));
        Assert.True(settings.Get<bool>("FromJson"));
        Assert.Null(settings.Get<string>("NotInSchema"));
        Assert.Equal("fallback", settings.Get("NotInSchema", "fallback"));

        settings.Set("Speed", "Fast");
        Assert.Equal("Fast", settings.Get<string>("Speed"));
        Assert.Equal("Fast", host.Settings.GetSetting(SamplePlugin.PluginId, "Speed", "?"));

        // A change made by the host (for example its settings form) reaches the plugin too.
        host.Settings.SetSetting(SamplePlugin.PluginId, "Volume", 3);
        host.Settings.SetSetting("other.plugin", "Volume", 3);
        Assert.Equal(new[] { "Speed", "Volume" }, plugin.ChangedKeys);

        // After unmount the plugin no longer hears about changes, so it can unload.
        await host.UnmountAsync();
        host.Settings.SetSetting(SamplePlugin.PluginId, "Speed", "Normal");
        Assert.Equal(2, plugin.ChangedKeys.Count);
    }

    [Fact]
    public void PluginSettings_OnAContextWithNoPlugin_ExplainsWhatToDo()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new FryPluginContext().GetPluginSettings());
        Assert.Contains("ApplyAsync", error.Message);

        var explicitSettings = new FryPluginContext().GetPluginSettings("some.plugin");
        Assert.Equal("some.plugin", explicitSettings.PluginId);
    }

    [Fact]
    public async Task FileSettingsStore_PersistsAcrossInstances_InTheHostsFormat()
    {
        var path = Path.Combine(_root, "settings.json");
        var changes = new List<string>();

        using (var store = new FileSettingsStore(path))
        {
            store.SettingChanged += (plugin, key) => changes.Add($"{plugin}:{key}");
            store.SetSetting("com.example", "Volume", 5);
            store.SetSetting("com.example", "Volume", 5); // unchanged: no event, no write
            store.SetSetting("com.example", "Name", "fry");
            await store.SaveAsync();
        }

        Assert.Equal(new[] { "com.example:Volume", "com.example:Name" }, changes);

        // Same layout as the FryWorld host's settings.json, so the host can switch stores without losing settings.
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        Assert.Equal("5", raw["com.example:Volume"]);
        Assert.Equal("\"fry\"", raw["com.example:Name"]);

        using var reopened = new FileSettingsStore(path);
        Assert.Equal(5, reopened.GetSetting("com.example", "Volume", 0));
        Assert.Equal("fry", reopened.GetSetting("com.example", "Name", ""));
        Assert.Equal(0, reopened.GetSetting("com.example", "Name", 0)); // wrong type -> default, not an exception

        Assert.True(reopened.RemovePluginSettings("com.example"));
        Assert.Equal(0, reopened.GetSetting("com.example", "Volume", 0));
    }

    [Fact]
    public void FileSettingsStore_FlushesOnItsOwnShortlyAfterAChange()
    {
        var path = Path.Combine(_root, "debounced.json");
        using var store = new FileSettingsStore(path);

        store.SetSetting("p", "k", 1);
        Assert.False(File.Exists(path));

        var deadline = DateTime.UtcNow + FileSettingsStore.FlushDelay + TimeSpan.FromSeconds(5);
        while (!File.Exists(path) && DateTime.UtcNow < deadline) Thread.Sleep(20);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void FileSettingsStore_CorruptFile_StartsEmpty()
    {
        var path = Path.Combine(_root, "corrupt.json");
        File.WriteAllText(path, "{ not json");

        using var store = new FileSettingsStore(path);
        Assert.Equal(42, store.GetSetting("p", "k", 42));
    }

    [Fact]
    public void FryPluginBase_Version_DefaultsToTheAssemblyVersion()
    {
        var expected = typeof(DeveloperExperienceTests).Assembly.GetName().Version!;
        var version = new VersionlessPlugin().Version;

        Assert.Equal(new Version(expected.Major, expected.Minor, expected.Build), version);
    }

    [Fact]
    public async Task AddFryWorldPluginHost_WiresTheHost_AndPluginsResolveHostServices()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        var installDir = Path.Combine(pluginsDir, TestPaths.FixturePluginId, "1.2.3");
        Directory.CreateDirectory(installDir);
        foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory, "*.*").Where(f => !f.EndsWith(".fryplugin")))
        {
            File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)));
        }

        var services = new ServiceCollection();
        services.AddSingleton<INotificationService, RecordingNotificationService>();
        services.AddFryWorldPluginHost(o =>
        {
            o.PluginsDirectory = pluginsDir;
            o.SettingsFilePath = Path.Combine(_root, "settings.json");
        });
        await using var provider = services.BuildServiceProvider();

        var host = provider.GetRequiredService<PluginHost>();
        Assert.Same(host.Context, provider.GetRequiredService<IFryPluginContext>());
        Assert.IsType<FileSettingsStore>(provider.GetRequiredService<ISettingsManager>());
        Assert.NotNull(provider.GetRequiredService<FryWorld.Sdk.Marketplace.IPluginMarketplaceService>());

        // Services registered in the container are visible to plugins through the context.
        Assert.True(host.Context.HasService<INotificationService>());

        var startup = provider.GetRequiredService<PluginStartup>();
        startup.Start();
        await startup.Completion.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(TestPaths.FixturePluginId));
        Assert.DoesNotContain(host.Diagnostics, d => d.Severity == PluginDiagnosticSeverity.Error);

        await host.UnmountPluginAsync(TestPaths.FixturePluginId);
        foreach (var package in PluginAssemblyLoader.ActivePackages.Where(p => p.AssemblyPath.StartsWith(_root)))
        {
            package.Dispose();
        }
    }

    [Fact]
    public async Task PluginStartup_ReportsLoadProblemsInTheHostsDiagnostics()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(pluginsDir);
        File.WriteAllText(Path.Combine(pluginsDir, "Broken.fryplugin"), "not a zip");

        await using var host = new PluginHost();
        var startup = new PluginStartup(host, pluginsDir);
        startup.Start();
        startup.Start(); // second call is ignored
        await startup.Completion.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Contains(host.Diagnostics, d => d.Path != null && d.Path.EndsWith("Broken.fryplugin"));
    }

    [Fact]
    public void AppLogService_CreateLogger_WritesToTheLog()
    {
        var category = "DxTest-" + Guid.NewGuid().ToString("N");
        var logger = AppLogService.Instance.CreateLogger(category);

        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, "Something {What}", "happened");

        var entry = Assert.Single(AppLogService.Instance.GetRecentEntries(1000), e => e.Category == category);
        Assert.Equal(AppLogLevel.Warning, entry.Level);
        Assert.Equal("Something happened", entry.Message);
    }

    [Fact]
    public void BuildTargets_PackageThePluginWithoutHostAssemblies_AndStampTheManifest()
    {
        var package = Path.Combine(TestPaths.FixturePluginDirectory, TestPaths.FixturePluginAssemblyName + ".fryplugin");
        Assert.True(File.Exists(package), "The fixture's build should have produced a .fryplugin through FryWorld.Sdk.targets.");

        using var archive = ZipFile.OpenRead(package);
        var entries = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();

        Assert.Contains(TestPaths.FixturePluginAssemblyName + ".dll", entries);
        Assert.Contains(TestPaths.FixturePluginAssemblyName + ".deps.json", entries);
        Assert.Contains("plugin.json", entries);
        Assert.DoesNotContain("FryWorld.Sdk.dll", entries);
        Assert.DoesNotContain(entries, e => e.StartsWith("Avalonia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.EndsWith(".pdb"));

        using var manifestStream = archive.GetEntry("plugin.json")!.Open();
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestStream)!;
        Assert.Equal(TestPaths.FixturePluginId, manifest.Id);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal("net10.0", manifest.TargetFramework);
        Assert.Equal(TestPaths.FixturePluginAssemblyName + ".dll", manifest.EntryPoint);
    }

    [Fact]
    public void BuildTargets_PackageLoadsInAHost()
    {
        var package = Path.Combine(TestPaths.FixturePluginDirectory, TestPaths.FixturePluginAssemblyName + ".fryplugin");
        using var result = FryPluginPackageLoader.UnpackAndLoad(package, Path.Combine(_root, "plugins"));

        Assert.Equal(TestPaths.FixturePluginId, Assert.Single(result.AssemblyPackage.Plugins).Id);
    }
}
