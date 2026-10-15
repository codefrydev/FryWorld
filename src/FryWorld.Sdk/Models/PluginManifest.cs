using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Models;

/// <summary>
/// Serializable schema representing the 'plugin.json' manifest found inside '.fryplugin' distribution archives.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Unique id in reverse-domain form; also the install folder name.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Semantic version; the SDK build targets stamp it from the project's <c>Version</c>.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>Who wrote the plugin.</summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>One-line description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>File name of the assembly holding the plugin, relative to plugin.json.</summary>
    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the <see cref="Plugins.IFryPlugin"/> type in <see cref="EntryPoint"/>, e.g.
    /// <c>"MyCompany.MyPlugin.MyPlugin"</c>. Optional; when set, the loader creates that type directly
    /// instead of scanning every public type in the assembly.
    /// </summary>
    [JsonPropertyName("entryType")]
    public string? EntryType { get; set; }

    /// <summary>Material icon kind, e.g. "CodeBraces".</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>Icon colour as #RRGGBB.</summary>
    [JsonPropertyName("iconColor")]
    public string? IconColor { get; set; }

    /// <summary>Category shown for filtering.</summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>The plugin's website.</summary>
    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    /// <summary>The plugin's source repository.</summary>
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    /// <summary>Ids of plugins this plugin needs.</summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    /// <summary>User settings, shown as a generated form in the host's plugin manager.</summary>
    [JsonPropertyName("settingsSchema")]
    public Dictionary<string, PluginSettingDefinition>? SettingsSchema { get; set; }

    /// <summary>Whether the plugin can be unloaded without restarting; false for plugins that cannot unload, such as those hosting Roslyn scripting.</summary>
    [JsonPropertyName("isCollectible")]
    public bool IsCollectible { get; set; } = true;

    /// <summary>What changed in this version.</summary>
    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Oldest host version the plugin runs on, e.g. <c>"1.2.0"</c>. Optional; hosts refuse to install or load
    /// the plugin when they are older.
    /// </summary>
    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; set; }

    /// <summary>Newest host version the plugin is known to run on. Optional; rarely needed.</summary>
    [JsonPropertyName("maxHostVersion")]
    public string? MaxHostVersion { get; set; }

    /// <summary>
    /// FryWorld.Sdk version the plugin was built against. Stamped by the SDK's build targets; informational.
    /// </summary>
    [JsonPropertyName("sdkVersion")]
    public string? SdkVersion { get; set; }

    /// <summary>
    /// Target framework the plugin was built for, e.g. <c>"net10.0"</c>. Stamped by the SDK's build targets;
    /// hosts refuse plugins built for a newer runtime than their own.
    /// </summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }
}
