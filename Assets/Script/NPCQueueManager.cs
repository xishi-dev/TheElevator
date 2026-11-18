using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCQueueManager : MonoBehaviour
{
    [Header("NPC Prefabs")]
    public GameObject[] npcPrefabs;
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    [Min(.1f)] public float spawnInterval = 2f;
    [Min(1)] public int maxNPCs = 10;
    [Tooltip("Maximum living NPCs created by this manager, including those removed from the queue.")]
    [Min(1)] public int maxSpawnedNPCs = 24;
    [Header("Queue Settings")]
    public Transform queueStart;
    public Vector3 queueDirection = Vector3.left;
    [Min(.1f)] public float spacing = 1.2f;
    private readonly List<NPCQueueAgent> npcList = new List<NPCQueueAgent>();
    private readonly List<NPCQueueAgent> spawnedNPCs = new List<NPCQueueAgent>();
    private Coroutine spawnRoutine;
    private bool hasStarted;
    public int QueuedNPCCount { get { Prune(); return npcList.Count; } }
    public int SpawnedNPCCount { get { Prune(); return spawnedNPCs.Count; } }

    private void Start() { hasStarted = true; StartSpawning(); }
    private void OnEnable() { if (hasStarted) StartSpawning(); }
    private void OnDisable()
    {
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }
    private void StartSpawning()
    {
        if (spawnRoutine != null) return;
        if (spawnPoint == null || queueStart == null || npcPrefabs == null || System.Array.Find(npcPrefabs, p => p != null) == null)
        {
            Debug.LogError("Assign a valid NPC prefab, Spawn Point and Queue Start.", this);
            enabled = false;
            return;
        }
        spawnRoutine = StartCoroutine(SpawnNPCs());
    }
    private IEnumerator SpawnNPCs()
    {
        while (true)
        {
            TrySpawnNPC();
            yield return new WaitForSeconds(Mathf.Max(.1f, spawnInterval));
        }
    }
    public bool TrySpawnNPC()
    {
        Prune();
        if (spawnPoint == null || queueStart == null || npcPrefabs == null || npcPrefabs.Length == 0 ||
            npcList.Count >= Mathf.Max(1, maxNPCs) || spawnedNPCs.Count >= Mathf.Max(1, maxSpawnedNPCs)) return false;
        // Skip missing entries instead of silently losing a random spawn.
        int start = Random.Range(0, npcPrefabs.Length);
        GameObject prefab = null;
        for (int offset = 0; offset < npcPrefabs.Length; offset++)
        {
            prefab = npcPrefabs[(start + offset) % npcPrefabs.Length];
            if (prefab != null) break;
        }
        if (prefab == null) return false;
        GameObject npc = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        NPCQueueAgent agent = npc.GetComponent<NPCQueueAgent>();
        if (agent == null) agent = npc.AddComponent<NPCQueueAgent>();
        spawnedNPCs.Add(agent);
        npcList.Add(agent);
        UpdateQueuePositions();
        return true;
    }
    private void Update() { UpdateQueuePositions(); }
    private void Prune()
    {
        npcList.RemoveAll(npc => npc == null || !npc.IsInQueue);
        spawnedNPCs.RemoveAll(npc => npc == null);
    }
    private void UpdateQueuePositions()
    {
        Prune();
        if (queueStart == null) return;
        Vector3 direction = Vector3.ProjectOnPlane(queueDirection, Vector3.up);
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.left;
        for (int i = 0; i < npcList.Count; i++)
            npcList[i].SetTarget(queueStart.position + direction * Mathf.Max(.1f, spacing) * i);
    }
    public void RemoveFromQueue(NPCQueueAgent agent)
    {
        if (agent != null && npcList.Remove(agent))
        {
            agent.LeaveQueue();
            UpdateQueuePositions();
        }
    }
}
