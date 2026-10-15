# FryWorld.Sdk

[![NuGet](https://img.shields.io/nuget/v/FryWorld.Sdk.svg)](https://www.nuget.org/packages/FryWorld.Sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

The plugin SDK for FryWorld and every app built on it. One package gives you:

- **The plugin contract**: `IFryPlugin`, a scoped context where everything you register is undone when your plugin unloads, typed pipelines, and registries for pages, overlays, commands, status bar widgets and more.
- **Packaging**: a Release build of your plugin produces a ready-to-ship `.fryplugin`, with no packaging script.
- **A standalone runner**: run your plugin as its own desktop app in about five lines.
- **A test host**: mount your plugin in a unit test and assert that it cleans up after itself.
- **The hosting runtime**: isolated, unloadable plugin loading, a marketplace with verified downloads, and diagnostics for anything that goes wrong.

## Install

```bash
dotnet add package FryWorld.Sdk
```

The host provides FryWorld.Sdk, Avalonia and the other shared frameworks at run time, and the build leaves them out of your package automatically.

## Write a plugin

A plugin is a class library with a `plugin.json` next to its project file:

```json
{
  "$schema": "https://raw.githubusercontent.com/codefrydev/FryWorld/main/src/FryWorld.Sdk/plugin.schema.json",
  "id": "com.example.mystudio",
  "name": "My Studio",
  "description": "An example studio."
}
```

`version`, `entryPoint`, `sdkVersion` and `targetFramework` are filled in by the build, so `<Version>` in the project file is the only place a version lives. The `$schema` line gives you completion and validation in VS Code and Rider.

```csharp
public sealed class MyStudioPlugin : FryPluginBase
{
    public override string Id => "com.example.mystudio";
    public override string Name => "My Studio";

    public override IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => new Dictionary<string, PluginSettingDefinition>
    {
        ["FontSize"] = new() { Type = PluginSettingType.Integer, Label = "Font size", DefaultValue = 14 }
    };

    public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        var settings = ctx.GetPluginSettings();
        var shell = ctx.GetService<IShellService>();

        // A full-viewport page in the host's navigation rail
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "mystudio.page",
            Title = "My Studio",
            IconKind = "Wrench",
            ViewFactory = () => new TextBlock { Text = "Hello", FontSize = settings.Get<int>("FontSize") }
        });

        // A floating overlay, opened on demand
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = "mystudio.notes",
            Title = "Notes",
            ViewFactory = () => new TextBox { AcceptsReturn = true }
        });

        // A command palette entry (⌘K / Ctrl+K) that opens it
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "mystudio.open-notes",
            Title = "Open Notes",
            Command = new RelayCommand(() => shell.ShowOverlay("mystudio.notes"))
        });

        // Everything registered through ctx is removed automatically when the plugin unmounts.
        return Task.CompletedTask;
    }
}
```

What the context gives you:

| Need | Use |
|---|---|
| Open your page or overlay, or show a dialog | `ctx.GetService<IShellService>()`: `NavigateTo`, `ShowOverlay`, `HideOverlay`, `ToggleOverlay`, `ShowDialogAsync` |
| Read and write your settings | `ctx.GetPluginSettings()`: `Get<T>` (falls back to your schema's default), `Set<T>`, and `Changed`, which also fires when the user edits a setting in the host |
| Show a message | `ctx.GetService<INotificationService>()` |
| Share a service with other plugins | `ctx.RegisterService<IMyService>(impl)`; declare it in `ProvidedServices`, and consumers list it in `RequiredServices` so they mount after you |
| Clean up something the context doesn't know about | `ctx.RegisterEffect(() => ...)` or `ctx.RegisterDisposable(x)` |
| Extension points | Pipelines; see below |

## Package it

```bash
dotnet build -c Release
```

This produces `bin/Release/net10.0/<AssemblyName>.fryplugin`, containing your assembly, every dependency it publishes (NuGet packages, project references, native `runtimes/` files, satellite assemblies, content), its `.deps.json`, and the stamped `plugin.json`. Host-provided assemblies are left out. For a Debug package, run `dotnet build -t:FryPluginPackage`.

Optional settings:

| Property / item | Effect |
|---|---|
| `FryPluginPackageName` | Package file name without extension (default: `$(AssemblyName)`) |
| `FryPluginPackageOutputPath` | Full output path of the package |
| `FryPluginPackageOnBuild` | `true`/`false` to package on every build (default: Release only) |
| `FryPluginPackageEnabled` | `false` to turn packaging off |
| `FryPluginIncludeSymbols`, `FryPluginIncludeDocumentation` | Include `.pdb` / `.xml` files |
| `<FryPluginContent Include="…" PackagePath="…" />` | Add extra files |
| `<FryPluginExclude Include="Foo.dll" />` | Leave a file out |

The build reports `FRY001` for an invalid `id`, `FRY002` when `entryPoint` names another assembly, and `FRY003` when `plugin.json`'s version disagrees with `<Version>`.

## Run it on its own

A runner project references your plugin, `Avalonia.Desktop` and a theme. Its whole `Program.cs` is:

```csharp
public static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        FryRunner.Configure<MyStudioPlugin>(app => app.Styles.Add(new FluentTheme()))
            .UsePlatformDetect()
            .WithInterFont()
            .StartWithClassicDesktopLifetime(args);
}
```

Pages become tabs, and overlays open as windows (a plugin with only an overlay, like a game, gets it as the main window). Commands are in a Commands menu, and status bar widgets run along the bottom. The shell, settings (saved to `<plugin id>.runner.settings.json`), notification and theme services behave as they do in the host. If the plugin fails to mount, the runner shows the error and diagnostics instead of a blank window.

## Test it

```csharp
public class MyStudioPluginTests
{
    [Fact]
    public async Task OpenNotes_ShowsTheNotesOverlay()
    {
        await using var host = await FryPluginTestHost.MountAsync<MyStudioPlugin>();

        await host.RunCommandAsync("mystudio.open-notes");

        Assert.Contains("mystudio.notes", host.Shell.OpenOverlays);
        await host.AssertCleanUnmountAsync(); // fails if anything outlives the plugin
    }
}
```

`FryPluginTestHost` needs no UI platform. It provides recording shell and notification services and in-memory settings. `AssertCleanUnmountAsync` lists every registration, service or pipeline handler your plugin left behind; any of those would keep its assemblies in memory after the host unloads it.

## Pipelines

Pipelines are named extension points that plugins hook into:

| Mode | Behaviour | Typical use |
|---|---|---|
| Waterfall | Around-middleware; each handler calls `next()` | Export filters, view transforms |
| Bail | First non-null result wins | File importers, converters |
| Parallel | All handlers at once | Telemetry, background sync |
| Serial | One after another | Validation, ordered checks |

Declare a `PipelineKey<TContext>` in a contract assembly shared by the plugins that use the pipeline. The compiler then rejects a handler written for the wrong context type, a mistake the string-named overloads let through silently:

```csharp
public static readonly PipelineKey<ExportContext> BeforeExport = new("export.before");

ctx.RegisterSerial(BeforeExport, e => { ... });
await ctx.ExecuteSerialAsync(BeforeExport, exportContext);
```

## Host plugins

```csharp
services.AddFryWorldPluginHost(o =>
{
    o.PluginsDirectory = Path.Combine(appData, "plugins");
    o.SettingsFilePath = Path.Combine(appData, "settings.json");
    o.Host.EventSynchronizationContext = uiSynchronizationContext; // registry events on the UI thread
});

// After the main window is created, so it appears immediately:
provider.GetRequiredService<PluginStartup>().Start();
```

What the hosting runtime does:

- **Loading:** each plugin gets its own load context and can be unloaded unless its manifest says `"isCollectible": false`. Packages are extracted once rather than on every launch.
- **Compatibility:** plugins whose `minHostVersion`, `maxHostVersion` or `targetFramework` don't fit the host are refused, with the reason.
- **Marketplace:** downloads are checked against the catalog's SHA-256. Updates unload the old version, and uninstalls defer deleting files that are in use until the next start.
- **Diagnostics:** everything that goes wrong (a corrupt package, a missing dependency, a plugin built against another SDK version, a slow mount) is recorded in `PluginHost.Diagnostics`, so you can show users why a plugin is missing.

## Compatibility

Plugins built against any earlier 0.x release keep compiling and loading, and every change is additive. `dotnet pack` enforces this against the last published version. The assembly version stays `1.0.0.0`, so built plugins never need rebinding. Obsolete APIs are removed only in a major release. See [docs/sdk/compatibility.md](https://github.com/codefrydev/FryWorld/blob/main/docs/sdk/compatibility.md).

## More

- [Concepts](https://github.com/codefrydev/FryWorld/blob/main/docs/sdk/concepts.md): lifecycle, scopes, registries, pipelines, isolation, threading
- [Plugin authoring guide](https://github.com/codefrydev/FryWorld/blob/main/docs/sdk/plugin-authoring.md)
- [Changelog](https://github.com/codefrydev/FryWorld/blob/main/src/FryWorld.Sdk/CHANGELOG.md)

## License

MIT. Copyright © 2026 Code Fry Dev.
