using System.Threading.Tasks;

namespace FryWorld.Sdk.Services;

/// <summary>
/// Lets a plugin drive the host's shell: open its own page, show or hide its overlays, and open its dialogs,
/// without knowing anything about the host's view models. Every FryWorld host and the standalone runner
/// provide it; get it with <c>ctx.GetService&lt;IShellService&gt;()</c>.
/// </summary>
/// <remarks>
/// Ids are the ones in the descriptors the plugin registered (<c>NavigationItemDescriptor.Id</c>,
/// <c>OverlayDescriptor.Id</c>, <c>DialogDescriptor.Id</c>). Methods can be called from any thread; the
/// host does the UI work on its UI thread. The boolean results say whether the id is registered, so a
/// call made from a background thread returns before the UI has changed.
/// </remarks>
public interface IShellService
{
    /// <summary>Shows the full-viewport page registered with <paramref name="navigationItemId"/>.</summary>
    /// <returns>False if no page with that id is registered.</returns>
    bool NavigateTo(string navigationItemId);

    /// <summary>Opens the overlay registered with <paramref name="overlayId"/>, or brings it to the front if already open.</summary>
    /// <returns>False if no overlay with that id is registered.</returns>
    bool ShowOverlay(string overlayId);

    /// <summary>Closes the overlay <paramref name="overlayId"/> if it is open.</summary>
    /// <returns>False if no overlay with that id is registered.</returns>
    bool HideOverlay(string overlayId);

    /// <summary>Opens the overlay <paramref name="overlayId"/> if it is closed, and closes it if it is open.</summary>
    /// <returns>False if no overlay with that id is registered.</returns>
    bool ToggleOverlay(string overlayId);

    /// <summary>Whether the overlay <paramref name="overlayId"/> is currently open.</summary>
    bool IsOverlayVisible(string overlayId);

    /// <summary>
    /// Opens the modal dialog registered with <paramref name="dialogId"/> and completes when it closes.
    /// </summary>
    /// <returns>False if no dialog with that id is registered.</returns>
    Task<bool> ShowDialogAsync(string dialogId);
}
