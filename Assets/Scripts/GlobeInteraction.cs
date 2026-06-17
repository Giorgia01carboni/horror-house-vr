using System.Collections;
using UnityEngine;

public class GlobeInteraction : MonoBehaviour
{
    [SerializeField] Transform globeSphere;
    [SerializeField] float narrativeRange  = 2.5f;
    [SerializeField] float spinForce       = 2f;
    [SerializeField] float vrHandRange     = 0.18f;
    [SerializeField] float maxAngularSpeed    = 10f;
    [SerializeField] float keyboardSpinForce  = 6f;

    Rigidbody globeRb;
    Camera    mainCam;
    float     globeRadius;
    bool      narrativeShown;
    Vector3   pendingTorque;

    void Start()
    {
        if (globeSphere == null)
            globeSphere = transform.Find("Globe");

        if (globeSphere != null)
        {
            globeRb = globeSphere.GetComponent<Rigidbody>();
            CacheGlobeRadius();
        }

        // Mirror ObjectInspector's camera lookup — Camera.main is unreliable in OVR
        var playerLook = GameObject.FindWithTag("Player")?.GetComponent<PlayerLook>();
        mainCam = playerLook?.cam ?? Camera.main;

        // Subscribe to the keyboard interact event on the Globe child
        var inspectable = globeSphere?.GetComponent<InspectableObject>();
        if (inspectable != null) inspectable.onInteract.AddListener(SpinKeyboard);
    }

    void SpinKeyboard()
    {
        if (globeRb == null || (VRPhysicsHand.Instance != null && VRPhysicsHand.Instance.IsVR)) return;

        // Spin around the horizontal axis perpendicular to the camera — feels like a natural push
        Vector3 spinAxis = Vector3.Cross(Vector3.up, mainCam != null ? mainCam.transform.forward : Vector3.forward);
        if (spinAxis.sqrMagnitude < 0.01f) spinAxis = Vector3.right;
        else spinAxis.Normalize();

        globeRb.AddTorque(spinAxis * keyboardSpinForce, ForceMode.Impulse);
    }

    void CacheGlobeRadius()
    {
        globeRadius = 0.1f;
        foreach (var r in globeSphere.GetComponentsInChildren<Renderer>())
        {
            Vector3 e = r.bounds.extents;
            float   s = Mathf.Max(e.x, e.y, e.z);
            if (s > globeRadius) globeRadius = s;
        }
    }

    void Update()
    {
        if (!narrativeShown)
            CheckNarrative();

        var hands = VRPhysicsHand.Instance;
        if (hands == null || !hands.IsVR || globeRb == null) return;

        bool rightContact = ProcessHand(hands.RightHand, hands.RightVelocity, OVRInput.Controller.RTouch);
        bool leftContact  = ProcessHand(hands.LeftHand,  hands.LeftVelocity,  OVRInput.Controller.LTouch);

        if (!rightContact) hands.StopHaptic(OVRInput.Controller.RTouch);
        if (!leftContact)  hands.StopHaptic(OVRInput.Controller.LTouch);
    }

    void FixedUpdate()
    {
        if (pendingTorque.sqrMagnitude > 0f)
        {
            globeRb.AddTorque(pendingTorque, ForceMode.Force);
            pendingTorque = Vector3.zero;
        }

        // Cap spin speed — real globes have terminal velocity from air resistance
        float speed = globeRb.angularVelocity.magnitude;
        if (speed > maxAngularSpeed)
            globeRb.angularVelocity = globeRb.angularVelocity.normalized * maxAngularSpeed;
    }

    bool ProcessHand(Transform hand, Vector3 vel, OVRInput.Controller controller)
    {
        if (hand == null) return false;

        Vector3 toHand = hand.position - globeSphere.position;
        float   dist   = toHand.magnitude;

        if (dist < globeRadius - vrHandRange || dist > globeRadius + vrHandRange) return false;

        Vector3 contactDir = toHand.normalized;

        // Strip the radial (push/pull) component from hand velocity — only tangential motion spins
        Vector3 handTangential = vel - Vector3.Dot(vel, contactDir) * contactDir;

        // Globe surface velocity at the contact point: v = ω × r
        Vector3 surfaceTangential = Vector3.Cross(globeRb.angularVelocity, contactDir * globeRadius);

        // Slip velocity: hand relative to globe surface.
        // Positive → hand leads surface → spins globe up.
        // Negative → surface leads hand → friction brakes the globe.
        // One formula handles both spin-up and braking naturally.
        Vector3 slip = handTangential - surfaceTangential;
        pendingTorque += Vector3.Cross(contactDir, slip) * spinForce;

        // Haptics: subtle base pulse + intensity that grows with spin speed
        float spinFraction = Mathf.Clamp01(globeRb.angularVelocity.magnitude / maxAngularSpeed);
        VRPhysicsHand.Instance.SetHaptic(controller, 0.3f, 0.1f + spinFraction * 0.25f);

        return true;
    }

    void CheckNarrative()
    {
        if (mainCam == null || globeSphere == null) return;

        float dist = Vector3.Distance(mainCam.transform.position, globeSphere.position);
        if (dist > narrativeRange) return;

        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, narrativeRange)) return;
        // Hit anything in the Antique Globe hierarchy (stand, sphere, etc.)
        if (!hit.transform.IsChildOf(transform) && hit.transform != transform) return;

        narrativeShown = true;
        StartCoroutine(ShowNarrative());
    }

    IEnumerator ShowNarrative()
    {
        HintManager.Instance?.Show(this,
            "I guess this guy was looking for inspiration of where to flee.", 3);
        yield return new WaitForSeconds(3.5f);
        HintManager.Instance?.Hide(this);
    }
}
