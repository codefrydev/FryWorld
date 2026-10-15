using System;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a discrete functional tool or action contributed by a plugin.
/// </summary>
public sealed class ToolDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Display name.</summary>
    public required string Title { get; init; }
    /// <summary>What the tool does.</summary>
    public string? Description { get; init; }
    /// <summary>Group it is listed under.</summary>
    public string? Category { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Key combination, for display.</summary>
    public string? Shortcut { get; init; }
    /// <summary>What the tool runs.</summary>
    public ICommand? Command { get; init; }
}
