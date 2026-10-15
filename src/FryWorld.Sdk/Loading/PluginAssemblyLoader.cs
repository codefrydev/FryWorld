using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>Full path of the plugin's entry assembly.</summary>
    public string AssemblyPath { get; }

    /// <summary>The plugins created from the entry assembly.</summary>
    public IReadOnlyList<IFryPlugin> Plugins { get; }

    /// <summary>The load context isolating the plugin's assemblies.</summary>
    public CollectiblePluginLoadContext Context => _context;

    /// <summary>Whether disposing this package unloads its assemblies from memory.</summary>
    public bool IsCollectible => _context.IsCollectible;

    /// <summary>The plugin.json next to the entry assembly, when there is one.</summary>
    public PluginManifest? Manifest { get; internal set; }

    /// <summary>Wraps plugins created from <paramref name="assemblyPath"/> in <paramref name="context"/>.</summary>
    public PluginAssemblyPackage(string assemblyPath, IReadOnlyList<IFryPlugin> plugins, CollectiblePluginLoadContext context)
    {
        AssemblyPath = assemblyPath;
        Plugins = plugins;
        _context = context;
    }

    /// <summary>
    /// Stops tracking the package and, when it is collectible, starts unloading its assemblies. Unmount the
    /// plugins first: the assemblies are only freed once nothing references the plugins or their types.
    /// </summary>
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
    private const string SdkAssemblyName = "FryWorld.Sdk";

    // Keyed by reference. Packages are removed when disposed; holding them any longer would keep a
    // collectible load context, and every assembly in it, alive for the life of the process.
    private static readonly ConcurrentDictionary<PluginAssemblyPackage, byte> _activePackages = new();

    /// <summary>Packages loaded and not yet disposed.</summary>
    public static IReadOnlyCollection<PluginAssemblyPackage> ActivePackages => _activePackages.Keys.ToList();

    /// <summary>
    /// Stops tracking <paramref name="package"/>. Called by <see cref="PluginAssemblyPackage.Dispose"/>.
    /// </summary>
    public static void Forget(PluginAssemblyPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _activePackages.TryRemove(package, out _);
    }

    /// <summary>
    /// Finds the loaded package that created <paramref name="plugin"/>.
    /// </summary>
    public static bool TryGetPackage(IFryPlugin plugin, out PluginAssemblyPackage package)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        foreach (var candidate in _activePackages.Keys)
        {
            if (candidate.Plugins.Any(p => ReferenceEquals(p, plugin)))
            {
                package = candidate;
                return true;
            }
        }

        package = null!;
        return false;
    }

    /// <summary>
    /// Finds the loaded package that contains the plugin with <paramref name="pluginId"/>.
    /// </summary>
    public static bool TryGetPackage(string pluginId, out PluginAssemblyPackage package)
    {
        foreach (var candidate in _activePackages.Keys)
        {
            if (candidate.Plugins.Any(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase)))
            {
                package = candidate;
                return true;
            }
        }

        package = null!;
        return false;
    }

    /// <summary>
    /// Loads a plugin assembly from disk into an isolated <see cref="CollectiblePluginLoadContext"/>
    /// and instantiates all discovered <see cref="IFryPlugin"/> types.
    /// When <paramref name="isCollectibleOverride"/> is not provided, the companion <c>plugin.json</c>
    /// manifest is consulted (defaulting to collectible <c>true</c> if not declared).
    /// </summary>
    /// <exception cref="PluginAbiMismatchException">The plugin was built against a host assembly version this host does not provide.</exception>
    public static PluginAssemblyPackage LoadAssembly(string assemblyPath, bool? isCollectibleOverride = null) =>
        LoadAssembly(assemblyPath, isCollectibleOverride, diagnostics: null);

    /// <summary>
    /// Loads a plugin assembly like <see cref="LoadAssembly(string, bool?)"/>, adding anything worth reporting to <paramref name="diagnostics"/>.
    /// </summary>
    /// <exception cref="PluginAbiMismatchException">The plugin was built against a host assembly version this host does not provide.</exception>
    public static PluginAssemblyPackage LoadAssembly(string assemblyPath, bool? isCollectibleOverride, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file '{assemblyPath}' not found.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        var manifest = FindCompanionManifest(fullPath);
        bool isCollectible = isCollectibleOverride ?? manifest?.IsCollectible ?? true;
        var context = new CollectiblePluginLoadContext(fullPath, isCollectible);

        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            var pluginTypes = DiscoverPluginTypes(assembly, fullPath, manifest, diagnostics);

            var plugins = new List<IFryPlugin>();
            foreach (var type in pluginTypes)
            {
                plugins.Add(CreatePlugin(type));
            }

            if (!string.IsNullOrWhiteSpace(manifest?.Id) && plugins.Count > 0 &&
                !plugins.Any(p => string.Equals(p.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)))
            {
                // The folder and package are named by the manifest id, the host by the code id; when they
                // differ, uninstall and updates cannot find the plugin.
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Instantiation, PluginDiagnosticSeverity.Warning,
                    $"plugin.json says id '{manifest.Id}' but the plugin's Id is '{string.Join("', '", plugins.Select(p => p.Id))}'. Make them match.",
                    PluginId: manifest.Id, Path: fullPath));
            }

            var package = new PluginAssemblyPackage(fullPath, plugins, context) { Manifest = manifest };
            _activePackages.TryAdd(package, 0);
            return package;
        }
        catch (Exception ex)
        {
            if (context.IsCollectible)
            {
                context.Unload();
            }

            var translated = TranslateLoadFailure(ex);
            if (!ReferenceEquals(translated, ex)) throw translated;
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

        // Default to collectible ALC when no readable manifest declares otherwise
        return FindCompanionManifest(assemblyPath)?.IsCollectible ?? true;
    }

    /// <summary>
    /// The plugin.json next to the assembly, or one folder up (the version-folder layout).
    /// </summary>
    private static PluginManifest? FindCompanionManifest(string assemblyPath)
    {
        var dir = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrEmpty(dir)) return null;

        if (TryReadManifest(Path.Combine(dir, "plugin.json"), out var manifest)) return manifest;

        var parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrEmpty(parent) && TryReadManifest(Path.Combine(parent, "plugin.json"), out manifest)) return manifest;

        return null;
    }

    /// <summary>
    /// Scans a directory for plugin packages and unpacked plugin folders, loading valid packages.
    /// </summary>
    public static IReadOnlyList<PluginAssemblyPackage> DiscoverAndLoadDirectory(string directoryPath) =>
        DiscoverAndLoadDirectoryCore(directoryPath, diagnostics: null);

    /// <summary>
    /// Scans a directory like <see cref="DiscoverAndLoadDirectory(string)"/>, adding a
    /// <see cref="PluginLoadDiagnostic"/> to <paramref name="diagnostics"/> for every package or folder that
    /// could not be loaded, and for every guess the loader had to make.
    /// </summary>
    public static IReadOnlyList<PluginAssemblyPackage> DiscoverAndLoadDirectory(string directoryPath, ICollection<PluginLoadDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return DiscoverAndLoadDirectoryCore(directoryPath, diagnostics);
    }

    /// <summary>
    /// Scans a directory like <see cref="DiscoverAndLoadDirectory(string, ICollection{PluginLoadDiagnostic})"/>
    /// on a thread-pool thread, so a UI thread stays responsive while packages are unpacked and assemblies load.
    /// </summary>
    /// <param name="directoryPath">The plugins directory.</param>
    /// <param name="diagnostics">
    /// Receives diagnostics from the background thread. Read it only after the returned task completes.
    /// </param>
    /// <param name="ct">Cancels the scan before it starts.</param>
    public static Task<IReadOnlyList<PluginAssemblyPackage>> DiscoverAndLoadDirectoryAsync(
        string directoryPath,
        ICollection<PluginLoadDiagnostic>? diagnostics = null,
        CancellationToken ct = default) =>
        Task.Run(() => DiscoverAndLoadDirectoryCore(directoryPath, diagnostics), ct);

    private static IReadOnlyList<PluginAssemblyPackage> DiscoverAndLoadDirectoryCore(string directoryPath, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<PluginAssemblyPackage>();
        }

        directoryPath = Path.GetFullPath(directoryPath);

        // Finish uninstalls and updates whose files were in use last time.
        PluginInstallLayout.CleanUpPendingRemovals(directoryPath, diagnostics);

        var packages = new List<PluginAssemblyPackage>();
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Accept(PluginAssemblyPackage package)
        {
            packages.Add(package);
            loadedPaths.Add(package.AssemblyPath);
            foreach (var plugin in package.Plugins) loadedIds.Add(plugin.Id);
        }

        // 1. Discover and unpack .fryplugin packages
        foreach (var pkgFile in Directory.GetFiles(directoryPath, "*.fryplugin", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var pkgResult = FryPluginPackageLoader.UnpackAndLoad(pkgFile, directoryPath, diagnostics);
                var duplicate = pkgResult.AssemblyPackage.Plugins.FirstOrDefault(p => loadedIds.Contains(p.Id));
                if (duplicate != null)
                {
                    // Typically the same plugin downloaded twice ("Snake.fryplugin", "Snake (1).fryplugin").
                    diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Discovery, PluginDiagnosticSeverity.Warning,
                        "Another package in the plugins folder already provides this plugin; this one was skipped.",
                        PluginId: duplicate.Id, Path: pkgFile));
                    pkgResult.Dispose();
                }
                else if (pkgResult.AssemblyPackage.Plugins.Count > 0)
                {
                    Accept(pkgResult.AssemblyPackage);
                }
                else
                {
                    diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Instantiation, PluginDiagnosticSeverity.Warning,
                        "The package contains no public, non-abstract IFryPlugin type.", PluginId: pkgResult.Manifest.Id, Path: pkgFile));
                    pkgResult.Dispose();
                }
            }
            catch (Exception ex)
            {
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Extraction, PluginDiagnosticSeverity.Error,
                    $"The package could not be loaded: {ex.Message}", Path: pkgFile, Exception: ex));
            }
        }

        // 2. Discover unpacked plugin subdirectories. The folder name is the plugin id, so a folder whose
        // plugin was already loaded from a package above is skipped rather than loaded a second time.
        foreach (var subDir in Directory.GetDirectories(directoryPath).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (loadedIds.Contains(Path.GetFileName(subDir)) || PluginInstallLayout.IsUninstalled(subDir)) continue;

            try
            {
                var entryDll = ResolvePluginDirectoryEntryDll(subDir, diagnostics);
                if (entryDll != null && !loadedPaths.Contains(entryDll))
                {
                    var package = LoadAssembly(entryDll, isCollectibleOverride: null, diagnostics);
                    if (package.Plugins.Count > 0 && !package.Plugins.Any(p => loadedIds.Contains(p.Id)))
                    {
                        Accept(package);
                    }
                    else
                    {
                        package.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.AssemblyLoad, PluginDiagnosticSeverity.Error,
                    $"The plugin folder could not be loaded: {ex.Message}", PluginId: Path.GetFileName(subDir), Path: subDir, Exception: ex));
            }
        }

        // 3. Discover top-level standalone DLLs (SearchOption.TopDirectoryOnly so subfolder dependencies aren't loaded)
        foreach (var dll in Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase))
        {
            // Native libraries and unrelated managed assemblies are not plugins; skip them without loading.
            if (loadedPaths.Contains(dll) || !ReferencesSdk(dll)) continue;

            try
            {
                var package = LoadAssembly(dll, isCollectibleOverride: null, diagnostics);
                if (package.Plugins.Count > 0 && !package.Plugins.Any(p => loadedIds.Contains(p.Id)))
                {
                    Accept(package);
                }
                else
                {
                    package.Dispose();
                }
            }
            catch (Exception ex)
            {
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.AssemblyLoad, PluginDiagnosticSeverity.Error,
                    $"The assembly could not be loaded: {ex.Message}", Path: dll, Exception: ex));
            }
        }

        return packages;
    }

    /// <summary>
    /// Resolves the entry assembly for a plugin directory, inspecting version subdirectories or plugin.json.
    /// When several versions are installed side by side, the highest version wins.
    /// </summary>
    public static string? ResolvePluginDirectoryEntryDll(string subDir) =>
        ResolvePluginDirectoryEntryDll(subDir, diagnostics: null);

    private static string? ResolvePluginDirectoryEntryDll(string subDir, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        if (!Directory.Exists(subDir)) return null;

        if (PluginInstallLayout.IsUninstalled(subDir)) return null;

        var versionDirs = Directory.GetDirectories(subDir)
            .Where(inner => File.Exists(Path.Combine(inner, "plugin.json")) && !PluginInstallLayout.IsRemoved(inner))
            .OrderByDescending(inner => Path.GetFileName(inner), PluginVersionComparer.Instance);

        foreach (var inner in versionDirs)
        {
            var dll = FindEntryDllInDirectory(inner, diagnostics);
            if (dll != null) return dll;
        }

        return FindEntryDllInDirectory(subDir, diagnostics);
    }

    /// <summary>
    /// Finds the entry DLL in a plugin folder by inspecting plugin.json manifest,
    /// then checking {folderName}.dll, and finally falling back to an assembly that references FryWorld.Sdk.
    /// </summary>
    public static string? FindEntryDllInDirectory(string dir) => FindEntryDllInDirectory(dir, diagnostics: null);

    internal static string? FindEntryDllInDirectory(string dir, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        if (!Directory.Exists(dir)) return null;
        dir = Path.GetFullPath(dir);

        var manifestFile = Path.Combine(dir, "plugin.json");
        if (TryReadManifest(manifestFile, out var manifest) && !string.IsNullOrWhiteSpace(manifest.EntryPoint))
        {
            var candidate = Path.Combine(dir, manifest.EntryPoint);
            if (File.Exists(candidate)) return candidate;

            diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Discovery, PluginDiagnosticSeverity.Warning,
                $"plugin.json names entry point '{manifest.EntryPoint}', which does not exist; guessing the entry assembly instead.",
                PluginId: manifest.Id, Path: dir));
        }

        var folderName = Path.GetFileName(dir);
        var candidateByName = Path.Combine(dir, $"{folderName}.dll");
        if (File.Exists(candidateByName)) return candidateByName;

        var guessed = FindAssemblyReferencingSdk(Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly));
        if (guessed != null)
        {
            diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Discovery, PluginDiagnosticSeverity.Warning,
                $"No entry point is declared; guessed '{Path.GetFileName(guessed)}'. Set \"entryPoint\" in plugin.json.",
                PluginId: manifest?.Id, Path: dir));
        }

        return guessed;
    }

    /// <summary>
    /// Picks the assembly most likely to hold the plugin: the first, by name, that references FryWorld.Sdk.
    /// Used only when the manifest does not name the entry point.
    /// </summary>
    internal static string? FindAssemblyReferencingSdk(IEnumerable<string> dllPaths) =>
        dllPaths.Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault(ReferencesSdk);

    /// <summary>
    /// Reads the assembly's metadata, without loading it, to see whether it references FryWorld.Sdk.
    /// </summary>
    private static bool ReferencesSdk(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) return false;

            var metadata = peReader.GetMetadataReader();
            if (metadata.IsAssembly && metadata.StringComparer.Equals(metadata.GetAssemblyDefinition().Name, SdkAssemblyName))
            {
                return false;
            }

            foreach (var handle in metadata.AssemblyReferences)
            {
                if (metadata.StringComparer.Equals(metadata.GetAssemblyReference(handle).Name, SdkAssemblyName))
                {
                    return true;
                }
            }
        }
        catch (BadImageFormatException)
        {
            // Native library or corrupt file: not a plugin.
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return false;
    }

    internal static bool TryReadManifest(string manifestFile, out PluginManifest manifest)
    {
        manifest = null!;
        if (!File.Exists(manifestFile)) return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestFile));
            if (parsed == null) return false;
            manifest = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IFryPlugin CreatePlugin(Type type)
    {
        try
        {
            return (IFryPlugin)Activator.CreateInstance(type)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new PluginException($"The constructor of plugin type '{type.FullName}' threw: {ex.InnerException.Message}", ex.InnerException);
        }
        catch (MissingMethodException ex)
        {
            throw new PluginException($"Plugin type '{type.FullName}' needs a public parameterless constructor.", ex);
        }
    }

    private static bool IsPluginType(Type? type) =>
        type is { IsAbstract: false, IsInterface: false } && typeof(IFryPlugin).IsAssignableFrom(type);

    /// <summary>
    /// Finds the plugin types to create, cheapest source first: the manifest's <c>entryType</c>, then
    /// <see cref="FryPluginEntryAttribute"/> on the assembly, and only then a scan of every public type.
    /// </summary>
    internal static IEnumerable<Type> DiscoverPluginTypes(Assembly assembly, string assemblyPath, PluginManifest? manifest, ICollection<PluginLoadDiagnostic>? diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(manifest?.EntryType))
        {
            var declared = assembly.GetType(manifest.EntryType, throwOnError: false);
            if (IsPluginType(declared)) return new[] { declared! };

            diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.Instantiation, PluginDiagnosticSeverity.Warning,
                $"plugin.json names entry type '{manifest.EntryType}', which is not a public, non-abstract IFryPlugin in the assembly; scanning instead.",
                PluginId: manifest.Id, Path: assemblyPath));
        }

        var attributed = assembly.GetCustomAttributes<FryPluginEntryAttribute>()
            .Select(a => a.PluginType)
            .Where(IsPluginType)
            .Distinct()
            .ToList();
        if (attributed.Count > 0) return attributed;

        try
        {
            return assembly.GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IFryPlugin).IsAssignableFrom(t))
                .ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.Where(e => e != null).Take(5))
            {
                var translated = TranslateLoadFailure(loaderException!);
                diagnostics?.Add(new PluginLoadDiagnostic(PluginLoadStage.AssemblyLoad, PluginDiagnosticSeverity.Warning,
                    $"Some types could not be loaded and were skipped: {translated.Message}", Path: assemblyPath, Exception: translated));
            }

            return ex.Types.Where(t => t != null && !t.IsAbstract && !t.IsInterface && typeof(IFryPlugin).IsAssignableFrom(t))!.ToList()!;
        }
    }

    /// <summary>
    /// The runtime reports a plugin built against a host assembly version this host does not have as a bare
    /// "file not found". Turn that into a <see cref="PluginAbiMismatchException"/> that says what to do.
    /// </summary>
    internal static Exception TranslateLoadFailure(Exception ex)
    {
        if (ex is PluginAbiMismatchException) return ex;

        var fileName = ex switch
        {
            FileNotFoundException notFound => notFound.FileName,
            FileLoadException loadFailure => loadFailure.FileName,
            _ => null
        };

        if (ex is ReflectionTypeLoadException typeLoad)
        {
            var first = typeLoad.LoaderExceptions.FirstOrDefault(e => e != null);
            return first != null && TranslateLoadFailure(first) is PluginAbiMismatchException abi ? abi : ex;
        }

        if (string.IsNullOrWhiteSpace(fileName)) return ex;

        AssemblyName requested;
        try
        {
            requested = new AssemblyName(fileName);
        }
        catch (Exception e) when (e is ArgumentException or FileLoadException)
        {
            return ex;
        }

        if (requested.Name == null || !CollectiblePluginLoadContext.IsHostProvided(requested.Name)) return ex;

        var hostVersion = AssemblyLoadContext.Default.Assemblies
            .Select(a => a.GetName())
            .FirstOrDefault(n => string.Equals(n.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
            ?.Version;

        return new PluginAbiMismatchException(requested.Name, requested.Version, hostVersion, ex);
    }

    /// <summary>
    /// Orders version folder names such as "1.10.0", "1.9.2" and "2.0.0-beta" by <see cref="PluginVersion"/>;
    /// names that are not versions sort below all versions.
    /// </summary>
    private sealed class PluginVersionComparer : IComparer<string>
    {
        public static readonly PluginVersionComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var xIsVersion = PluginVersion.TryParse(x, out var vx);
            var yIsVersion = PluginVersion.TryParse(y, out var vy);

            if (xIsVersion && yIsVersion)
            {
                var byVersion = vx.CompareTo(vy);
                if (byVersion != 0) return byVersion;
            }
            else if (xIsVersion != yIsVersion)
            {
                return xIsVersion ? 1 : -1;
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
