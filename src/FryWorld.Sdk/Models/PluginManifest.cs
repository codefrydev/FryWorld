using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Models;

/// <summary>
/// Serializable schema representing the 'plugin.json' manifest found inside '.fryplugin' distribution archives.
/// </summary>
public sealed class PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("iconColor")]
    public string? IconColor { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("settingsSchema")]
    public Dictionary<string, PluginSettingDefinition>? SettingsSchema { get; set; }

    [JsonPropertyName("isCollectible")]
    public bool IsCollectible { get; set; } = true;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}
