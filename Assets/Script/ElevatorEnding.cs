using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Random = UnityEngine.Random;

public class ElevatorEnding : MonoBehaviour
{
    [Header("Ending Camera")]
    [SerializeField] private Camera endingCamera;

    [Header("Ending Path")]
    [SerializeField] private Transform doorPoint;
    [SerializeField] private Transform outsidePoint;

    [Header("Ending Timing")]
    [Min(0f)]
    [SerializeField] private float doorWait = 0.5f;

    [Min(1f)]
    [SerializeField] private float finishDelay = 6f;

    [Header("Ending Sound")]
    [SerializeField] private AudioSource endingAudioSource;
    [SerializeField] private AudioClip endingSound;

    [Tooltip("Delay after NPCs are launched before the ending sound plays.")]
    [Min(0f)]
    [SerializeField] private float endingSoundDelay = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float endingSoundVolume = 1f;

    [Header("EXPLOSIVE FORCE")]
    [SerializeField] private float minLaunchForce = 10f;
    [SerializeField] private float maxLaunchForce = 17f;

    [SerializeField] private float sideForce = 4.5f;

    [SerializeField] private float minUpForce = 2f;
    [SerializeField] private float maxUpForce = 5f;

    [SerializeField] private float spinForce = 10f;

    [Header("Ragdoll Physics")]
    [SerializeField] private float colliderSize = 1f;

    [SerializeField] private float linearDamping = 0.05f;
    [SerializeField] private float angularDamping = 0.15f;

    [SerializeField] private float maxDepenetrationVelocity = 10f;

    [SerializeField] private string physicsLayer = "Default";

    private GameObject physicsRoot;
    private int ragdollLayer;
    private bool playing;

    private class RagdollPart
    {
        public Transform bone;
        public Rigidbody body;
        public Collider collider;
    }

    private class RagdollNPC
    {
        public NPCQueueAgent agent;

        public Rigidbody pelvis;

        public Vector3 pelvisOffset;

        public bool fallback;

        public readonly List<RagdollPart> parts =
            new List<RagdollPart>();
    }

    private readonly List<RagdollNPC> ragdolls =
        new List<RagdollNPC>();

    public float DoorWait => Mathf.Max(0f, doorWait);

    public bool IsReady
    {
        get
        {
            if (endingCamera == null ||
                doorPoint == null ||
                outsidePoint == null)
            {
                return false;
            }

            Vector3 direction =
                outsidePoint.position - doorPoint.position;

            direction.y = 0f;

            return direction.sqrMagnitude > 0.25f;
        }
    }

    // =====================================
    // ENDING CAMERA
    // =====================================

    public bool ShowEndingCamera(CameraSwitcher switcher)
    {
        if (!IsReady || switcher == null)
            return false;

        switcher.enabled = false;

        if (switcher.cameraLobby != null)
            switcher.cameraLobby.SetActive(false);

        if (switcher.cameraElevator != null)
            switcher.cameraElevator.SetActive(false);

        endingCamera.gameObject.SetActive(true);
        endingCamera.enabled = true;

        return true;
    }

    // =====================================
    // PLAY ENDING
    // =====================================

    public IEnumerator Play(List<NPCQueueAgent> passengers)
    {
        if (!IsReady || playing)
            yield break;

        playing = true;

        DisableGameplaySystems();

        physicsRoot = new GameObject(
            "Ending Full Ragdoll Physics"
        );

        ragdollLayer =
            LayerMask.NameToLayer(physicsLayer);

        if (ragdollLayer < 0)
            ragdollLayer = 0;

        Vector3 forward =
            outsidePoint.position - doorPoint.position;

        forward.y = 0f;
        forward.Normalize();

        Vector3 right =
            Vector3.Cross(Vector3.up, forward).normalized;

        ragdolls.Clear();

        // Create real physical bodies for every NPC.

        foreach (NPCQueueAgent agent in passengers)
        {
            if (agent == null)
                continue;

            RagdollNPC ragdoll = CreateRagdoll(agent);

            if (ragdoll != null)
                ragdolls.Add(ragdoll);
        }

        Physics.SyncTransforms();

        // Release every ragdoll into physics.

        foreach (RagdollNPC ragdoll in ragdolls)
        {
            LaunchRagdoll(
                ragdoll,
                forward,
                right
            );
        }

        Debug.Log(
            "[ENDING] Launched " +
            ragdolls.Count +
            " full physics passengers."
        );

        // Play the sound after the ragdolls launch.
        // The delay is adjustable in the Inspector.
        float totalFinishDelay = Mathf.Max(1f, finishDelay);

        if (endingAudioSource != null && endingSound != null)
        {
            float soundDelay = Mathf.Clamp(
                endingSoundDelay,
                0f,
                totalFinishDelay
            );

            if (soundDelay > 0f)
                yield return new WaitForSeconds(soundDelay);

            endingAudioSource.PlayOneShot(
                endingSound,
                endingSoundVolume
            );

            // Keep the original total finish timing unchanged.
            float remainingDelay = totalFinishDelay - soundDelay;

            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);
        }
        else
        {
            yield return new WaitForSeconds(totalFinishDelay);
        }

