using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    [NotifyPropertyChangedFor(nameof(IconBackgroundBrush))]
    private string _indexTag = "01";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconBackgroundBrush))]
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
    private Bitmap? _iconBitmap;

    public IBrush IconBackgroundBrush
    {
        get
        {
            if (Color.TryParse(IndexColorHex, out var c))
            {
                // Vibrant glass tint of brand color
                return new SolidColorBrush(Color.FromArgb(36, c.R, c.G, c.B));
            }
            return new SolidColorBrush(Color.FromArgb(36, 37, 99, 235));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstalled))]
    [NotifyPropertyChangedFor(nameof(StatusIconKind))]
    [NotifyPropertyChangedFor(nameof(StatusBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(StatusForegroundBrush))]
    [NotifyPropertyChangedFor(nameof(PrimaryActionIconKind))]
    private LauncherAppStatus _status = LauncherAppStatus.NotInstalled;

    public bool IsInstalled => Status == LauncherAppStatus.Installed;

    public string StatusIconKind => Status switch
    {
        LauncherAppStatus.Installed => "CheckCircle",
        LauncherAppStatus.UpdateAvailable => "Update",
        _ => "ArrowDownCircle"
    };

    public IBrush StatusBackgroundBrush => Status switch
    {
        LauncherAppStatus.Installed => new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),       // Emerald tint
        LauncherAppStatus.UpdateAvailable => new SolidColorBrush(Color.FromArgb(32, 245, 158, 11)), // Amber tint
        _ => new SolidColorBrush(Color.FromArgb(28, 99, 102, 241))                                // Indigo tint
    };

    public IBrush StatusForegroundBrush => Status switch
    {
        LauncherAppStatus.Installed => new SolidColorBrush(Color.FromRgb(16, 185, 129)),       // Emerald
        LauncherAppStatus.UpdateAvailable => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
        _ => new SolidColorBrush(Color.FromRgb(129, 140, 248))                                // Indigo/Primary
    };

    public string PrimaryActionIconKind => Status switch
    {
        LauncherAppStatus.Installed => "RocketLaunch",
        LauncherAppStatus.UpdateAvailable => "Update",
        _ => "Download"
    };

    [ObservableProperty]
    private string _statusText = "AVAILABLE";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionShortText))]
    private string _version = "Version 1.0.0 · Oct 15, 2026";

    public string VersionShortText => SelectedVersion != null && !string.IsNullOrWhiteSpace(SelectedVersion.Version)
        ? $"v{SelectedVersion.Version.TrimStart('v')}"
        : Version.Contains('·') ? Version.Split('·')[0].Trim() : Version;

    [ObservableProperty]
    private string _installedVersion = string.Empty;

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private string _websiteUrl = "https://codefrydev.in";

    [ObservableProperty]
    private string _repositoryUrl = "https://github.com/codefrydev";

    [ObservableProperty]
    private string _primaryActionText = "INSTALL";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public ObservableCollection<MarketplacePluginVersion> AvailableVersions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleVersions))]
    private MarketplacePluginVersion? _selectedVersion;

    public bool HasMultipleVersions => AvailableVersions.Count > 1;

    public Func<LauncherAppCardViewModel, Task>? OnPrimaryActionAsync { get; set; }
    public Func<LauncherAppCardViewModel, Task>? OnUninstallActionAsync { get; set; }
    public Action<LauncherAppCardViewModel>? OnOptionsAction { get; set; }
    public Action<string>? OnStatusFeedback { get; set; }

    partial void OnSelectedVersionChanged(MarketplacePluginVersion? value)
    {
        if (value == null) return;
        FormattedSize = value.FormattedSize;

        if (IsInstalled)
        {
            if (string.Equals(value.Version, InstalledVersion, StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "INSTALLED";
                PrimaryActionText = "LAUNCH";
            }
            else
            {
                StatusText = $"v{value.Version} AVAILABLE";
                PrimaryActionText = $"SWITCH TO v{value.Version}";
            }
        }
        else
        {
            StatusText = $"AVAILABLE v{value.Version}";
            PrimaryActionText = "INSTALL";
        }
    }

    [RelayCommand]
    private async Task ExecutePrimaryActionAsync()
    {
        if (OnPrimaryActionAsync != null)
        {
            await OnPrimaryActionAsync(this);
        }
    }

    [RelayCommand]
    private async Task ExecuteUninstallAsync()
    {
        if (OnUninstallActionAsync != null)
        {
            await OnUninstallActionAsync(this);
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

    [RelayCommand]
    private async Task CopyIdAsync()
    {
        if (string.IsNullOrWhiteSpace(Id)) return;
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(Id);
                OnStatusFeedback?.Invoke($"Copied ID '{Id}' to clipboard");
            }
        }
        catch { }
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
