using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using FryWorld.Sdk.Services;
using FryWorld.Sdk.Theming;

namespace FryWorld.Services;

/// <summary>
/// Core dynamic theme and color management service for the FryWorld host application.
/// Manages theme catalogs, procedural harmonic generators, single-pass Avalonia resource layer
/// swaps, and settings persistence.
/// </summary>
public sealed class ThemeService : IThemeManager
{
    private const string SettingPluginId = "core";
    private const string SettingThemeKey = "theme.activeId";

    private readonly ConcurrentDictionary<string, ThemeDefinition> _themes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IThemeTokenContributor> _contributors = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _layerGate = new();
    private readonly ThemeLayerProvider _layerProvider = new();
    private readonly List<IResourceDictionary> _roots = new();
    private readonly Dictionary<IResourceDictionary, ThemeLayerProvider> _installed = new();
    private IReadOnlyDictionary<object, object?>? _lastCommittedValues;

    private string _activeThemeId = BuiltInThemes.DarkPlus.Id;
    private AppTheme _currentTheme = AppTheme.Dark;
    private ISettingsManager? _settingsManager;
    private bool _isLayerInstalled;

    /// <inheritdoc />
    public AppTheme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public string ActiveThemeId => _activeThemeId;

    /// <inheritdoc />
    public ThemeDefinition? ActiveTheme => _themes.TryGetValue(_activeThemeId, out var theme) ? theme : null;

    /// <inheritdoc />
    public event Action<AppTheme>? ThemeChanged;

    /// <inheritdoc />
    public event Action<string>? ThemeDefinitionChanged;

    public ThemeService(ISettingsManager? settingsManager = null)
    {
        _settingsManager = settingsManager;

        // Register default domain token contributors
        _contributors["csharpstudio"] = new FryWorld.Sdk.Theming.Contributors.DeveloperStudioTokenContributor();
        _contributors["imageeditor"] = new FryWorld.Sdk.Theming.Contributors.PhotoStudioTokenContributor();
        _contributors["circuitstudio"] = new FryWorld.Sdk.Theming.Contributors.CircuitStudioTokenContributor();

        foreach (var theme in BuiltInThemes.All)
        {
            _themes[theme.Id] = theme;
        }

        EnsureLayerInstalled();
    }

    /// <summary>Attaches settings manager for persistence if resolved after construction.</summary>
    public void SetSettingsManager(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        var savedTheme = _settingsManager.GetSetting<string?>(SettingPluginId, SettingThemeKey, null);
        if (!string.IsNullOrWhiteSpace(savedTheme) && _themes.ContainsKey(savedTheme))
        {
            ApplyTheme(savedTheme);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ThemeDefinition> GetAvailableThemes() => _themes.Values.ToList();

    /// <inheritdoc />
    public void RegisterTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (string.IsNullOrWhiteSpace(theme.Id)) throw new ArgumentException("Theme ID cannot be empty.", nameof(theme));
        HarmonicColorGenerator.EnsureCompleteTheme(theme);
        _themes[theme.Id] = theme;
    }

    /// <inheritdoc />
    public bool ApplyTheme(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);

        if (!_themes.TryGetValue(themeId, out var def))
        {
            return false;
        }

        _activeThemeId = themeId;
        _currentTheme = def.IsDark ? AppTheme.Dark : AppTheme.Light;
        _overrides.Clear();

        CommitTheme(def);

        _settingsManager?.SetSetting(SettingPluginId, SettingThemeKey, themeId);
        ThemeChanged?.Invoke(_currentTheme);
        ThemeDefinitionChanged?.Invoke(_activeThemeId);
        return true;
    }

    /// <inheritdoc />
    public void ApplyHarmonicTheme(float hueDegrees, ColorHarmonyMode mode, bool isDark = true)
    {
        var theme = HarmonicColorGenerator.GenerateHarmonicTheme(hueDegrees, mode, isDark, _contributors.Values);
        RegisterTheme(theme);
        ApplyTheme(theme.Id);
    }

    /// <inheritdoc />
    public void ApplyRandomHarmonicTheme(bool isDark = true)
    {
        var theme = HarmonicColorGenerator.GenerateRandomHarmonicTheme(isDark, _contributors.Values);
        RegisterTheme(theme);
        ApplyTheme(theme.Id);
    }

    /// <inheritdoc />
    public void SetTheme(AppTheme theme)
    {
        if (theme == AppTheme.Light)
        {
            ApplyTheme(BuiltInThemes.LightPlus.Id);
        }
        else
        {
            ApplyTheme(BuiltInThemes.DarkPlus.Id);
        }
    }

    /// <inheritdoc />
    public void ToggleTheme()
    {
        if (_currentTheme == AppTheme.Dark)
        {
            ApplyTheme(BuiltInThemes.LightPlus.Id);
        }
        else
        {
            ApplyTheme(BuiltInThemes.DarkPlus.Id);
        }
    }

    /// <inheritdoc />
    public void SetColorOverride(string tokenName, string hexOrRgb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexOrRgb);

        if (!Color.TryParse(hexOrRgb, out var color))
        {
            throw new ArgumentException($"Invalid color specification: '{hexOrRgb}'.", nameof(hexOrRgb));
        }

        _overrides[tokenName] = hexOrRgb;

