
using UnityEngine;

[DefaultExecutionOrder(20000)]
[DisallowMultipleComponent]
public class PassengerExitStatus : MonoBehaviour
{
    public enum ExitState
    {
        Waiting,
        MustExit,
        Overdue,
        Delivered
    }

    [Header("Target Indicator")]
    [SerializeField] private int markerFontSize = 26;
    [SerializeField] private float markerWidth = 110f;
    [SerializeField] private float markerHeight = 42f;
    [SerializeField] private float ringRadius = 28f;
    [SerializeField] private float ringThickness = 4f;
    [SerializeField] private float markerGap = 14f;

    [Header("Position")]
    [SerializeField] private float headOffsetY = 0.08f;

    public ExitState State { get; private set; }
        = ExitState.Waiting;

    public int RequestedFloor { get; private set; }

    public bool HasBoarded { get; private set; }

    public bool MustExit =>
        State == ExitState.MustExit;

    public bool IsOverdue =>
        State == ExitState.Overdue;

    public bool IsPending =>
        MustExit || IsOverdue;

    private float penaltyRemainder;

    private Transform headBone;
    private CapsuleCollider capsule;
    private CameraSwitcher cameraSwitcher;

    private GUIStyle labelStyle;

    // NEW: Shared loading screen reference.
    private static CanvasGroup blackFadePanel;
    private static int lastLoadingSearchFrame = -1;

    // =====================================
    // SETUP
    // =====================================

