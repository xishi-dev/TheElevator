
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
    private bool missionMode;
    private int missionSpawnLimit;
    private int priorityArrival;
    private uint missionLighting = 1;
    public int MissionSpawned { get; private set; }
    public NPCQueueAgent PriorityPassenger { get; private set; }
    public IReadOnlyList<NPCQueueAgent> SpawnedAgents => spawnedNPCs;

    public void SetMissionLighting(uint mask)
    {
        missionLighting = mask;
        foreach (var agent in spawnedNPCs)
            if (agent != null) SetLighting(agent.gameObject);
    }

    private void SetLighting(GameObject npc)
    {
        foreach (var renderer in npc.GetComponentsInChildren<Renderer>())
            renderer.renderingLayerMask = missionLighting;
    }

    public void PrepareMission(int amount, float seconds)
    {
        StopSpawning();
        missionMode = true;
        missionSpawnLimit = Mathf.Max(0, amount);
        MissionSpawned = 0;
        PriorityPassenger = null;
        priorityArrival = Random.Range(1, Mathf.Min(missionSpawnLimit, 5) + 1);
        spawnInterval = Mathf.Max(.1f, seconds * .78f / Mathf.Max(1, amount - 5));
        if (hasStarted && isActiveAndEnabled) StartSpawning();
    }

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
        StopSpawning();
    }

    private void StopSpawning()
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
        if (missionMode)
        {
            int initial = Mathf.Min(5, missionSpawnLimit);
            for (int i = 0; i < initial; i++)
                if (!TrySpawnNPC()) break;
        }
        while (!missionMode || MissionSpawned < missionSpawnLimit)
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
            (missionMode ? MissionSpawned >= missionSpawnLimit
                : npcList.Count >= maxNPCs || spawnedNPCs.Count >= maxSpawnedNPCs))
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

        Vector3 position = spawnPoint.position;
        if (missionMode)
        {
            int lane = npcList.Count % 5;
            int row = npcList.Count / 5;
            position = MissionSlot(lane, row);
        }
        Quaternion facing = spawnPoint.rotation;
        Vector3 toLobby = Vector3.ProjectOnPlane(queueStart.position - spawnPoint.position, Vector3.up);
        if (missionMode && toLobby.sqrMagnitude > .01f) facing = Quaternion.LookRotation(toLobby);
        GameObject npc = Instantiate(selectedPrefab, position, facing);
        if (npc.scene != gameObject.scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(npc, gameObject.scene);

        NPCQueueAgent agent =
            npc.GetComponent<NPCQueueAgent>();

        if (agent == null)
            agent = npc.AddComponent<NPCQueueAgent>();
        if (missionMode)
        {
            MissionSpawned++;
            if (MissionSpawned == priorityArrival) PriorityPassenger = agent;
            agent.moveSpeed = Mathf.Max(agent.moveSpeed, 4.5f);
            agent.gameObject.AddComponent<PassengerRagdoll>();
            SetLighting(npc);
        }

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
            Vector3 slot = missionMode
                ? MissionSlot(i % 5, i / 5)
                : queueStart.position + direction * Mathf.Max(.1f, spacing) * i;
            npcList[i].SetTarget(slot);
        }
    }

    private Vector3 MissionSlot(int column, int row)
    {
        Vector3 along = Vector3.ProjectOnPlane(spawnPoint.position - queueStart.position, Vector3.up);
        along = along.sqrMagnitude > .01f ? along.normalized : Vector3.right;
        Vector3 intoLobby = Vector3.Cross(Vector3.up, along);
        return queueStart.position + along * (column * .99f) + intoLobby * (row * .88f);
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
        if (passengers != null) spawnedNPCs.AddRange(passengers);

        spawnPoint = newSpawnPoint;
        queueStart = newQueueStart;

        Debug.Log("NPC queue prepared for new floor.");
    }
}
