
using System.Collections.Generic;
using UnityEngine;

public class NPCWalkAwayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private NPCGrabController grabController;

    [Header("Walk Away Settings")]
    [SerializeField] private float waitBeforeWalking = 4f;
    [SerializeField] private float walkSpeed = 1.7f;
    [SerializeField] private float rotateSpeed = 360f;

    [Header("Remove NPC Settings")]
    [SerializeField] private float minWalkDistance = 2f;
    [SerializeField] private float screenMargin = 0.15f;
    [SerializeField] private float maxWalkTime = 25f;

    private class NPCState
    {
        public float waitTimer = 0f;
        public bool isWalkingAway = false;
        public float walkedDistance = 0f;
        public float walkTimer = 0f;
        public Vector3 walkDirection;
    }

    private readonly Dictionary<NPCQueueAgent, NPCState>
        npcStates = new Dictionary<NPCQueueAgent, NPCState>();

    private readonly List<NPCQueueAgent> removeList =
        new List<NPCQueueAgent>();

    private float scanTimer = 0f;

    private void Update()
    {
        if (grabController != null &&
            !grabController.enabled)
        {
            return;
        }

        scanTimer -= Time.deltaTime;

        if (scanTimer <= 0f)
        {
            ScanNPCs();
            scanTimer = 0.5f;
        }

        UpdateNPCs();
    }

    private void ScanNPCs()
    {
        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null)
                continue;

            if (!npcStates.ContainsKey(agent))
            {
                npcStates.Add(agent, new NPCState());
            }
        }
    }

    private void UpdateNPCs()
    {
        removeList.Clear();

        foreach (var entry in npcStates)
        {
            NPCQueueAgent agent = entry.Key;
            NPCState state = entry.Value;

            if (agent == null)
            {
                removeList.Add(agent);
                continue;
            }

            if (agent.IsInQueue)
            {
                ResetState(state);
                continue;
            }

            if (agent.IsGrabbed || agent.IsLowering)
            {
                ResetState(state);
                continue;
            }

            if (IsInsideElevator(agent))
            {
                ResetState(state);
                continue;
            }

            if (!state.isWalkingAway)
            {
                state.waitTimer += Time.deltaTime;

                if (state.waitTimer >= waitBeforeWalking)
                {
                    StartWalkingAway(agent, state);
                }

                continue;
            }

            WalkAway(agent, state);
        }

        foreach (NPCQueueAgent agent in removeList)
        {
            npcStates.Remove(agent);
        }
    }

    private void StartWalkingAway(
        NPCQueueAgent agent,
        NPCState state)
    {
        state.isWalkingAway = true;
        state.walkedDistance = 0f;
        state.walkTimer = 0f;

        if (lobbyCamera != null)
        {
            state.walkDirection =
                -lobbyCamera.transform.right;
        }
        else
        {
            state.walkDirection = Vector3.left;
        }

        state.walkDirection.y = 0f;

        if (state.walkDirection.sqrMagnitude < 0.001f)
        {
            state.walkDirection = Vector3.left;
        }

        state.walkDirection.Normalize();

        Debug.Log(
            agent.name + " เริ่มเดินออกจาก Lobby"
        );
    }

    private void WalkAway(
        NPCQueueAgent agent,
        NPCState state)
    {
        Transform npc = agent.transform;

        Vector3 direction = state.walkDirection;

        float distance = Mathf.Max(0.1f, walkSpeed)
                         * Time.deltaTime;

        npc.position += direction * distance;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            npc.rotation = Quaternion.RotateTowards(
                npc.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime
            );
        }

        state.walkedDistance += distance;
        state.walkTimer += Time.deltaTime;

        if (state.walkedDistance >= minWalkDistance)
        {
            if (IsOutsideScreen(agent) ||
                state.walkTimer >= maxWalkTime)
            {
                Debug.Log(
                    agent.name + " เดินออกจากฉากแล้ว"
                );

                Destroy(agent.gameObject);

                removeList.Add(agent);
            }
        }
    }

    private bool IsInsideElevator(NPCQueueAgent agent)
    {
        if (elevatorZone == null)
            return false;

        Vector3 worldPosition = agent.transform.position;

        CapsuleCollider capsule =
            agent.GetComponentInChildren<CapsuleCollider>();

        if (capsule != null)
        {
            worldPosition =
                capsule.transform.TransformPoint(
                    capsule.center
                );
        }

        Vector3 localPosition =
            elevatorZone.transform.InverseTransformPoint(
                worldPosition
            );

        localPosition -= elevatorZone.center;

        Vector3 halfSize =
            elevatorZone.size * 0.5f;

        return
            Mathf.Abs(localPosition.x) <= halfSize.x &&
            Mathf.Abs(localPosition.y) <= halfSize.y &&
            Mathf.Abs(localPosition.z) <= halfSize.z;
    }

    private bool IsOutsideScreen(NPCQueueAgent agent)
    {
        if (lobbyCamera == null)
            return false;

        Vector3 samplePoint =
            agent.transform.position + Vector3.up;

        Vector3 screenPoint =
            lobbyCamera.WorldToViewportPoint(samplePoint);

        return screenPoint.z <= 0f ||
               screenPoint.x < -screenMargin;
    }

    private void ResetState(NPCState state)
    {
        state.waitTimer = 0f;
        state.isWalkingAway = false;
        state.walkedDistance = 0f;
        state.walkTimer = 0f;
    }
}
