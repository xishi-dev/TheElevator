
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCQueueManager : MonoBehaviour
{
    [Header("NPC Prefabs")]
    public GameObject[] npcPrefabs;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    [Min(0.1f)] public float spawnInterval = 2f;
    [Min(1)] public int maxNPCs = 10;
    [Min(1)] public int maxSpawnedNPCs = 24;

    [Header("Queue Settings")]
    public Transform queueStart;
    public Vector3 queueDirection = Vector3.left;
    [Min(0.1f)] public float spacing = 1.2f;

    private readonly List<NPCQueueAgent> npcList =
        new List<NPCQueueAgent>();

    private readonly List<NPCQueueAgent> spawnedNPCs =
        new List<NPCQueueAgent>();

    private Coroutine spawnRoutine;
    private bool hasStarted;

    public int QueuedNPCCount
    {
        get
        {
            Prune();
            return npcList.Count;
        }
    }

    public int SpawnedNPCCount
    {
        get
        {
            Prune();
            return spawnedNPCs.Count;
        }
    }

    private void Start()
    {
        hasStarted = true;
        StartSpawning();
    }

    private void OnEnable()
    {
        if (hasStarted)
            StartSpawning();
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void StartSpawning()
    {
        if (spawnRoutine != null)
            return;

        if (spawnPoint == null ||
            queueStart == null ||
            npcPrefabs == null ||
            npcPrefabs.Length == 0)
        {
            Debug.LogError(
                "NPC Queue setup is incomplete.", this
            );
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

            yield return new WaitForSeconds(
                Mathf.Max(0.1f, spawnInterval)
            );
        }
    }

    public bool TrySpawnNPC()
    {
        Prune();

        if (spawnPoint == null ||
            queueStart == null ||
            npcPrefabs == null ||
            npcPrefabs.Length == 0 ||
            npcList.Count >= maxNPCs ||
            spawnedNPCs.Count >= maxSpawnedNPCs)
        {
            return false;
        }

        int start = Random.Range(0, npcPrefabs.Length);
        GameObject selectedPrefab = null;

        for (int i = 0; i < npcPrefabs.Length; i++)
        {
            GameObject candidate =
                npcPrefabs[(start + i) % npcPrefabs.Length];

            if (candidate != null)
            {
                selectedPrefab = candidate;
                break;
            }
        }

        if (selectedPrefab == null)
            return false;

        GameObject npc = Instantiate(
            selectedPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        NPCQueueAgent agent =
            npc.GetComponent<NPCQueueAgent>();

        if (agent == null)
            agent = npc.AddComponent<NPCQueueAgent>();

        spawnedNPCs.Add(agent);
        npcList.Add(agent);

        UpdateQueuePositions();
        return true;
    }

    private void Update()
    {
        UpdateQueuePositions();
    }

    private void Prune()
    {
        npcList.RemoveAll(
            npc => npc == null || !npc.IsInQueue
        );

        spawnedNPCs.RemoveAll(npc => npc == null);
    }

    private void UpdateQueuePositions()
    {
        Prune();

        if (queueStart == null)
            return;

        Vector3 direction = Vector3.ProjectOnPlane(
            queueDirection,
            Vector3.up
        );

        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.left;

        for (int i = 0; i < npcList.Count; i++)
        {
            npcList[i].SetTarget(
                queueStart.position +
                direction * Mathf.Max(0.1f, spacing) * i
            );
        }
    }

    public void RemoveFromQueue(NPCQueueAgent agent)
    {
        if (agent != null && npcList.Remove(agent))
        {
            agent.LeaveQueue();
            UpdateQueuePositions();
        }
    }

    public void ConfigureForFloor(
        Transform newSpawnPoint,
        Transform newQueueStart,
        HashSet<NPCQueueAgent> passengers)
    {
        if (newSpawnPoint == null || newQueueStart == null)
        {
            Debug.LogError(
                "Missing spawn/queue point for next floor.",
                this
            );
            return;
        }

        foreach (NPCQueueAgent agent in spawnedNPCs)
        {
            if (agent == null)
                continue;

            bool isPassenger =
                passengers != null &&
                passengers.Contains(agent);

            if (!isPassenger)
                Destroy(agent.gameObject);
        }

        npcList.Clear();
        spawnedNPCs.Clear();

        spawnPoint = newSpawnPoint;
        queueStart = newQueueStart;

        Debug.Log("NPC queue prepared for new floor.");
    }
}
