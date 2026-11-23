
using UnityEngine;

public class RagdollBalanceController : MonoBehaviour
{
    [Header("Physics References")]
    [SerializeField] private Rigidbody hips;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private Transform poseReference;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayers = 1;
    [SerializeField] private float probeHeight = 0.2f;
    [SerializeField] private float probeDistance = 0.35f;
    [SerializeField] private float maxRecoveryTilt = 55f;

    [Header("Upright Balance")]
    [SerializeField] private float uprightStrength = 8f;
    [SerializeField] private float uprightDamping = 3f;
    [SerializeField] private float maxBalanceTorque = 8f;

    [Header("Center Of Balance")]
    [SerializeField] private float centerStrength = 12f;
    [SerializeField] private float centerDamping = 5f;
    [SerializeField] private float maxCenterForce = 15f;

    [Header("Leg Standing Support")]
    [SerializeField] private bool enableStandingSupport = false;
    [SerializeField] private float heightStrength = 180f;
    [SerializeField] private float heightDamping = 35f;
    [SerializeField] private float gravitySupport = 0.35f;
    [SerializeField] private float maxHeightForce = 150f;

    [Header("Stability")]
    [SerializeField] private float supportBlendTime = 0.5f;
    [SerializeField] private float maxHeightError = 0.25f;

    private Rigidbody leftFootBody;
    private Rigidbody rightFootBody;

    private Vector3 hipsLocalUp;

    private float leftStandingHeight;
    private float rightStandingHeight;
    private float totalMass;
    private float supportBlend;

    private void Start()
    {
        if (hips == null ||
            leftFoot == null ||
            rightFoot == null ||
            poseReference == null)
        {
            Debug.LogError(
                "Balance V2: Missing references!",
                this
            );

            enabled = false;
            return;
        }

        // FIX:
        // Feet may not have Rigidbody directly.
        // Find Rigidbody on the foot or its parents.
        leftFootBody =
            leftFoot.GetComponentInParent<Rigidbody>();

        rightFootBody =
            rightFoot.GetComponentInParent<Rigidbody>();

        if (leftFootBody == null ||
            rightFootBody == null ||
            leftFootBody == hips ||
            rightFootBody == hips)
        {
            Debug.LogError(
                "Balance V2: Cannot find valid leg Rigidbodies!",
                this
            );

            enabled = false;
            return;
        }

        Transform refHips =
            FindBone(poseReference, "Hips");

        Transform refLeftSupport =
            FindBone(
                poseReference,
                leftFootBody.gameObject.name
            );

        Transform refRightSupport =
            FindBone(
                poseReference,
                rightFootBody.gameObject.name
            );

        if (refHips == null ||
            refLeftSupport == null ||
            refRightSupport == null)
        {
            Debug.LogError(
                "Balance V2: Reference bones missing!",
                this
            );

            enabled = false;
            return;
        }

        // Match height to the actual support bone.
        leftStandingHeight = Mathf.Max(
            0.2f,
            refHips.position.y -
            refLeftSupport.position.y
        );

        rightStandingHeight = Mathf.Max(
            0.2f,
            refHips.position.y -
            refRightSupport.position.y
        );

        hipsLocalUp =
            Quaternion.Inverse(hips.rotation) *
            Vector3.up;

        totalMass = 0f;

        Rigidbody[] bodies =
            GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody body in bodies)
        {
            totalMass += body.mass;
        }

        supportBlend = 0f;

