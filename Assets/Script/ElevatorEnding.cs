
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElevatorEnding : MonoBehaviour
{
    [Header("Ending Camera")]
    [SerializeField] private Camera endingCamera;

    [Header("Ending Path")]
    [SerializeField] private Transform doorPoint;
    [SerializeField] private Transform outsidePoint;

    [Header("Ending Timing")]
    [Min(0f)]
    [SerializeField] private float doorWait = 0.5f;

    [Min(0.05f)]
    [SerializeField] private float gatherDuration = 0.08f;

    [Min(0.05f)]
    [SerializeField] private float doorCrossDuration = 0.10f;

    [Min(0.1f)]
    [SerializeField] private float launchDuration = 0.55f;

    [Min(0f)]
    [SerializeField] private float launchStagger = 0.025f;

    [Min(0f)]
    [SerializeField] private float finishDelay = 2.5f;

    [Header("Ejection Settings")]
    [Min(0f)]
    [SerializeField] private float sideSpread = 3.2f;

    [Min(0f)]
    [SerializeField] private float extraDistance = 2.5f;

    [Min(0f)]
    [SerializeField] private float jumpHeightMin = 0.8f;

    [Min(0f)]
    [SerializeField] private float jumpHeightMax = 2.2f;

    [Min(0f)]
    [SerializeField] private float spinDegrees = 720f;

    // =====================================
    // NEW: CHAOTIC EJECTION SETTINGS
    // =====================================

    [Header("Chaotic Ejection")]
    [Min(0f)]
    [SerializeField] private float randomLaunchDelay = 0.2f;

    [Min(0f)]
    [SerializeField] private float doorLaneSpread = 0.18f;

    [Min(0.1f)]
    [SerializeField] private float doorClearDistance = 0.9f;

    [Min(0.1f)]
    [SerializeField] private float minSpeedMultiplier = 0.65f;

    [Min(0.1f)]
    [SerializeField] private float maxSpeedMultiplier = 1.65f;

    [Range(0f, 1f)]
    [SerializeField] private float flipChance = 0.9f;

    [Min(1)]
    [SerializeField] private int maxFlipTurns = 2;

    [Min(0.1f)]
    [SerializeField] private float burstPower = 1.6f;

    public float DoorWait => Mathf.Max(0f, doorWait);

    // =====================================
    // VALIDATE ENDING
    // =====================================

    public bool IsReady
    {
        get
        {
            if (endingCamera == null ||
                doorPoint == null ||
                outsidePoint == null)
            {
                return false;
            }

            Vector3 direction =
                outsidePoint.position - doorPoint.position;

            direction.y = 0f;

            return direction.sqrMagnitude > 0.25f;
        }
    }

    // =====================================
    // ENDING CAMERA
    // =====================================

    public bool ShowEndingCamera(CameraSwitcher switcher)
    {
        if (!IsReady || switcher == null)
            return false;

        switcher.enabled = false;

        if (switcher.cameraLobby != null)
            switcher.cameraLobby.SetActive(false);

        if (switcher.cameraElevator != null)
            switcher.cameraElevator.SetActive(false);

        endingCamera.gameObject.SetActive(true);
        endingCamera.enabled = true;

        return true;
    }

    // =====================================
    // PLAY FINAL ENDING
    // =====================================

    public IEnumerator Play(List<NPCQueueAgent> passengers)
    {
        if (!IsReady)
            yield break;

        // Disable normal crowd collision
        // during the final cinematic.
        foreach (NPCBodyCollisionManager manager in
                 FindObjectsByType<NPCBodyCollisionManager>(
                     FindObjectsSortMode.None))
        {
            manager.enabled = false;
        }

        Vector3 forward =
            outsidePoint.position - doorPoint.position;

        forward.y = 0f;
        forward.Normalize();

        Vector3 right =
            Vector3.Cross(Vector3.up, forward).normalized;

        List<Coroutine> activeLaunches =
            new List<Coroutine>();

        int index = 0;

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
                continue;

            agent.LeaveQueue();
            agent.SetGrabbed(false);

            PassengerExitStatus status =
                agent.GetComponent<PassengerExitStatus>();

            // Hide EXIT / LATE during ending.
            if (status != null)
                status.enabled = false;

            // Each NPC launches at a different time.
            float delay =
                Mathf.Min(index * launchStagger, 0.25f) +
                Random.Range(0f, randomLaunchDelay);

            // Random launch speed.
            float speedMultiplier = Random.Range(
                Mathf.Min(
                    minSpeedMultiplier,
                    maxSpeedMultiplier
                ),
                Mathf.Max(
                    minSpeedMultiplier,
                    maxSpeedMultiplier
                )
            );

            float flightTime = Mathf.Max(
                0.15f,
                launchDuration /
                Mathf.Max(0.1f, speedMultiplier)
            );

            Coroutine launch = StartCoroutine(
                EjectPassenger(
                    agent,
                    forward,
                    right,
                    delay,
                    flightTime
                )
            );

            activeLaunches.Add(launch);

            index++;
        }

        // Wait until every NPC finishes flying.
        foreach (Coroutine launch in activeLaunches)
        {
            if (launch != null)
                yield return launch;
        }

        yield return new WaitForSeconds(
            Mathf.Max(0f, finishDelay)
        );
    }

    // =====================================
    // RANDOM CHAOTIC EJECTION
    // =====================================

    private IEnumerator EjectPassenger(
        NPCQueueAgent agent,
        Vector3 forward,
        Vector3 right,
        float delay,
        float flightTime)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (agent == null)
            yield break;

        Transform npc = agent.transform;

        Vector3 start = npc.position;
        float groundY = start.y;

        // Random position inside doorway.
        float laneOffset = Random.Range(
            -doorLaneSpread,
            doorLaneSpread
        );

        Vector3 lane =
            right * laneOffset;

        // Move through the open doorway first.
        Vector3 insidePoint =
            doorPoint.position -
            forward * 0.45f +
            lane;

        insidePoint.y = groundY;

        Vector3 clearPoint =
            doorPoint.position +
            forward * doorClearDistance +
            lane;

        clearPoint.y = groundY;

        // =====================================
        // RANDOM LAUNCH DIRECTION
        // =====================================

        float sideOffset = Random.Range(
            -sideSpread,
            sideSpread
        );

        // Make some NPCs fly extremely sideways.
        if (Random.value < 0.45f)
        {
            float sideSign =
                Random.value < 0.5f ? -1f : 1f;

            sideOffset =
                sideSign *
                Random.Range(
                    sideSpread * 0.65f,
                    sideSpread
                );
        }

        float forwardDistance = Random.Range(
            0f,
            extraDistance
        );

        Vector3 landingPoint =
            outsidePoint.position +
            forward * forwardDistance +
            right * sideOffset;

        landingPoint.y = groundY;

        Quaternion facing =
            Quaternion.LookRotation(forward);

        Quaternion initialRotation = npc.rotation;

        // =====================================
        // RANDOM JUMP HEIGHT
        // =====================================

        float jumpHeight = Random.Range(
            Mathf.Min(
                jumpHeightMin,
                jumpHeightMax
            ),
            Mathf.Max(
                jumpHeightMin,
                jumpHeightMax
            )
        );

        // =====================================
        // RANDOM 3D ROTATION
        // =====================================

        float finalYaw = Random.Range(-180f, 180f);

        int maxYawTurns = Mathf.Max(
            0,
            Mathf.FloorToInt(spinDegrees / 360f)
        );

        float yawSpin =
            Random.Range(-maxYawTurns, maxYawTurns + 1)
            * 360f + finalYaw;

        float pitchSpin = 0f;
        float rollSpin = 0f;

        if (Random.value < flipChance)
        {
            int turns = Random.Range(
                1,
                Mathf.Max(1, maxFlipTurns) + 1
            );

            float sign =
                Random.value < 0.5f ? -1f : 1f;

            pitchSpin =
                turns * 360f * sign;
        }

        if (Random.value < flipChance)
        {
            int turns = Random.Range(
                1,
                Mathf.Max(1, maxFlipTurns) + 1
            );

            float sign =
                Random.value < 0.5f ? -1f : 1f;

            rollSpin =
                turns * 360f * sign;
        }

        // =====================================
        // MOVE TO DOOR QUICKLY
        // =====================================

        float elapsed = 0f;

        float duration =
            Mathf.Max(0.05f, gatherDuration);

        while (elapsed < duration)
        {
            if (agent == null)
                yield break;

            elapsed += Time.deltaTime;

            float p =
                Mathf.Clamp01(elapsed / duration);

            npc.position = Vector3.Lerp(
                start,
                insidePoint,
                p
            );

            npc.rotation = Quaternion.Slerp(
                initialRotation,
                facing,
                p
            );

            yield return null;
        }

        // =====================================
        // BURST THROUGH THE DOOR
        // =====================================

        elapsed = 0f;

        duration =
            Mathf.Max(0.05f, doorCrossDuration);

        while (elapsed < duration)
        {
            if (agent == null)
                yield break;

            elapsed += Time.deltaTime;

            float p =
                Mathf.Clamp01(elapsed / duration);

            npc.position = Vector3.Lerp(
                insidePoint,
                clearPoint,
                p
            );

            yield return null;
        }

        // =====================================
        // EXISTING LIMB RAGDOLL
        // =====================================

        PassengerRagdoll ragdoll =
            agent.GetComponent<PassengerRagdoll>();

        if (ragdoll == null)
        {
            ragdoll =
                agent.gameObject
                    .AddComponent<PassengerRagdoll>();
        }

        Vector3 launchDirection =
            (landingPoint - clearPoint).normalized;

        ragdoll.Jolt(launchDirection, 1f);

        // =====================================
        // EXTREME CHAOTIC FLIGHT
        // =====================================

        elapsed = 0f;

        while (elapsed < flightTime)
        {
            if (agent == null)
                yield break;

            elapsed += Time.deltaTime;

            float p =
                Mathf.Clamp01(elapsed / flightTime);

            // Fast initial burst, slower near landing.
            float burstProgress =
                1f - Mathf.Pow(
                    1f - p,
                    Mathf.Max(0.1f, burstPower)
                );

            Vector3 position = Vector3.Lerp(
                clearPoint,
                landingPoint,
                burstProgress
            );

            // Strong airborne arc.
            float arc =
                4f * p * (1f - p);

            position.y =
                groundY + arc * jumpHeight;

            npc.position = position;

            // Spin around all three axes.
            npc.rotation =
                facing *
                Quaternion.Euler(
                    pitchSpin * p,
                    yawSpin * p,
                    rollSpin * p
                );

            yield return null;
        }

        // =====================================
        // LANDING
        // =====================================

        if (agent != null)
        {
            npc.position = landingPoint;

            npc.rotation =
                facing *
                Quaternion.Euler(
                    0f,
                    finalYaw,
                    0f
                );

            // Extra limb reaction on landing.
            if (ragdoll != null)
            {
                ragdoll.Jolt(
                    -launchDirection,
                    1f
                );
            }
        }
    }
}
