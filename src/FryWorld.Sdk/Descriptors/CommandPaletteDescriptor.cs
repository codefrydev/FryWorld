using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an action or tool searchable in the Command Palette (⌘K / Ctrl+K).
/// </summary>
public sealed class CommandPaletteDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Category { get; init; }
    public string? IconKind { get; init; }
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
