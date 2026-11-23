
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(11000)]
public class NPCSquishManager : MonoBehaviour
{
    [Header("Crowd Detection")]
    [SerializeField] private float contactRange = 0.2f;
    [SerializeField] private float minVerticalOverlap = 0.3f;
    [SerializeField] private float scanInterval = 0.4f;

    [Header("Funny Body Movement")]
    [SerializeField] private float maxSideLean = 8f;
    [SerializeField] private float maxFrontLean = 6f;
    [SerializeField] private float maxArmFold = 12f;
    [SerializeField] private float headCounterLean = 3f;

    [Header("Smooth Movement")]
    [SerializeField] private float smoothTime = 0.16f;

    [Header("Funny Wobble")]
    [SerializeField] private float wobbleDegrees = 1.2f;
    [SerializeField] private float wobbleSpeed = 7f;

    private class Body
    {
        public NPCQueueAgent agent;
        public CapsuleCollider capsule;
        public SkinnedMeshRenderer renderer;

        public Transform spine;
        public Transform chest;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;

        public float squeeze;
        public float side;
        public float front;

        public float squeezeVelocity;
        public float sideVelocity;
        public float frontVelocity;

        public float targetSqueeze;
        public float targetSide;
        public float targetFront;
    }

    private readonly List<Body> bodies =
        new List<Body>();

    private float nextScanTime;

    private void OnEnable()
    {
        nextScanTime = 0f;
    }

