namespace FryWorld.Sdk.Shortcuts;

/// <summary>
/// Precedence tiers for resolving conflicting shortcut bindings.
/// Higher tiers override lower tiers when matching key gestures in the same scope.
/// </summary>
public enum ShortcutTier
{
    /// <summary>
    /// Default host shell shortcut provided by the core FryWorld application.
    /// </summary>
    HostDefault = 0,

    /// <summary>
    /// Default shortcut contributed by a plugin in its source code.
    /// </summary>
    PluginDefault = 1,

    /// <summary>
    /// Keymap extension shortcut contributed by a keymap profile (e.g. Vim keymap, Eagle CAD keymap).
    /// </summary>
    KeymapExtension = 2,

    /// <summary>
    /// User override configured in settings or user keybindings.
    /// </summary>
    UserOverride = 3
}
