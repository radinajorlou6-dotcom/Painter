using UnityEngine;

public abstract class PaintBucketScript : MonoBehaviour, IInteractable
{
    [SerializeField] protected string bucketColour;
    [SerializeField] protected string abilityToUnlock;
    protected bool isEmpty = false;
    public virtual void Interact()
    {
        if (isEmpty) return; // Prevent interaction if the bucket is already empty
        SetBucketEmpty();
        DebugUtils.Log("Interacted with paint bucket!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveBucketState(bucketColour, true);
            GameManager.Instance.UnlockAbility(abilityToUnlock);
        }
        //PLAY ANIMATION OF EMPTYING BUCKET
        // Implement the logic for what happens when the player interacts with the paint bucket
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {

        // Safety check: If you didn't manually type a color name, grab the sprite color
        if (string.IsNullOrEmpty(bucketColour))
        {
            bucketColour = GetComponent<SpriteRenderer>().color.ToString();
        }

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
