using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class ObjectPooling : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 10;
    private Queue<GameObject> objectPool = new Queue<GameObject>();

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

        Debug.Log("Spawning object from pool. Pool size before spawn: " + objectPool.Count);
        if (objectPool.Count == 0)
        {
            GameObject backupObj = Instantiate(prefab, transform);
            backupObj.SetActive(false);
            objectPool.Enqueue(backupObj);
            if (backupObj.TryGetComponent(out IPoolable poolableItem))
            {
                poolableItem.AssignPool(this);
            }
            objectPool.Enqueue(backupObj);
        }

        GameObject objectToSpawn = objectPool.Dequeue();
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.SetActive(true);
        return objectToSpawn;
    }

    public void ReturnToPool(GameObject obj)
    {
        Debug.Log("Returning object to pool. Pool size before return: " + objectPool.Count);
        obj.SetActive(false);
        objectPool.Enqueue(obj);
    }

    public IEnumerator ReturnToPoolWithDelay(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool(obj);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
