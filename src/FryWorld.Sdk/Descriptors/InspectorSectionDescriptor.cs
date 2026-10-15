using System;
using System.Windows.Input;
using Avalonia.Controls;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an inspector/property section contributed by a plugin.
/// </summary>
public sealed class InspectorSectionDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Section heading.</summary>
    public required string Title { get; init; }
    /// <summary>Position among sections; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>Creates the section's content.</summary>
    public required Func<Control> ViewFactory { get; init; }
}

/// <summary>
/// Descriptor representing a customizable keyboard shortcut contributed by a plugin.
/// </summary>
public sealed class ShortcutDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>What the shortcut does, shown in the host's shortcut list.</summary>
    public required string Description { get; init; }
    /// <summary>Key combination, e.g. "Ctrl+Shift+M".</summary>
    public required string KeyGesture { get; init; }
    /// <summary>What the shortcut runs.</summary>
    public ICommand? Command { get; init; }
}
