using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// Result of unpacking and loading a self-contained .fryplugin distribution package.
/// </summary>
public sealed class FryPluginPackageResult : IDisposable
{
    /// <summary>The package's manifest, or one synthesized from the file name when the package has none.</summary>
    public required PluginManifest Manifest { get; init; }

    /// <summary>The loaded entry assembly and the plugins created from it.</summary>
    public required PluginAssemblyPackage AssemblyPackage { get; init; }

    /// <summary>Where the package was extracted: <c>&lt;plugins&gt;/&lt;id&gt;/&lt;version&gt;</c>.</summary>
    public required string InstallDirectory { get; init; }

    /// <summary>Disposes the loaded package; see <see cref="PluginAssemblyPackage.Dispose"/>.</summary>
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
    private const string ManifestFileName = "plugin.json";

    /// <summary>
    /// Written into an install folder after a successful extraction; identifies the package it came from.
    /// </summary>
    internal const string InstallStampFileName = ".fryinstall";

    /// <summary>
    /// Identifies a package file by size and last-write time, which change whenever the file is replaced.
    /// </summary>
    internal readonly record struct InstallStamp(long Length, long LastWriteUtcTicks)
    {
        public static InstallStamp Of(string packageFilePath)
        {
            var info = new FileInfo(packageFilePath);
            return new InstallStamp(info.Length, info.LastWriteTimeUtc.Ticks);
        }

        public bool MatchesFile(string stampPath)
        {
            try
            {
                if (!File.Exists(stampPath)) return false;
                var stored = JsonSerializer.Deserialize<InstallStamp>(File.ReadAllText(stampPath));
                return stored == this;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void WriteTo(string stampPath) => File.WriteAllText(stampPath, JsonSerializer.Serialize(this));
    }

    /// <summary>
    /// Reads the plugin.json of a .fryplugin package without extracting it, for example to check the plugin's
    /// id, version and requirements before installing.
    /// </summary>
    /// <returns>The manifest, or null when the package has none.</returns>
    /// <exception cref="PluginException">plugin.json is not valid JSON.</exception>
    public static PluginManifest? ReadManifest(string packageFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFilePath);
        using var archive = ZipFile.OpenRead(packageFilePath);
        return ReadManifest(archive, out _);
    }

    private static PluginManifest? ReadManifest(ZipArchive archive, out string manifestFolder)
    {
        // Prefer a manifest at the archive root; accept one inside a single top-level folder, which is what
        // zipping a folder (rather than its contents) produces.
        var manifestEntry =
            archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, ManifestFileName, StringComparison.OrdinalIgnoreCase)) ??
            archive.Entries
                .Where(e => string.Equals(Path.GetFileName(e.FullName), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName.Count(c => c == '/'))
                .FirstOrDefault();

        manifestFolder = string.Empty;
        if (manifestEntry == null) return null;

        manifestFolder = Path.GetDirectoryName(manifestEntry.FullName) ?? string.Empty;
        using var stream = manifestEntry.Open();
        try
        {
            return JsonSerializer.Deserialize<PluginManifest>(stream);
        }
        catch (JsonException ex)
        {
            throw new PluginException($"The package's plugin.json is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts <paramref name="packageFilePath"/> to <c>&lt;targetPluginsDirectory&gt;/&lt;id&gt;/&lt;version&gt;</c>
    /// and loads its entry assembly.
    /// </summary>
    public static FryPluginPackageResult UnpackAndLoad(string packageFilePath, string targetPluginsDirectory) =>
        UnpackAndLoad(packageFilePath, targetPluginsDirectory, diagnostics: null);

    /// <summary>
    /// Extracts and loads a package like <see cref="UnpackAndLoad(string, string)"/>, adding anything worth
    /// reporting to <paramref name="diagnostics"/>.
    /// </summary>
    public static FryPluginPackageResult UnpackAndLoad(string packageFilePath, string targetPluginsDirectory, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPluginsDirectory);

        if (!File.Exists(packageFilePath))
        {
            throw new FileNotFoundException($"Plugin package '{packageFilePath}' not found.");
        }

        Directory.CreateDirectory(targetPluginsDirectory);

        // 1. Read manifest from ZIP archive
        PluginManifest? manifest;
        string manifestFolder;
        using (var archive = ZipFile.OpenRead(packageFilePath))
        {
            manifest = ReadManifest(archive, out manifestFolder);
        }

        if (manifest == null)
        {
            diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Extraction, PluginDiagnosticSeverity.Warning,
                "The package has no plugin.json; its id is taken from the file name and its version defaults to 1.0.0.",
                Path: packageFilePath));
        }

        var pluginId = !string.IsNullOrWhiteSpace(manifest?.Id)
            ? manifest.Id
            : Path.GetFileNameWithoutExtension(packageFilePath);

        var pluginVersion = !string.IsNullOrWhiteSpace(manifest?.Version)
            ? manifest.Version.Trim()
            : "1.0.0";

        // Security: validate destination directory against directory traversal
        var destinationFolder = PluginIdValidator.ResolveInstallDirectory(targetPluginsDirectory, pluginId, pluginVersion);

        // 2. Extract package contents, unless this exact package was already extracted there. Re-extracting
        // on every launch was the bulk of startup time for large plugins.
        var stampPath = Path.Combine(destinationFolder, InstallStampFileName);
        var stamp = InstallStamp.Of(packageFilePath);
        if (!stamp.MatchesFile(stampPath))
        {
            if (Directory.Exists(destinationFolder))
            {
                try
                {
                    Directory.Delete(destinationFolder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Typically a file locked by an already-loaded copy; extraction below overwrites what it can.
                    diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Extraction, PluginDiagnosticSeverity.Warning,
                        $"The previous install could not be removed first: {ex.Message}", PluginId: pluginId, Path: destinationFolder, Exception: ex));
                }
            }

            Directory.CreateDirectory(destinationFolder);

            // ExtractToDirectory rejects entries that would land outside the folder.
            ZipFile.ExtractToDirectory(packageFilePath, destinationFolder, overwriteFiles: true);

            // Written last, so an interrupted extraction is redone next time.
            stamp.WriteTo(stampPath);
        }

