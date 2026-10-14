using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Models;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Core contract implemented by all FryWorld plugins and extensions.
/// Inspired by the Cordis architecture: capabilities mount reversibly into an active context.
/// </summary>
public interface IFryPlugin
{
    /// <summary>
    /// Unique identifier for this plugin (e.g. "com.codefry.csharpstudio", "com.codefry.snake").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// User-friendly display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version of the plugin.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Service contracts required by this plugin before it can mount (declarative injection graph).
    /// </summary>
    IReadOnlyList<Type> RequiredServices { get; }

    /// <summary>
    /// Service contracts provided by this plugin (used for DAG dependency graph sorting).
    /// </summary>
    IReadOnlyList<Type> ProvidedServices => Array.Empty<Type>();

    /// <summary>
    /// Declarative configuration schema describing configurable user settings for this plugin.
    /// </summary>
    IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => null;

    /// <summary>
    /// Mounts the plugin into the active context, registering services, pipelines, views, and reversible effects.
    /// </summary>
    Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default);
}
