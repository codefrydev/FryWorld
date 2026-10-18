# Changelog

All notable changes to FryWorld.Sdk. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/) under the [compatibility policy](../../docs/sdk/compatibility.md): every 0.x release is additive, so plugins built against 0.0.2 keep compiling and loading.

## [0.3.0] - 2026-10-19

### Added

- **Universal Shortcut & Keybinding Subsystem:** `FryWorld.Sdk.Shortcuts` providing production-grade, extensible, and context-aware keyboard shortcut routing.
- **Dynamic Shortcut Registry:** `IShortcutRegistry` and `ShortcutRegistry` providing thread-safe, lock-free snapshot cataloging of keyboard bindings with 4-tier precedence (`HostDefault`, `PluginDefault`, `KeymapExtension`, `UserOverride`).
- **Context-Aware Scoping:** `ShortcutScope` hierarchy (`Global`, `Plugin`, `Overlay`, `Focus`) ensuring scoped hotkeys only fire when their designated workspace or overlay is active.
- **Focus & Input Shielding:** `ShortcutManager.IsInputShielded` prevents single-letter shortcuts from hijacking text input when keyboard focus is inside editable text presenters (`TextBox`, `AutoCompleteBox`, `TextEditor`, `NumericUpDown`).
- **Cross-Platform Key Gestures & Chords:** `PlatformKeyGesture` and `KeyStroke` supporting modifier aliases (`"Mod"`, `"CmdOrCtrl"`, `"Cmd"`, `"Ctrl"`, `"Alt"`, `"Shift"`, `"Meta"`), punctuation keys (`","`, `"+"`), and multi-key chord sequences (`"Mod+K, Mod+C"`).
- **Decoupled Command Handlers:** `IShortcutManager.RegisterCommandHandler` allowing plugins to declare bindings upfront with semantic command IDs and dynamically bind execution delegates at viewmodel runtime with automatic `IDisposable` rollback.
- **Plugin Context Integration:** `IFryPluginContext.Shortcuts` and `RegisterShortcut(ShortcutBinding)` with automatic scoped unmounting on plugin teardown.

## [0.2.0] - 2026-10-15

### Added

- **Perceptual Color Science Engine:** `FryWorld.Sdk.Theming.ColorMath` featuring Material 3 HCT (Hue, Chroma, Tone) CAM16 appearance modeling, full Oklab and OKLCH polar color spaces with gamut mapping, APCA 0.98G contrast solver, WCAG 2.1 AA/AAA contrast solver, and Brettel/Viénot color blindness simulator (`Deuteranopia`, `Protanopia`, `Tritanopia`).
- **11-Stop Tonal Scale Engine:** `TonalScaleEngine` synthesizing standard 11-stop tonal ramps (`50`, `100`, `200`, `300`, `400`, `500`, `600`, `700`, `800`, `900`, `950`) across all semantic roles (`Primary`, `Secondary`, `Tertiary`, `Neutral`, `NeutralVariant`, `Success`, `Warning`, `Error`, `Info`).
- **Procedural Color Harmony Studio:** `HarmonicColorGenerator` synthesizing mathematically balanced palettes using standard wheel angles (`Analogous`, `Complementary`, `SplitComplementary`, `Triadic`, `Tetradic`, `Monochromatic`) and `ApplyRandomHarmonicTheme`.
- **Universal Theming Contract:** `IThemeManager : IThemeService` providing `ActiveThemeId`, `ActiveTheme`, `GetAvailableThemes()`, `ApplyTheme(string)`, `RegisterTheme(ThemeDefinition)`, `ApplyHarmonicTheme(float, ColorHarmonyMode, bool)`, and `ResetToDefaults()`.
- **Extensible Token Architecture:** `IThemeTokenContributor` allows plugins to contribute custom domain-specific tokens (e.g. `DeveloperStudioTokenContributor` for `Ds...` editor syntax/chrome tokens, `PhotoStudioTokenContributor` for `Ps...` photo studio tokens) without bloating the host runtime dictionary.
- **Dynamic Resource Root Tracking:** `IThemeManager.TrackResourceRoot(StyledElement)` synchronizes dynamic theme layers directly into window resource dictionaries for immediate single-pass recoloring without resource shadowing.
- **Curated Theme Presets:** `BuiltInThemes` shipping `DarkPlus`, `LightPlus`, `Dracula Pro`, `Cyberpunk Neon`, `Monokai Classic`, `One Dark Pro`, and `Nord Arctic`.

## [0.1.0] - 2026-10-15

### Added

