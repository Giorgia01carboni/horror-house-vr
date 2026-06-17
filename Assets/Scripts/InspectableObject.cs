using UnityEngine;
using UnityEngine.Events;

public class InspectableObject : MonoBehaviour
{
    [Tooltip("Child transform whose position aligns to the screen center during inspection. Leave null to use the object's pivot.")]
    public Transform inspectAnchor;

    [Tooltip("Override inspection distance (metres from camera). 0 = auto-fit based on bounds.")]
    public float inspectDistance = 0f;

    [Tooltip("Initial local rotation when entering inspect mode. Default (0,0,0) = identity. Use this to make the object face the camera correctly on entry.")]
    public Vector3 inspectStartEuler = Vector3.zero;

    [Tooltip("If true, pressing [E] fires onInteract in-place instead of entering the examine mode.")]
    public bool interactInPlace = false;

    [Tooltip("Fired when the player presses [E] while looking at this object and interactInPlace is true.")]
    public UnityEvent onInteract;
}
