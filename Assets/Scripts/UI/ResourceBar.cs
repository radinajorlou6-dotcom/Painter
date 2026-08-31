using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One placeholder bar. Deliberately dumb: it knows how to draw a 0..1 fill and nothing about what
/// the number means, so the same component serves health, ink, shield and cooldown, and swapping in
/// real art later is a change to the Image rather than to any gameplay code.
///
/// Set it up as a filled Image (Image Type = Filled, Fill Method = Horizontal) and drop that Image
/// into Fill.
/// </summary>
public class ResourceBar : MonoBehaviour
{
    [Tooltip("An Image with Image Type set to Filled. Its fillAmount is what this drives.")]
    [SerializeField] private Image fill;

    [Tooltip("Optional. Hidden entirely when the thing it tracks doesn't exist — a boss bar with " +
             "no boss, or an ability that hasn't been unlocked yet.")]
    [SerializeField] private GameObject container;

    [Header("Colour")]
    [Tooltip("Tint at full. Leave both the same to keep one flat colour.")]
    [SerializeField] private Color fullColour = Color.white;
    [Tooltip("Tint at empty. Health bars read well going from full to a warning colour.")]
    [SerializeField] private Color emptyColour = Color.white;

    [Header("Feel")]
    [Tooltip("Seconds for the bar to catch up to a new value. 0 snaps. A little smoothing hides " +
             "the single-frame jumps you get from ink and shield changes.")]
    [SerializeField] private float smoothTime = 0.08f;

    private float displayed;
    private float velocity;
    private bool primed;

    /// <summary>
    /// Sets the bar from a current/max pair. Guards a zero max, which is what an uninitialised
    /// Health or an unassigned resource looks like and would otherwise be a NaN fill.
    /// </summary>
    public void SetValue(float current, float max)
    {
        SetFraction(max > 0f ? current / max : 0f);
    }

    public void SetFraction(float fraction)
    {
        Show(true);

        fraction = Mathf.Clamp01(fraction);

        // First value is taken whole: easing up from zero on the frame the HUD appears would look
        // like the player spawning with no health.
        if (!primed)
        {
            displayed = fraction;
            primed = true;
        }
        else
        {
            displayed = smoothTime <= 0f
                ? fraction
                : Mathf.SmoothDamp(displayed, fraction, ref velocity, smoothTime);
        }

        if (fill == null) return;

        fill.fillAmount = displayed;
        fill.color = Color.Lerp(emptyColour, fullColour, displayed);
    }

    /// <summary>Hides the whole bar — for a boss that isn't in the scene, or a locked ability.</summary>
    public void Show(bool visible)
    {
        GameObject target = container != null ? container : gameObject;
        if (target.activeSelf != visible) target.SetActive(visible);
    }
}
