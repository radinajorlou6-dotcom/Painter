using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class ColourControl : MonoBehaviour
{
    protected Rigidbody2D rb;
    protected Tilemap tilemap;
    [SerializeField] protected Color unlockedColour; //The colour of the object after being unlocked
    [SerializeField] protected string colour; //The name of the colour that will be used to check if the player has unlocked it
    protected TilemapCollider2D tilemapCollider;
    
    protected virtual void OnEnable()
    {
        GameManager.OnColourUnlocked += UnlockColour;
    }

    protected virtual void OnDisable()
    {
        GameManager.OnColourUnlocked -= UnlockColour;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        tilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<TilemapCollider2D>();
        if (GameManager.Instance != null && GameManager.Instance.IsBucketEmpty(colour))
        {
            UnlockColour();
        }
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    protected virtual void UnlockColour()
    {
        tilemapCollider.enabled = false;
        tilemap.color = unlockedColour;
    }

    protected virtual void UnlockColour(string colourName)
    {
        if (colourName == colour)
        {
            UnlockColour();
        }
    }
}
