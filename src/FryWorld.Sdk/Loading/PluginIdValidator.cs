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

    /// <summary>Throws <see cref="ArgumentException"/> unless <paramref name="identifier"/> is safe to use as a folder name.</summary>
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

        // "." names the parent directory itself, and a leading dot makes a hidden folder; identifiers are
        // used as folder names, so they must start and end with a letter or digit.
        if (!char.IsAsciiLetterOrDigit(identifier[0]) || !char.IsAsciiLetterOrDigit(identifier[^1]))
        {
            throw new ArgumentException($"Invalid identifier '{identifier}'. It must start and end with a letter or digit.", parameterName);
        }
    }

    /// <summary>The install folder for a plugin id and version, after validating both.</summary>
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
