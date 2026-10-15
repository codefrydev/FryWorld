using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;

namespace FryWorld.ViewModels;

public enum PluginsManagerTab
{
    Installed,
    Marketplace,
    Updates
}

public partial class PluginsManagerViewModel : ViewModelBase
{
    private readonly PluginHost _host;
    private readonly IPluginMarketplaceService _marketplace;

    [ObservableProperty]
    private PluginsManagerTab _selectedTab = PluginsManagerTab.Marketplace;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<PluginItemViewModel> InstalledPlugins { get; } = new();

    /// <summary>Warnings and errors from loading and mounting plugins, newest first, so users can see why a plugin is missing.</summary>
    public ObservableCollection<PluginProblemViewModel> Problems { get; } = new();

    public bool HasProblems => Problems.Count > 0;
    public ObservableCollection<MarketplacePluginItem> MarketplacePlugins { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public PluginsManagerViewModel(PluginHost host, IPluginMarketplaceService marketplace)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _marketplace = marketplace ?? throw new ArgumentNullException(nameof(marketplace));

        Categories.Add("All");
        Categories.Add("Developer");
        Categories.Add("Creative");
        Categories.Add("Games");
        Categories.Add("Media");
        Categories.Add("Productivity");
        Categories.Add("Utility");

        RefreshInstalledList();

        // Plugins load in the background after startup; keep the list in step as they arrive and change state.
        _host.PluginStateChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshInstalledList);

        Problems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProblems));
        foreach (var diagnostic in _host.Diagnostics) AddProblem(diagnostic);
        _host.DiagnosticReported += diagnostic => Avalonia.Threading.Dispatcher.UIThread.Post(() => AddProblem(diagnostic));
    }

    private void AddProblem(PluginLoadDiagnostic diagnostic)
    {
        if (diagnostic.Severity == PluginDiagnosticSeverity.Info) return;
        Problems.Insert(0, new PluginProblemViewModel(diagnostic));
    }

    [RelayCommand]
    public void ClearProblems() => Problems.Clear();

    [RelayCommand]
    public void RefreshInstalledList()
    {
        InstalledPlugins.Clear();
        foreach (var plugin in _host.GetRegisteredPlugins())
        {
            var vm = new PluginItemViewModel(plugin)
            {
                State = _host.GetPluginState(plugin.Id) ?? PluginLifecycleState.Registered
            };
            InstalledPlugins.Add(vm);
        }
    }

    [RelayCommand]
    public async Task RefreshMarketplaceAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading marketplace catalog...";

        try
        {
            var items = await _marketplace.FetchCatalogAsync(forceRefresh: true);
            MarketplacePlugins.Clear();
            foreach (var item in items)
            {
                MarketplacePlugins.Add(item);
            }
            StatusMessage = $"{MarketplacePlugins.Count} plugins available in catalog.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Marketplace load error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task TogglePluginAsync(PluginItemViewModel item)
    {
        if (item.IsEnabled)
        {
            await _host.MountPluginAsync(item.Plugin);
            item.State = PluginLifecycleState.Active;
        }
        else
        {
            await _host.UnmountPluginAsync(item.Id);
            item.State = PluginLifecycleState.Unloaded;
        }
    }
}

/// <summary>One row in the Plugins Manager's Problems list.</summary>
public sealed class PluginProblemViewModel
{
    public PluginProblemViewModel(PluginLoadDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }

    public PluginLoadDiagnostic Diagnostic { get; }

    public bool IsError => Diagnostic.Severity == PluginDiagnosticSeverity.Error;

    public string IconKind => IsError ? "AlertCircleOutline" : "AlertOutline";

    public string Title => Diagnostic.PluginId ?? System.IO.Path.GetFileName(Diagnostic.Path ?? string.Empty) switch
    {
        "" => "Plugins",
        var name => name
    };

    public string Message => Diagnostic.Message;

    /// <summary>Stage, time and the underlying error, for the tooltip.</summary>
    public string Details =>
        $"{Diagnostic.Stage} · {Diagnostic.Timestamp.ToLocalTime():HH:mm:ss}" +
        (Diagnostic.Path != null ? $"\n{Diagnostic.Path}" : string.Empty) +
        (Diagnostic.Exception != null ? $"\n{Diagnostic.Exception.GetType().Name}: {Diagnostic.Exception.Message}" : string.Empty);
}
