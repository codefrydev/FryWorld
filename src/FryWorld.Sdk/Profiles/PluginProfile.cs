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
    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = "default";

    [JsonPropertyName("enabledBundles")]
    public List<string> EnabledBundles { get; set; } = new();

    [JsonPropertyName("disabledPlugins")]
    public List<string> DisabledPlugins { get; set; } = new();
}

/// <summary>
/// Utility for loading and applying runtime plugin profiles.
/// </summary>
public static class ProfileLoader
{
    public static PluginProfile LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new PluginProfile();
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<PluginProfile>(json) ?? new PluginProfile();
    }

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
