using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FryWorld.Sdk.Marketplace;

/// <summary>
/// Settings for <see cref="PluginMarketplaceService"/>.
/// </summary>
public sealed class PluginMarketplaceOptions
{
    /// <summary>Catalog URL; the default FryWorld catalog when null.</summary>
    public string? CatalogUrl { get; set; }

    /// <summary>Client for catalog and package downloads; a shared default client when null. Not disposed by the service.</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>This host's version, checked against plugins' <c>minHostVersion</c>/<c>maxHostVersion</c>; <see cref="PluginCompatibility.DefaultHostVersion"/> when null.</summary>
    public Version? HostVersion { get; set; }

    /// <summary>
    /// When true, a download whose catalog entry has no <c>sha256</c> is refused. When false (the default),
    /// such downloads are allowed with a warning; downloads that have a checksum are always verified.
    /// </summary>
    public bool RequireChecksum { get; set; }

    /// <summary>Receives catalog and install problems. Defaults to no logging.</summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;
}

/// <summary>
/// Service managing remote marketplace catalog discovery, package downloads, and installation.
/// </summary>
public sealed class PluginMarketplaceService : IPluginMarketplaceService, IDisposable
{
    private const string DefaultCatalogUrl = "https://raw.githubusercontent.com/codefrydev/PDFCreator-resources/refs/heads/main/plugins/catalog.json";
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _catalogUrl;
    private readonly string _pluginsDirectory;
    private readonly PluginHost _host;
    private readonly HttpClient _httpClient;
    private readonly PluginMarketplaceOptions _options;
    private readonly List<MarketplacePluginItem> _cachedCatalog = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public event Action? CatalogRefreshed;

    /// <summary>Creates the service for plugins installed in <paramref name="pluginsDirectory"/>.</summary>
    public PluginMarketplaceService(
        PluginHost host,
        string pluginsDirectory,
        string? catalogUrl = null)
        : this(host, pluginsDirectory, new PluginMarketplaceOptions { CatalogUrl = catalogUrl })
    {
    }

    /// <summary>Creates the service with the given options.</summary>
    public PluginMarketplaceService(PluginHost host, string pluginsDirectory, PluginMarketplaceOptions options)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _pluginsDirectory = pluginsDirectory ?? throw new ArgumentNullException(nameof(pluginsDirectory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _catalogUrl = options.CatalogUrl ?? DefaultCatalogUrl;
        _httpClient = options.HttpClient ?? SharedHttpClient;
    }

    /// <inheritdoc />
    public IReadOnlyList<MarketplacePluginItem> GetCachedCatalog()
    {
        lock (_lock) { return _cachedCatalog.ToList(); }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarketplacePluginItem>> FetchCatalogAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cachedCatalog.Count > 0 && !forceRefresh)
            {
                return _cachedCatalog.ToList();
            }
        }

