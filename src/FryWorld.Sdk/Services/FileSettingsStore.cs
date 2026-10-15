using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FryWorld.Sdk.Services;

/// <summary>
/// <see cref="ISettingsManager"/> backed by a JSON file, for hosts and standalone runners.
/// </summary>
/// <remarks>
/// The file maps <c>"pluginId:key"</c> to the setting's JSON value. Writes are coalesced: a change is
/// flushed at most <see cref="FlushDelay"/> later, so a slider drag does not rewrite the file hundreds of
/// times a second, and staleness after a crash is bounded by that delay. <see cref="SaveAsync"/> and
/// <see cref="Dispose"/> flush immediately. The file is replaced atomically, so an interrupted write cannot
/// leave it truncated.
/// </remarks>
public sealed class FileSettingsStore : ISettingsManager, IDisposable
{
    /// <summary>How long after a change the file is written.</summary>
    public static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions FileJsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private int _flushScheduled;
    private bool _isDisposed;

    /// <inheritdoc />
    public event Action<string, string>? SettingChanged;

    /// <summary>Opens (or will create) the settings file at <paramref name="filePath"/>.</summary>
    public FileSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
        _flushTimer = new Timer(_ => FlushScheduled(), null, Timeout.Infinite, Timeout.Infinite);
        Load();
    }

    /// <summary>The settings file.</summary>
    public string FilePath => _filePath;

    private static string Key(string pluginId, string settingKey) => $"{pluginId}:{settingKey}";

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath));
            if (stored == null) return;
            foreach (var (key, value) in stored) _values[key] = value;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable file starts empty rather than failing the host; the next save replaces it.
        }
    }

    /// <inheritdoc />
    public T GetSetting<T>(string pluginId, string settingKey, T defaultValue)
    {
        if (!_values.TryGetValue(Key(pluginId, settingKey), out var raw)) return defaultValue;

        try
        {
            return JsonSerializer.Deserialize<T>(raw) ?? defaultValue;
        }
        catch (JsonException)
        {
            // Stored with a different type than requested; the caller's default is the safest answer.
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public void SetSetting<T>(string pluginId, string settingKey, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);

        var key = Key(pluginId, settingKey);
        var raw = JsonSerializer.Serialize(value);
        if (_values.TryGetValue(key, out var current) && current == raw) return;

        _values[key] = raw;
        ScheduleFlush();
        SettingChanged?.Invoke(pluginId, settingKey);
    }

    /// <summary>Removes every setting stored for <paramref name="pluginId"/>, for example when it is uninstalled.</summary>
    /// <returns>Whether anything was removed.</returns>
    public bool RemovePluginSettings(string pluginId)
    {
        var prefix = pluginId + ":";
        var removed = false;
        foreach (var key in _values.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            removed |= _values.TryRemove(key, out _);
        }

        if (removed) ScheduleFlush();
        return removed;
    }

    /// <inheritdoc />
    public Task SaveAsync() => Task.Run(Save);

    /// <summary>Writes the file now, on the calling thread.</summary>
    public void Save()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        var snapshot = new SortedDictionary<string, string>(_values, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(snapshot, FileJsonOptions);

        lock (_writeLock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }

    // Arms the timer once; later changes ride along instead of pushing the write further out, so a
    // continuous gesture is still saved every FlushDelay.
    private void ScheduleFlush()
    {
        if (_isDisposed) return;
        if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) != 0) return;

        try
        {
            _flushTimer.Change(FlushDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
        }
    }

    private void FlushScheduled()
    {
        if (Volatile.Read(ref _flushScheduled) == 0) return;
        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Try again on the next change rather than crashing a timer thread.
        }
    }

    /// <summary>Writes any pending change and stops the flush timer.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _flushTimer.Dispose();

        if (Volatile.Read(ref _flushScheduled) == 1)
        {
            Save();
        }
    }
}

/// <summary>
/// <see cref="ISettingsManager"/> that keeps settings in memory only, for tests and hosts with nowhere to persist.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsManager
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event Action<string, string>? SettingChanged;

    /// <inheritdoc />
    public T GetSetting<T>(string pluginId, string settingKey, T defaultValue)
    {
        if (!_values.TryGetValue($"{pluginId}:{settingKey}", out var raw)) return defaultValue;
        try
        {
            return JsonSerializer.Deserialize<T>(raw) ?? defaultValue;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public void SetSetting<T>(string pluginId, string settingKey, T value)
    {
        var key = $"{pluginId}:{settingKey}";
        var raw = JsonSerializer.Serialize(value);
        if (_values.TryGetValue(key, out var current) && current == raw) return;

        _values[key] = raw;
        SettingChanged?.Invoke(pluginId, settingKey);
    }

    /// <inheritdoc />
    public Task SaveAsync() => Task.CompletedTask;
}
