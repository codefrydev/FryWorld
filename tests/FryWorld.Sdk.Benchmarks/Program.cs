using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace FryWorld.Sdk.Benchmarks;

/// <summary>
/// Run with: dotnet run -c Release --project tests/FryWorld.Sdk.Benchmarks -- --filter '*'
/// Results land in BenchmarkDotNet.Artifacts/results/ (Markdown tables) for before/after comparison.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        // In-process: the benchmarks load plugins from the fixtures folder next to this assembly,
        // which a separately generated benchmark project would not have.
        var config = DefaultConfig.Instance
            .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
            .AddDiagnoser(MemoryDiagnoser.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
