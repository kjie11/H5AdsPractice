using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] itemPrefabs;

    [SerializeField] private Vector3 spawnSize = new Vector3(5f, 1f, 2f);

    [SerializeField] private int spawnCount = 30;

    void Start()
    {
        SpawnItems();
    }

    void SpawnItems()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnOneItem();
        }
    }

    void SpawnOneItem()
    {
        if (itemPrefabs.Length == 0)
            return;

        GameObject prefab =
            itemPrefabs[Random.Range(0, itemPrefabs.Length)];

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnSize.x / 2f, spawnSize.x / 2f),
            Random.Range(-spawnSize.y / 2f, spawnSize.y / 2f),
            Random.Range(-spawnSize.z / 2f, spawnSize.z / 2f)
        );

        Vector3 spawnPosition = transform.position + randomOffset;

        Quaternion randomRotation = Random.rotation;

        GameObject item= Instantiate(
            prefab,
            spawnPosition,
            randomRotation
        );

        Rigidbody rb=item.GetComponent<Rigidbody>();
        if(rb!=null){
            rb.linearVelocity=Vector3.forward*200f;
        }
    }
}