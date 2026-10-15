using System.Collections.Generic;

namespace FryWorld.Sdk.Theming;

/// <summary>
/// Predefined built-in theme presets ready to be applied or customized.
/// </summary>
public static class BuiltInThemes
{
    /// <summary>Standard modern Dark+ palette.</summary>
    public static readonly ThemeDefinition DarkPlus = new()
    {
        Id = "dark-plus",
        Name = "Dark+ (Default)",
        Description = "Standard modern Dark+ palette with slate surfaces and vibrant blue accents",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#0D1117",
            ["M3SurfaceColor"] = "#0D1117",
            ["M3SurfaceContainerLowestBrush"] = "#080C11",
            ["M3SurfaceContainerLowestColor"] = "#080C11",
            ["M3SurfaceContainerLowBrush"] = "#121721",
            ["M3SurfaceContainerLowColor"] = "#121721",
            ["M3SurfaceContainerBrush"] = "#161D2A",
            ["M3SurfaceContainerColor"] = "#161D2A",
            ["M3SurfaceContainerHighBrush"] = "#1C2536",
            ["M3SurfaceContainerHighColor"] = "#1C2536",
            ["M3SurfaceContainerHighestBrush"] = "#253046",
            ["M3SurfaceContainerHighestColor"] = "#253046",
            ["M3PrimaryBrush"] = "#2F81F7",
            ["M3PrimaryColor"] = "#2F81F7",
            ["M3PrimaryContainerBrush"] = "#1A2F81F7",
            ["M3PrimaryContainerColor"] = "#1A2F81F7",
            ["M3OnPrimaryBrush"] = "#FFFFFF",
            ["M3OnPrimaryColor"] = "#FFFFFF",
            ["M3OnPrimaryContainerBrush"] = "#2F81F7",
            ["M3OnPrimaryContainerColor"] = "#2F81F7",
            ["M3SecondaryBrush"] = "#A855F7",
            ["M3SecondaryColor"] = "#A855F7",
            ["M3SecondaryContainerBrush"] = "#21262D",
            ["M3SecondaryContainerColor"] = "#21262D",
            ["M3OnSecondaryBrush"] = "#C9D1D9",
            ["M3OnSecondaryColor"] = "#C9D1D9",
            ["M3OutlineBrush"] = "#30363D",
            ["M3OutlineColor"] = "#30363D",
            ["M3OutlineVariantBrush"] = "#21262D",
            ["M3OutlineVariantColor"] = "#21262D",
            ["M3OnSurfaceBrush"] = "#F8FAFC",
            ["M3OnSurfaceColor"] = "#F8FAFC",
            ["M3OnSurfaceVariantBrush"] = "#8B949E",
            ["M3OnSurfaceVariantColor"] = "#8B949E",
            ["M3ErrorBrush"] = "#F85149",
            ["M3ErrorColor"] = "#F85149",

            // Dev Studio tokens
            ["DsBgBrush"] = "#0D1117",
            ["DsSurfaceBrush"] = "#161B22",
            ["DsSurfaceHoverBrush"] = "#21262D",
            ["DsSurfaceHighBrush"] = "#1C2128",
            ["DsBorderBrush"] = "#21262D",
            ["DsBorderSubtleBrush"] = "#171B22",
            ["DsTextBrush"] = "#C9D1D9",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#8B949E",
            ["DsPrimaryBrush"] = "#2F81F7",
            ["DsPrimaryHoverBrush"] = "#1F6FEB",
            ["DsPrimarySubtleBrush"] = "#1A2F81F7",
            ["DsSelectionBrush"] = "#992F81F7",
            ["DsFocusBorderBrush"] = "#8C2F81F7",
            ["DsPrimaryBorderSubtleBrush"] = "#332F81F7"
        }
    };

    /// <summary>Standard modern Light+ palette.</summary>
    public static readonly ThemeDefinition LightPlus = new()
    {
        Id = "light-plus",
        Name = "Light+ (Default)",
        Description = "Clean, crisp daytime palette with high-contrast typography",
        IsDark = false,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#F8FAFD",
            ["M3SurfaceColor"] = "#F8FAFD",
            ["M3SurfaceContainerLowestBrush"] = "#FFFFFF",
            ["M3SurfaceContainerLowestColor"] = "#FFFFFF",
            ["M3SurfaceContainerLowBrush"] = "#F1F5F9",
            ["M3SurfaceContainerLowColor"] = "#F1F5F9",
            ["M3SurfaceContainerBrush"] = "#E9EEF6",
            ["M3SurfaceContainerColor"] = "#E9EEF6",
            ["M3SurfaceContainerHighBrush"] = "#E1E8F2",
            ["M3SurfaceContainerHighColor"] = "#E1E8F2",
            ["M3SurfaceContainerHighestBrush"] = "#D7E1EE",
            ["M3SurfaceContainerHighestColor"] = "#D7E1EE",
            ["M3PrimaryBrush"] = "#0969DA",
            ["M3PrimaryColor"] = "#0969DA",
            ["M3PrimaryContainerBrush"] = "#E0F2FE",
            ["M3PrimaryContainerColor"] = "#E0F2FE",
            ["M3OnPrimaryBrush"] = "#FFFFFF",
            ["M3OnPrimaryColor"] = "#FFFFFF",
            ["M3OnPrimaryContainerBrush"] = "#0969DA",
            ["M3OnPrimaryContainerColor"] = "#0969DA",
            ["M3SecondaryBrush"] = "#9333EA",
            ["M3SecondaryColor"] = "#9333EA",
            ["M3SecondaryContainerBrush"] = "#F3E8FF",
            ["M3SecondaryContainerColor"] = "#F3E8FF",
            ["M3OnSecondaryBrush"] = "#FFFFFF",
            ["M3OnSecondaryColor"] = "#FFFFFF",
            ["M3OutlineBrush"] = "#CBD5E1",
            ["M3OutlineColor"] = "#CBD5E1",
            ["M3OutlineVariantBrush"] = "#E2E8F0",
            ["M3OutlineVariantColor"] = "#E2E8F0",
            ["M3OnSurfaceBrush"] = "#0F172A",
            ["M3OnSurfaceColor"] = "#0F172A",
            ["M3OnSurfaceVariantBrush"] = "#475569",
            ["M3OnSurfaceVariantColor"] = "#475569",
            ["M3ErrorBrush"] = "#DC2626",
            ["M3ErrorColor"] = "#DC2626",

            // Dev Studio tokens
            ["DsBgBrush"] = "#FFFFFF",
            ["DsSurfaceBrush"] = "#F6F8FA",
            ["DsSurfaceHoverBrush"] = "#EAEEF2",
            ["DsSurfaceHighBrush"] = "#FFFFFF",
            ["DsBorderBrush"] = "#D0D7DE",
            ["DsBorderSubtleBrush"] = "#EAEFF5",
            ["DsTextBrush"] = "#24292F",
            ["DsTextWhiteBrush"] = "#0969DA",
            ["DsMutedBrush"] = "#57606A",
            ["DsPrimaryBrush"] = "#0969DA",
            ["DsPrimaryHoverBrush"] = "#0854B0",
            ["DsPrimarySubtleBrush"] = "#1A0969DA",
            ["DsSelectionBrush"] = "#880969DA",
            ["DsFocusBorderBrush"] = "#8C0969DA",
            ["DsPrimaryBorderSubtleBrush"] = "#330969DA"
        }
    };

    /// <summary>Dracula Pro gothic dark purple theme.</summary>
    public static readonly ThemeDefinition Dracula = new()
    {
        Id = "dracula",
        Name = "Dracula Pro",
        Description = "Dark gothic purple theme with vibrant pastel pink, cyan, and violet accents",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#21222C",
            ["M3SurfaceColor"] = "#21222C",
            ["M3SurfaceContainerLowestBrush"] = "#191A21",
            ["M3SurfaceContainerLowestColor"] = "#191A21",
            ["M3SurfaceContainerLowBrush"] = "#242632",
            ["M3SurfaceContainerLowColor"] = "#242632",
            ["M3SurfaceContainerBrush"] = "#282A36",
            ["M3SurfaceContainerColor"] = "#282A36",
            ["M3SurfaceContainerHighBrush"] = "#343746",
            ["M3SurfaceContainerHighColor"] = "#343746",
            ["M3SurfaceContainerHighestBrush"] = "#44475A",
            ["M3SurfaceContainerHighestColor"] = "#44475A",
            ["M3PrimaryBrush"] = "#BD93F9",
            ["M3PrimaryColor"] = "#BD93F9",
            ["M3PrimaryContainerBrush"] = "#382952",
            ["M3PrimaryContainerColor"] = "#382952",
            ["M3OnPrimaryBrush"] = "#282A36",
            ["M3OnPrimaryColor"] = "#282A36",
            ["M3OnPrimaryContainerBrush"] = "#BD93F9",
            ["M3OnPrimaryContainerColor"] = "#BD93F9",
            ["M3SecondaryBrush"] = "#FF79C6",
            ["M3SecondaryColor"] = "#FF79C6",
            ["M3SecondaryContainerBrush"] = "#422436",
            ["M3SecondaryContainerColor"] = "#422436",
            ["M3OnSecondaryBrush"] = "#282A36",
            ["M3OnSecondaryColor"] = "#282A36",
            ["M3OutlineBrush"] = "#6272A4",
            ["M3OutlineColor"] = "#6272A4",
            ["M3OutlineVariantBrush"] = "#44475A",
            ["M3OutlineVariantColor"] = "#44475A",
            ["M3OnSurfaceBrush"] = "#F8F8F2",
            ["M3OnSurfaceColor"] = "#F8F8F2",
            ["M3OnSurfaceVariantBrush"] = "#6272A4",
            ["M3OnSurfaceVariantColor"] = "#6272A4",
            ["M3ErrorBrush"] = "#FF5555",
            ["M3ErrorColor"] = "#FF5555",

            // Dev Studio tokens
            ["DsBgBrush"] = "#21222C",
            ["DsSurfaceBrush"] = "#282A36",
            ["DsSurfaceHoverBrush"] = "#343746",
            ["DsSurfaceHighBrush"] = "#44475A",
            ["DsBorderBrush"] = "#44475A",
            ["DsBorderSubtleBrush"] = "#343746",
            ["DsTextBrush"] = "#F8F8F2",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#6272A4",
            ["DsPrimaryBrush"] = "#BD93F9",
            ["DsPrimaryHoverBrush"] = "#D6ACFF",
            ["DsPrimarySubtleBrush"] = "#1ABD93F9",
            ["DsSelectionBrush"] = "#99BD93F9",
            ["DsFocusBorderBrush"] = "#8CBD93F9",
            ["DsPrimaryBorderSubtleBrush"] = "#33BD93F9"
        }
    };

    /// <summary>Cyberpunk Neon high-contrast theme.</summary>
    public static readonly ThemeDefinition Cyberpunk = new()
    {
        Id = "cyberpunk",
        Name = "Cyberpunk Neon",
        Description = "Futuristic high-contrast neon cyan and hot pink theme on pitch black",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#0D0E15",
            ["M3SurfaceColor"] = "#0D0E15",
            ["M3SurfaceContainerLowestBrush"] = "#07070B",
            ["M3SurfaceContainerLowestColor"] = "#07070B",
            ["M3SurfaceContainerLowBrush"] = "#10111A",
            ["M3SurfaceContainerLowColor"] = "#10111A",
            ["M3SurfaceContainerBrush"] = "#13141F",
            ["M3SurfaceContainerColor"] = "#13141F",
            ["M3SurfaceContainerHighBrush"] = "#1D1E2C",
            ["M3SurfaceContainerHighColor"] = "#1D1E2C",
            ["M3SurfaceContainerHighestBrush"] = "#242638",
            ["M3SurfaceContainerHighestColor"] = "#242638",
            ["M3PrimaryBrush"] = "#00FFCC",
            ["M3PrimaryColor"] = "#00FFCC",
            ["M3PrimaryContainerBrush"] = "#00382E",
            ["M3PrimaryContainerColor"] = "#00382E",
            ["M3OnPrimaryBrush"] = "#0D0E15",
            ["M3OnPrimaryColor"] = "#0D0E15",
            ["M3OnPrimaryContainerBrush"] = "#00FFCC",
            ["M3OnPrimaryContainerColor"] = "#00FFCC",
            ["M3SecondaryBrush"] = "#FF007F",
            ["M3SecondaryColor"] = "#FF007F",
            ["M3SecondaryContainerBrush"] = "#38001C",
            ["M3SecondaryContainerColor"] = "#38001C",
            ["M3OnSecondaryBrush"] = "#FFFFFF",
            ["M3OnSecondaryColor"] = "#FFFFFF",
            ["M3OutlineBrush"] = "#383A54",
            ["M3OutlineColor"] = "#383A54",
            ["M3OutlineVariantBrush"] = "#2C2D42",
            ["M3OutlineVariantColor"] = "#2C2D42",
            ["M3OnSurfaceBrush"] = "#FFFFFF",
            ["M3OnSurfaceColor"] = "#FFFFFF",
            ["M3OnSurfaceVariantBrush"] = "#727894",
            ["M3OnSurfaceVariantColor"] = "#727894",
            ["M3ErrorBrush"] = "#FF3366",
            ["M3ErrorColor"] = "#FF3366",

            // Dev Studio tokens
            ["DsBgBrush"] = "#0D0E15",
            ["DsSurfaceBrush"] = "#13141F",
            ["DsSurfaceHoverBrush"] = "#1D1E2C",
            ["DsSurfaceHighBrush"] = "#242638",
            ["DsBorderBrush"] = "#2C2D42",
            ["DsBorderSubtleBrush"] = "#1E1F30",
            ["DsTextBrush"] = "#E0E6F0",
            ["DsTextWhiteBrush"] = "#00FFCC",
            ["DsMutedBrush"] = "#727894",
            ["DsPrimaryBrush"] = "#00FFCC",
            ["DsPrimaryHoverBrush"] = "#33FFD6",
            ["DsPrimarySubtleBrush"] = "#1A00FFCC",
            ["DsSelectionBrush"] = "#9900FFCC",
            ["DsFocusBorderBrush"] = "#8C00FFCC",
            ["DsPrimaryBorderSubtleBrush"] = "#3300FFCC"
        }
    };

    /// <summary>Monokai Classic warm retro theme.</summary>
    public static readonly ThemeDefinition Monokai = new()
    {
        Id = "monokai",
        Name = "Monokai Classic",
        Description = "Warm retro coding theme with vibrant lime green, amber, and deep charcoal",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#272822",
            ["M3SurfaceColor"] = "#272822",
            ["M3SurfaceContainerLowestBrush"] = "#1E1F1C",
            ["M3SurfaceContainerLowestColor"] = "#1E1F1C",
            ["M3SurfaceContainerLowBrush"] = "#22231E",
            ["M3SurfaceContainerLowColor"] = "#22231E",
            ["M3SurfaceContainerBrush"] = "#2C2D27",
            ["M3SurfaceContainerColor"] = "#2C2D27",
            ["M3SurfaceContainerHighBrush"] = "#3E3D32",
            ["M3SurfaceContainerHighColor"] = "#3E3D32",
            ["M3SurfaceContainerHighestBrush"] = "#49483E",
            ["M3SurfaceContainerHighestColor"] = "#49483E",
            ["M3PrimaryBrush"] = "#A6E22E",
            ["M3PrimaryColor"] = "#A6E22E",
            ["M3PrimaryContainerBrush"] = "#2C3D0C",
            ["M3PrimaryContainerColor"] = "#2C3D0C",
            ["M3OnPrimaryBrush"] = "#1E1F1C",
            ["M3OnPrimaryColor"] = "#1E1F1C",
            ["M3OnPrimaryContainerBrush"] = "#A6E22E",
            ["M3OnPrimaryContainerColor"] = "#A6E22E",
            ["M3SecondaryBrush"] = "#FD971F",
            ["M3SecondaryColor"] = "#FD971F",
            ["M3SecondaryContainerBrush"] = "#442808",
            ["M3SecondaryContainerColor"] = "#442808",
            ["M3OnSecondaryBrush"] = "#1E1F1C",
            ["M3OnSecondaryColor"] = "#1E1F1C",
            ["M3OutlineBrush"] = "#49483E",
            ["M3OutlineColor"] = "#49483E",
            ["M3OutlineVariantBrush"] = "#3E3D32",
            ["M3OutlineVariantColor"] = "#3E3D32",
            ["M3OnSurfaceBrush"] = "#F8F8F2",
            ["M3OnSurfaceColor"] = "#F8F8F2",
            ["M3OnSurfaceVariantBrush"] = "#75715E",
            ["M3OnSurfaceVariantColor"] = "#75715E",
            ["M3ErrorBrush"] = "#F92672",
            ["M3ErrorColor"] = "#F92672",

            // Dev Studio tokens
            ["DsBgBrush"] = "#272822",
            ["DsSurfaceBrush"] = "#1E1F1C",
            ["DsSurfaceHoverBrush"] = "#3E3D32",
            ["DsSurfaceHighBrush"] = "#49483E",
            ["DsBorderBrush"] = "#3E3D32",
            ["DsBorderSubtleBrush"] = "#2E2E28",
            ["DsTextBrush"] = "#F8F8F2",
            ["DsTextWhiteBrush"] = "#FD971F",
            ["DsMutedBrush"] = "#75715E",
            ["DsPrimaryBrush"] = "#A6E22E",
            ["DsPrimaryHoverBrush"] = "#B8EA4E",
            ["DsPrimarySubtleBrush"] = "#1AA6E22E",
            ["DsSelectionBrush"] = "#99A6E22E",
            ["DsFocusBorderBrush"] = "#8CA6E22E",
            ["DsPrimaryBorderSubtleBrush"] = "#33A6E22E"
        }
    };

    /// <summary>One Dark Pro balanced navy theme.</summary>
    public static readonly ThemeDefinition OneDark = new()
    {
        Id = "one-dark",
        Name = "One Dark Pro",
        Description = "Deep balanced navy theme with sky blue accents and warm accents",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#1E1E24",
            ["M3SurfaceColor"] = "#1E1E24",
            ["M3SurfaceContainerLowestBrush"] = "#18181D",
            ["M3SurfaceContainerLowestColor"] = "#18181D",
            ["M3SurfaceContainerLowBrush"] = "#21252B",
            ["M3SurfaceContainerLowColor"] = "#21252B",
            ["M3SurfaceContainerBrush"] = "#282C34",
            ["M3SurfaceContainerColor"] = "#282C34",
            ["M3SurfaceContainerHighBrush"] = "#353B45",
            ["M3SurfaceContainerHighColor"] = "#353B45",
            ["M3SurfaceContainerHighestBrush"] = "#3E4451",
            ["M3SurfaceContainerHighestColor"] = "#3E4451",
            ["M3PrimaryBrush"] = "#61AFEF",
            ["M3PrimaryColor"] = "#61AFEF",
            ["M3PrimaryContainerBrush"] = "#18324E",
            ["M3PrimaryContainerColor"] = "#18324E",
            ["M3OnPrimaryBrush"] = "#1E1E24",
            ["M3OnPrimaryColor"] = "#1E1E24",
            ["M3OnPrimaryContainerBrush"] = "#61AFEF",
            ["M3OnPrimaryContainerColor"] = "#61AFEF",
            ["M3SecondaryBrush"] = "#C678DD",
            ["M3SecondaryColor"] = "#C678DD",
            ["M3SecondaryContainerBrush"] = "#3A1B44",
            ["M3SecondaryContainerColor"] = "#3A1B44",
            ["M3OnSecondaryBrush"] = "#FFFFFF",
            ["M3OnSecondaryColor"] = "#FFFFFF",
            ["M3OutlineBrush"] = "#4B5263",
            ["M3OutlineColor"] = "#4B5263",
            ["M3OutlineVariantBrush"] = "#3E4451",
            ["M3OutlineVariantColor"] = "#3E4451",
            ["M3OnSurfaceBrush"] = "#ABB2BF",
            ["M3OnSurfaceColor"] = "#ABB2BF",
            ["M3OnSurfaceVariantBrush"] = "#5C6370",
            ["M3OnSurfaceVariantColor"] = "#5C6370",
            ["M3ErrorBrush"] = "#E06C75",
            ["M3ErrorColor"] = "#E06C75",

            // Dev Studio tokens
            ["DsBgBrush"] = "#1E1E24",
            ["DsSurfaceBrush"] = "#282C34",
            ["DsSurfaceHoverBrush"] = "#353B45",
            ["DsSurfaceHighBrush"] = "#3E4451",
            ["DsBorderBrush"] = "#3E4451",
            ["DsBorderSubtleBrush"] = "#2C313A",
            ["DsTextBrush"] = "#ABB2BF",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#5C6370",
            ["DsPrimaryBrush"] = "#61AFEF",
            ["DsPrimaryHoverBrush"] = "#74B9F0",
            ["DsPrimarySubtleBrush"] = "#1A61AFEF",
            ["DsSelectionBrush"] = "#9961AFEF",
            ["DsFocusBorderBrush"] = "#8C61AFEF",
            ["DsPrimaryBorderSubtleBrush"] = "#3361AFEF"
        }
    };

    /// <summary>Nord Arctic polar frost theme.</summary>
    public static readonly ThemeDefinition Nord = new()
    {
        Id = "nord",
        Name = "Nord Arctic",
        Description = "An arctic, north-bluish clean and elegant dark palette",
        IsDark = true,
        Colors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M3SurfaceBrush"] = "#242933",
            ["M3SurfaceColor"] = "#242933",
            ["M3SurfaceContainerLowestBrush"] = "#1E222A",
            ["M3SurfaceContainerLowestColor"] = "#1E222A",
            ["M3SurfaceContainerLowBrush"] = "#2E3440",
            ["M3SurfaceContainerLowColor"] = "#2E3440",
            ["M3SurfaceContainerBrush"] = "#3B4252",
            ["M3SurfaceContainerColor"] = "#3B4252",
            ["M3SurfaceContainerHighBrush"] = "#434C5E",
            ["M3SurfaceContainerHighColor"] = "#434C5E",
            ["M3SurfaceContainerHighestBrush"] = "#4C566A",
            ["M3SurfaceContainerHighestColor"] = "#4C566A",
            ["M3PrimaryBrush"] = "#88C0D0",
            ["M3PrimaryColor"] = "#88C0D0",
            ["M3PrimaryContainerBrush"] = "#233942",
            ["M3PrimaryContainerColor"] = "#233942",
            ["M3OnPrimaryBrush"] = "#2E3440",
            ["M3OnPrimaryColor"] = "#2E3440",
            ["M3OnPrimaryContainerBrush"] = "#88C0D0",
            ["M3OnPrimaryContainerColor"] = "#88C0D0",
            ["M3SecondaryBrush"] = "#81A1C1",
            ["M3SecondaryColor"] = "#81A1C1",
            ["M3SecondaryContainerBrush"] = "#23303D",
            ["M3SecondaryContainerColor"] = "#23303D",
            ["M3OnSecondaryBrush"] = "#ECEFF4",
            ["M3OnSecondaryColor"] = "#ECEFF4",
            ["M3OutlineBrush"] = "#4C566A",
            ["M3OutlineColor"] = "#4C566A",
            ["M3OutlineVariantBrush"] = "#3B4252",
            ["M3OutlineVariantColor"] = "#3B4252",
            ["M3OnSurfaceBrush"] = "#ECEFF4",
            ["M3OnSurfaceColor"] = "#ECEFF4",
            ["M3OnSurfaceVariantBrush"] = "#D8DEE9",
            ["M3OnSurfaceVariantColor"] = "#D8DEE9",
            ["M3ErrorBrush"] = "#BF616A",
            ["M3ErrorColor"] = "#BF616A",

            // Dev Studio tokens
            ["DsBgBrush"] = "#242933",
            ["DsSurfaceBrush"] = "#2E3440",
            ["DsSurfaceHoverBrush"] = "#3B4252",
            ["DsSurfaceHighBrush"] = "#434C5E",
            ["DsBorderBrush"] = "#434C5E",
            ["DsBorderSubtleBrush"] = "#3B4252",
            ["DsTextBrush"] = "#ECEFF4",
            ["DsTextWhiteBrush"] = "#FFFFFF",
            ["DsMutedBrush"] = "#D8DEE9",
            ["DsPrimaryBrush"] = "#88C0D0",
            ["DsPrimaryHoverBrush"] = "#8FBCBB",
            ["DsPrimarySubtleBrush"] = "#1A88C0D0",
            ["DsSelectionBrush"] = "#9988C0D0",
            ["DsFocusBorderBrush"] = "#8C88C0D0",
            ["DsPrimaryBorderSubtleBrush"] = "#3388C0D0"
        }
    };

    static BuiltInThemes()
    {
        foreach (var theme in All)
        {
            HarmonicColorGenerator.EnsureCompleteTheme(theme);
        }
    }

    /// <summary>All built-in theme presets.</summary>
    public static IReadOnlyList<ThemeDefinition> All =>
    [
        DarkPlus,
        LightPlus,
        Dracula,
        Cyberpunk,
        Monokai,
        OneDark,
        Nord
    ];
}
