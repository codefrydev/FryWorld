using System;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Services;

/// <summary>Colour theme of the host.</summary>
public enum AppTheme
{
    /// <summary>Dark theme.</summary>
    Dark,
    /// <summary>Light theme.</summary>
    Light,
    /// <summary>Follow the operating system.</summary>
    System
}

/// <summary>Reads and changes the host's colour theme. Hosts and the standalone runner provide it.</summary>
public interface IThemeService
{
    /// <summary>The theme the user chose.</summary>
    AppTheme CurrentTheme { get; }
    /// <summary>Raised after the theme changes.</summary>
    event Action<AppTheme>? ThemeChanged;
    /// <summary>Switches to <paramref name="theme"/>.</summary>
    void SetTheme(AppTheme theme);
    /// <summary>Switches between dark and light.</summary>
    void ToggleTheme();
}

/// <summary>Shows short, non-blocking messages to the user.</summary>
public interface INotificationService
{
    /// <summary>Shows an informational message.</summary>
    void ShowToast(string message, string title = "Information", string? iconKind = null);
    /// <summary>Shows an error message.</summary>
    void ShowError(string message, string title = "Error");
}

/// <summary>Stores plugin settings for the host. Plugins usually use <c>ctx.GetPluginSettings()</c> instead, which is bound to the plugin.</summary>
public interface ISettingsManager
{
    /// <summary>The stored value of <paramref name="settingKey"/> for <paramref name="pluginId"/>, or <paramref name="defaultValue"/>.</summary>
    T GetSetting<T>(string pluginId, string settingKey, T defaultValue);
    /// <summary>Stores <paramref name="value"/>; implementations persist it, possibly after a short delay.</summary>
    void SetSetting<T>(string pluginId, string settingKey, T value);
    /// <summary>Persists every pending change now.</summary>
    Task SaveAsync();

    /// <summary>
    /// Raised after <see cref="SetSetting{T}"/> changes a value, with the plugin id and setting key, so a
    /// plugin can react when the user edits its settings in the host. Stores written before this event
    /// existed do not raise it.
    /// </summary>
    event Action<string, string>? SettingChanged
    {
        add { }
        remove { }
    }
}
