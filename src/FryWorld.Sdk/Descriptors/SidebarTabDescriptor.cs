using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an editor sidebar tab contributed by a plugin.
/// </summary>
public sealed class SidebarTabDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Tab caption.</summary>
    public required string Title { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Position among tabs; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>Creates the tab's content when it is first shown.</summary>
    public required Func<Control> ViewFactory { get; init; }
}

/// <summary>
/// Descriptor representing a modal dialog contributed by a plugin.
/// </summary>
public sealed class DialogDescriptor
{
    /// <summary>Unique id; used with <c>IShellService.ShowDialogAsync</c>.</summary>
    public required string Id { get; init; }
    /// <summary>Dialog title.</summary>
    public required string Title { get; init; }
    /// <summary>Width when opened.</summary>
    public double DefaultWidth { get; init; } = 520;
    /// <summary>Height when opened.</summary>
    public double DefaultHeight { get; init; } = 420;
    /// <summary>Creates the dialog's content each time it opens.</summary>
    public required Func<Control> ViewFactory { get; init; }
}
