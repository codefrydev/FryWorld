using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// </summary>
    public static PluginAssemblyPackage LoadAssembly(string assemblyPath, bool isCollectible = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file '{assemblyPath}' not found.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
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
            if (isCollectible)
            {
                context.Unload();
            }
            throw;
        }
    }

    /// <summary>
    /// Scans a directory for plugin DLLs and loads all valid packages.
    /// </summary>
    public static IReadOnlyList<PluginAssemblyPackage> DiscoverAndLoadDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<PluginAssemblyPackage>();
        }

        var packages = new List<PluginAssemblyPackage>();
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

        foreach (var dll in dllFiles)
        {
            try
            {
                var package = LoadAssembly(dll, isCollectible: true);
                if (package.Plugins.Count > 0)
                {
                    packages.Add(package);
                }
                else
                {
                    package.Dispose(); // Unload non-plugin DLLs immediately
                }
            }
            catch
            {
                // Silently skip non-managed or non-plugin binaries
            }
        }

        return packages;
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
