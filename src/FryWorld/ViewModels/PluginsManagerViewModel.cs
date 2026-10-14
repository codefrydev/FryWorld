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
    }

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