        Debug.Log(
            "Balance V2 ready! Left: " +
            leftFootBody.name +
            " | Right: " +
            rightFootBody.name
        );
    }

    private Transform FindBone(
        Transform root,
        string boneName)
    {
        Transform[] bones =
            root.GetComponentsInChildren<Transform>(true);

        foreach (Transform bone in bones)
        {
            if (bone.name == boneName)
                return bone;
        }

        return null;
    }

    private void FixedUpdate()
    {
        if (hips == null || hips.isKinematic)
            return;

        bool leftGrounded = ProbeFoot(leftFoot);
        bool rightGrounded = ProbeFoot(rightFoot);

        int groundedCount =
            (leftGrounded ? 1 : 0) +
            (rightGrounded ? 1 : 0);

        Vector3 currentUp =
            hips.rotation * hipsLocalUp;

        float tilt = Vector3.Angle(
            currentUp,
            Vector3.up
        );

        bool canBalance =
            groundedCount > 0 &&
            tilt <= maxRecoveryTilt;

        float targetBlend =
            canBalance ? 1f : 0f;

        supportBlend = Mathf.MoveTowards(
            supportBlend,
            targetBlend,
            Time.fixedDeltaTime /
            Mathf.Max(0.05f, supportBlendTime)
        );

        if (!canBalance)
            return;

        ApplyUprightTorque(currentUp);

        ApplyCenterForce(
            leftGrounded,
            rightGrounded,
            groundedCount
        );

        if (enableStandingSupport)
        {
            ApplyLegSupport(
                leftGrounded,
                rightGrounded,
                groundedCount
            );
        }
    }

    private bool ProbeFoot(Transform foot)
    {
        Vector3 origin =
            foot.position +
            Vector3.up * probeHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            probeHeight + probeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Ignore own body parts.
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y < 0.65f)
                continue;

            return true;
        }

        return false;
    }

    private void ApplyUprightTorque(Vector3 currentUp)
    {
        Vector3 error =
            Vector3.Cross(currentUp, Vector3.up);

        Vector3 angularVelocity =
            Vector3.ProjectOnPlane(
                hips.angularVelocity,
                Vector3.up
            );

        Vector3 torque =
            error * uprightStrength -
            angularVelocity * uprightDamping;

        torque = Vector3.ClampMagnitude(
            torque,
            maxBalanceTorque
        );

        hips.AddTorque(
            torque * supportBlend,
            ForceMode.Force
        );
    }

    private void ApplyCenterForce(
        bool leftGrounded,
        bool rightGrounded,
        int groundedCount)
    {
        if (groundedCount == 0)
            return;

        Vector3 center = Vector3.zero;
        Vector3 footVelocity = Vector3.zero;

        if (leftGrounded)
        {
            center += leftFoot.position;
            footVelocity += leftFootBody.linearVelocity;
        }

        if (rightGrounded)
        {
            center += rightFoot.position;
            footVelocity += rightFootBody.linearVelocity;
        }

        center /= groundedCount;
        footVelocity /= groundedCount;

        Vector3 offset =
            center - hips.position;

        offset.y = 0f;

        Vector3 relativeVelocity =
            hips.linearVelocity - footVelocity;

        relativeVelocity.y = 0f;

        Vector3 force =
            offset * centerStrength -
            relativeVelocity * centerDamping;

        force = Vector3.ClampMagnitude(
            force,
            maxCenterForce
        ) * supportBlend;

        hips.AddForce(force, ForceMode.Force);

        Vector3 reaction =
            -force / groundedCount;

        if (leftGrounded && !leftFootBody.isKinematic)
        {
            leftFootBody.AddForce(
                reaction,
                ForceMode.Force
            );
        }

        if (rightGrounded && !rightFootBody.isKinematic)
        {
            rightFootBody.AddForce(
                reaction,
                ForceMode.Force
            );
        }
    }

    private void ApplyLegSupport(
        bool leftGrounded,
        bool rightGrounded,
        int groundedCount)
    {
        if (groundedCount == 0)
            return;

        float maxForcePerFoot =
            maxHeightForce / groundedCount;

        float gravityForcePerFoot =
            totalMass *
            Mathf.Abs(Physics.gravity.y) *
            gravitySupport /
            groundedCount;

        if (leftGrounded)
        {
            ExtendLeg(
                leftFootBody,
                leftStandingHeight,
                gravityForcePerFoot,
                maxForcePerFoot
            );
        }

        if (rightGrounded)
        {
            ExtendLeg(
                rightFootBody,
                rightStandingHeight,
                gravityForcePerFoot,
                maxForcePerFoot
            );
        }
    }

    private void ExtendLeg(
        Rigidbody supportBody,
        float standingHeight,
        float gravityForce,
        float maxForce)
    {
        if (supportBody == null ||
            supportBody.isKinematic)
            return;

        float currentLegHeight =
            hips.position.y -
            supportBody.position.y;

        float heightError = Mathf.Clamp(
            standingHeight - currentLegHeight,
            -maxHeightError,
            maxHeightError
        );

        float relativeVelocity =
            hips.linearVelocity.y -
            supportBody.linearVelocity.y;

        float force =
            heightError * heightStrength -
            relativeVelocity * heightDamping +
            gravityForce;

        force = Mathf.Clamp(
            force,
            0f,
            maxForce
        ) * supportBlend;

        Vector3 supportForce =
            Vector3.up * force;

        hips.AddForce(
            supportForce,
            ForceMode.Force
        );

        supportBody.AddForce(
            -supportForce,
            ForceMode.Force
        );
    }
}
