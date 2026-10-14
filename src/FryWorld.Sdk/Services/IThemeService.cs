using System;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Services;

public enum AppTheme
{
    Dark,
    Light,
    System
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    event Action<AppTheme>? ThemeChanged;
    void SetTheme(AppTheme theme);
    void ToggleTheme();
}

public interface INotificationService
{
    void ShowToast(string message, string title = "Information", string? iconKind = null);
    void ShowError(string message, string title = "Error");
}

public interface ISettingsManager
{
    T GetSetting<T>(string pluginId, string settingKey, T defaultValue);
    void SetSetting<T>(string pluginId, string settingKey, T value);
    Task SaveAsync();
}
