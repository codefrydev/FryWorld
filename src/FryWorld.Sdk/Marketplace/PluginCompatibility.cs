using System;
using System.Globalization;
using System.Reflection;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Marketplace;

/// <summary>Why a plugin can or cannot run on this host.</summary>
public enum PluginCompatibilityStatus
{
    /// <summary>The plugin can run.</summary>
    Compatible,

    /// <summary>The plugin needs a newer host (its <c>minHostVersion</c> is above this host's version).</summary>
    HostUpgradeRequired,

    /// <summary>The plugin declares a <c>maxHostVersion</c> below this host's version.</summary>
    HostTooNew,

    /// <summary>The plugin targets a newer .NET than this host runs on.</summary>
    IncompatibleRuntime,

    /// <summary>The plugin was built against a newer FryWorld.Sdk (major or minor) than the host has, so it may use APIs the host lacks.</summary>
    HostSdkTooOld
}

/// <summary>The outcome of <see cref="PluginCompatibility.Check(string?, string?, string?, Version?, string?)"/>.</summary>
/// <param name="Status">Why.</param>
/// <param name="IsCompatible">Whether the plugin can run.</param>
/// <param name="Message">A sentence to show the user, e.g. "Requires FryWorld 1.4.0 or newer (this is 1.2.0)."</param>
public readonly record struct PluginCompatibilityResult(PluginCompatibilityStatus Status, bool IsCompatible, string Message);

