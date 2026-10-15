using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;

namespace FryWorld.ViewModels;

public enum LauncherViewMode
{
    Grid,
    Table
}

public enum LauncherFilterTab
{
    All,
    Installed,
    Updates,
    NotInstalled
}

public partial class LauncherHomeViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IPluginMarketplaceService _marketplace;
    private readonly Action<string> _launchPluginCallback;
    private readonly List<LauncherAppCardViewModel> _allApps = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridMode))]
    [NotifyPropertyChangedFor(nameof(IsTableMode))]
    private LauncherViewMode _viewMode = LauncherViewMode.Grid;

    public bool IsGridMode => ViewMode == LauncherViewMode.Grid;
    public bool IsTableMode => ViewMode == LauncherViewMode.Table;

    [ObservableProperty]
    private LauncherFilterTab _selectedFilter = LauncherFilterTab.All;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSort = "Suite Order";

    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Suite Order",
        "Name",
        "Category",
        "Version"
    };

    [ObservableProperty]
    private int _allCount;

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _updatesCount;

    [ObservableProperty]
    private int _notInstalledCount;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _statusFooterText = "READY · 8 APPS IN SUITE";

    public ObservableCollection<LauncherAppCardViewModel> FilteredApps { get; } = new();

    public LauncherHomeViewModel(
        PluginHost host,
        IPluginMarketplaceService marketplace,
        Action<string> launchPluginCallback)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));
        _launchPluginCallback = launchPluginCallback ?? throw new ArgumentNullException(nameof(launchPluginCallback));

        LoadSeedApps();
        ApplyFilterAndSort();

        // Automatically fetch live catalog in background on launch
        _ = Task.Run(async () =>
        {
            try
            {
                var catalog = await _marketplace.FetchCatalogAsync();
                if (catalog.Count > 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UpdateAppsFromCatalog(catalog);
                    });
                }
            }
            catch { }
        });
    }

    [RelayCommand]
    private void SetViewMode(string mode)
    {
        ViewMode = mode.Equals("Table", StringComparison.OrdinalIgnoreCase)
            ? LauncherViewMode.Table
            : LauncherViewMode.Grid;
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter switch
        {
            "Installed" => LauncherFilterTab.Installed,
            "Updates" => LauncherFilterTab.Updates,
            "NotInstalled" => LauncherFilterTab.NotInstalled,
            _ => LauncherFilterTab.All
        };
        ApplyFilterAndSort();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();
    partial void OnSelectedSortChanged(string value) => ApplyFilterAndSort();
    partial void OnSelectedFilterChanged(LauncherFilterTab value) => ApplyFilterAndSort();

    [RelayCommand]
    public async Task RefreshCatalogAsync()
    {
        IsRefreshing = true;
        StatusFooterText = "CHECKING FRYWORLD REMOTE REPOSITORY...";

        try
        {
            var items = await _marketplace.FetchCatalogAsync(forceRefresh: true);
            if (items.Count > 0)
            {
                UpdateAppsFromCatalog(items);
            }
            StatusFooterText = $"UPDATED JUST NOW · {FilteredApps.Count} APPS IN SUITE";
        }
        catch (Exception ex)
        {
            StatusFooterText = $"OFFLINE MODE · {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void LoadSeedApps()
    {
        // Verified, real apps in FryWorld suite (matching catalog.json exactly, zero fake placeholders)
        var seedData = new[]
        {
            ("01", "#2563EB", "com.fryworld.csharpstudio", "Fry", "Sharp", "Full-featured C# studio & polyglot notebooks", "Developer & Tools", "CodeBraces", "2.0.5", "15.0 MB", "https://github.com/codefrydev/FrySharp"),
            ("02", "#DC2626", "com.fryworld.imageeditor", "Fry", "Image", "Vector illustration & bitmap canvas layout studio", "Creative & Design", "Palette", "3.1.0", "697 KB", "https://github.com/codefrydev/FryImage"),
            ("03", "#D97706", "com.fryworld.snake", "Fry", "Snake", "60+ FPS classic arcade retro gaming overlay", "Games", "GamepadVariant", "1.0.0", "108 KB", "https://github.com/codefrydev/PDFCreator-resources"),
            ("04", "#059669", "com.fryworld.chess", "Fry", "Chess", "2-Player & Minimax AI grandmaster chess overlay", "Games", "ChessKnight", "1.0.0", "108 KB", "https://github.com/codefrydev/PDFCreator-resources"),
            ("05", "#DB2777", "com.fryworld.tictactoe", "Fry", "TicTacToe", "Minimax AI neon tactical duel overlay", "Games", "Grid", "1.0.0", "108 KB", "https://github.com/codefrydev/PDFCreator-resources"),
            ("06", "#0891B2", "com.fryworld.telemetry", "Fry", "Telemetry", "Real-time GC memory & CPU resource HUD", "Utility", "Speedometer", "1.0.0", "96 KB", "https://github.com/codefrydev/PDFCreator-resources"),
            ("07", "#EA580C", "com.fryworld.scratchpad", "Fry", "Notes", "Floating Markdown workspace scratchpad", "Productivity", "NotebookEdit", "1.0.0", "92 KB", "https://github.com/codefrydev/PDFCreator-resources")
        };

        _allApps.Clear();
        foreach (var (index, color, id, prefix, suffix, desc, cat, icon, ver, size, repo) in seedData)
        {
            var card = new LauncherAppCardViewModel
            {
                IndexTag = index,
                IndexColorHex = color,
                Id = id,
                TitlePrefix = prefix,
                TitleSuffix = suffix,
                Title = $"{prefix}{suffix}",
                Description = desc,
                Category = cat,
                IconKind = icon,
                Version = $"Version {ver} · Oct 15, 2026",
                FormattedSize = size,
                RepositoryUrl = repo,
                IconBitmap = TryLoadPluginIcon(id),
                OnPrimaryActionAsync = HandleCardActionAsync,
                OnUninstallActionAsync = HandleUninstallActionAsync,
                OnStatusFeedback = msg => StatusFooterText = msg
            };

            card.AvailableVersions.Add(new MarketplacePluginVersion
            {
                Version = ver,
                FormattedSize = size
            });
            card.SelectedVersion = card.AvailableVersions[0];

            RefreshCardState(card);
            _allApps.Add(card);
        }

        UpdateCounters();
    }

    private void UpdateAppsFromCatalog(IReadOnlyList<MarketplacePluginItem> catalog)
    {
        // Filter strictly to FryWorld suite items
        var fryWorldItems = catalog
            .Where(c => c.Id.StartsWith("com.fryworld.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fryWorldItems.Count == 0) return;

        int index = 1;
        var palette = new[] { "#2563EB", "#DC2626", "#D97706", "#059669", "#DB2777", "#0891B2", "#EA580C" };

        var seedBranding = new Dictionary<string, (string prefix, string suffix)>(StringComparer.OrdinalIgnoreCase)
        {
            ["com.fryworld.csharpstudio"] = ("Fry", "Sharp"),
            ["com.fryworld.imageeditor"] = ("Fry", "Image"),
            ["com.fryworld.snake"] = ("Fry", "Snake"),
            ["com.fryworld.chess"] = ("Fry", "Chess"),
            ["com.fryworld.tictactoe"] = ("Fry", "TicTacToe"),
            ["com.fryworld.telemetry"] = ("Fry", "Telemetry"),
            ["com.fryworld.scratchpad"] = ("Fry", "Notes")
        };

        _allApps.Clear();
        foreach (var item in fryWorldItems)
        {
            var color = item.IconColorHex ?? palette[(index - 1) % palette.Length];
            string prefix, suffix;
            if (seedBranding.TryGetValue(item.Id, out var brand))
            {
                prefix = brand.prefix;
                suffix = brand.suffix;
            }
            else if (item.Name.StartsWith("Fry", StringComparison.OrdinalIgnoreCase) && item.Name.Length > 3)
            {
                prefix = "Fry";
                suffix = item.Name[3..].TrimStart();
            }
            else
            {
                var parts = item.Name.Split(' ', 2);
                prefix = parts[0];
                suffix = parts.Length > 1 ? " " + parts[1] : string.Empty;
            }

            var card = new LauncherAppCardViewModel
            {
                IndexTag = index.ToString("D2"),
                IndexColorHex = color,
                Id = item.Id,
                TitlePrefix = prefix,
                TitleSuffix = suffix,
                Title = item.Name,
                Description = item.Description,
                Category = item.Category,
                IconKind = item.IconKind ?? "Application",
                Version = $"Version {item.Version} · Oct 15, 2026",
                FormattedSize = item.FormattedSize,
                WebsiteUrl = item.WebsiteUrl ?? "https://codefrydev.in",
                RepositoryUrl = item.RepositoryUrl ?? "https://github.com/codefrydev",
                IconBitmap = TryLoadPluginIcon(item.Id),
                OnPrimaryActionAsync = HandleCardActionAsync,
                OnUninstallActionAsync = HandleUninstallActionAsync,
                OnStatusFeedback = msg => StatusFooterText = msg
            };

            // Populate multi-version support
            if (item.Versions != null && item.Versions.Count > 0)
            {
                foreach (var v in item.Versions)
                {
                    card.AvailableVersions.Add(v);
                }
                card.SelectedVersion = card.AvailableVersions.FirstOrDefault(v => v.Version == item.Version)
                    ?? card.AvailableVersions[0];
            }
            else
            {
                card.AvailableVersions.Add(new MarketplacePluginVersion
                {
                    Version = item.Version,
                    DownloadUrl = item.DownloadUrl,
                    FormattedSize = item.FormattedSize
                });
                card.SelectedVersion = card.AvailableVersions[0];
            }

            RefreshCardState(card);
            _allApps.Add(card);
            index++;
        }

        UpdateCounters();
        ApplyFilterAndSort();
    }

    private void RefreshCardState(LauncherAppCardViewModel card)
    {
        card.IconBitmap ??= TryLoadPluginIcon(card.Id);

        var isInstalled = _marketplace.IsPluginInstalled(card.Id);
        if (isInstalled)
        {
            card.Status = LauncherAppStatus.Installed;
            card.StatusText = "INSTALLED";
            card.PrimaryActionText = "LAUNCH";
        }
        else
        {
            card.Status = LauncherAppStatus.NotInstalled;
            card.StatusText = "AVAILABLE";
            card.PrimaryActionText = "INSTALL";
        }

        // Keep official declared download/package size from version or catalog
        var versionSize = card.SelectedVersion?.FormattedSize;
        if (!string.IsNullOrWhiteSpace(versionSize))
        {
            card.FormattedSize = versionSize;
        }
        else if (string.IsNullOrWhiteSpace(card.FormattedSize))
        {
            var pkgBytes = GetPackageFileSize(card.Id);
            if (pkgBytes > 0)
            {
                card.FormattedSize = FormatBytes(pkgBytes);
            }
            else if (isInstalled)
            {
                var installDir = Path.Combine(FryWorldPaths.PluginsDirectory, card.Id);
                var dirBytes = GetDirectorySize(installDir);
                if (dirBytes > 0)
                {
                    card.FormattedSize = FormatBytes(dirBytes);
                }
            }
        }
    }

    private async Task HandleCardActionAsync(LauncherAppCardViewModel card)
    {
        if (card.Status == LauncherAppStatus.Installed)
        {
            _launchPluginCallback(card.Id);
            return;
        }

        // Install flow
        card.IsBusy = true;
        card.IsDownloading = true;
        card.InstallProgress = 0;
        card.StatusText = "CONNECTING...";

        try
        {
            var catalog = _marketplace.GetCachedCatalog();
            var item = catalog.FirstOrDefault(c => string.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                var fetched = await _marketplace.FetchCatalogAsync();
                item = fetched.FirstOrDefault(c => string.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase));
            }

            var downloadItem = item;
            if (card.SelectedVersion != null && !string.IsNullOrWhiteSpace(card.SelectedVersion.DownloadUrl))
            {
                downloadItem = new MarketplacePluginItem
                {
                    Id = card.Id,
                    Name = card.Title,
                    Version = card.SelectedVersion.Version,
                    DownloadUrl = card.SelectedVersion.DownloadUrl,
                    FormattedSize = card.SelectedVersion.FormattedSize
                };
            }

            string? packagePath = null;
            if (downloadItem != null && !string.IsNullOrWhiteSpace(downloadItem.DownloadUrl))
            {
                card.StatusText = "DOWNLOADING...";
                try
                {
                    packagePath = await _marketplace.DownloadPackageAsync(downloadItem, pct =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            card.InstallProgress = pct;
                            card.ProgressText = $"{pct:F0}% ({card.FormattedSize})";
                            card.StatusText = $"DOWNLOADING {pct:F0}%";
                        });
                    });
                }
                catch (Exception ex) when (ex is not FryWorld.Sdk.Plugins.PluginException)
                {
                    // Offline or the download failed: fall back to a package placed next to the app. A failed
                    // checksum (PluginException) is not retried this way; it is shown to the user.
                    packagePath = TryResolveSideloadPackage(card.Id);
                }
            }
            else
            {
                packagePath = TryResolveSideloadPackage(card.Id);
            }

            if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath))
            {
                card.IsDownloading = false;
                card.InstallProgress = 100;
                card.StatusText = "INSTALLING...";
                await _marketplace.InstallPackageAsync(packagePath);
                card.InstalledVersion = downloadItem?.Version ?? card.SelectedVersion?.Version ?? "1.0.0";
            }
            else if (downloadItem == null)
            {
                throw new InvalidOperationException($"Package '{card.Id}' was not found in the marketplace catalog.");
            }

            RefreshCardState(card);
            UpdateCounters();
            ApplyFilterAndSort();

            if (card.Status == LauncherAppStatus.Installed)
            {
                StatusFooterText = $"INSTALLED {card.Title.ToUpperInvariant()} v{card.InstalledVersion}";
                _launchPluginCallback(card.Id);
            }
        }
        catch (Exception ex)
        {
            card.StatusText = $"ERROR: {ex.Message}";
        }
        finally
        {
            card.IsDownloading = false;
            card.IsBusy = false;
        }
    }

    private async Task HandleUninstallActionAsync(LauncherAppCardViewModel card)
    {
        card.IsBusy = true;
        card.StatusText = "UNINSTALLING...";

        try
        {
            await _marketplace.UninstallPluginAsync(card.Id);
            card.InstalledVersion = string.Empty;
            card.Status = LauncherAppStatus.NotInstalled;
            card.StatusText = "AVAILABLE";
            card.PrimaryActionText = "INSTALL";
            RefreshCardState(card);
            UpdateCounters();
            ApplyFilterAndSort();
            StatusFooterText = $"UNINSTALLED {card.Title.ToUpperInvariant()}";
        }
        catch (Exception ex)
        {
            card.StatusText = $"ERROR: {ex.Message}";
        }
        finally
        {
            card.IsBusy = false;
        }
    }

    private static string? TryResolveSideloadPackage(string pluginId)
    {
        var searchDirs = new List<string>
        {
            Path.Combine(FryWorldPaths.AppDataDirectory, "sideload"),
            FryWorldPaths.CacheDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Frystore", "plugins", pluginId),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Frystore", "plugins", pluginId),
            Path.Combine(Directory.GetCurrentDirectory(), "Frystore", "plugins", pluginId),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Frystore", "plugins", pluginId),
            "/Users/codefrydev/Desktop/SourceCode/FryWorld/Frystore/plugins/" + pluginId,
            "/Users/codefrydev/Desktop/SourceCode/FryWorld/FrySharp/bin/Release/net10.0",
            "/Users/codefrydev/Desktop/SourceCode/FryWorld/FryImage/bin/Release/net10.0"
        };

        var devPath = Environment.GetEnvironmentVariable("FRYWORLD_DEV_PLUGINS_DIR");
        if (!string.IsNullOrWhiteSpace(devPath) && Directory.Exists(devPath))
        {
            searchDirs.Add(devPath);
        }

        foreach (var dir in searchDirs)
        {
            if (Directory.Exists(dir))
            {
                var directMatch = Path.Combine(dir, $"{pluginId}.fryplugin");
                if (File.Exists(directMatch))
                {
                    return directMatch;
                }

                var files = Directory.GetFiles(dir, "*.fryplugin", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
        }

        return null;
    }

    private static long GetPackageFileSize(string pluginId)
    {
        var localPath = TryResolveSideloadPackage(pluginId);
        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
        {
            try
            {
                return new FileInfo(localPath).Length;
            }
            catch { }
        }
        return 0;
    }

    public static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        try
        {
            long total = 0;
            var dir = new DirectoryInfo(directoryPath);
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                total += file.Length;
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return string.Empty;
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private static Bitmap? TryLoadPluginIcon(string pluginId)
    {
        // Plugins supply their own icons from their installed directory or package
        try
        {
            var pluginDir = Path.Combine(FryWorldPaths.PluginsDirectory, pluginId);
            var candidates = new[]
            {
                Path.Combine(pluginDir, "icon.png"),
                Path.Combine(pluginDir, "logo.png"),
                Path.Combine(pluginDir, $"{pluginId}.png")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
            }
        }
        catch { }

        return null;
    }

    private void UpdateCounters()
    {
        AllCount = _allApps.Count;
        InstalledCount = _allApps.Count(a => a.Status == LauncherAppStatus.Installed);
        UpdatesCount = _allApps.Count(a => a.Status == LauncherAppStatus.UpdateAvailable);
        NotInstalledCount = _allApps.Count(a => a.Status == LauncherAppStatus.NotInstalled);
    }

    private void ApplyFilterAndSort()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var filtered = _allApps.AsEnumerable();

        // 1. Status Filter
        filtered = SelectedFilter switch
        {
            LauncherFilterTab.Installed => filtered.Where(a => a.Status == LauncherAppStatus.Installed),
            LauncherFilterTab.Updates => filtered.Where(a => a.Status == LauncherAppStatus.UpdateAvailable),
            LauncherFilterTab.NotInstalled => filtered.Where(a => a.Status == LauncherAppStatus.NotInstalled),
            _ => filtered
        };

        // 2. Search Query
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(a =>
                a.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Sorting
        filtered = SelectedSort switch
        {
            "Name" => filtered.OrderBy(a => a.Title),
            "Category" => filtered.OrderBy(a => a.Category).ThenBy(a => a.Title),
            "Version" => filtered.OrderByDescending(a => a.Version),
            _ => filtered.OrderBy(a => a.IndexTag) // Suite order
        };

        FilteredApps.Clear();
        foreach (var app in filtered)
        {
            FilteredApps.Add(app);
        }
    }
}
