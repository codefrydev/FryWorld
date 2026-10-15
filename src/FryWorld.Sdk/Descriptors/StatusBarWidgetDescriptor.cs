using System;
using System.Windows.Input;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a dynamic widget embedded in the status bar.
/// </summary>
public sealed class StatusBarWidgetDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Text shown when there is no custom control.</summary>
    public string? Label { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Tooltip text.</summary>
    public string? Tooltip { get; init; }
    /// <summary>Position along the bar; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>Whether it sits on the right side of the bar.</summary>
    public bool AlignRight { get; init; } = true;
    /// <summary>What clicking it runs; it is a button when set.</summary>
    public ICommand? ClickCommand { get; init; }

    /// <summary>
    /// Custom Avalonia control factory if custom visual formatting is required.
    /// </summary>
    public Func<Control>? CustomControlFactory { get; init; }
}
