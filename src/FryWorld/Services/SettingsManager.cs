using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FryWorld.Sdk.Services;

namespace FryWorld.Services;

public sealed class NotificationService : INotificationService
{
    public event Action<string, string, string?>? ToastRequested;

    public void ShowToast(string message, string title = "Information", string? iconKind = null)
    {
        ToastRequested?.Invoke(message, title, iconKind);
    }

    public void ShowError(string message, string title = "Error")
    {
        ToastRequested?.Invoke(message, title, "AlertCircle");
    }
}

public sealed class SettingsManager : ISettingsManager
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly string _filePath;

    public SettingsManager(string? filePath = null)
    {
        _filePath = filePath ?? FryWorldPaths.SettingsFilePath;
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var dict = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json);
                if (dict != null)
                {
                    foreach (var (k, v) in dict) _store[k] = v;
                }
            }
        }
        catch { }
    }

    public T GetSetting<T>(string pluginId, string settingKey, T defaultValue)
    {
        var key = $"{pluginId}:{settingKey}";
        if (_store.TryGetValue(key, out var raw))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(raw) ?? defaultValue;
            }
            catch { }
        }
        return defaultValue;
    }

    public void SetSetting<T>(string pluginId, string settingKey, T value)
    {
        var key = $"{pluginId}:{settingKey}";
        _store[key] = JsonSerializer.Serialize(value);
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch { }
    }
}
