using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Marketplace;

namespace FryWorld.ViewModels;

public enum LauncherAppStatus
{
    NotInstalled,
    Installed,
    UpdateAvailable
}

public partial class LauncherAppCardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _indexTag = "01";

    [ObservableProperty]
    private string _indexColorHex = "#2563EB";

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _titlePrefix = string.Empty;

    [ObservableProperty]
    private string _titleSuffix = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = "Tools";

    [ObservableProperty]
    private string _iconKind = "Application";

    [ObservableProperty]
    private LauncherAppStatus _status = LauncherAppStatus.NotInstalled;

    [ObservableProperty]
    private string _statusText = "AVAILABLE TO INSTALL";

    [ObservableProperty]
    private string _version = "Version 1.0.0 · Oct 14, 2026";

    [ObservableProperty]
    private string _websiteUrl = "https://codefrydev.in";

    [ObservableProperty]
    private string _repositoryUrl = "https://github.com/codefrydev";

    [ObservableProperty]
    private string _primaryActionText = "INSTALL";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _installProgress;

    public Func<LauncherAppCardViewModel, Task>? OnPrimaryActionAsync { get; set; }
    public Action<LauncherAppCardViewModel>? OnOptionsAction { get; set; }

    [RelayCommand]
    private async Task ExecutePrimaryActionAsync()
    {
        if (OnPrimaryActionAsync != null)
        {
            await OnPrimaryActionAsync(this);
        }
    }

    [RelayCommand]
    private void ExecuteOptions()
    {
        OnOptionsAction?.Invoke(this);
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        OpenUrl(WebsiteUrl);
    }

    [RelayCommand]
    private void OpenRepository()
    {
        OpenUrl(RepositoryUrl);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
