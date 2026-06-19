using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ObjectPooling : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 10;
    private Queue<GameObject> objectPool = new Queue<GameObject>();
    private bool currentlyPlaying = true;

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        currentlyPlaying = (newState == GameManager.GameState.Playing);
    }

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            if (obj.TryGetComponent(out IPoolable poolableItem))
            {
                poolableItem.AssignPool(this);
            }
            objectPool.Enqueue(obj);
        }
    }

    public GameObject SpawnFromPool(Vector3 position, Quaternion rotation)
    {
        if (!currentlyPlaying) return null;
        DebugUtils.Log("Spawning object from pool. Pool size before spawn: ");
        if (objectPool.Count == 0)
        {
            GameObject backupObj = Instantiate(prefab, transform);
            backupObj.SetActive(false);
            objectPool.Enqueue(backupObj);
            if (backupObj.TryGetComponent(out IPoolable poolableItem))
            {
                poolableItem.AssignPool(this);
            }
        }

        GameObject objectToSpawn = objectPool.Dequeue();
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.transform.SetParent(null); //unparenting so it doesnt move with character after its been shot
        objectToSpawn.SetActive(true);
        return objectToSpawn;
    }

    public void ReturnToPool(GameObject obj)
    {
        DebugUtils.Log("Returning object to pool. Pool size before return: " + objectPool.Count);
        obj.transform.SetParent(this.transform);
        obj.SetActive(false);
        objectPool.Enqueue(obj);
    }

    public IEnumerator ReturnToPoolWithDelay(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool(obj);
    }
}
