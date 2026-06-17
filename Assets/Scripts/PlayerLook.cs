using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Camera cam;
    private float xRotation = 0f;

    public float xSensitivity = 30f;
    public float ySensitivity = 30f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public bool LookEnabled = true;

    public void ProcessLook(Vector2 input)
    {
        if (!LookEnabled) return;
        float mouseX = input.x;
        float mouseY = input.y;
        xRotation -= mouseY * ySensitivity * 0.01f;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * mouseX * xSensitivity * 0.01f);
    }
}
