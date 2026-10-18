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

