using System;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a discrete functional tool or action contributed by a plugin.
/// </summary>
public sealed class ToolDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? IconKind { get; init; }
    public string? Shortcut { get; init; }
    public ICommand? Command { get; init; }
}
