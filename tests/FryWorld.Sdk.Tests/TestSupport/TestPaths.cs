using System;
using System.IO;
using Xunit;

namespace FryWorld.Sdk.Tests.TestSupport;

/// <summary>
/// Locations of the plugins the tests load from disk.
/// </summary>
public static class TestPaths
{
    public const string FixturePluginId = "com.fryworld.tests.fixture";
    public const string FixturePluginAssemblyName = "FryWorld.Sdk.FixturePlugin";

    /// <summary>
    /// Build output of tests/Fixtures/FryWorld.Sdk.FixturePlugin, copied here by the test project.
    /// </summary>
    public static string FixturePluginDirectory =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "FixturePlugin");

    public static string FixturePluginAssembly =>
        Path.Combine(FixturePluginDirectory, FixturePluginAssemblyName + ".dll");

    /// <summary>
    /// The released FrySharp package, for the opt-in integration tests. Set FRYWORLD_CSHARPEDITOR_PACKAGE
    /// to point elsewhere; otherwise the sibling FrySharp checkout's Release output is used if it exists.
    /// </summary>
    public static string? CSharpEditorPackage
    {
        get
        {
            var fromEnv = Environment.GetEnvironmentVariable("FRYWORLD_CSHARPEDITOR_PACKAGE");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return File.Exists(fromEnv) ? fromEnv : null;
            }

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "FrySharp", "bin", "Release", "net10.0", "CSharpEditor.fryplugin");
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }

    /// <summary>
    /// The FryWorld plugins directory on this machine, if FrySharp is installed in it.
    /// </summary>
    public static string? InstalledPluginsDirectoryWithCSharpEditor
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "FryWorld", "plugins");

            return Directory.Exists(Path.Combine(dir, "com.fryworld.csharpstudio")) ? dir : null;
        }
    }
}

/// <summary>
/// Runs only when a built FrySharp package is available, and reports Skipped otherwise.
/// </summary>
public sealed class CSharpEditorPackageFactAttribute : FactAttribute
{
    public CSharpEditorPackageFactAttribute()
    {
        if (TestPaths.CSharpEditorPackage is null)
        {
            Skip = "FrySharp package not built (set FRYWORLD_CSHARPEDITOR_PACKAGE or build FrySharp in Release).";
        }
    }
}

/// <summary>
/// Runs only when FrySharp is installed in this machine's FryWorld plugins directory, and reports Skipped otherwise.
/// </summary>
public sealed class InstalledCSharpEditorFactAttribute : FactAttribute
{
    public InstalledCSharpEditorFactAttribute()
    {
        if (TestPaths.InstalledPluginsDirectoryWithCSharpEditor is null)
        {
            Skip = "FrySharp is not installed in the FryWorld plugins directory.";
        }
    }
}
