
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public class NPCBodyCollisionManager : MonoBehaviour
{
    [Header("Wall Collision")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallSkin = 0.02f;

    [Header("NPC Collision")]
    [SerializeField] private float bodyRadiusScale = 1f;
    [SerializeField] private float extraSpacing = 0.01f;
    [SerializeField] private float minVerticalOverlap = 0.15f;

    [Header("Collision Quality")]
    [SerializeField, Range(1, 8)]
    private int solverIterations = 4;

    [SerializeField] private float maxPushPerPass = 0.25f;
    [SerializeField] private float scanInterval = 0.3f;

    private class NPCBody
    {
        public NPCQueueAgent agent;
        public CapsuleCollider capsule;
    }

    private readonly List<NPCBody> bodies =
        new List<NPCBody>();

    private float nextScanTime;

    private void OnEnable()
    {
        nextScanTime = 0f;
    }

    private void LateUpdate()
    {
        if (Time.time >= nextScanTime)
        {
            ScanNPCs();

            nextScanTime = Time.time +
                Mathf.Max(0.1f, scanInterval);
        }

        if (bodies.Count < 2)
            return;

        Physics.SyncTransforms();

        for (int iteration = 0;
             iteration < solverIterations;
             iteration++)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                for (int j = i + 1;
                     j < bodies.Count;
                     j++)
                {
                    ResolveCollision(
                        bodies[i],
                        bodies[j]
                    );
                }
            }
        }

        Physics.SyncTransforms();
    }

    // =====================================
    // FIND NPC BODIES
    // =====================================

    private void ScanNPCs()
    {
        bodies.Clear();

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null ||
                !agent.isActiveAndEnabled)
                continue;

            CapsuleCollider capsule =
                agent.GetComponent<CapsuleCollider>();

            if (capsule == null)
            {
                capsule =
                    agent.GetComponentInChildren<CapsuleCollider>();
            }

            if (capsule == null ||
                !capsule.enabled ||
                capsule.isTrigger)
                continue;

            bodies.Add(new NPCBody
            {
                agent = agent,
                capsule = capsule
            });
        }
    }

    // =====================================
    // NPC COLLISION - ORIGINAL SYSTEM
    // =====================================

    private void ResolveCollision(
        NPCBody a,
        NPCBody b)
    {
        if (!IsValid(a) || !IsValid(b))
            return;

        if (a.agent.IsInQueue &&
            b.agent.IsInQueue)
            return;

        Bounds boundsA = a.capsule.bounds;
        Bounds boundsB = b.capsule.bounds;

        float verticalOverlap = Mathf.Min(
            boundsA.max.y,
            boundsB.max.y
        ) - Mathf.Max(
            boundsA.min.y,
            boundsB.min.y
        );

        if (verticalOverlap < minVerticalOverlap)
            return;

        Vector3 centerA = GetCenter(a.capsule);
        Vector3 centerB = GetCenter(b.capsule);

        Vector3 difference = centerB - centerA;

        difference.y = 0f;

        float distance = difference.magnitude;

        float radiusA = GetRadius(a.capsule);
        float radiusB = GetRadius(b.capsule);

        float requiredDistance =
            radiusA + radiusB + extraSpacing;

        if (distance >= requiredDistance)
            return;

        Vector3 direction;

        if (distance > 0.0001f)
        {
            direction = difference / distance;
        }
        else
        {
            direction = Vector3.right;
        }

        float overlap = requiredDistance - distance;

        // Preserve original grabbed NPC behavior.

        if (a.agent.IsGrabbed &&
            !b.agent.IsGrabbed)
        {
            PushWithFallback(
                b, direction,
                a, -direction,
                overlap
            );

            return;
        }

        if (b.agent.IsGrabbed &&
            !a.agent.IsGrabbed)
        {
            PushWithFallback(
                a, -direction,
                b, direction,
                overlap
            );

            return;
        }

        if (a.agent.IsInQueue)
        {
            TryPush(b, direction * overlap);
            return;
        }

        if (b.agent.IsInQueue)
        {
            TryPush(a, -direction * overlap);
            return;
        }

        float half = overlap * 0.5f;

        float movedA = TryPush(
            a,
            -direction * half
        );

        float movedB = TryPush(
            b,
            direction * half
        );

        float remaining =
            overlap - movedA - movedB;

        if (remaining > 0.001f)
        {
            float extraB = TryPush(
                b,
                direction * remaining
            );

            remaining -= extraB;
        }

        if (remaining > 0.001f)
        {
            TryPush(
                a,
                -direction * remaining
            );
        }
    }

    private void PushWithFallback(
        NPCBody first,
        Vector3 firstDirection,
        NPCBody fallback,
        Vector3 fallbackDirection,
        float distance)
    {
        float moved = TryPush(
            first,
            firstDirection * distance
        );

        float remaining = distance - moved;

        if (remaining > 0.001f)
        {
            TryPush(
                fallback,
                fallbackDirection * remaining
            );
        }
    }

    // =====================================
    // NEW: SAFE NPC PUSH
    // =====================================

    private float TryPush(
        NPCBody body,
        Vector3 movement)
    {
        if (!IsValid(body))
            return 0f;

        movement.y = 0f;

        float distance = movement.magnitude;

        if (distance < 0.0001f)
            return 0f;

        float allowed = Mathf.Min(
            distance,
            Mathf.Max(0.01f, maxPushPerPass)
        );

        Vector3 direction = movement.normalized;

        Physics.SyncTransforms();

        // Use the actual physical capsule.
        // Do not shrink wall collision using
        // bodyRadiusScale.

        GetWorldCapsule(
            body.capsule,
            out Vector3 point1,
            out Vector3 point2,
            out float radius
        );

        float feetY = body.capsule.bounds.min.y;

        float skin = Mathf.Max(0f, wallSkin);

        // If no Wall Layers are assigned,
        // still check solid colliders.

        int mask = wallLayers.value != 0
            ? wallLayers.value
            : ~0;

        // =================================
        // CHECK EXISTING WALL OVERLAPS
        // =================================

        Collider[] currentOverlaps =
            Physics.OverlapCapsule(
                point1,
                point2,
                radius,
                mask,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider obstacle in currentOverlaps)
        {
            if (!IsWallObstacle(
                body,
                obstacle,
                feetY))
            {
                continue;
            }

            if (GetWallPenetration(
                body,
                obstacle,
                Vector3.zero,
                out Vector3 escapeDirection,
                out float penetration))
            {
                if (penetration > 0.001f)
                {
                    // Never push an NPC deeper
                    // into a wall it already touches.

                    if (Vector3.Dot(
                        direction,
                        escapeDirection
                    ) <= 0.0001f)
                    {
                        return 0f;
                    }
                }
            }
        }

        // =================================
        // CHECK THE ENTIRE MOVEMENT PATH
        // =================================

        RaycastHit[] hits =
            Physics.CapsuleCastAll(
                point1,
                point2,
                radius,
                direction,
                allowed + skin,
                mask,
                QueryTriggerInteraction.Ignore
            );

        foreach (RaycastHit hit in hits)
        {
            if (!IsWallObstacle(
                body,
                hit.collider,
                feetY))
            {
                continue;
            }

            // Allow escaping an existing
            // wall overlap, but never moving
            // further into the wall.

            if (hit.distance <= 0.001f)
            {
                bool alreadyOverlapping =
                    GetWallPenetration(
                        body,
                        hit.collider,
                        Vector3.zero,
                        out Vector3 escapeDirection,
                        out float penetration
                    );

                if (alreadyOverlapping &&
                    penetration > 0.001f &&
                    Vector3.Dot(
                        direction,
                        escapeDirection
                    ) > 0.0001f)
                {
                    continue;
                }
            }

            float safeDistance = Mathf.Max(
                0f,
                hit.distance - skin
            );

            allowed = Mathf.Min(
                allowed,
                safeDistance
            );
        }

        if (allowed <= 0.0001f)
            return 0f;

        // =================================
        // FINAL POSITION VALIDATION
        // =================================

        // A capsule cast alone may not catch
        // existing overlaps or crowded corners.
        //
        // Check the destination before moving.
        // If blocked, try a smaller movement.

        for (int attempt = 0;
             attempt < 7;
             attempt++)
        {
            if (allowed <= 0.0001f)
                return 0f;

            Vector3 displacement =
                direction * allowed;

            if (IsDestinationSafe(
                body,
                point1,
                point2,
                radius,
                feetY,
                mask,
                displacement))
            {
                body.agent.transform.position +=
                    displacement;

                Physics.SyncTransforms();

                return allowed;
            }

            allowed *= 0.5f;
        }

        // No safe movement.
        // Allow crowd overlap instead of
        // forcing an NPC through a wall.

        return 0f;
    }

    // =====================================
    // NEW: FILTER SOLID WALLS
    // =====================================

    private bool IsWallObstacle(
        NPCBody body,
        Collider obstacle,
        float feetY)
    {
        if (obstacle == null ||
            !obstacle.enabled ||
            obstacle.isTrigger)
        {
            return false;
        }

        // Ignore the NPC's own colliders.

        if (obstacle == body.capsule ||
            obstacle.transform.IsChildOf(
                body.agent.transform
            ))
        {
            return false;
        }

        // Other NPCs are handled by the
        // original crowd collision solver.

        if (obstacle.GetComponentInParent<NPCQueueAgent>()
            != null)
        {
            return false;
        }

        // Ignore the floor underneath the NPC.

        if (obstacle.bounds.max.y <= feetY + 0.05f)
        {
            return false;
        }

        return true;
    }

    // =====================================
    // NEW: WALL PENETRATION TEST
    // =====================================

    private bool GetWallPenetration(
        NPCBody body,
        Collider obstacle,
        Vector3 displacement,
        out Vector3 escapeDirection,
        out float penetration)
    {
        escapeDirection = Vector3.zero;
        penetration = 0f;

        if (!IsValid(body) || obstacle == null)
            return false;

        Transform capsuleTransform =
            body.capsule.transform;

        return Physics.ComputePenetration(
            body.capsule,
            capsuleTransform.position + displacement,
            capsuleTransform.rotation,
            obstacle,
            obstacle.transform.position,
            obstacle.transform.rotation,
            out escapeDirection,
            out penetration
        );
    }

    // =====================================
    // NEW: VALIDATE DESTINATION
    // =====================================

    private bool IsDestinationSafe(
        NPCBody body,
        Vector3 point1,
        Vector3 point2,
        float radius,
        float feetY,
        int mask,
        Vector3 displacement)
    {
        Collider[] destinationOverlaps =
            Physics.OverlapCapsule(
                point1 + displacement,
                point2 + displacement,
                radius,
                mask,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider obstacle in destinationOverlaps)
        {
            if (!IsWallObstacle(
                body,
                obstacle,
                feetY))
            {
                continue;
            }

            bool destinationPenetrates =
                GetWallPenetration(
                    body,
                    obstacle,
                    displacement,
                    out Vector3 destinationDirection,
                    out float destinationDepth
                );

            if (!destinationPenetrates ||
                destinationDepth <= 0.0001f)
            {
                continue;
            }

            bool alreadyPenetrating =
                GetWallPenetration(
                    body,
                    obstacle,
                    Vector3.zero,
                    out Vector3 currentEscapeDirection,
                    out float currentDepth
                );

            // Only allow overlap if the NPC
            // is already inside the wall slightly
            // and this move reduces penetration.

            if (alreadyPenetrating &&
                destinationDepth < currentDepth - 0.0001f &&
                Vector3.Dot(
                    displacement,
                    currentEscapeDirection
                ) > 0f)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // =====================================
    // WORLD CAPSULE
    // =====================================

    private void GetWorldCapsule(
        CapsuleCollider capsule,
        out Vector3 point1,
        out Vector3 point2,
        out float radius)
    {
        Transform t = capsule.transform;

        Vector3 s = t.lossyScale;

        Vector3 axis;
        float axisScale;
        float radiusScale;

        if (capsule.direction == 0)
        {
            axis = t.right;
            axisScale = Mathf.Abs(s.x);

            radiusScale = Mathf.Max(
                Mathf.Abs(s.y),
                Mathf.Abs(s.z)
            );
        }
        else if (capsule.direction == 2)
        {
            axis = t.forward;
            axisScale = Mathf.Abs(s.z);

            radiusScale = Mathf.Max(
                Mathf.Abs(s.x),
                Mathf.Abs(s.y)
            );
        }
        else
        {
            axis = t.up;
            axisScale = Mathf.Abs(s.y);

            radiusScale = Mathf.Max(
                Mathf.Abs(s.x),
                Mathf.Abs(s.z)
            );
        }

        // Use the real collider radius
        // for wall protection.

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

        float halfSegment = Mathf.Max(
            0f,
            height * 0.5f - radius
        );

        point1 = center + axis * halfSegment;
        point2 = center - axis * halfSegment;
    }

    // =====================================
    // ORIGINAL NPC CENTER
    // =====================================

    private Vector3 GetCenter(
        CapsuleCollider capsule)
    {
        return capsule.transform.TransformPoint(
            capsule.center
        );
    }

    // =====================================
    // ORIGINAL NPC CROWD RADIUS
    // =====================================

    private float GetRadius(
        CapsuleCollider capsule)
    {
        Vector3 scale =
            capsule.transform.lossyScale;

        float radiusScale;

        if (capsule.direction == 0)
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            );
        }
        else if (capsule.direction == 2)
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y)
            );
        }
        else
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.z)
            );
        }

        return Mathf.Max(
            0.01f,
            capsule.radius *
            radiusScale *
            bodyRadiusScale
        );
    }

    // =====================================
    // ORIGINAL VALIDATION
    // =====================================

    private bool IsValid(NPCBody body)
    {
        return body != null &&
               body.agent != null &&
               body.agent.isActiveAndEnabled &&
               body.capsule != null &&
               body.capsule.enabled &&
               !body.capsule.isTrigger;
    }
}
