
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(21000)]
[RequireComponent(typeof(NPCBodyCollisionManager))]
public class ElevatorBoundaryGuard : MonoBehaviour
{
    [Header("Elevator References")]
    [SerializeField] private BoxCollider elevatorZone;
    [SerializeField] private Transform doorPoint;
    [SerializeField] private Transform outsidePoint;

    [Header("Wall Protection")]
    [Min(0f)]
    [SerializeField] private float wallPadding = 0.15f;

    [Min(0f)]
    [SerializeField] private float heightTolerance = 1f;

    private NPCBodyCollisionManager bodyCollisionManager;

    private readonly HashSet<NPCQueueAgent> passengers =
        new HashSet<NPCQueueAgent>();

    private Vector3 previousZonePosition;
    private bool initialized;

    private void Awake()
    {
        bodyCollisionManager =
            GetComponent<NPCBodyCollisionManager>();
    }

    private void OnEnable()
    {
        passengers.Clear();
        initialized = false;
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f)
            return;

        if (elevatorZone == null ||
            doorPoint == null ||
            outsidePoint == null)
        {
            return;
        }

        // Do not interfere with the final cinematic.
        if (bodyCollisionManager == null ||
            !bodyCollisionManager.isActiveAndEnabled)
        {
            passengers.Clear();
            initialized = false;
            return;
        }

        Transform zone = elevatorZone.transform;

        // Reset tracking when the elevator changes floors.
        if (!initialized ||
            Vector3.Distance(
                previousZonePosition,
                zone.position
            ) > 0.01f)
        {
            passengers.Clear();
            previousZonePosition = zone.position;
            initialized = true;
        }

        Vector3 worldForward =
            outsidePoint.position - doorPoint.position;

        worldForward.y = 0f;

        if (worldForward.sqrMagnitude < 0.001f)
            return;

        Vector3 localForward =
            zone.InverseTransformDirection(
                worldForward.normalized
            );

        bool frontIsZ =
            Mathf.Abs(localForward.z) >=
            Mathf.Abs(localForward.x);

        float frontSign = frontIsZ
            ? Mathf.Sign(localForward.z)
            : Mathf.Sign(localForward.x);

        Physics.SyncTransforms();

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        passengers.RemoveWhere(agent => agent == null);

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null ||
                !agent.isActiveAndEnabled)
            {
                passengers.Remove(agent);
                continue;
            }

            if (agent.IsInQueue)
            {
                passengers.Remove(agent);
                continue;
            }

            ProtectPassenger(
                agent,
                frontIsZ,
                frontSign
            );
        }

        Physics.SyncTransforms();
    }

    private void ProtectPassenger(
        NPCQueueAgent agent,
        bool frontIsZ,
        float frontSign)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule == null || !capsule.enabled)
            return;

        Transform zone = elevatorZone.transform;

        Bounds body = capsule.bounds;

        Vector3 localCenter =
            zone.InverseTransformPoint(body.center) -
            elevatorZone.center;

        Vector3 halfSize =
            elevatorZone.size * 0.5f;

        float verticalScale =
            Mathf.Max(
                0.001f,
                zone.TransformVector(Vector3.up).magnitude
            );

        float localHeightTolerance =
            heightTolerance / verticalScale;

        // Ignore NPCs on other floors.
        if (Mathf.Abs(localCenter.y) >
            halfSize.y + localHeightTolerance)
        {
            passengers.Remove(agent);
            return;
        }

        Vector3 localExtents =
            GetLocalExtents(body, zone);

        float side = frontIsZ
            ? localCenter.x
            : localCenter.z;

        float depth = frontIsZ
            ? localCenter.z * frontSign
            : localCenter.x * frontSign;

        float halfWidth = frontIsZ
            ? halfSize.x
            : halfSize.z;

        float halfDepth = frontIsZ
            ? halfSize.z
            : halfSize.x;

        float bodyWidth = frontIsZ
            ? localExtents.x
            : localExtents.z;

        float bodyDepth = frontIsZ
            ? localExtents.z
            : localExtents.x;

        // Register NPCs that enter the cabin.
        bool insideCabin =
            Mathf.Abs(side) <= halfWidth &&
            depth >= -halfDepth &&
            depth <= halfDepth;

        if (insideCabin)
        {
            passengers.Add(agent);
        }

        if (!passengers.Contains(agent))
            return;

        // Allow NPCs to leave through the front door.
        // Stop tracking once the full body is outside.
        if (depth > halfDepth + bodyDepth)
        {
            passengers.Remove(agent);
            return;
        }

        float sideScale = frontIsZ
            ? zone.TransformVector(Vector3.right).magnitude
            : zone.TransformVector(Vector3.forward).magnitude;

        float depthScale = frontIsZ
            ? zone.TransformVector(Vector3.forward).magnitude
            : zone.TransformVector(Vector3.right).magnitude;

        float sidePadding =
            wallPadding / Mathf.Max(0.001f, sideScale);

        float depthPadding =
            wallPadding / Mathf.Max(0.001f, depthScale);

        float sideLimit = Mathf.Max(
            0f,
            halfWidth - bodyWidth - sidePadding
        );

        float backLimit = Mathf.Min(
            0f,
            -halfDepth + bodyDepth + depthPadding
        );

        // Protect both side walls.
        float safeSide = Mathf.Clamp(
            side,
            -sideLimit,
            sideLimit
        );

        // Protect the back wall.
        // Do not clamp the front exit.
        float safeDepth = Mathf.Max(
            depth,
            backLimit
        );

        Vector3 correction = Vector3.zero;

        if (frontIsZ)
        {
            correction.x = safeSide - side;

            correction.z =
                (safeDepth - depth) * frontSign;
        }
        else
        {
            correction.z = safeSide - side;

            correction.x =
                (safeDepth - depth) * frontSign;
        }

        if (correction.sqrMagnitude < 0.00000001f)
            return;

        // Correct horizontal position only.
        Vector3 worldCorrection =
            zone.TransformVector(correction);

        worldCorrection.y = 0f;

        agent.transform.position += worldCorrection;
    }

    private Vector3 GetLocalExtents(
        Bounds bounds,
        Transform zone)
    {
        Vector3 e = bounds.extents;

        Matrix4x4 m = zone.worldToLocalMatrix;

        return new Vector3(
            Mathf.Abs(m.m00) * e.x +
            Mathf.Abs(m.m01) * e.y +
            Mathf.Abs(m.m02) * e.z,

            Mathf.Abs(m.m10) * e.x +
            Mathf.Abs(m.m11) * e.y +
            Mathf.Abs(m.m12) * e.z,

            Mathf.Abs(m.m20) * e.x +
            Mathf.Abs(m.m21) * e.y +
            Mathf.Abs(m.m22) * e.z
        );
    }
}
