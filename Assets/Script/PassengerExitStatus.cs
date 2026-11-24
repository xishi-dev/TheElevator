
using TMPro;
using UnityEngine;

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
    private TextMeshPro markerText;
    private Transform markerTransform;

    private void Awake()
    {
        CreateMarker();
    }

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

        RefreshMarker();
    }

    public void MarkOverdue()
    {
        if (State != ExitState.MustExit)
            return;

        State = ExitState.Overdue;
        RefreshMarker();
    }

    public void MarkDelivered()
    {
        if (!IsPending)
            return;

        State = ExitState.Delivered;
        RefreshMarker();
    }

    public int CalculateLatePenalty(
        float seconds,
        float pointsPerSecond)
    {
        if (!IsOverdue)
            return 0;

        penaltyRemainder +=
            Mathf.Max(0f, seconds) *
            Mathf.Max(0f, pointsPerSecond);

        int points = Mathf.FloorToInt(
            penaltyRemainder
        );

        penaltyRemainder -= points;

        return points;
    }

    private void CreateMarker()
    {
        GameObject marker = new GameObject(
            "Passenger Exit Marker"
        );

        marker.transform.SetParent(
            transform,
            false
        );

        markerTransform = marker.transform;

        markerText = marker.AddComponent<TextMeshPro>();

        markerText.text = "";
        markerText.fontSize = 4f;
        markerText.fontStyle = FontStyles.Bold;
        markerText.alignment = TextAlignmentOptions.Center;
        markerText.textWrappingMode = TextWrappingModes.NoWrap;

        markerTransform.localScale =
            Vector3.one * 0.15f;

        marker.SetActive(false);
    }

    private void RefreshMarker()
    {
        if (markerText == null)
            return;

        bool show = IsPending;

        markerText.gameObject.SetActive(show);

        if (!show)
            return;

        if (IsOverdue)
        {
            markerText.text = "! LATE !";
            markerText.color =
                new Color(1f, 0.2f, 0.15f);
        }
        else
        {
            markerText.text = "EXIT";
            markerText.color =
                new Color(0.2f, 1f, 0.3f);
        }
    }

    private void LateUpdate()
    {
        if (markerTransform == null ||
            !markerTransform.gameObject.activeSelf)
            return;

        SkinnedMeshRenderer skin =
            GetComponentInChildren<SkinnedMeshRenderer>();

        float height = transform.position.y + 2.2f;

        if (skin != null)
            height = skin.bounds.max.y + 0.3f;

        markerTransform.position = new Vector3(
            transform.position.x,
            height,
            transform.position.z
        );

        Camera cam = Camera.main;

        if (cam == null)
        {
            Camera[] cameras = Camera.allCameras;

            if (cameras.Length > 0)
                cam = cameras[0];
        }

        if (cam != null)
        {
            markerTransform.rotation =
                Quaternion.LookRotation(
                    markerTransform.position -
                    cam.transform.position,
                    Vector3.up
                );
        }
    }
}
