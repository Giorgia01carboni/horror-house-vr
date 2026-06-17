using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableChessPiece : MonoBehaviour
{
    [SerializeField] float grabRange = 0.25f;
    [SerializeField] float throwMultiplier = 2.5f;

    [Header("Grip pose (mirrors the pencil — tweak to fit the piece in hand)")]
    [Tooltip("Offset of the piece relative to the controller while held. " +
             "A small forward/up value keeps the base visible so you can aim it at the king.")]
    [SerializeField] Vector3 gripLocalPosition = new Vector3(0f, 0f, 0.03f);
    [SerializeField] Vector3 gripLocalRotation = Vector3.zero;

    Rigidbody _rb;
    OVRInput.Controller _grabbingController = OVRInput.Controller.None;
    bool _kbHeld;
    Camera _cam;

    // Every chess piece registers here so a grab can pick ONLY the nearest piece to the
    // hand. Without this, a generous grab range lets one grip snatch a whole fistful of
    // the clustered pieces.
    static readonly System.Collections.Generic.List<GrabbableChessPiece> _all = new();
    void OnEnable()  { if (!_all.Contains(this)) _all.Add(this); }
    void OnDisable() { _all.Remove(this); }

    public bool IsHeld => _grabbingController != OVRInput.Controller.None || _kbHeld;
    bool IsVR   => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
                || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _cam = Camera.main;
    }

    void Update()
    {
        if (IsVR) HandleVR();
        else      HandleKeyboard();
    }

    void HandleVR()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null) return;

        if (!IsHeld)
        {
            bool rClose = hand.RightHand != null && Vector3.Distance(hand.RightHand.position, transform.position) <= grabRange;
            bool lClose = hand.LeftHand  != null && Vector3.Distance(hand.LeftHand.position,  transform.position) <= grabRange;

            if (rClose || lClose) ShowHint("Grip to grab piece");
            else                  HideHint();

            TryGrab(OVRInput.Controller.RTouch, hand.RightHand);
            TryGrab(OVRInput.Controller.LTouch, hand.LeftHand);
        }
        else
        {
            HideHint();
            bool rightReleased = _grabbingController == OVRInput.Controller.RTouch
                && OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            bool leftReleased  = _grabbingController == OVRInput.Controller.LTouch
                && OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);

            if (rightReleased || leftReleased) ReleaseVR();
        }
    }

    void TryGrab(OVRInput.Controller ctrl, Transform handTransform)
    {
        if (handTransform == null) return;
        if (!OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, ctrl)) return;
        float myDist = Vector3.Distance(handTransform.position, transform.position);
        if (myDist > grabRange) return;
        // Only grab the nearest piece in range — prevents all clustered pieces from
        // grabbing simultaneously when grabRange is generous.
        foreach (var other in _all)
        {
            if (other == this || other.IsHeld) continue;
            float d = Vector3.Distance(handTransform.position, other.transform.position);
            if (d <= grabRange && d < myDist) return;
        }
        GrabVR(ctrl, handTransform);
    }

    void GrabVR(OVRInput.Controller ctrl, Transform handTransform)
    {
        _grabbingController = ctrl;
        _rb.isKinematic = true;
        // Discrete avoids the kinematic piece generating contacts with every adjacent
        // piece as it moves across the crowded board (the "snowplow" effect).
        _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        transform.SetParent(handTransform);
        transform.localPosition = gripLocalPosition;
        transform.localRotation = Quaternion.Euler(gripLocalRotation);
    }

    void ReleaseVR()
    {
        var hand = VRPhysicsHand.Instance;
        Vector3 vel = _grabbingController == OVRInput.Controller.RTouch
            ? hand.RightVelocity : hand.LeftVelocity;

        _grabbingController = OVRInput.Controller.None;
        transform.SetParent(null);
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.velocity = vel * throwMultiplier;
        _rb.angularVelocity = Vector3.zero;
        HideHint();
    }

    void HandleKeyboard()
    {
        if (VRRevolver.GunIsHeld) { HideHint(); return; }

        // Piece manipulation only available while in chess inspect mode
        if (!ChessboardInteraction.IsInspecting)
        {
            HideHint();
            return;
        }

        if (!_kbHeld)
        {
            if (_cam == null) return;
            if (Vector3.Distance(_cam.transform.position, transform.position) <= 2f)
            {
                ShowHint("[E] Pick up piece");
                if (Input.GetKeyDown(KeyCode.E)) GrabKeyboard();
            }
            else
            {
                HideHint();
            }
        }
        else
        {
            ShowHint("[E] Put down piece");
            if (Input.GetKeyDown(KeyCode.E)) DropKeyboard();
        }
    }

    void GrabKeyboard()
    {
        _kbHeld         = true;
        _rb.isKinematic = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        transform.SetParent(_cam.transform);
        transform.localPosition = new Vector3(0.15f, -0.15f, 0.4f);
        transform.localRotation = Quaternion.identity;
    }

    void DropKeyboard()
    {
        _kbHeld = false;
        transform.SetParent(null);
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        HideHint();
    }

    void ShowHint(string msg)
    {
        if (HintManager.Instance != null) HintManager.Instance.Show(this, msg, 1);
    }

    void HideHint()
    {
        if (HintManager.Instance != null) HintManager.Instance.Hide(this);
    }
}
