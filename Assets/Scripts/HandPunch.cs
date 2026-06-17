using UnityEngine;

public class HandPunch : MonoBehaviour
{
    public enum Hand { Right, Left }

    [SerializeField] private Hand hand;
    [SerializeField] private float velocityThreshold = 2.5f;

    private void OnTriggerEnter(Collider other)
    {
        OVRInput.Controller controller = hand == Hand.Right
            ? OVRInput.Controller.RTouch
            : OVRInput.Controller.LTouch;

        float speed = OVRInput.GetLocalControllerVelocity(controller).magnitude;
        if (speed < velocityThreshold) return;

        other.GetComponentInParent<BreakableWindow>()?.TriggerBreak();
    }
}
