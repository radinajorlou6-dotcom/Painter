using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A tilemap that is "greyed out" until its colour is unlocked. When the matching colour is
/// unlocked (via a paint bucket), it recolours and its collider turns off so the player can
/// pass through. Configure which colour this responds to via the Inspector dropdown.
/// </summary>
public class ColourControl : MonoBehaviour
{
    protected Rigidbody2D rb;
    protected Tilemap tilemap;
    [SerializeField] protected Color unlockedColour; //The colour of the object after being unlocked
    [SerializeField] protected PaintColour colour;   //Which colour unlock this object reacts to
    protected TilemapCollider2D tilemapCollider;

    protected virtual void OnEnable()
    {
        GameManager.OnColourUnlocked += HandleColourUnlocked;
    }

    protected virtual void OnDisable()
    {
        GameManager.OnColourUnlocked -= HandleColourUnlocked;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        tilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<TilemapCollider2D>();
        if (GameManager.Instance != null && GameManager.Instance.IsBucketEmpty(colour))
        {
            Unlock();
        }
    }

    /// <summary>
    /// Disables the blocking collider. Tinting is deliberately not done here — <see cref="ColourReveal"/>
    /// owns how a locked thing looks, so that the drained state, the fade and the restored colour
    /// are the same code for a tilemap, a background and an enemy. Two components writing the same
    /// tint would only fight.
    /// </summary>
    protected virtual void Unlock()
    {
        tilemapCollider.enabled = false;
    }

    /// <summary>Event handler: only reacts when the unlocked colour matches ours.</summary>
    protected virtual void HandleColourUnlocked(PaintColour colourName)
    {
        if (colourName == colour)
        {
            Unlock();
        }
    }
}
