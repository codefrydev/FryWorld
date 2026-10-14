using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace FryWorld.Sdk.Descriptors;

/// <summary>
/// Descriptor representing an action button placed inside a dynamic ribbon or toolbar group.
/// </summary>
public sealed class RibbonActionDescriptor
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Tooltip { get; init; }
    public string? IconKind { get; init; }
    public string? GroupId { get; init; }
    public int Order { get; init; } = 100;
    public ICommand? Command { get; init; }
}

/// <summary>
/// Descriptor representing a dynamic header ribbon or toolbar tab contributed by a plugin.
/// </summary>
public sealed class RibbonTabDescriptor
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public int Order { get; init; } = 100;
    public List<RibbonActionDescriptor> Actions { get; init; } = new();
}