/// <summary>
/// Checks a plugin's declared host and runtime requirements (from its manifest or a catalog entry) against
/// this host, so incompatible plugins are refused with a clear reason instead of failing to load.
/// </summary>
public static class PluginCompatibility
{
    private static readonly Lazy<Version> EntryAssemblyVersion = new(() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));

    /// <summary>The version used when a host does not say: its entry assembly's version.</summary>
    public static Version DefaultHostVersion => EntryAssemblyVersion.Value;

    private static readonly Lazy<PluginVersion> SdkVersion = new(() =>
    {
        var informational = typeof(PluginCompatibility).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return PluginVersion.TryParse(informational, out var version) ? version : PluginVersion.Parse("0.0.0");
    });

    /// <summary>The FryWorld.Sdk version this host runs, from the SDK assembly's informational version.</summary>
    public static PluginVersion CurrentSdkVersion => SdkVersion.Value;

    /// <summary>The target framework moniker of the runtime the host is on, e.g. <c>net10.0</c>.</summary>
    public static string CurrentTargetFramework => $"net{Environment.Version.Major}.{Environment.Version.Minor}";

    /// <summary>Checks the requirements in a plugin's manifest.</summary>
    /// <remarks>
    /// Also compares the manifest's stamped <c>sdkVersion</c> with <see cref="CurrentSdkVersion"/>: patch releases
    /// add no API, so only a newer major or minor version is refused.
    /// </remarks>
    public static PluginCompatibilityResult Check(PluginManifest manifest, Version? hostVersion = null, string? hostTargetFramework = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var result = Check(manifest.MinHostVersion, manifest.MaxHostVersion, manifest.TargetFramework, hostVersion, hostTargetFramework);
        if (!result.IsCompatible) return result;

        return CheckSdkVersion(manifest.SdkVersion, CurrentSdkVersion) ?? result;
    }

    /// <summary>
    /// Refuses a plugin built against a newer FryWorld.Sdk major or minor version than <paramref name="hostSdkVersion"/>;
    /// null when compatible or when <paramref name="pluginSdkVersion"/> is not given.
    /// </summary>
    public static PluginCompatibilityResult? CheckSdkVersion(string? pluginSdkVersion, PluginVersion hostSdkVersion)
    {
        if (!PluginVersion.TryParse(pluginSdkVersion, out var built)) return null;

        // A host whose SDK version is unknown (0.0.0, e.g. a local build) cannot judge; let it load.
        if (hostSdkVersion.Major == 0 && hostSdkVersion.Minor == 0 && hostSdkVersion.Patch == 0) return null;

        if ((built.Major, built.Minor).CompareTo((hostSdkVersion.Major, hostSdkVersion.Minor)) > 0)
        {
            return new PluginCompatibilityResult(PluginCompatibilityStatus.HostSdkTooOld, false,
                $"Built against FryWorld.Sdk {built.Major}.{built.Minor}; this host has {hostSdkVersion.Major}.{hostSdkVersion.Minor}. Update the app.");
        }

        return null;
    }

    /// <summary>Checks the requirements of a catalog release, before downloading it.</summary>
    public static PluginCompatibilityResult Check(MarketplacePluginVersion release, Version? hostVersion = null, string? hostTargetFramework = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        return Check(release.MinHostVersion, release.MaxHostVersion, release.TargetFramework, hostVersion, hostTargetFramework);
    }

    /// <summary>Checks a plugin's requirements against <paramref name="hostVersion"/> and <paramref name="hostTargetFramework"/>.</summary>
    /// <param name="minHostVersion">Oldest host version the plugin runs on; unchecked when empty or unparsable.</param>
    /// <param name="maxHostVersion">Newest host version the plugin runs on; unchecked when empty or unparsable.</param>
    /// <param name="targetFramework">The plugin's target framework, e.g. <c>net10.0</c>; unchecked when empty.</param>
    /// <param name="hostVersion">This host's version; <see cref="DefaultHostVersion"/> when null.</param>
    /// <param name="hostTargetFramework">This host's runtime; <see cref="CurrentTargetFramework"/> when null.</param>
    public static PluginCompatibilityResult Check(
        string? minHostVersion,
        string? maxHostVersion,
        string? targetFramework,
        Version? hostVersion = null,
        string? hostTargetFramework = null)
    {
        var host = PluginVersion.FromVersion(hostVersion ?? DefaultHostVersion);
        var hostTfm = hostTargetFramework ?? CurrentTargetFramework;

        if (!string.IsNullOrWhiteSpace(targetFramework) && !IsTargetFrameworkSupported(hostTfm, targetFramework))
        {
            return new PluginCompatibilityResult(PluginCompatibilityStatus.IncompatibleRuntime, false,
                $"Requires .NET '{targetFramework.Trim()}'; this host runs on '{hostTfm}'.");
        }

        if (PluginVersion.TryParse(minHostVersion, out var min) && host < min)
        {
            return new PluginCompatibilityResult(PluginCompatibilityStatus.HostUpgradeRequired, false,
                $"Requires FryWorld {min} or newer (this is {host}).");
        }

        if (PluginVersion.TryParse(maxHostVersion, out var max) && host > max)
        {
            return new PluginCompatibilityResult(PluginCompatibilityStatus.HostTooNew, false,
                $"Supports FryWorld up to {max} (this is {host}).");
        }

        return new PluginCompatibilityResult(PluginCompatibilityStatus.Compatible, true, "Compatible with this version of FryWorld.");
    }

    /// <summary>
    /// Whether a host on <paramref name="hostTfm"/> can run a plugin built for <paramref name="pluginTfm"/>:
    /// any <c>netstandard</c>, or a <c>netX.Y</c> no newer than the host's. Platform suffixes
    /// (<c>net10.0-windows</c>) are ignored.
    /// </summary>
    public static bool IsTargetFrameworkSupported(string hostTfm, string pluginTfm)
    {
        var plugin = pluginTfm.Trim().ToLowerInvariant();
        var host = hostTfm.Trim().ToLowerInvariant();
        if (plugin == host || plugin.StartsWith("netstandard", StringComparison.Ordinal)) return true;

        return TryParseNetVersion(host, out var hostVersion) &&
               TryParseNetVersion(plugin, out var pluginVersion) &&
               pluginVersion <= hostVersion;
    }

    private static bool TryParseNetVersion(string tfm, out Version version)
    {
        version = new Version(0, 0);
        if (!tfm.StartsWith("net", StringComparison.Ordinal) || tfm.StartsWith("netstandard", StringComparison.Ordinal)) return false;

        var numbers = tfm[3..];
        var dash = numbers.IndexOf('-');
        if (dash >= 0) numbers = numbers[..dash];

        return Version.TryParse(numbers.Contains('.') ? numbers : numbers + ".0", out version!) &&
               version.Major >= 5; // net472 and friends are .NET Framework, which a .NET host cannot load
    }
}

/// <summary>
/// Thrown when installing or loading a plugin whose declared requirements this host does not meet.
/// </summary>
public sealed class PluginIncompatibleException : PluginException
{
    /// <summary>Creates the exception for <paramref name="pluginId"/>.</summary>
    public PluginIncompatibleException(string pluginId, PluginCompatibilityResult result)
        : base($"Plugin '{pluginId}' cannot run here. {result.Message}")
    {
        PluginId = pluginId;
        Result = result;
    }

    /// <summary>The plugin that was refused.</summary>
    public string PluginId { get; }

    /// <summary>Why it was refused.</summary>
    public PluginCompatibilityResult Result { get; }
}
