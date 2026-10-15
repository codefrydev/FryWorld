using System;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Where in a plugin's life a <see cref="PluginLoadDiagnostic"/> was raised.
/// </summary>
public enum PluginLoadStage
{
    /// <summary>Scanning the plugins directory for packages and plugin folders.</summary>
    Discovery,

    /// <summary>Unpacking a .fryplugin archive.</summary>
    Extraction,

    /// <summary>Loading the plugin's assemblies into its load context.</summary>
    AssemblyLoad,

    /// <summary>Creating the plugin's <see cref="IFryPlugin"/> instances.</summary>
    Instantiation,

    /// <summary>Ordering plugins by their service dependencies.</summary>
    DependencyResolution,

    /// <summary>Running the plugin's <see cref="IFryPlugin.ApplyAsync"/>.</summary>
    Mount,

    /// <summary>Rolling back the plugin's effects.</summary>
    Unmount,

    /// <summary>Removing an installed plugin from disk.</summary>
    Uninstall,

    /// <summary>Registering or dispatching pipeline handlers.</summary>
    Pipeline
}

/// <summary>
/// How serious a <see cref="PluginLoadDiagnostic"/> is.
/// </summary>
public enum PluginDiagnosticSeverity
{
    /// <summary>Worth knowing; nothing went wrong.</summary>
    Info,

    /// <summary>The plugin works, but something is likely to cause trouble (for example, a guessed entry assembly).</summary>
    Warning,

    /// <summary>The plugin, or part of it, did not load or did not unload cleanly.</summary>
    Error
}

/// <summary>
/// A record of something that went wrong, or nearly did, while finding, loading, mounting or unloading a plugin.
/// Hosts show these so users and plugin authors can see why a plugin is missing instead of it silently not appearing.
/// </summary>
/// <param name="Stage">Where it happened.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">A sentence a person can act on.</param>
/// <param name="PluginId">The plugin involved, when known.</param>
/// <param name="Path">The file or directory involved, when there is one.</param>
/// <param name="Exception">The underlying error, when there is one.</param>
public sealed record PluginLoadDiagnostic(
    PluginLoadStage Stage,
    PluginDiagnosticSeverity Severity,
    string Message,
    string? PluginId = null,
    string? Path = null,
    Exception? Exception = null)
{
    /// <summary>When the diagnostic was raised.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public override string ToString() =>
        $"[{Severity}] [{Stage}] {(PluginId != null ? $"{PluginId}: " : string.Empty)}{Message}" +
        (Path != null ? $" ({Path})" : string.Empty) +
        (Exception != null ? $" — {Exception.GetType().Name}: {Exception.Message}" : string.Empty);
}
