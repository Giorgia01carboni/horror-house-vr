using System.Collections;
using UnityEngine;

public class doorOpener : MonoBehaviour
{
    [Header("Crack open (enigma solved)")]
    [SerializeField] float ajarAngle    = -28f;   // local-Y degrees; flip sign if wrong direction
    [SerializeField] float ajarDuration = 1.5f;

    [Header("Full open threshold")]
    [SerializeField] float fullAngle    = -95f;   // absolute local-Y when considered fully open

    [Header("VR push feel")]
    [Tooltip("How much hand speed (m/s) translates into door angular speed (deg/s).")]
    [SerializeField] float pushSensitivity = 180f;
    [Tooltip("How quickly the door slows when the hand stops. Higher = stops faster.")]
    [SerializeField] float angularDrag     = 5f;

    [Header("KB / Mouse")]
    [SerializeField] float     kbOpenDuration = 1.0f;
    [SerializeField] AudioClip lockedSound;          // Assets/Sounds/whatdoingdoor-93604.mp3
    [SerializeField] AudioClip crackSound;           // Assets/Sounds/door-squeak-(open)-#1-made-with-Voicemod.mp3

    public enum DoorState { Locked, Ajar, Open }
    public DoorState State { get; private set; } = DoorState.Locked;

    BoxCollider _zone;       // the Cube trigger child
    bool        _playerNear;
    bool        _lockedSoundPlaying;

    // Physics state (VR push)
    float _currentAngle;
    float _angularVel;
    bool  _physicsActive;    // true once the player first contacts the door

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    // The opening always goes from ajarAngle toward fullAngle
    float _openSign; // sign that points from ajar toward full

    void Start()
    {
        _zone     = GetComponentInChildren<BoxCollider>();
        _openSign = Mathf.Sign(fullAngle - ajarAngle); // -1 in our default case
    }

    // ── Called by ChessEnigma ─────────────────────────────────────────────────

    public void CrackOpen()
    {
        if (State != DoorState.Locked) return;
        State         = DoorState.Ajar;
        _currentAngle = 0f;
        if (crackSound != null) StartCoroutine(PlayCrackSound());
        StartCoroutine(CrackCoroutine());
    }

    IEnumerator PlayCrackSound()
    {
        var src          = gameObject.AddComponent<AudioSource>();
        src.clip         = crackSound;
        src.spatialBlend = 1f;
        src.Play();
        yield return new WaitForSeconds(1f);
        src.Stop();
        Destroy(src);
    }

    IEnumerator CrackCoroutine()
    {
        Quaternion startRot = transform.localRotation;
        Quaternion endRot   = Quaternion.Euler(0f, ajarAngle, 0f);
        float elapsed = 0f;
        while (elapsed < ajarDuration && !_physicsActive)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ajarDuration));
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            _currentAngle           = Mathf.Lerp(0f, ajarAngle, t);
            yield return null;
        }
        if (!_physicsActive)
        {
            transform.localRotation = endRot;
            _currentAngle           = ajarAngle;
        }
    }

    // ── Called by playerDetector ──────────────────────────────────────────────

    public void SetPlayerNear(bool near)
    {
        _playerNear = near;
        RefreshPrompt();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (State == DoorState.Open) return;

        if (IsVR)
        {
            if (State == DoorState.Ajar) UpdateVRPush();
        }
        else
        {
            if (State == DoorState.Locked) UpdateLocked();
            else if (State == DoorState.Ajar) UpdateKeyboard();
        }
    }

    void UpdateLocked()
    {
        RefreshPrompt();
        if (_playerNear && !_lockedSoundPlaying && Input.GetKeyDown(KeyCode.E))
            StartCoroutine(LockedSequence());
    }

IEnumerator LockedSequence()
    {
        _lockedSoundPlaying = true;
        HintManager.Instance?.Hide(this);

        if (lockedSound != null)
        {
            var src          = gameObject.AddComponent<AudioSource>();
            src.clip         = lockedSound;
            src.spatialBlend = 1f;
            src.Play();
            yield return new WaitForSeconds(3f);
            src.Stop();
            Destroy(src);
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }

        HintManager.Instance?.Show(this, "It's closed.", 3);
        yield return new WaitForSeconds(2.5f);
        HintManager.Instance?.Hide(this);

        _lockedSoundPlaying = false;
    }

    void UpdateVRPush()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null || _zone == null) return;

        // Determine which hand (if any) is touching the door
        Vector3 handVel     = Vector3.zero;
        bool    handOnDoor  = false;

        if (hand.RightHand != null && _zone.bounds.Contains(hand.RightHand.position))
        {
            handVel    = hand.RightVelocity;
            handOnDoor = true;
        }
        else if (hand.LeftHand != null && _zone.bounds.Contains(hand.LeftHand.position))
        {
            handVel    = hand.LeftVelocity;
            handOnDoor = true;
        }

        if (handOnDoor)
        {
            _physicsActive = true;

            // Horizontal hand speed drives the door in the opening direction.
            // The player can only push it open, not push it closed — so we
            // only add velocity when the hand moves in the opening direction.
            float hSpeed = new Vector3(handVel.x, 0f, handVel.z).magnitude;
            if (hSpeed > 0.05f)
                _angularVel += hSpeed * pushSensitivity * _openSign * Time.deltaTime;
        }

        if (!_physicsActive) return;

        // Apply drag — door coasts to a stop when the hand lifts
        _angularVel = Mathf.Lerp(_angularVel, 0f, angularDrag * Time.deltaTime);

        // Advance angle
        _currentAngle += _angularVel * Time.deltaTime;

        // Clamp so the door can only swing between ajar and fully open
        float minA = Mathf.Min(ajarAngle, fullAngle);
        float maxA = Mathf.Max(ajarAngle, fullAngle);
        _currentAngle = Mathf.Clamp(_currentAngle, minA, maxA);

        transform.localRotation = Quaternion.Euler(0f, _currentAngle, 0f);

        // Snap fully open when close enough
        if (Mathf.Abs(_currentAngle - fullAngle) <= 4f)
        {
            transform.localRotation = Quaternion.Euler(0f, fullAngle, 0f);
            State       = DoorState.Open;
            _angularVel = 0f;
        }
    }

    void UpdateKeyboard()
    {
        RefreshPrompt();
        if (_playerNear && Input.GetKeyDown(KeyCode.E))
        {
            State = DoorState.Open;
            HintManager.Instance?.Hide(this);
            StartCoroutine(RotateTo(fullAngle, kbOpenDuration));
        }
    }

void RefreshPrompt()
    {
        if (IsVR || !_playerNear) { HintManager.Instance?.Hide(this); return; }
        if (_lockedSoundPlaying) return; // coroutine owns the hint during this window

        if (State == DoorState.Locked)
            HintManager.Instance?.Show(this, "[E] Open", 2);
        else if (State == DoorState.Ajar)
            HintManager.Instance?.Show(this, "Enter? [E]", 3);
        else
            HintManager.Instance?.Hide(this);
    }

    IEnumerator RotateTo(float targetLocalY, float duration)
    {
        Quaternion startRot = transform.localRotation;
        Quaternion endRot   = Quaternion.Euler(0f, targetLocalY, 0f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.localRotation = endRot;
    }
}
