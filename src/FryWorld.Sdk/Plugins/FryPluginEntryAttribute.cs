using System;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Names the plugin types an assembly exposes, so the loader can create them without scanning every public
/// type in the assembly. Optional: without it, every public, non-abstract <see cref="IFryPlugin"/> type is used.
/// </summary>
/// <example>
/// <code>[assembly: FryPluginEntry(typeof(MyStudioPlugin))]</code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FryPluginEntryAttribute : Attribute
{
    /// <summary>Declares <paramref name="pluginType"/> as one of the assembly's plugins.</summary>
    public FryPluginEntryAttribute(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        PluginType = pluginType;
    }

    /// <summary>A public, non-abstract type implementing <see cref="IFryPlugin"/> with a parameterless constructor.</summary>
    public Type PluginType { get; }
}
