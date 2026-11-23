
using UnityEngine;
using UnityEngine.InputSystem;

public class NPCGrabController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CameraSwitcher cameraSwitcher;
    [SerializeField] private NPCQueueManager queueManager;

    [Header("Elevator")]
    [SerializeField] private BoxCollider elevatorZone;
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

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        if (Mouse.current == null)
        {
            ReleaseNPC();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            grabbedTransform == null)
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
    }

    private void OnDisable() { ReleaseNPC(); }
    private void OnApplicationFocus(bool focused) { if (!focused) ReleaseNPC(); }

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

    private void TryGrabNPC()
    {
        Camera cam = GetCurrentCamera();

        if (cam == null)
        {
            Debug.LogWarning("กรุณากำหนด Player Camera");
            return;
        }

        Ray ray = cam.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        Physics.SyncTransforms();

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            npcLayer,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // A selectable NPC behind a wall must not be grabbed through that wall.
        if (Physics.Raycast(ray, out RaycastHit obstruction, hit.distance, obstacleLayers, QueryTriggerInteraction.Ignore))
            return;

        NPCQueueAgent agent =
            hit.collider.GetComponentInParent<NPCQueueAgent>();

        if (agent == null)
            return;

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
            return;
        }

        grabbedAgent = agent;
        grabbedTransform = agent.transform;
        grabbedCapsule = capsule;

        scrollOffset = 0f;

        scrollDirection = GetElevatorDirection();

        // หยุดระบบ Queue
        agent.SetGrabbed(true);
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

        // เลือกกล้องตามตำแหน่งตัวละครก่อนเริ่มลาก
        UpdateElevatorCameraState();

        // คำนวณตำแหน่งเมาส์
        RefreshDragOffset();

        Debug.Log("จับตัวละครแล้ว: " + grabbedTransform.name);
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

        // ตรวจสอบว่าตัวละครเข้าออกลิฟต์หรือไม่
        UpdateElevatorCameraState();
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
    // ELEVATOR CAMERA AUTO SWITCH
    // =====================================

    private bool IsInsideElevator()
    {
        if (elevatorZone == null ||
            grabbedTransform == null)
            return false;

        Vector3 worldPosition =
            grabbedCapsule != null
            ? grabbedCapsule.bounds.center
            : grabbedTransform.position;

        // แปลงตำแหน่งเป็น Local Space ของ ElevatorZone
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

    private void UpdateElevatorCameraState()
    {
        if (cameraSwitcher == null ||
            elevatorZone == null ||
            grabbedTransform == null)
            return;

        bool inside = IsInsideElevator();

        // ถ้ากล้องตรงกับตำแหน่งอยู่แล้ว ไม่ต้องเปลี่ยน
        if (inside == cameraSwitcher.IsElevatorView)
            return;

        if (inside)
        {
            cameraSwitcher.ShowElevator();

            Debug.Log("เข้า Elevator: เปลี่ยนกล้องแล้ว");
        }
        else
        {
            cameraSwitcher.ShowLobby();

            Debug.Log("ออก Elevator: กลับ Lobby แล้ว");
        }

        // ปรับตำแหน่งเมาส์หลังเปลี่ยนกล้อง
        RefreshDragOffset();
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
        if (grabbedTransform == null)
            return;

        NPCQueueAgent agent = grabbedAgent;

        grabbedTransform = null;
        grabbedAgent = null;
        grabbedCapsule = null;

        if (agent != null) agent.ReleaseToGround(lowerSpeed);

        Debug.Log("ปล่อยตัวละครแล้ว");
    }

}
