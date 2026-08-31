using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// The key the boss drops. Collecting it ends the level and takes the player to the victory scene.
///
/// Works both ways the project already collects things: walk over it, or stand near it and press
/// Interact. Walk-over is the default because a boss drop is a reward, not a puzzle — but the
/// <see cref="IInteractable"/> path costs nothing and means the key still works if you'd rather put
/// it on the Interactable layer and have the player press for it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour, IInteractable
{
    [Header("Destination")]
    [Tooltip("Scene loaded once the key is collected. Must be in File > Build Settings.")]
    [SerializeField] private string victorySceneName = "Victory";

    [Header("Collection")]
    [Tooltip("Collect it by walking into it. Untick to require the Interact key, in which case " +
             "the key also needs to be on the Interactable layer for CombatInput to find it.")]
    [SerializeField] private bool autoPickupOnTouch = true;

    [Tooltip("Seconds to fade to black before the victory scene loads. 0 cuts straight there.")]
    [SerializeField] private float fadeDuration = 0.5f;

    [Tooltip("Write the save before leaving, so beating the boss survives even if the player " +
             "quits from the victory screen.")]
    [SerializeField] private bool saveBeforeLeaving = true;

    [Header("Events")]
    [Tooltip("Fires the moment it is collected, before the fade — hook up audio, VFX or a flourish.")]
    public UnityEvent OnCollected;

    private bool collected;

    private void Reset()
    {
        // Walk-over pickup needs a trigger; a solid key would just be an obstacle.
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        // Caught here rather than at the moment of pickup, because by then the player has beaten
        // the boss and a missing scene would strand them with nothing to do.
        if (string.IsNullOrEmpty(victorySceneName) || !Application.CanStreamedLevelBeLoaded(victorySceneName))
        {
            DebugUtils.LogError(
                $"KeyPickup on '{name}' points at scene '{victorySceneName}', which isn't in Build " +
                "Settings. Collecting the key will do nothing until it's added.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoPickupOnTouch) return;
        if (!other.CompareTag("Player")) return;

        Collect();
    }

    /// <summary>The Interact-key path, for when auto pickup is off.</summary>
    public void Interact()
    {
        Collect();
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;

        DebugUtils.Log($"Key '{name}' collected — heading to {victorySceneName}");
        OnCollected?.Invoke();

        // Stop it firing twice if the player is overlapping more than one collider on it, without
        // hiding it — whatever OnCollected kicked off should still be visible through the fade.
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

        StartCoroutine(GoToVictory());
    }

    private IEnumerator GoToVictory()
    {
        if (saveBeforeLeaving) GameManager.Instance?.SaveGame();

        // Unscaled inside ScreenFader, so this still plays out if anything has frozen time.
        if (ScreenFader.Instance != null && fadeDuration > 0f)
        {
            yield return ScreenFader.Instance.FadeOut(fadeDuration);
        }

        if (!string.IsNullOrEmpty(victorySceneName) && Application.CanStreamedLevelBeLoaded(victorySceneName))
        {
            SceneManager.LoadScene(victorySceneName);
        }
    }
}
