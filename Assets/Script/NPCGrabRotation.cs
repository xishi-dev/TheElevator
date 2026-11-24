
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NPCGrabController))]
public class NPCGrabRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationAngle = 90f;

    private NPCGrabController grabController;

    private void Awake()
    {
        grabController = GetComponent<NPCGrabController>();
    }

    private void LateUpdate()
    {
        // Do not rotate during loading or ending.
        if (grabController == null ||
            !grabController.isActiveAndEnabled)
        {
            return;
        }

        if (Time.timeScale <= 0f)
            return;

        if (Keyboard.current == null ||
            Mouse.current == null)
        {
            return;
        }

        // Must be holding the left mouse button.
        if (!Mouse.current.leftButton.isPressed)
            return;

        // Rotate only once per R key press.
        if (!Keyboard.current.rKey.wasPressedThisFrame)
            return;

        NPCQueueAgent heldPassenger = null;

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null ||
                !agent.isActiveAndEnabled ||
                !agent.IsGrabbed ||
                agent.IsLowering)
            {
                continue;
            }

            // Only rotate when exactly one NPC is held.
            if (heldPassenger != null)
                return;

            heldPassenger = agent;
        }

        if (heldPassenger == null)
            return;

        // Rotate left around the vertical axis.
        heldPassenger.transform.Rotate(
            Vector3.up,
            -rotationAngle,
            Space.World
        );

        Physics.SyncTransforms();
    }
}
