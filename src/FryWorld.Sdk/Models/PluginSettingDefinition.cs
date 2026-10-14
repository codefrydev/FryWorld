using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Models;

/// <summary>
/// Supported data types for declarative plugin configuration settings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PluginSettingType
{
    String,
    Boolean,
    Integer,
    Double,
    Select,
    FilePath,
    DirectoryPath,
    Color
}

/// <summary>
/// Declarative definition of a user-configurable plugin setting.
/// Enables automatic UI form generation in the Plugins Manager.
/// </summary>
public sealed class PluginSettingDefinition
{
    [JsonPropertyName("type")]
    public PluginSettingType Type { get; set; } = PluginSettingType.String;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("min")]
    public double? MinValue { get; set; }

    [JsonPropertyName("max")]
    public double? MaxValue { get; set; }

    [JsonPropertyName("step")]
    public double? Step { get; set; }
}
