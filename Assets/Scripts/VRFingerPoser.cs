using System.Collections.Generic;
using UnityEngine;

/// Drives the visible finger bones of Man_03 from the Quest controller buttons,
/// because with controllers (not hand-tracking) the body-tracking finger pose is
/// only a guess. The hand/wrist still follow the controller via the Movement SDK
/// retargeting; this only overrides the finger phalanges:
///
///   Index trigger -> index finger curl
///   Grip          -> middle / ring / pinky curl
///   Thumb         -> curls when the thumb is NOT resting on a face button / stick
///
/// Bones are read from the Humanoid Animator (rig-agnostic). Runs after the
/// retargeter (high execution order + LateUpdate) so it wins on the fingers.
///
/// TUNING (in headset): if a finger bends the wrong way or sideways, flip/change
/// 'Curl Axis' (and 'Thumb Curl Axis'); use 'Left Hand Axis Flip' if the left hand
/// mirrors wrong. Adjust the max angles for how far each group closes.
[DefaultExecutionOrder(20000)]
public class VRFingerPoser : MonoBehaviour
{
    [Header("Curl axes (local bone space) - tune in headset")]
    [SerializeField] Vector3 curlAxis = new Vector3(0f, 0f, -1f);
    [SerializeField] Vector3 thumbCurlAxis = new Vector3(0f, -1f, 0f);
    [Tooltip("Flip the curl axis sign on the LEFT hand if it bends the wrong way.")]
    [SerializeField] bool leftHandAxisFlip = false;

    [Header("Max curl per phalanx (degrees)")]
    [SerializeField] float fingerMaxAngle = 60f;
    [SerializeField] float thumbMaxAngle  = 35f;

    [Header("Response")]
    [Tooltip("How fast the fingers follow the button value (higher = snappier).")]
    [SerializeField] float smoothing = 14f;

    class Finger
    {
        public Transform[] bones;
        public Quaternion[] openRot;
        public Vector3 axis;
        public float maxAngle;
        public float curl; // smoothed 0..1
    }

    // Per hand: index 0..4 = thumb, index, middle, ring, pinky
    readonly List<Finger> right = new List<Finger>();
    readonly List<Finger> left  = new List<Finger>();
    bool ready;

    bool IsVR => OVRPlugin.initialized;

    void Start()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("VRFingerPoser: needs a Humanoid Animator on the character.", this);
            enabled = false;
            return;
        }

        BuildHand(animator, true,  right);
        BuildHand(animator, false, left);
        ready = right.Count > 0 || left.Count > 0;
    }

    void BuildHand(Animator a, bool isRight, List<Finger> outList)
    {
        // Order matters: thumb, index, middle, ring, pinky
        AddFinger(a, outList, isRight, true,
            HB(isRight, "ThumbProximal"), HB(isRight, "ThumbIntermediate"), HB(isRight, "ThumbDistal"));
        AddFinger(a, outList, isRight, false,
            HB(isRight, "IndexProximal"), HB(isRight, "IndexIntermediate"), HB(isRight, "IndexDistal"));
        AddFinger(a, outList, isRight, false,
            HB(isRight, "MiddleProximal"), HB(isRight, "MiddleIntermediate"), HB(isRight, "MiddleDistal"));
        AddFinger(a, outList, isRight, false,
            HB(isRight, "RingProximal"), HB(isRight, "RingIntermediate"), HB(isRight, "RingDistal"));
        AddFinger(a, outList, isRight, false,
            HB(isRight, "LittleProximal"), HB(isRight, "LittleIntermediate"), HB(isRight, "LittleDistal"));
    }

    HumanBodyBones HB(bool isRight, string name)
    {
        string full = (isRight ? "Right" : "Left") + name;
        return (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), full);
    }

    void AddFinger(Animator a, List<Finger> outList, bool isRight, bool isThumb, params HumanBodyBones[] ids)
    {
        var bones = new List<Transform>();
        foreach (var id in ids)
        {
            var t = a.GetBoneTransform(id);
            if (t != null) bones.Add(t);
        }

        var f = new Finger
        {
            bones = bones.ToArray(),
            openRot = new Quaternion[bones.Count],
            axis = (isThumb ? thumbCurlAxis : curlAxis),
            maxAngle = (isThumb ? thumbMaxAngle : fingerMaxAngle)
        };
        if (!isRight && leftHandAxisFlip) f.axis = -f.axis;
        for (int i = 0; i < bones.Count; i++) f.openRot[i] = bones[i].localRotation;
        outList.Add(f);
    }

    void LateUpdate()
    {
        if (!ready || !IsVR) return;

        ApplyHand(right, OVRInput.Controller.RTouch);
        ApplyHand(left,  OVRInput.Controller.LTouch);
    }

    void ApplyHand(List<Finger> hand, OVRInput.Controller c)
    {
        if (hand.Count < 5) return;

        float index = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c);
        float grip  = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger,  c);
        float thumb = ThumbCurl(c);

        // 0 thumb, 1 index, 2 middle, 3 ring, 4 pinky
        DriveFinger(hand[0], thumb);
        DriveFinger(hand[1], index);
        DriveFinger(hand[2], grip);
        DriveFinger(hand[3], grip);
        DriveFinger(hand[4], grip);
    }

    float ThumbCurl(OVRInput.Controller c)
    {
        // Thumb is "up" (extended) while resting on the stick or a face button,
        // and curls in when it lifts off everything.
        bool resting =
            OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, c) ||
            OVRInput.Get(OVRInput.Touch.One, c) ||   // A / X
            OVRInput.Get(OVRInput.Touch.Two, c);     // B / Y
        return resting ? 0f : 1f;
    }

    void DriveFinger(Finger f, float target)
    {
        f.curl = Mathf.Lerp(f.curl, Mathf.Clamp01(target), 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        float angle = f.curl * f.maxAngle;
        var bend = Quaternion.AngleAxis(angle, f.axis);
        for (int i = 0; i < f.bones.Length; i++)
            f.bones[i].localRotation = f.openRot[i] * bend;
    }
}
