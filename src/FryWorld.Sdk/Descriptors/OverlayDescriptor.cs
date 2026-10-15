using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>How the host frames an overlay.</summary>
public enum OverlayChromeMode
{
    /// <summary>A card with the host's title bar and close button.</summary>
    StandardCard,
    /// <summary>No host frame; the overlay draws its own title bar.</summary>
    CustomChrome,
    /// <summary>A compact floating pill.</summary>
    FloatingPill
}

/// <summary>
/// Descriptor representing a floating, draggable, or docked shell overlay.
/// Ideal for games (Snake, Chess, TicTacToe) and persistent utility widgets (Music Player, Telemetry HUD, Scratchpad).
/// </summary>
public sealed class OverlayDescriptor
{
    /// <summary>Unique id; used with <c>IShellService.ShowOverlay</c>.</summary>
    public required string Id { get; init; }
    /// <summary>Title shown in the overlay's frame.</summary>
    public required string Title { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Width when first opened.</summary>
    public double DefaultWidth { get; init; } = 480;
    /// <summary>Height when first opened.</summary>
    public double DefaultHeight { get; init; } = 360;
    /// <summary>Smallest width the user can resize it to.</summary>
    public double MinWidth { get; init; } = 280;
    /// <summary>Smallest height the user can resize it to.</summary>
    public double MinHeight { get; init; } = 200;
    /// <summary>Whether the user can resize it.</summary>
    public bool IsResizable { get; init; } = true;
    /// <summary>Whether it opens as soon as the plugin mounts.</summary>
    public bool AutoOpenOnMount { get; init; } = false;
    /// <summary>How the host frames it.</summary>
    public OverlayChromeMode ChromeMode { get; init; } = OverlayChromeMode.StandardCard;
    /// <summary>Whether <see cref="ChromeMode"/> is <see cref="OverlayChromeMode.CustomChrome"/>.</summary>
    public bool HasCustomChrome => ChromeMode == OverlayChromeMode.CustomChrome;

    /// <summary>
    /// Factory delegate producing the overlay view control.
    /// </summary>
    public required Func<Control> ViewFactory { get; init; }

    /// <summary>
    /// Optional factory delegate producing the view model associated with this overlay.
    /// </summary>
    public Func<object?>? ViewModelFactory { get; init; }
}
