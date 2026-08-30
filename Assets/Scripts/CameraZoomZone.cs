using UnityEngine;

/// <summary>
/// A trigger volume that pulls the camera in or pushes it out by a fixed amount while the player is
/// inside it. Box one over a corridor at 0.6 and the walls close in on the approach; the room at
/// the end needs nothing, because leaving the zone eases back to the level's authored framing.
///
/// Use this when you want to dictate the framing. When you'd rather the space decide it for you,
/// use <see cref="CameraRoom"/> instead — it sizes the camera to the room's walls and stops it
/// panning past them.
/// </summary>
public class CameraZoomZone : CameraFramingZone
{
    [Header("Zoom")]
    [Tooltip("Framing while the player is inside, as a fraction of the level's default camera size.\n\n" +
             "Below 1 zooms in — 0.6 is a snug corridor.\n" +
             "Above 1 zooms out — 1.4 opens up for a big reveal.\n\n" +
             "A fraction rather than an absolute size, so a zone reads the same in any scene: the " +
             "levels don't agree on default framing (10.21 against 8.06).")]
    [SerializeField] private float zoomMultiplier = 0.6f;

    public override float GetTargetSize(float defaultSize, float aspect)
    {
        return defaultSize * zoomMultiplier;
    }
}
