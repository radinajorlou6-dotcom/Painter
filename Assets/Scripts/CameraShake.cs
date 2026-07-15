using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Handles camera shake independently of the game's timescale using Cinemachine 3.
/// Must be attached to the CinemachineCamera GameObject (the Virtual Camera).
/// </summary>
public class CameraShake : CinemachineExtension
{
    public static CameraShake Instance { get; private set; }

    private float shakeDuration = 0f;
    private float shakeIntensity = 0f;
    private Vector3 shakeOffset = Vector3.zero;

    protected override void Awake()
    {
        base.Awake();
        
        if (Instance != null && Instance != this)
        {
            // Only remove the duplicate component, never the camera GameObject it lives on.
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Triggers a screen shake.
    /// </summary>
    /// <param name="intensity">The maximum offset in units.</param>
    /// <param name="duration">Duration of the shake in seconds.</param>
    public void Shake(float intensity, float duration)
    {
        if (shakeDuration > 0f)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }
        else
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize)
        {
            if (shakeDuration > 0f)
            {
                shakeDuration -= Time.unscaledDeltaTime;
                
                if (shakeDuration <= 0f)
                {
                    shakeOffset = Vector3.zero;
                }
                else
                {
                    Vector2 randomPoint = Random.insideUnitCircle * shakeIntensity;
                    shakeOffset = new Vector3(randomPoint.x, randomPoint.y, 0f);
                }
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            state.RawPosition += shakeOffset;
        }
    }
}

