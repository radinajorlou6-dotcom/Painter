using UnityEngine;

// Singleton for sounds that aren't tied to a specific entity (UI clicks, music
// stingers, ambience). Reuses the same AudioType enum and AudioController logic
// as per-entity controllers, but there's only ever one of these and it survives
// scene loads. Usage: GlobalAudioController.Instance.Play(AudioType.UIClick);
[RequireComponent(typeof(AudioController))]
public class GlobalAudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != GetComponent<AudioController>())
        {
            Destroy(gameObject);
            return;
        }

        Instance = GetComponent<AudioController>();
        DontDestroyOnLoad(gameObject);
    }
}