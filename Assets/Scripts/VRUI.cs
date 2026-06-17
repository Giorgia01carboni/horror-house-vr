using UnityEngine;

/// Shared helper for making runtime-built UI canvases visible in VR.
///
/// ScreenSpaceOverlay canvases do not render to the headset, so any full-screen
/// UI (pause menu, endings, game over) is invisible in VR. This converts an
/// already-created canvas into a head-locked WORLD-space canvas parented to the
/// VR camera, so it fills the view and follows the head.
///
/// Only the VR branch should call this; the keyboard/mouse path keeps using
/// ScreenSpaceOverlay unchanged.
public static class VRUI
{
    public static bool IsVR => UnityEngine.XR.XRSettings.enabled;

    public static Transform GetVRCamera()
    {
        if (Camera.main != null) return Camera.main.transform;
        var rig = Object.FindObjectOfType<OVRCameraRig>();
        return rig != null ? rig.centerEyeAnchor : null;
    }

    /// Convert a canvas to a head-locked world-space canvas in front of the eyes.
    /// distance/scale are tuned for a near-full-FOV overlay on Quest 3 / Quest Pro;
    /// adjust here if edges aren't fully covered in the headset.
    public static void MakeHeadLocked(Canvas canvas, float distance = 0.5f, float scale = 0.0012f, Vector2 size = default)
    {
        if (canvas == null) return;

        canvas.renderMode = RenderMode.WorldSpace;

        var t = canvas.transform;
        var cam = GetVRCamera();
        if (cam != null) t.SetParent(cam, false);

        t.localPosition = new Vector3(0f, 0f, distance);
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one * scale;

        var rt = (RectTransform)t;
        rt.sizeDelta = size == default ? new Vector2(1920f, 1080f) : size;
    }
}
