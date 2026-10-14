using System;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

public enum OverlayChromeMode
{
    StandardCard,
    CustomChrome,
    FloatingPill
}

/// <summary>
/// Descriptor representing a floating, draggable, or docked shell overlay.
/// Ideal for games (Snake, Chess, TicTacToe) and persistent utility widgets (Music Player, Telemetry HUD, Scratchpad).
/// </summary>
public sealed class OverlayDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? IconKind { get; init; }
    public double DefaultWidth { get; init; } = 480;
    public double DefaultHeight { get; init; } = 360;
    public double MinWidth { get; init; } = 280;
    public double MinHeight { get; init; } = 200;
    public bool IsResizable { get; init; } = true;
    public bool AutoOpenOnMount { get; init; } = false;
    public OverlayChromeMode ChromeMode { get; init; } = OverlayChromeMode.StandardCard;
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
