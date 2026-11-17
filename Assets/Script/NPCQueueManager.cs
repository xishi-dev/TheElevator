
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCQueueManager : MonoBehaviour
{
    [Header("NPC Prefabs")]
    public GameObject[] npcPrefabs;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public float spawnInterval = 2f;
    public int maxNPCs = 10;

    [Header("Queue Settings")]
    public Transform queueStart;
    public Vector3 queueDirection = Vector3.left;
    public float spacing = 1.2f;

    private List<NPCQueueAgent> npcList =
        new List<NPCQueueAgent>();

    private void Start()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0 ||
            spawnPoint == null || queueStart == null)
        {
            Debug.LogError(
                "กรุณากำหนด NPC Prefabs และจุด Spawn/Queue"
            );

            enabled = false;
            return;
        }

        StartCoroutine(SpawnNPCs());
    }

    // ระบบ Spawn NPC อัตโนมัติ
    private IEnumerator SpawnNPCs()
    {
        while (true)
        {
            npcList.RemoveAll(npc => npc == null);

            if (npcList.Count < maxNPCs)
            {
                CreateNPC();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // สร้าง NPC
    private void CreateNPC()
    {
        int randomIndex = Random.Range(
            0,
            npcPrefabs.Length
        );

        GameObject selectedPrefab = npcPrefabs[randomIndex];

        if (selectedPrefab == null)
            return;

        GameObject npc = Instantiate(
            selectedPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        NPCQueueAgent agent =
            npc.GetComponent<NPCQueueAgent>();

        if (agent == null)
        {
            agent = npc.AddComponent<NPCQueueAgent>();
        }

        npcList.Add(agent);

        UpdateQueuePositions();
    }

    private void Update()
    {
        UpdateQueuePositions();
    }

    // จัดตำแหน่ง NPC ในแถว
    private void UpdateQueuePositions()
    {
        Vector3 direction = queueDirection.normalized;

        for (int i = 0; i < npcList.Count; i++)
        {
            if (npcList[i] == null)
                continue;

            Vector3 targetPosition =
                queueStart.position + direction * spacing * i;

            npcList[i].SetTarget(targetPosition);
        }
    }

    // นำ NPC ออกจากแถวเมื่อถูกจับ
    public void RemoveFromQueue(NPCQueueAgent agent)
    {
        if (agent == null)
            return;

        if (npcList.Remove(agent))
        {
            agent.LeaveQueue();

            UpdateQueuePositions();

            Debug.Log("NPC ถูกนำออกจาก Queue แล้ว");
        }
    }
}
