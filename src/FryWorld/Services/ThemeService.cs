using System;
using Avalonia;
using Avalonia.Styling;
using FryWorld.Sdk.Services;

namespace FryWorld.Services;

public sealed class ThemeService : IThemeService
{
    private AppTheme _currentTheme = AppTheme.Dark;

    public AppTheme CurrentTheme => _currentTheme;
    public event Action<AppTheme>? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        _currentTheme = theme;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }

        ThemeChanged?.Invoke(theme);
    }

    public void ToggleTheme()
    {
        SetTheme(_currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }
}
