using UnityEngine;

// Attach to the carillon root (or ensure it is reachable via GetComponentInParent from the
// InspectableObject that ObjectInspector examines).
//
// VR:  grip one hand near the box body to hold it, then move the other hand in a
//      circle near the handle to spin the crank — melody plays while it spins.
// KB:  press E to enter ObjectInspector examine mode, rotate the box until the handle
//      faces you, then hold E to play — the melody and crank spin stop the instant you release.
public class CarillonInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform handleTransform;   // Musicbox_handle child

    [Header("Body grab (VR)")]
    [SerializeField] float bodyGrabRange = 0.15f;

    [Header("Handle spin (VR)")]
    [SerializeField] float handleProximity  = 0.12f;
    [SerializeField] float spinDrag         = 3f;

    [Header("Handle spin (KB — plays while E held)")]
    [SerializeField] float   kbHandleSpinSpeed  = 360f;   // deg/s while E is held
    [SerializeField] Vector3 kbHandleSpinAxis   = new Vector3(1, 0, 0); // local axis of the crank — change if wrong
    [SerializeField] float   handleFacingDotMin = 0.35f;  // lower = more lenient facing check

    [Header("Prompt text")]
    [SerializeField] string kbPlayPrompt = "Wanna play? Hold [E]";

    [Header("Audio")]
    [SerializeField] AudioClip melodyClip;
    [SerializeField] float minSpinDegPerSec = 45f;

    // VR state
    OVRInput.Controller _bodyCtrl    = OVRInput.Controller.None;
    Transform           _grabHand;
    Vector3             _grabOffset;
    Quaternion          _grabRotOffset;
    float               _handleAngVel;
    Rigidbody           _rb;

    InspectableObject[] _inspectables; // all InspectableObjects on self or children
    AudioSource         _src;
    Camera              _cam;

    bool IsVR => VRPhysicsHand.Instance != null && VRPhysicsHand.Instance.IsVR;

    void Start()
    {
        if (handleTransform == null)
            handleTransform = transform.Find("Musicbox_handle");

        _inspectables = GetComponentsInChildren<InspectableObject>(includeInactive: true);

        var playerLook = GameObject.FindWithTag("Player")?.GetComponent<PlayerLook>();
        _cam = playerLook?.cam ?? Camera.main;

        _src              = gameObject.AddComponent<AudioSource>();
        _src.clip         = melodyClip;
        _src.loop         = true;
        _src.spatialBlend = 1f;
        _src.playOnAwake  = false;

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        // Held = kinematic (we drive the transform); released = gravity takes over.
        // useGravity MUST be forced on: a Rigidbody added earlier in the Inspector may
        // have had gravity disabled, which left the carillon hanging in mid-air.
        _rb.useGravity  = true;
        _rb.isKinematic = true;
        // Continuous detection so a fast drop doesn't tunnel through floors/furniture.
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        EnsureSolidCollider();
    }

    // The carillon's only collider is the inspection BoxCollider (a trigger), which can't
    // stop a falling Rigidbody, so it drops straight through whatever is underneath.
    // Add a matching non-trigger collider for physics, leaving the trigger one intact.
    void EnsureSolidCollider()
    {
        foreach (var c in GetComponents<Collider>())
            if (!c.isTrigger) return; // already have a solid collider — nothing to do

        var solid = gameObject.AddComponent<BoxCollider>();
        solid.isTrigger = false;

        // Size the physics box to the actual visible mesh, NOT the inspection trigger box
        // (which is deliberately oversized for easy examine). A too-tall box sticks out
        // below the carillon, so it rests on a "cushion" above the furniture instead of
        // settling on it.
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            Vector3 ls = transform.lossyScale;
            solid.center = transform.InverseTransformPoint(b.center);
            solid.size   = new Vector3(
                b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        }
    }

    void Update()
    {
        if (IsVR)
        {
            var hand = VRPhysicsHand.Instance;
            UpdateBodyGrab(hand);
            UpdateHandleSpin(hand);
            UpdateVRAudio();
        }
        else
        {
            UpdateKeyboard();
        }
    }

    // ── Keyboard / Mouse ───────────────────────────────────────────────────────

    void UpdateKeyboard()
    {
        if (VRRevolver.GunIsHeld) { HintManager.Instance?.Hide(this); StopMelody(); return; }

        // Only active while ObjectInspector is in examine mode for any part of this carillon
        bool examining = false;
        if (_inspectables != null)
            foreach (var ins in _inspectables)
                if (ins == ObjectInspector.CurrentlyExamined) { examining = true; break; }

        if (!examining)
        {
            HintManager.Instance?.Hide(this);
            StopMelody();
            return;
        }

        // Determine whether the handle is turned to face the camera
        bool handleFacing = HandleFacingCamera();

        if (handleFacing)
            HintManager.Instance?.Show(this, kbPlayPrompt, 2);
        else
            HintManager.Instance?.Hide(this);

        if (handleFacing && Input.GetKey(KeyCode.E))
        {
            PlayMelody();
            if (handleTransform != null)
                handleTransform.Rotate(kbHandleSpinAxis, kbHandleSpinSpeed * Time.unscaledDeltaTime, Space.Self);
        }
        else
        {
            StopMelody();
        }
    }

    bool HandleFacingCamera()
    {
        if (_cam == null || handleTransform == null) return false;

        // The handle is "facing" the camera when its spin axis roughly aligns with the
        // camera-to-handle direction. Uses kbHandleSpinAxis (configurable in Inspector).
        Vector3 worldAxis   = handleTransform.TransformDirection(kbHandleSpinAxis).normalized;
        Vector3 camToHandle = (handleTransform.position - _cam.transform.position).normalized;
        float   dot         = Mathf.Abs(Vector3.Dot(worldAxis, camToHandle));
        return dot >= handleFacingDotMin;
    }

    // ── VR body grab ───────────────────────────────────────────────────────────

    void UpdateBodyGrab(VRPhysicsHand hand)
    {
        if (_bodyCtrl == OVRInput.Controller.None)
        {
            TryGrabBody(OVRInput.Controller.RTouch, hand.RightHand);
            TryGrabBody(OVRInput.Controller.LTouch, hand.LeftHand);
        }
        else
        {
            if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, _bodyCtrl))
            {
                Vector3 throwVel = _bodyCtrl == OVRInput.Controller.RTouch
                    ? VRPhysicsHand.Instance.RightVelocity
                    : VRPhysicsHand.Instance.LeftVelocity;
                _bodyCtrl = OVRInput.Controller.None;
                _grabHand = null;
                if (_rb != null)
                {
                    _rb.isKinematic = false;
                    _rb.velocity    = throwVel;
                }
            }
            else if (_grabHand != null)
            {
                transform.position = _grabHand.position + _grabHand.TransformDirection(_grabOffset);
                transform.rotation = _grabHand.rotation * _grabRotOffset;
            }
        }
    }

    void TryGrabBody(OVRInput.Controller ctrl, Transform handTf)
    {
        if (handTf == null) return;
        if (!OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, ctrl)) return;
        if (Vector3.Distance(handTf.position, transform.position) > bodyGrabRange) return;

        _bodyCtrl      = ctrl;
        _grabHand      = handTf;
        _grabOffset    = Quaternion.Inverse(handTf.rotation) * (transform.position - handTf.position);
        _grabRotOffset = Quaternion.Inverse(handTf.rotation) * transform.rotation;

        // Suspend physics while held (it may have been falling after a previous release).
        if (_rb != null)
        {
            _rb.velocity        = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
        }
    }

    // ── VR handle spin ─────────────────────────────────────────────────────────

    void UpdateHandleSpin(VRPhysicsHand hand)
    {
        if (handleTransform == null) return;

        OVRInput.Controller freeCtrl = _bodyCtrl == OVRInput.Controller.RTouch
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
        Transform freeHand = freeCtrl == OVRInput.Controller.RTouch
            ? hand.RightHand : hand.LeftHand;

        bool canSpin = _bodyCtrl != OVRInput.Controller.None && freeHand != null;

        if (canSpin)
        {
            Vector3 toHand      = freeHand.position - handleTransform.position;
            Vector3 spinAxis    = handleTransform.right;
            Vector3 toHand_perp = toHand - Vector3.Dot(toHand, spinAxis) * spinAxis;
            float   radius      = toHand_perp.magnitude;

            if (toHand.magnitude <= handleProximity && radius > 0.01f)
            {
                Vector3 tangent   = Vector3.Cross(spinAxis, toHand_perp).normalized;
                Vector3 handVel   = freeCtrl == OVRInput.Controller.RTouch
                    ? hand.RightVelocity : hand.LeftVelocity;
                float   tangSpeed = Vector3.Dot(handVel, tangent);
                _handleAngVel = (tangSpeed / radius) * Mathf.Rad2Deg;
            }
            else
            {
                Decay();
            }
        }
        else
        {
            Decay();
        }

        if (_handleAngVel != 0f)
            handleTransform.Rotate(Vector3.right, _handleAngVel * Time.deltaTime, Space.Self);
    }

    void Decay()
    {
        _handleAngVel = Mathf.Lerp(_handleAngVel, 0f, spinDrag * Time.deltaTime);
        if (Mathf.Abs(_handleAngVel) < 1f) _handleAngVel = 0f;
    }

    // ── Audio helpers ──────────────────────────────────────────────────────────

    void UpdateVRAudio()
    {
        if (melodyClip == null) return;
        bool spinning = Mathf.Abs(_handleAngVel) >= minSpinDegPerSec;
        if ( spinning) PlayMelody();
        else          StopMelody();
    }

    void PlayMelody()
    {
        if (_src == null || _src.isPlaying) return;
        _src.Play();
        _src.time = 1.0f; // skip the silent/lead-in first second
    }

    void StopMelody()
    {
        if (_src != null && _src.isPlaying) _src.Stop();
    }
}
