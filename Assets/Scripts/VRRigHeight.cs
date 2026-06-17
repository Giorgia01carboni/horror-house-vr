using UnityEngine;

/// Keeps the VR camera rig at a fixed eye height above the player root.
///
/// Background: with EyeLevel tracking origin the headset reports its pose
/// relative to the rig, so the rig must sit at eye height. Something resets the
/// rig's localPosition.y to 0 at runtime (camera collapses to the feet). This
/// re-applies the intended height every LateUpdate, after that reset, so the
/// camera stays at eye level in both the Meta XR Simulator and on a real headset.
[DefaultExecutionOrder(10000)]
public class VRRigHeight : MonoBehaviour
{
    [Tooltip("Eye height (metres) of the rig above the player root.")]
    [SerializeField] float eyeHeight = 1.69f;

    [Tooltip("Only enforce the height when VR controllers are connected.")]
    [SerializeField] bool onlyInVR = true;

    bool active;
    bool logged;

    void OnEnable() => Reevaluate();

    void Reevaluate()
    {
        active = !onlyInVR || UnityEngine.XR.XRSettings.enabled;
    }

    void LateUpdate()
    {
        if (!active) Reevaluate();

        if (!logged)
        {
            logged = true;
            Debug.Log($"[VRRigHeight] XRSettings.enabled={UnityEngine.XR.XRSettings.enabled} | OVRPlugin.initialized={OVRPlugin.initialized} | IsControllerConnected(R)={OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)} | active={active} | localY={transform.localPosition.y:F3}");
        }

        if (!active) return;

        Vector3 p = transform.localPosition;
        if (!Mathf.Approximately(p.y, eyeHeight))
        {
            p.y = eyeHeight;
            transform.localPosition = p;
        }
    }
}
