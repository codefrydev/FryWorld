using System;
using FryWorld.Sdk.Hosting;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// In the DI namespace, like other AddXxx extensions, so it is found without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the FryWorld plugin runtime in a host's service container.
/// </summary>
public static class FryWorldServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="PluginHost"/> (whose context falls back to this container for services, so
    /// plugins can resolve host services), its <see cref="IFryPluginContext"/>, and, when
    /// <see cref="FryWorldHostOptions.PluginsDirectory"/> is set, <see cref="IPluginMarketplaceService"/> and
    /// <see cref="PluginStartup"/>. Registrations the host made earlier win.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddFryWorldPluginHost(o =>
    /// {
    ///     o.PluginsDirectory = Path.Combine(appData, "plugins");
    ///     o.SettingsFilePath = Path.Combine(appData, "settings.json");
    ///     o.Host.EventSynchronizationContext = uiSynchronizationContext;
    /// });
    /// // after the main window is created:
    /// provider.GetRequiredService&lt;PluginStartup&gt;().Start();
    /// </code>
    /// </example>
    public static IServiceCollection AddFryWorldPluginHost(this IServiceCollection services, Action<FryWorldHostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FryWorldHostOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton(sp =>
        {
            // Use the container's logging when the host has not chosen a logger.
            if (options.Host.Logger is NullLogger && sp.GetService<ILoggerFactory>() is { } loggerFactory)
            {
                options.Host.Logger = loggerFactory.CreateLogger("FryWorld.Plugins");
            }

            return new PluginHost(new FryPluginContext(fallbackServiceProvider: sp), options.Host);
        });
        services.TryAddSingleton<IFryPluginContext>(sp => sp.GetRequiredService<PluginHost>().Context);

        if (options.SettingsFilePath != null)
        {
            services.TryAddSingleton<ISettingsManager>(_ => new FileSettingsStore(options.SettingsFilePath));
        }
        else
        {
            services.TryAddSingleton<ISettingsManager, InMemorySettingsStore>();
        }

        if (options.PluginsDirectory != null)
        {
            services.TryAddSingleton<IPluginMarketplaceService>(sp =>
                new PluginMarketplaceService(sp.GetRequiredService<PluginHost>(), options.PluginsDirectory, new PluginMarketplaceOptions
                {
                    CatalogUrl = options.CatalogUrl,
                    HostVersion = options.HostVersion,
                    Logger = options.Host.Logger
                }));
            services.TryAddSingleton(sp => new PluginStartup(sp.GetRequiredService<PluginHost>(), options.PluginsDirectory, options.HostVersion));
        }

        return services;
    }
}
