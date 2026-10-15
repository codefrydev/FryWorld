using System;
using System.Collections.Generic;
using System.IO;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// How plugins are laid out in a plugins directory, and how removals that cannot happen right away (because
/// a plugin's files are in use) are deferred to the next start.
/// </summary>
/// <remarks>
/// <code>
/// plugins/
///   com.example.studio/          one folder per plugin id
///     1.2.0/                     one folder per installed version; the highest is loaded
///       plugin.json, *.dll, ...
///     1.1.0/.remove              superseded version: skipped, deleted at next start
///   com.example.old/.uninstall   uninstalled plugin: skipped, deleted at next start
///   Sideloaded.fryplugin         packages dropped here are extracted on start
/// </code>
/// </remarks>
public static class PluginInstallLayout
{
    /// <summary>Marker file in a plugin's folder: the plugin was uninstalled and is removed at next start.</summary>
    public const string UninstallMarkerFileName = ".uninstall";

    /// <summary>Marker file in a version folder: the version was superseded and is removed at next start.</summary>
    public const string RemoveMarkerFileName = ".remove";

    /// <summary>Whether <paramref name="pluginDirectory"/> has been uninstalled.</summary>
    public static bool IsUninstalled(string pluginDirectory) =>
        File.Exists(Path.Combine(pluginDirectory, UninstallMarkerFileName));

    /// <summary>Whether <paramref name="versionDirectory"/> has been superseded.</summary>
    public static bool IsRemoved(string versionDirectory) =>
        File.Exists(Path.Combine(versionDirectory, RemoveMarkerFileName));

    /// <summary>
    /// Deletes <paramref name="directory"/>, or, when files in it are in use, marks it with
    /// <paramref name="markerFileName"/> so it is skipped and deleted at next start.
    /// </summary>
    /// <returns>True when the directory is gone now; false when it was marked for later.</returns>
    public static bool DeleteOrMark(string directory, string markerFileName)
    {
        if (!Directory.Exists(directory)) return true;

        try
        {
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Some files may already be gone; the marker keeps what is left from loading.
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, markerFileName), DateTimeOffset.UtcNow.ToString("O"));
            return false;
        }
    }

    /// <summary>
    /// Deletes everything marked for removal under <paramref name="pluginsDirectory"/>. What is still in use
    /// stays marked, and is reported to <paramref name="diagnostics"/>.
    /// </summary>
    public static void CleanUpPendingRemovals(string pluginsDirectory, ICollection<Plugins.PluginLoadDiagnostic>? diagnostics = null)
    {
        if (!Directory.Exists(pluginsDirectory)) return;

        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
        {
            if (IsUninstalled(pluginDir))
            {
                TryDelete(pluginDir, diagnostics);
                continue;
            }

            foreach (var versionDir in Directory.GetDirectories(pluginDir))
            {
                if (IsRemoved(versionDir)) TryDelete(versionDir, diagnostics);
            }
        }
    }

    private static void TryDelete(string directory, ICollection<Plugins.PluginLoadDiagnostic>? diagnostics)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics?.Add(new Plugins.PluginLoadDiagnostic(Plugins.PluginLoadStage.Uninstall, Plugins.PluginDiagnosticSeverity.Info,
                $"Could not remove yet ({ex.Message}); will retry at next start.", Path: directory, Exception: ex));
        }
    }
}
