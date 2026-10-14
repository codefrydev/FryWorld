# Headless UI Testing & Visual Snapshots in FryWorld

FryWorld includes a complete **Headless UI Testing** framework and an off-screen **Visual Snapshot Tool (`tools/UiSnapshots`)**.
These tools allow developers and autonomous AI agents to test, audit, and visually inspect the entire user interface without a physical display or window manager.

---

## 1. Automated Headless Tests (`tests/FryWorld.Tests`)

Automated headless tests run as standard xUnit tests via `dotnet test`.

```bash
# Run all automated tests (Sdk unit tests + Headless UI integration tests)
dotnet test FryWorld.slnx
```

### What is tested headlessly:
- **`MainWindowHeadlessTests`**:
  - Off-screen rendering of `MainWindow` and non-empty Skia frame bitmap validation.
  - Navigation Rail item switching (Suite Launcher <-> Plugins & Store).
  - Dynamic capability mounting (registering a dynamic plugin studio view and verifying selection + caching).
- **`LauncherHomeHeadlessTests`**:
  - Seed app catalog verification (12 default suite apps).
  - Segmented status filters (`All`, `Installed`, `NotInstalled`, `Updates`).
  - Search query filtering across App Name, Description, Category, and Package ID.
  - View mode switching between 4-column Grid and Tabular List.
- **`CommandPaletteHeadlessTests`**:
  - ⌘K / Ctrl+K Command Palette trigger and modal overlay rendering.
  - Search filtering of registered capability shortcuts and command palette items.
- **`ShellOverlayHeadlessTests`**:
  - In-situ floating overlay lifecycle, canvas rendering, and dismiss action.
- **`ThemingHeadlessTests`**:
  - Dynamic Theme switching between Material 3 Expressive Dark and Light palettes.

---

## 2. Visual Audit Snapshot Runner (`tools/UiSnapshots`)

The visual snapshot runner renders real views, themes, and layouts off-screen with the Skia engine and saves pixel-accurate PNGs to `test-artifacts/snapshots/`.

```bash
# Render all primary views and states in both Dark and Light themes
dotnet run --project tools/UiSnapshots -- all

# Render individual views:
dotnet run --project tools/UiSnapshots -- main            # MainWindow (Dark)
dotnet run --project tools/UiSnapshots -- main --light    # MainWindow (Light)
dotnet run --project tools/UiSnapshots -- launcher        # Launcher Home (Grid)
dotnet run --project tools/UiSnapshots -- table           # Launcher Home (Table)
dotnet run --project tools/UiSnapshots -- plugins         # Plugins & Store Manager
dotnet run --project tools/UiSnapshots -- palette         # ⌘K Command Palette
dotnet run --project tools/UiSnapshots -- overlay         # Shell Floating Overlay

# Test dynamic in-situ mounting of a plugin (.fryplugin archive or .dll assembly):
dotnet run --project tools/UiSnapshots -- plugin /path/to/Plugin.fryplugin
```

---

## 3. Image Comparison & Diffing Tool (`tools/UiSnapshots/images.py`)

Python utility (requires `Pillow`) to inspect pixel differences, crop coordinates, or generate diffs:

```bash
# Compare two snapshot images (generates diff with changed areas boxed in red):
python3 tools/UiSnapshots/images.py compare test-artifacts/snapshots/fryworld_main_dark.png test-artifacts/snapshots/fryworld_main_light.png

# Grid overlay with coordinate markings every 100px:
python3 tools/UiSnapshots/images.py grid test-artifacts/snapshots/fryworld_launcher_grid.png

# Zoom into a specific UI component:
python3 tools/UiSnapshots/images.py zoom test-artifacts/snapshots/fryworld_launcher_grid.png 50 50 300 200 --scale 2
```

---

## 4. How It Works Under The Hood

1. **Headless Avalonia Setup**:
   Configured with Skia drawing:
   ```csharp
   AppBuilder.Configure<App>()
       .UseSkia()
       .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
       .WithInterFont()
       .SetupWithoutStarting();
   ```
2. **Pumping the UI Thread**:
   With no native message loop, `Dispatcher.UIThread.RunJobs()` is manually pumped to process bindings, layout passes, and animations.
3. **Thread Safety in Tests**:
   Tests are run on `Dispatcher.UIThread` via `TestHeadlessSession.Run(...)` with `[CollectionDefinition("HeadlessTests", DisableParallelization = true)]` to ensure reliable thread access.
4. **Frame Capture**:
   `window.CaptureRenderedFrame()` captures the off-screen Skia surface into an Avalonia `WriteableBitmap`, which is verified and saved using `PngBitmapEncoderOptions.Default`.
