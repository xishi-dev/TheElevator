using UnityEngine;

/// <summary>Only used by the separate character preview scene.</summary>
public sealed class CharacterAnimationDemo : MonoBehaviour
{
    public Animator animator;
    public TextMesh caption;
    public float phaseOffset;
    private float initialY;
    private int previous = -1;
    private static readonly string[] Names = { "IDLE", "WALK", "GRABBED", "FALLING", "LAND", "HEAD SHAKE", "ARM FLICK", "LEG SHAKE", "LOOK AROUND", "STRETCH", "SHRUG" };
    private void Start() { initialY = transform.position.y; }
    private void Update()
    {
        float cycle = Time.time + phaseOffset;
        int state = Mathf.FloorToInt(cycle / 2.4f) % Names.Length;
        if (state != previous)
        {
            animator.SetInteger("Motion", state);
            animator.SetFloat("PlaybackSpeed", 1);
            if (caption != null) caption.text = name + "\n" + Names[state];
            previous = state;
        }
        var p = transform.position;
        float t = cycle % 2.4f;
        p.y = initialY + (state == 2 ? Mathf.SmoothStep(0, .65f, t / .35f) : state == 3 ? Mathf.Max(0, .65f - t * .8f) : 0);
        transform.position = p;
    }
}
