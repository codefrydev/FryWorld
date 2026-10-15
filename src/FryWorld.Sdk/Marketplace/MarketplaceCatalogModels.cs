using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Marketplace;

/// <summary>
/// Data model representing an extension item in the remote marketplace catalog feed.
/// </summary>
public sealed class MarketplacePluginItem
{
    /// <summary>The plugin's id; matches the <c>id</c> in its plugin.json.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Who publishes the plugin.</summary>
    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = "Code Fry Dev";

    /// <summary>The latest version; its package is at <see cref="DownloadUrl"/>.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>Category shown for filtering, e.g. "Developer &amp; Tools".</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Tools";

    /// <summary>One-line description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Longer description for the plugin's detail page.</summary>
    [JsonPropertyName("longDescription")]
    public string? LongDescription { get; set; }

    /// <summary>Average rating out of 5.</summary>
    [JsonPropertyName("rating")]
    public double Rating { get; set; } = 5.0;

    /// <summary>How many ratings <see cref="Rating"/> averages.</summary>
    [JsonPropertyName("ratingCount")]
    public int RatingCount { get; set; } = 1;

    /// <summary>How many times the plugin has been installed.</summary>
    [JsonPropertyName("installCount")]
    public int InstallCount { get; set; }

    /// <summary>Download size for display, e.g. "15.0 MB".</summary>
    [JsonPropertyName("formattedSize")]
    public string FormattedSize { get; set; } = string.Empty;

    /// <summary>Material icon kind, e.g. "CodeBraces".</summary>
    [JsonPropertyName("iconKind")]
    public string? IconKind { get; set; }

    /// <summary>Icon colour as #RRGGBB.</summary>
    [JsonPropertyName("iconColorHex")]
    public string? IconColorHex { get; set; }

    /// <summary>SPDX license expression, e.g. "MIT".</summary>
    [JsonPropertyName("license")]
    public string License { get; set; } = "MIT";

    /// <summary>Whether the publisher has been verified.</summary>
    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; } = true;

    /// <summary>Whether the plugin is published by the FryWorld team.</summary>
    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = true;

    /// <summary>Where the latest version's .fryplugin is downloaded from.</summary>
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>The plugin's website.</summary>
    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    /// <summary>The plugin's source repository.</summary>
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    /// <summary>Search tags.</summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>Short feature highlights for the plugin's card.</summary>
    [JsonPropertyName("highlights")]
    public List<string> Highlights { get; set; } = new();

    /// <summary>What the plugin adds to the host, e.g. "Command: Open C# Studio".</summary>
    [JsonPropertyName("contributedFeatures")]
    public List<string> ContributedFeatures { get; set; } = new();

    /// <summary>Every published release, newest first by convention. Each carries its own checksum and requirements.</summary>
    [JsonPropertyName("versions")]
    public List<MarketplacePluginVersion> Versions { get; set; } = new();
}

/// <summary>
/// Model representing a specific version release of a plugin package.
/// </summary>
public sealed class MarketplacePluginVersion
{
    /// <summary>The release's version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>When it was released, as an ISO date.</summary>
    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    /// <summary>Where its .fryplugin is downloaded from.</summary>
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Hex SHA-256 of the .fryplugin. Downloads are verified against it and refused when they do not match.
    /// </summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    /// <summary>Download size for display.</summary>
    [JsonPropertyName("formattedSize")]
    public string FormattedSize { get; set; } = string.Empty;

    /// <summary>Oldest host version this release runs on. Optional.</summary>
    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; set; }

    /// <summary>Newest host version this release runs on. Optional.</summary>
    [JsonPropertyName("maxHostVersion")]
    public string? MaxHostVersion { get; set; }

    /// <summary>Target framework the release was built for, e.g. "net10.0".</summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    /// <summary>What changed in this release.</summary>
    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}
