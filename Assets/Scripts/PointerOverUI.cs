using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Answers one question reliably: is the mouse currently over a piece of UI?
///
/// EventSystem.IsPointerOverGameObject() is the usual way to ask, but this project runs
/// in "Both" input mode with the Input System's InputSystemUIInputModule driving the
/// EventSystem. That module does not register the legacy mouse pointer id the
/// parameterless overload looks for, so it can report false while the cursor is sitting
/// right on top of a slider. Raycasting the canvases directly sidesteps the mismatch and
/// gives the same answer under either input backend.
/// </summary>
public static class PointerOverUI
{
    // Both reused between calls so a per-frame check costs no allocations.
    private static readonly List<RaycastResult> Results = new List<RaycastResult>(8);
    private static PointerEventData cachedPointer;
    private static EventSystem cachedFor;

    /// <summary>True if any UI graphic sits under the given screen position.</summary>
    public static bool AtScreenPosition(Vector2 screenPosition)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return false;

        PointerEventData pointer = GetPointer(events, screenPosition);

        Results.Clear();
        events.RaycastAll(pointer, Results);
        return Results.Count > 0;
    }

    /// <summary>True if any UI graphic sits under the mouse right now.</summary>
    public static bool AtMouse() => AtScreenPosition(Input.mousePosition);

    private static PointerEventData GetPointer(EventSystem events, Vector2 screenPosition)
    {
        // The EventSystem can be swapped out between scenes, so rebuild if it changed.
        if (cachedPointer == null || cachedFor != events)
        {
            cachedPointer = new PointerEventData(events);
            cachedFor = events;
        }

        cachedPointer.position = screenPosition;
        return cachedPointer;
    }
}
