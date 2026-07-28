using UnityEngine;
using System.Collections.Generic;

public enum AudioType
{
    //Needed by player
    Idle, GotHurt, Died, Move, Jump, Fall, Slash, Stab, ShieldDraw, ShieldBreak, ProjectileShoot,
    ProjectileHit, PlatformDraw, Slingshot,

    //Needed by BaseEnemy
    Explode, GroundSmuch,

    //Needed by KnightEnemy
    Charge, Bash, ChargeUp,
}

// Attach one of these to each entity that makes sound (Player, KnightEnemy, etc).
// Each entity owns its own AudioMap, so a shared AudioType like "Move" can sound
// different per entity. For sounds that aren't tied to a specific entity
// (UI, music, ambience) use GlobalAudioController instead.
public class AudioController : MonoBehaviour
{
    [System.Serializable]
    private struct AudioEntry
    {
        public AudioType Type;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume;
        public float Pitch;
        public bool Loop;
        public bool is3D; //just means it varies by distance
        public float minDistance; //At which distance should sound be 100%
        public float maxDistance; //At which distance does sound fully disappear
    }

    [Header("AudioMap")]
    [SerializeField] private List<AudioEntry> audios = new List<AudioEntry>();

    [Header("Source Pool")]
    [SerializeField] private int poolSize = 4;

    private Dictionary<AudioType, AudioEntry> audioLookup;
    private List<AudioSource> sourcePool;
    private Dictionary<AudioType, AudioSource> activeLoops = new Dictionary<AudioType, AudioSource>();

    private void Awake()
    {
        audioLookup = new Dictionary<AudioType, AudioEntry>();
        foreach (var entry in audios)
        {
            audioLookup[entry.Type] = entry;
        }

        sourcePool = new List<AudioSource>(poolSize);
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sourcePool.Add(source);
        }
    }

    public void Play(AudioType type)
    {
        Play(type, Vector3.zero, false, null);
    }

    public void Play(AudioType type, Vector3 position)
    {
        Play(type, position, true, null);
    }

    // pitchOverride lets a caller scale playback pitch at runtime (e.g. fall speed)
    // without needing a separate AudioEntry per variation.
    public void Play(AudioType type, float pitchOverride)
    {
        Play(type, Vector3.zero, false, pitchOverride);
    }

    private void Play(AudioType type, Vector3 position, bool useWorldPosition, float? pitchOverride)
    {
        if (!audioLookup.TryGetValue(type, out AudioEntry entry))
        {
            Debug.LogWarning($"AudioController ({name}): no entry configured for {type}");
            return;
        }

        if (entry.Clip == null)
        {
            Debug.LogWarning($"AudioController ({name}): entry for {type} has no clip assigned");
            return;
        }

        AudioSource source = GetFreeSource();
        if (source == null) return;

        source.clip = entry.Clip;
        source.volume = entry.Volume;
        source.pitch = pitchOverride ?? entry.Pitch;
        source.loop = entry.Loop;

        if (entry.is3D && useWorldPosition)
        {
            source.spatialBlend = 1f;
            source.minDistance = entry.minDistance;
            source.maxDistance = entry.maxDistance;
            source.transform.position = position;
        }
        else
        {
            source.spatialBlend = 0f;
        }

        source.Play();
    }

    public void Stop(AudioType type)
    {
        if (!audioLookup.TryGetValue(type, out AudioEntry entry)) return;

        foreach (var source in sourcePool)
        {
            if (source.clip == entry.Clip && source.isPlaying)
            {
                source.Stop();
            }
        }

        StopLoop(type);
    }

    // Starts a continuous loop for this type if one isn't already running, claiming
    // a pooled source for as long as the loop lasts. Call UpdateLoop each frame after
    // to live-tweak volume/pitch (e.g. based on fall speed), and StopLoop when done.
    public void StartLoop(AudioType type)
    {
        if (activeLoops.ContainsKey(type)) return;

        if (!audioLookup.TryGetValue(type, out AudioEntry entry) || entry.Clip == null)
        {
            Debug.LogWarning($"AudioController ({name}): no valid entry for loop {type}");
            return;
        }

        AudioSource source = GetFreeSource();
        if (source == null) return;

        source.clip = entry.Clip;
        source.volume = entry.Volume;
        source.pitch = entry.Pitch;
        source.loop = true;
        source.spatialBlend = 0f;
        source.Play();

        activeLoops[type] = source;
    }

    // Live-updates an already-started loop's volume and/or pitch. No-op if the loop isn't running.
    public void UpdateLoop(AudioType type, float? volume = null, float? pitch = null)
    {
        if (!activeLoops.TryGetValue(type, out AudioSource source)) return;

        if (volume.HasValue) source.volume = volume.Value;
        if (pitch.HasValue) source.pitch = pitch.Value;
    }

    public void StopLoop(AudioType type)
    {
        if (!activeLoops.TryGetValue(type, out AudioSource source)) return;

        source.Stop();
        source.loop = false;
        activeLoops.Remove(type);
    }

    private AudioSource GetFreeSource()
    {
        foreach (var source in sourcePool)
        {
            if (!source.isPlaying) return source;
        }

        // All busy: steal the oldest one (index 0) rather than dropping the sound
        AudioSource stolen = sourcePool[0];
        sourcePool.RemoveAt(0);
        sourcePool.Add(stolen);
        return stolen;
    }
}