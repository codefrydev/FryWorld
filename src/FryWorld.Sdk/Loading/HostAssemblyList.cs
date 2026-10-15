using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FryWorld.Sdk.Loading;

/// <summary>
/// The assemblies every host provides to plugins, read from build/FryWorld.Sdk.HostAssemblies.txt (embedded
/// in this assembly). The build targets use the same file to leave these assemblies out of plugin packages.
/// </summary>
internal static class HostAssemblyList
{
    private const string ResourceName = "FryWorld.Sdk.HostAssemblies.txt";

    private static readonly Lazy<(HashSet<string> Exact, string[] Prefixes)> Patterns = new(Load);

    /// <summary>Whether <paramref name="assemblyName"/> is on the list (exact name or a <c>Prefix*</c> entry).</summary>
    public static bool Matches(string assemblyName)
    {
        var (exact, prefixes) = Patterns.Value;
        if (exact.Contains(assemblyName)) return true;

        foreach (var prefix in prefixes)
        {
            if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static (HashSet<string>, string[]) Load()
    {
        using var stream = typeof(HostAssemblyList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing from FryWorld.Sdk.");
        using var reader = new StreamReader(stream);

        var entries = reader.ReadToEnd()
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        var exact = new HashSet<string>(entries.Where(e => !e.EndsWith('*')), StringComparer.OrdinalIgnoreCase);
        var prefixes = entries.Where(e => e.EndsWith('*')).Select(e => e[..^1]).ToArray();
        return (exact, prefixes);
    }
}
