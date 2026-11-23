
using System.Collections.Generic;
using UnityEngine;

public class ActiveRagdollMuscles : MonoBehaviour
{
    [Header("Animation Reference")]
    [SerializeField] private Transform poseReference;

    [Header("Testing")]
    [SerializeField] private bool torsoOnly = true;

    [Header("Muscle Settings")]
    [SerializeField] private float muscleStrength = 8f;
    [SerializeField] private float muscleDamping = 2f;
    [SerializeField] private float maxMuscleTorque = 2f;
    [SerializeField] private float deadZoneDegrees = 2f;

    private class Muscle
    {
        public Rigidbody body;
        public Rigidbody parentBody;

        public Transform target;
        public Transform targetParent;
    }

    private readonly List<Muscle> muscles =
        new List<Muscle>();

    private void OnEnable()
    {
        if (Application.isPlaying)
            SetupMuscles();
    }

    private void SetupMuscles()
    {
        muscles.Clear();

        if (poseReference == null)
        {
            Debug.LogError(
                "Active Ragdoll: Missing Pose Reference!"
            );

            enabled = false;
            return;
        }

        // Find reference bones by name.
        Dictionary<string, Transform> targets =
            new Dictionary<string, Transform>();

        Transform[] referenceBones =
            poseReference.GetComponentsInChildren<
                Transform>(true);

        foreach (Transform bone in referenceBones)
        {
            if (!targets.ContainsKey(bone.name))
                targets.Add(bone.name, bone);
        }

        // Use existing Character Joints.
        CharacterJoint[] joints =
            GetComponentsInChildren<CharacterJoint>();

        foreach (CharacterJoint joint in joints)
        {
            if (joint.connectedBody == null)
                continue;

            string boneName = joint.gameObject.name;

            // First test only the upper torso.
            if (torsoOnly &&
                boneName != "Spine" &&
                boneName != "Chest" &&
                boneName != "Head")
            {
                continue;
            }

            if (!targets.TryGetValue(
                boneName,
                out Transform target))
                continue;

            if (!targets.TryGetValue(
                joint.connectedBody.gameObject.name,
                out Transform targetParent))
                continue;

            Rigidbody body =
                joint.GetComponent<Rigidbody>();

            if (body == null)
                continue;

            muscles.Add(new Muscle
            {
                body = body,
                parentBody = joint.connectedBody,
                target = target,
                targetParent = targetParent
            });
        }

        Debug.Log(
            "Stable Ragdoll: Connected " +
            muscles.Count + " joint muscles."
        );
    }

    private void FixedUpdate()
    {
        foreach (Muscle muscle in muscles)
        {
            if (muscle.body == null ||
                muscle.parentBody == null ||
                muscle.target == null ||
                muscle.targetParent == null)
                continue;

            ApplyMuscle(muscle);
        }
    }

    private void ApplyMuscle(Muscle muscle)
    {
        Rigidbody child = muscle.body;
        Rigidbody parent = muscle.parentBody;

        if (child.isKinematic)
            return;

        // Target rotation relative to parent bone.
        Quaternion targetRelative =
            Quaternion.Inverse(
                muscle.targetParent.rotation
            ) * muscle.target.rotation;

        // Current rotation relative to parent Rigidbody.
        Quaternion currentRelative =
            Quaternion.Inverse(parent.rotation) *
            child.rotation;

        // Rotation needed to reach the target.
        Quaternion error =
            targetRelative *
            Quaternion.Inverse(currentRelative);

        if (error.w < 0f)
        {
            error = new Quaternion(
                -error.x,
                -error.y,
                -error.z,
                -error.w
            );
        }

        error.ToAngleAxis(
            out float angle,
            out Vector3 axis
        );

        if (angle > 180f)
            angle -= 360f;

        if (float.IsNaN(angle) ||
            axis.sqrMagnitude < 0.000001f)
            return;

        // Convert rotation error to world space.
        Vector3 worldAxis =
            parent.rotation * axis.normalized;

        Vector3 rotationError =
            worldAxis * angle * Mathf.Deg2Rad;

        // Angular velocity relative to parent.
        Vector3 relativeVelocity =
            child.angularVelocity -
            parent.angularVelocity;

        // Spring-damper torque.
        Vector3 torque =
            rotationError * muscleStrength -
            relativeVelocity * muscleDamping;

        // Stop tiny corrective movements.
        if (Mathf.Abs(angle) < deadZoneDegrees &&
            relativeVelocity.magnitude < 0.05f)
        {
            return;
        }

        // Prevent excessive torque.
        torque = Vector3.ClampMagnitude(
            torque,
            Mathf.Max(0f, maxMuscleTorque)
        );

        // Apply torque to the bone.
        child.AddTorque(
            torque,
            ForceMode.Force
        );

        // Equal and opposite torque on its parent.
        if (!parent.isKinematic)
        {
            parent.AddTorque(
                -torque,
                ForceMode.Force
            );
        }
    }
}
