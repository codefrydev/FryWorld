using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// Container holding instantiated plugins from an isolated assembly package, along with its collectible context.
/// </summary>
public sealed class PluginAssemblyPackage : IDisposable
{
    private readonly CollectiblePluginLoadContext _context;
    private bool _isDisposed;

    public string AssemblyPath { get; }
    public IReadOnlyList<IFryPlugin> Plugins { get; }
    public CollectiblePluginLoadContext Context => _context;
    public bool IsCollectible => _context.IsCollectible;

    public PluginAssemblyPackage(string assemblyPath, IReadOnlyList<IFryPlugin> plugins, CollectiblePluginLoadContext context)
    {
        AssemblyPath = assemblyPath;
        Plugins = plugins;
        _context = context;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        PluginAssemblyLoader.Forget(this);

        if (_context.IsCollectible)
        {
            _context.Unload();
        }
    }
}

/// <summary>
/// Static loader managing discovery, assembly reflection, and lifecycle of dynamic plugin assemblies.
/// </summary>
public static class PluginAssemblyLoader
{
    private static readonly ConcurrentBag<PluginAssemblyPackage> _activePackages = new();

    public static IReadOnlyCollection<PluginAssemblyPackage> ActivePackages => _activePackages.ToList();

    public static void Forget(PluginAssemblyPackage package)
    {
        // cooperative removal
    }

    /// <summary>
    /// Loads a plugin assembly from disk into an isolated <see cref="CollectiblePluginLoadContext"/>
    /// and instantiates all discovered <see cref="IFryPlugin"/> types.
    /// When <paramref name="isCollectibleOverride"/> is not provided, the companion <c>plugin.json</c>
    /// manifest is consulted (defaulting to collectible <c>true</c> if not declared).
    /// </summary>
    public static PluginAssemblyPackage LoadAssembly(string assemblyPath, bool? isCollectibleOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file '{assemblyPath}' not found.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        bool isCollectible = isCollectibleOverride ?? DetermineIsCollectible(fullPath);
        var context = new CollectiblePluginLoadContext(fullPath, isCollectible);

        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            var pluginTypes = DiscoverPluginTypes(assembly);

            var plugins = new List<IFryPlugin>();
            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IFryPlugin plugin)
                {
                    plugins.Add(plugin);
                }
            }

            var package = new PluginAssemblyPackage(fullPath, plugins, context);
            _activePackages.Add(package);
            return package;
        }
        catch
        {
            if (context.IsCollectible)
            {
                context.Unload();
            }
            throw;
        }
    }

    /// <summary>
    /// Checks whether the companion <c>plugin.json</c> declares <c>"isCollectible": false</c>.
    /// Defaults to <c>true</c> (collectible ALC) for standard plugins.
    /// </summary>
    public static bool DetermineIsCollectible(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath)) return true;

        try
        {
            var dir = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(dir))
            {
                var candidates = new[]
                {
                    Path.Combine(dir, "plugin.json"),
                    Path.Combine(Path.GetDirectoryName(dir) ?? string.Empty, "plugin.json")
                };

                foreach (var manifestFile in candidates)
                {
                    if (File.Exists(manifestFile))
                    {
                        var json = File.ReadAllText(manifestFile);
                        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);
                        if (manifest != null)
                        {
                            return manifest.IsCollectible;
                        }
                    }
                }
            }
        }
        catch
        {
            // Default to collectible ALC on failure
        }

        return true;
    }

    /// <summary>
    /// Scans a directory for plugin packages and unpacked plugin folders, loading valid packages.
    /// </summary>
    public static IReadOnlyList<PluginAssemblyPackage> DiscoverAndLoadDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<PluginAssemblyPackage>();
        }

        var packages = new List<PluginAssemblyPackage>();
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Discover and unpack .fryplugin packages
        var packageFiles = Directory.GetFiles(directoryPath, "*.fryplugin", SearchOption.TopDirectoryOnly);
        foreach (var pkgFile in packageFiles)
        {
            try
            {
                var pkgResult = FryPluginPackageLoader.UnpackAndLoad(pkgFile, directoryPath);
                if (pkgResult.AssemblyPackage.Plugins.Count > 0)
                {
                    packages.Add(pkgResult.AssemblyPackage);
                    loadedPaths.Add(pkgResult.AssemblyPackage.AssemblyPath);
                }
                else
                {
                    pkgResult.Dispose();
                }
            }
            catch
            {
                // Ignore corrupted archives during scan
            }
        }

        // 2. Discover unpacked plugin subdirectories
        var subDirectories = Directory.GetDirectories(directoryPath);
        foreach (var subDir in subDirectories)
        {
            try
            {
                var entryDll = ResolvePluginDirectoryEntryDll(subDir);
                if (entryDll != null && !loadedPaths.Contains(entryDll))
                {
                    var package = LoadAssembly(entryDll);
                    if (package.Plugins.Count > 0)
                    {
                        packages.Add(package);
                        loadedPaths.Add(entryDll);
                    }
                    else
                    {
                        package.Dispose();
                    }
                }
            }
            catch
            {
                // Skip non-plugin subdirectories
            }
        }

        // 3. Discover top-level standalone DLLs (SearchOption.TopDirectoryOnly so subfolder dependencies aren't loaded)
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (var dll in dllFiles)
        {
            if (loadedPaths.Contains(dll)) continue;

            try
            {
                var package = LoadAssembly(dll);
                if (package.Plugins.Count > 0)
                {
                    packages.Add(package);
                    loadedPaths.Add(dll);
                }
                else
                {
                    package.Dispose();
                }
            }
            catch
            {
                // Silently skip non-managed or non-plugin binaries
            }
        }

        return packages;
    }

    /// <summary>
    /// Resolves the entry assembly for a plugin directory, inspecting version subdirectories or plugin.json.
    /// </summary>
    public static string? ResolvePluginDirectoryEntryDll(string subDir)
    {
        if (!Directory.Exists(subDir)) return null;

        var innerDirs = Directory.GetDirectories(subDir);
        foreach (var inner in innerDirs)
        {
            var manifestCandidate = Path.Combine(inner, "plugin.json");
            if (File.Exists(manifestCandidate))
            {
                var dll = FindEntryDllInDirectory(inner);
                if (dll != null) return dll;
            }
        }

        return FindEntryDllInDirectory(subDir);
    }

    /// <summary>
    /// Finds the entry DLL in a plugin folder by inspecting plugin.json manifest,
    /// then checking {folderName}.dll, and finally falling back to any .dll in the top directory.
    /// </summary>
    public static string? FindEntryDllInDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return null;

        var manifestFile = Path.Combine(dir, "plugin.json");
        if (File.Exists(manifestFile))
        {
            try
            {
                var json = File.ReadAllText(manifestFile);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json);
                if (!string.IsNullOrWhiteSpace(manifest?.EntryPoint))
                {
                    var candidate = Path.Combine(dir, manifest.EntryPoint);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
        }

        var folderName = Path.GetFileName(dir);
        var candidateByName = Path.Combine(dir, $"{folderName}.dll");
        if (File.Exists(candidateByName)) return candidateByName;

        return Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static IEnumerable<Type> DiscoverPluginTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IFryPlugin).IsAssignableFrom(t));
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null && !t.IsAbstract && !t.IsInterface && typeof(IFryPlugin).IsAssignableFrom(t))!;
        }
    }
}