    private void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                GetComponentInChildren<CapsuleCollider>();
        }

        cameraSwitcher =
            FindFirstObjectByType<CameraSwitcher>();

        FindHeadBone();
    }

    private void FindHeadBone()
    {
        Animator animator =
            GetComponentInChildren<Animator>(true);

        if (animator != null && animator.isHuman)
        {
            headBone = animator.GetBoneTransform(
                HumanBodyBones.Head
            );
        }

        if (headBone != null)
            return;

        Transform[] bones =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform bone in bones)
        {
            if (bone == null)
                continue;

            if (bone.name == "Head" ||
                bone.name.EndsWith(":Head"))
            {
                headBone = bone;
                return;
            }
        }
    }

    // =====================================
    // NEW: LOADING SCREEN CHECK
    // =====================================

    private bool IsLoadingScreenActive()
    {
        // Find the existing BlackFadePanel,
        // including when it is inactive.
        if (blackFadePanel == null &&
            lastLoadingSearchFrame != Time.frameCount)
        {
            lastLoadingSearchFrame = Time.frameCount;

            CanvasGroup[] groups =
                FindObjectsByType<CanvasGroup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (CanvasGroup group in groups)
            {
                if (group == null)
                    continue;

                if (group.gameObject.name == "BlackFadePanel")
                {
                    blackFadePanel = group;
                    break;
                }
            }
        }

        if (blackFadePanel == null)
            return false;

        // Hide markers during fade out,
        // loading and fade in.
        return blackFadePanel.gameObject.activeInHierarchy;
    }

    // =====================================
    // PASSENGER STATUS
    // =====================================

    public void MarkBoarded()
    {
        HasBoarded = true;
    }

    public void RequestExit(int floor)
    {
        if (State != ExitState.Waiting)
            return;

        RequestedFloor = floor;
        State = ExitState.MustExit;
    }

    public void MarkOverdue()
    {
        if (State != ExitState.MustExit)
            return;

        State = ExitState.Overdue;
    }

    public void MarkDelivered()
    {
        if (!IsPending)
            return;

        State = ExitState.Delivered;
    }

    // =====================================
    // LATE PENALTY
    // =====================================

    public int CalculateLatePenalty(
        float seconds,
        float pointsPerSecond)
    {
        if (!IsOverdue)
            return 0;

        penaltyRemainder +=
            Mathf.Max(0f, seconds) *
            Mathf.Max(0f, pointsPerSecond);

        int points =
            Mathf.FloorToInt(penaltyRemainder);

        penaltyRemainder -= points;

        return points;
    }

    // =====================================
    // ACTIVE CAMERA
    // =====================================

    private Camera GetActiveCamera()
    {
        if (cameraSwitcher != null)
        {
            Camera active =
                cameraSwitcher.ActiveCamera;

            if (active != null &&
                active.isActiveAndEnabled)
            {
                return active;
            }
        }

        if (Camera.main != null &&
            Camera.main.isActiveAndEnabled)
        {
            return Camera.main;
        }

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != null &&
                cam.isActiveAndEnabled)
            {
                return cam;
            }
        }

        return null;
    }

    // =====================================
    // GET EXACT HEAD POSITION
    // =====================================

    private Vector3 GetHeadPosition()
    {
        // Prefer the real animated head bone.
        // Do not use SkinnedMeshRenderer.bounds
        // because it can push the marker
        // far above the visible character.
        if (headBone != null)
        {
            return headBone.position +
                   Vector3.up * headOffsetY;
        }

        // Fallback for NPCs without a head bone.
        if (capsule != null && capsule.enabled)
        {
            Bounds bounds = capsule.bounds;

            return new Vector3(
                bounds.center.x,
                bounds.max.y - 0.15f,
                bounds.center.z
            );
        }

        return transform.position +
               Vector3.up * 1.65f;
    }

    // =====================================
    // GUI STYLE
    // =====================================

    private void EnsureStyle()
    {
        if (labelStyle != null)
            return;

        labelStyle = new GUIStyle(GUI.skin.label);

        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment =
            TextAnchor.MiddleCenter;

        labelStyle.wordWrap = false;
    }

    // =====================================
    // DRAW SCREEN-SPACE LINE
    // =====================================

    private void DrawLine(
        Vector2 from,
        Vector2 to,
        Color color,
        float thickness)
    {
        Vector2 direction = to - from;

        if (direction.sqrMagnitude < 0.01f)
            return;

        float length = direction.magnitude;

        float angle = Mathf.Atan2(
            direction.y,
            direction.x
        ) * Mathf.Rad2Deg;

        Vector2 center = (from + to) * 0.5f;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        GUI.color = color;

        GUIUtility.RotateAroundPivot(
            angle,
            center
        );

        GUI.DrawTexture(
            new Rect(
                center.x - length * 0.5f,
                center.y - thickness * 0.5f,
                length,
                thickness
            ),
            Texture2D.whiteTexture
        );

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    // =====================================
    // DRAW RING AROUND NPC HEAD
    // =====================================

    private void DrawTargetRing(
        Vector2 center,
        float radius,
        Color color,
        float thickness)
    {
        const int segments = 16;

        for (int i = 0; i < segments; i++)
        {
            float a1 =
                i * Mathf.PI * 2f / segments;

            float a2 =
                (i + 1) *
                Mathf.PI * 2f / segments;

            Vector2 from = center +
                new Vector2(
                    Mathf.Cos(a1),
                    Mathf.Sin(a1)
                ) * radius;

            Vector2 to = center +
                new Vector2(
                    Mathf.Cos(a2),
                    Mathf.Sin(a2)
                ) * radius;

            DrawLine(
                from,
                to,
                color,
                thickness
            );
        }
    }

    // =====================================
    // DRAW EXIT / LATE INDICATOR
    // =====================================

    private void OnGUI()
    {
        if (!IsPending)
            return;

        if (Event.current.type != EventType.Repaint)
            return;

        // NEW:
        // Do not draw EXIT / LATE over
        // the floor loading screen.
        //
        // This only hides the visual marker.
        // Passenger status remains unchanged.
        if (IsLoadingScreenActive())
            return;

        Camera cam = GetActiveCamera();

        if (cam == null)
            return;

        Vector3 screenPoint =
            cam.WorldToScreenPoint(
                GetHeadPosition()
            );

        if (screenPoint.z <= 0f)
            return;

        float scale = Mathf.Clamp(
            Screen.height / 1080f,
            0.8f,
            1.5f
        );

        float headX = screenPoint.x;
        float headY =
            Screen.height - screenPoint.y;

        // Do not draw a misleading ring
        // if the NPC is completely offscreen.
        if (headX < 0f ||
            headX > Screen.width ||
            headY < 0f ||
            headY > Screen.height)
        {
            return;
        }

        EnsureStyle();

        float width = markerWidth * scale;
        float height = markerHeight * scale;

        float radius = ringRadius * scale;
        float thickness =
            ringThickness * scale;

        Vector2 head = new Vector2(
            headX,
            headY
        );

        // Put the label above the NPC head.
        float labelX =
            headX - width * 0.5f;

        float labelY =
            headY - radius -
            markerGap * scale - height;

        bool placeBelow = labelY < 8f;

        // If the head is near the top edge,
        // place the label below the head.
        if (placeBelow)
        {
            labelY =
                headY + radius +
                markerGap * scale;
        }

        labelX = Mathf.Clamp(
            labelX,
            8f,
            Mathf.Max(
                8f,
                Screen.width - width - 8f
            )
        );

        labelY = Mathf.Clamp(
            labelY,
            8f,
            Mathf.Max(
                8f,
                Screen.height - height - 8f
            )
        );

        Rect labelRect = new Rect(
            labelX,
            labelY,
            width,
            height
        );

        Color accent = IsOverdue
            ? new Color(1f, 0.22f, 0.22f, 1f)
            : new Color(0.2f, 1f, 0.35f, 1f);

        string label = IsOverdue
            ? "LATE!"
            : "EXIT";

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;

        GUI.depth = -150;

        // Connect label to the actual head.
        Vector2 lineStart = new Vector2(
            labelRect.center.x,
            placeBelow
                ? labelRect.yMin
                : labelRect.yMax
        );

        DrawLine(
            lineStart,
            head,
            accent,
            3f * scale
        );

        // Draw a visible ring around
        // the NPC who needs to leave.
        DrawTargetRing(
            head,
            radius,
            accent,
            thickness
        );

        // Small dot exactly on the head.
        float dotSize = 7f * scale;

        GUI.color = accent;

        GUI.DrawTexture(
            new Rect(
                headX - dotSize * 0.5f,
                headY - dotSize * 0.5f,
                dotSize,
                dotSize
            ),
            Texture2D.whiteTexture
        );

        // Dark label background.
        GUI.color = new Color(
            0.025f,
            0.035f,
            0.04f,
            0.9f
        );

        GUI.DrawTexture(
            labelRect,
            Texture2D.whiteTexture
        );

        // Colored label border.
        GUI.color = accent;

        GUI.DrawTexture(
            new Rect(
                labelRect.x,
                labelRect.y,
                5f * scale,
                labelRect.height
            ),
            Texture2D.whiteTexture
        );

        // Label text.
        labelStyle.fontSize =
            Mathf.RoundToInt(
                markerFontSize * scale
            );

        labelStyle.normal.textColor = accent;

        GUI.color = Color.white;

        GUI.Label(
            labelRect,
            label,
            labelStyle
        );

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }
}
