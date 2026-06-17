using UnityEngine;

// Attach to an empty child GameObject placed at the inside handle position on Doors1.
// Set the 'door' reference to the DoorHandler on the DoorTrigger.
// ChessEnigma.DisableTrigger() must be called first so the outside trigger doesn't interfere.
public class DoorInsideHandle : MonoBehaviour
{
    [SerializeField] DoorHandler door;
    [SerializeField] float vrHandleRange = 0.18f;
    [SerializeField] float kbRange       = 1.8f;

    Camera _cam;

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    void Start()
    {
        _cam = FindObjectOfType<PlayerLook>()?.cam ?? Camera.main;
    }

    void Update()
    {
        if (door == null) { HideHint(); return; }
        if (!door.TriggerDisabled || door.IsUnlocked) { HideHint(); return; }

        if (IsVR) HandleVR();
        else      HandleKeyboard();
    }

    void HandleVR()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null) { HideHint(); return; }

        bool rClose = hand.RightHand != null
            && Vector3.Distance(hand.RightHand.position, transform.position) <= vrHandleRange;
        bool lClose = hand.LeftHand != null
            && Vector3.Distance(hand.LeftHand.position, transform.position) <= vrHandleRange;

        if (!rClose && !lClose) { HideHint(); return; }

        ShowHint("Grip to unlock door");

        if (rClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        { HideHint(); door.UnlockFromInside(); }
        else if (lClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
        { HideHint(); door.UnlockFromInside(); }
    }

    void HandleKeyboard()
    {
        if (_cam == null) { HideHint(); return; }

        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > kbRange) { HideHint(); return; }

        ShowHint("[E] Unlock door");

        if (Input.GetKeyDown(KeyCode.E))
        { HideHint(); door.UnlockFromInside(); }
    }

    void ShowHint(string msg) => HintManager.Instance?.Show(this, msg, 2);
    void HideHint()           => HintManager.Instance?.Hide(this);
}
