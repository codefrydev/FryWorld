using System;
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

    private static readonly HashSet<string> HostSharedFrameworkAssemblies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Avalonia", "Avalonia.Base", "Avalonia.Controls", "Avalonia.Desktop",
            "Avalonia.Markup", "Avalonia.Markup.Xaml", "Avalonia.Skia",
            "CommunityToolkit.Mvvm", "Material.Icons", "Material.Icons.Avalonia",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions"
        };

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public CollectiblePluginLoadContext(string pluginPath, bool isCollectible = true)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: isCollectible)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
    }

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

        // Stage 3 — Shared host frameworks (Avalonia, MVVM toolkit)
        if (assemblyName.Name != null && HostSharedFrameworkAssemblies.Contains(assemblyName.Name))
        {
            var shared = ResolveFromDefaultContext(assemblyName);
            if (shared != null) return shared;
        }

        return null;
    }

    private static Assembly? ResolveFromDefaultContext(AssemblyName assemblyName)
    {
        var loaded = Default.Assemblies.FirstOrDefault(a =>
            string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        if (loaded != null) return loaded;

        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch
        {
            return null;
        }
    }
}
