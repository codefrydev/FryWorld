using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Marketplace;

public interface IPluginMarketplaceService
{
    event Action? CatalogRefreshed;

    IReadOnlyList<MarketplacePluginItem> GetCachedCatalog();

    Task<IReadOnlyList<MarketplacePluginItem>> FetchCatalogAsync(bool forceRefresh = false, CancellationToken ct = default);

    Task<string> DownloadPackageAsync(
        MarketplacePluginItem item,
        Action<double>? progressCallback = null,
        CancellationToken ct = default);

    Task InstallPackageAsync(string packageFilePath, CancellationToken ct = default);

    Task UninstallPluginAsync(string pluginId, CancellationToken ct = default);

    bool IsPluginInstalled(string pluginId);

    bool HasUpdateAvailable(string pluginId, string installedVersion);
}
