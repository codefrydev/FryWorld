using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FryWorld.Sdk.Tests.TestSupport;

/// <summary>
/// Collects log messages so tests can assert on what the SDK reported.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public bool HasWarningContaining(string text) =>
        Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Enqueue((logLevel, formatter(state, exception)));
}
