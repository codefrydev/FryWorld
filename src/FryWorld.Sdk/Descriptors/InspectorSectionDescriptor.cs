using System;
using System.Windows.Input;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an inspector/property section contributed by a plugin.
/// </summary>
public sealed class InspectorSectionDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Order { get; init; } = 100;
    public required Func<Control> ViewFactory { get; init; }
}

/// <summary>
/// Descriptor representing a customizable keyboard shortcut contributed by a plugin.
/// </summary>
public sealed class ShortcutDescriptor
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public required string KeyGesture { get; init; }
    public ICommand? Command { get; init; }
}
