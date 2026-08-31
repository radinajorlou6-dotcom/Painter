using UnityEngine;

/// <summary>
/// An interactable paint bucket. Emptying it unlocks its colour in the world and, optionally,
/// grants the player an ability. Configure the colour, whether it grants an ability, and which
/// ability via the Inspector.
/// </summary>
public class PaintBucketScript : MonoBehaviour, IInteractable
{
    [SerializeField] protected PaintColour bucketColour;
    [SerializeField] protected bool grantsAbility = false;
    [SerializeField] protected AbilityType abilityToUnlock;

    [Tooltip("Where the player reappears after dying, once this bucket has been emptied. " +
             "Defaults to the bucket's own position. Put it on solid ground — the player is " +
             "placed here directly, with no ground check.")]
    [SerializeField] protected Transform respawnPoint;

    protected bool isEmpty = false;

    public virtual void Interact()
    {
        if (isEmpty) return; // Prevent interaction if the bucket is already empty
        SetBucketEmpty();
        DebugUtils.Log("Interacted with paint bucket!");

        if (GameManager.Instance != null)
        {
            // Order matters: SaveBucketState is what fires the unlock event and triggers the
            // autosave, so everything this bucket grants has to be applied before it. The ability
            // used to be unlocked afterwards, which meant a save taken here was always one step
            // behind — dying after picking up a bucket would cost you the ability it gave.
            Vector3 point = respawnPoint != null ? respawnPoint.position : transform.position;
            GameManager.Instance.SetCheckpoint(point);

            if (grantsAbility)
            {
                GameManager.Instance.UnlockAbility(abilityToUnlock);
            }

            GameManager.Instance.SaveBucketState(bucketColour, true);
        }
        //TODO: PLAY ANIMATION OF EMPTYING BUCKET
    }

    protected virtual void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsBucketEmpty(bucketColour))
        {
            SetBucketEmpty();
        }
    }

    protected void SetBucketEmpty()
    {
        isEmpty = true;
        //TODO: PLAY ANIMATION OF EMPTYING BUCKET
    }
}