        // 3. Resolve entryPoint DLL, relative to the folder the manifest was in
        var contentFolder = Path.Combine(destinationFolder, manifestFolder);
        string? entryPointDll = null;
        if (!string.IsNullOrWhiteSpace(manifest?.EntryPoint))
        {
            var declared = Path.Combine(contentFolder, manifest.EntryPoint);
            if (File.Exists(declared))
            {
                entryPointDll = declared;
            }
            else
            {
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Extraction, PluginDiagnosticSeverity.Warning,
                    $"plugin.json names entry point '{manifest.EntryPoint}', which is not in the package; guessing the entry assembly instead.",
                    PluginId: pluginId, Path: packageFilePath));
            }
        }

        if (entryPointDll == null)
        {
            entryPointDll =
                PluginAssemblyLoader.FindAssemblyReferencingSdk(Directory.GetFiles(contentFolder, "*.dll", SearchOption.TopDirectoryOnly)) ??
                PluginAssemblyLoader.FindAssemblyReferencingSdk(Directory.GetFiles(destinationFolder, "*.dll", SearchOption.AllDirectories)) ??
                throw new FileNotFoundException($"No valid entry point assembly found in extracted package for '{pluginId}'.");

            // A missing plugin.json and a wrong entryPoint were reported above; this covers an omitted one.
            if (manifest != null && string.IsNullOrWhiteSpace(manifest.EntryPoint))
            {
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Extraction, PluginDiagnosticSeverity.Warning,
                    $"Guessed entry assembly '{Path.GetFileName(entryPointDll)}'. Set \"entryPoint\" in plugin.json.",
                    PluginId: pluginId, Path: packageFilePath));
            }
        }

        // 4. Load assembly into isolated collectible context
        var isCollectible = manifest?.IsCollectible ?? true;
        var package = PluginAssemblyLoader.LoadAssembly(entryPointDll, isCollectible, diagnostics);

        manifest ??= new PluginManifest
        {
            Id = pluginId,
            Name = pluginId,
            Version = pluginVersion,
            EntryPoint = Path.GetFileName(entryPointDll)
        };

        package.Manifest = manifest;

        return new FryPluginPackageResult
        {
            Manifest = manifest,
            AssemblyPackage = package,
            InstallDirectory = destinationFolder
        };
    }
}
