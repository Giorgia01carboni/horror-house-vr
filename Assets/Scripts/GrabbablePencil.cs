using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbablePencil : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Empty child GameObject placed at the writing tip of the pencil mesh.")]
    [SerializeField] Transform tipTransform;
    [SerializeField] Camera    playerCamera;
    [SerializeField] float grabRange        = 0.25f;
    [SerializeField] float kbInteractRange  = 2f;

    [Header("Grip pose (adjust to fit your pencil mesh)")]
    [SerializeField] Vector3 gripLocalPosition = new Vector3(0f, 0f, -0.06f);
    [SerializeField] Vector3 gripLocalRotation = new Vector3(75f, 0f, 0f);

    Rigidbody         _rb;
    OVRInput.Controller _grabbingController = OVRInput.Controller.None;

    public bool      IsHeld => _grabbingController != OVRInput.Controller.None
                            || _kbHeld;
    public Transform Tip    => tipTransform;

    bool _kbHeld;
    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        if (IsVR)
            HandleVR();
        else
            HandleKeyboard();
    }

    void HandleVR()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null) return;

        if (!IsHeld)
        {
            bool rightClose = hand.RightHand != null
                && Vector3.Distance(hand.RightHand.position, transform.position) <= grabRange;
            bool leftClose  = hand.LeftHand  != null
                && Vector3.Distance(hand.LeftHand.position,  transform.position) <= grabRange;

            if ((rightClose || leftClose) && LookingAt())
                ShowHint("Grip to grab pencil");
            else
                HideHint();

            TryVRGrab(OVRInput.Controller.RTouch, hand.RightHand);
            TryVRGrab(OVRInput.Controller.LTouch, hand.LeftHand);
        }
        else
        {
            HideHint();

            bool rightReleased = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            bool leftReleased  = OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);

            if ((_grabbingController == OVRInput.Controller.RTouch && rightReleased)
             || (_grabbingController == OVRInput.Controller.LTouch && leftReleased))
                Drop();
        }
    }

    void TryVRGrab(OVRInput.Controller controller, Transform handTransform)
    {
        if (handTransform == null) return;
        if (!OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, controller)) return;
        if (Vector3.Distance(handTransform.position, transform.position) > grabRange) return;

        GrabVR(handTransform, controller);
    }

    void GrabVR(Transform handTransform, OVRInput.Controller controller)
    {
        _grabbingController = controller;
        _rb.isKinematic     = true;
        transform.SetParent(handTransform);
        transform.localPosition = gripLocalPosition;
        transform.localRotation = Quaternion.Euler(gripLocalRotation);
    }

    void HandleKeyboard()
    {
        if (VRRevolver.GunIsHeld) { HideHint(); return; }

        if (!_kbHeld)
        {
            if (playerCamera == null) return;

            bool nearby = Vector3.Distance(playerCamera.transform.position, transform.position)
                          <= kbInteractRange;

            if (nearby && LookingAt())
            {
                ShowHint("[E] Pick up pencil");
                if (Input.GetKeyDown(KeyCode.E))
                    GrabKeyboard(playerCamera.transform);
            }
            else
            {
                HideHint();
            }
        }
        else
        {
            ShowHint("[E] Put down pencil");
            if (Input.GetKeyDown(KeyCode.E))
                Drop();
        }
    }

    void GrabKeyboard(Transform camTransform)
    {
        _kbHeld         = true;
        _rb.isKinematic = true;
        transform.SetParent(playerCamera.transform);
        transform.localPosition = gripLocalPosition + new Vector3(0.1f, -0.1f, 0.3f);
        transform.localRotation = Quaternion.Euler(gripLocalRotation);
    }

    void Drop()
    {
        _grabbingController = OVRInput.Controller.None;
        _kbHeld             = false;
        transform.SetParent(null);
        _rb.isKinematic = false;
        HideHint();
    }

    bool LookingAt()
    {
        if (playerCamera == null) return false;
        Vector3 dir = (transform.position - playerCamera.transform.position).normalized;
        return Vector3.Dot(dir, playerCamera.transform.forward) > 0.97f;
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
