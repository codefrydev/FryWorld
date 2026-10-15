using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using BenchmarkDotNet.Attributes;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Benchmarks;

/// <summary>
/// The shell reads registries on every change notification and on every command-palette keystroke,
/// so reads must be cheap. Writes only happen when plugins mount or unmount.
/// </summary>
[MemoryDiagnoser]
public class RegistryBenchmarks
{
    private FryPluginContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        _context = new FryPluginContext();
        for (var i = 0; i < 50; i++)
        {
            _context.RegisterNavigationItem(new NavigationItemDescriptor
            {
                Id = $"nav.{i}",
                Title = $"Page {i}",
                Order = 100 - i,
                ViewFactory = static () => new TextBlock()
            });
        }

        for (var i = 0; i < 200; i++)
        {
            _context.RegisterCommand(new CommandPaletteDescriptor { Id = $"cmd.{i}", Title = $"Command {i}" });
        }
    }

    [Benchmark]
    public int GetNavigationItems() => _context.GetRegisteredNavigationItems().Count;

    [Benchmark]
    public int GetCommands() => _context.GetRegisteredCommands().Count;
}

/// <summary>
/// Pipelines run on hot paths such as rendering and export filters, so dispatch overhead matters.
/// </summary>
[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private const int HandlerCount = 10;
    private FryPluginContext _context = null!;
    private readonly List<int> _payload = new();

    [GlobalSetup]
    public void Setup()
    {
        _context = new FryPluginContext();
        for (var i = 0; i < HandlerCount; i++)
        {
            _context.RegisterSerial<List<int>>("bench.serial", static _ => Task.CompletedTask);
            _context.RegisterParallel<List<int>>("bench.parallel", static _ => Task.CompletedTask);
            _context.RegisterWaterfall<List<int>>("bench.waterfall", static (_, next) => next());
            _context.RegisterBail<List<int>, string>("bench.bail", static _ => Task.FromResult<string?>(null));
        }
    }

    [Benchmark]
    public Task Serial() => _context.ExecuteSerialAsync("bench.serial", _payload);

    [Benchmark]
    public Task Parallel() => _context.ExecuteParallelAsync("bench.parallel", _payload);

    [Benchmark]
    public Task Waterfall() => _context.ExecuteWaterfallAsync("bench.waterfall", _payload);

    [Benchmark]
    public Task<string?> Bail() => _context.ExecuteBailAsync<List<int>, string>("bench.bail", _payload);
}