        if (ActiveTheme != null)
        {
            CommitTheme(ActiveTheme);
            ThemeDefinitionChanged?.Invoke(_activeThemeId);
        }
    }

    /// <inheritdoc />
    public string? GetColor(string tokenName)
    {
        if (_overrides.TryGetValue(tokenName, out var ov)) return ov;
        if (ActiveTheme?.Colors.TryGetValue(tokenName, out var col) == true) return col;
        return null;
    }

    /// <inheritdoc />
    public void ResetToDefaults()
    {
        _overrides.Clear();
        ApplyTheme(_currentTheme == AppTheme.Light ? BuiltInThemes.LightPlus.Id : BuiltInThemes.DarkPlus.Id);
    }

    /// <inheritdoc />
    public IDisposable RegisterTokenContributor(IThemeTokenContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        _contributors[contributor.Id] = contributor;

        if (ActiveTheme != null)
        {
            CommitTheme(ActiveTheme);
            ThemeDefinitionChanged?.Invoke(_activeThemeId);
        }

        return new ActionDisposable(() => UnregisterTokenContributor(contributor.Id));
    }

    /// <inheritdoc />
    public bool UnregisterTokenContributor(string contributorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);
        var removed = _contributors.TryRemove(contributorId, out _);
        if (removed && ActiveTheme != null)
        {
            CommitTheme(ActiveTheme);
            ThemeDefinitionChanged?.Invoke(_activeThemeId);
        }
        return removed;
    }

    /// <inheritdoc />
    public IReadOnlyList<IThemeTokenContributor> GetTokenContributors() =>
        _contributors.Values.ToList();

    private void EnsureLayerInstalled()
    {
        if (_isLayerInstalled) return;

        void Install()
        {
            if (Application.Current?.Resources is { } resources)
            {
                if (!resources.MergedDictionaries.Contains(_layerProvider))
                {
                    resources.MergedDictionaries.Add(_layerProvider);
                }
                _isLayerInstalled = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Install();
        }
        else
        {
            Dispatcher.UIThread.Post(Install);
        }
    }

    private void CommitTheme(ThemeDefinition def)
    {
        var values = new Dictionary<object, object?>();

        lock (_layerGate)
        {
            var combinedColors = new Dictionary<string, string>(def.Colors, StringComparer.OrdinalIgnoreCase);

            // Let registered token contributors enrich domain tokens
            foreach (var contributor in _contributors.Values)
            {
                contributor.ContributeTokens(def, combinedColors);
            }

            // 1. Process theme colors
            foreach (var (key, hex) in combinedColors)
            {
                if (Color.TryParse(hex, out var color))
                {
                    AddToken(values, key, color);
                }
            }

            // 2. Apply any custom overrides
            foreach (var (key, hex) in _overrides)
            {
                if (Color.TryParse(hex, out var color))
                {
                    AddToken(values, key, color);
                }
            }
        }

        _lastCommittedValues = values;

        void ApplyToApp()
        {
            EnsureLayerInstalled();

            if (Application.Current is { } app)
            {
                var targetVariant = def.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
                if (app.RequestedThemeVariant != targetVariant)
                {
                    app.RequestedThemeVariant = targetVariant;
                }
            }

            _layerProvider.Replace(values);

            foreach (var root in _roots.ToList())
            {
                ApplyToRoot(root, values);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyToApp();
        }
        else
        {
            Dispatcher.UIThread.Post(ApplyToApp);
        }
    }

    /// <inheritdoc />
    public void TrackResourceRoot(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is Window window)
        {
            AttachResourceRoot(window.Resources);
            window.Closed += (_, _) => DetachResourceRoot(window.Resources);
            return;
        }

        element.AttachedToLogicalTree += (_, _) => AttachResourceRoot(element.Resources);
        element.DetachedFromLogicalTree += (_, _) => DetachResourceRoot(element.Resources);
        if (((Avalonia.LogicalTree.ILogical)element).IsAttachedToLogicalTree) AttachResourceRoot(element.Resources);
    }

    /// <summary>Starts showing the theme layer in the specified resource root.</summary>
    public void AttachResourceRoot(IResourceDictionary root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (_roots.Contains(root)) return;
        _roots.Add(root);

        if (_lastCommittedValues != null)
        {
            ApplyToRoot(root, _lastCommittedValues);
        }
    }

    /// <summary>Stops showing the theme layer in the specified resource root.</summary>
    public void DetachResourceRoot(IResourceDictionary root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _roots.Remove(root);
        if (_installed.Remove(root, out var layer))
        {
            root.MergedDictionaries.Remove(layer);
        }
    }

    private void ApplyToRoot(IResourceDictionary root, IReadOnlyDictionary<object, object?> values)
    {
        if (_installed.TryGetValue(root, out var layer) && root.MergedDictionaries.Contains(layer))
        {
            layer.Replace(values);
        }
        else
        {
            layer = new ThemeLayerProvider();
            layer.Replace(values);
            root.MergedDictionaries.Add(layer);
            _installed[root] = layer;
        }
    }

    private static void AddToken(Dictionary<object, object?> dict, string key, Color color)
    {
        var brush = new ImmutableSolidColorBrush(color);

        if (key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase))
        {
            dict[key] = brush;
            var colorKey = key[..^5] + "Color";
            dict[colorKey] = color;
        }
        else if (key.EndsWith("Color", StringComparison.OrdinalIgnoreCase))
        {
            dict[key] = color;
            var brushKey = key[..^5] + "Brush";
            dict[brushKey] = brush;
        }
        else
        {
            dict[key] = brush;
            dict[key + "Brush"] = brush;
            dict[key + "Color"] = color;
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
