using System.Collections.Generic;

namespace FryWorld.Sdk.Profiles;

/// <summary>
/// A collection of related plugins packaged as a logical bundle.
/// </summary>
public interface IFryPluginBundle
{
    /// <summary>Unique id, used to enable or disable the bundle in a profile.</summary>
    string BundleId { get; }
    /// <summary>Display name.</summary>
    string Name { get; }
    /// <summary>The plugins in the bundle.</summary>
    IReadOnlyList<Plugins.IFryPlugin> Plugins { get; }
}
