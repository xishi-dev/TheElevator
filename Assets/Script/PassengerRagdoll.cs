
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Briefly lets the forearms and shins swing on physics joints
/// while the body stays controllable.
/// </summary>
[DefaultExecutionOrder(9500)]
[DisallowMultipleComponent]
public sealed class PassengerRagdoll : MonoBehaviour
{
    private sealed class Limb
    {
        public Transform bone;
        public Rigidbody anchor;
        public Rigidbody body;
        public Quaternion restLocal;
        public Quaternion appliedOffset = Quaternion.identity;
        public Quaternion lastOutput;
    }

    private static readonly string[] BoneNames =
    {
        "LeftForearm",
        "RightForearm",
        "LeftShin",
        "RightShin"
    };

    private static int activeCount;
    private const int ActiveLimit = 6;

    private readonly List<Limb> limbs = new List<Limb>();

    private GameObject physicsRoot;
    private float endTime, nextHit;
    private bool active;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveCount()
    {
        activeCount = 0;
    }

    public void Jolt(Vector3 direction, float strength)
    {
        if (!isActiveAndEnabled ||
            Time.time < nextHit ||
            (!active && activeCount >= ActiveLimit))
            return;

        if (physicsRoot == null)
            CreateLimbBodies();

        if (limbs.Count == 0)
            return;

        nextHit = Time.time + .3f;
        endTime = Time.time + .65f;

        if (!active)
        {
            active = true;
            activeCount++;

            physicsRoot.SetActive(true);

            foreach (var limb in limbs)
            {
                limb.restLocal = limb.bone.localRotation;
                limb.appliedOffset = Quaternion.identity;

                limb.anchor.position = limb.bone.position;
                limb.anchor.rotation = limb.bone.rotation;

                limb.body.position = limb.anchor.position;
                limb.body.rotation = limb.anchor.rotation;

                limb.body.linearVelocity = Vector3.zero;
                limb.body.angularVelocity = Vector3.zero;
            }
        }

        for (int i = 0; i < limbs.Count; i++)
        {
            Vector3 swing =
                Vector3.Cross(transform.up, direction) +
                transform.right * (i % 2 == 0 ? .5f : -.5f);

            limbs[i].body.AddTorque(
                swing * Mathf.Clamp01(strength) * .22f,
                ForceMode.Impulse
            );
        }
    }

    private void CreateLimbBodies()
    {
        var skin = GetComponentInChildren<SkinnedMeshRenderer>();

        if (skin == null)
            return;

        physicsRoot = new GameObject("Passenger limb physics");
        physicsRoot.hideFlags = HideFlags.DontSave;
        physicsRoot.SetActive(false);

        foreach (Transform bone in skin.bones)
        {
            if (bone == null ||
                System.Array.IndexOf(BoneNames, bone.name) < 0 ||
                bone.childCount == 0)
                continue;

            var anchor = AddBody(bone.name + " pivot", bone);

            anchor.isKinematic = true;
            anchor.useGravity = false;

            var body = AddBody(bone.name + " limb", bone);

            body.mass = .35f;
            body.inertiaTensor = Vector3.one * .08f;
            body.angularDamping = 4f;
            body.maxAngularVelocity = 12f;

            body.centerOfMass =
                bone.InverseTransformPoint(
                    bone.GetChild(0).position
                ) * .5f;

            var joint =
                body.gameObject.AddComponent<ConfigurableJoint>();

            joint.connectedBody = anchor;
            joint.autoConfigureConnectedAnchor = false;

            joint.xMotion =
                joint.yMotion =
                joint.zMotion =
                ConfigurableJointMotion.Locked;

            joint.angularXMotion =
                joint.angularYMotion =
                joint.angularZMotion =
                ConfigurableJointMotion.Limited;

            joint.lowAngularXLimit =
                new SoftJointLimit { limit = -48 };

            joint.highAngularXLimit =
                new SoftJointLimit { limit = 48 };

            joint.angularYLimit =
                new SoftJointLimit { limit = 26 };

            joint.angularZLimit =
                new SoftJointLimit { limit = 34 };

            joint.rotationDriveMode = RotationDriveMode.Slerp;

            joint.slerpDrive = new JointDrive
            {
                positionSpring = 6,
                positionDamper = 1.5f,
                maximumForce = 20
            };

            limbs.Add(new Limb
            {
                bone = bone,
                anchor = anchor,
                body = body
            });
        }
    }

    private Rigidbody AddBody(string bodyName, Transform bone)
    {
        var node = new GameObject(bodyName);

        node.transform.SetParent(physicsRoot.transform, false);
        node.transform.SetPositionAndRotation(
            bone.position,
            bone.rotation
        );

        return node.AddComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!active)
            return;

        foreach (var limb in limbs)
        {
            limb.anchor.MovePosition(limb.bone.position);

            limb.anchor.MoveRotation(
                limb.bone.parent.rotation * limb.restLocal
            );
        }
    }

    private void LateUpdate()
    {
        if (!active)
            return;

        float remaining = endTime - Time.time;

        if (remaining <= 0)
        {
            Stop();
            return;
        }

        float weight = Mathf.Clamp01(remaining / .22f);

        foreach (var limb in limbs)
        {
            Quaternion delta =
                Quaternion.Inverse(limb.anchor.rotation) *
                limb.body.rotation;

            var animationPose = limb.bone.localRotation;

            if (Quaternion.Angle(
                animationPose,
                limb.lastOutput) < .05f)
            {
                animationPose *=
                    Quaternion.Inverse(limb.appliedOffset);
            }

            limb.appliedOffset = Quaternion.Slerp(
                Quaternion.identity,
                delta,
                weight
            );

            limb.lastOutput =
                animationPose * limb.appliedOffset;

            limb.bone.localRotation = limb.lastOutput;
        }
    }

    public void Stop()
    {
        if (!active)
            return;

        active = false;
        activeCount = Mathf.Max(0, activeCount - 1);

        foreach (var limb in limbs)
        {
            if (limb.bone != null &&
                Quaternion.Angle(
                    limb.bone.localRotation,
                    limb.lastOutput) < .05f)
            {
                limb.bone.localRotation *=
                    Quaternion.Inverse(limb.appliedOffset);
            }
        }

        if (physicsRoot != null)
            physicsRoot.SetActive(false);
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        if (physicsRoot != null)
            Destroy(physicsRoot);
    }
}
