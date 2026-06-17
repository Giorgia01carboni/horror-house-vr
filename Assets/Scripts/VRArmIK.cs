using UnityEngine;

/// Analytic two-bone IK that makes the avatar's arms follow the Quest controllers,
/// WITHOUT body tracking / the Movement SDK retargeting rig.
///
/// For each arm it reads the Humanoid bones (upperarm -> lowerarm -> hand) from the
/// Animator and bends them so the hand bone lands on the controller anchor
/// (RightHandAnchor / LeftHandAnchor of the OVRCameraRig). Because the anchors are
/// the REAL controller positions in tracking space, physically reaching / leaning
/// down moves the hands too — so grabbing rocks etc. keeps working (the grab is
/// measured from the hand_r bone, which this pins to the controller).
///
/// Runs in LateUpdate after the Animator, and BEFORE VRFingerPoser (exec order
/// 20000) so the fingers curl on top of the IK-posed hand.
///
/// Add this component to Man_03 (the root with the Animator + OVRCameraRig child).
/// Keyboard / editor (no VR) is untouched.
///
/// TUNING (in headset):
///  - If a hand points the wrong way, set the per-hand 'Hand Rotation Offset'.
///  - If an elbow bends the wrong direction (e.g. points up or into the body),
///    tweak 'Elbow Hint Local' (it is mirrored automatically for the left arm).
[DefaultExecutionOrder(15000)]
public class VRArmIK : MonoBehaviour
{
    [Header("Hand")]
    [Tooltip("Match the hand bone orientation to the controller.")]
    [SerializeField] bool matchHandRotation = true;
    [Tooltip("Euler offset applied to the RIGHT hand so the bone lines up with the controller. Tune in headset.")]
    [SerializeField] Vector3 rightHandRotationOffset = Vector3.zero;
    [Tooltip("Euler offset applied to the LEFT hand so the bone lines up with the controller. Tune in headset.")]
    [SerializeField] Vector3 leftHandRotationOffset = Vector3.zero;
    [Tooltip("Position offset (in controller-anchor local space) for where the wrist sits relative to the " +
             "controller. Push -Y / -Z to seat the hand lower / further back. Tune in headset.")]
    [SerializeField] Vector3 rightHandPositionOffset = Vector3.zero;
    [SerializeField] Vector3 leftHandPositionOffset = Vector3.zero;

    [Header("Elbow")]
    [Tooltip("Direction (in player-root space) the right elbow is pushed toward. " +
             "Default points down / back / outward. Mirrored on X for the left arm.")]
    [SerializeField] Vector3 elbowHintLocal = new Vector3(1f, -1f, -0.5f);

    [Header("Blend")]
    [Range(0f, 1f)]
    [Tooltip("0 = leave the animation pose, 1 = full IK to the controller.")]
    [SerializeField] float weight = 1f;

    class Arm
    {
        public Transform upper, lower, hand, target;
        public Vector3 handOffset;
        public Vector3 handPosOffset;
        public bool isRight;
    }

    Arm right, left;
    bool ready;

    bool IsVR => OVRPlugin.initialized;

