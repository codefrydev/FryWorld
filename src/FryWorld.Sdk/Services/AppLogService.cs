using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FryWorld.Sdk.Services;

public enum AppLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public sealed class AppLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public AppLogLevel Level { get; init; }
    public string Category { get; init; } = "General";
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Category}] {Message}" +
        (Exception != null ? $"\nException: {Exception}" : string.Empty);
}

/// <summary>
/// Singleton diagnostic logging service providing an in-memory ring buffer for debugging and telemetry.
/// </summary>
public sealed class AppLogService
{
    private static readonly Lazy<AppLogService> _instance = new(() => new AppLogService());
    public static AppLogService Instance => _instance.Value;

    private readonly ConcurrentQueue<AppLogEntry> _logBuffer = new();
    private const int MaxBufferSize = 1000;

    public event Action<AppLogEntry>? EntryLogged;

    public void Log(AppLogLevel level, string category, string message, Exception? ex = null)
    {
        var entry = new AppLogEntry
        {
            Level = level,
            Category = category,
            Message = message,
            Exception = ex
        };

        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > MaxBufferSize)
        {
            _logBuffer.TryDequeue(out _);
        }

        Debug.WriteLine(entry.ToString());
        EntryLogged?.Invoke(entry);
    }

    public void LogInfo(string category, string message) => Log(AppLogLevel.Info, category, message);
    public void LogWarning(string category, string message, Exception? ex = null) => Log(AppLogLevel.Warning, category, message, ex);
    public void LogError(string category, string message, Exception? ex = null) => Log(AppLogLevel.Error, category, message, ex);

    public IReadOnlyList<AppLogEntry> GetRecentEntries(int count = 100)
    {
        return _logBuffer.Reverse().Take(count).Reverse().ToList();
    }
}
