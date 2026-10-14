using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    private LauncherViewMode _viewMode = LauncherViewMode.Grid;

    [ObservableProperty]
    private LauncherFilterTab _selectedFilter = LauncherFilterTab.All;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSort = "Suite order";

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
    private string _statusFooterText = "CHECKED JUST NOW · USING FRYWORLD CATALOG API";

    public ObservableCollection<LauncherAppCardViewModel> FilteredApps { get; } = new();

    public IReadOnlyList<string> SortOptions { get; } = new[]
    {
        "Suite order",
        "Name",
        "Category",
        "Version"
    };

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
        var seedData = new[]
        {
            ("01", "#2563EB", "com.fryworld.csharpstudio", "Fry", "Sharp", "Full-featured C# studio & polyglot notebooks", "Developer", "CodeBraces", "2.0.5"),
            ("02", "#DC2626", "com.fryworld.imageeditor", "Fry", "Image", "Vector illustration & bitmap canvas layout studio", "Creative", "Palette", "1.2.0"),
            ("03", "#9333EA", "com.fryworld.frypdf", "Fry", "Pdf", "High-performance PDF deconstruction & document tools", "Productivity", "FilePdfBox", "1.1.0"),
            ("04", "#D97706", "com.fryworld.snake", "Fry", "Snake", "60+ FPS classic arcade retro gaming overlay", "Games", "GamepadVariant", "1.0.0"),
            ("05", "#059669", "com.fryworld.chess", "Fry", "Chess", "2-Player & Minimax AI grandmaster chess overlay", "Games", "ChessKnight", "1.0.2"),
            ("06", "#DB2777", "com.fryworld.tictactoe", "Fry", "TicTacToe", "Minimax AI neon tactical duel overlay", "Games", "Grid", "1.1.0"),
            ("07", "#65A30D", "com.fryworld.musicplayer", "Fry", "Music", "Hi-Fi background playlist audio visualizer", "Media", "Music", "1.0.1"),
            ("08", "#0891B2", "com.fryworld.telemetry", "Fry", "Telemetry", "Real-time GC memory & CPU resource HUD", "Utility", "Speedometer", "1.0.0"),
            ("09", "#EA580C", "com.fryworld.scratchpad", "Fry", "Notes", "Floating Markdown workspace scratchpad", "Productivity", "NotebookEdit", "1.0.0"),
            ("10", "#10B981", "com.fryworld.tabuladata", "Fry", "Grid", "Spreadsheet, CSV, and tabular data engine", "Data", "TableLarge", "1.0.0"),
            ("11", "#0D9488", "com.fryworld.cadstudio", "Fry", "CAD", "Computer-aided vector drafting & blueprints", "Design", "RulerSquare", "0.9.0"),
            ("12", "#4F46E5", "com.fryworld.soundstudio", "Fry", "Audio", "Waveform editor & multi-track sound engineering", "Audio", "Waveform", "0.9.2")
        };

        _allApps.Clear();
        foreach (var (index, color, id, prefix, suffix, desc, cat, icon, ver) in seedData)
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
                Version = $"Version {ver} · Oct 14, 2026",
                OnPrimaryActionAsync = HandleCardActionAsync
            };

            RefreshCardState(card);
            _allApps.Add(card);
        }

        UpdateCounters();
    }

    private void UpdateAppsFromCatalog(IReadOnlyList<MarketplacePluginItem> catalog)
    {
        int index = 1;
        var palette = new[] { "#2563EB", "#DC2626", "#9333EA", "#D97706", "#059669", "#DB2777", "#65A30D", "#0891B2", "#EA580C", "#10B981", "#0D9488", "#4F46E5" };

        _allApps.Clear();
        foreach (var item in catalog)
        {
            var color = palette[(index - 1) % palette.Length];
            var parts = item.Name.Split(' ', 2);
            var prefix = parts[0];
            var suffix = parts.Length > 1 ? parts[1] : string.Empty;

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
                Version = $"Version {item.Version} · Oct 14, 2026",
                WebsiteUrl = item.WebsiteUrl ?? "https://codefrydev.in",
                RepositoryUrl = item.RepositoryUrl ?? "https://github.com/codefrydev",
                OnPrimaryActionAsync = HandleCardActionAsync
            };

            RefreshCardState(card);
            _allApps.Add(card);
            index++;
        }

        UpdateCounters();
        ApplyFilterAndSort();
    }

    private void RefreshCardState(LauncherAppCardViewModel card)
    {
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
            card.StatusText = "AVAILABLE TO INSTALL";
            card.PrimaryActionText = "INSTALL";
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
        card.StatusText = "INSTALLING...";

        try
        {
            await Task.Delay(400); // UI feedback
            RefreshCardState(card);
            UpdateCounters();
            ApplyFilterAndSort();
        }
        finally
        {
            card.IsBusy = false;
        }
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
                a.Category.Contains(query, StringComparison.OrdinalIgnoreCase));
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
