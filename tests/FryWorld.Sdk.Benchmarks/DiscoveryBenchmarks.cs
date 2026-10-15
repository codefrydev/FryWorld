using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using FryWorld.Sdk.Loading;

namespace FryWorld.Sdk.Benchmarks;

/// <summary>
/// Startup cost of finding and loading installed plugins: a plugins folder holding ten .fryplugin packages.
/// Cold = nothing extracted yet (first launch after install). Warm = already extracted (every later launch).
/// </summary>
[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private const int PluginCount = 10;
    private string _pluginsDir = null!;

    [GlobalSetup]
    public void Setup()
    {
        var fixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures", "FixturePlugin");
        _pluginsDir = Path.Combine(Path.GetTempPath(), "FryWorldSdkBench_" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(_pluginsDir, ".staging");

        for (var i = 0; i < PluginCount; i++)
        {
            var stage = Path.Combine(staging, i.ToString());
            Directory.CreateDirectory(stage);
            foreach (var file in Directory.GetFiles(fixtureDir))
            {
                File.Copy(file, Path.Combine(stage, Path.GetFileName(file)));
            }

            // Each package needs its own id so each gets its own install folder.
            var manifestPath = Path.Combine(stage, "plugin.json");
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
            manifest["id"] = $"com.fryworld.bench.plugin{i}";
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            ZipFile.CreateFromDirectory(stage, Path.Combine(_pluginsDir, $"Bench{i}.fryplugin"));
        }

        Directory.Delete(staging, recursive: true);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_pluginsDir, recursive: true); } catch { }
    }

    [IterationSetup(Target = nameof(Cold))]
    public void RemoveExtractedPlugins()
    {
        foreach (var dir in Directory.GetDirectories(_pluginsDir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Benchmark]
    public int Cold() => DiscoverLoadAndUnload();

    [Benchmark]
    public int Warm() => DiscoverLoadAndUnload();

    private int DiscoverLoadAndUnload()
    {
        var packages = PluginAssemblyLoader.DiscoverAndLoadDirectory(_pluginsDir);
        foreach (var package in packages) package.Dispose();
        return packages.Count;
    }
}
