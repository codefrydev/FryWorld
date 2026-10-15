using System;
using FryWorld.Sdk.Services;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Conveniences for plugins, on the context they receive in <see cref="IFryPlugin.ApplyAsync"/>.
/// </summary>
public static class FryPluginContextExtensions
{
    /// <summary>
    /// The settings of the plugin this context was given to, with defaults taken from its
    /// <see cref="IFryPlugin.SettingsSchema"/>. Uses the host's <see cref="ISettingsManager"/>, or an
    /// in-memory store when the host has none (so plugins also run in tests).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The context is not one a <see cref="PluginHost"/> passed to a plugin, so there is no plugin to bind to;
    /// use <see cref="GetPluginSettings(IFryPluginContext, string)"/> instead.
    /// </exception>
    public static IPluginSettings GetPluginSettings(this IFryPluginContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var plugin = FryPluginContext.GetOwningPlugin(ctx) ?? throw new InvalidOperationException(
            "This context does not belong to a mounted plugin. Call GetPluginSettings() on the context passed to " +
            "ApplyAsync, or pass the plugin id explicitly.");

        return CreateSettings(ctx, plugin.Id, plugin);
    }

    /// <summary>
    /// The settings of plugin <paramref name="pluginId"/>. Prefer <see cref="GetPluginSettings(IFryPluginContext)"/>
    /// inside a plugin, which also knows the plugin's schema defaults.
    /// </summary>
    public static IPluginSettings GetPluginSettings(this IFryPluginContext ctx, string pluginId)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var plugin = FryPluginContext.GetOwningPlugin(ctx);
        return CreateSettings(ctx, pluginId, plugin != null && string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase) ? plugin : null);
    }

    private static IPluginSettings CreateSettings(IFryPluginContext ctx, string pluginId, IFryPlugin? plugin)
    {
        var store = ctx.TryGetService<ISettingsManager>(out var hostStore) ? hostStore : FallbackStore.Value;
        var settings = new PluginSettings(store, pluginId, plugin?.SettingsSchema);

        // Unsubscribe from the store when the plugin unmounts, so the plugin can unload.
        ctx.RegisterEffect(settings.Dispose);
        return settings;
    }

    private static readonly Lazy<ISettingsManager> FallbackStore = new(() => new InMemorySettingsStore());
}
