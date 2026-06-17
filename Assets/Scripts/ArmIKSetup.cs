using UnityEngine;
using UnityEngine.Animations.Rigging;

public class ArmIKSetup : MonoBehaviour
{
    const string RightUpperArm = "root/pelvis/spine_01/spine_02/spine_03/clavicle_r/upperarm_r";
    const string RightLowerArm = "root/pelvis/spine_01/spine_02/spine_03/clavicle_r/upperarm_r/lowerarm_r";
    const string RightHand     = "root/pelvis/spine_01/spine_02/spine_03/clavicle_r/upperarm_r/lowerarm_r/hand_r";
    const string RightAnchor   = "[BuildingBlock] Camera Rig/TrackingSpace/RightHandAnchor";

    const string LeftUpperArm  = "root/pelvis/spine_01/spine_02/spine_03/clavicle_l/upperarm_l";
    const string LeftLowerArm  = "root/pelvis/spine_01/spine_02/spine_03/clavicle_l/upperarm_l/lowerarm_l";
    const string LeftHand      = "root/pelvis/spine_01/spine_02/spine_03/clavicle_l/upperarm_l/lowerarm_l/hand_l";
    const string LeftAnchor    = "[BuildingBlock] Camera Rig/TrackingSpace/LeftHandAnchor";

    void Start()
    {
        Transform t = transform;

        Transform rightAnchor = GameObject.Find("RightHandAnchor")?.transform;
        Transform leftAnchor  = GameObject.Find("LeftHandAnchor")?.transform;

        Debug.LogWarning($"[ArmIKSetup] START CALLED — rightAnchor={(rightAnchor == null ? "NULL" : rightAnchor.name)} | leftAnchor={(leftAnchor == null ? "NULL" : leftAnchor.name)}", this);
        Debug.LogWarning($"[ArmIKSetup] upperarm_r={t.Find(RightUpperArm)?.name ?? "NULL"} | hand_r={t.Find(RightHand)?.name ?? "NULL"} | upperarm_l={t.Find(LeftUpperArm)?.name ?? "NULL"} | hand_l={t.Find(LeftHand)?.name ?? "NULL"}", this);

        // Elbow hints: static GameObjects parented to the player at natural elbow positions.
        // Without these, TwoBoneIK has no pole target and bends the arms upward.
        var rightHint = new GameObject("ElbowHint_R");
        rightHint.transform.SetParent(t, false);
        rightHint.transform.localPosition = new Vector3( 0.45f, 0.9f, -0.2f);

        var leftHint = new GameObject("ElbowHint_L");
        leftHint.transform.SetParent(t, false);
        leftHint.transform.localPosition = new Vector3(-0.45f, 0.9f, -0.2f);

        foreach (var ik in GetComponentsInChildren<TwoBoneIKConstraint>(true))
        {
            var d = ik.data;
            if (ik.gameObject.name == "IK_RightArm")
            {
                d.root   = t.Find(RightUpperArm);
                d.mid    = t.Find(RightLowerArm);
                d.tip    = t.Find(RightHand);
                d.target = rightAnchor;
                d.hint   = rightHint.transform;
                d.targetPositionWeight = 1f;
                d.targetRotationWeight = 1f;
                d.hintWeight = 1f;
            }
            else if (ik.gameObject.name == "IK_LeftArm")
            {
                d.root   = t.Find(LeftUpperArm);
                d.mid    = t.Find(LeftLowerArm);
                d.tip    = t.Find(LeftHand);
                d.target = leftAnchor;
                d.hint   = leftHint.transform;
                d.targetPositionWeight = 1f;
                d.targetRotationWeight = 1f;
                d.hintWeight = 1f;
            }
            ik.data = d;
        }

        var rigBuilder = GetComponent<RigBuilder>();
        if (rigBuilder != null)
        {
            rigBuilder.layers.Clear();
            var rig = GetComponentInChildren<Rig>(true);
            if (rig != null)
                rigBuilder.layers.Add(new RigLayer(rig, true));
            rigBuilder.Build();
        }

        Destroy(this);
    }
}
