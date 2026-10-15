namespace FryWorld.Sdk.Plugins;

/// <summary>
/// Execution states representing a plugin's lifecycle inside <see cref="PluginHost"/>.
/// </summary>
public enum PluginLifecycleState
{
    /// <summary>Known to the host, not mounted yet.</summary>
    Registered,
    /// <summary>Its <c>ApplyAsync</c> is running.</summary>
    Mounting,
    /// <summary>Mounted; its registrations are live.</summary>
    Active,
    /// <summary>Its registrations are being rolled back.</summary>
    Unmounting,
    /// <summary>Unmounted; it left nothing behind.</summary>
    Unloaded,
    /// <summary>Failed to mount or unmount; see <see cref="PluginRegistrationEntry.FailureError"/>.</summary>
    Failed
}
