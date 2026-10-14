using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FryWorld.Sdk.Marketplace;

/// <summary>
/// Data model representing an extension item in the remote marketplace catalog feed.
/// </summary>
public sealed class MarketplacePluginItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = "Code Fry Dev";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Tools";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("longDescription")]
    public string? LongDescription { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; } = 5.0;

    [JsonPropertyName("ratingCount")]
    public int RatingCount { get; set; } = 1;

    [JsonPropertyName("installCount")]
    public int InstallCount { get; set; }

    [JsonPropertyName("formattedSize")]
    public string FormattedSize { get; set; } = string.Empty;

    [JsonPropertyName("iconKind")]
    public string? IconKind { get; set; }

    [JsonPropertyName("iconColorHex")]
    public string? IconColorHex { get; set; }

    [JsonPropertyName("license")]
    public string License { get; set; } = "MIT";

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; } = true;

    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = true;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("websiteUrl")]
    public string? WebsiteUrl { get; set; }

    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("highlights")]
    public List<string> Highlights { get; set; } = new();

    [JsonPropertyName("contributedFeatures")]
    public List<string> ContributedFeatures { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<MarketplacePluginVersion> Versions { get; set; } = new();
}

/// <summary>
/// Model representing a specific version release of a plugin package.
/// </summary>
public sealed class MarketplacePluginVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("formattedSize")]
    public string FormattedSize { get; set; } = string.Empty;

    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; set; }

    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }
}
