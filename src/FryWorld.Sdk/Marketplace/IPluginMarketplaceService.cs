using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Marketplace;

/// <summary>Browses the plugin catalog and installs, updates and uninstalls plugins.</summary>
public interface IPluginMarketplaceService
{
    /// <summary>Raised after the catalog has been fetched.</summary>
    event Action? CatalogRefreshed;

    /// <summary>The catalog as last fetched, without a network request.</summary>
    IReadOnlyList<MarketplacePluginItem> GetCachedCatalog();

    /// <summary>Fetches the catalog, or returns the cached copy when offline.</summary>
    Task<IReadOnlyList<MarketplacePluginItem>> FetchCatalogAsync(bool forceRefresh = false, CancellationToken ct = default);

    /// <summary>Downloads a plugin package, verified against its published checksum, and returns its path.</summary>
    Task<string> DownloadPackageAsync(
        MarketplacePluginItem item,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    /// <summary>Installs and mounts a downloaded or local .fryplugin.</summary>
    Task InstallPackageAsync(string packageFilePath, CancellationToken ct = default);

    /// <summary>Unmounts, unloads and deletes a plugin.</summary>
    Task UninstallPluginAsync(string pluginId, CancellationToken ct = default);

    /// <summary>Whether the plugin is installed.</summary>
    bool IsPluginInstalled(string pluginId);

    /// <summary>Whether the catalog has a newer version than <paramref name="installedVersion"/>.</summary>
    bool HasUpdateAvailable(string pluginId, string installedVersion);

    /// <summary>
    /// The installed version of <paramref name="pluginId"/> that loads at the next start, or null when it is not
    /// installed. Implementations written before this member existed return null.
    /// </summary>
    string? GetInstalledVersion(string pluginId) => null;

    /// <summary>Checks a catalog release against this host before downloading it.</summary>
    PluginCompatibilityResult CheckCompatibility(MarketplacePluginVersion release) => PluginCompatibility.Check(release);
}
