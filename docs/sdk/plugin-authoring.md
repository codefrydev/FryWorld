# Writing a FryWorld plugin

From an empty folder to a published `.fryplugin`.

## 1. Create the projects

```
MyStudio/
  MyStudio.csproj          the plugin (class library)
  plugin.json
  MyStudioPlugin.cs
  Runner/MyStudio.Runner.csproj   optional: runs the plugin on its own
  Tests/MyStudio.Tests.csproj     optional: unit tests
```

`MyStudio.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <Version>1.0.0</Version>
    <DefaultItemExcludes>$(DefaultItemExcludes);Runner/**;Tests/**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FryWorld.Sdk" Version="0.1.0" />
    <!-- Your own dependencies are packed with the plugin; Avalonia etc. come from the host. -->
  </ItemGroup>
</Project>
```

You no longer need `PrivateAssets="all"` on FryWorld.Sdk, a hand-written `PackageFryPlugin` target, or a list of DLLs to exclude. The SDK's build targets do all of that.

`plugin.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/codefrydev/FryWorld/main/src/FryWorld.Sdk/plugin.schema.json",
  "id": "com.example.mystudio",
  "name": "My Studio",
  "description": "An example studio.",
  "author": "You"
}
```

Use a reverse-domain `id` and never change it: it identifies your plugin in the store, in settings and on disk. Leave `version` and `entryPoint` out, because the build fills them in.

## 2. Implement the plugin

Subclass `FryPluginBase` and register what you contribute in `ApplyAsync`; see the [README](../../src/FryWorld.Sdk/README.md#write-a-plugin). Guidelines:

- **Register everything through `ctx`.** Then unmounting removes it. For anything else (event subscriptions, timers, processes), use `ctx.RegisterEffect`.
- **Keep `ApplyAsync` fast.** It runs during host startup. Build views in `ViewFactory` and start heavy work lazily, or in a background task registered with `ctx.RegisterDisposable`.
- **Navigate through `IShellService`.** Never reach into the host's view models by reflection; the shell service works in every host and in the runner.
- **Read settings through `ctx.GetPluginSettings()`.** Declare them in `SettingsSchema` (or in `settingsSchema` in plugin.json) and the host generates a settings form for them.
- **Use the service contracts for cross-plugin dependencies.** List them in `RequiredServices` and `ProvidedServices` so the host mounts plugins in the right order.

## 3. Run it

`Runner/MyStudio.Runner.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\MyStudio.csproj" />
    <PackageReference Include="FryWorld.Sdk" Version="0.1.0" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.2" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.2" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.1.2" />
  </ItemGroup>
</Project>
```

`Program.cs`:

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

## 4. Test it

Reference FryWorld.Sdk and your plugin from a test project, then use `FryPluginTestHost`; see the [README](../../src/FryWorld.Sdk/README.md#test-it). End at least one test with `AssertCleanUnmountAsync()`, because a plugin that leaks registrations cannot be unloaded.

## 5. Package and publish

```bash
dotnet build -c Release
```

The package is `bin/Release/net10.0/MyStudio.fryplugin`. To try it in FryWorld, drop it into the plugins folder and restart:

- macOS: `~/Library/Application Support/FryWorld/plugins`
- Windows: `%LOCALAPPDATA%\FryWorld\plugins`
- Linux: `~/.config/FryWorld/plugins`

To publish it in the store, add a catalog entry that gives the package's `downloadUrl` and its `sha256`; FryWorld refuses downloads that don't match. Set `minHostVersion` if the plugin uses an SDK feature newer than some hosts in use.

## Running in FryPDF

FryPDF hosts FryWorld.Sdk plugins alongside its own, so the same `.fryplugin` runs there. Drop it into FryPDF's `fryworld-plugins` folder, beside its `plugins` folder.

- **Where it shows up:** pages, overlays, commands, sidebars, inspectors, dialogs, shortcuts and ribbon tabs appear in FryPDF's UI. Following FryPDF's own policy, status bar widgets from external plugins are not shown in the document editor.
- **Services:** `IShellService`, `ctx.GetPluginSettings()` and `IThemeService` behave as in FryWorld.
- **PDF features:** to add importers, exporters, templates, OCR engines, data connectors or canvas elements, reference `FryPdf.PluginSdk` too. Call its scoped `ctx.Register…` extensions (from `PdfEditorApp.Core.Plugins.FryWorldInterop`) after checking `ctx.IsFryPdfHost()`.

## Troubleshooting

When a plugin doesn't appear, the reason is in the host's diagnostics (`PluginHost.Diagnostics`, also written to the app log):

| Message | Cause |
|---|---|
| "requires service … but it was not provided" | A `RequiredServices` entry that no plugin or host provides. |
| "built against FryWorld.Sdk X but this host provides Y" | Rebuild against the SDK version the host ships. |
| "plugin.json says id A but the plugin's Id is B" | Make the manifest `id` and the code `Id` match. |
| "Requires FryWorld X or newer" | The host is older than the plugin's `minHostVersion`. |
| "Took N ms to mount" | Move slow work out of `ApplyAsync`. |
| "Another package … already provides this plugin" | Two `.fryplugin` files for the same id in the plugins folder. |
