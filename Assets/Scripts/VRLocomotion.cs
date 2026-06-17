using UnityEngine;

/// VR locomotion extras for the OVRPlayerController-driven player (VRPlayer):
///  - SPRINT: hold the configured button (default = left thumbstick click / L3) while
///    pushing the stick to run. Scales OVRPlayerController's move speed.
///  - FOOTSTEPS: re-drives the existing PlayerAudio (which used to be triggered by
///    Man_03's animation events). Plays walk/run footsteps on grass/wood, stopping when
///    idle or airborne. PlayerAudio handles the grass/wood choice (inside/outside flag),
///    so the existing Door/Window SetInsideHouse calls keep working once PlayerAudio
///    lives on the active player.
///
/// Setup (in the Inspector, on VRPlayer):
///   1. Add an Audio Source (no clip needed; PlayerAudio drives it).
///   2. Add the Player Audio component and assign its two footstep clips
///      (tip: copy the component off Man_03 to keep the clip references).
///   3. Add this VRLocomotion component.
/// Keyboard / non-VR is untouched.
[DefaultExecutionOrder(100)]
public class VRLocomotion : MonoBehaviour
{
    [Header("Sprint")]
    [Tooltip("How much faster running is than walking (1 = no change).")]
    [SerializeField] float sprintMultiplier = 1.9f;
    [Tooltip("Hold this on the LEFT controller to run. Default = thumbstick click (L3).")]
    [SerializeField] OVRInput.Button sprintButton = OVRInput.Button.PrimaryThumbstick;
    [Tooltip("Left-stick push past this counts as 'moving'.")]
    [SerializeField] float moveDeadzone = 0.2f;

    OVRPlayerController _pc;
    CharacterController _cc;
    PlayerAudio _audio;

    bool IsVR => UnityEngine.XR.XRSettings.enabled;

    void Start()
    {
        _pc    = GetComponent<OVRPlayerController>();
        _cc    = GetComponent<CharacterController>();
        _audio = GetComponent<PlayerAudio>();
        if (_audio == null) _audio = FindObjectOfType<PlayerAudio>();
    }

    void Update()
    {
        if (!IsVR) return;

        Vector2 stick   = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        bool    moving  = stick.magnitude > moveDeadzone;
        bool    sprinting = moving && OVRInput.Get(sprintButton, OVRInput.Controller.LTouch);

        if (_pc != null)
            _pc.SetMoveScaleMultiplier(sprinting ? sprintMultiplier : 1f);

        if (_audio != null)
        {
            _audio.SetGrounded(_cc == null || _cc.isGrounded);
            _audio.UpdateMovement(moving, sprinting);
        }
    }
}
