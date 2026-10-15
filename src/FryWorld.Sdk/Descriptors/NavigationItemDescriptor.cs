using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a full-viewport workspace navigation item or studio page.
/// Used by plugins (e.g. C# Code Studio, Image Editor, or future studios) to mount directly into the Navigation Rail.
/// </summary>
public sealed class NavigationItemDescriptor
{
    /// <summary>Unique id; used with <c>IShellService.NavigateTo</c>.</summary>
    public required string Id { get; init; }
    /// <summary>Name shown in the navigation rail.</summary>
    public required string Title { get; init; }
    /// <summary>Tooltip text.</summary>
    public string? Tooltip { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Accent colour as #RRGGBB.</summary>
    public string? AccentColor { get; init; }
    /// <summary>Position in the rail; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>Group it is listed under.</summary>
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
