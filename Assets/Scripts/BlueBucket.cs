using UnityEngine;

public class BlueBucket : PaintBucketScript
{
    public override void Interact()
    {
        if (isEmpty) return; // Prevent interaction if the bucket is already empty
        base.Interact();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnlockAbility("Slingshot");
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        bucketColour = "blue";
        base.Start();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void SetBucketEmpty()
    {
        isEmpty = true;
        //PLAY ANIMATION OF EMPTYING BUCKET
    }
}
