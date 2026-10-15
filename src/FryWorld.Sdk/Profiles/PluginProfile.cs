using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Profiles;

/// <summary>
/// Runtime profile definition enabling/disabling plugin bundles and individual plugins.
/// </summary>
public sealed class PluginProfile
{
    /// <summary>Name of the profile.</summary>
    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = "default";

    /// <summary>Bundles to load; all bundles when empty.</summary>
    [JsonPropertyName("enabledBundles")]
    public List<string> EnabledBundles { get; set; } = new();

    /// <summary>Plugins to leave out, even when their bundle is enabled.</summary>
    [JsonPropertyName("disabledPlugins")]
    public List<string> DisabledPlugins { get; set; } = new();
}

/// <summary>
/// Utility for loading and applying runtime plugin profiles.
/// </summary>
public static class ProfileLoader
{
    /// <summary>Reads a profile from JSON; an empty profile when the file does not exist.</summary>
    public static PluginProfile LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new PluginProfile();
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PluginProfile>(json) ?? new PluginProfile();
    }

    /// <summary>Registers the plugins of the enabled bundles with <paramref name="host"/>, except disabled ones.</summary>
    public static void ApplyProfile(PluginProfile profile, PluginHost host, IEnumerable<IFryPluginBundle> availableBundles)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(host);

        var enabledBundlesSet = new HashSet<string>(profile.EnabledBundles, StringComparer.OrdinalIgnoreCase);
        var disabledPluginsSet = new HashSet<string>(profile.DisabledPlugins, StringComparer.OrdinalIgnoreCase);

        foreach (var bundle in availableBundles)
        {
            // If enabledBundles is empty, all bundles are enabled by default
            if (enabledBundlesSet.Count > 0 && !enabledBundlesSet.Contains(bundle.BundleId))
            {
                continue;
            }

            foreach (var plugin in bundle.Plugins)
            {
                if (disabledPluginsSet.Contains(plugin.Id))
                {
                    continue;
                }

                try
                {
                    host.RegisterPlugin(plugin);
                }
                catch { }
            }
        }
    }
}
