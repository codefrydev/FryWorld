using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// Security validator guarding against directory traversal attacks in plugin IDs and versions.
/// </summary>
public static partial class PluginIdValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled)]
    private static partial Regex SafeIdentifierRegex();

    public static void ValidateIdentifier(string identifier, string parameterName = "id")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier, parameterName);

        if (identifier.Contains("..") || identifier.Contains('/') || identifier.Contains('\\'))
        {
            throw new ArgumentException($"Invalid identifier '{identifier}'. Path traversal sequences are strictly forbidden.", parameterName);
        }

        if (!SafeIdentifierRegex().IsMatch(identifier))
        {
            throw new ArgumentException($"Invalid identifier '{identifier}'. Only alphanumeric characters, dashes, dots, and underscores are permitted.", parameterName);
        }
    }

    public static string ResolveInstallDirectory(string basePluginsDirectory, string pluginId, string? version, string context = "")
    {
        ValidateIdentifier(pluginId, nameof(pluginId));

        if (!string.IsNullOrWhiteSpace(version))
        {
            ValidateIdentifier(version, nameof(version));
            return Path.Combine(basePluginsDirectory, pluginId, version);
        }

        return Path.Combine(basePluginsDirectory, pluginId);
    }
}
