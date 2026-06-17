using UnityEngine;

// Attach to Key3 (child of the painting).
// KB:  collected via GrabbablePainting when back of painting faces camera + E.
// VR:  grip near the key → tape rips, key collected.
// On collection: plays tape rip sound, key disappears, brief "Key collected" hint.
[RequireComponent(typeof(Rigidbody))]
public class TapedKey : MonoBehaviour
{
    [SerializeField] AudioClip tapeRipClip;
    [SerializeField] float vrGrabRange = 0.12f;

    Rigidbody   _rb;
    AudioSource _audio;
    bool        _collected;

    public bool IsCollected => _collected;
    public static bool KeyCollected { get; private set; }

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    void Start()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity  = false;

        // Own AudioSource instead of PlayClipAtPoint: PlayClipAtPoint auto-destroys its temp
        // object after clip.length * Time.timeScale, which is 0 during KB examine mode
        // (timeScale == 0) — so the rip was being cut off before it sounded. 2D + ignore pause
        // guarantees it's audible in both modes.
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake         = false;
        _audio.spatialBlend        = 0f;
        _audio.ignoreListenerPause = true;

        BuildTapeStrips();
    }

    void Update()
    {
        if (_collected || !IsVR) return;

        var hand = VRPhysicsHand.Instance;
        if (hand == null) return;

        bool rClose = hand.RightHand != null
            && Vector3.Distance(hand.RightHand.position, transform.position) <= vrGrabRange;
        bool lClose = hand.LeftHand != null
            && Vector3.Distance(hand.LeftHand.position, transform.position) <= vrGrabRange;

        if (!rClose && !lClose) { HideHint(); return; }

        ShowHint("Grip to take key");

        if (rClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            Collect();
        else if (lClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
            Collect();
    }

    public void Collect()
    {
        if (_collected) return;
        _collected   = true;
        KeyCollected = true;
        HideHint();

        if (tapeRipClip != null)
            _audio.PlayOneShot(tapeRipClip);

        HintManager.Instance?.Show(this, "<color=#D4AF37>Key collected.</color>", 4);

        var runner2 = new GameObject("KeyDialogueRunner").AddComponent<KeyHintRunner>();
        runner2.Init(this, 1.2f, "Mmm... Are those <color=#8B0000>car</color> keys?", 3);

        // Hide visually — don't Destroy yet because ObjectInspector's layer-restoration
        // would throw MissingReferenceException on a destroyed child object.
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;

        // Safe to fully destroy a few seconds later once examine mode has exited
        Destroy(gameObject, 4f);

        var runner = new GameObject("KeyHintRunner").AddComponent<KeyHintRunner>();
        runner.Init(this, 2.5f);
    }

    void BuildTapeStrips()
    {
        float s = transform.lossyScale.x; // 0.19 — divide world target by this
        CreateStrip(s, 0f);
        CreateStrip(s, 55f);
    }

    void CreateStrip(float parentScale, float zAngle)
    {
        // World target: 10mm wide, 38mm long, 2mm thick — crossing the key in an X
        float w      = 0.010f / parentScale;
        float h      = 0.038f / parentScale;
        float d      = 0.002f / parentScale;
        float offset = 0.006f / parentScale; // just proud of the back face

        var go  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "TapeStrip";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.back * offset;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        go.transform.localScale    = new Vector3(w, h, d);

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.material.color    = new Color(0.92f, 0.88f, 0.72f, 0.75f);
    }

    void ShowHint(string msg) => HintManager.Instance?.Show(this, msg, 2);
    void HideHint()           => HintManager.Instance?.Hide(this);
}

// Helper: after a delay either shows a new hint or hides the existing one
public class KeyHintRunner : MonoBehaviour
{
    TapedKey _key;
    float    _delay;
    string   _showMsg;
    int      _priority;
    float    _showDuration;

    // Hide after delay
    public void Init(TapedKey key, float delay)
    { _key = key; _delay = delay; _showMsg = null; }

    // Show a message after delay, then hide it after showDuration
    public void Init(TapedKey key, float delay, string msg, int priority, float showDuration = 3.5f)
    { _key = key; _delay = delay; _showMsg = msg; _priority = priority; _showDuration = showDuration; }

    void Update()
    {
        _delay -= Time.deltaTime;
        if (_delay > 0f) return;

        if (_showMsg != null)
        {
            HintManager.Instance?.Show(_key, _showMsg, _priority);
            _showMsg      = null;
            _delay        = _showDuration; // reuse delay for hide
        }
        else
        {
            HintManager.Instance?.Hide(_key);
            Destroy(gameObject);
        }
    }
}
