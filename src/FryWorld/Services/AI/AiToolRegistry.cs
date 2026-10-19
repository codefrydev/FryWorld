namespace FryWorld.Services.AI;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FryWorld.Sdk.AI;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using Microsoft.Extensions.AI;

/// <summary>
/// Central registry combining built-in host system tools with dynamic tools registered by active plugins.
/// </summary>
public class AiToolRegistry : IAiToolRegistry
{
    private readonly IFryPluginContext _context;
    private readonly Func<string?> _getActivePluginId;
    private readonly List<AiToolDescriptor> _builtInTools = new();

    public event Action? ToolsChanged;

    public AiToolRegistry(IFryPluginContext context, Func<string?> getActivePluginId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _getActivePluginId = getActivePluginId ?? throw new ArgumentNullException(nameof(getActivePluginId));

        RegisterBuiltInTools();
        _context.AiToolsChanged += () => ToolsChanged?.Invoke();
    }

    private void RegisterBuiltInTools()
    {
        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_list_plugins",
            Description = "Lists all installed, mounted, and available plugins in the FryWorld host environment.",
            Category = "System",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ListPlugins, "fryworld_list_plugins", "Lists all installed and active plugins.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_navigate",
            Description = "Navigates the FryWorld host interface to a specific workspace or plugin view (e.g. 'circuit', 'csharp', 'com.fryworld.circuit', 'Circuit Studio').",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(NavigateToPlugin, "fryworld_navigate", "Navigates to the specified plugin by ID, suffix, or title.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_get_active_workspace",
            Description = "Returns the currently active workspace, title, category, and list of open floating overlays.",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(GetActiveWorkspace, "fryworld_get_active_workspace", "Returns active workspace state.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_show_overlay",
            Description = "Opens or brings to front a floating picture-in-picture overlay window (e.g. 'com.fryworld.circuit.overlay' or 'circuit').",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ShowOverlay, "fryworld_show_overlay", "Opens a floating overlay.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_hide_overlay",
            Description = "Closes an open floating overlay window.",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(HideOverlay, "fryworld_hide_overlay", "Closes a floating overlay.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_list_overlays",
            Description = "Lists all registered floating overlays in FryWorld and their current visibility.",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ListOverlays, "fryworld_list_overlays", "Lists all available overlays.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_list_commands",
            Description = "Lists available commands in the FryWorld Command Palette.",
            Category = "System",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ListCommands, "fryworld_list_commands", "Lists all available commands.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_execute_command",
            Description = "Executes an action registered in the FryWorld Command Palette by command ID.",
            Category = "System",
            RequiresConfirmation = true,
            Function = AIFunctionFactory.Create(ExecuteCommand, "fryworld_execute_command", "Executes a command palette action.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_scaffold_plugin",
            Description = "Generates a complete new FryWorld plugin and SDK project template on disk with manifest and C# entry point.",
            Category = "Extensibility",
            RequiresConfirmation = true,
            Function = AIFunctionFactory.Create(ScaffoldPlugin, "fryworld_scaffold_plugin", "Scaffolds a new plugin project.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_get_ui_state",
            Description = "Returns the comprehensive UI state of FryWorld, including active workspace ID, title, current theme, all open floating overlays, and list of all available workspaces.",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(GetUiState, "fryworld_get_ui_state", "Returns comprehensive UI state.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_list_workspaces",
            Description = "Lists all registered workspaces/studios in the FryWorld navigation rail, including their ID, title, category, icon, and whether they are currently active.",
            Category = "Navigation",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ListWorkspaces, "fryworld_list_workspaces", "Lists all workspaces.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_set_theme",
            Description = "Changes or toggles the FryWorld visual theme. Supports 'dark', 'light', 'toggle', 'system', or any specific theme ID.",
            Category = "System",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(SetTheme, "fryworld_set_theme", "Sets or toggles the application theme.")
        });

        _builtInTools.Add(new AiToolDescriptor
        {
            Name = "fryworld_show_toast",
            Description = "Displays a non-blocking toast notification banner in FryWorld.",
            Category = "System",
            RequiresConfirmation = false,
            Function = AIFunctionFactory.Create(ShowToast, "fryworld_show_toast", "Displays a toast notification.")
        });
    }

    [Description("Lists all registered and mounted plugins.")]
    private string ListPlugins()
    {
        var navItems = _context.GetRegisteredNavigationItems();
        var plugins = navItems.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            category = n.Category,
            icon = n.IconKind
        });

        return JsonSerializer.Serialize(new { activePlugin = _getActivePluginId(), plugins }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Navigates the host interface to a plugin view.")]
    private string NavigateToPlugin([Description("The plugin or navigation ID to activate (e.g. 'circuit', 'com.fryworld.circuit', 'Circuit Studio').")] string pluginId)
    {
        if (_context.TryGetService<IShellService>(out var shell))
        {
            bool success = shell.NavigateTo(pluginId);
            return success ? $"Successfully navigated to workspace '{pluginId}'." : $"Could not navigate to '{pluginId}': ID not found.";
        }

        return "Shell service is not available.";
    }

    private static string? NormalizePluginId(string? id) =>
        id?.StartsWith("dyn.") == true ? id["dyn.".Length..] : id;

    [Description("Returns the currently active workspace, title, and open floating overlays.")]
    private string GetActiveWorkspace()
    {
        string? rawActiveId = _getActivePluginId();
        string? activeId = NormalizePluginId(rawActiveId);
        var navItems = _context.GetRegisteredNavigationItems();
        var currentNav = navItems.FirstOrDefault(n =>
            string.Equals(n.Id, activeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Id, rawActiveId, StringComparison.OrdinalIgnoreCase));

        var overlays = _context.GetRegisteredOverlays();
        var openOverlays = new List<object>();
        if (_context.TryGetService<IShellService>(out var shell))
        {
            foreach (var o in overlays)
            {
                if (shell.IsOverlayVisible(o.Id))
                {
                    openOverlays.Add(new { id = o.Id, title = o.Title });
                }
            }
        }

        return JsonSerializer.Serialize(new
        {
            activeWorkspaceId = activeId ?? "home",
            title = currentNav?.Title ?? "Home",
            category = currentNav?.Category ?? "Launcher",
            openOverlays
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Opens a floating picture-in-picture overlay window.")]
    private string ShowOverlay([Description("The overlay ID or name to open.")] string overlayId)
    {
        if (_context.TryGetService<IShellService>(out var shell))
        {
            bool success = shell.ShowOverlay(overlayId);
            return success ? $"Successfully opened overlay '{overlayId}'." : $"Overlay '{overlayId}' not found.";
        }
        return "Shell service is not available.";
    }

    [Description("Closes an open floating overlay window.")]
    private string HideOverlay([Description("The overlay ID or name to close.")] string overlayId)
    {
        if (_context.TryGetService<IShellService>(out var shell))
        {
            bool success = shell.HideOverlay(overlayId);
            return success ? $"Successfully closed overlay '{overlayId}'." : $"Overlay '{overlayId}' was not open or not found.";
        }
        return "Shell service is not available.";
    }

    [Description("Lists all registered floating overlays in FryWorld.")]
    private string ListOverlays()
    {
        var overlays = _context.GetRegisteredOverlays();
        _context.TryGetService<IShellService>(out var shell);

        var list = overlays.Select(o => new
        {
            id = o.Id,
            title = o.Title,
            isOpen = shell?.IsOverlayVisible(o.Id) ?? false,
            defaultWidth = o.DefaultWidth,
            defaultHeight = o.DefaultHeight
        });

        return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Lists commands registered in the command palette.")]
    private string ListCommands()
    {
        var cmds = _context.GetRegisteredCommands().Select(c => new
        {
            id = c.Id,
            title = c.Title,
            subtitle = c.Subtitle,
            category = c.Category,
            shortcut = c.ShortcutKey
        });

        return JsonSerializer.Serialize(cmds, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Executes a command registered in the command palette.")]
    private string ExecuteCommand([Description("The command ID to execute.")] string commandId)
    {
        var cmd = _context.GetRegisteredCommands().FirstOrDefault(c => string.Equals(c.Id, commandId, StringComparison.OrdinalIgnoreCase));
        if (cmd == null) return $"Command '{commandId}' not found.";

        if (cmd.Command != null && cmd.Command.CanExecute(null))
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => cmd.Command.Execute(null));
            return $"Dispatched command '{commandId}' successfully.";
        }

        return $"Command '{commandId}' is currently disabled.";
    }

    [Description("Scaffolds a new plugin project directory with .csproj, plugin.json, and Plugin.cs entry point.")]
    private string ScaffoldPlugin(
        [Description("Unique plugin identifier, e.g. 'com.mycompany.mytool'.")] string pluginId,
        [Description("Human-readable plugin name.")] string pluginName,
        [Description("Target directory where the project folder should be created.")] string targetDirectory)
    {
        try
        {
            var projectDir = Path.Combine(targetDirectory, pluginName.Replace(" ", ""));
            Directory.CreateDirectory(projectDir);

            // 1. plugin.json
            var manifest = $$"""
            {
              "id": "{{pluginId}}",
              "name": "{{pluginName}}",
              "version": "1.0.0",
              "author": "FryWorld Developer",
              "entryDll": "{{pluginName.Replace(" ", "")}}.dll",
              "description": "{{pluginName}} plugin for FryWorld",
              "category": "Tools"
            }
            """;
            File.WriteAllText(Path.Combine(projectDir, "plugin.json"), manifest);

            // 2. csproj
            var csproj = $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <AssemblyName>{{pluginName.Replace(" ", "")}}</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="FryWorld.Sdk" Version="0.3.0" />
              </ItemGroup>
            </Project>
            """;
            File.WriteAllText(Path.Combine(projectDir, $"{pluginName.Replace(" ", "")}.csproj"), csproj);

            // 3. Plugin.cs
            var pluginCs = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using FryWorld.Sdk.Plugins;
            using FryWorld.Sdk.Descriptors;
            using FryWorld.Sdk.AI;
            using Microsoft.Extensions.AI;

            namespace {{pluginName.Replace(" ", "")}};

            [FryPluginEntry]
            public class {{pluginName.Replace(" ", "")}}Plugin : IFryPlugin
            {
                public string Id => "{{pluginId}}";
                public string Name => "{{pluginName}}";
                public Version Version => new(1, 0, 0);
                public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
                public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema => null;

                public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
                {
                    // Register dynamic tools, context provider, and workspace UI
                    return Task.CompletedTask;
                }
            }
            """;
            File.WriteAllText(Path.Combine(projectDir, $"{pluginName.Replace(" ", "")}Plugin.cs"), pluginCs);

            return $"Successfully scaffolded plugin project '{pluginName}' at: {projectDir}";
        }
        catch (Exception ex)
        {
            return $"Failed to scaffold plugin: {ex.Message}";
        }
    }

    [Description("Returns comprehensive UI state including active workspace, theme, open overlays, and workspace list.")]
    private string GetUiState()
    {
        string? rawActiveId = _getActivePluginId();
        string? activeId = NormalizePluginId(rawActiveId);
        var navItems = _context.GetRegisteredNavigationItems();
        var currentNav = navItems.FirstOrDefault(n =>
            string.Equals(n.Id, activeId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Id, rawActiveId, StringComparison.OrdinalIgnoreCase));

        string currentTheme = "Unknown";
        if (_context.TryGetService<IThemeService>(out var themeService))
        {
            currentTheme = themeService.CurrentTheme.ToString();
        }

        var overlays = _context.GetRegisteredOverlays();
        var openOverlays = new List<object>();
        if (_context.TryGetService<IShellService>(out var shell))
        {
            foreach (var o in overlays)
            {
                if (shell.IsOverlayVisible(o.Id))
                {
                    openOverlays.Add(new { id = o.Id, title = o.Title });
                }
            }
        }

        var workspaces = navItems.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            category = n.Category,
            icon = n.IconKind,
            isActive = string.Equals(n.Id, activeId, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(n.Id, rawActiveId, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            activeWorkspaceId = activeId ?? "home",
            title = currentNav?.Title ?? "Home",
            category = currentNav?.Category ?? "Launcher",
            theme = currentTheme,
            totalWorkspaces = workspaces.Count,
            openOverlays,
            workspaces
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Lists all registered workspaces in the navigation rail.")]
    private string ListWorkspaces()
    {
        string? rawActiveId = _getActivePluginId();
        string? activeId = NormalizePluginId(rawActiveId);
        var navItems = _context.GetRegisteredNavigationItems();
        var workspaces = navItems.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            category = n.Category,
            icon = n.IconKind,
            isActive = string.Equals(n.Id, activeId, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(n.Id, rawActiveId, StringComparison.OrdinalIgnoreCase)
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            activeWorkspaceId = activeId ?? "home",
            total = workspaces.Count,
            workspaces
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Changes or toggles the visual theme.")]
    private string SetTheme([Description("Target theme: 'dark', 'light', 'toggle', 'system', or a registered theme ID.")] string theme)
    {
        if (!_context.TryGetService<IThemeService>(out var themeService))
        {
            return "Theme service is not available.";
        }

        string normalized = theme.Trim().ToLowerInvariant();
        if (normalized == "toggle")
        {
            themeService.ToggleTheme();
            return $"Toggled theme. Current theme is now {themeService.CurrentTheme}.";
        }
        else if (normalized == "dark")
        {
            themeService.SetTheme(AppTheme.Dark);
            return "Switched theme to Dark.";
        }
        else if (normalized == "light")
        {
            themeService.SetTheme(AppTheme.Light);
            return "Switched theme to Light.";
        }
        else if (normalized == "system")
        {
            themeService.SetTheme(AppTheme.System);
            return "Switched theme to follow System.";
        }
        else if (_context.TryGetService<FryWorld.Sdk.Theming.IThemeManager>(out var themeMgr))
        {
            if (themeMgr.ApplyTheme(theme))
            {
                return $"Applied theme '{theme}'. Current theme is {themeService.CurrentTheme}.";
            }
        }

        return $"Unknown theme '{theme}'. Supported values: 'dark', 'light', 'toggle', 'system'.";
    }

    [Description("Displays a non-blocking toast notification in FryWorld.")]
    private string ShowToast(
        [Description("The message text to display.")] string message,
        [Description("The notification title.")] string title = "Information",
        [Description("Optional toast type: 'info', 'information', 'error', 'warning'.")] string? type = "info")
    {
        if (!_context.TryGetService<INotificationService>(out var notifService))
        {
            return "Notification service is not available.";
        }

        string t = type?.Trim().ToLowerInvariant() ?? "info";
        if (t == "error")
        {
            notifService.ShowError(message, title);
        }
        else
        {
            notifService.ShowToast(message, title);
        }

        return $"Displayed notification: [{title}] {message}";
    }

    public IReadOnlyList<AiToolDescriptor> GetRegisteredTools()
    {
        var pluginTools = _context.GetRegisteredAiTools();
        var all = new List<AiToolDescriptor>(_builtInTools.Count + pluginTools.Count);
        all.AddRange(_builtInTools);
        all.AddRange(pluginTools);
        return all;
    }

    public IReadOnlyList<AITool> GetAiTools(string? pluginScope = null)
    {
        var tools = GetRegisteredTools();
        if (string.IsNullOrWhiteSpace(pluginScope))
        {
            return tools.Select(t => (AITool)t.Function).ToList();
        }

        return tools
            .Where(t => t.PluginId == null || string.Equals(t.PluginId, pluginScope, StringComparison.OrdinalIgnoreCase))
            .Select(t => (AITool)t.Function)
            .ToList();
    }

    public bool TryGetTool(string toolName, [NotNullWhen(true)] out AiToolDescriptor? tool)
    {
        tool = GetRegisteredTools().FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
        return tool != null;
    }
}