        try
        {
            var response = await _httpClient.GetStringAsync(_catalogUrl, ct);
            var items = JsonSerializer.Deserialize<List<MarketplacePluginItem>>(response) ?? new List<MarketplacePluginItem>();

            lock (_lock)
            {
                _cachedCatalog.Clear();
                _cachedCatalog.AddRange(items);
            }

            CatalogRefreshed?.Invoke();
            return items;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            // Offline, a bad catalog URL or a bad catalog: keep showing what was fetched last. Only the
            // caller's own cancellation propagates.
            _options.Logger.LogWarning(ex, "Could not fetch the plugin catalog from {CatalogUrl}; using the cached copy.", _catalogUrl);
            lock (_lock) { return _cachedCatalog.ToList(); }
        }
    }

    /// <summary>
    /// Downloads <paramref name="item"/>'s package and verifies it against the catalog's published SHA-256.
    /// </summary>
    /// <returns>Path of the downloaded .fryplugin.</returns>
    /// <exception cref="PluginException">The download does not match its checksum (it is deleted), or a checksum is required and the catalog has none.</exception>
    public async Task<string> DownloadPackageAsync(
        MarketplacePluginItem item,
        Action<double>? progressCallback = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.DownloadUrl);

        // Both end up in a file name, so a catalog entry must not be able to point it outside the folder.
        PluginIdValidator.ValidateIdentifier(item.Id, "item.Id");
        PluginIdValidator.ValidateIdentifier(item.Version, "item.Version");

        var expectedSha256 = FindExpectedSha256(item);
        if (expectedSha256 == null)
        {
            if (_options.RequireChecksum)
            {
                throw new PluginException($"The catalog publishes no checksum for '{item.Id}' {item.Version}, and this host requires one.");
            }
            _options.Logger.LogWarning("The catalog publishes no checksum for {PluginId} {Version}; installing it unverified.", item.Id, item.Version);
        }

        var downloadDir = Path.Combine(Path.GetTempPath(), "FryWorld", "Downloads");
        Directory.CreateDirectory(downloadDir);
        var finalPath = Path.Combine(downloadDir, $"{item.Id}_{item.Version}.fryplugin");
        var partialPath = finalPath + ".partial";

        string actualSha256;
        try
        {
            using var response = await _httpClient.GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    hash.AppendData(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0 && progressCallback != null)
                    {
                        progressCallback((double)totalRead / totalBytes * 100.0);
                    }
                }
            }

            actualSha256 = Convert.ToHexString(hash.GetHashAndReset());
        }
        catch
        {
            TryDeleteFile(partialPath);
            throw;
        }

        if (expectedSha256 != null && !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(partialPath);
            throw new PluginException(
                $"The package downloaded for '{item.Id}' {item.Version} does not match its published checksum and was deleted. " +
                "The download may be corrupt or tampered with.");
        }

        File.Move(partialPath, finalPath, overwrite: true);
        return finalPath;
    }

    /// <summary>
    /// The SHA-256 the catalog publishes for the release <paramref name="item"/> describes: from the item's own
    /// version list, or else from the cached catalog (hosts often build a bare item for a chosen version).
    /// </summary>
    private string? FindExpectedSha256(MarketplacePluginItem item)
    {
        static string? Match(IEnumerable<MarketplacePluginVersion> versions, MarketplacePluginItem item) =>
            versions.FirstOrDefault(v =>
                    string.Equals(v.DownloadUrl, item.DownloadUrl, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.Version, item.Version, StringComparison.OrdinalIgnoreCase))
                ?.Sha256 is { Length: > 0 } sha ? sha : null;

        var fromItem = Match(item.Versions, item);
        if (fromItem != null) return fromItem;

        lock (_lock)
        {
            var catalogItem = _cachedCatalog.FirstOrDefault(c => string.Equals(c.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            return catalogItem == null ? null : Match(catalogItem.Versions, item);
        }
    }

    /// <summary>
    /// Installs a .fryplugin and mounts it. A loaded copy of the same plugin (an older version) is unmounted and
    /// unloaded first, and its version folder is removed, or marked for removal at next start if in use.
    /// </summary>
    /// <exception cref="PluginIncompatibleException">The plugin requires a different host or runtime.</exception>
    public async Task InstallPackageAsync(string packageFilePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFilePath);

        var manifest = FryPluginPackageLoader.ReadManifest(packageFilePath);
        if (manifest != null)
        {
            var compatibility = PluginCompatibility.Check(manifest, _options.HostVersion);
            if (!compatibility.IsCompatible)
            {
                throw new PluginIncompatibleException(manifest.Id, compatibility);
            }
        }

        var pluginId = !string.IsNullOrWhiteSpace(manifest?.Id) ? manifest.Id : Path.GetFileNameWithoutExtension(packageFilePath);
        await RemoveLoadedPluginAsync(pluginId);

        var result = FryPluginPackageLoader.UnpackAndLoad(packageFilePath, _pluginsDirectory);

        // This version is now the installed one: undo an earlier uninstall and retire other versions.
        var pluginDirectory = Path.GetDirectoryName(result.InstallDirectory)!;
        TryDeleteFile(Path.Combine(pluginDirectory, PluginInstallLayout.UninstallMarkerFileName));
        foreach (var otherVersion in Directory.GetDirectories(pluginDirectory)
                     .Where(d => !string.Equals(Path.GetFullPath(d), Path.GetFullPath(result.InstallDirectory), StringComparison.OrdinalIgnoreCase)))
        {
            PluginInstallLayout.DeleteOrMark(otherVersion, PluginInstallLayout.RemoveMarkerFileName);
        }

        foreach (var plugin in result.AssemblyPackage.Plugins)
        {
            if (!string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                await RemoveLoadedPluginAsync(plugin.Id);
            }
            await _host.MountPluginAsync(plugin, ct);
        }
    }

    /// <summary>
    /// Unmounts and unloads <paramref name="pluginId"/>, deletes its files (or marks them for removal at next
    /// start when they are in use), and deletes any .fryplugin for it in the plugins folder, which would
    /// otherwise reinstall it on the next start.
    /// </summary>
    public async Task UninstallPluginAsync(string pluginId, CancellationToken ct = default)
    {
        // The id becomes part of a path that is deleted recursively, so it must not be able to escape
        // the plugins directory ("..", separators, rooted paths).
        PluginIdValidator.ValidateIdentifier(pluginId, nameof(pluginId));

        await RemoveLoadedPluginAsync(pluginId);

        var targetFolder = PluginIdValidator.ResolveInstallDirectory(_pluginsDirectory, pluginId, version: null);
        if (!PluginInstallLayout.DeleteOrMark(targetFolder, PluginInstallLayout.UninstallMarkerFileName))
        {
            _host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.Uninstall, PluginDiagnosticSeverity.Info,
                "Some of the plugin's files are in use; they will be removed when the app next starts.",
                PluginId: pluginId, Path: targetFolder));
        }

        foreach (var package in FindPackagesFor(pluginId))
        {
            TryDeleteFile(package);
        }
    }

    /// <summary>Unmounts the plugin, removes it from the host and unloads its assemblies, if it is loaded.</summary>
    private async Task RemoveLoadedPluginAsync(string pluginId)
    {
        // Capture the mounted instance first: unmounting removes it from the host.
        var mounted = _host.GetRegistration(pluginId)?.Plugin;

        await _host.UnmountPluginAsync(pluginId);
        _host.UnregisterPlugin(pluginId);

        // Unload the plugin's assemblies so their files can be deleted and their memory freed.
        var found = mounted != null
            ? PluginAssemblyLoader.TryGetPackage(mounted, out var package)
            : PluginAssemblyLoader.TryGetPackage(pluginId, out package);
        if (found)
        {
            package.Dispose();
        }
    }

    /// <summary>.fryplugin files at the top of the plugins folder whose manifest id is <paramref name="pluginId"/>.</summary>
    private IEnumerable<string> FindPackagesFor(string pluginId)
    {
        if (!Directory.Exists(_pluginsDirectory)) yield break;

        foreach (var file in Directory.GetFiles(_pluginsDirectory, "*.fryplugin", SearchOption.TopDirectoryOnly))
        {
            PluginManifest? manifest = null;
            try
            {
                manifest = FryPluginPackageLoader.ReadManifest(file);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or PluginException)
            {
                // Not a readable package; not ours to delete.
            }

            var id = !string.IsNullOrWhiteSpace(manifest?.Id) ? manifest.Id : Path.GetFileNameWithoutExtension(file);
            if (string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase)) yield return file;
        }
    }

    /// <inheritdoc />
    public bool IsPluginInstalled(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) return false;
        try
        {
            PluginIdValidator.ValidateIdentifier(pluginId, nameof(pluginId));
        }
        catch (ArgumentException)
        {
            return false;
        }

        var targetFolder = Path.Combine(_pluginsDirectory, pluginId);
        if (Directory.Exists(targetFolder) && !PluginInstallLayout.IsUninstalled(targetFolder))
        {
            return true;
        }

        return _host.GetPluginState(pluginId) == PluginLifecycleState.Active;
    }

    /// <summary>
    /// The version of <paramref name="pluginId"/> that loads at the next start (the highest installed version not
    /// marked for removal), or null when it is not installed.
    /// </summary>
    public string? GetInstalledVersion(string pluginId)
    {
        if (!IsPluginInstalled(pluginId)) return null;

        var pluginFolder = Path.Combine(_pluginsDirectory, pluginId);
        if (!Directory.Exists(pluginFolder)) return null;

        return Directory.GetDirectories(pluginFolder)
            .Where(d => !PluginInstallLayout.IsRemoved(d) && PluginAssemblyLoader.TryReadManifest(Path.Combine(d, "plugin.json"), out _))
            .Select(d => PluginAssemblyLoader.TryReadManifest(Path.Combine(d, "plugin.json"), out var m) ? m.Version : Path.GetFileName(d))
            .Select(v => (Text: v, Parsed: PluginVersion.TryParse(v, out var p) ? p : (PluginVersion?)null))
            .OrderByDescending(v => v.Parsed ?? default)
            .Select(v => v.Text)
            .FirstOrDefault();
    }

    /// <summary>Checks a catalog release against this host before downloading it.</summary>
    public PluginCompatibilityResult CheckCompatibility(MarketplacePluginVersion release) =>
        PluginCompatibility.Check(release, _options.HostVersion);

    /// <inheritdoc />
    public bool HasUpdateAvailable(string pluginId, string installedVersion)
    {
        lock (_lock)
        {
            var item = _cachedCatalog.FirstOrDefault(c => string.Equals(c.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            if (item == null) return false;

            if (PluginVersion.TryParse(item.Version, out var remote) && PluginVersion.TryParse(installedVersion, out var local))
            {
                return remote > local;
            }

            return !string.Equals(item.Version, installedVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Nothing to release; an injected <see cref="HttpClient"/> belongs to the caller.</summary>
    public void Dispose()
    {
    }
}
