using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabbableRock : MonoBehaviour
{
    Rigidbody rb;
    bool isHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public bool IsHeld => isHeld;

    public void Grab(Transform holdPoint)
    {
        if (isHeld) return;
        isHeld = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.isKinematic = true;
        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Release(Vector3 throwVelocity)
    {
        if (!isHeld) return;
        isHeld = false;
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        if (throwVelocity.sqrMagnitude > 0.01f)
            rb.drag = 0f;
        rb.velocity = throwVelocity;
    }
}