    private void LateUpdate()
    {
        if (Time.time >= nextScanTime)
        {
            ScanNPCs();

            nextScanTime = Time.time +
                Mathf.Max(0.1f, scanInterval);
        }
        foreach (Body body in bodies)
        {
            body.targetSqueeze = 0f;
            body.targetSide = 0f;
            body.targetFront = 0f;
        }
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1;
                 j < bodies.Count;
                 j++)
            {
                CheckCrowd(
                    bodies[i],
                    bodies[j]
                );
            }
        }

        foreach (Body body in bodies)
        {
            UpdateReaction(body);
        }
    }
    private void ScanNPCs()
    {
        bodies.RemoveAll(
            body => body.agent == null
        );

        NPCQueueAgent[] agents =
            FindObjectsByType<NPCQueueAgent>(
                FindObjectsSortMode.None
            );

        foreach (NPCQueueAgent agent in agents)
        {
            if (agent == null)
                continue;

            bool alreadyExists = false;

            foreach (Body existing in bodies)
            {
                if (existing.agent == agent)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (alreadyExists)
                continue;

            Body body = CreateBody(agent);

            if (body != null)
                bodies.Add(body);
        }
    }

    private Body CreateBody(NPCQueueAgent agent)
    {
        CapsuleCollider capsule =
            agent.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule =
                agent.GetComponentInChildren<CapsuleCollider>();
        }

        if (capsule == null)
            return null;

        Transform root = agent.transform;

        Body body = new Body();

        body.agent = agent;
        body.capsule = capsule;

        body.renderer =
            root.GetComponentInChildren<SkinnedMeshRenderer>();

        body.spine = FindBone(root, "Spine");
        body.chest = FindBone(root, "Chest");
        body.head = FindBone(root, "Head");

        body.leftArm =
            FindBone(root, "LeftUpperArm");

        body.rightArm =
            FindBone(root, "RightUpperArm");

        if (body.spine == null ||
            body.chest == null ||
            body.head == null ||
            body.leftArm == null ||
            body.rightArm == null)
        {
            Debug.LogWarning(
                "NPC missing required bones: " +
                agent.name
            );

            return null;
        }

        return body;
    }

    private Transform FindBone(
        Transform root,
        string boneName)
    {
        Transform[] children =
            root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == boneName)
                return child;
        }

        return null;
    }
    private void CheckCrowd(Body a, Body b)
    {
        if (!IsValid(a) || !IsValid(b))
            return;

        // ไม่ทำท่าเบียดระหว่างรอต่อแถว
        if (a.agent.IsInQueue ||
            b.agent.IsInQueue)
            return;

        Bounds boundsA = a.capsule.bounds;
        Bounds boundsB = b.capsule.bounds;

        float overlapY = Mathf.Min(
            boundsA.max.y,
            boundsB.max.y
        ) - Mathf.Max(
            boundsA.min.y,
            boundsB.min.y
        );

        if (overlapY < minVerticalOverlap)
            return;

        Vector3 centerA = GetCenter(a.capsule);
        Vector3 centerB = GetCenter(b.capsule);

        Vector3 direction = centerB - centerA;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance < 0.0001f)
        {
            direction = Vector3.right;
            distance = 0f;
        }
        else
        {
            direction /= distance;
        }

        float radiusA = GetRadius(a.capsule);
        float radiusB = GetRadius(b.capsule);

        float gap =
            distance - (radiusA + radiusB);

        if (gap >= contactRange)
            return;

        float pressure = Mathf.Clamp01(
            (contactRange - gap) /
            Mathf.Max(0.01f, contactRange)
        );

        AddPressure(a, direction, pressure);
        AddPressure(b, -direction, pressure);
    }

    private void AddPressure(
        Body body,
        Vector3 direction,
        float pressure)
    {
        body.targetSqueeze = Mathf.Clamp01(
            body.targetSqueeze + pressure
        );

        float side =
            Vector3.Dot(
                direction,
                body.agent.transform.right
            );

        float front =
            Vector3.Dot(
                direction,
                body.agent.transform.forward
            );

        body.targetSide += side * pressure;
        body.targetFront += front * pressure;
    }
    private void UpdateReaction(Body body)
    {
        if (!IsValid(body))
            return;

        float response = Mathf.Max(
            0.02f,
            smoothTime
        );

        body.squeeze = Mathf.SmoothDamp(
            body.squeeze,
            body.targetSqueeze,
            ref body.squeezeVelocity,
            response
        );

        body.side = Mathf.SmoothDamp(
            body.side,
            Mathf.Clamp(body.targetSide, -1f, 1f),
            ref body.sideVelocity,
            response
        );

        body.front = Mathf.SmoothDamp(
            body.front,
            Mathf.Clamp(body.targetFront, -1f, 1f),
            ref body.frontVelocity,
            response
        );
        if (body.renderer != null &&
            !body.renderer.isVisible)
            return;

        float sideLean =
            body.side * maxSideLean;

        float frontLean =
            -body.front * maxFrontLean;

        float wobble =
            Mathf.Sin(
                Time.time * wobbleSpeed +
                body.agent.GetInstanceID() * 0.13f
            ) *
            wobbleDegrees *
            body.squeeze;

        sideLean += wobble;

        float armFold =
            body.squeeze * maxArmFold;

        RotateBone(
            body.spine,
            frontLean * 0.45f,
            sideLean * 0.45f
        );

        RotateBone(
            body.chest,
            frontLean * 0.55f,
            sideLean * 0.55f
        );

        RotateBone(
            body.head,
            -frontLean * 0.2f,
            -sideLean * headCounterLean /
                Mathf.Max(1f, maxSideLean)
        );

        RotateBone(
            body.leftArm,
            0f,
            -armFold
        );

        RotateBone(
            body.rightArm,
            0f,
            armFold
        );
    }

    private void RotateBone(
        Transform bone,
        float rotationX,
        float rotationZ)
    {
        if (bone == null)
            return;
        bone.localRotation =
            bone.localRotation *
            Quaternion.Euler(
                rotationX,
                0f,
                rotationZ
            );
    }
    private Vector3 GetCenter(
        CapsuleCollider capsule)
    {
        return capsule.transform.TransformPoint(
            capsule.center
        );
    }

    private float GetRadius(
        CapsuleCollider capsule)
    {
        Vector3 s =
            capsule.transform.lossyScale;

        float radiusScale;

        if (capsule.direction == 0)
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(s.y),
                Mathf.Abs(s.z)
            );
        }
        else if (capsule.direction == 2)
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(s.x),
                Mathf.Abs(s.y)
            );
        }
        else
        {
            radiusScale = Mathf.Max(
                Mathf.Abs(s.x),
                Mathf.Abs(s.z)
            );
        }

        return capsule.radius * radiusScale;
    }

    private bool IsValid(Body body)
    {
        return body != null &&
               body.agent != null &&
               body.agent.isActiveAndEnabled &&
               body.capsule != null &&
               body.capsule.enabled;
    }
}
