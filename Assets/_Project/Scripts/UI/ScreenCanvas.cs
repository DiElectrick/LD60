using UnityEngine;

/// <summary>
/// Finds the screen-space canvas the module UI is drawn on.
/// <see cref="Object.FindAnyObjectByType{T}"/> alone is not enough: enemy health bars are
/// world-space canvases, so a lookup can land on one of those instead of the HUD.
/// </summary>
public static class ScreenCanvas
{
    /// <summary>Root screen-space canvas (overlay or camera), or null when there is none.</summary>
    public static Canvas Find()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);

        foreach (var candidate in canvases)
        {
            if (candidate != null && candidate.isRootCanvas && candidate.renderMode != RenderMode.WorldSpace)
            {
                return candidate;
            }
        }

        // Fallback: any screen-space canvas, then anything at all.
        foreach (var candidate in canvases)
        {
            if (candidate != null && candidate.renderMode != RenderMode.WorldSpace) return candidate;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }
}
