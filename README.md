# FryWorld — The Modern Plugin Ecosystem & Creative Suite Launcher

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Avalonia UI 12](https://img.shields.io/badge/Avalonia-12.1.2-red.svg)](https://avaloniaui.net/)

**FryWorld** is the central platform and flagship desktop application for the Fry ecosystem. It hosts the pure **`FryWorld.Sdk`** and provides an ultra-fast, modern **Creative & Developer Suite Launcher** built on Avalonia UI and Google Material Design 3 (M3) Expressive.

---

## 🚀 Key Highlights

1. **Pure Universal SDK (`FryWorld.Sdk`)**:
   - Free from PDF or domain-specific dependencies.
   - Built for high performance on C# 13 and .NET 10.
   - Microkernel architecture inspired by Cordis with typed dispatch pipelines: `Waterfall`, `Bail`, `Parallel`, `Serial`.
   - Dynamic registries for Full-Viewport Pages, Floating Overlays, Command Palette (`⌘K`), Status Bar widgets, Header Toolbars, Sidebars, and Inspectors.
   - Isolated assembly loading via `CollectiblePluginLoadContext`.
2. **Creative & Developer Suite Launcher**:
   - Sleek 4-column card grid with numbered index pills (`01`, `02`... in vibrant curated colors).
   - High-res squircle icons and dual-tone title typography.
   - 1-click **`LAUNCH`** (instant 0ms switch to workspace/overlay) and **`INSTALL`**.
   - Segmented filter counters: `ALL`, `INSTALLED`, `UPDATES`, `NOT INSTALLED`.
   - Grid and Table view modes.
3. **Plugins Manager Studio**:
   - 3-tab layout: Installed, Marketplace, and Updates.
   - Auto-generated settings configuration form from plugin `settingsSchema`.

---

## 🛠️ Build & Run Commands

```bash
# Build the entire solution (Warning-free required)
dotnet build FryWorld.slnx

# Run all automated tests (Sdk unit tests + Headless UI integration tests)
dotnet test FryWorld.slnx

# Run Headless UI Visual Snapshots (render real views & dialogs off-screen)
dotnet run --project tools/UiSnapshots -- all

# Run the FryWorld Desktop Host
dotnet run --project src/FryWorld/FryWorld.csproj
```

---

## 🧩 Building a Plugin for FryWorld

1. Create a new `.NET 10` Class Library:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="FryWorld.Sdk" Version="1.0.0" PrivateAssets="all" />
     </ItemGroup>
   </Project>
   ```

2. Implement `IFryPlugin`:
   ```csharp
   using FryWorld.Sdk.Descriptors;
   using FryWorld.Sdk.Plugins;

   public class MyStudioPlugin : FryPluginBase
   {
       public override string Id => "com.example.mystudio";
       public override string Name => "My Studio";

       public override Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
       {
           // Register full-viewport workspace studio page
           ctx.RegisterNavigationItem(new NavigationItemDescriptor
           {
               Id = "my.studio",
               Title = "My Studio",
               IconKind = "Wrench",
               ViewFactory = () => new MyStudioView()
           });

           // Register Command Palette action (⌘K / Ctrl+K)
           ctx.RegisterCommand(new CommandPaletteDescriptor
           {
               Id = "cmd.mystudio.open",
               Title = "Open My Studio",
               Category = "Navigation",
               ShortcutKey = "Ctrl+Shift+M"
           });

           return Task.CompletedTask;
       }
   }
   ```

---

## 📄 License
MIT License. Copyright © 2026 Code Fry Dev.