        // No scripted destination.
        // No scripted landing.
        // Let physics do everything.

        SyncRagdollVisuals();

        Physics.SyncTransforms();
    }

    // =====================================
    // DISABLE NORMAL GAMEPLAY MOVEMENT
    // =====================================

    private void DisableGameplaySystems()
    {
        foreach (NPCBodyCollisionManager manager in
                 FindObjectsByType<NPCBodyCollisionManager>(
                     FindObjectsSortMode.None))
        {
            manager.enabled = false;
        }

        foreach (NPCWalkAwayManager manager in
                 FindObjectsByType<NPCWalkAwayManager>(
                     FindObjectsSortMode.None))
        {
            manager.enabled = false;
        }

        foreach (NPCSquishManager manager in
                 FindObjectsByType<NPCSquishManager>(
                     FindObjectsSortMode.None))
        {
            manager.enabled = false;
        }
    }

    // =====================================
    // CREATE FULL RAGDOLL
    // =====================================

    private RagdollNPC CreateRagdoll(
        NPCQueueAgent agent)
    {
        Bounds originalBounds = GetOriginalBounds(agent);

        float height = Mathf.Max(
            0.5f,
            originalBounds.size.y
        );

        agent.LeaveQueue();
        agent.SetGrabbed(false);

        Animator animator =
            agent.GetComponentInChildren<Animator>(true);

        // Stop normal character scripts.
        // Only affects this NPC during the ending.

        foreach (MonoBehaviour behaviour in
                 agent.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        // Stop animation from overriding physics.

        foreach (Animator a in
                 agent.GetComponentsInChildren<Animator>(true))
        {
            a.applyRootMotion = false;
            a.enabled = false;
        }

        // Disable the original gameplay colliders.

        foreach (Collider c in
                 agent.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
        }

        // Disable any old physical bodies.

        foreach (Rigidbody rb in
                 agent.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (SkinnedMeshRenderer skin in
                 agent.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.updateWhenOffscreen = true;
        }

        Transform root = agent.transform;

        // Find the actual skeleton.

        Transform hips = GetBone(
            animator,
            root,
            HumanBodyBones.Hips,
            "Hips", "Pelvis", "Hip"
        );

        Transform spine = GetBone(
            animator,
            root,
            HumanBodyBones.Spine,
            "Spine"
        );

        Transform chest = GetBone(
            animator,
            root,
            HumanBodyBones.Chest,
            "Chest", "Spine2", "Spine1"
        );

        Transform head = GetBone(
            animator,
            root,
            HumanBodyBones.Head,
            "Head"
        );

        if (chest == null)
            chest = spine;

        RagdollNPC ragdoll = new RagdollNPC
        {
            agent = agent
        };

        // Fallback for models without usable bones.
        // Still uses real physics instead of animation.

        if (hips == null ||
            chest == null ||
            head == null ||
            hips == root ||
            hips == chest ||
            hips == head ||
            chest == head)
        {
            Debug.LogWarning(
                "[ENDING] Missing ragdoll bones: " +
                agent.name +
                ". Using whole-body physics."
            );

            return CreateFallback(
                ragdoll,
                originalBounds
            );
        }

        ragdoll.pelvisOffset =
            hips.position - root.position;

        // =================================
        // PELVIS
        // =================================

        RagdollPart pelvisPart = AddPart(
            ragdoll,
            hips,
            chest,
            null,
            height * 0.12f,
            5f
        );

        ragdoll.pelvis = pelvisPart.body;

        // =================================
        // CHEST
        // =================================

        RagdollPart chestPart = AddPart(
            ragdoll,
            chest,
            head,
            pelvisPart,
            height * 0.14f,
            4f
        );

        // =================================
        // HEAD
        // =================================

        AddPart(
            ragdoll,
            head,
            null,
            chestPart,
            height * 0.14f,
            2f
        );

        // =================================
        // LEFT ARM
        // =================================

        AddArm(
            ragdoll,
            animator,
            root,
            chestPart,
            true,
            height
        );

        // =================================
        // RIGHT ARM
        // =================================

        AddArm(
            ragdoll,
            animator,
            root,
            chestPart,
            false,
            height
        );

        // =================================
        // LEFT LEG
        // =================================

        AddLeg(
            ragdoll,
            animator,
            root,
            pelvisPart,
            true,
            height
        );

        // =================================
        // RIGHT LEG
        // =================================

        AddLeg(
            ragdoll,
            animator,
            root,
            pelvisPart,
            false,
            height
        );

        // Prevent a ragdoll from exploding because
        // its own colliders overlap.
        //
        // Different NPCs can still hit each other.

        IgnoreSelfCollisions(ragdoll);

        // Sync parent bones before child bones.

        ragdoll.parts.Sort(
            (a, b) =>
                GetBoneDepth(a.bone).CompareTo(
                    GetBoneDepth(b.bone)
                )
        );

        if (ragdoll.parts.Count < 11)
        {
            Debug.LogWarning(
                "[ENDING] Partial skeleton: " +
                agent.name +
                " | Parts: " +
                ragdoll.parts.Count
            );
        }

        return ragdoll;
    }

    // =====================================
    // CREATE ARM PHYSICS
    // =====================================

    private void AddArm(
        RagdollNPC ragdoll,
        Animator animator,
        Transform root,
        RagdollPart parent,
        bool left,
        float height)
    {
        string side = left ? "Left" : "Right";

        Transform upperArm = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftUpperArm
                : HumanBodyBones.RightUpperArm,
            side + "UpperArm",
            side + "Arm"
        );

        Transform forearm = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftLowerArm
                : HumanBodyBones.RightLowerArm,
            side + "Forearm",
            side + "LowerArm"
        );

        Transform hand = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand,
            side + "Hand"
        );

        if (upperArm == null || forearm == null)
            return;

        RagdollPart upperPart = AddPart(
            ragdoll,
            upperArm,
            forearm,
            parent,
            height * 0.045f,
            1.2f
        );

        RagdollPart lowerPart = AddPart(
            ragdoll,
            forearm,
            hand,
            upperPart,
            height * 0.04f,
            0.8f
        );

        if (hand != null)
        {
            AddPart(
                ragdoll,
                hand,
                null,
                lowerPart,
                height * 0.035f,
                0.4f
            );
        }
    }

    // =====================================
    // CREATE LEG PHYSICS
    // =====================================

    private void AddLeg(
        RagdollNPC ragdoll,
        Animator animator,
        Transform root,
        RagdollPart parent,
        bool left,
        float height)
    {
        string side = left ? "Left" : "Right";

        Transform thigh = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftUpperLeg
                : HumanBodyBones.RightUpperLeg,
            side + "Thigh",
            side + "UpperLeg",
            side + "UpLeg"
        );

        Transform shin = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftLowerLeg
                : HumanBodyBones.RightLowerLeg,
            side + "Shin",
            side + "LowerLeg",
            side + "Leg",
            side + "Calf"
        );

        Transform foot = GetBone(
            animator,
            root,
            left
                ? HumanBodyBones.LeftFoot
                : HumanBodyBones.RightFoot,
            side + "Foot"
        );

        if (thigh == null || shin == null)
            return;

        RagdollPart thighPart = AddPart(
            ragdoll,
            thigh,
            shin,
            parent,
            height * 0.065f,
            2.2f
        );

        RagdollPart shinPart = AddPart(
            ragdoll,
            shin,
            foot,
            thighPart,
            height * 0.05f,
            1.5f
        );

        if (foot != null)
        {
            AddPart(
                ragdoll,
                foot,
                null,
                shinPart,
                height * 0.045f,
                0.6f
            );
        }
    }

    // =====================================
    // CREATE RIGIDBODY AND COLLIDER
    // =====================================

    private RagdollPart AddPart(
        RagdollNPC ragdoll,
        Transform bone,
        Transform nextBone,
        RagdollPart parent,
        float radius,
        float mass)
    {
        GameObject obj = new GameObject(
            ragdoll.agent.name + "_" + bone.name
        );

        obj.layer = ragdollLayer;

        obj.transform.SetParent(
            physicsRoot.transform,
            false
        );

        obj.transform.SetPositionAndRotation(
            bone.position,
            bone.rotation
        );

        Rigidbody body = obj.AddComponent<Rigidbody>();

        ConfigureBody(body, mass);

        radius = Mathf.Max(
            0.025f,
            radius * Mathf.Max(0.1f, colliderSize)
        );

        Collider collider;

        if (nextBone != null &&
            Vector3.Distance(
                bone.position,
                nextBone.position
            ) > 0.01f)
        {
            Vector3 difference =
                nextBone.position - bone.position;

            GameObject shape = new GameObject("Collider");

            shape.layer = ragdollLayer;

            shape.transform.SetParent(
                obj.transform,
                false
            );

            shape.transform.localPosition =
                obj.transform.InverseTransformVector(
                    difference * 0.5f
                );

            shape.transform.localRotation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    obj.transform.InverseTransformDirection(
                        difference.normalized
                    )
                );

            CapsuleCollider capsule =
                shape.AddComponent<CapsuleCollider>();

            capsule.direction = 1;
            capsule.radius = radius;

            capsule.height = Mathf.Max(
                radius * 2f,
                difference.magnitude + radius
            );

            collider = capsule;
        }
        else
        {
            SphereCollider sphere =
                obj.AddComponent<SphereCollider>();

            sphere.radius = radius;

            collider = sphere;
        }

        RagdollPart part = new RagdollPart
        {
            bone = bone,
            body = body,
            collider = collider
        };

        ragdoll.parts.Add(part);

        if (parent != null)
        {
            CreateJoint(
                body,
                parent.body
            );
        }

        return part;
    }

    // =====================================
    // RIGIDBODY SETTINGS
    // =====================================

    private void ConfigureBody(
        Rigidbody body,
        float mass)
    {
        body.mass = mass;

        body.isKinematic = true;
        body.useGravity = true;

        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        body.maxAngularVelocity = 25f;

        body.maxDepenetrationVelocity =
            maxDepenetrationVelocity;

        body.solverIterations = 8;
        body.solverVelocityIterations = 4;

        body.constraints = RigidbodyConstraints.None;
    }

    // =====================================
    // REAL PHYSICAL JOINT
    // =====================================

    private void CreateJoint(
        Rigidbody body,
        Rigidbody parent)
    {
        CharacterJoint joint =
            body.gameObject.AddComponent<CharacterJoint>();

        joint.connectedBody = parent;

        joint.anchor = Vector3.zero;
        joint.autoConfigureConnectedAnchor = true;

        joint.lowTwistLimit = new SoftJointLimit
        {
            limit = -60f
        };

        joint.highTwistLimit = new SoftJointLimit
        {
            limit = 60f
        };

        joint.swing1Limit = new SoftJointLimit
        {
            limit = 70f
        };

        joint.swing2Limit = new SoftJointLimit
        {
            limit = 70f
        };

        joint.enableCollision = false;

        joint.enableProjection = true;
        joint.projectionDistance = 0.12f;
        joint.projectionAngle = 15f;

        joint.enablePreprocessing = false;

        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;
    }

    // =====================================
    // EXPLOSIVE RANDOM FORCE
    // =====================================

    private void LaunchRagdoll(
        RagdollNPC ragdoll,
        Vector3 forward,
        Vector3 right)
    {
        float launchForce = Random.Range(
            Mathf.Min(minLaunchForce, maxLaunchForce),
            Mathf.Max(minLaunchForce, maxLaunchForce)
        );

        float sideways = Random.Range(
            -sideForce,
            sideForce
        );

        float upward = Random.Range(
            Mathf.Min(minUpForce, maxUpForce),
            Mathf.Max(minUpForce, maxUpForce)
        );

        Vector3 velocity =
            forward * launchForce +
            right * sideways +
            Vector3.up * upward;

        // Activate every physical body first.

        foreach (RagdollPart part in ragdoll.parts)
        {
            part.body.isKinematic = false;
        }

        // Apply real physical impulses.
        // The physics engine controls movement
        // from this point onward.

        foreach (RagdollPart part in ragdoll.parts)
        {
            part.body.AddForce(
                velocity,
                ForceMode.VelocityChange
            );

            part.body.WakeUp();
        }

        // Random violent spin.

        if (ragdoll.pelvis != null)
        {
            ragdoll.pelvis.AddTorque(
                Random.onUnitSphere *
                Random.Range(
                    spinForce * 0.5f,
                    spinForce
                ),
                ForceMode.VelocityChange
            );
        }

        // Add random limb reactions.

        foreach (RagdollPart part in ragdoll.parts)
        {
            if (part.body == ragdoll.pelvis)
                continue;

            part.body.AddTorque(
                Random.onUnitSphere *
                Random.Range(1f, spinForce * 0.6f),
                ForceMode.VelocityChange
            );
        }
    }

    // =====================================
    // IGNORE COLLISIONS WITH OWN BODY
    // =====================================

    private void IgnoreSelfCollisions(
        RagdollNPC ragdoll)
    {
        for (int i = 0; i < ragdoll.parts.Count; i++)
        {
            for (int j = i + 1;
                 j < ragdoll.parts.Count;
                 j++)
            {
                Collider a =
                    ragdoll.parts[i].collider;

                Collider b =
                    ragdoll.parts[j].collider;

                if (a != null && b != null)
                {
                    Physics.IgnoreCollision(
                        a,
                        b,
                        true
                    );
                }
            }
        }
    }

    // =====================================
    // SYNC MODEL WITH REAL PHYSICS
    // =====================================

    private void LateUpdate()
    {
        if (!playing)
            return;

        SyncRagdollVisuals();
    }

    private void SyncRagdollVisuals()
    {
        foreach (RagdollNPC ragdoll in ragdolls)
        {
            if (ragdoll.agent == null ||
                ragdoll.pelvis == null)
            {
                continue;
            }

            Transform root = ragdoll.agent.transform;

            if (ragdoll.fallback)
            {
                root.SetPositionAndRotation(
                    ragdoll.pelvis.transform.position,
                    ragdoll.pelvis.transform.rotation
                );

                continue;
            }

            // Keep the NPC root near the physical pelvis
            // for the existing scoring system.

            root.position =
                ragdoll.pelvis.transform.position -
                ragdoll.pelvisOffset;

            // Copy the actual physics pose.
            // There is no standing or landing animation.

            foreach (RagdollPart part in ragdoll.parts)
            {
                if (part.bone == null ||
                    part.body == null)
                {
                    continue;
                }

                part.bone.SetPositionAndRotation(
                    part.body.transform.position,
                    part.body.transform.rotation
                );
            }
        }
    }

    // =====================================
    // FIND CHARACTER BONES
    // =====================================

    private Transform GetBone(
        Animator animator,
        Transform root,
        HumanBodyBones humanoidBone,
        params string[] names)
    {
        if (animator != null &&
            animator.avatar != null &&
            animator.avatar.isValid &&
            animator.isHuman)
        {
            Transform bone =
                animator.GetBoneTransform(humanoidBone);

            if (bone != null)
                return bone;
        }

        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        foreach (Transform bone in transforms)
        {
            foreach (string boneName in names)
            {
                if (string.Equals(
                    bone.name,
                    boneName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return bone;
                }

                if (bone.name.EndsWith(
                    ":" + boneName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return bone;
                }
            }
        }

        return null;
    }

    private int GetBoneDepth(Transform bone)
    {
        int depth = 0;

        while (bone != null)
        {
            depth++;
            bone = bone.parent;
        }

        return depth;
    }

    // =====================================
    // ORIGINAL NPC BOUNDS
    // =====================================

    private Bounds GetOriginalBounds(
        NPCQueueAgent agent)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule != null && capsule.enabled)
            return capsule.bounds;

        return new Bounds(
            agent.transform.position +
            Vector3.up * 0.9f,
            new Vector3(0.7f, 1.8f, 0.7f)
        );
    }

    // =====================================
    // FALLBACK: WHOLE BODY PHYSICS
    // =====================================

    private RagdollNPC CreateFallback(
        RagdollNPC ragdoll,
        Bounds originalBounds)
    {
        GameObject obj = new GameObject(
            ragdoll.agent.name + "_PhysicsBody"
        );

        obj.layer = ragdollLayer;

        obj.transform.SetParent(
            physicsRoot.transform,
            false
        );

        obj.transform.SetPositionAndRotation(
            ragdoll.agent.transform.position,
            ragdoll.agent.transform.rotation
        );

        Rigidbody body =
            obj.AddComponent<Rigidbody>();

        ConfigureBody(body, 12f);

        CapsuleCollider capsule =
            obj.AddComponent<CapsuleCollider>();

        capsule.center =
            obj.transform.InverseTransformPoint(
                originalBounds.center
            );

        capsule.radius = Mathf.Max(
            0.1f,
            Mathf.Max(
                originalBounds.extents.x,
                originalBounds.extents.z
            )
        );

        capsule.height = Mathf.Max(
            originalBounds.size.y,
            capsule.radius * 2f
        );

        ragdoll.parts.Add(
            new RagdollPart
            {
                bone = ragdoll.agent.transform,
                body = body,
                collider = capsule
            }
        );

        ragdoll.pelvis = body;
        ragdoll.fallback = true;

        return ragdoll;
    }

    // =====================================
    // CLEANUP WHEN SCENE IS DESTROYED
    // =====================================

    private void OnDestroy()
    {
        if (physicsRoot != null)
            Destroy(physicsRoot);
    }
}