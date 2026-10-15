using System;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Base exception for all plugin-related runtime errors.
/// </summary>
public class PluginException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public PluginException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and the error that caused it.</summary>
    public PluginException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a plugin requires a service contract that no mounted plugin and no host service provides.
/// </summary>
public class PluginMissingDependencyException : PluginException
{
    /// <summary>The plugin that could not mount.</summary>
    public string PluginId { get; }

    /// <summary>The service the plugin declared in <see cref="IFryPlugin.RequiredServices"/> but nothing provides.</summary>
    public Type MissingServiceType { get; }

    /// <summary>Creates the exception for <paramref name="pluginId"/> missing <paramref name="missingServiceType"/>.</summary>
    public PluginMissingDependencyException(string pluginId, Type missingServiceType)
        : base($"Plugin '{pluginId}' requires service '{missingServiceType.FullName}', but it was not provided by any active plugin or host service.")
    {
        PluginId = pluginId;
        MissingServiceType = missingServiceType;
    }
}

/// <summary>
/// Thrown when plugins' service requirements form a cycle, so no mount order satisfies all of them.
/// </summary>
public class PluginCircularDependencyException : PluginException
{
    /// <summary>Creates the exception with a message naming the plugins in the cycle.</summary>
    public PluginCircularDependencyException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a plugin was compiled against a version of a host-provided assembly that this host does not
/// provide. The runtime reports this as a bare <see cref="System.IO.FileNotFoundException"/> or
/// <see cref="System.IO.FileLoadException"/> even when the file is on disk, so the loader translates it into
/// this type to keep the cause actionable.
/// </summary>
public class PluginAbiMismatchException : PluginException
{
    /// <summary>The host-provided assembly the plugin asked for, e.g. <c>FryWorld.Sdk</c> or <c>Avalonia.Base</c>.</summary>
    public string AssemblyName { get; }

    /// <summary>The version the plugin was compiled against, when known.</summary>
    public Version? RequestedVersion { get; }

    /// <summary>The version this host provides, or null when the host does not provide the assembly at all.</summary>
    public Version? HostVersion { get; }

    /// <summary>Creates the exception for a plugin built against a different version of <paramref name="assemblyName"/>.</summary>
    public PluginAbiMismatchException(
        string assemblyName, Version? requestedVersion, Version? hostVersion, Exception innerException)
        : base($"This plugin was built against {assemblyName} " +
               $"{requestedVersion?.ToString() ?? "an unknown version"} but this host provides " +
               $"{hostVersion?.ToString() ?? "no matching version"}. Rebuild the plugin against the installed host.",
               innerException)
    {
        AssemblyName = assemblyName;
        RequestedVersion = requestedVersion;
        HostVersion = hostVersion;
    }
}
