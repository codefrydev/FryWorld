using System;
using System.Windows.Input;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing a dynamic widget embedded in the status bar.
/// </summary>
public sealed class StatusBarWidgetDescriptor
{
    public required string Id { get; init; }
    public string? Label { get; init; }
    public string? IconKind { get; init; }
    public string? Tooltip { get; init; }
    public int Order { get; init; } = 100;
    public bool AlignRight { get; init; } = true;
    public ICommand? ClickCommand { get; init; }

    /// <summary>
    /// Custom Avalonia control factory if custom visual formatting is required.
    /// </summary>
    public Func<Control>? CustomControlFactory { get; init; }
}
