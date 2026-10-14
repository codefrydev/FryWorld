# FryWorld.Sdk

[![NuGet](https://img.shields.io/nuget/v/FryWorld.Sdk.svg)](https://www.nuget.org/packages/FryWorld.Sdk)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

**`FryWorld.Sdk`** is the pure, high-performance plugin microkernel, dynamic capability registry, and runtime SDK for the **FryWorld** desktop platform.

Inspired by the Cordis composability architecture, `FryWorld.Sdk` enables developers to build decoupled, modular desktop applications, full-viewport workspace studios, floating mini-app overlays, and dynamic tools.

---

## 🚀 Installation

Add the SDK to your plugin or library project:

```bash
dotnet add package FryWorld.Sdk
```

### Critical Requirement: Set `PrivateAssets="all"`
Because the host application (`FryWorld`) provides the `FryWorld.Sdk` runtime contract assembly at runtime, **always mark `FryWorld.Sdk` with `PrivateAssets="all"`** in your plugin `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="FryWorld.Sdk" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

This prevents shipping duplicate contract assemblies inside your `.fryplugin` bundle and guarantees consistent type identity.

---

## 🧩 Building a Plugin

Subclass `FryPluginBase` or implement `IFryPlugin`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;

public class MyCustomStudioPlugin : FryPluginBase
{
    public override string Id => "com.example.mystudio";
    public override string Name => "My Custom Studio";
    public override Version Version => new(1, 0, 0);

    public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        // 1. Mount a Full-Viewport Workspace Studio Page (Navigation Rail)
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "studio.main",
            Title = "My Studio",
            IconKind = "Wrench",
            ViewFactory = () => new MyStudioView()
        });

        // 2. Mount a Floating Shell Overlay Mini-App (Snake, Music Player, etc.)
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = "overlay.hud",
            Title = "Studio Telemetry",
            DefaultWidth = 320,
            DefaultHeight = 240,
            ViewFactory = () => new TelemetryView()
        });

        // 3. Register a Command Palette Action (⌘K / Ctrl+K)
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.open.studio",
            Title = "Open My Studio",
            Category = "Studio Tools",
            ShortcutKey = "Ctrl+Shift+M"
        });

        return Task.CompletedTask;
    }
}
```

---

## ⚡ Cordis Dispatch Pipelines

`FryWorld.Sdk` provides 4 typed dispatch modes via `ctx.Pipelines`:
1. **`Waterfall`**: Around-middleware with `next()` execution (export filters, view transformations).
2. **`Bail`**: Short-circuits on the first non-null/handled result (file format importers, converters).
3. **`Parallel`**: Concurrent asynchronous broadcast via `Task.WhenAll` (telemetry, background sync).
4. **`Serial`**: Sequentially awaited pipeline (validation rules, checks).

---

## 📄 License
MIT License. Copyright © 2026 Code Fry Dev.
