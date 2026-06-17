using System.Collections;
using UnityEngine;
using TMPro;

public class DoorHandler : MonoBehaviour
{
    public GameObject Instruction;
    public GameObject ThisTrigger;
    public AudioSource DoorOpenSound;
    public bool Action = false;

    [Header("Unlock / Swing")]
    [SerializeField] Transform pivotTransform;
    [SerializeField] float openAngle    = -90f;
    [SerializeField] float openDuration = 1.2f;
    [SerializeField] AudioClip unlockSound;

    [Header("Outside Chase")]
    [SerializeField] EnemyAI outsideEnemy;
    [SerializeField] float outsideChaseDelay = 20f;

    private bool doorAlreadyTried = false;
    private bool playerInZone     = false;
    private string approachHint;
    private bool _isUnlocked      = false;
    private bool _playerIsOutside = false;
    private float _outsideTimer   = 0f;
    private bool _exitDialoguePlayed = false;

    public bool IsUnlocked      => _isUnlocked;
    public bool TriggerDisabled { get; private set; }

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    void Start()
    {
        Instruction = GameObject.FindWithTag("TextMesh");
        approachHint = Instruction?.GetComponent<TextMeshProUGUI>()?.text ?? "[E] Interact";
        Instruction?.SetActive(false);

        if (pivotTransform == null) pivotTransform = transform;
    }

    void OnTriggerEnter(Collider col)
    {
        if (col.tag != "Player") return;
        playerInZone = true;

        if (_isUnlocked)
        {
            // Door is open: player entering from outside means going back inside
            PlayerGoesInside();
            return;
        }

        if (!doorAlreadyTried)
        {
            if (IsVR)
            {
                // VR has no [E] key and the approachHint is keyboard text; play the
                // "door is locked / another way" narrative automatically on approach.
                doorAlreadyTried = true;
                StartCoroutine(PlayLockedSequence());
            }
            else
            {
                HintManager.Instance?.Show(this, approachHint, 2);
                Action = true;
            }
        }
    }

    void OnTriggerExit(Collider col)
    {
        if (col.tag != "Player") return;
        playerInZone = false;

        if (_isUnlocked)
        {
            // Door is open: player leaving the outside zone means going back outside
            PlayerGoesOutside();
            return;
        }

        HintManager.Instance?.Hide(this);
        Action = false;
    }

    void Update()
    {
        if (!_isUnlocked)
        {
            if (!VRRevolver.GunIsHeld && Input.GetKeyDown(KeyCode.E) && Action)
            {
                Action = false;
                doorAlreadyTried = true;
                HintManager.Instance?.Hide(this);
                StartCoroutine(PlayLockedSequence());
            }
            return;
        }

        if (_playerIsOutside)
        {
            _outsideTimer += Time.deltaTime;
            if (_outsideTimer >= outsideChaseDelay)
                outsideEnemy?.StartChase();
        }
    }

    // Called by DoorInsideHandle when player presses E to exit
    public void UnlockFromInside()
    {
        PlayerGoesOutside();
        Unlock();
    }

    public void PlayerGoesOutside()
    {
        if (_playerIsOutside) return;
        _playerIsOutside = true;
        FindObjectOfType<PlayerAudio>()?.SetInsideHouse(false);
        HorrorFog.Instance?.SetInsideHouse(false);
        AmbientSoundManager.Instance?.SetInsideHouse(false);

        if (!_exitDialoguePlayed)
        {
            _exitDialoguePlayed = true;
            StartCoroutine(ExitDialogue());
        }
    }

    IEnumerator ExitDialogue()
    {
        yield return new WaitForSeconds(0.8f);
        HintManager.Instance?.Show(this, "<color=#CC0000>Alright... Let's get out of this place.</color>", 5);
        yield return new WaitForSeconds(4f);
        HintManager.Instance?.Hide(this);
    }

    public void PlayerGoesInside()
    {
        if (!_playerIsOutside) return;
        _playerIsOutside = false;
        _outsideTimer    = 0f;
        outsideEnemy?.StopChase();
        FindObjectOfType<PlayerAudio>()?.SetInsideHouse(true);
        HorrorFog.Instance?.SetInsideHouse(true);
        AmbientSoundManager.Instance?.SetInsideHouse(true);
    }

    public void DisableTrigger()
    {
        TriggerDisabled = true;
        Action = false;
        HintManager.Instance?.Hide(this);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    public void Unlock()
    {
        if (_isUnlocked) return;
        _isUnlocked = true;
        Action = false;
        HintManager.Instance?.Hide(this);
        StartCoroutine(OpenDoor());
    }

    IEnumerator OpenDoor()
    {
        if (unlockSound != null)
        {
            if (DoorOpenSound != null) { DoorOpenSound.clip = unlockSound; DoorOpenSound.Play(); }
            else AudioSource.PlayClipAtPoint(unlockSound, pivotTransform.position);
        }

        Quaternion startRot = pivotTransform.localRotation;
        Quaternion endRot   = startRot * Quaternion.Euler(0f, openAngle, 0f);
        float t = 0f;

        while (t < openDuration)
        {
            t += Time.deltaTime;
            pivotTransform.localRotation = Quaternion.Slerp(startRot, endRot, t / openDuration);
            yield return null;
        }
        pivotTransform.localRotation = endRot;

        // Re-enable the trigger so re-entry can be detected.
        // Wait long enough for the player to fully exit the zone first.
        yield return new WaitForSeconds(3f);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    IEnumerator PlayLockedSequence()
    {
        // Show the line right away (no longer waiting for the door rattle to finish first),
        // and keep it on screen for a shorter beat.
        DoorOpenSound?.Play();
        HintManager.Instance?.Show(this,
            "The door is locked. Maybe there's <color=#8B0000>another way</color> to enter here.", 3);
        yield return new WaitForSeconds(2f);
        HintManager.Instance?.Hide(this);
        if (DoorOpenSound != null && DoorOpenSound.isPlaying) DoorOpenSound.Stop();
    }
}
