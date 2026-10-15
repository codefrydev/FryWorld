using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FryWorld.Sdk.Services;

/// <summary>Severity of an <see cref="AppLogEntry"/>.</summary>
public enum AppLogLevel
{
    /// <summary>Detail for developers.</summary>
    Debug,
    /// <summary>Normal operation.</summary>
    Info,
    /// <summary>Something unexpected that the app recovered from.</summary>
    Warning,
    /// <summary>An operation failed.</summary>
    Error
}

/// <summary>One message in the <see cref="AppLogService"/> buffer.</summary>
public sealed class AppLogEntry
{
    /// <summary>When it was logged, in UTC.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    /// <summary>How serious it is.</summary>
    public AppLogLevel Level { get; init; }
    /// <summary>The component that logged it, e.g. "PluginHost".</summary>
    public string Category { get; init; } = "General";
    /// <summary>The message.</summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>The error that was logged with it, if any.</summary>
    public Exception? Exception { get; init; }

    /// <inheritdoc />
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
    /// <summary>The process-wide log.</summary>
    public static AppLogService Instance => _instance.Value;

    private readonly ConcurrentQueue<AppLogEntry> _logBuffer = new();
    private const int MaxBufferSize = 1000;

    /// <summary>Raised after every entry is added, on the thread that logged it.</summary>
    public event Action<AppLogEntry>? EntryLogged;

    /// <summary>Adds an entry; the oldest entries are dropped beyond 1000.</summary>
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

    /// <summary>Logs at <see cref="AppLogLevel.Info"/>.</summary>
    public void LogInfo(string category, string message) => Log(AppLogLevel.Info, category, message);
    /// <summary>Logs at <see cref="AppLogLevel.Warning"/>.</summary>
    public void LogWarning(string category, string message, Exception? ex = null) => Log(AppLogLevel.Warning, category, message, ex);
    /// <summary>Logs at <see cref="AppLogLevel.Error"/>.</summary>
    public void LogError(string category, string message, Exception? ex = null) => Log(AppLogLevel.Error, category, message, ex);

    /// <summary>The newest <paramref name="count"/> entries, oldest first.</summary>
    public IReadOnlyList<AppLogEntry> GetRecentEntries(int count = 100)
    {
        return _logBuffer.Reverse().Take(count).Reverse().ToList();
    }

    /// <summary>
    /// An <see cref="ILogger"/> that writes to this log, so SDK components that take a logger (such as
    /// <c>PluginHostOptions.Logger</c>) show up in the host's log view.
    /// </summary>
    public ILogger CreateLogger(string category) => new AppLogLogger(this, category);
}

/// <summary>
/// <see cref="ILoggerProvider"/> over <see cref="AppLogService"/>, for hosts using Microsoft.Extensions.Logging:
/// <c>loggingBuilder.AddProvider(new AppLogLoggerProvider())</c>.
/// </summary>
public sealed class AppLogLoggerProvider : ILoggerProvider
{
    private readonly AppLogService _log;

    /// <summary>Writes to <see cref="AppLogService.Instance"/>.</summary>
    public AppLogLoggerProvider() : this(AppLogService.Instance)
    {
    }

    /// <summary>Writes to <paramref name="log"/>.</summary>
    public AppLogLoggerProvider(AppLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => _log.CreateLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

internal sealed class AppLogLogger : ILogger
{
    private readonly AppLogService _log;
    private readonly string _category;

    public AppLogLogger(AppLogService log, string category)
    {
        _log = log;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var level = logLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => AppLogLevel.Debug,
            LogLevel.Information => AppLogLevel.Info,
            LogLevel.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Error
        };
        _log.Log(level, _category, formatter(state, exception), exception);
    }
}
