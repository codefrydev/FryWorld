using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FryWorld.ViewModels;

namespace FryWorld.Views;

public partial class CommandPaletteDialog : UserControl
{
    public CommandPaletteDialog()
    {
        InitializeComponent();

        AttachedToVisualTree += (s, e) => FocusSearchBox();
        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
    }

    private void FocusSearchBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is CommandPaletteViewModel vm)
            {
                vm.CloseCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clicking outside the card on the backdrop closes the dialog
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Prevent backdrop click when clicking inside the card
        e.Handled = true;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            vm.ExecuteSelectedCommandCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (CommandsListBox.ItemCount > 0)
            {
                var idx = CommandsListBox.SelectedIndex;
                if (idx < CommandsListBox.ItemCount - 1)
                {
                    CommandsListBox.SelectedIndex = idx + 1;
                }
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Up)
        {
            if (CommandsListBox.ItemCount > 0)
            {
                var idx = CommandsListBox.SelectedIndex;
                if (idx > 0)
                {
                    CommandsListBox.SelectedIndex = idx - 1;
                }
                e.Handled = true;
            }
        }
    }

    private void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.ExecuteSelectedCommandCommand.Execute(null);
        }
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm && e.Key == Key.Enter)
        {
            vm.ExecuteSelectedCommandCommand.Execute(null);
            e.Handled = true;
        }
    }
}

