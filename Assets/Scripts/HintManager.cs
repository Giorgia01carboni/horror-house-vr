using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [SerializeField] TextMeshProUGUI hintText;

    [Header("VR hint (world-space, shown only in VR)")]
    [Tooltip("Distance in front of the headset the hint floats, in metres.")]
    [SerializeField] float vrDistance = 1.5f;
    [Tooltip("World scale of the VR hint canvas. Lower = smaller text.")]
    [SerializeField] float vrCanvasScale = 0.0022f;

    readonly Dictionary<object, (string message, int priority)> hints = new();

    // VR-only: a separate world-space canvas. The shared screen canvas is
    // ScreenSpaceOverlay (invisible in VR), and it also hosts the full-screen
    // fade overlay, so we must NOT convert it — we mirror the hint here instead.
    bool isVR;
    Transform vrCanvas;
    TextMeshProUGUI vrHintText;
    Transform vrCam;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // VR detection is done lazily (not in Start): at Start the OVR controllers
    // often aren't reported as connected yet, so we'd never build the VR canvas.
    void EnsureVRHint()
    {
        if (vrHintText != null) return; // already built
        bool nowVR = UnityEngine.XR.XRSettings.enabled;
        if (!nowVR) return;
        isVR = true;
        BuildVRHint();
        Refresh(); // surface any hint that's already pending
    }

    void BuildVRHint()
    {
        if (hintText == null) return; // need a template to clone the font/style from

        var go = new GameObject("VRHintCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(600f, 200f);
        go.transform.localScale = Vector3.one * vrCanvasScale;
        vrCanvas = go.transform;

        // Clone the existing hint label so we inherit its TMP font asset and styling.
        vrHintText = Instantiate(hintText, go.transform);
        var crt = vrHintText.rectTransform;
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        crt.localScale = Vector3.one;
        crt.localPosition = Vector3.zero;
        vrHintText.alignment = TextAlignmentOptions.Center;
        vrHintText.enableAutoSizing = true;
        vrHintText.fontSizeMin = 12f;
        vrHintText.fontSizeMax = 96f;

        // Render the hint ON TOP of all geometry (ignore depth) so it stays readable at a
        // fixed, comfortable distance even when a wall is closer than vrDistance — instead
        // of pulling the canvas right up to the eyes (which made it look giant).
        var mat = vrHintText.fontMaterial; // .fontMaterial returns an instance, not the shared asset
        mat.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = 4000; // after opaque + transparent geometry
        vrHintText.gameObject.SetActive(false);

        ResolveVRCamera();
    }

    void ResolveVRCamera()
    {
        if (Camera.main != null) { vrCam = Camera.main.transform; return; }
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null) vrCam = rig.centerEyeAnchor;
    }

    void LateUpdate()
    {
        EnsureVRHint();
        if (!isVR || vrCanvas == null) return;
        if (vrCam == null) { ResolveVRCamera(); if (vrCam == null) return; }

        // Fixed comfortable distance; the canvas renders on top of geometry (see BuildVRHint),
        // so it stays visible inside tight rooms without being pulled into the player's face.
        vrCanvas.position = vrCam.position + vrCam.forward * vrDistance;
        vrCanvas.rotation = Quaternion.LookRotation(vrCanvas.position - vrCam.position);
    }

    public void Show(object source, string message, int priority = 0)
    {
        hints[source] = (message, priority);
        Refresh();
    }

    public void Hide(object source)
    {
        hints.Remove(source);
        Refresh();
    }

    void Refresh()
    {
        if (hints.Count == 0)
        {
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (vrHintText != null) vrHintText.gameObject.SetActive(false);
            return;
        }

        string best = null;
        int bestPriority = int.MinValue;
        foreach (var h in hints.Values)
        {
            if (h.priority > bestPriority) { bestPriority = h.priority; best = h.message; }
        }

        if (hintText != null)
        {
            hintText.text = best;
            hintText.gameObject.SetActive(true);
        }
        if (vrHintText != null)
        {
            vrHintText.text = best;
            vrHintText.gameObject.SetActive(true);
        }
    }
}
