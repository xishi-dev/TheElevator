
using UnityEngine;

public class NPCQueueAgent : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float rotateSpeed = 8f;

    private Vector3 targetPosition;
    private bool hasTarget;
    private bool isGrabbed;
    private bool isInQueue = true;
    private bool isLowering;

    private float groundY;
    private float loweringSpeed;

    private NPCCharacterAnimation visuals;
    private PassengerRagdoll ragdoll;

    public bool IsInQueue => isInQueue;
    public bool IsGrabbed => isGrabbed;
    public bool IsLowering => isLowering;
    public float GroundY => groundY;

    private void Awake()
    {
        groundY = transform.position.y;

        visuals = GetComponent<NPCCharacterAnimation>();
        ragdoll = GetComponent<PassengerRagdoll>();
    }

    public void SetTarget(Vector3 position)
    {
        if (isGrabbed || !isInQueue)
            return;

        targetPosition = position;
        hasTarget = true;
    }

    public void SetGrabbed(bool grabbed)
    {
        if (grabbed && !isGrabbed && !isLowering)
            groundY = transform.position.y;

        isGrabbed = grabbed;
        isLowering = false;

        if (visuals == null)
            visuals = GetComponent<NPCCharacterAnimation>();

        if (visuals != null)
        {
            if (grabbed)
                visuals.SetHeld(true);
            else
                visuals.Land();
        }
    }

    public void ReleaseToGround(float speed)
    {
        if (!isGrabbed && !isLowering)
            return;

        isLowering = true;
        loweringSpeed = Mathf.Max(0.1f, speed);

        if (visuals != null)
            visuals.BeginFall();
    }

    public void LeaveQueue()
    {
        isInQueue = false;
        hasTarget = false;
    }

    private void Update()
    {
        if (isLowering)
        {
            Vector3 position = transform.position;

            position.y = Mathf.MoveTowards(
                position.y,
                groundY,
                loweringSpeed * Time.deltaTime
            );

            transform.position = position;

            if (Mathf.Abs(position.y - groundY) <= 0.0001f)
            {
                SetGrabbed(false);

                // NEW: กระตุกแขนขาด้วย Ragdoll ตอนลงพื้น
                if (ragdoll == null)
                    ragdoll = GetComponent<PassengerRagdoll>();

                if (ragdoll != null)
                {
                    ragdoll.Jolt(
                        -transform.up,
                        0.45f
                    );
                }
            }

            return;
        }

        if (isGrabbed)
            return;

        groundY = transform.position.y;

        if (!isInQueue || !hasTarget)
            return;

        Vector3 destination = targetPosition;
        destination.y = transform.position.y;

        Vector3 direction =
            destination - transform.position;

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            Mathf.Max(0f, moveSpeed) * Time.deltaTime
        );

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Mathf.Max(0f, rotateSpeed) * Time.deltaTime
            );
        }
    }
}
