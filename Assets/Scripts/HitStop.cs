using System.Collections;
using UnityEngine;

/// <summary>
/// Brief "hit stop" freeze frames that punch up impacts (a staple of 2D action
/// game-feel). Any script can call <c>HitStop.Instance?.Freeze(seconds)</c>;
/// overlapping calls simply extend the freeze rather than stacking. Everything
/// runs on unscaled time so it still works while <see cref="Time.timeScale"/> is 0.
/// </summary>
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    private float freezeUntil;
    private Coroutine routine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>Freeze the game for a short moment. Safe to call repeatedly.</summary>
    public void Freeze(float duration)
    {
        if (duration <= 0f) return;

        // Never fight an intentional pause (the pause menu owns timeScale then).
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentState == GameManager.GameState.Paused)
        {
            return;
        }

        freezeUntil = Mathf.Max(freezeUntil, Time.unscaledTime + duration);
        if (routine == null) routine = StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        Time.timeScale = 0f;
        while (Time.unscaledTime < freezeUntil) yield return null;
        Time.timeScale = 1f;
        routine = null;
    }

    private void OnDisable()
    {
        // Safety net: never leave the game frozen if this object is torn down mid-freeze.
        if (routine != null)
        {
            Time.timeScale = 1f;
            routine = null;
        }
    }
}
