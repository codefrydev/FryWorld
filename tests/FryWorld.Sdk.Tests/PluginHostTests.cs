using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Plugins;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class PluginHostTests
{
    private class ServiceA { }
    private class ServiceB { }

    private class PluginProvidingA : FryPluginBase
    {
        public override string Id => "test.provider.a";
        public override string Name => "Provider A";
        public override IReadOnlyList<Type> ProvidedServices => new[] { typeof(ServiceA) };

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            ctx.RegisterService(new ServiceA());
            return Task.CompletedTask;
        }
    }

    private class PluginConsumingAAndProvidingB : FryPluginBase
    {
        public override string Id => "test.consumer.a.provider.b";
        public override string Name => "Consumer A Provider B";
        public override IReadOnlyList<Type> RequiredServices => new[] { typeof(ServiceA) };
        public override IReadOnlyList<Type> ProvidedServices => new[] { typeof(ServiceB) };

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            var a = ctx.GetService<ServiceA>();
            Assert.NotNull(a);
            ctx.RegisterService(new ServiceB());
            return Task.CompletedTask;
        }
    }

    private class PluginConsumingB : FryPluginBase
    {
        public override string Id => "test.consumer.b";
        public override string Name => "Consumer B";
        public override IReadOnlyList<Type> RequiredServices => new[] { typeof(ServiceB) };

        public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
        {
            var b = ctx.GetService<ServiceB>();
            Assert.NotNull(b);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void TopologicalSort_OrdersDependenciesBeforeConsumers()
    {
        var p1 = new PluginConsumingB();
        var p2 = new PluginProvidingA();
        var p3 = new PluginConsumingAAndProvidingB();

        // Shuffled registration order
        var sorted = PluginHost.TopologicalSort(new List<IFryPlugin> { p1, p2, p3 });

        var indexA = sorted.IndexOf(p2);
        var indexAB = sorted.IndexOf(p3);
        var indexB = sorted.IndexOf(p1);

        Assert.True(indexA < indexAB, "Provider A must precede Consumer A");
        Assert.True(indexAB < indexB, "Provider B must precede Consumer B");
    }

    [Fact]
    public async Task StartAsync_MountsAllPluginsInOrder()
    {
        var host = new PluginHost();
        host.RegisterPlugin(new PluginConsumingB());
        host.RegisterPlugin(new PluginProvidingA());
        host.RegisterPlugin(new PluginConsumingAAndProvidingB());

        await host.StartAsync();

        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("test.provider.a"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("test.consumer.a.provider.b"));
        Assert.Equal(PluginLifecycleState.Active, host.GetPluginState("test.consumer.b"));
    }

    [Fact]
    public void PluginScope_ExecutesEffectsInLIFOOrder()
    {
        var order = new List<int>();
        var scope = new PluginScope();

        scope.RegisterEffect(() => order.Add(1));
        scope.RegisterEffect(() => order.Add(2));
        scope.RegisterEffect(() => order.Add(3));

        scope.Dispose();

        Assert.Equal(new[] { 3, 2, 1 }, order);
    }
}
