
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
    private readonly RaycastHit[] wallHits = new RaycastHit[32];
    private BoxCollider cabin;
    public void ConfigureCabin(BoxCollider zone) => cabin = zone;

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

        bool inCabin = cabin != null && cabin.bounds.Contains(centerA) && cabin.bounds.Contains(centerB);
        float compression = inCabin ? .72f : 1f;
        float radiusA = GetRadius(a.capsule) * compression;
        float radiusB = GetRadius(b.capsule) * compression;

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
        if (overlap > .07f && (a.agent.IsGrabbed || b.agent.IsGrabbed))
        {
            if (a.agent.TryGetComponent(out PassengerRagdoll first)) first.Jolt(-direction, overlap * 2f);
            if (b.agent.TryGetComponent(out PassengerRagdoll second)) second.Jolt(direction, overlap * 2f);
            ShiftAudio.Play(ShiftCue.Bump);
        }

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

        if (wallLayers.value != 0)
        {
            GetWorldCapsule(
                body.capsule,
                out Vector3 point1,
                out Vector3 point2,
                out float radius
            );

            float feet = Mathf.Min(point1.y, point2.y) - radius;
            int count = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, wallHits,
                allowed + wallSkin, wallLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = wallHits[i];
                if (hit.collider == null || hit.collider.bounds.max.y <= feet + wallSkin ||
                    Vector3.Dot(hit.normal, direction) >= -.001f) continue;
                allowed = Mathf.Clamp(hit.distance - wallSkin, 0f, allowed);
            }
        }

        if (allowed <= 0f)
            return 0f;

        body.agent.transform.position +=
            direction * allowed;

        return allowed;
    }
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

        radius = Mathf.Max(
            0.01f,
            capsule.radius *
            radiusScale *
            bodyRadiusScale
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

    private Vector3 GetCenter(
        CapsuleCollider capsule)
    {
        return capsule.transform.TransformPoint(
            capsule.center
        );
    }
    private float GetRadius(
        CapsuleCollider capsule)
    {
        Vector3 scale = capsule.transform.lossyScale;

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
