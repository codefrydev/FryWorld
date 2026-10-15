using System;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Hosting;

/// <summary>
/// Settings for <c>services.AddFryWorldPluginHost(...)</c>.
/// </summary>
public sealed class FryWorldHostOptions
{
    /// <summary>
    /// Where plugins are installed. When set, the marketplace service and <see cref="PluginStartup"/> are
    /// registered too.
    /// </summary>
    public string? PluginsDirectory { get; set; }

    /// <summary>Marketplace catalog URL; the SDK's default catalog when null.</summary>
    public string? CatalogUrl { get; set; }

    /// <summary>
    /// This host's version, compared with a plugin's <c>minHostVersion</c> and <c>maxHostVersion</c>. When null,
    /// the version of the host's entry assembly is used.
    /// </summary>
    public Version? HostVersion { get; set; }

    /// <summary>
    /// Where plugin settings are saved. When set, a <see cref="Services.FileSettingsStore"/> is registered as the
    /// <see cref="Services.ISettingsManager"/> unless the host registered its own; otherwise settings are kept in memory.
    /// </summary>
    public string? SettingsFilePath { get; set; }

    /// <summary>Settings for the <see cref="PluginHost"/> itself (logging, event thread, mount concurrency).</summary>
    public PluginHostOptions Host { get; } = new();
}
