namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Execution states representing a plugin's lifecycle inside <see cref="PluginHost"/>.
/// </summary>
public enum PluginLifecycleState
{
    Registered,
    Mounting,
    Active,
    Unmounting,
    Unloaded,
    Failed
}
