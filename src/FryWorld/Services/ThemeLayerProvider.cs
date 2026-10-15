using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Styling;

namespace FryWorld.Services;

/// <summary>
/// Dynamic theme layer resource provider. Swaps the entire dictionary of themed tokens
/// in one commit, avoiding Avalonia multiple-notification UI freezes and resource shadowing.
/// </summary>
internal sealed class ThemeLayerProvider : ResourceProvider
{
    private IReadOnlyDictionary<object, object?> _values = new Dictionary<object, object?>();

    public override bool HasResources => _values.Count > 0;

    public override bool TryGetResource(object key, ThemeVariant? theme, out object? value) =>
        _values.TryGetValue(key, out value);

    /// <summary>Replaces the entire set of token resources with a single notification.</summary>
    public void Replace(IReadOnlyDictionary<object, object?> values)
    {
        _values = values;
        RaiseResourcesChanged();
    }
}
