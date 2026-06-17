using UnityEngine;

/// Snap turn for VR with the RIGHT thumbstick (the project had no turn system, so
/// you could only face the rig's original forward). Rotates the player root in
/// fixed angle steps around the headset position, so locomotion forward, body and
/// legs all stay aligned with where you look. Snap turn is the VR comfort standard.
///
/// Attach to the Man_03 root (the object with the CharacterController / camera rig child).
public class VRTurnProvider : MonoBehaviour
{
    [Tooltip("Degrees rotated per snap.")]
    [SerializeField] float snapAngle = 35f;
    [Tooltip("Stick X must exceed this to trigger a snap.")]
    [SerializeField] float snapThreshold = 0.7f;

    Transform pivot;            // headset (CenterEyeAnchor); falls back to this transform
    bool snapArmed = true;      // snap fires once until the stick returns to centre

    bool IsVR => OVRPlugin.initialized;

    void Start()
    {
        var rig = GetComponentInChildren<OVRCameraRig>(true);
        if (rig != null && rig.centerEyeAnchor != null)
            pivot = rig.centerEyeAnchor;
        else
            pivot = transform;
    }

    void Update()
    {
        if (!IsVR) return;

        // Right controller's stick is its PRIMARY thumbstick. SecondaryThumbstick
        // combined with RTouch always returns (0,0) (Touch controllers have one
        // stick), which is why snap turn never fired.
        float x = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;

        if (snapArmed && Mathf.Abs(x) >= snapThreshold)
        {
            RotateAroundPivot(Mathf.Sign(x) * snapAngle);
            snapArmed = false;
        }
        else if (Mathf.Abs(x) < snapThreshold * 0.5f)
        {
            snapArmed = true;
        }
    }

    void RotateAroundPivot(float degrees)
    {
        // Rotate the whole player around the headset's vertical axis so the view
        // pivots around the user, not around a possibly-offset root origin.
        Vector3 pivotPos = pivot != null ? pivot.position : transform.position;
        transform.RotateAround(pivotPos, Vector3.up, degrees);
    }
}
