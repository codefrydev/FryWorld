using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Pipelines;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Tests;
using FryWorld.Sdk.Tests.TestSupport;
using Xunit;

[assembly: FryPluginEntry(typeof(PerformanceBehaviourTests.AttributedPlugin))]

namespace FryWorld.Sdk.Tests;

/// <summary>
/// The behaviour behind the Phase 2 speed-ups: snapshots, batched notifications, typed pipelines,
/// extract-once installs, cheap entry-type discovery and parallel mounting.
/// </summary>
public sealed class PerformanceBehaviourTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryWorldSdkPerf_" + Guid.NewGuid().ToString("N"));

    public PerformanceBehaviourTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    public sealed class AttributedPlugin : FryPluginBase
    {
        public override string Id => "test.attributed";
        public override string Name => "Attributed";
        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class DelegatePlugin : FryPluginBase
    {
        private readonly Func<IFryPluginContext, CancellationToken, Task> _apply;

        public DelegatePlugin(string id, Func<IFryPluginContext, CancellationToken, Task> apply,
            Type[]? requires = null, Type[]? provides = null)
        {
            Id = id;
            _apply = apply;
            RequiredServices = requires ?? Array.Empty<Type>();
            ProvidedServices = provides ?? Array.Empty<Type>();
        }

        public override string Id { get; }
        public override string Name => Id;
        public override IReadOnlyList<Type> RequiredServices { get; }
        public override IReadOnlyList<Type> ProvidedServices { get; }
        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default) => _apply(ctx, ct);
    }

    private sealed class RecordingSyncContext : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = new();
        public override void Post(SendOrPostCallback d, object? state) => Posted.Add((d, state));
        public void RunAll()
        {
            foreach (var (callback, state) in Posted.ToList()) callback(state);
            Posted.Clear();
        }
    }

    private static CommandPaletteDescriptor Command(string id) => new() { Id = id, Title = id };

    [Fact]
    public void Registry_Read_ReturnsTheSameSnapshotUntilSomethingChanges()
    {
        var ctx = new FryPluginContext();
        ctx.RegisterCommand(Command("a"));

        var first = ctx.GetRegisteredCommands();
        Assert.Same(first, ctx.GetRegisteredCommands());

        ctx.RegisterCommand(Command("b"));
        var second = ctx.GetRegisteredCommands();
        Assert.NotSame(first, second);
        Assert.Single(first);
        Assert.Equal(new[] { "a", "b" }, second.Select(c => c.Id));
        Assert.Throws<NotSupportedException>(() => ((IList<CommandPaletteDescriptor>)second).Add(Command("c")));
    }

    [Fact]
    public void Registry_OrderedRegistries_SortStablyByOrder()
    {
        var ctx = new FryPluginContext();
        foreach (var (id, order) in new[] { ("late", 200), ("first", 10), ("tie.1", 100), ("tie.2", 100) })
        {
            ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor { Id = id, Order = order });
        }

        Assert.Equal(new[] { "first", "tie.1", "tie.2", "late" }, ctx.GetRegisteredStatusBarWidgets().Select(w => w.Id));
    }

    [Fact]
    public void DeferChangeNotifications_RaisesEachChangedRegistryOnce()
    {
        var ctx = new FryPluginContext();
        var commandEvents = 0;
        var toolEvents = 0;
        var overlayEvents = 0;
        ctx.CommandsChanged += () => commandEvents++;
        ctx.ToolsChanged += () => toolEvents++;
        ctx.OverlaysChanged += () => overlayEvents++;

        using (ctx.DeferChangeNotifications())
        {
            using (ctx.DeferChangeNotifications())
            {
                for (var i = 0; i < 5; i++) ctx.RegisterCommand(Command($"cmd.{i}"));
            }
            ctx.RegisterTool(new ToolDescriptor { Id = "tool", Title = "Tool" });
            Assert.Equal(0, commandEvents);
        }

        Assert.Equal(1, commandEvents);
        Assert.Equal(1, toolEvents);
        Assert.Equal(0, overlayEvents);
    }

    [Fact]
    public async Task Mount_RaisesOneEventPerRegistryNotOnePerRegistration()
    {
        await using var host = new PluginHost();
        var commandEvents = 0;
        host.Context.CommandsChanged += () => commandEvents++;

        await host.MountPluginAsync(new DelegatePlugin("test.many.commands", (ctx, _) =>
        {
            for (var i = 0; i < 10; i++) ctx.RegisterCommand(Command($"cmd.{i}"));
            return Task.CompletedTask;
        }));
        Assert.Equal(1, commandEvents);

        await host.UnmountPluginAsync("test.many.commands");
        Assert.Equal(2, commandEvents);
        Assert.Empty(host.Context.GetRegisteredCommands());
    }

    [Fact]
    public void NotificationContext_PostsEventsToIt()
    {
        var sync = new RecordingSyncContext();
        var ctx = new FryPluginContext { NotificationContext = sync };
        var raised = 0;
        ctx.CommandsChanged += () => raised++;

        ctx.RegisterCommand(Command("posted"));
        Assert.Equal(0, raised);
        Assert.Single(sync.Posted);

        sync.RunAll();
        Assert.Equal(1, raised);
    }

    [Fact]
    public void HostOption_EventSynchronizationContext_IsAppliedToTheRootContext()
    {
        var sync = new RecordingSyncContext();
        using var host = new PluginHost(null, new PluginHostOptions { EventSynchronizationContext = sync });

        Assert.Same(sync, ((FryPluginContext)host.Context).NotificationContext);
    }

    [Fact]
    public async Task PipelineKey_RegistersAndExecutesThroughTheTypedOverloads()
    {
        var key = new PipelineKey<List<string>>("typed.serial");
        var bailKey = new PipelineKey<string, string>("typed.bail");
        await using var host = new PluginHost();

        await host.MountPluginAsync(new DelegatePlugin("test.typed.pipeline", (ctx, _) =>
        {
            ctx.RegisterSerial(key, log => { log.Add("ran"); return Task.CompletedTask; });
            ctx.RegisterBail(bailKey, s => Task.FromResult<string?>(s == "mine" ? "handled" : null));
            return Task.CompletedTask;
        }));

        var log = new List<string>();
        await host.Context.ExecuteSerialAsync(key, log);
        Assert.Equal(new[] { "ran" }, log);
        Assert.Equal("handled", await host.Context.ExecuteBailAsync(bailKey, "mine"));
        Assert.Null(await host.Context.ExecuteBailAsync(bailKey, "other"));

        await host.UnmountPluginAsync("test.typed.pipeline");
        log.Clear();
        await host.Context.ExecuteSerialAsync(key, log);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Pipelines_HandlerThatThrowsSynchronously_SurfacesAsAFaultedTask()
    {
        var pipelines = new PipelineManager();
        pipelines.RegisterSerial<int>("throws", _ => throw new InvalidOperationException("serial"));
        pipelines.RegisterParallel<int>("throws", _ => throw new InvalidOperationException("parallel"));
        pipelines.RegisterWaterfall<int>("throws", (_, _) => throw new InvalidOperationException("waterfall"));

        var serial = pipelines.ExecuteSerialAsync("throws", 1);
        var parallel = pipelines.ExecuteParallelAsync("throws", 1);
        var waterfall = pipelines.ExecuteWaterfallAsync("throws", 1);

        Assert.Equal("serial", (await Assert.ThrowsAsync<InvalidOperationException>(() => serial)).Message);
        Assert.Equal("parallel", (await Assert.ThrowsAsync<InvalidOperationException>(() => parallel)).Message);
        Assert.Equal("waterfall", (await Assert.ThrowsAsync<InvalidOperationException>(() => waterfall)).Message);
    }

    [Fact]
    public async Task Pipelines_SerialRunsHandlersInOrder_AcrossAsyncHandlers()
    {
        var pipelines = new PipelineManager();
        var log = new List<int>();
        pipelines.RegisterSerial<List<int>>("order", l => { l.Add(1); return Task.CompletedTask; });
        pipelines.RegisterSerial<List<int>>("order", async l => { await Task.Yield(); l.Add(2); });
        pipelines.RegisterSerial<List<int>>("order", l => { l.Add(3); return Task.CompletedTask; });

        await pipelines.ExecuteSerialAsync("order", log);

        Assert.Equal(new[] { 1, 2, 3 }, log);
    }

    [Fact]
    public void UnpackAndLoad_SamePackageTwice_ExtractsOnlyOnce()
    {
        var staging = Path.Combine(_root, "staging");
        Directory.CreateDirectory(staging);
        foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory))
        {
            File.Copy(file, Path.Combine(staging, Path.GetFileName(file)));
        }
        var packageFile = Path.Combine(_root, "Fixture.fryplugin");
        ZipFile.CreateFromDirectory(staging, packageFile);
        var pluginsDir = Path.Combine(_root, "plugins");

        string installDir;
        using (var first = FryPluginPackageLoader.UnpackAndLoad(packageFile, pluginsDir))
        {
            installDir = first.InstallDirectory;
        }

        // A file the package does not contain survives only if the second load did not re-extract.
        var marker = Path.Combine(installDir, "marker.txt");
        File.WriteAllText(marker, "still here");

        using (FryPluginPackageLoader.UnpackAndLoad(packageFile, pluginsDir)) { }
        Assert.True(File.Exists(marker));

        // Replacing the package (new timestamp) does re-extract.
        File.SetLastWriteTimeUtc(packageFile, DateTime.UtcNow.AddMinutes(1));
        using (FryPluginPackageLoader.UnpackAndLoad(packageFile, pluginsDir)) { }
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void EntryType_InManifest_IsUsed_AndAWrongOneIsReported()
    {
        var assembly = typeof(PerformanceBehaviourTests).Assembly;
        var diagnostics = new List<PluginLoadDiagnostic>();

        var named = PluginAssemblyLoader.DiscoverPluginTypes(assembly, "test.dll",
            new PluginManifest { Id = "test", EntryType = typeof(AttributedPlugin).FullName }, diagnostics);
        Assert.Equal(new[] { typeof(AttributedPlugin) }, named);
        Assert.Empty(diagnostics);

        var wrong = PluginAssemblyLoader.DiscoverPluginTypes(assembly, "test.dll",
            new PluginManifest { Id = "test", EntryType = "No.Such.Type" }, diagnostics);
        Assert.Contains(typeof(AttributedPlugin), wrong);
        Assert.Contains(diagnostics, d => d.Message.Contains("No.Such.Type"));
    }

    [Fact]
    public void EntryAttribute_LimitsDiscoveryToTheDeclaredTypes()
    {
        // This test assembly declares [assembly: FryPluginEntry(typeof(AttributedPlugin))], so the other
        // IFryPlugin types in it must not be picked up.
        var types = PluginAssemblyLoader.DiscoverPluginTypes(typeof(PerformanceBehaviourTests).Assembly, "test.dll", manifest: null, diagnostics: null);

        Assert.Equal(new[] { typeof(AttributedPlugin) }, types);
    }

    [Fact]
    public async Task Mount_RecordsDuration_AndReportsSlowPlugins()
    {
        await using var host = new PluginHost(null, new PluginHostOptions { SlowMountThreshold = TimeSpan.FromMilliseconds(5) });

        await host.MountPluginAsync(new DelegatePlugin("test.fast", (_, _) => Task.CompletedTask));
        await host.MountPluginAsync(new DelegatePlugin("test.slow", (_, ct) => Task.Delay(60, ct)));

        Assert.NotNull(host.GetRegistration("test.fast")!.MountDuration);
        Assert.True(host.GetRegistration("test.slow")!.MountDuration >= TimeSpan.FromMilliseconds(50));
        var slow = Assert.Single(host.Diagnostics);
        Assert.Equal("test.slow", slow.PluginId);
        Assert.Equal(PluginDiagnosticSeverity.Warning, slow.Severity);
    }

    private interface IProvided { }
    private sealed class Provided : IProvided { }

    [Fact]
    public async Task StartAsync_WithConcurrency_MountsIndependentPluginsTogether_AndDependentsAfter()
    {
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var order = new List<string>();

        async Task WaitForPeer(string id)
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.TrySetResult();
            // Only completes if the other independent plugin is mounting at the same time.
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            lock (order) order.Add(id);
        }

        await using var host = new PluginHost(null, new PluginHostOptions { MountConcurrency = 4 });
        host.RegisterPlugin(new DelegatePlugin("test.consumer", (ctx, _) =>
        {
            lock (order) order.Add("test.consumer");
            return Task.CompletedTask;
        }, requires: new[] { typeof(IProvided) }));
        host.RegisterPlugin(new DelegatePlugin("test.provider", async (ctx, _) =>
        {
            await WaitForPeer("test.provider");
            ctx.RegisterService<IProvided>(new Provided());
        }, provides: new[] { typeof(IProvided) }));
        host.RegisterPlugin(new DelegatePlugin("test.independent", (_, _) => WaitForPeer("test.independent")));

        await host.StartAsync();

        Assert.All(new[] { "test.consumer", "test.provider", "test.independent" },
            id => Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(id)));
        Assert.Equal("test.consumer", order[^1]);
    }

    [Fact]
    public void Options_MountConcurrencyBelowOne_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PluginHost(null, new PluginHostOptions { MountConcurrency = 0 }));
    }

    [Fact]
    public async Task DiscoverAndLoadDirectoryAsync_LoadsOffTheCallingThread()
    {
        var pluginsDir = Path.Combine(_root, "plugins");
        var installDir = Path.Combine(pluginsDir, TestPaths.FixturePluginId, "1.2.3");
        Directory.CreateDirectory(installDir);
        foreach (var file in Directory.GetFiles(TestPaths.FixturePluginDirectory))
        {
            File.Copy(file, Path.Combine(installDir, Path.GetFileName(file)));
        }

        var diagnostics = new List<PluginLoadDiagnostic>();
        var packages = await PluginAssemblyLoader.DiscoverAndLoadDirectoryAsync(pluginsDir, diagnostics);
        try
        {
            Assert.Equal(TestPaths.FixturePluginId, Assert.Single(Assert.Single(packages).Plugins).Id);
            Assert.Empty(diagnostics);
        }
        finally
        {
            foreach (var p in packages) p.Dispose();
        }
    }
}
