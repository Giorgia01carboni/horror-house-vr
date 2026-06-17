using UnityEngine;

// Attach to the painting root alongside InspectableObject.
//
// VR:  grip near the frame to grab and freely rotate the painting.
//      Release grip → painting stays at current rotation.
//
// KB:  ObjectInspector examine mode (E to enter, RMB to exit).
//      Rotate with mouse. When the back faces the camera the key hint appears.
//      E then collects the key instead of exiting examine mode.
[RequireComponent(typeof(Rigidbody))]
public class GrabbablePainting : MonoBehaviour
{
    [Header("VR")]
    [SerializeField] float vrGrabRange = 0.30f;

    [Header("Back-face detection")]
    [Tooltip("Local direction that points out of the BACK of the painting.")]
    [SerializeField] Vector3 backNormal   = Vector3.back;
    [SerializeField] float   backDotMin   = 0.5f;

    Rigidbody _rb;
    Camera    _cam;
    TapedKey  _tapedKey;

    // VR state
    OVRInput.Controller _heldBy          = OVRInput.Controller.None;
    Transform           _heldByTransform;
    Vector3             _localGrabOffset;
    Quaternion          _grabRotationOffset;

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
    bool IsHeld => _heldBy != OVRInput.Controller.None;

    void Start()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity  = false;
        _cam     = FindObjectOfType<PlayerLook>()?.cam ?? Camera.main;
        _tapedKey = GetComponentInChildren<TapedKey>(true);
    }

    void Update()
    {
        if (IsVR) HandleVR();
        else      HandleKB();
    }

    void LateUpdate()
    {
        if (!IsHeld) return;
        transform.rotation = _heldByTransform.rotation * _grabRotationOffset;
        transform.position = _heldByTransform.position
            + transform.TransformDirection(_localGrabOffset);
    }

    // ── VR ───────────────────────────────────────────────────────────────────────

    void HandleVR()
    {
        if (IsHeld)
        {
            bool released =
                (_heldBy == OVRInput.Controller.RTouch &&
                 OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) ||
                (_heldBy == OVRInput.Controller.LTouch &&
                 OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch));
            if (released) ReleaseVR();
            return;
        }

        var hand = VRPhysicsHand.Instance;
        bool rNear = hand?.RightHand != null
            && Vector3.Distance(hand.RightHand.position, transform.position) <= vrGrabRange;
        bool lNear = hand?.LeftHand != null
            && Vector3.Distance(hand.LeftHand.position, transform.position) <= vrGrabRange;

        if (rNear || lNear)
            HintManager.Instance?.Show(this, "Grip to grab painting", 2);
        else
            HintManager.Instance?.Hide(this);

        TryVRGrab(OVRInput.Controller.RTouch, hand?.RightHand);
        TryVRGrab(OVRInput.Controller.LTouch, hand?.LeftHand);
    }

    void TryVRGrab(OVRInput.Controller ctrl, Transform hand)
    {
        if (hand == null) return;
        if (!OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, ctrl)) return;
        if (Vector3.Distance(hand.position, transform.position) > vrGrabRange) return;

        _heldBy             = ctrl;
        _heldByTransform    = hand;
        _grabRotationOffset = Quaternion.Inverse(hand.rotation) * transform.rotation;
        _localGrabOffset    = transform.InverseTransformDirection(transform.position - hand.position);
        HintManager.Instance?.Show(this, "Release to place", 2);
    }

    void ReleaseVR()
    {
        _heldBy          = OVRInput.Controller.None;
        _heldByTransform = null;
        HintManager.Instance?.Hide(this);
    }

    // ── Keyboard (ObjectInspector examine mode) ──────────────────────────────────

    void HandleKB()
    {
        if (_tapedKey == null || _tapedKey.IsCollected) { HintManager.Instance?.Hide(this); return; }

        // Only active while ObjectInspector is examining this painting
        var inspectable = GetComponentInChildren<InspectableObject>(true);
        if (inspectable == null || inspectable != ObjectInspector.CurrentlyExamined)
        {
            HintManager.Instance?.Hide(this);   // clear the prompt when we stop examining
            return;
        }

        if (BackFacingCamera())
        {
            HintManager.Instance?.Show(this, "[E] Take the key", 3);
            if (Input.GetKeyDown(KeyCode.E))
            {
                HintManager.Instance?.Hide(this);
                _tapedKey.Collect();
            }
        }
        else
        {
            HintManager.Instance?.Hide(this);   // not facing the back → no prompt
        }
    }

    bool BackFacingCamera()
    {
        if (_cam == null) return false;
        Vector3 worldBack = transform.TransformDirection(backNormal).normalized;
        Vector3 toCamera  = (_cam.transform.position - transform.position).normalized;
        return Vector3.Dot(worldBack, toCamera) >= backDotMin;
    }
}
