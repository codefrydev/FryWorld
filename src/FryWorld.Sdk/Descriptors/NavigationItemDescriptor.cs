using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a full-viewport workspace navigation item or studio page.
/// Used by plugins (e.g. C# Code Studio, Image Editor, or future studios) to mount directly into the Navigation Rail.
/// </summary>
public sealed class NavigationItemDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Tooltip { get; init; }
    public string? IconKind { get; init; }
    public string? AccentColor { get; init; }
    public int Order { get; init; } = 100;
    public string? Category { get; init; }

    /// <summary>
    /// Factory delegate producing the Avalonia view control when navigated to.
    /// </summary>
    public required Func<Control> ViewFactory { get; init; }

    /// <summary>
    /// Optional factory delegate producing the view model associated with this view.
    /// </summary>
    public Func<object?>? ViewModelFactory { get; init; }
}
