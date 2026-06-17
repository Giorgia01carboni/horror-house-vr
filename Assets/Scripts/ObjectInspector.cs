using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ObjectInspector : MonoBehaviour
{
    [SerializeField] float interactRange    = 2.5f;
    [SerializeField] float rotationSpeed   = 3.5f;
    [SerializeField] float transitionTime  = 0.3f;

    // Layer used to isolate the inspected object so it renders above the dark overlay.
    // Assign any unused layer (default 31). Must match the inspection camera's culling mask.
    [SerializeField] int inspectLayer = 31;

    // ── References ──────────────────────────────────────────────────────────────
    Camera       mainCam;
    InputManager inputManager;
    PlayerInteract playerInteract;

    // ── Runtime state ───────────────────────────────────────────────────────────
    InspectableObject candidate;   // object the player is looking at
    InspectableObject current;     // object currently being inspected
    bool inTransition;

    // ── Saved world state (restored on exit) ────────────────────────────────────
    Transform  savedParent;
    Vector3    savedLocalPos;
    Quaternion savedLocalRot;
    Vector3    savedLocalScale;
    GameObject[] layerGOs;
    int[]        savedLayers;
    int savedMainCamMask;

    // ── Scene objects created at Start ──────────────────────────────────────────
    Transform inspectPivot;
    Camera    inspectCam;
    Image     overlayImg;
    GameObject overlayRoot;

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);

    public static InspectableObject CurrentlyExamined { get; private set; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    void Start()
    {
        mainCam        = GetComponent<PlayerLook>()?.cam ?? Camera.main;
        inputManager   = GetComponent<InputManager>();
        playerInteract = GetComponent<PlayerInteract>();

        BuildInspectCamera();
        BuildOverlay();

        var pg = new GameObject("InspectPivot");
        pg.transform.SetParent(mainCam.transform);
        pg.transform.localPosition = Vector3.zero;
        pg.transform.localRotation = Quaternion.identity;
        inspectPivot = pg.transform;
    }

    void Update()
    {
        if (PauseMenu.IsPaused || inTransition) return;

        if (current != null)
            UpdateInspection();
        else
            UpdateDetection();
    }

    // ── Detection ───────────────────────────────────────────────────────────────

    void UpdateDetection()
    {
        if (playerInteract != null && playerInteract.IsHoldingRock) { ClearCandidate(); return; }
        if (VRRevolver.GunIsHeld) { ClearCandidate(); return; }

        InspectableObject found = null;
        var ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, interactRange))
            found = hit.collider.GetComponentInParent<InspectableObject>();

        if (found != candidate)
        {
            ClearCandidate();
            candidate = found;
            candidate?.GetComponent<ObjectHighlighter>()?.SetHighlight(true);
        }
        if (candidate == null) return;

        string hint = candidate.interactInPlace ? "[E]  Interact" : "[E] / [LMB]  Examine";
        HintManager.Instance?.Show(this, hint, 1);

        bool trigger = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
        if (IsVR) trigger |= OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

        if (trigger)
        {
            if (candidate.interactInPlace)
                candidate.onInteract.Invoke();
            else
                StartCoroutine(Enter(candidate));
        }
    }

    void ClearCandidate()
    {
        if (candidate != null)
        {
            HintManager.Instance?.Hide(this);
            candidate.GetComponent<ObjectHighlighter>()?.SetHighlight(false);
        }
        candidate = null;
    }

    // ── Inspection input ────────────────────────────────────────────────────────

    void UpdateInspection()
    {
        float rx = Input.GetAxis("Mouse X") * rotationSpeed;
        float ry = Input.GetAxis("Mouse Y") * rotationSpeed;
        inspectPivot.Rotate(Vector3.up,              -rx, Space.World);
        inspectPivot.Rotate(mainCam.transform.right,  ry, Space.World);

        bool exit = Input.GetMouseButtonDown(1);
        bool suppressE = current.GetComponentInParent<CarillonInteraction>() != null
                      || current.GetComponentInParent<GrabbablePainting>() != null;
        if (!suppressE)
            exit |= Input.GetKeyDown(KeyCode.E);
        if (IsVR) exit |= OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        if (exit) StartCoroutine(Exit());
    }

    // ── Transitions ─────────────────────────────────────────────────────────────

    IEnumerator Enter(InspectableObject target)
    {
        inTransition = true;
        ClearCandidate();

        if (inputManager   != null) inputManager.enabled   = false;
        if (playerInteract != null) playerInteract.enabled = false;
        Time.timeScale = 0f;

        overlayRoot.SetActive(true);
        yield return StartCoroutine(Fade(0f, 1f));

        // Save state
        savedParent     = target.transform.parent;
        savedLocalPos   = target.transform.localPosition;
        savedLocalRot   = target.transform.localRotation;
        savedLocalScale = target.transform.localScale;

        // For paintings we preserve world rotation so the face-forward orientation is kept
        bool isPainting = target.GetComponentInParent<GrabbablePainting>() != null;
        Quaternion savedWorldRot = target.transform.rotation;

        // Exclude inspect layer from main camera so it only shows in inspectCam
        savedMainCamMask    = mainCam.cullingMask;
        mainCam.cullingMask = savedMainCamMask & ~(1 << inspectLayer);

        SetLayers(target.gameObject, inspectLayer, out layerGOs, out savedLayers);

        // Position pivot at the right distance, reset its rotation
        float dist = target.inspectDistance > 0f
            ? target.inspectDistance
            : AutoFitDistance(target.gameObject);
        inspectPivot.localPosition = Vector3.forward * dist;
        inspectPivot.localRotation = Quaternion.identity;

        // Reparent — keep localScale, set rotation, center on anchor
        target.transform.SetParent(inspectPivot, false);
        target.transform.localScale = savedLocalScale;

        if (isPainting)
            // Preserve world rotation so the player always sees the face they were looking at
            target.transform.rotation = savedWorldRot;
        else
            target.transform.localRotation = target.inspectStartEuler == Vector3.zero
                ? Quaternion.identity
                : Quaternion.Euler(target.inspectStartEuler);

        target.transform.localPosition = target.inspectAnchor != null
            ? -target.inspectAnchor.localPosition
            : Vector3.zero;

        inspectCam.enabled = true;
        current = target;
        CurrentlyExamined = current;

        yield return StartCoroutine(Fade(1f, 0.88f));
        inTransition = false;
    }

    IEnumerator Exit()
    {
        inTransition = true;

        yield return StartCoroutine(Fade(0.88f, 1f));

        var target = current;
        current = null;
        CurrentlyExamined = null;

        RestoreLayers(layerGOs, savedLayers);
        mainCam.cullingMask = savedMainCamMask;

        target.transform.SetParent(savedParent, false);
        target.transform.localPosition = savedLocalPos;
        target.transform.localRotation = savedLocalRot;
        target.transform.localScale    = savedLocalScale;

        inspectCam.enabled = false;

        yield return StartCoroutine(Fade(1f, 0f));
        overlayRoot.SetActive(false);

        Time.timeScale = 1f;
        if (inputManager   != null) inputManager.enabled   = true;
        if (playerInteract != null) playerInteract.enabled = true;

        inTransition = false;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.unscaledDeltaTime;
            overlayImg.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / transitionTime));
            yield return null;
        }
        overlayImg.color = new Color(0f, 0f, 0f, to);
    }

    // ── Scene setup ─────────────────────────────────────────────────────────────

    void BuildInspectCamera()
    {
        var go = new GameObject("InspectCam");
        go.transform.SetParent(mainCam.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        inspectCam = go.AddComponent<Camera>();
        inspectCam.clearFlags    = CameraClearFlags.Depth;
        inspectCam.cullingMask   = 1 << inspectLayer;
        inspectCam.depth         = mainCam.depth + 2;
        inspectCam.fieldOfView   = mainCam.fieldOfView;
        inspectCam.nearClipPlane = 0.01f;
        inspectCam.farClipPlane  = 50f;
        inspectCam.enabled = false;

        // A warm point light that only illuminates the inspected object
        var lg = new GameObject("InspectLight");
        lg.transform.SetParent(go.transform);
        lg.transform.localPosition = new Vector3(0.4f, 0.6f, -0.2f);
        var lt = lg.AddComponent<Light>();
        lt.type        = LightType.Point;
        lt.color       = new Color(1f, 0.95f, 0.88f);
        lt.intensity   = 2f;
        lt.range       = 5f;
        lt.cullingMask = 1 << inspectLayer;
    }

    void BuildOverlay()
    {
        // A camera at depth+1 that hosts only the dark overlay canvas.
        // The inspectCam at depth+2 then draws the object on top of it.
        var uiCamGO = new GameObject("InspectUICam");
        uiCamGO.transform.SetParent(mainCam.transform);
        var uiCam = uiCamGO.AddComponent<Camera>();
        uiCam.clearFlags  = CameraClearFlags.Depth;
        uiCam.cullingMask = 0;
        uiCam.depth       = mainCam.depth + 1;

        overlayRoot = new GameObject("InspectCanvas");
        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCam;
        canvas.sortingOrder = 10;
        overlayRoot.AddComponent<CanvasScaler>();

        var igo = new GameObject("Overlay");
        igo.transform.SetParent(overlayRoot.transform, false);
        overlayImg = igo.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0f);
        var r = overlayImg.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        overlayRoot.SetActive(false);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    float AutoFitDistance(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0.6f;

        var bounds = renderers[0].bounds;
        foreach (var rnd in renderers) bounds.Encapsulate(rnd.bounds);

        // Largest axis of the object's world-space bounding box
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        // Distance so the object subtends ~50% of the vertical FOV
        float fovRad = mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        return Mathf.Clamp((size * 0.5f) / Mathf.Tan(fovRad), 0.3f, 2.5f);
    }

    void SetLayers(GameObject root, int layer, out GameObject[] gos, out int[] original)
    {
        var list = new List<GameObject>();
        CollectAll(root.transform, list);
        gos = list.ToArray();
        original = new int[gos.Length];
        for (int i = 0; i < gos.Length; i++) { original[i] = gos[i].layer; gos[i].layer = layer; }
    }

    void RestoreLayers(GameObject[] gos, int[] original)
    {
        for (int i = 0; i < gos.Length; i++) gos[i].layer = original[i];
    }

    void CollectAll(Transform t, List<GameObject> list)
    {
        list.Add(t.gameObject);
        foreach (Transform c in t) CollectAll(c, list);
    }
}
