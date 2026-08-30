using UnityEngine;

/// <summary>
/// A trigger volume that reports whether the player is standing in it, so a prompt can use a
/// Zone condition. Put one at the mouth of a room to explain something the moment the player
/// arrives, rather than tying the prompt to an ability or a counter.
///
/// Both show and hide can point at zones: "show while in the ledge zone, hide once they've
/// jumped" is a show-Zone plus a hide-Signal.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialZone : MonoBehaviour
{
    [Tooltip("Name a prompt's Zone condition points at. Must match exactly.")]
    [SerializeField] private string zoneId;

    private void Reset()
    {
        // Trigger volumes only — a solid one would shove the player off the ledge it's explaining.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        TutorialManager.Instance?.SetZoneOccupied(zoneId, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        TutorialManager.Instance?.SetZoneOccupied(zoneId, false);
    }

    private void OnDisable()
    {
        // Leaving the flag set would strand a prompt on screen if the zone is disabled or the
        // scene unloads while the player is still inside it.
        TutorialManager.Instance?.SetZoneOccupied(zoneId, false);
    }
}
