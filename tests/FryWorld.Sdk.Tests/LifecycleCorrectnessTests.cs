using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Pipelines;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Tests.TestSupport;
using Xunit;

namespace FryWorld.Sdk.Tests;

/// <summary>
/// Scope, mount, unmount and pipeline behaviour that plugins rely on to leave nothing behind.
/// </summary>
public class LifecycleCorrectnessTests
{
    private sealed class DelegatePlugin : FryPluginBase
    {
        private readonly Func<IFryPluginContext, Task> _apply;

        public DelegatePlugin(string id, Func<IFryPluginContext, Task> apply, params Type[] requires)
        {
            Id = id;
            _apply = apply;
            RequiredServices = requires;
        }

        public override string Id { get; }
        public override string Name => Id;
        public override IReadOnlyList<Type> RequiredServices { get; }
        public int ApplyCount { get; private set; }

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            ApplyCount++;
            return _apply(ctx);
        }
    }

    private interface IGreeter { string Name { get; } }
    private sealed record Greeter(string Name) : IGreeter;

    private static CommandPaletteDescriptor Command(string id) => new() { Id = id, Title = id };

    [Fact]
    public void Scope_EffectDisposedEarly_RunsOnceAndLeavesTheScope()
    {
        var scope = new PluginScope();
        var runs = 0;

        var handle = scope.RegisterEffect(() => runs++);
        scope.RegisterEffect(() => { });
        Assert.Equal(2, scope.ActiveEffectCount);

        handle.Dispose();
        handle.Dispose();
        Assert.Equal(1, runs);
        Assert.Equal(1, scope.ActiveEffectCount);

        scope.Dispose();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Scope_ThrowingEffect_DoesNotStopTheOthers()
    {
        var scope = new PluginScope();
        var ran = new List<string>();
        scope.RegisterEffect(() => ran.Add("first"));
        scope.RegisterEffect(() => throw new InvalidOperationException("boom"));
        scope.RegisterEffect(() => ran.Add("last"));

        scope.Dispose();

        Assert.Equal(new[] { "last", "first" }, ran);
    }

    [Fact]
    public async Task Mount_PluginThatThrows_RollsBackWhatItRegistered()
    {
        await using var host = new PluginHost();
        var plugin = new DelegatePlugin("test.throws", ctx =>
        {
            ctx.RegisterCommand(Command("cmd.before.failure"));
            ctx.RegisterService<IGreeter>(new Greeter("partial"));
            throw new InvalidOperationException("boom");
        });

        host.RegisterPlugin(plugin);
        await host.StartAsync();

        Assert.Equal(PluginLifecycleState.Failed, host.GetPluginState(plugin.Id));
        Assert.IsType<InvalidOperationException>(host.GetRegistration(plugin.Id)!.FailureError);
        Assert.Empty(host.Context.GetRegisteredCommands());
        Assert.False(host.Context.HasService<IGreeter>());

        var diagnostic = Assert.Single(host.Diagnostics);
        Assert.Equal(PluginLoadStage.Mount, diagnostic.Stage);
        Assert.Equal(PluginDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(plugin.Id, diagnostic.PluginId);
    }

    [Fact]
    public async Task Mount_FailedPlugin_CanBeMountedAgain()
    {
        await using var host = new PluginHost();
        var fail = true;
        var plugin = new DelegatePlugin("test.flaky", ctx =>
        {
            ctx.RegisterCommand(Command("cmd.flaky"));
            if (fail) throw new InvalidOperationException("first attempt fails");
            return Task.CompletedTask;
        });

        await host.MountPluginAsync(plugin);
        Assert.Equal(PluginLifecycleState.Failed, host.GetPluginState(plugin.Id));

        fail = false;
        await host.MountPluginAsync(plugin);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(plugin.Id));
        Assert.Single(host.Context.GetRegisteredCommands());
        Assert.Null(host.GetRegistration(plugin.Id)!.FailureError);
    }

    [Fact]
    public async Task Mount_UnmountedPlugin_CanBeMountedAgain()
    {
        await using var host = new PluginHost();
        var plugin = new DelegatePlugin("test.remount", ctx =>
        {
            ctx.RegisterCommand(Command("cmd.remount"));
            return Task.CompletedTask;
        });

        await host.MountPluginAsync(plugin);
        await host.UnmountPluginAsync(plugin.Id);
        Assert.Empty(host.Context.GetRegisteredCommands());

        await host.MountPluginAsync(plugin);
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(plugin.Id));
        Assert.Single(host.Context.GetRegisteredCommands());
    }

    [Fact]
    public async Task Mount_MissingRequiredService_FailsWithoutRunningThePlugin()
    {
        await using var host = new PluginHost();
        var plugin = new DelegatePlugin("test.needs.greeter", _ => Task.CompletedTask, typeof(IGreeter));

        await host.MountPluginAsync(plugin);

        Assert.Equal(0, plugin.ApplyCount);
        Assert.Equal(PluginLifecycleState.Failed, host.GetPluginState(plugin.Id));
        var error = Assert.IsType<PluginMissingDependencyException>(host.GetRegistration(plugin.Id)!.FailureError);
        Assert.Equal(typeof(IGreeter), error.MissingServiceType);
        Assert.Contains(nameof(IGreeter), Assert.Single(host.Diagnostics).Message);
    }

    [Fact]
    public async Task Mount_RequiredServiceFromHost_IsSatisfied()
    {
        await using var host = new PluginHost();
        host.Context.RegisterService<IGreeter>(new Greeter("host"));
        var plugin = new DelegatePlugin("test.needs.greeter", _ => Task.CompletedTask, typeof(IGreeter));

        await host.MountPluginAsync(plugin);

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState(plugin.Id));
    }

    [Fact]
    public async Task Dispose_TearsDownInReverseMountOrder()
    {
        var teardown = new List<string>();
        var host = new PluginHost();

        foreach (var id in new[] { "test.a", "test.b", "test.c" })
        {
            await host.MountPluginAsync(new DelegatePlugin(id, ctx =>
            {
                ctx.RegisterEffect(() => teardown.Add(id));
                return Task.CompletedTask;
            }));
        }

        await host.DisposeAsync();

        Assert.Equal(new[] { "test.c", "test.b", "test.a" }, teardown);
    }

    [Fact]
    public async Task Service_OverriddenByAnotherPlugin_IsRestoredWhenTheOverrideUnmounts()
    {
        await using var host = new PluginHost();
        await host.MountPluginAsync(new DelegatePlugin("test.original", ctx =>
        {
            ctx.RegisterService<IGreeter>(new Greeter("original"));
            return Task.CompletedTask;
        }));
        await host.MountPluginAsync(new DelegatePlugin("test.override", ctx =>
        {
            ctx.RegisterService<IGreeter>(new Greeter("override"));
            return Task.CompletedTask;
        }));

        Assert.Equal("override", host.Context.GetService<IGreeter>().Name);

        await host.UnmountPluginAsync("test.override");
        Assert.Equal("original", host.Context.GetService<IGreeter>().Name);

        await host.UnmountPluginAsync("test.original");
        Assert.False(host.Context.HasService<IGreeter>());
    }

    [Fact]
    public async Task Waterfall_NextCalledTwice_RunsTheRestOfTheChainOnce()
    {
        var logger = new RecordingLogger();
        var pipelines = new PipelineManager(logger);
        var downstream = 0;
        var terminal = 0;

        pipelines.RegisterWaterfall<object>("double.next", async (_, next) =>
        {
            await next();
            await next();
        });
        pipelines.RegisterWaterfall<object>("double.next", (_, next) =>
        {
            downstream++;
            return next();
        });

        await pipelines.ExecuteWaterfallAsync<object>("double.next", new object(), () =>
        {
            terminal++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, downstream);
        Assert.Equal(1, terminal);
        Assert.True(logger.HasWarningContaining("more than once"));
    }

    [Fact]
    public void Pipeline_HandlersWithDifferentContextTypes_AreReported()
    {
        var logger = new RecordingLogger();
        var pipelines = new PipelineManager(logger);

        pipelines.RegisterSerial<string>("mixed", _ => Task.CompletedTask);
        Assert.Empty(logger.Entries);

        pipelines.RegisterSerial<int>("mixed", _ => Task.CompletedTask);
        Assert.True(logger.HasWarningContaining("'mixed'"));
    }
}
