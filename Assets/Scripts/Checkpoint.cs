using UnityEngine;

/// <summary>
/// A respawn point. When the player walks into its trigger, this becomes the active
/// checkpoint the player will respawn at after dying. Needs a Collider2D set as a trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Where the player reappears. Defaults to this object's position if left empty.")]
    [SerializeField] private Transform respawnPoint;

    // Runs in the editor when the component is added: make the collider a trigger by default.
    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        GameManager.Instance?.SetCheckpoint(position);
    }
}
