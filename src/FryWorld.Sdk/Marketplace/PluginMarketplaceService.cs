using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Marketplace;

/// <summary>
/// Service managing remote marketplace catalog discovery, package downloads, and installation.
/// </summary>
public sealed class PluginMarketplaceService : IPluginMarketplaceService, IDisposable
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _catalogUrl;
    private readonly string _pluginsDirectory;
    private readonly PluginHost _host;
    private readonly List<MarketplacePluginItem> _cachedCatalog = new();
    private readonly object _lock = new();

    public event Action? CatalogRefreshed;

    public PluginMarketplaceService(
        PluginHost host,
        string pluginsDirectory,
        string? catalogUrl = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _pluginsDirectory = pluginsDirectory ?? throw new ArgumentNullException(nameof(pluginsDirectory));
        _catalogUrl = catalogUrl ?? "https://raw.githubusercontent.com/codefrydev/PDFCreator-resources/refs/heads/main/plugins/catalog.json";
    }

    public IReadOnlyList<MarketplacePluginItem> GetCachedCatalog()
    {
        lock (_lock) { return _cachedCatalog.ToList(); }
    }

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
        catch
        {
            // Fallback to local cached items if offline
            lock (_lock) { return _cachedCatalog.ToList(); }
        }
    }

    public async Task<string> DownloadPackageAsync(
        MarketplacePluginItem item,
        Action<double>? progressCallback = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.DownloadUrl);

        var tempDir = Path.Combine(Path.GetTempPath(), "FryWorld", "Downloads");
        Directory.CreateDirectory(tempDir);

        var tempFilePath = Path.Combine(tempDir, $"{item.Id}_{item.Version}.fryplugin");
        if (File.Exists(tempFilePath))
        {
            File.Delete(tempFilePath);
        }

        using var response = await _httpClient.GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            if (totalBytes > 0 && progressCallback != null)
            {
                progressCallback((double)totalRead / totalBytes * 100.0);
            }
        }

        return tempFilePath;
    }

    public async Task InstallPackageAsync(string packageFilePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFilePath);

        var result = FryPluginPackageLoader.UnpackAndLoad(packageFilePath, _pluginsDirectory);
        foreach (var plugin in result.AssemblyPackage.Plugins)
        {
            await _host.MountPluginAsync(plugin, ct);
        }
    }

    public async Task UninstallPluginAsync(string pluginId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        await _host.UnmountPluginAsync(pluginId);

        var targetFolder = Path.Combine(_pluginsDirectory, pluginId);
        if (Directory.Exists(targetFolder))
        {
            try
            {
                Directory.Delete(targetFolder, recursive: true);
            }
            catch { }
        }
    }

    public bool IsPluginInstalled(string pluginId)
    {
        var targetFolder = Path.Combine(_pluginsDirectory, pluginId);
        return Directory.Exists(targetFolder) || _host.GetRegisteredPlugins().Any(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasUpdateAvailable(string pluginId, string installedVersion)
    {
        lock (_lock)
        {
            var item = _cachedCatalog.FirstOrDefault(c => string.Equals(c.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            if (item == null) return false;

            if (Version.TryParse(item.Version, out var remoteVer) && Version.TryParse(installedVersion, out var localVer))
            {
                return remoteVer > localVer;
            }

            return !string.Equals(item.Version, installedVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Dispose()
    {
        // cooperative
    }
}
