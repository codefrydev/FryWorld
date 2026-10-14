using System.Collections.Generic;
using System.Threading.Tasks;
using FryWorld.Sdk.Pipelines;
using Xunit;

namespace FryWorld.Sdk.Tests;

public class PipelineEngineTests
{
    private class TestContext
    {
        public List<string> Trace { get; } = new();
    }

    [Fact]
    public async Task WaterfallPipeline_WrapsAndDelegatesCorrectly()
    {
        var manager = new PipelineManager();
        var ctx = new TestContext();

        manager.RegisterWaterfall<TestContext>("test:pipeline", async (context, next) =>
        {
            context.Trace.Add("middleware1:before");
            await next();
            context.Trace.Add("middleware1:after");
        });

        manager.RegisterWaterfall<TestContext>("test:pipeline", async (context, next) =>
        {
            context.Trace.Add("middleware2:before");
            await next();
            context.Trace.Add("middleware2:after");
        });

        await manager.ExecuteWaterfallAsync("test:pipeline", ctx, terminal: () =>
        {
            ctx.Trace.Add("terminal");
            return Task.CompletedTask;
        });

        Assert.Equal(new[]
        {
            "middleware1:before",
            "middleware2:before",
            "terminal",
            "middleware2:after",
            "middleware1:after"
        }, ctx.Trace);
    }

    [Fact]
    public async Task BailPipeline_ShortCircuitsOnFirstResult()
    {
        var manager = new PipelineManager();

        manager.RegisterBail<string, string>("test:bail", input => Task.FromResult<string?>(null));
        manager.RegisterBail<string, string>("test:bail", input => Task.FromResult<string?>($"handled:{input}"));
        manager.RegisterBail<string, string>("test:bail", input => Task.FromResult<string?>("never_reached"));

        var result = await manager.ExecuteBailAsync<string, string>("test:bail", "hello");

        Assert.Equal("handled:hello", result);
    }

    [Fact]
    public async Task ParallelPipeline_ExecutesAllHandlers()
    {
        var manager = new PipelineManager();
        var list = new List<int>();

        manager.RegisterParallel<int>("test:parallel", val => { lock (list) list.Add(val * 1); return Task.CompletedTask; });
        manager.RegisterParallel<int>("test:parallel", val => { lock (list) list.Add(val * 2); return Task.CompletedTask; });
        manager.RegisterParallel<int>("test:parallel", val => { lock (list) list.Add(val * 3); return Task.CompletedTask; });

        await manager.ExecuteParallelAsync("test:parallel", 10);

        Assert.Contains(10, list);
        Assert.Contains(20, list);
        Assert.Contains(30, list);
    }
}
