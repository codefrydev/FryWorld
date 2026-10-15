# FryWorld.Sdk concepts

How the plugin runtime works, for plugin authors and for people building hosts.

## Lifecycle

A host keeps one `PluginHost`. Plugins move through these states:

```
Registered ──MountPluginAsync──▶ Mounting ──ApplyAsync ok──▶ Active ──UnmountPluginAsync──▶ Unmounting ──▶ Unloaded
                                     │
                                     └──ApplyAsync throws, or a RequiredService is missing──▶ Failed
```

- `StartAsync` mounts every registered plugin in dependency order: a plugin that lists a service in `RequiredServices` mounts after the plugin that lists it in `ProvidedServices`. If any required service is still missing when a plugin mounts, that plugin fails with `PluginMissingDependencyException` before its `ApplyAsync` runs.
- A plugin that fails is fully rolled back. Anything it registered before the exception is removed, so a broken plugin leaves nothing half-installed. It can be mounted again later.
- A plugin can be unmounted and mounted again; each mount gets a fresh scope.
- `PluginRegistrationEntry.MountDuration` records how long `ApplyAsync` took. Mounts slower than `PluginHostOptions.SlowMountThreshold` (500 ms by default) are reported, because they delay every plugin mounted after them.

## Scopes and effects

The context passed to `ApplyAsync` is scoped to the plugin. Everything registered through it is recorded in the plugin's `PluginScope`: services, pipeline handlers, descriptors and explicit `RegisterEffect` callbacks. On unmount the scope undoes them in reverse order. Each step runs even if an earlier one throws, and failures become diagnostics.

Every `Register*` call also returns an `IDisposable`. Disposing it removes that one registration early, and it is not undone a second time on unmount.

When two plugins register the same service contract, the later registration wins. If that plugin unmounts, the earlier registration becomes current again.

Registrations made around the context, for example straight on `ctx.Pipelines`, are not scoped. `FryPluginTestHost.AssertCleanUnmountAsync` finds these leaks.

## Registries

The host shell reads what plugins contribute from registries on the context: navigation items (pages), overlays, commands, status bar widgets, tools, ribbon tabs, sidebar tabs, inspector sections, shortcuts and dialogs.

- **Reads are free.** `GetRegistered*` returns an immutable, already-sorted snapshot; no lock is taken and nothing is allocated. The snapshot is rebuilt only when something registers or unregisters.
- **Change events are batched.** Each registry has a `…Changed` event. While a plugin mounts or unmounts, events are held back and each changed registry fires once afterwards. Hosts can do the same around their own batches with `FryPluginContext.DeferChangeNotifications()`.
- **Events can arrive on the UI thread.** Set `PluginHostOptions.EventSynchronizationContext`, or `FryPluginContext.NotificationContext`, to the UI thread's context.

## Pipelines

A pipeline is a named extension point. Handlers are stored per name and per context type, as arrays replaced on each change, so executing a pipeline is a single lookup.

- **Waterfall.** Middleware calls `next()` to continue the chain. A second call to `next()` is ignored and logged, so a buggy middleware cannot run the rest of the chain twice.
- **Bail.** The first non-null result wins.
- **Parallel.** Every handler starts at once. If they all complete synchronously, no `Task.WhenAll` is allocated.
- **Serial.** Handlers run one after another.

A handler only runs when the pipeline is executed with the context type it was registered for. Mixing context types under one name is reported. `PipelineKey<TContext>` turns that mistake into a compile error.

## Isolation and the shared-assembly list

Each plugin assembly loads into its own `CollectiblePluginLoadContext`. Assemblies resolve in three stages:

1. **Contract.** `FryWorld.Sdk` (and `FryWorld`) always come from the host, so `IFryPlugin` is the same type on both sides.
2. **The plugin's own dependencies.** Anything the plugin ships, resolved through its `.deps.json` or its folder, wins.
3. **Shared frameworks.** If the plugin does not ship them, Avalonia, SkiaSharp, CommunityToolkit.Mvvm, Material.Icons, and the Microsoft.Extensions DI and Logging abstractions come from the host.

The shared frameworks are listed once, in `build/FryWorld.Sdk.HostAssemblies.txt`. The loader reads that list at run time (it is embedded in the assembly), and the build targets read the same file to leave those assemblies out of `.fryplugin` packages. The two can therefore never disagree, and plugins never ship a second copy of a UI type.

A plugin's load context is collectible, and its assemblies are freed after unmount and `PluginAssemblyPackage.Dispose()`. That holds only while nothing else references the plugin's types: no view still on screen, no event handler still subscribed. Plugins that cannot unload (Roslyn scripting, for example) set `"isCollectible": false` in plugin.json.

If a plugin was built against a version of a host assembly that the host does not have, the runtime reports a bare "file not found". The loader translates that into a `PluginAbiMismatchException` that names the assembly and both versions.

## On disk

```
plugins/
  com.example.studio/          one folder per plugin id
    1.2.0/                     one folder per installed version; the highest is loaded
      plugin.json  *.dll  .fryinstall
    1.1.0/.remove              superseded: skipped, deleted at next start
  com.example.old/.uninstall   uninstalled: skipped, deleted at next start
  Sideloaded.fryplugin         packages dropped here are extracted on start
```

- `.fryinstall` records which package a folder was extracted from, so a package is extracted once rather than on every launch.
- Marker files defer the removal of files that are in use, which happens on Windows and with non-collectible plugins. Updates and uninstalls therefore never fail because a plugin is loaded.

## Threading

- `ApplyAsync` runs on whichever thread called `StartAsync` or `MountPluginAsync`. `PluginStartup` calls them from a thread-pool thread, so the UI stays responsive. Create views in view factories, which the host calls on the UI thread, not in `ApplyAsync`.
- `IShellService` methods can be called from any thread; the host marshals the UI work.
- With `PluginHostOptions.MountConcurrency` above 1, plugins that don't depend on each other mount concurrently on thread-pool threads. Registries, services and pipelines are all thread-safe.

## Diagnostics

Problems are recorded as `PluginLoadDiagnostic`s rather than swallowed. Each one carries a stage, a severity, a message, a plugin id, a path and an exception. The loader adds them to the collection you pass in. `PluginHost` keeps them in `Diagnostics` and raises `DiagnosticReported`. `PluginStartup` forwards everything into the host, so a host has one list to show.
