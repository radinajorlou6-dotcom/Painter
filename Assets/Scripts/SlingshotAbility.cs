using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class SlingshotAbility : MonoBehaviour
{
    [Header("Slingshot Settings")]
    [Tooltip("Maximum distance the player can drag before the launch vector clamps.")]
    [SerializeField] private float maxDragDistance = 3f;
    [Tooltip("Multiplier applied to the final launch vector.")]
    [SerializeField] private float launchPower = 10f;
    [Tooltip("Length of each arrowhead wing in world units.")]
    [SerializeField] private float arrowheadLength = 0.3f;
    [Tooltip("Angle of the arrowhead wings in degrees.")]
    [SerializeField] private float arrowheadAngle = 25f;

    [Header("Preview Line")]
    [SerializeField] private LineRenderer previewLine;
    [SerializeField] private Color previewColor = new Color(0.2f, 0.9f, 1f, 0.8f);
    [SerializeField] private float previewWidth = 0.08f;

    private Camera mainCamera;
    private PlayerMovement playerMovementScript;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private Vector2 dragStart;
    private Vector2 dragCurrent;
    private bool isDragging;
    [SerializeField] private int maxSlingshotUses = 1;
    private int slingshotsLeft;
    private bool wasGroundedLastFrame = false;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            DebugUtils.LogError("SlingshotAbility requires a Rigidbody2D on the same GameObject.");
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            DebugUtils.LogError("SlingshotAbility could not find a Camera.main.");
        }

        playerMovement = GetComponent<PlayerMovement>();

        if (previewLine == null)
        {
            CreatePreviewLine();
        }
        ConfigurePreviewLine();
        DisablePreviewLine();
    }
    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        slingshotsLeft = maxSlingshotUses;
        if (playerMovement == null)
        {
            DebugUtils.LogError("SlingshotAbility requires a PlayerMovement component on the same GameObject.");
        }
    }

    private void Update()
    {
        // Reset slingshot uses only once when landing (transition from not grounded to grounded)
        if (playerMovement.isGrounded && !wasGroundedLastFrame && playerMovement.isGroundedOn != "Drawn Platforms")
        {
            slingshotsLeft = maxSlingshotUses;
        }
        wasGroundedLastFrame = playerMovement.isGrounded;
        
        if (!isDragging) return;

        dragCurrent = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 launchVector = CalculateLaunchVector(dragStart, dragCurrent);

        Vector2 previewStart = transform.position;
        Vector2 previewEnd = previewStart + launchVector;

        DrawPreviewLine(previewStart, previewEnd, launchVector.normalized);
    }

    public void OnSlingshotClick(InputAction.CallbackContext context)
    {
        if (!GameManager.Instance.unlockedAbilities.ContainsKey("Slingshot") || !GameManager.Instance.unlockedAbilities["Slingshot"]) return;
        if (context.started)
        {
            if (!IsShiftHeld()) return;
            if (slingshotsLeft <= 0) return;

            dragStart = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            isDragging = true;
            EnablePreviewLine();
        }

        if (context.canceled && isDragging)
        {
            slingshotsLeft--;
            dragCurrent = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 launchVector = CalculateLaunchVector(dragStart, dragCurrent);
            Launch(launchVector);
            StopDragging();
        }
    }

    private Vector2 CalculateLaunchVector(Vector2 start, Vector2 current)
    {
        Vector2 raw = start - current;
        return Vector2.ClampMagnitude(raw, maxDragDistance);
    }

    private void Launch(Vector2 launchVector)
    {
        if (rb == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(launchVector * launchPower, ForceMode2D.Impulse);

    }

    private void StopDragging()
    {
        isDragging = false;
        DisablePreviewLine();
    }

    private void CreatePreviewLine()
    {
        GameObject previewObject = new GameObject("SlingshotPreviewLine");
        previewObject.transform.SetParent(transform);
        previewLine = previewObject.AddComponent<LineRenderer>();
    }

    private void ConfigurePreviewLine()
    {
        previewLine.positionCount = 5;
        previewLine.useWorldSpace = true;
        previewLine.loop = false;
        previewLine.startWidth = previewWidth;
        previewLine.endWidth = previewWidth;
        previewLine.material = new Material(Shader.Find("Sprites/Default"));
        previewLine.startColor = previewColor;
        previewLine.endColor = previewColor;
        previewLine.numCapVertices = 4;
    }

    private void EnablePreviewLine()
    {
        if (previewLine != null)
        {
            previewLine.enabled = true;
        }
    }

    private void DisablePreviewLine()
    {
        if (previewLine != null)
        {
            previewLine.enabled = false;
        }
    }

    private void DrawPreviewLine(Vector2 start, Vector2 end, Vector2 direction)
    {
        if (previewLine == null) return;

        Vector2 clampedDirection = direction.normalized;
        Vector2 arrowBase = end;

        Vector2 leftWing = RotateVector(-clampedDirection, arrowheadAngle) * arrowheadLength;
        Vector2 rightWing = RotateVector(-clampedDirection, -arrowheadAngle) * arrowheadLength;

        previewLine.positionCount = 5;
        previewLine.SetPosition(0, start);
        previewLine.SetPosition(1, end);
        previewLine.SetPosition(2, end + leftWing);
        previewLine.SetPosition(3, end);
        previewLine.SetPosition(4, end + rightWing);
    }

    private bool IsShiftHeld()
    {
        return Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    private Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }
}
