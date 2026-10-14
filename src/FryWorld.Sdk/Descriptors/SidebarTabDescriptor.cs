using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an editor sidebar tab contributed by a plugin.
/// </summary>
public sealed class SidebarTabDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? IconKind { get; init; }
    public int Order { get; init; } = 100;
    public required Func<Control> ViewFactory { get; init; }
}

/// <summary>
/// Descriptor representing a modal dialog contributed by a plugin.
/// </summary>
public sealed class DialogDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public double DefaultWidth { get; init; } = 520;
    public double DefaultHeight { get; init; } = 420;
    public required Func<Control> ViewFactory { get; init; }
}
