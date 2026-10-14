using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FryWorld.Sdk.Plugins;

namespace FryWorld.ViewModels;

public partial class PluginItemViewModel : ViewModelBase
{
    public IFryPlugin Plugin { get; }

    public string Id => Plugin.Id;
    public string Name => Plugin.Name;
    public string Version => Plugin.Version.ToString();

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private PluginLifecycleState _state = PluginLifecycleState.Active;

    [ObservableProperty]
    private string _category = "General";

    public PluginItemViewModel(IFryPlugin plugin)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }
}
