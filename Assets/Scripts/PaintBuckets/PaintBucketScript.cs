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
    protected bool isEmpty = false;

    public virtual void Interact()
    {
        if (isEmpty) return; // Prevent interaction if the bucket is already empty
        SetBucketEmpty();
        DebugUtils.Log("Interacted with paint bucket!");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveBucketState(bucketColour, true);
            if (grantsAbility)
            {
                GameManager.Instance.UnlockAbility(abilityToUnlock);
            }
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
