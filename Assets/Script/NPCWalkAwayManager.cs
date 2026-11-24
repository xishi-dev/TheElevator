
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

    // =====================================
    // NEW: WALL COLLISION SETTINGS
    // =====================================

    [Header("Wall Collision - NEW")]
    [SerializeField] private LayerMask solidLayers = ~0;

    [Min(0f)]
    [SerializeField] private float exitClearance = 0.12f;

    [Min(0.01f)]
    [SerializeField] private float wallClearance = 0.06f;

    [Min(0.2f)]
    [SerializeField] private float pathCheckDistance = 1.2f;

    // =====================================
    // ORIGINAL NPC STATE
    // =====================================

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

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        // Preserve the original behavior:
        // do not walk during transitions.
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

        // Update physics collider positions
        // before checking for obstacles.
        Physics.SyncTransforms();

        UpdateNPCs();
    }

    // =====================================
    // FIND NPCS - ORIGINAL
    // =====================================

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
                npcStates.Add(
                    agent,
                    new NPCState()
                );
            }
        }
    }

    // =====================================
    // UPDATE NPCS
    // =====================================

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

            // Original queue protection.
            if (agent.IsInQueue)
            {
                ResetState(state);
                continue;
            }

            // Original grab/fall protection.
            if (agent.IsGrabbed || agent.IsLowering)
            {
                ResetState(state);
                continue;
            }

            // NEW:
            // NPCs marked EXIT or LATE
            // must not walk away automatically.
            PassengerExitStatus exitStatus =
                agent.GetComponent<PassengerExitStatus>();

            if (exitStatus != null &&
                exitStatus.IsPending)
            {
                ResetState(state);
                continue;
            }

            // NEW:
            // Require the whole NPC body
            // to be outside the elevator.
            if (!IsFullyOutsideElevator(agent))
            {
                ResetState(state);
                continue;
            }

            // Original waiting timer.
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

    // =====================================
    // ORIGINAL ELEVATOR CHECK
    // =====================================

    private bool IsInsideElevator(NPCQueueAgent agent)
    {
        if (agent == null || elevatorZone == null)
            return false;

        Vector3 worldPosition =
            agent.transform.position;

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

    // =====================================
    // NEW: GET NPC BODY BOUNDS
    // =====================================

    private Bounds GetNPCBounds(NPCQueueAgent agent)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule != null && capsule.enabled)
        {
            return capsule.bounds;
        }

        // Fallback if this NPC
        // has no enabled capsule.
        return new Bounds(
            agent.transform.position + Vector3.up * 0.9f,
            new Vector3(0.7f, 1.8f, 0.7f)
        );
    }

    // =====================================
    // NEW: FULL BODY OUTSIDE CHECK
    // =====================================

    private bool IsFullyOutsideElevator(
        NPCQueueAgent agent)
    {
        if (agent == null)
            return false;

        if (elevatorZone == null)
        {
            // Preserve the old behavior
            // when no elevator zone is set.
            return true;
        }

        Bounds body = GetNPCBounds(agent);
        Bounds zone = elevatorZone.bounds;

        float margin = Mathf.Max(0f, exitClearance);

        // Compare the full horizontal body
        // footprint with the elevator area.
        // The NPC must clear one side entirely.

        bool outsideLeft =
            body.max.x < zone.min.x - margin;

        bool outsideRight =
            body.min.x > zone.max.x + margin;

        bool outsideBack =
            body.max.z < zone.min.z - margin;

        bool outsideFront =
            body.min.z > zone.max.z + margin;

        return outsideLeft ||
               outsideRight ||
               outsideBack ||
               outsideFront;
    }

    // =====================================
    // ORIGINAL WALK DIRECTION
    // =====================================

    private Vector3 GetOriginalWalkDirection()
    {
        Vector3 direction;

        if (lobbyCamera != null)
        {
            direction = -lobbyCamera.transform.right;
        }
        else
        {
            direction = Vector3.left;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.left;
        }

        return direction.normalized;
    }

    // =====================================
    // START WALKING
    // =====================================

    private void StartWalkingAway(
        NPCQueueAgent agent,
        NPCState state)
    {
        state.isWalkingAway = true;
        state.walkedDistance = 0f;
        state.walkTimer = 0f;

        Vector3 preferredDirection =
            GetOriginalWalkDirection();

        // NEW:
        // Check the route before starting.
        state.walkDirection = FindOpenDirection(
            agent,
            preferredDirection
        );

        Debug.Log(
            agent.name + " เริ่มเดินออกจาก Lobby"
        );
    }

    // =====================================
    // NEW: BUILD COLLISION CAPSULE
    // =====================================

    private void GetBodyCapsule(
        NPCQueueAgent agent,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius,
        out float feetY)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule != null &&
            capsule.enabled &&
            capsule.direction == 1)
        {
            Vector3 scale =
                capsule.transform.lossyScale;

            radius = capsule.radius *
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z)
                );

            radius = Mathf.Max(0.05f, radius);

            float height = Mathf.Max(
                capsule.height * Mathf.Abs(scale.y),
                radius * 2f
            );

            Vector3 center =
                capsule.transform.TransformPoint(
                    capsule.center
                );

            Vector3 up =
                capsule.transform.up;

            float halfSegment =
                Mathf.Max(0f, height * 0.5f - radius);

            pointA =
                center + up * halfSegment;

            pointB =
                center - up * halfSegment;

            feetY = capsule.bounds.min.y;
            return;
        }

        // Fallback capsule based on bounds.
        Bounds bounds = GetNPCBounds(agent);

        radius = Mathf.Max(
            0.08f,
            Mathf.Min(
                bounds.extents.x,
                bounds.extents.z
            )
        );

        float halfSegmentFallback =
            Mathf.Max(0f, bounds.extents.y - radius);

        pointA = bounds.center +
                 Vector3.up * halfSegmentFallback;

        pointB = bounds.center -
                 Vector3.up * halfSegmentFallback;

        feetY = bounds.min.y;
    }

    // =====================================
    // NEW: IGNORE NON-WALL COLLIDERS
    // =====================================

    private bool IsSolidObstacle(
        NPCQueueAgent agent,
        Collider other,
        float feetY)
    {
        if (other == null || !other.enabled)
            return false;

        // Ignore trigger zones.
        if (other.isTrigger)
            return false;

        // Ignore ElevatorZone.
        if (other == elevatorZone)
            return false;

        // Ignore own colliders,
        // including ragdoll parts.
        if (other.transform.IsChildOf(agent.transform))
            return false;

        // Ignore other NPCs so crowds
        // do not permanently block walking.
        if (other.GetComponentInParent<NPCQueueAgent>()
            != null)
        {
            return false;
        }

        // Ignore the floor underneath NPC.
        if (other.bounds.max.y <= feetY + 0.08f)
        {
            return false;
        }

        return true;
    }

    // =====================================
    // NEW: SAFE MOVEMENT CHECK
    // =====================================

    private float GetSafeMoveDistance(
        NPCQueueAgent agent,
        Vector3 direction,
        float requestedDistance)
    {
        if (agent == null ||
            requestedDistance <= 0f ||
            direction.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        direction.y = 0f;
        direction.Normalize();

        GetBodyCapsule(
            agent,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius,
            out float feetY
        );

        float clearance =
            Mathf.Max(0.01f, wallClearance);

        float safeDistance = requestedDistance;

        // Check for walls ahead.
        RaycastHit[] hits =
            Physics.CapsuleCastAll(
                pointA,
                pointB,
                radius,
                direction,
                requestedDistance + clearance,
                solidLayers,
                QueryTriggerInteraction.Ignore
            );

        foreach (RaycastHit hit in hits)
        {
            if (!IsSolidObstacle(
                agent,
                hit.collider,
                feetY))
            {
                continue;
            }

            float allowedDistance =
                Mathf.Max(
                    0f,
                    hit.distance - clearance
                );

            safeDistance = Mathf.Min(
                safeDistance,
                allowedDistance
            );
        }

        // Also ensure the destination
        // does not overlap a wall.
        // Try smaller steps if necessary.
        for (int i = 0; i < 6; i++)
        {
            if (safeDistance <= 0.001f)
                return 0f;

            Vector3 movement =
                direction * safeDistance;

            Collider[] overlaps =
                Physics.OverlapCapsule(
                    pointA + movement,
                    pointB + movement,
                    radius,
                    solidLayers,
                    QueryTriggerInteraction.Ignore
                );

            bool blocked = false;

            foreach (Collider other in overlaps)
            {
                if (IsSolidObstacle(
                    agent,
                    other,
                    feetY))
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                return safeDistance;
            }

            safeDistance *= 0.5f;
        }

        return 0f;
    }

    // =====================================
    // NEW: FIND AN OPEN DIRECTION
    // =====================================

    private Vector3 FindOpenDirection(
        NPCQueueAgent agent,
        Vector3 preferredDirection)
    {
        if (preferredDirection.sqrMagnitude < 0.001f)
        {
            preferredDirection = Vector3.left;
        }

        preferredDirection.y = 0f;
        preferredDirection.Normalize();

        float[] angles =
        {
            0f,
            -25f, 25f,
            -50f, 50f,
            -75f, 75f,
            -90f, 90f
        };

        Vector3 bestDirection = Vector3.zero;
        float bestScore = -1f;

        float checkDistance =
            Mathf.Max(0.2f, pathCheckDistance);

        foreach (float angle in angles)
        {
            Vector3 direction =
                Quaternion.AngleAxis(
                    angle,
                    Vector3.up
                ) * preferredDirection;

            direction.y = 0f;
            direction.Normalize();

            float availableDistance =
                GetSafeMoveDistance(
                    agent,
                    direction,
                    checkDistance
                );

            // Prefer the original path,
            // but allow turning if blocked.
            float directionPreference =
                Mathf.Max(
                    0f,
                    Vector3.Dot(
                        direction,
                        preferredDirection
                    )
                );

            float candidateScore =
                availableDistance +
                directionPreference * 0.25f;

            if (availableDistance > 0.1f &&
                candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestDirection = direction;
            }
        }

        return bestDirection;
    }

    // =====================================
    // WALK AWAY - COLLISION FIX
    // =====================================

    private void WalkAway(
        NPCQueueAgent agent,
        NPCState state)
    {
        if (agent == null)
            return;

        // Keep the original walk timer.
        state.walkTimer += Time.deltaTime;

        float distance =
            Mathf.Max(0.1f, walkSpeed) *
            Time.deltaTime;

        Vector3 direction =
            state.walkDirection;

        float safeDistance =
            GetSafeMoveDistance(
                agent,
                direction,
                distance
            );

        // NEW:
        // If blocked, try another route.
        if (safeDistance < distance * 0.5f)
        {
            Vector3 originalDirection =
                GetOriginalWalkDirection();

            Vector3 alternative =
                FindOpenDirection(
                    agent,
                    originalDirection
                );

            if (alternative.sqrMagnitude > 0.001f)
            {
                direction = alternative;

                safeDistance =
                    GetSafeMoveDistance(
                        agent,
                        direction,
                        distance
                    );
            }
        }

        // NEW:
        // Never pass through a wall.
        if (safeDistance <= 0.001f)
        {
            state.walkDirection = Vector3.zero;
            return;
        }

        state.walkDirection = direction;

        Transform npc = agent.transform;

        // Move only the distance
        // confirmed safe by Physics.
        npc.position += direction * safeDistance;

        // Keep original rotation behavior.
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            npc.rotation =
                Quaternion.RotateTowards(
                    npc.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
        }

        // Record actual distance moved.
        state.walkedDistance += safeDistance;

        // Keep original despawn system,
        // but avoid deleting stuck NPCs
        // right at a nearby wall.
        if (state.walkedDistance >= minWalkDistance)
        {
            bool outsideScreen =
                IsOutsideScreen(agent);

            bool walkedFarEnough =
                state.walkedDistance >=
                Mathf.Max(
                    3f,
                    minWalkDistance * 3f
                );

            bool exceededWalkTime =
                state.walkTimer >= maxWalkTime;

            if (outsideScreen ||
                (exceededWalkTime && walkedFarEnough))
            {
                Debug.Log(
                    agent.name + " เดินออกจากฉากแล้ว"
                );

                Destroy(agent.gameObject);
                removeList.Add(agent);
            }
        }
    }

    // =====================================
    // ORIGINAL OUTSIDE SCREEN CHECK
    // =====================================

    private bool IsOutsideScreen(NPCQueueAgent agent)
    {
        if (agent == null || lobbyCamera == null)
            return false;

        Vector3 samplePoint =
            agent.transform.position + Vector3.up;

        Vector3 screenPoint =
            lobbyCamera.WorldToViewportPoint(
                samplePoint
            );

        return screenPoint.z <= 0f ||
               screenPoint.x < -screenMargin;
    }

    // =====================================
    // ORIGINAL RESET STATE
    // =====================================

    private void ResetState(NPCState state)
    {
        state.waitTimer = 0f;
        state.isWalkingAway = false;
        state.walkedDistance = 0f;
        state.walkTimer = 0f;
        state.walkDirection = Vector3.zero;
    }
}
