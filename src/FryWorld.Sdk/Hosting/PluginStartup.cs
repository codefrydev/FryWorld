using System;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Loading;
using FryWorld.Sdk.Marketplace;
using FryWorld.Sdk.Plugins;

namespace FryWorld.Sdk.Hosting;

/// <summary>
/// Loads and mounts the plugins installed in a directory, in the background, so a host's window can appear
/// without waiting for packages to unpack and assemblies to load. Everything that goes wrong lands in
/// <see cref="PluginHost.Diagnostics"/>.
/// </summary>
public sealed class PluginStartup
{
    private readonly PluginHost _host;
    private readonly string _pluginsDirectory;
    private readonly Version? _hostVersion;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    /// <summary>Prepares to load the plugins in <paramref name="pluginsDirectory"/> into <paramref name="host"/>.</summary>
    /// <param name="host">The host to mount them in.</param>
    /// <param name="pluginsDirectory">Where plugins are installed.</param>
    /// <param name="hostVersion">
    /// This host's version; plugins whose manifest requires another are skipped with an error diagnostic.
    /// <see cref="PluginCompatibility.DefaultHostVersion"/> when null.
    /// </param>
    public PluginStartup(PluginHost host, string pluginsDirectory, Version? hostVersion = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);
        _pluginsDirectory = pluginsDirectory;
        _hostVersion = hostVersion;
    }

    /// <summary>
    /// Completes when every installed plugin has been loaded and mounted, or has failed to. Never faults.
    /// Await it before looking a plugin up by id, so a plugin that is still loading is not missed.
    /// </summary>
    public Task Completion => _completion.Task;

    /// <summary>Whether <see cref="Start"/> has been called.</summary>
    public bool HasStarted => Volatile.Read(ref _started) != 0;

    /// <summary>Starts loading on a background thread. Calling it again does nothing.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var diagnostics = new System.Collections.Generic.List<PluginLoadDiagnostic>();
        try
        {
            var packages = await PluginAssemblyLoader.DiscoverAndLoadDirectoryAsync(_pluginsDirectory, diagnostics)
                .ConfigureAwait(false);

            foreach (var diagnostic in diagnostics)
            {
                _host.ReportDiagnostic(diagnostic);
            }

            foreach (var package in packages)
            {
                if (package.Manifest != null)
                {
                    var compatibility = PluginCompatibility.Check(package.Manifest, _hostVersion);
                    if (!compatibility.IsCompatible)
                    {
                        _host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.AssemblyLoad, PluginDiagnosticSeverity.Error,
                            $"Not loaded: {compatibility.Message}", PluginId: package.Manifest.Id, Path: package.AssemblyPath,
                            Exception: new PluginIncompatibleException(package.Manifest.Id, compatibility)));
                        package.Dispose();
                        continue;
                    }
                }

                foreach (var plugin in package.Plugins)
                {
                    try
                    {
                        _host.RegisterPlugin(plugin);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.Discovery, PluginDiagnosticSeverity.Warning,
                            ex.Message, PluginId: plugin.Id, Path: package.AssemblyPath, Exception: ex));
                    }
                }
            }

            // Still off the caller's thread, thanks to ConfigureAwait(false) above.
            await _host.StartAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _host.ReportDiagnostic(new PluginLoadDiagnostic(PluginLoadStage.Discovery, PluginDiagnosticSeverity.Error,
                $"Loading installed plugins failed: {ex.Message}", Path: _pluginsDirectory, Exception: ex));
        }
        finally
        {
            _completion.TrySetResult();
        }
    }
}
