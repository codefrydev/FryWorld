using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using FryWorld.Sdk.Models;

namespace FryWorld.Sdk.Services;

/// <summary>
/// One plugin's settings. Get it inside <c>ApplyAsync</c> with <c>ctx.GetPluginSettings()</c>; it is bound to
/// the plugin, so keys are the plugin's own setting names with no plugin id to pass around.
/// </summary>
public interface IPluginSettings
{
    /// <summary>The plugin these settings belong to.</summary>
    string PluginId { get; }

    /// <summary>
    /// The stored value of <paramref name="key"/>, or else the default declared for it in the plugin's
    /// <c>SettingsSchema</c>, or else <c>default(T)</c>.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>The stored value of <paramref name="key"/>, or <paramref name="defaultValue"/> when nothing is stored.</summary>
    T Get<T>(string key, T defaultValue);

    /// <summary>Stores <paramref name="value"/> for <paramref name="key"/>; persisted by the host's settings store.</summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Raised with the key after one of this plugin's settings changes, whether the plugin or the user
    /// (in the host's settings UI) changed it. Handlers are removed automatically when the plugin unmounts.
    /// </summary>
    event Action<string>? Changed;
}

/// <summary>
/// <see cref="IPluginSettings"/> over any <see cref="ISettingsManager"/>.
/// </summary>
internal sealed class PluginSettings : IPluginSettings, IDisposable
{
    private readonly ISettingsManager _store;
    private readonly IReadOnlyDictionary<string, PluginSettingDefinition>? _schema;

    public PluginSettings(ISettingsManager store, string pluginId, IReadOnlyDictionary<string, PluginSettingDefinition>? schema)
    {
        _store = store;
        PluginId = pluginId;
        _schema = schema;
        _store.SettingChanged += OnStoreSettingChanged;
    }

    public string PluginId { get; }

    public event Action<string>? Changed;

    public T? Get<T>(string key) => _store.GetSetting(PluginId, key, SchemaDefault<T>(key));

    public T Get<T>(string key, T defaultValue) => _store.GetSetting(PluginId, key, defaultValue);

    public void Set<T>(string key, T value) => _store.SetSetting(PluginId, key, value);

    private void OnStoreSettingChanged(string pluginId, string key)
    {
        if (string.Equals(pluginId, PluginId, StringComparison.OrdinalIgnoreCase))
        {
            Changed?.Invoke(key);
        }
    }

    /// <summary>
    /// The schema's default as <typeparamref name="T"/>. Defaults declared in code are CLR values; defaults
    /// read from plugin.json arrive as <see cref="JsonElement"/>.
    /// </summary>
    private T? SchemaDefault<T>(string key)
    {
        if (_schema == null || !_schema.TryGetValue(key, out var definition)) return default;

        switch (definition.DefaultValue)
        {
            case null:
                return default;
            case T typed:
                return typed;
            case JsonElement element:
                try
                {
                    return element.Deserialize<T>();
                }
                catch (JsonException)
                {
                    return default;
                }
            case IConvertible convertible:
                try
                {
                    var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    return (T)convertible.ToType(target, CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    return default;
                }
            default:
                return default;
        }
    }

    public void Dispose()
    {
        _store.SettingChanged -= OnStoreSettingChanged;
        Changed = null;
    }
}
