using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Models;

/// <summary>
/// Supported data types for declarative plugin configuration settings.
/// </summary>
[JsonConverter(typeof(PluginSettingTypeJsonConverter))]
public enum PluginSettingType
{
    /// <summary>Free text.</summary>
    String,
    /// <summary>On/off switch.</summary>
    Boolean,
    /// <summary>Whole number, optionally bounded by <see cref="PluginSettingDefinition.MinValue"/> and <see cref="PluginSettingDefinition.MaxValue"/>.</summary>
    Integer,
    /// <summary>Decimal number, optionally bounded and stepped.</summary>
    Double,
    /// <summary>One of <see cref="PluginSettingDefinition.Options"/>.</summary>
    Select,
    /// <summary>Path to a file, picked with a file dialog.</summary>
    FilePath,
    /// <summary>Path to a folder, picked with a folder dialog.</summary>
    DirectoryPath,
    /// <summary>Colour as #RRGGBB or #AARRGGBB.</summary>
    Color
}

/// <summary>
/// Permissive JSON converter that gracefully maps common type names (such as "number", "bool", "dir")
/// to <see cref="PluginSettingType"/> without throwing deserialization errors.
/// </summary>
public sealed class PluginSettingTypeJsonConverter : JsonConverter<PluginSettingType>
{
    /// <inheritdoc />
    public override PluginSettingType Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return PluginSettingType.String;

            return str.ToLowerInvariant() switch
            {
                "string" => PluginSettingType.String,
                "boolean" or "bool" => PluginSettingType.Boolean,
                "integer" or "int" => PluginSettingType.Integer,
                "double" or "float" or "number" or "num" => PluginSettingType.Double,
                "select" => PluginSettingType.Select,
                "filepath" or "file" => PluginSettingType.FilePath,
                "directorypath" or "directory" or "dir" or "folder" => PluginSettingType.DirectoryPath,
                "color" or "colour" => PluginSettingType.Color,
                _ => Enum.TryParse<PluginSettingType>(str, ignoreCase: true, out var parsed) ? parsed : PluginSettingType.String
            };
        }

        if (reader.TokenType == System.Text.Json.JsonTokenType.Number && reader.TryGetInt32(out var intVal) && Enum.IsDefined(typeof(PluginSettingType), intVal))
        {
            return (PluginSettingType)intVal;
        }

        return PluginSettingType.String;
    }

    /// <inheritdoc />
    public override void Write(System.Text.Json.Utf8JsonWriter writer, PluginSettingType value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Declarative definition of a user-configurable plugin setting.
/// Enables automatic UI form generation in the Plugins Manager.
/// </summary>
public sealed class PluginSettingDefinition
{
    /// <summary>What kind of value it is; decides the form control.</summary>
    [JsonPropertyName("type")]
    public PluginSettingType Type { get; set; } = PluginSettingType.String;

    /// <summary>Label shown next to the control.</summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Help text shown under the control.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Value used until the user changes it. A CLR value in code, a JSON value in plugin.json.</summary>
    [JsonPropertyName("default")]
    public object? DefaultValue { get; set; }

    /// <summary>Choices for a <see cref="PluginSettingType.Select"/> setting.</summary>
    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    /// <summary>Smallest allowed number.</summary>
    [JsonPropertyName("min")]
    public double? MinValue { get; set; }

    /// <summary>Largest allowed number.</summary>
    [JsonPropertyName("max")]
    public double? MaxValue { get; set; }

    /// <summary>Increment for number controls.</summary>
    [JsonPropertyName("step")]
    public double? Step { get; set; }
}
