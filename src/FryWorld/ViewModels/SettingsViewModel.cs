using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Theming;
using FryWorld.Sdk.Theming.ColorMath;

namespace FryWorld.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IThemeManager _themeManager;
    private readonly PluginHost _host;

    [ObservableProperty]
    private string _activeThemeId;

    [ObservableProperty]
    private float _baseHue = 210f;

    [ObservableProperty]
    private ColorHarmonyMode _selectedHarmonyMode = ColorHarmonyMode.Analogous;

    [ObservableProperty]
    private bool _isDark = true;

    public IReadOnlyList<ThemeDefinition> AvailableThemes => _themeManager.GetAvailableThemes();

    public IReadOnlyList<ColorHarmonyMode> HarmonyModes { get; } = Enum.GetValues<ColorHarmonyMode>();

    public ThemeDefinition? ActiveTheme => _themeManager.ActiveTheme;

    // Palette Preview Properties
    public string PrimaryPreview => GetTokenHex("M3PrimaryBrush", "#38BDF8");
    public string SecondaryPreview => GetTokenHex("M3SecondaryBrush", "#A855F7");
    public string TertiaryPreview => GetTokenHex("M3TertiaryBrush", "#EC4899");
    public string SurfacePreview => GetTokenHex("M3SurfaceBrush", "#0D1117");
    public string SurfaceLowPreview => GetTokenHex("M3SurfaceContainerLowBrush", "#161D2A");
    public string SurfaceHighPreview => GetTokenHex("M3SurfaceContainerHighBrush", "#1C2536");
    public string OutlinePreview => GetTokenHex("M3OutlineBrush", "#30363D");
    public string OnSurfacePreview => GetTokenHex("M3OnSurfaceBrush", "#F8FAFC");
    public string ErrorPreview => GetTokenHex("M3ErrorBrush", "#EF4444");
    public string WarningPreview => GetTokenHex("M3WarningBrush", "#F59E0B");
    public string SuccessPreview => GetTokenHex("TonalSuccess600", "#10B981");
    public string InfoPreview => GetTokenHex("TonalInfo600", "#06B6D4");

    public string BaseHueColorHex => ColorRgb.FromHex(HarmonicColorGenerator.HslToHex(BaseHue, 0.85f, 0.55f)).ToHex();

    // Diagnostics
    public int LoadedPluginCount => _host.GetRegisteredPlugins().Count;
    public string HeapMemoryText => $"{GC.GetTotalMemory(false) / 1024 / 1024} MB";
    public string HostVersionText => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public string PluginsPath => FryWorldPaths.PluginsDirectory;
    public string SettingsPath => FryWorldPaths.SettingsFilePath;

    public SettingsViewModel(IThemeManager themeManager, PluginHost host)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        _activeThemeId = _themeManager.ActiveThemeId;
        _isDark = _themeManager.CurrentTheme == Sdk.Services.AppTheme.Dark;

        _themeManager.ThemeDefinitionChanged += OnThemeChanged;
        _themeManager.ThemeChanged += _ => RefreshPreview();
    }

    private void OnThemeChanged(string newThemeId)
    {
        ActiveThemeId = newThemeId;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        OnPropertyChanged(nameof(ActiveTheme));
        OnPropertyChanged(nameof(AvailableThemes));
        OnPropertyChanged(nameof(PrimaryPreview));
        OnPropertyChanged(nameof(SecondaryPreview));
        OnPropertyChanged(nameof(TertiaryPreview));
        OnPropertyChanged(nameof(SurfacePreview));
        OnPropertyChanged(nameof(SurfaceLowPreview));
        OnPropertyChanged(nameof(SurfaceHighPreview));
        OnPropertyChanged(nameof(OutlinePreview));
        OnPropertyChanged(nameof(OnSurfacePreview));
        OnPropertyChanged(nameof(ErrorPreview));
        OnPropertyChanged(nameof(WarningPreview));
        OnPropertyChanged(nameof(SuccessPreview));
        OnPropertyChanged(nameof(InfoPreview));
        OnPropertyChanged(nameof(HeapMemoryText));
        OnPropertyChanged(nameof(LoadedPluginCount));
    }

    partial void OnBaseHueChanged(float value)
    {
        OnPropertyChanged(nameof(BaseHueColorHex));
    }

    [RelayCommand]
    public void ApplyTheme(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;
        _themeManager.ApplyTheme(themeId);
        ActiveThemeId = themeId;
        RefreshPreview();
    }

    [RelayCommand]
    public void ApplyRandomHarmonic()
    {
        _themeManager.ApplyRandomHarmonicTheme(IsDark);
        ActiveThemeId = _themeManager.ActiveThemeId;
        RefreshPreview();
    }

    [RelayCommand]
    public void ApplyCustomHarmonic()
    {
        _themeManager.ApplyHarmonicTheme(BaseHue, SelectedHarmonyMode, IsDark);
        ActiveThemeId = _themeManager.ActiveThemeId;
        RefreshPreview();
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        _themeManager.ResetToDefaults();
        RefreshPreview();
    }

    [RelayCommand]
    public void ResetDefaults() => ResetToDefaults();

    private string GetTokenHex(string tokenName, string fallback)
    {
        var col = _themeManager.GetColor(tokenName);
        if (!string.IsNullOrWhiteSpace(col)) return col;
        if (ActiveTheme?.Colors.TryGetValue(tokenName, out var themeCol) == true) return themeCol;
        return fallback;
    }
}
