using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;

namespace FryWorld.ViewModels;

public partial class CommandPaletteItemViewModel : ViewModelBase
{
    public CommandPaletteDescriptor Descriptor { get; }

    public string Title => Descriptor.Title;
    public string? Subtitle => Descriptor.Subtitle;
    public string? Category => Descriptor.Category ?? "Commands";
    public string? IconKind => Descriptor.IconKind ?? "Flash";
    public string? ShortcutKey => Descriptor.ShortcutKey;

    public CommandPaletteItemViewModel(CommandPaletteDescriptor descriptor)
    {
        Descriptor = descriptor;
    }
}

public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly IFryPluginContext _context;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private CommandPaletteItemViewModel? _selectedItem;

    public ObservableCollection<CommandPaletteItemViewModel> FilteredCommands { get; } = new();

    public event Action? RequestClose;

    public CommandPaletteViewModel(IFryPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        RefreshList();
    }

    partial void OnSearchQueryChanged(string value) => RefreshList();

    public void RefreshList()
    {
        var all = _context.GetRegisteredCommands();
        var query = SearchQuery?.Trim() ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             (c.Subtitle != null && c.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)));

        FilteredCommands.Clear();
        foreach (var cmd in matches)
        {
            FilteredCommands.Add(new CommandPaletteItemViewModel(cmd));
        }

        if (FilteredCommands.Count > 0 && SelectedItem == null)
        {
            SelectedItem = FilteredCommands[0];
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public async Task ExecuteItemAsync(CommandPaletteItemViewModel? item)
    {
        if (item != null)
        {
            SelectedItem = item;
        }
        await ExecuteSelectedCommandAsync();
    }

    [RelayCommand]
    public async Task ExecuteSelectedCommandAsync()
    {
        if (SelectedItem?.Descriptor != null)
        {
            var desc = SelectedItem.Descriptor;
            RequestClose?.Invoke();

            if (desc.AsyncAction != null)
            {
                await desc.AsyncAction();
            }
            else if (desc.Command?.CanExecute(null) == true)
            {
                desc.Command.Execute(null);
            }
        }
    }
}
