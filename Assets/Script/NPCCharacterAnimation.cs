using UnityEngine;

/// <summary>Visual state only; queue and grab controllers retain ownership of movement.</summary>
[DisallowMultipleComponent]
public sealed class NPCCharacterAnimation : MonoBehaviour
{
    public enum Motion { Idle, Walk, Grabbed, Falling, Land, HeadShake, ArmFlick, LegShake, LookAround, Stretch, Shrug }
    [SerializeField] private Animator animator;
    [SerializeField, Range(0, 5)] private int favouriteGesture;
    [SerializeField] private Vector2 gestureInterval = new Vector2(3.5f, 8f);
    private static readonly int MotionId = Animator.StringToHash("Motion");
    private static readonly int PlaybackId = Animator.StringToHash("PlaybackSpeed");
    private Vector3 lastPosition;
    private float nextGesture, gestureEnd, landingEnd;
    private float personalitySpeed = 1;
    private bool held, falling;
    private Motion current;
    public Motion CurrentMotion => current;
    public bool IsHeld => held;
    public bool IsFalling => falling;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        personalitySpeed = Random.Range(.91f, 1.09f);
        lastPosition = transform.position;
        ScheduleGesture();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.SetFloat(PlaybackId, personalitySpeed);
            animator.Play("Idle", 0, Random.value);
        }
    }

    private void OnEnable() { lastPosition = transform.position; }

    private void LateUpdate()
    {
        Vector3 displacement = transform.position - lastPosition;
        lastPosition = transform.position;
        if (animator == null) return;
        if (held) { SetMotion(Motion.Grabbed); return; }
        if (falling) { SetMotion(Motion.Falling); return; }
        if (Time.time < landingEnd) { SetMotion(Motion.Land); return; }
        float speed = new Vector2(displacement.x, displacement.z).magnitude / Mathf.Max(Time.deltaTime, .0001f);
        if (speed > .06f)
        {
            gestureEnd = 0;
            ScheduleGesture();
            SetMotion(Motion.Walk);
            animator.SetFloat(PlaybackId, personalitySpeed * Mathf.Clamp(speed / 1.8f, .65f, 1.6f));
            return;
        }
        animator.SetFloat(PlaybackId, personalitySpeed);
        if (Time.time < gestureEnd) return;
        if (Time.time >= nextGesture)
        {
            int gesture = Random.value < .35f ? Mathf.Clamp(favouriteGesture, 0, 5) : Random.Range(0, 6);
            SetMotion((Motion)((int)Motion.HeadShake + gesture));
            gestureEnd = Time.time + 1.8f / personalitySpeed;
            ScheduleGesture();
        }
        else SetMotion(Motion.Idle);
    }

    public void SetHeld(bool value)
    {
        held = value;
        falling = false;
        gestureEnd = landingEnd = 0;
        if (value) SetMotion(Motion.Grabbed);
        else { SetMotion(Motion.Idle); ScheduleGesture(); }
    }

    public void BeginFall()
    {
        held = false;
        falling = true;
        gestureEnd = landingEnd = 0;
        SetMotion(Motion.Falling);
    }

    public void Land()
    {
        held = falling = false;
        landingEnd = Time.time + .65f / Mathf.Max(.01f, personalitySpeed);
        gestureEnd = 0;
        SetMotion(Motion.Land);
        ScheduleGesture();
    }

    private void SetMotion(Motion motion)
    {
        current = motion;
        if (animator == null) return;
        animator.SetInteger(MotionId, (int)motion);
        if (motion != Motion.Walk) animator.SetFloat(PlaybackId, personalitySpeed > 0 ? personalitySpeed : 1);
    }

    private void ScheduleGesture()
    {
        float minimum = Mathf.Max(.1f, gestureInterval.x);
        nextGesture = Time.time + Random.Range(minimum, Mathf.Max(minimum, gestureInterval.y));
    }
}
