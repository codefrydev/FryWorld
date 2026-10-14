using System;
using System.IO;

namespace FryWorld;

/// <summary>
/// Cross-platform and sandbox/MSIX-safe paths for FryWorld application data and plugin storage.
/// </summary>
public static class FryWorldPaths
{
    private static readonly Lazy<string> _appDataDir = new(() =>
    {
        string root;
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            root = Path.Combine(home, "Library", "Application Support", "FryWorld");
        }
        else if (OperatingSystem.IsWindows())
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FryWorld");
        }
        else
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(config))
            {
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }
            root = Path.Combine(config, "FryWorld");
        }

        Directory.CreateDirectory(root);
        return root;
    });

    public static string AppDataDirectory => _appDataDir.Value;

    public static string PluginsDirectory
    {
        get
        {
            var path = Path.Combine(AppDataDirectory, "plugins");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string CacheDirectory
    {
        get
        {
            var path = Path.Combine(AppDataDirectory, "cache");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");
}
