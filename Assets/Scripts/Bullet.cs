using UnityEngine;

// Spawned at runtime by VRRevolver.SpawnBullet().
// Rigidbody and SphereCollider are added before this component, so GetComponent is safe in Start.
public class Bullet : MonoBehaviour
{
    [HideInInspector] public Vector3 launchVelocity;
    [HideInInspector] public float   lifetime = 5f;

    void Start()
    {
        // Start() is deferred to the next frame, by which point PhysX has registered the Rigidbody.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.velocity = launchVelocity;
        Destroy(gameObject, lifetime);
    }
}