- **Packaging:** `build/FryWorld.Sdk.targets` turns a Release build of any library with a `plugin.json` into a `.fryplugin`. The package holds the plugin's publish closure minus host-provided assemblies, plus a `plugin.json` stamped with `version`, `entryPoint`, `sdkVersion` and `targetFramework`. Build checks FRY001–FRY003 flag manifest problems.
- **Manifest schema:** `plugin.schema.json` gives editors completion and validation for plugin.json. New optional fields: `entryType`, `minHostVersion`, `maxHostVersion`, `sdkVersion`, `targetFramework`.
- **Shell service:** `IShellService` (`NavigateTo`, `ShowOverlay`, `HideOverlay`, `ToggleOverlay`, `IsOverlayVisible`, `ShowDialogAsync`), so plugins drive the shell without reflecting into host view models.
- **Plugin settings:** `IPluginSettings` through `ctx.GetPluginSettings()`, with schema defaults and change notifications. `ISettingsManager.SettingChanged` (default interface member). `FileSettingsStore`, with debounced atomic writes in the host's file format, and `InMemorySettingsStore`.
- **Standalone runner:** `FryRunner.Configure<TPlugin>()` runs any plugin as its own app.
- **Test host:** `FryPluginTestHost` with `RunCommandAsync`, `AssertCleanUnmountAsync` (throws `FryPluginLeakException`), `RecordingShellService` and `RecordingNotificationService`.
- **Hosting:** `services.AddFryWorldPluginHost(...)`, `FryWorldHostOptions`, and `PluginStartup` for background loading.
- **Host options:** `PluginHostOptions`, covering logger, event synchronization context, slow-mount threshold and mount concurrency.
- **Diagnostics:** `PluginLoadDiagnostic`, exposed through `PluginHost.Diagnostics`, `DiagnosticReported`, `ReportDiagnostic`, `GetRegistration` and `PluginRegistrationEntry.MountDuration`. The loader gains overloads that take a diagnostics collection, and `DiscoverAndLoadDirectoryAsync`.
- **Exceptions:** `PluginException`, `PluginMissingDependencyException`, `PluginCircularDependencyException`, `PluginAbiMismatchException`, `PluginIncompatibleException`.
- **Typed pipelines:** `PipelineKey<TContext>` and `PipelineKey<TContext, TResult>`, with typed overloads on `IPipelineManager` and `IFryPluginContext`. `PipelineManager(ILogger)`.
- **Entry types:** `[assembly: FryPluginEntry(typeof(...))]` and the manifest `entryType` skip assembly scanning.
- **Context:** `FryPluginContext.DeferChangeNotifications()` and `FryPluginContext.NotificationContext`.
- **Marketplace:** `PluginMarketplaceOptions` (injectable `HttpClient`, `HostVersion`, `RequireChecksum`, logger). `GetInstalledVersion`, `CheckCompatibility`, `PluginCompatibility`, `PluginVersion` (SemVer 2.0 ordering) and `MarketplacePluginVersion.MaxHostVersion`.
- **Loader helpers:** `PluginInstallLayout`, `FryPluginPackageLoader.ReadManifest`, `PluginAssemblyLoader.TryGetPackage` and `PluginAssemblyPackage.Manifest`.
- **Logging:** `AppLogService.CreateLogger` and `AppLogLoggerProvider` bridge `ILogger` into the app log.

### Changed

- **Defaults:** `FryPluginBase.Version` now defaults to the plugin assembly's version instead of `1.0.0`.
- **Required services:** `RequiredServices` is now enforced. A plugin whose required service is missing fails with `PluginMissingDependencyException` instead of failing later.
- **Startup speed:** packages are extracted once (tracked by a `.fryinstall` stamp) instead of on every launch. Warm discovery of 10 plugins went from 22.7 ms to 5.6 ms.
- **Registry reads:** these are lock-free and allocation-free (709 ns and 1.5 KB down to about 0 ns and 0 B for 50 items). Change events are batched per mount.
- **Pipeline dispatch:** about 5–7× faster. Serial went from 314 ns to 42 ns, and Bail from 331 ns to 60 ns with no allocation.
- **Version folders:** the highest version folder is loaded. Duplicate packages for one plugin are skipped with a diagnostic.
- **Entry assembly:** when plugin.json names no entry point, the loader picks the assembly that references FryWorld.Sdk instead of the first DLL listed.
- **Plugin ids:** ids must start and end with a letter or digit.
- **Installs:** installing a plugin that is already loaded replaces it (unmount, unload, mount the new version). Before, the new version silently never mounted.

### Fixed

- **Unloading:** collectible plugin load contexts can be unloaded. The loader used to keep every package alive forever.
- **Failed mounts:** a plugin that fails to mount no longer leaves the registrations it made before failing.
- **Effects:** disposing a registration handle early no longer makes its effect run again on unmount.
- **Overridden services:** a plugin unmounting no longer removes a service another plugin registered later, and an overridden service is restored when the override goes away.
- **Teardown order:** host teardown runs in reverse mount order.
- **Uninstall:** it no longer accepts ids that escape the plugins folder (`..`, `.`). It now unloads the plugin's assemblies and deletes its sideloaded `.fryplugin`, which used to reinstall it on the next start.
- **Downloads:** they are verified against the catalog's SHA-256, written atomically, and refuse ids or versions that would escape the download folder.
- **Pipelines:** a waterfall middleware calling `next()` twice no longer re-runs the rest of the chain. Handlers registered under one name with different context types are reported instead of skipped silently.
- **Load failures:** failures for host assembly versions are reported as `PluginAbiMismatchException` instead of a bare "file not found".
- **Archives:** packages zipped with a top-level folder now load.

## [0.0.2]

First public release.
