using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// Collectible <see cref="AssemblyLoadContext"/> that enables dynamic plugin assemblies to be loaded,
/// isolated, and later fully unloaded from process memory.
/// </summary>
public sealed class CollectiblePluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> HostContractAssemblies =
        new(StringComparer.OrdinalIgnoreCase) { "FryWorld.Sdk", "FryWorld" };

    /// <summary>
    /// Whether the host supplies <paramref name="assemblyName"/> to plugins (the SDK contract or a shared
    /// framework such as Avalonia), rather than each plugin bringing its own copy.
    /// </summary>
    internal static bool IsHostProvided(string assemblyName) =>
        HostContractAssemblies.Contains(assemblyName) || HostAssemblyList.Matches(assemblyName);

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    /// <summary>Creates a context resolving dependencies next to <paramref name="pluginPath"/>; it can be unloaded when <paramref name="isCollectible"/> is true.</summary>
    public CollectiblePluginLoadContext(string pluginPath, bool isCollectible = true)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: isCollectible)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
    }

    /// <summary>Resolves an assembly for the plugin: the host's copy for the SDK contract, the plugin's own copy when it has one, and otherwise the host's copy of shared frameworks.</summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Stage 1 — Host contract assemblies (FryWorld.Sdk). Must always resolve to the host's copy
        // to preserve IFryPlugin type identity across load context boundaries.
        if (assemblyName.Name != null && HostContractAssemblies.Contains(assemblyName.Name))
        {
            var hostAssembly = ResolveFromDefaultContext(assemblyName);
            if (hostAssembly != null) return hostAssembly;
        }

        // Stage 2 — Plugin private dependencies win over host defaults
        string? resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath != null && File.Exists(resolvedPath))
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        // Direct directory probe fallback
        if (!string.IsNullOrEmpty(_pluginDirectory))
        {
            var candidate = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(candidate))
            {
                return LoadFromAssemblyPath(candidate);
            }
        }

        // Stage 3 — Shared host frameworks (Avalonia, MVVM toolkit, etc.)
        if (assemblyName.Name != null && HostAssemblyList.Matches(assemblyName.Name))
        {
            var shared = ResolveFromDefaultContext(assemblyName);
            if (shared != null) return shared;
        }

        return null;
    }

    // Host assemblies already resolved from the default context, by simple name. The default context never
    // unloads, so an entry stays valid for the life of the process; every plugin load context shares it.
    private static readonly ConcurrentDictionary<string, Assembly> DefaultContextCache = new(StringComparer.OrdinalIgnoreCase);

    private static Assembly? ResolveFromDefaultContext(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (DefaultContextCache.TryGetValue(name, out var cached)) return cached;

        var resolved = FindOrLoadInDefaultContext(assemblyName, name);
        if (resolved != null)
        {
            DefaultContextCache.TryAdd(name, resolved);
        }
        return resolved;
    }

    private static Assembly? FindOrLoadInDefaultContext(AssemblyName assemblyName, string name)
    {
        foreach (var assembly in Default.Assemblies)
        {
            if (string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }
        }

        // Load by simple name first so the host's version wins over the version the plugin compiled against.
        try
        {
            return Default.LoadFromAssemblyName(new AssemblyName(name));
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            try
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }
            catch (Exception inner) when (inner is FileNotFoundException or FileLoadException or BadImageFormatException)
            {
                return null;
            }
        }
    }
}
