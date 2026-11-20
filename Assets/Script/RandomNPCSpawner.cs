
using UnityEngine;

public class RandomNPCSpawner : MonoBehaviour
{
    [Header("NPC Prefab")]
    public GameObject npcPrefab;

    [Header("NPC Amount")]
    public int minNPC = 2;
    public int maxNPC = 8;

    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    private void Start()
    {
        SpawnNPCs();
    }

    void SpawnNPCs()
    {
        if (npcPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("???????? NPC Prefab ??? Spawn Points");
            return;
        }

        int maxSpawn = Mathf.Min(maxNPC, spawnPoints.Length);
        int minSpawn = Mathf.Clamp(minNPC, 0, maxSpawn);

        int amount = Random.Range(minSpawn, maxSpawn + 1);

        Debug.Log("???? NPC ?????: " + amount);

        // ??????????????????????????????
        Transform[] shuffled = (Transform[])spawnPoints.Clone();

        for (int i = 0; i < shuffled.Length; i++)
        {
            int randomIndex = Random.Range(i, shuffled.Length);
            Transform temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }

        for (int i = 0; i < amount; i++)
        {
            Instantiate(
                npcPrefab,
                shuffled[i].position,
                shuffled[i].rotation
            );
        }
    }
}
