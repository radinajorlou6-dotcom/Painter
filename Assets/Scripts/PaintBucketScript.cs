using UnityEngine;

public abstract class PaintBucketScript : MonoBehaviour, IInteractable
{
    [SerializeField] protected string bucketColour;
    protected bool isEmpty = false;
    public virtual void Interact()
    {
        if (isEmpty) return; // Prevent interaction if the bucket is already empty
        SetBucketEmpty();
        Debug.Log("Interacted with paint bucket!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveBucketState(bucketColour, true);
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

    // Update is called once per frame
    void Update()
    {
        
    }

    protected void SetBucketEmpty()
    {
        isEmpty = true;
        //TODO: PLAY ANIMATION OF EMPTYING BUCKET
    }
}
