using System.Collections.Generic;

namespace FryWorld.Sdk.Profiles;

/// <summary>
/// A collection of related plugins packaged as a logical bundle.
/// </summary>
public interface IFryPluginBundle
{
    string BundleId { get; }
    string Name { get; }
    IReadOnlyList<Plugins.IFryPlugin> Plugins { get; }
}
