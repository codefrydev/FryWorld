# Claude Code Guidelines for FryWorld

## Build & Test Commands
- Build: `dotnet build FryWorld.slnx` (Warning-free required: `TreatWarningsAsErrors=true`)
- Run Tests: `dotnet test FryWorld.slnx`
- Launch App: `dotnet run --project src/FryWorld/FryWorld.csproj`

## Architecture & Code Rules
1. **Pure Universal SDK (`FryWorld.Sdk`)**:
   - Zero application-specific or PDF dependencies.
   - Clean namespaces: `FryWorld.Sdk.Plugins`, `FryWorld.Sdk.Descriptors`, `FryWorld.Sdk.Pipelines`, `FryWorld.Sdk.Loading`, `FryWorld.Sdk.Marketplace`.
   - Reversible effects: all plugin registrations must bind cleanup actions to `PluginScope` (`ctx.RegisterEffect(...)`).
2. **Google Material Design 3 (M3) Expressive Mandate**:
   - Shapes: Full pill (`CornerRadius="{StaticResource M3ShapeCornerFull}"` or `9999`) for buttons, search inputs, segmented capsules. Extra Large (28px) for dialogs. Large (16px) for cards.
   - Dynamic Brushes: Always reference dynamic tokens: `M3PrimaryBrush`, `M3SurfaceBrush`, `M3SurfaceContainer...`, `M3OutlineBrush`. Never hardcode hex colors in XAML.
3. **CommunityToolkit.Mvvm**:
   - Maximize use of source generators (`[ObservableProperty]`, `[RelayCommand]`).
