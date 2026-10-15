using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Models;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Convenient abstract base class for creating plugins for FryWorld.
/// </summary>
public abstract class FryPluginBase : IFryPlugin
{
    private Version? _version;

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// The plugin's version. Defaults to the plugin assembly's version (the project's <c>Version</c>
    /// property), so the version is set in one place; prerelease and build metadata are dropped.
    /// </summary>
    public virtual Version Version => _version ??= ReadAssemblyVersion(GetType().Assembly);

    /// <inheritdoc />
    public virtual IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    /// <inheritdoc />
    public virtual IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();

    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => null;

    /// <inheritdoc />
    public abstract Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default);

    private static Version ReadAssemblyVersion(Assembly assembly)
    {
        // "2.0.5-beta.1+abc123" -> 2.0.5
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var end = informational.IndexOfAny(new[] { '-', '+' });
            var numeric = end > 0 ? informational[..end] : informational;
            if (Version.TryParse(numeric, out var parsed)) return parsed;
        }

        return assembly.GetName().Version is { } assemblyVersion
            ? new Version(assemblyVersion.Major, assemblyVersion.Minor, Math.Max(assemblyVersion.Build, 0))
            : new Version(1, 0, 0);
    }
}
