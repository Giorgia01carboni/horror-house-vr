using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMotor motor;
    private PlayerLook look;

    bool IsVR => OVRPlugin.initialized;

    void Awake()
    {
        EnsureInit();
    }

    // Initialise the input wrapper exactly once. Called from both Awake and
    // OnEnable because OnEnable can fire before Awake (e.g. when PauseMenu
    // toggles this component's `enabled`), and calling onFoot.Enable() on an
    // uninitialised struct throws a NullReferenceException.
    void EnsureInit()
    {
        if (playerInput != null) return;
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();
        onFoot.Jump.performed += ctx => motor.Jump();
        onFoot.Sprint.performed += ctx => motor.Sprint();
    }

    void Update()
    {
        if (IsVR && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
            motor.Jump();
    }

    void FixedUpdate()
    {
        if (IsVR)
            motor.ProcessMove(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch));
        else
            motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>());
    }

    private void LateUpdate()
    {
        if (!IsVR)
            look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }

    private void OnEnable()
    {
        EnsureInit();
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}
