using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Represents a keyboard shortcut binding that maps a key gesture to an action within a designated scope and context.
/// </summary>
public sealed class ShortcutBinding
{
    private PlatformKeyGesture? _parsedGesture;

    /// <summary>
    /// Unique binding identifier (e.g. "circuit.tool.wire" or "custom.save").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The key gesture or chord sequence (e.g. "W", "Mod+S", "Mod+K, Mod+C").
    /// </summary>
    public required string KeyGesture { get; init; }

    /// <summary>
    /// Gets the lazily parsed cross-platform gesture representation.
    /// </summary>
    public PlatformKeyGesture ParsedGesture => _parsedGesture ??= PlatformKeyGesture.Parse(KeyGesture);

    /// <summary>
    /// Optional semantic command identifier referenced by this binding.
    /// </summary>
    public string? CommandId { get; init; }

    /// <summary>
    /// Optional synchronous command action.
    /// </summary>
    public ICommand? Command { get; init; }

    /// <summary>
    /// Optional asynchronous action delegate.
    /// </summary>
    public Func<IShortcutExecutionContext, Task>? AsyncAction { get; init; }

    /// <summary>
    /// The activation scope in which this shortcut is active (e.g. "global", "plugin:circuit", "overlay:circuit.overlay").
    /// Default is <see cref="ShortcutScope.Global"/>.
    /// </summary>
    public string Scope { get; init; } = ShortcutScope.Global;

    /// <summary>
    /// Optional context key expression or flag name required to be true for this shortcut to fire (e.g. "circuit.hasSelection").
    /// </summary>
    public string? When { get; init; }

    /// <summary>
    /// Optional strongly-typed predicate evaluated against the current shortcut context.
    /// </summary>
    public Func<IShortcutContext, bool>? WhenPredicate { get; init; }

    /// <summary>
    /// Numerical priority within the same scope. Higher values evaluate before lower values.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Precedence tier of this binding (HostDefault, PluginDefault, KeymapExtension, UserOverride).
    /// </summary>
    public ShortcutTier Tier { get; init; } = ShortcutTier.PluginDefault;

    /// <summary>
    /// Human-readable title for UI listings and the Command Palette.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Category or group name (e.g. "Tools", "Navigation", "File").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Explanatory description of what the shortcut does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this binding is currently enabled. Can be set to false by user overrides to disable a shortcut.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
