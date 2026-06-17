using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] Transform holdPoint;
    [SerializeField] TextMeshProUGUI hintText;
    [SerializeField] float grabRange = 2.5f;
    [SerializeField] float vrGrabRange = 0.3f;
    [SerializeField] float throwForce = 30f;
    [Tooltip("VR only: multiplier applied to the measured hand speed (m/s) when throwing.")]
    [SerializeField] float vrThrowMultiplier = 1.4f;
    [Tooltip("VR only: hard cap on throw speed (m/s) so a fast flick can't launch the rock into orbit.")]
    [SerializeField] float vrMaxThrowSpeed = 12f;
    [SerializeField] GameObject crosshair;

    GrabbableRock heldRock;
    Camera cam;
    Animator animator;
    Transform kbHoldPoint;
    Transform vrHand;          // real right controller anchor: reaches the floor when you crouch
    VRRevolver _revolver;

    public bool IsHoldingRock => heldRock != null; // camera-relative hold point used in keyboard mode (IK corrupts holdPoint)

    bool IsVR => UnityEngine.XR.XRSettings.enabled;

    void Start()
    {
        cam = GetComponent<PlayerLook>()?.cam;
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (cam == null) Debug.LogError("PlayerInteract: no camera found. Assign it on PlayerLook.", this);
        animator = GetComponentInChildren<Animator>();

        // Create a hold point parented to the camera so IK on hand_r doesn't corrupt the throw origin
        var kbGO = new GameObject("KBHoldPoint");
        kbGO.transform.SetParent(cam.transform);
        kbGO.transform.localPosition = new Vector3(0.25f, -0.15f, 0.5f);
        kbGO.transform.localRotation = Quaternion.identity;
        kbHoldPoint = kbGO.transform;

        // VR grab/hold reference = the real right controller. With arm IK (no torso
        // bend) the hand bone can't reach the floor, but the controller anchor does
        // when you physically crouch, so grabbing rocks from the ground works.
        var rig = GetComponentInChildren<OVRCameraRig>(true);
        vrHand = (rig != null && rig.rightHandAnchor != null) ? rig.rightHandAnchor : holdPoint;

        _revolver = FindObjectOfType<VRRevolver>();
        EnsureCrosshair();
        CreateGrabMarker();
    }

    // VR debug aid: a small solid dot pinned to the exact point the grab is measured
    // from (the right controller anchor). Makes it obvious where to put your hand to
    // grab. Kept small (5 cm) so it doesn't block the view; the collider is removed so
    // it can't be picked up by the grab's OverlapSphere or block physics.
    void CreateGrabMarker()
    {
        if (!IsVR || vrHand == null) return;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "VRGrabMarker";
        var col = marker.GetComponent<Collider>();
        if (col != null) Destroy(col);

        marker.transform.SetParent(vrHand, false);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = Vector3.one * 0.05f;

        var rend = marker.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = new Color(0.2f, 0.9f, 1f, 1f); // bright cyan, opaque is fine at this size
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }

    void EnsureCrosshair()
    {
        if (crosshair != null) { crosshair.SetActive(false); return; }

        // Only attach to a ScreenSpaceOverlay canvas so inspection/pause canvases don't interfere
        Canvas canvas = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null)
        {
            var canvasGO = new GameObject("ThrowCrosshairCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        var go = new GameObject("ThrowCrosshair");
        go.transform.SetParent(canvas.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.1f, 0.1f, 0.85f);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(12f, 12f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        crosshair = go;
        crosshair.SetActive(false);
    }

    void Update()
    {
        if (IsVR)
            HandleVRInput();
        else
            HandleKeyboardInput();
    }

    bool LookingAt(Vector3 targetPos)
    {
        if (cam == null) return false;
        Vector3 dir = (targetPos - cam.transform.position).normalized;
        return Vector3.Dot(dir, cam.transform.forward) > 0.5f;
    }

    void HandleKeyboardInput()
    {
        if (_revolver != null && _revolver.IsHeld) { HideHint(); return; }

        if (heldRock == null)
        {
            GrabbableRock nearest = FindNearestRock(transform.position, grabRange);
            if (nearest != null && LookingAt(nearest.transform.position))
            {
                ShowHint("[LMB] / [E] Pick up rock");
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                    Grab(nearest);
            }
            else
            {
                HideHint();
            }
        }
        else
        {
            ShowHint("[RMB] Throw    [E] Drop");
            if (Input.GetMouseButtonDown(1))
                ThrowKeyboard();
            else if (Input.GetKeyDown(KeyCode.E))
                Drop();
        }
    }

    void HandleVRInput()
    {
        bool gripDown = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        bool gripUp   = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        if (heldRock == null)
        {
            GrabbableRock nearest = FindNearestRock(vrHand.position, vrGrabRange);
            if (nearest != null)
            {
                ShowHint("Grip to grab rock");
                if (gripDown) Grab(nearest);
            }
            else if (FindNearestRock(transform.position, grabRange) != null)
            {
                // Body is near a rock but the hand isn't in reach yet. The VR grab
                // is hand-proximity based, so prompt the player to reach out.
                ShowHint("Reach out with your hand and hold grip to grab the rock");
            }
            else
            {
                HideHint();
            }
        }
        else
        {
            ShowHint("Release grip to throw");
            if (gripUp)
            {
                // Use VRPhysicsHand's smoothed velocity so throw strength tracks the real
                // hand motion instead of a single noisy frame. Falls back to the raw
                // controller velocity if the hand provider isn't in the scene.
                var hand = VRPhysicsHand.Instance;
                Vector3 handVelocity = hand != null
                    ? hand.RightVelocity
                    : OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch);
                GrabbableRock rock = heldRock;
                heldRock = null;
                if (crosshair != null) crosshair.SetActive(false);
                if (handVelocity.magnitude > 0.5f)
                {
                    Vector3 v = handVelocity * vrThrowMultiplier;
                    if (v.magnitude > vrMaxThrowSpeed) v = v.normalized * vrMaxThrowSpeed;
                    rock.Release(v);
                }
                else
                {
                    rock.Release(Vector3.zero);
                }
            }
        }
    }

    GrabbableRock FindNearestRock(Vector3 origin, float range)
    {
        Collider[] hits = Physics.OverlapSphere(origin, range);
        GrabbableRock closest = null;
        float closestDist = float.MaxValue;
        foreach (var hit in hits)
        {
            var rock = hit.GetComponent<GrabbableRock>();
            if (rock == null || rock.IsHeld) continue;
            float dist = Vector3.Distance(origin, rock.transform.position);
            if (dist < closestDist) { closestDist = dist; closest = rock; }
        }
        return closest;
    }

    void Grab(GrabbableRock rock)
    {
        Transform attach = IsVR ? vrHand : kbHoldPoint;
        if (attach == null)
        {
            Debug.LogError("PlayerInteract: no hold point available (VR controller anchor / KB hold point missing).", this);
            return;
        }
        heldRock = rock;
        rock.Grab(attach);
        if (crosshair != null) crosshair.SetActive(true);
    }

    void ThrowKeyboard()
    {
        GrabbableRock rock = heldRock;
        heldRock = null;
        if (crosshair != null) crosshair.SetActive(false);
        animator?.SetTrigger("Throw");
        Vector3 from     = kbHoldPoint.position;
        Vector3 target   = GetAimTarget();
        Vector3 velocity = ComputeBallisticVelocity(from, target, throwForce);
        rock.Release(velocity);
    }

    // Solves for the flat-arc velocity that lands the rock exactly on 'to' at the given speed.
    // Falls back to a direct aim if the target is unreachable at that speed.
    Vector3 ComputeBallisticVelocity(Vector3 from, Vector3 to, float speed)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        Vector3 delta = to - from;
        float y = delta.y;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        float d = deltaXZ.magnitude;

        if (d < 0.001f)
            return Vector3.up * (y >= 0f ? speed : -speed);

        float v2 = speed * speed;
        float discriminant = v2 * v2 - g * (g * d * d + 2f * y * v2);

        if (discriminant < 0f)
            return delta.normalized * speed;

        // Low-angle (flat) solution: tan(θ) = (v² - √D) / (g·d)
        float tanTheta = (v2 - Mathf.Sqrt(discriminant)) / (g * d);
        float vH = speed / Mathf.Sqrt(1f + tanTheta * tanTheta);
        float vV = tanTheta * vH;

        return deltaXZ.normalized * vH + Vector3.up * vV;
    }

    Vector3 GetAimTarget()
    {
        if (cam == null) return transform.position + transform.forward * 50f;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<GrabbableRock>() != null) continue;
            return hit.point;
        }
        return ray.origin + ray.direction * 50f;
    }

    void Drop()
    {
        GrabbableRock rock = heldRock;
        heldRock = null;
        if (crosshair != null) crosshair.SetActive(false);
        rock.Release(Vector3.zero);
    }

    void ShowHint(string message, int priority = 1)
    {
        if (HintManager.Instance != null) { HintManager.Instance.Show(this, message, priority); return; }
        if (hintText == null) return;
        hintText.text = message;
        hintText.gameObject.SetActive(true);
    }

    void HideHint()
    {
        if (HintManager.Instance != null) { HintManager.Instance.Hide(this); return; }
        if (hintText == null) return;
        hintText.gameObject.SetActive(false);
    }
}
