
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NPCGrabController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [SerializeField] private NPCQueueManager queueManager;

    [Header("Elevator")]
    [SerializeField] private Transform elevatorMoveDirection;

    [Header("Layers")]
    [SerializeField] private LayerMask npcLayer = ~0;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Grab Settings")]
    [SerializeField] private float liftHeight = 0.7f;
    [SerializeField] private float liftSpeed = 2f;
    [SerializeField] private float dragSpeed = 15f;
    [SerializeField] private float lowerSpeed = 2.5f;
    [SerializeField] private float maxRayDistance = 100f;

    [Header("Scroll Movement")]
    [SerializeField] private float scrollMovePerStep = 0.35f;
    [SerializeField] private float maxScrollDistance = 3f;
    [SerializeField] private bool invertScrollDirection = false;

    [Header("Wall Collision")]
    [SerializeField] private float collisionSkin = 0.04f;

    private NPCQueueAgent grabbedAgent;
    private Transform grabbedTransform;
    private CapsuleCollider grabbedCapsule;

    private Plane dragPlane;

    private Vector3 dragOffset;
    private Vector3 scrollDirection;

    private float originalY;
    private float scrollOffset;

    private bool warnedMissingWallLayer = false;
    public bool HasPassenger => grabbedAgent != null;
    public NPCQueueAgent HeldPassenger => grabbedAgent;
    public NPCQueueAgent HoveredPassenger { get; private set; }
    public bool IsHandingOff => handoff != null;
    public bool InputSuspended { get; set; }
    public event System.Action<NPCQueueAgent> PassengerReleased;
    public System.Func<NPCQueueAgent, bool> CanGrabPassenger { get; set; }
    public System.Func<NPCQueueAgent, Vector3[]> HandoffPath { get; set; }
    private Vector3[] handoff;
    private int handoffIndex;
    private float handoffDeadline;

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        if (InputSuspended) return;
        if (Mouse.current == null)
        {
            ReleaseNPC();
            return;
        }

        if (handoff == null &&
            (Mouse.current.rightButton.wasPressedThisFrame ||
             (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)))
        {
            if (grabbedAgent != null) BeginHandoff();
            else if (!PointerOverInterface()) TryQuickHandoff();
        }
        if (handoff != null)
        {
            StepHandoff();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            grabbedTransform == null && !PointerOverInterface())
        {
            TryGrabNPC();
        }

        if (grabbedTransform != null &&
            Mouse.current.leftButton.isPressed)
        {
            DragNPC();
        }

        if (grabbedTransform != null &&
            Mouse.current.leftButton.wasReleasedThisFrame)
        {
            ReleaseNPC();
        }
        HoveredPassenger = grabbedAgent == null && !PointerOverInterface()
            ? PassengerUnderPointer(GetCurrentCamera()) : null;
    }

    private static bool PointerOverInterface() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void OnDisable() { ReleaseNPC(); HoveredPassenger = null; }
    private void OnApplicationFocus(bool focused) { if (!focused) ReleaseNPC(); }

    public void ReleaseHeldPassenger() => ReleaseNPC();

    public bool IsVisibleFrom(Camera camera, Vector3 point) =>
        camera != null && !Physics.Linecast(camera.transform.position, point, obstacleLayers, QueryTriggerInteraction.Ignore);

    private void BeginHandoff()
    {
        StartHandoff(HandoffPath?.Invoke(grabbedAgent));
    }

    private void TryQuickHandoff()
    {
        if (HandoffPath == null) return;
        Physics.SyncTransforms();
        NPCQueueAgent agent = PassengerUnderPointer(GetCurrentCamera());
        if (agent == null) return;
        Vector3[] path = HandoffPath(agent);
        if (path != null && GrabPassenger(agent)) StartHandoff(path);
    }

    private void StartHandoff(Vector3[] path)
    {
        handoff = path;
        handoffIndex = 0;
        handoffDeadline = Time.time + 1.1f;
    }

    private void StepHandoff()
    {
        if (grabbedTransform == null || Time.time >= handoffDeadline || handoffIndex >= handoff.Length)
        {
            ReleaseNPC();
            return;
        }
        Vector3 current = grabbedTransform.position;
        Vector3 next = handoff[handoffIndex];
        next.y = current.y;
        // The handoff uses the same wall sweep as an ordinary drag.
        Vector3 safe = GetSafePosition(current, Vector3.MoveTowards(current, next, dragSpeed * 1.65f * Time.deltaTime));
        safe.y = Mathf.MoveTowards(current.y, originalY + liftHeight, 8f * Time.deltaTime);
        grabbedTransform.position = safe;
        Physics.SyncTransforms();
        if (Vector3.ProjectOnPlane(safe - next, Vector3.up).sqrMagnitude < .007f)
            handoffIndex++;
    }

    // =====================================
    // CAMERA
    // =====================================

    private Camera GetCurrentCamera()
    {
        if (cameraSwitcher != null &&
            cameraSwitcher.ActiveCamera != null)
        {
            return cameraSwitcher.ActiveCamera;
        }

        return playerCamera;
    }

    // =====================================
    // ELEVATOR DIRECTION
    // =====================================

    private Vector3 GetElevatorDirection()
    {
        Vector3 direction;

        if (elevatorMoveDirection != null)
        {
            direction = elevatorMoveDirection.forward;
        }
        else
        {
            Camera cam = GetCurrentCamera();

            direction = cam != null
                ? cam.transform.forward
                : Vector3.forward;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        direction.Normalize();

        if (invertScrollDirection)
            direction = -direction;

        return direction;
    }

    // =====================================
    // GRAB NPC
    // =====================================

    private NPCQueueAgent PassengerUnderPointer(Camera cam)
    {
        if (cam == null || Mouse.current == null) return null;
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, npcLayer, QueryTriggerInteraction.Ignore))
            return null;
        // A selectable NPC behind a wall must not be grabbed through that wall.
        if (Physics.Raycast(ray, hit.distance, obstacleLayers, QueryTriggerInteraction.Ignore)) return null;
        NPCQueueAgent agent = hit.collider.GetComponentInParent<NPCQueueAgent>();
        return agent != null && (CanGrabPassenger == null || CanGrabPassenger(agent)) ? agent : null;
    }

    private void TryGrabNPC()
    {
        Physics.SyncTransforms();
        NPCQueueAgent agent = PassengerUnderPointer(GetCurrentCamera());
        if (agent != null) GrabPassenger(agent);
    }

    private bool GrabPassenger(NPCQueueAgent agent)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule == null || !capsule.enabled)
        {
            Debug.LogWarning(
                "NPC ไม่มี Capsule Collider ที่เปิดใช้งาน"
            );
            return false;
        }

        grabbedAgent = agent;
        grabbedTransform = agent.transform;
        grabbedCapsule = capsule;

        scrollOffset = 0f;

        scrollDirection = GetElevatorDirection();

        // หยุดระบบ Queue
        agent.SetGrabbed(true);
        ShiftAudio.Play(ShiftCue.Grab);
        originalY = agent.GroundY;

        if (queueManager != null)
            queueManager.RemoveFromQueue(agent);

        agent.LeaveQueue();

        // สร้างระนาบลาก
        float targetY = originalY + liftHeight;

        dragPlane = new Plane(
            Vector3.up,
            new Vector3(0f, targetY, 0f)
        );

        // คำนวณตำแหน่งเมาส์
        RefreshDragOffset();
        return true;
    }

    // =====================================
    // DRAG NPC
    // =====================================

    private void DragNPC()
    {
        Camera cam = GetCurrentCamera();

        if (cam == null || grabbedTransform == null)
            return;

        Ray ray = cam.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!dragPlane.Raycast(ray, out float enter))
            return;

        // อ่าน Scroll Wheel
        HandleScrollMovement();

        // คำนวณตำแหน่งเป้าหมาย
        Vector3 targetPosition =
            ray.GetPoint(enter) +
            dragOffset +
            scrollDirection * scrollOffset;

        targetPosition.y = originalY + liftHeight;

        Vector3 currentPosition =
            grabbedTransform.position;

        // เคลื่อนที่แนวราบ
        Vector3 horizontalTarget = new Vector3(
            targetPosition.x,
            currentPosition.y,
            targetPosition.z
        );

        Vector3 desiredPosition = Vector3.MoveTowards(
            currentPosition,
            horizontalTarget,
            dragSpeed * Time.deltaTime
        );

        // ตรวจสอบกำแพงก่อนเคลื่อนที่
        Vector3 safePosition = GetSafePosition(
            currentPosition,
            desiredPosition
        );

        // ค่อย ๆ ยกตัวละคร
        safePosition.y = Mathf.MoveTowards(
            currentPosition.y,
            originalY + liftHeight,
            liftSpeed * Time.deltaTime
        );

        grabbedTransform.position = safePosition;

        Physics.SyncTransforms();

    }

    // =====================================
    // SCROLL MOVEMENT
    // =====================================

    private void HandleScrollMovement()
    {
        float rawScroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(rawScroll) < 0.01f)
            return;

        // รองรับเมาส์หลายรูปแบบ
        float steps = Mathf.Abs(rawScroll) > 10f
            ? rawScroll / 120f
            : rawScroll;

        steps = Mathf.Clamp(steps, -3f, 3f);

        scrollOffset +=
            steps * scrollMovePerStep;

        scrollOffset = Mathf.Clamp(
            scrollOffset,
            -maxScrollDistance,
            maxScrollDistance
        );
    }

    // =====================================
    // WALL COLLISION
    // =====================================

    private Vector3 GetSafePosition(
        Vector3 currentPosition,
        Vector3 desiredPosition)
    {
        Vector3 movement =
            desiredPosition - currentPosition;

        movement.y = 0f;

        float distance = movement.magnitude;

        if (distance < 0.0001f)
            return currentPosition;

        // ตรวจสอบว่าได้กำหนด Wall Layer หรือยัง
        if (obstacleLayers.value == 0)
        {
            if (!warnedMissingWallLayer)
            {
                Debug.LogWarning(
                    "กรุณากำหนด Obstacle Layers เป็น Wall"
                );

                warnedMissingWallLayer = true;
            }

            return desiredPosition;
        }

        Vector3 direction = movement.normalized;

        GetWorldCapsule(
            grabbedCapsule,
            out Vector3 point1,
            out Vector3 point2,
            out float radius
        );

        RaycastHit[] hits = Physics.CapsuleCastAll(
            point1,
            point2,
            radius,
            direction,
            distance + collisionSkin,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // ไม่ตรวจชนตัวเอง
            if (hit.collider.transform == grabbedTransform ||
                hit.collider.transform.IsChildOf(grabbedTransform))
                continue;

            // ยังไม่ให้ NPC ขวาง NPC ในระบบทดสอบนี้
            if (hit.collider.GetComponentInParent<NPCQueueAgent>() != null)
                continue;

            // PhysX reports initial floor overlaps with a synthetic normal opposing
            // the cast. Ignore colliders entirely below the capsule's feet first.
            float feetY = Mathf.Min(point1.y, point2.y) - radius;
            if (hit.collider.bounds.max.y <= feetY + collisionSkin)
                continue;

            // Floors and tangential contacts must not block horizontal dragging.
            if (Vector3.Dot(hit.normal, direction) >= -0.001f)
                continue;

            if (hit.distance < nearestDistance)
                nearestDistance = hit.distance;
        }

        if (nearestDistance < float.PositiveInfinity)
        {
            float allowedDistance = Mathf.Clamp(
                nearestDistance - collisionSkin,
                0f,
                distance
            );

            return currentPosition +
                   direction * allowedDistance;
        }

        return desiredPosition;
    }

    // คำนวณ CapsuleCollider ใน World Space
    private void GetWorldCapsule(
        CapsuleCollider capsule,
        out Vector3 point1,
        out Vector3 point2,
        out float radius)
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

        radius = capsule.radius * radiusScale;

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
    // REFRESH DRAG OFFSET
    // =====================================

    private void RefreshDragOffset()
    {
        Camera cam = GetCurrentCamera();

        if (cam == null ||
            grabbedTransform == null ||
            Mouse.current == null)
            return;

        Ray ray = cam.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!dragPlane.Raycast(ray, out float enter))
            return;

        Vector3 hitPoint = ray.GetPoint(enter);

        Vector3 liftedPosition =
            grabbedTransform.position;

        liftedPosition.y = originalY + liftHeight;

        dragOffset =
            liftedPosition -
            hitPoint -
            scrollDirection * scrollOffset;

        dragOffset.y = 0f;
    }

    // =====================================
    // RELEASE NPC
    // =====================================

    private void ReleaseNPC()
    {
        handoff = null;
        if (grabbedTransform == null)
            return;

        NPCQueueAgent agent = grabbedAgent;

        grabbedTransform = null;
        grabbedAgent = null;
        grabbedCapsule = null;

        if (agent != null)
        {
            agent.ReleaseToGround(lowerSpeed);
            ShiftAudio.Play(ShiftCue.Release);
            PassengerReleased?.Invoke(agent);
        }

    }

}
