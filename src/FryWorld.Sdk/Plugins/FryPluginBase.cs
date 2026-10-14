using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Models;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Convenient abstract base class for creating plugins for FryWorld.
/// </summary>
public abstract class FryPluginBase : IFryPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public virtual Version Version => new(1, 0, 0);

    public virtual IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
    public virtual IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();
    public virtual IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => null;

    public abstract Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default);
}
