using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FryWorld.Sdk.Shortcuts;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a customizable keyboard shortcut contributed by a plugin.
/// </summary>
public sealed class ShortcutDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }

    /// <summary>What the shortcut does, shown in the host's shortcut list.</summary>
    public required string Description { get; init; }

    /// <summary>Key combination, e.g. "Ctrl+Shift+M" or "Mod+S".</summary>
    public required string KeyGesture { get; init; }

    /// <summary>What the shortcut runs.</summary>
    public ICommand? Command { get; init; }

    /// <summary>Target activation scope, e.g. "global", "plugin:circuit", "circuit". Default is "global".</summary>
    public string Scope { get; init; } = ShortcutScope.Global;

    /// <summary>Optional context key condition or expression (e.g. "hasSelection").</summary>
    public string? When { get; init; }

    /// <summary>Optional predicate evaluated against the active shortcut context.</summary>
    public Func<IShortcutContext, bool>? WhenPredicate { get; init; }

    /// <summary>Priority within the same scope; higher values take precedence.</summary>
    public int Priority { get; init; } = 0;

    /// <summary>Category or grouping name.</summary>
    public string? Category { get; init; }

    /// <summary>Optional human-readable title.</summary>
    public string? Title { get; init; }

    /// <summary>Asynchronous execution delegate.</summary>
    public Func<Task>? AsyncAction { get; init; }

    /// <summary>Whether this shortcut is currently enabled.</summary>
    public bool IsEnabled { get; init; } = true;
}
