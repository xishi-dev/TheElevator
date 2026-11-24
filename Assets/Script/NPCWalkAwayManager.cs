
using System.Collections.Generic;
using UnityEngine;

public class NPCWalkAwayManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera lobbyCamera;
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private NPCGrabController grabController;

    [Header("Exit Direction")]
    [SerializeField] private Transform doorPoint;
    [SerializeField] private Transform outsidePoint;

    [Min(0f)]
    [SerializeField] private float exitClearance = 0.12f;

    [Min(0.1f)]
    [SerializeField] private float forwardClearance = 0.8f;

    [Header("Wall Collision")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallSkin = 0.05f;

    [Header("Walk Away Settings")]
    [SerializeField] private float waitBeforeWalking = 4f;
    [SerializeField] private float walkSpeed = 1.7f;
    [SerializeField] private float rotateSpeed = 360f;

    // NEW: NPC dropped outside before boarding.

    [Header("Failed Boarding - Fast Exit")]
    [Min(0f)]
    [SerializeField] private float failedBoardingWait = 0.5f;

    [Min(0.1f)]
    [SerializeField] private float failedBoardingSpeed = 9f;

    [Min(0f)]
    [SerializeField] private float failedBoardingRotateSpeed = 1080f;

    [Header("Remove NPC Settings")]
    [SerializeField] private float minWalkDistance = 2f;
    [SerializeField] private float screenMargin = 0.15f;
    [SerializeField] private float maxWalkTime = 25f;

    private class NPCState
    {
        public float waitTimer;
        public bool isWalkingAway;
        public float walkedDistance;
        public float walkTimer;
    }

    private readonly Dictionary<NPCQueueAgent, NPCState>
        npcStates = new Dictionary<NPCQueueAgent, NPCState>();

    private readonly List<NPCQueueAgent> removeList =
        new List<NPCQueueAgent>();

    private float scanTimer;

    private void Update()
    {
        if (grabController != null &&
            !grabController.isActiveAndEnabled)
        {
            return;
        }

        if (elevatorZone == null ||
            doorPoint == null ||
            outsidePoint == null)
        {
            return;
        }

        Vector3 forward =
            outsidePoint.position - doorPoint.position;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        Vector3 sideways =
            Vector3.Cross(Vector3.up, forward).normalized;

        if (lobbyCamera != null)
        {
            if (Vector3.Dot(
                sideways,
                -lobbyCamera.transform.right) < 0f)
            {
                sideways = -sideways;
            }
        }

        scanTimer -= Time.deltaTime;

        if (scanTimer <= 0f)
        {
            ScanNPCs();

            // Detect newly dropped NPCs faster.
            scanTimer = 0.1f;
        }

        Physics.SyncTransforms();

        UpdateNPCs(forward, sideways);
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

    private void UpdateNPCs(
        Vector3 forward,
        Vector3 sideways)
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

            // Never move NPCs that are still queued,
            // held by the player, or falling.

            if (agent.IsInQueue ||
                agent.IsGrabbed ||
                agent.IsLowering ||
                !agent.isActiveAndEnabled)
            {
                ResetState(state);
                continue;
            }

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            // EXIT / LATE must be delivered first.

            if (status != null && status.IsPending)
            {
                ResetState(state);
                continue;
            }

            bool delivered =
                status != null &&
                status.State ==
                PassengerExitStatus.ExitState.Delivered;

            // NEW:
            // NPC has left the queue,
            // but has never boarded the elevator.

            bool failedBoarding =
                !delivered &&
                !agent.IsInQueue &&
                (status == null || !status.HasBoarded);

            CapsuleCollider capsule =
                GetCapsule(agent);

            Bounds body =
                GetBodyBounds(agent, capsule);

            Bounds zone = elevatorZone.bounds;

            // Ignore NPCs on other floors.

            if (body.max.y < zone.min.y - 0.5f ||
                body.min.y > zone.max.y + 0.5f)
            {
                ResetState(state);
                continue;
            }

            float clearance =
                GetFrontClearance(body, forward);

            if (!delivered)
            {
                if (failedBoarding)
                {
                    // Allow a failed boarding NPC
                    // to walk outward when its center
                    // is already beyond the door.

                    float bodyExtent =
                        Mathf.Abs(forward.x) *
                        body.extents.x +
                        Mathf.Abs(forward.z) *
                        body.extents.z;

                    if (clearance + bodyExtent < 0f)
                    {
                        ResetState(state);
                        continue;
                    }
                }
                else if (clearance < exitClearance)
                {
                    ResetState(state);
                    continue;
                }
            }

            // Choose different waiting times.

            float requiredWait = failedBoarding
                ? failedBoardingWait
                : waitBeforeWalking;

            if (!state.isWalkingAway)
            {
                state.waitTimer += Time.deltaTime;

                if (state.waitTimer < requiredWait)
                    continue;

                state.isWalkingAway = true;
                state.walkedDistance = 0f;
                state.walkTimer = 0f;

                if (failedBoarding)
                {
                    Debug.Log(
                        "[FAILED BOARDING] " +
                        agent.name +
                        " is leaving quickly."
                    );
                }
                else
                {
                    Debug.Log(
                        agent.name +
                        " is walking away."
                    );
                }
            }

            WalkAway(
                agent,
                state,
                capsule,
                forward,
                sideways,
                clearance,
                failedBoarding
            );
        }

        foreach (NPCQueueAgent agent in removeList)
        {
            npcStates.Remove(agent);
        }
    }

    // =====================================
    // DISTANCE FROM ELEVATOR FRONT
    // =====================================

    private float GetFrontClearance(
        Bounds body,
        Vector3 forward)
    {
        Transform zone = elevatorZone.transform;

        Vector3 center =
            zone.TransformPoint(elevatorZone.center);

        Vector3 halfSize =
            elevatorZone.size * 0.5f;

        float zoneExtent =
            Mathf.Abs(Vector3.Dot(
                zone.TransformVector(
                    Vector3.right * halfSize.x),
                forward)) +
            Mathf.Abs(Vector3.Dot(
                zone.TransformVector(
                    Vector3.forward * halfSize.z),
                forward));

        float bodyExtent =
            Mathf.Abs(forward.x) * body.extents.x +
            Mathf.Abs(forward.z) * body.extents.z;

        return Vector3.Dot(
            body.center - center,
            forward
        ) - zoneExtent - bodyExtent;
    }

    // =====================================
    // WALK AWAY
    // =====================================

    private void WalkAway(
        NPCQueueAgent agent,
        NPCState state,
        CapsuleCollider capsule,
        Vector3 forward,
        Vector3 sideways,
        float clearance,
        bool failedBoarding)
    {
        Vector3 direction =
            clearance < exitClearance + forwardClearance
                ? forward
                : sideways;

        // NEW: Failed boarding NPCs move faster.

        float currentSpeed = failedBoarding
            ? failedBoardingSpeed
            : walkSpeed;

        float currentRotateSpeed = failedBoarding
            ? failedBoardingRotateSpeed
            : rotateSpeed;

        float distance =
            Mathf.Max(0.1f, currentSpeed) *
            Time.deltaTime;

        float allowed = GetSafeDistance(
            agent,
            capsule,
            direction,
            distance
        );

        // Avoid walking into elevator walls.

        if (allowed <= 0.0001f &&
            direction != forward)
        {
            direction = forward;

            allowed = GetSafeDistance(
                agent,
                capsule,
                direction,
                distance
            );
        }

        // Try the opposite side if the fast exit
        // is blocked by an obstacle.

        if (failedBoarding &&
            allowed <= 0.0001f &&
            clearance >= exitClearance + forwardClearance)
        {
            direction = -sideways;

            allowed = GetSafeDistance(
                agent,
                capsule,
                direction,
                distance
            );
        }

        if (allowed > 0.0001f)
        {
            agent.transform.position +=
                direction * allowed;

            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            agent.transform.rotation =
                Quaternion.RotateTowards(
                    agent.transform.rotation,
                    targetRotation,
                    currentRotateSpeed * Time.deltaTime
                );

            state.walkedDistance += allowed;
        }

        state.walkTimer += Time.deltaTime;

        // Remove failed boarding NPCs only after
        // they have cleared the elevator
        // and left the camera view.

        if (failedBoarding)
        {
            if (clearance >= exitClearance &&
                IsOutsideScreen(agent))
            {
                Destroy(agent.gameObject);
                removeList.Add(agent);
            }

            return;
        }

        // Preserve the normal removal rules.

        if (state.walkedDistance >= minWalkDistance &&
            clearance >= exitClearance)
        {
            if (IsOutsideScreen(agent) ||
                state.walkTimer >= maxWalkTime)
            {
                Destroy(agent.gameObject);
                removeList.Add(agent);
            }
        }
    }

    // =====================================
    // WALL PROTECTION
    // =====================================

    private float GetSafeDistance(
        NPCQueueAgent agent,
        CapsuleCollider capsule,
        Vector3 direction,
        float distance)
    {
        Bounds body = GetBodyBounds(agent, capsule);

        Vector3 point1;
        Vector3 point2;
        float radius;

        if (capsule != null && capsule.enabled)
        {
            Transform t = capsule.transform;
            Vector3 scale = t.lossyScale;

            Vector3 axis;
            float axisScale;
            float radiusScale;

            if (capsule.direction == 0)
            {
                axis = t.right;
                axisScale = Mathf.Abs(scale.x);

                radiusScale = Mathf.Max(
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)
                );
            }
            else if (capsule.direction == 2)
            {
                axis = t.forward;
                axisScale = Mathf.Abs(scale.z);

                radiusScale = Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y)
                );
            }
            else
            {
                axis = t.up;
                axisScale = Mathf.Abs(scale.y);

                radiusScale = Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z)
                );
            }

            radius = Mathf.Max(
                0.01f,
                capsule.radius * radiusScale
            );

            float height = Mathf.Max(
                capsule.height * axisScale,
                radius * 2f
            );

            Vector3 center =
                t.TransformPoint(capsule.center);

            float segment = Mathf.Max(
                0f,
                height * 0.5f - radius
            );

            point1 = center + axis * segment;
            point2 = center - axis * segment;
        }
        else
        {
            radius = 0.35f;

            point1 = body.center + Vector3.up * 0.55f;
            point2 = body.center - Vector3.up * 0.55f;
        }

        int mask = wallLayers.value != 0
            ? wallLayers.value
            : ~0;

        float skin = Mathf.Max(0f, wallSkin);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            point1,
            point2,
            radius,
            direction,
            distance + skin,
            mask,
            QueryTriggerInteraction.Ignore
        );

        float allowed = distance;

        foreach (RaycastHit hit in hits)
        {
            Collider obstacle = hit.collider;

            if (!IsWallObstacle(agent, obstacle, body))
                continue;

            allowed = Mathf.Min(
                allowed,
                Mathf.Max(0f, hit.distance - skin)
            );
        }

        // Validate the destination.

        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (allowed <= 0.0001f)
                return 0f;

            Vector3 displacement = direction * allowed;

            Collider[] overlaps = Physics.OverlapCapsule(
                point1 + displacement,
                point2 + displacement,
                radius,
                mask,
                QueryTriggerInteraction.Ignore
            );

            bool blocked = false;

            foreach (Collider obstacle in overlaps)
            {
                if (!IsWallObstacle(agent, obstacle, body))
                    continue;

                if (capsule == null ||
                    !capsule.enabled)
                {
                    blocked = true;
                    break;
                }

                if (Physics.ComputePenetration(
                    capsule,
                    capsule.transform.position + displacement,
                    capsule.transform.rotation,
                    obstacle,
                    obstacle.transform.position,
                    obstacle.transform.rotation,
                    out Vector3 escape,
                    out float depth) &&
                    depth > 0.0001f)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
                return allowed;

            allowed *= 0.5f;
        }

        return 0f;
    }

    private bool IsWallObstacle(
        NPCQueueAgent agent,
        Collider obstacle,
        Bounds body)
    {
        if (obstacle == null ||
            !obstacle.enabled ||
            obstacle.isTrigger)
        {
            return false;
        }

        if (obstacle.transform.IsChildOf(
            agent.transform))
        {
            return false;
        }

        if (obstacle.GetComponentInParent<NPCQueueAgent>()
            != null)
        {
            return false;
        }

        if (obstacle.bounds.max.y <= body.min.y + 0.05f)
            return false;

        return true;
    }

    private CapsuleCollider GetCapsule(
        NPCQueueAgent agent)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        return capsule;
    }

    private Bounds GetBodyBounds(
        NPCQueueAgent agent,
        CapsuleCollider capsule)
    {
        if (capsule != null && capsule.enabled)
            return capsule.bounds;

        return new Bounds(
            agent.transform.position + Vector3.up * 0.9f,
            new Vector3(0.7f, 1.8f, 0.7f)
        );
    }

    private bool IsOutsideScreen(
        NPCQueueAgent agent)
    {
        if (lobbyCamera == null)
            return false;

        Vector3 sample =
            agent.transform.position + Vector3.up;

        Vector3 screenPoint =
            lobbyCamera.WorldToViewportPoint(sample);

        return screenPoint.z <= 0f ||
               screenPoint.x < -screenMargin ||
               screenPoint.x > 1f + screenMargin;
    }

    private void ResetState(NPCState state)
    {
        state.waitTimer = 0f;
        state.isWalkingAway = false;
        state.walkedDistance = 0f;
        state.walkTimer = 0f;
    }
}
