
using UnityEngine;

[DefaultExecutionOrder(9000)]
public class NPCGroundStabilizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCGrabController grabController;
    [SerializeField] private NPCBodyCollisionManager bodyCollisionManager;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers;

    [Min(0.1f)]
    [SerializeField] private float probeHeight = 0.7f;

    [Min(0.1f)]
    [SerializeField] private float probeBelow = 0.35f;

    [SerializeField] private float groundOffset = 0.02f;

    [Min(0.01f)]
    [SerializeField] private float maxCorrection = 0.7f;

    [Min(0.02f)]
    [SerializeField] private float checkInterval = 0.1f;

    private bool pausedCrowd;
    private float nextGroundCheck;

    private void LateUpdate()
    {
        if (grabController == null ||
            bodyCollisionManager == null)
        {
            return;
        }

        // Stop crowd movement during transitions.
        // Also prevents interference with Floor 7 ending.

        if (!grabController.isActiveAndEnabled)
        {
            if (bodyCollisionManager.enabled)
            {
                bodyCollisionManager.enabled = false;
                pausedCrowd = true;
            }

            nextGroundCheck = 0f;
            return;
        }

        // Restore crowd movement during gameplay.

        if (pausedCrowd)
        {
            bodyCollisionManager.enabled = true;
            pausedCrowd = false;
        }

        if (Time.timeScale <= 0f)
            return;

        if (groundLayers.value == 0)
            return;

        if (Time.time < nextGroundCheck)
            return;

        nextGroundCheck =
            Time.time + Mathf.Max(0.02f, checkInterval);

        Physics.SyncTransforms();

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null ||
                !agent.isActiveAndEnabled)
            {
                continue;
            }

            // Never move NPCs while being held,
            // falling or waiting in the queue.

            if (agent.IsGrabbed ||
                agent.IsLowering ||
                agent.IsInQueue)
            {
                continue;
            }

            StabilizeGround(agent);
        }

        Physics.SyncTransforms();
    }

    private void StabilizeGround(NPCQueueAgent agent)
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

        Bounds body = capsule.bounds;

        float feetY = body.min.y;

        // Detect the actual floor under the NPC.

        Vector3 rayOrigin = new Vector3(
            body.center.x,
            feetY + probeHeight,
            body.center.z
        );

        float rayDistance =
            probeHeight + probeBelow;

        if (!Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // Only accept horizontal ground surfaces.

        if (Vector3.Dot(hit.normal, Vector3.up) < 0.7f)
            return;

        float correction =
            hit.point.y + groundOffset - feetY;

        // Ignore very small changes.

        if (Mathf.Abs(correction) < 0.005f)
            return;

        // Do not teleport NPCs large distances.

        if (Mathf.Abs(correction) > maxCorrection)
            return;

        Vector3 position = agent.transform.position;

        // Adjust only height.
        // Preserve the original X and Z positions.

        position.y += correction;

        agent.transform.position = position;
    }

    private void OnDisable()
    {
        if (pausedCrowd &&
            bodyCollisionManager != null &&
            grabController != null &&
            grabController.isActiveAndEnabled)
        {
            bodyCollisionManager.enabled = true;
        }

        pausedCrowd = false;
    }
}
