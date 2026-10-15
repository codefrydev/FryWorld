using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an action button placed inside a dynamic ribbon or toolbar group.
/// </summary>
public sealed class RibbonActionDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Button text.</summary>
    public required string Label { get; init; }
    /// <summary>Tooltip text.</summary>
    public string? Tooltip { get; init; }
    /// <summary>Material icon kind.</summary>
    public string? IconKind { get; init; }
    /// <summary>Group within the tab the button belongs to.</summary>
    public string? GroupId { get; init; }
    /// <summary>Position within the group; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>What the button does.</summary>
    public ICommand? Command { get; init; }
}

/// <summary>
/// Descriptor representing a dynamic header ribbon or toolbar tab contributed by a plugin.
/// </summary>
public sealed class RibbonTabDescriptor
{
    /// <summary>Unique id.</summary>
    public required string Id { get; init; }
    /// <summary>Tab caption.</summary>
    public required string Header { get; init; }
    /// <summary>Position among tabs; lower comes first.</summary>
    public int Order { get; init; } = 100;
    /// <summary>The tab's buttons.</summary>
    public List<RibbonActionDescriptor> Actions { get; init; } = new();
}
