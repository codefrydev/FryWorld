using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an action or tool searchable in the Command Palette (⌘K / Ctrl+K).
/// </summary>
public sealed class CommandPaletteDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>What the palette shows and searches.</summary>
    public required string Title { get; init; }
    /// <summary>Second line of explanation.</summary>
    public string? Subtitle { get; init; }
    /// <summary>Group it is listed under.</summary>
    public string? Category { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Key combination, for display.</summary>
    public string? ShortcutKey { get; init; }

    /// <summary>
    /// Synchronous command action.
    /// </summary>
    public ICommand? Command { get; init; }

    /// <summary>
    /// Asynchronous command action delegate.
    /// </summary>
    public Func<Task>? AsyncAction { get; init; }
}
