using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Settings for a <see cref="PluginHost"/>.
/// </summary>
public sealed class PluginHostOptions
{
    /// <summary>
    /// Receives plugin lifecycle messages and every <see cref="PluginLoadDiagnostic"/>. Defaults to no logging.
    /// </summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Where the root context raises its registry change events (<c>NavigationItemsChanged</c> and the
    /// rest); see <see cref="FryPluginContext.NotificationContext"/>. Set it to the UI thread's context so
    /// handlers can update bound collections directly. Null (the default) raises them on whichever thread
    /// made the change.
    /// </summary>
    public SynchronizationContext? EventSynchronizationContext { get; set; }

    /// <summary>
    /// A plugin whose <see cref="IFryPlugin.ApplyAsync"/> takes longer than this is reported with a warning
    /// diagnostic, since it delays every plugin mounted after it. Defaults to 500 ms.
    /// </summary>
    public TimeSpan SlowMountThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How many plugins <see cref="PluginHost.StartAsync"/> may mount at the same time. Plugins are only
    /// mounted together when neither depends on the other. Defaults to 1, which mounts them one at a time
    /// on the calling thread; values above 1 run <see cref="IFryPlugin.ApplyAsync"/> on thread-pool threads.
    /// </summary>
    public int MountConcurrency { get; set; } = 1;
}