    void Start()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("VRArmIK: needs a Humanoid Animator on the character.", this);
            enabled = false;
            return;
        }

        var rig = GetComponentInChildren<OVRCameraRig>(true);
        if (rig == null)
        {
            Debug.LogError("VRArmIK: no OVRCameraRig found under this object.", this);
            enabled = false;
            return;
        }

        right = BuildArm(animator, rig.rightHandAnchor, true);
        left  = BuildArm(animator, rig.leftHandAnchor,  false);
        ready = right != null || left != null;
    }

    Arm BuildArm(Animator a, Transform anchor, bool isRight)
    {
        Transform upper = a.GetBoneTransform(isRight ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform lower = a.GetBoneTransform(isRight ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        Transform hand  = a.GetBoneTransform(isRight ? HumanBodyBones.RightHand     : HumanBodyBones.LeftHand);
        if (upper == null || lower == null || hand == null || anchor == null)
        {
            Debug.LogWarning($"VRArmIK: missing arm bones or anchor ({(isRight ? "right" : "left")}); that arm is disabled.", this);
            return null;
        }
        return new Arm
        {
            upper = upper, lower = lower, hand = hand, target = anchor,
            handOffset = isRight ? rightHandRotationOffset : leftHandRotationOffset,
            handPosOffset = isRight ? rightHandPositionOffset : leftHandPositionOffset,
            isRight = isRight
        };
    }

    void LateUpdate()
    {
        if (!ready || !IsVR || weight <= 0f) return;
        Solve(right);
        Solve(left);
    }

    void Solve(Arm arm)
    {
        if (arm == null) return;

        Transform upper = arm.upper, lower = arm.lower, hand = arm.hand;
        Vector3 A = upper.position;
        Vector3 targetPos = arm.target.position + arm.target.rotation * arm.handPosOffset;

        float a = Vector3.Distance(upper.position, lower.position); // upper-arm length
        float b = Vector3.Distance(lower.position, hand.position);  // fore-arm length
        if (a < 1e-4f || b < 1e-4f) return;

        // Clamp the reach to what the arm can physically span.
        Vector3 toTarget = targetPos - A;
        float c = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);

        // Cache the current pose so we can blend back by 'weight'.
        Quaternion upper0 = upper.rotation;
        Quaternion lower0 = lower.rotation;

        // 1. Bend the elbow so |A -> hand| == c (law of cosines).
        float cosElbow = Mathf.Clamp((a * a + b * b - c * c) / (2f * a * b), -1f, 1f);
        float elbowTarget = Mathf.Acos(cosElbow);                       // desired interior angle at elbow
        Vector3 ba = upper.position - lower.position;
        Vector3 bh = hand.position - lower.position;
        float elbowCurrent = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba.normalized, bh.normalized), -1f, 1f));
        Vector3 elbowAxis = Vector3.Cross(ba, bh);
        if (elbowAxis.sqrMagnitude < 1e-8f)
            elbowAxis = Vector3.Cross(ba, ElbowHintPos(arm, A, c) - lower.position);
        if (elbowAxis.sqrMagnitude < 1e-8f) elbowAxis = transform.up;
        elbowAxis.Normalize();
        lower.rotation = Quaternion.AngleAxis((elbowTarget - elbowCurrent) * Mathf.Rad2Deg, elbowAxis) * lower.rotation;

        // 2. Aim the whole arm so the hand reaches the target.
        Quaternion aim = Quaternion.FromToRotation(hand.position - A, targetPos - A);
        upper.rotation = aim * upper.rotation;

        // 3. Roll the arm about the shoulder->target axis so the elbow faces the hint.
        Vector3 axisAT = (targetPos - A).normalized;
        Vector3 elbowDir = Vector3.ProjectOnPlane(lower.position - A, axisAT);
        Vector3 hintDir  = Vector3.ProjectOnPlane(ElbowHintPos(arm, A, c) - A, axisAT);
        if (elbowDir.sqrMagnitude > 1e-8f && hintDir.sqrMagnitude > 1e-8f)
        {
            float roll = Vector3.SignedAngle(elbowDir, hintDir, axisAT);
            upper.rotation = Quaternion.AngleAxis(roll, axisAT) * upper.rotation;
        }

        // Blend back toward the animation pose if weight < 1.
        if (weight < 1f)
        {
            upper.rotation = Quaternion.Slerp(upper0, upper.rotation, weight);
            lower.rotation = Quaternion.Slerp(lower0, lower.rotation, weight);
        }

        // 4. Match the hand to the controller orientation.
        if (matchHandRotation)
        {
            Quaternion handTarget = arm.target.rotation * Quaternion.Euler(arm.handOffset);
            hand.rotation = weight < 1f ? Quaternion.Slerp(hand.rotation, handTarget, weight) : handTarget;
        }
    }

    // A point the elbow is pushed toward, in player-root space (mirrored for the left arm).
    Vector3 ElbowHintPos(Arm arm, Vector3 shoulder, float armLength)
    {
        Vector3 hint = elbowHintLocal;
        if (!arm.isRight) hint.x = -hint.x;
        return shoulder + transform.rotation * (hint.normalized * armLength);
    }
}
