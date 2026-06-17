using UnityEngine;

/// General-purpose VR hand state provider. Attach to the player.
/// Any interactable can read smoothed hand velocities and trigger haptics
/// via VRPhysicsHand.Instance without duplicating OVR calls or hand-finding logic.
public class VRPhysicsHand : MonoBehaviour
{
    public static VRPhysicsHand Instance { get; private set; }

    [SerializeField] int velocitySamples = 5;

    public Transform RightHand     { get; private set; }
    public Transform LeftHand      { get; private set; }
    public Vector3   RightVelocity { get; private set; }
    public Vector3   LeftVelocity  { get; private set; }

    public bool IsVR => UnityEngine.XR.XRSettings.enabled;

    Vector3[] rightBuf;
    Vector3[] leftBuf;
    int       bufIndex;

    Vector3 lastRightPos;
    Vector3 lastLeftPos;
    bool    hasLastPos;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        rightBuf = new Vector3[velocitySamples];
        leftBuf  = new Vector3[velocitySamples];
    }

    void Start()
    {
        var rh = GameObject.Find("RightHandAnchor");
        var lh = GameObject.Find("LeftHandAnchor");
        if (rh != null) RightHand = rh.transform;
        if (lh != null) LeftHand  = lh.transform;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // World-space velocity from the actual anchor motion. This is directionally
        // correct (OVRInput.GetLocalControllerVelocity is in tracking-space, which is
        // wrong once the rig is rotated) and is the true hand speed for throwing.
        if (RightHand != null && LeftHand != null)
        {
            if (hasLastPos)
            {
                rightBuf[bufIndex] = (RightHand.position - lastRightPos) / dt;
                leftBuf [bufIndex] = (LeftHand.position  - lastLeftPos)  / dt;
                bufIndex = (bufIndex + 1) % velocitySamples;
            }
            lastRightPos = RightHand.position;
            lastLeftPos  = LeftHand.position;
            hasLastPos   = true;
        }

        RightVelocity = Average(rightBuf);
        LeftVelocity  = Average(leftBuf);
    }

    static Vector3 Average(Vector3[] buf)
    {
        var sum = Vector3.zero;
        foreach (var v in buf) sum += v;
        return sum / buf.Length;
    }

    /// Call each frame the controller should vibrate. Stops automatically when you stop calling it.
    public void SetHaptic(OVRInput.Controller controller, float frequency, float amplitude)
        => OVRInput.SetControllerVibration(frequency, amplitude, controller);

    public void StopHaptic(OVRInput.Controller controller)
        => OVRInput.SetControllerVibration(0f, 0f, controller);
}
