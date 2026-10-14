using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FryWorld.Sdk.Models;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// Result of unpacking and loading a self-contained .fryplugin distribution package.
/// </summary>
public sealed class FryPluginPackageResult : IDisposable
{
    public required PluginManifest Manifest { get; init; }
    public required PluginAssemblyPackage AssemblyPackage { get; init; }
    public required string InstallDirectory { get; init; }

    public void Dispose()
    {
        AssemblyPackage.Dispose();
    }
}

/// <summary>
/// Manages validation, extraction, and loading of self-contained .fryplugin packages.
/// A .fryplugin package is a ZIP archive containing a plugin.json manifest, assemblies, and assets.
/// </summary>
public static class FryPluginPackageLoader
{
    public static FryPluginPackageResult UnpackAndLoad(string packageFilePath, string targetPluginsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPluginsDirectory);

        if (!File.Exists(packageFilePath))
        {
            throw new FileNotFoundException($"Plugin package '{packageFilePath}' not found.");
        }

        Directory.CreateDirectory(targetPluginsDirectory);

        // 1. Read manifest from ZIP archive
        PluginManifest? manifest = null;
        using (var archive = ZipFile.OpenRead(packageFilePath))
        {
            var manifestEntry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, "plugin.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(e.FullName), "plugin.json", StringComparison.OrdinalIgnoreCase));

            if (manifestEntry != null)
            {
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                manifest = JsonSerializer.Deserialize<PluginManifest>(json);
            }
        }

        var pluginId = !string.IsNullOrWhiteSpace(manifest?.Id)
            ? manifest.Id
            : Path.GetFileNameWithoutExtension(packageFilePath);

        var pluginVersion = !string.IsNullOrWhiteSpace(manifest?.Version)
            ? manifest.Version.Trim()
            : "1.0.0";

        // Security: validate destination directory against directory traversal
        var destinationFolder = PluginIdValidator.ResolveInstallDirectory(targetPluginsDirectory, pluginId, pluginVersion);

        if (Directory.Exists(destinationFolder))
        {
            try
            {
                Directory.Delete(destinationFolder, recursive: true);
            }
            catch { }
        }

        Directory.CreateDirectory(destinationFolder);

        // 2. Extract package contents
        ZipFile.ExtractToDirectory(packageFilePath, destinationFolder, overwriteFiles: true);

        // 3. Resolve entryPoint DLL
        string entryPointDll = string.Empty;
        if (!string.IsNullOrWhiteSpace(manifest?.EntryPoint))
        {
            entryPointDll = Path.Combine(destinationFolder, manifest.EntryPoint);
        }

        if (string.IsNullOrEmpty(entryPointDll) || !File.Exists(entryPointDll))
        {
            var candidates = Directory.GetFiles(destinationFolder, "*.dll", SearchOption.AllDirectories);
            entryPointDll = candidates.FirstOrDefault() ??
                throw new FileNotFoundException($"No valid entry point assembly found in extracted package for '{pluginId}'.");
        }

        // 4. Load assembly into isolated collectible context
        var isCollectible = manifest?.IsCollectible ?? true;
        var package = PluginAssemblyLoader.LoadAssembly(entryPointDll, isCollectible);

        manifest ??= new PluginManifest
        {
            Id = pluginId,
            Name = pluginId,
            Version = pluginVersion,
            EntryPoint = Path.GetFileName(entryPointDll)
        };

        return new FryPluginPackageResult
        {
            Manifest = manifest,
            AssemblyPackage = package,
            InstallDirectory = destinationFolder
        };
    }
}
