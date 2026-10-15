using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FryWorld.Sdk.Models;

/// <summary>
/// A semantic version (<c>major.minor.patch[-prerelease][+build]</c>) as plugins, catalogs and manifests write
/// it, ordered by the Semantic Versioning 2.0 rules: <c>1.0.0-alpha &lt; 1.0.0-alpha.1 &lt; 1.0.0-beta &lt; 1.0.0</c>.
/// <see cref="System.Version"/> cannot parse prerelease tags, which is why update checks used to fall back to
/// "is the string different".
/// </summary>
public readonly struct PluginVersion : IComparable<PluginVersion>, IEquatable<PluginVersion>
{
    private PluginVersion(int major, int minor, int patch, string prerelease, string build)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Build = build;
    }

    /// <summary>The major version.</summary>
    public int Major { get; }

    /// <summary>The minor version.</summary>
    public int Minor { get; }

    /// <summary>The patch version.</summary>
    public int Patch { get; }

    /// <summary>The prerelease tag without the leading '-', or empty for a release.</summary>
    public string Prerelease { get; }

    /// <summary>Build metadata without the leading '+'; ignored when comparing.</summary>
    public string Build { get; }

    /// <summary>Whether this is a prerelease.</summary>
    public bool IsPrerelease => !string.IsNullOrEmpty(Prerelease);

    /// <summary>
    /// Parses <c>1</c>, <c>1.2</c>, <c>1.2.3</c>, <c>1.2.3.4</c> (the fourth part is ignored), with an optional
    /// leading <c>v</c>, prerelease tag and build metadata.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out PluginVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim();
        if (s[0] is 'v' or 'V') s = s[1..];

        var build = string.Empty;
        var plus = s.IndexOf('+');
        if (plus >= 0)
        {
            build = s[(plus + 1)..];
            s = s[..plus];
        }

        var prerelease = string.Empty;
        var dash = s.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = s[(dash + 1)..];
            s = s[..dash];
            if (prerelease.Length == 0) return false;
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 4) return false;

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return false;
            if (i < 3) numbers[i] = n;
        }

        version = new PluginVersion(numbers[0], numbers[1], numbers[2], prerelease, build);
        return true;
    }

    /// <summary>Parses <paramref name="text"/>, throwing <see cref="FormatException"/> if it is not a version.</summary>
    public static PluginVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"'{text}' is not a version.");

    /// <summary>Converts a <see cref="System.Version"/>; its revision is dropped.</summary>
    public static PluginVersion FromVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new PluginVersion(version.Major, version.Minor, Math.Max(version.Build, 0), string.Empty, string.Empty);
    }

    /// <inheritdoc />
    public int CompareTo(PluginVersion other)
    {
        var byNumbers = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byNumbers != 0) return byNumbers;

        // A release is newer than any of its prereleases.
        if (IsPrerelease != other.IsPrerelease) return IsPrerelease ? -1 : 1;
        return ComparePrerelease(Prerelease ?? string.Empty, other.Prerelease ?? string.Empty);
    }

    private static int ComparePrerelease(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftIsNumber = int.TryParse(left[i], NumberStyles.None, CultureInfo.InvariantCulture, out var l);
            var rightIsNumber = int.TryParse(right[i], NumberStyles.None, CultureInfo.InvariantCulture, out var r);

            var result = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => l.CompareTo(r),
                (true, false) => -1, // numeric identifiers sort before alphanumeric ones
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i])
            };
            if (result != 0) return result;
        }

        return left.Length.CompareTo(right.Length);
    }

    /// <inheritdoc />
    public bool Equals(PluginVersion other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PluginVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease ?? string.Empty);

    /// <inheritdoc />
    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}" +
        (IsPrerelease ? "-" + Prerelease : string.Empty) +
        (string.IsNullOrEmpty(Build) ? string.Empty : "+" + Build);

    /// <summary>Whether <paramref name="left"/> is older than <paramref name="right"/>.</summary>
    public static bool operator <(PluginVersion left, PluginVersion right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is newer than <paramref name="right"/>.</summary>
    public static bool operator >(PluginVersion left, PluginVersion right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is the same version as or older than <paramref name="right"/>.</summary>
    public static bool operator <=(PluginVersion left, PluginVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is the same version as or newer than <paramref name="right"/>.</summary>
    public static bool operator >=(PluginVersion left, PluginVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Whether the two are the same version (build metadata ignored).</summary>
    public static bool operator ==(PluginVersion left, PluginVersion right) => left.Equals(right);

    /// <summary>Whether the two are different versions.</summary>
    public static bool operator !=(PluginVersion left, PluginVersion right) => !left.Equals(right);
}
