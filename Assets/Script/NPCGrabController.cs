
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

    [Header("Mouse Drag")]
    [SerializeField] private float mouseSensitivity = 1f;
    [SerializeField] private float maxMouseDeltaPixels = 150f;
    [SerializeField] private float minLobbyCameraDistance = 1.2f;

    [Header("Scroll Movement")]
    [SerializeField] private float scrollMovePerStep = 0.35f;
    [SerializeField] private float maxScrollDistance = 3f;
    [SerializeField] private bool invertScrollDirection = false;

    [Header("Wall Collision")]
    [SerializeField] private float collisionSkin = 0.04f;

    [Header("Camera")]
    [SerializeField] private bool switchCameraOnRelease = true;

    private NPCQueueAgent grabbedAgent;
    private Transform grabbedTransform;
    private CapsuleCollider grabbedCapsule;

    private Vector3 dragTarget;
    private Vector3 scrollDirection;
    private Vector2 lastMousePosition;

    private Camera dragCamera;

    private float originalY;
    private float scrollOffset;

    private bool warnedMissingWallLayer;

    public bool HasPassenger => grabbedAgent != null;
    public NPCQueueAgent HeldPassenger => grabbedAgent;

    public NPCQueueAgent HoveredPassenger
    {
        get;
        private set;
    }

    public bool InputSuspended { get; set; }

    public event System.Action<NPCQueueAgent> PassengerReleased;

    public System.Func<NPCQueueAgent, bool>
        CanGrabPassenger
    { get; set; }
    private void Update()
    {
        if (InputSuspended)
        {
            HoveredPassenger = null;
            return;
        }

        if (Mouse.current == null)
        {
            ReleaseNPC();
            HoveredPassenger = null;
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame &&
            grabbedAgent == null &&
            !PointerOverInterface())
        {
            TryGrabNPC();
        }

        if (grabbedAgent != null &&
            Mouse.current.leftButton.isPressed)
        {
            DragNPC();
        }

        if (grabbedAgent != null &&
            Mouse.current.leftButton.wasReleasedThisFrame)
        {
            ReleaseNPC();
        }

        HoveredPassenger =
            grabbedAgent == null &&
            !PointerOverInterface()
                ? PassengerUnderPointer(GetCurrentCamera())
                : null;
    }

    private static bool PointerOverInterface()
    {
        return EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }

    private void OnDisable()
    {
        ReleaseNPC();
        HoveredPassenger = null;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            ReleaseNPC();
        }
    }

    public void ReleaseHeldPassenger()
    {
        ReleaseNPC();
    }

    public bool IsVisibleFrom(Camera camera, Vector3 point)
    {
        return camera != null &&
            !Physics.Linecast(
                camera.transform.position,
                point,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );
    }
    private Camera GetCurrentCamera()
    {
        if (cameraSwitcher != null &&
            cameraSwitcher.ActiveCamera != null)
        {
            return cameraSwitcher.ActiveCamera;
        }

        return playerCamera;
    }

    private Camera GetLobbyCamera()
    {
        if (cameraSwitcher != null &&
            cameraSwitcher.cameraLobby != null)
        {
            Camera cam =
                cameraSwitcher.cameraLobby.GetComponent<Camera>();

            if (cam == null)
            {
                cam = cameraSwitcher.cameraLobby
                    .GetComponentInChildren<Camera>(true);
            }

            if (cam != null)
            {
                return cam;
            }
        }

        return playerCamera;
    }
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
        {
            direction = Vector3.forward;
        }

        direction.Normalize();

        if (invertScrollDirection)
        {
            direction = -direction;
        }

        return direction;
    }
    private NPCQueueAgent PassengerUnderPointer(Camera cam)
    {
        if (cam == null || Mouse.current == null)
        {
            return null;
        }

        Ray ray = cam.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxRayDistance,
            npcLayer,
            QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        if (Physics.Raycast(
            ray,
            hit.distance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        NPCQueueAgent agent =
            hit.collider.GetComponentInParent<NPCQueueAgent>();

        if (agent == null)
        {
            return null;
        }

        if (CanGrabPassenger != null &&
            !CanGrabPassenger(agent))
        {
            return null;
        }

        return agent;
    }
    private void TryGrabNPC()
    {
        Physics.SyncTransforms();

        NPCQueueAgent agent =
            PassengerUnderPointer(GetCurrentCamera());

        if (agent == null)
        {
            return;
        }

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
                "NPC ไม่มี CapsuleCollider ที่เปิดใช้งาน"
            );

            return;
        }

        grabbedAgent = agent;
        grabbedTransform = agent.transform;
        grabbedCapsule = capsule;

        agent.SetGrabbed(true);

        originalY = agent.GroundY;

        if (queueManager != null)
        {
            queueManager.RemoveFromQueue(agent);
        }

        agent.LeaveQueue();

        scrollOffset = 0f;
        scrollDirection = GetElevatorDirection();

        dragTarget = grabbedTransform.position;

        lastMousePosition =
            Mouse.current.position.ReadValue();

        dragCamera = GetCurrentCamera();
    }
    private void DragNPC()
    {
        if (grabbedTransform == null ||
            Mouse.current == null)
        {
            return;
        }

        Camera cam = GetCurrentCamera();

        if (cam == null)
        {
            return;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (cam != dragCamera)
        {
            dragCamera = cam;
            lastMousePosition = mousePosition;
            dragTarget = grabbedTransform.position;
            return;
        }

        Vector2 mouseDelta =
            mousePosition - lastMousePosition;

        lastMousePosition = mousePosition;

        mouseDelta = Vector2.ClampMagnitude(
            mouseDelta,
            Mathf.Max(1f, maxMouseDeltaPixels)
        );

        float depth = Vector3.Dot(
            grabbedTransform.position -
            cam.transform.position,
            cam.transform.forward
        );

        depth = Mathf.Clamp(depth, 1f, 20f);

        float worldPerPixel;

        if (cam.orthographic)
        {
            worldPerPixel =
                cam.orthographicSize * 2f /
                Mathf.Max(1f, Screen.height);
        }
        else
        {
            worldPerPixel =
                2f *
                Mathf.Tan(
                    cam.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad
                ) *
                depth /
                Mathf.Max(1f, Screen.height);
        }

        Vector3 right =
            Vector3.ProjectOnPlane(
                cam.transform.right,
                Vector3.up
            ).normalized;

        Vector3 up =
            Vector3.ProjectOnPlane(
                cam.transform.up,
                Vector3.up
            );

        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.ProjectOnPlane(
                cam.transform.forward,
                Vector3.up
            );
        }

        if (up.sqrMagnitude > 0.0001f)
        {
            up.Normalize();
        }

        float sensitivity =
            Mathf.Max(0.01f, mouseSensitivity);

        dragTarget +=
            (
                right * mouseDelta.x +
                up * mouseDelta.y
            ) *
            worldPerPixel *
            sensitivity;

        float previousScroll = scrollOffset;

        HandleScrollMovement();

        dragTarget +=
            scrollDirection *
            (scrollOffset - previousScroll);

        dragTarget =
            KeepInFrontOfLobbyCamera(dragTarget);

        Vector3 currentPosition =
            grabbedTransform.position;

        Vector3 horizontalTarget = new Vector3(
            dragTarget.x,
            currentPosition.y,
            dragTarget.z
        );

        Vector3 desiredPosition =
            Vector3.MoveTowards(
                currentPosition,
                horizontalTarget,
                dragSpeed * Time.deltaTime
            );

        Vector3 safePosition =
            GetSafePosition(
                currentPosition,
                desiredPosition
            );

        if (Vector3.Distance(
            safePosition,
            desiredPosition) > 0.001f)
        {
            dragTarget.x = safePosition.x;
            dragTarget.z = safePosition.z;
        }

        safePosition.y = Mathf.MoveTowards(
            currentPosition.y,
            originalY + liftHeight,
            liftSpeed * Time.deltaTime
        );

        grabbedTransform.position = safePosition;

        Physics.SyncTransforms();

        UpdateElevatorCameraState();
    }
    private void HandleScrollMovement()
    {
        float rawScroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(rawScroll) < 0.01f)
        {
            return;
        }

        float steps =
            Mathf.Abs(rawScroll) > 10f
                ? rawScroll / 120f
                : rawScroll;

        steps = Mathf.Clamp(steps, -3f, 3f);

        scrollOffset = Mathf.Clamp(
            scrollOffset +
            steps * scrollMovePerStep,
            -maxScrollDistance,
            maxScrollDistance
        );
    }
    private Vector3 KeepInFrontOfLobbyCamera(Vector3 position)
    {
        Camera lobby = GetLobbyCamera();

        if (lobby == null)
        {
            return position;
        }

        Vector3 forward =
            Vector3.ProjectOnPlane(
                lobby.transform.forward,
                Vector3.up
            );

        if (forward.sqrMagnitude < 0.0001f)
        {
            return position;
        }

        forward.Normalize();

        float depth = Vector3.Dot(
            position - lobby.transform.position,
            forward
        );

        float minimum =
            Mathf.Max(0.1f, minLobbyCameraDistance);

        if (depth < minimum)
        {
            position +=
                forward * (minimum - depth);
        }

        return position;
    }
    private Vector3 GetSafePosition(
        Vector3 currentPosition,
        Vector3 desiredPosition)
    {
        Vector3 movement =
            desiredPosition - currentPosition;

        movement.y = 0f;

        float distance = movement.magnitude;

        if (distance < 0.0001f)
        {
            return currentPosition;
        }

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

        float nearestDistance =
            float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform == grabbedTransform ||
                hit.collider.transform.IsChildOf(grabbedTransform))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<
                NPCQueueAgent>() != null)
            {
                continue;
            }

            float feetY =
                Mathf.Min(point1.y, point2.y) -
                radius;

            if (hit.collider.bounds.max.y <=
                feetY + collisionSkin)
            {
                continue;
            }

            if (Vector3.Dot(
                hit.normal,
                direction) >= -0.001f)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
            }
        }

        if (nearestDistance <
            float.PositiveInfinity)
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
    private bool IsInsideElevator(
        Transform npc,
        CapsuleCollider capsule)
    {
        if (elevatorZone == null || npc == null)
        {
            return false;
        }

        Vector3 worldPosition =
            capsule != null
                ? capsule.bounds.center
                : npc.position;

        Vector3 localPosition =
            elevatorZone.transform.InverseTransformPoint(
                worldPosition
            ) - elevatorZone.center;

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
        {
            return;
        }

        bool inside = IsInsideElevator(
            grabbedTransform,
            grabbedCapsule
        );

        if (inside == cameraSwitcher.IsElevatorView)
        {
            return;
        }

        if (inside)
        {
            cameraSwitcher.ShowElevator();
        }
        else
        {
            cameraSwitcher.ShowLobby();
        }

        dragCamera = GetCurrentCamera();

        if (Mouse.current != null)
        {
            lastMousePosition =
                Mouse.current.position.ReadValue();
        }

        dragTarget = grabbedTransform.position;
    }
    private void ReleaseNPC()
    {
        if (grabbedTransform == null)
        {
            return;
        }

        bool insideElevator = IsInsideElevator(
            grabbedTransform,
            grabbedCapsule
        );

        NPCQueueAgent agent = grabbedAgent;

        grabbedTransform = null;
        grabbedAgent = null;
        grabbedCapsule = null;
        dragCamera = null;

        if (agent != null)
        {
            agent.ReleaseToGround(lowerSpeed);

            PassengerReleased?.Invoke(agent);
        }

        if (switchCameraOnRelease &&
            cameraSwitcher != null &&
            elevatorZone != null &&
            insideElevator != cameraSwitcher.IsElevatorView)
        {
            if (insideElevator)
            {
                cameraSwitcher.ShowElevator();
            }
            else
            {
                cameraSwitcher.ShowLobby();
            }
        }
    }
}
