using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RockBreakTrigger : MonoBehaviour
{
    [SerializeField] BreakableWindow window;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<GrabbableRock>() != null)
            window?.TriggerBreak();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<GrabbableRock>() != null)
            window?.TriggerBreak();
    }
}
