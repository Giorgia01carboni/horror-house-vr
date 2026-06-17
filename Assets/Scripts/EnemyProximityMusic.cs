using UnityEngine;

/// Looping enemy music that swells as the enemy nears the player.
///
/// 2D (spatialBlend 0) so it plays at full, uniform loudness — a 3D source pans/spreads
/// and is perceived much quieter at the same volume. The proximity swell is driven manually
/// from the distance between this enemy and the active AudioListener (the player's ears),
/// which tracks correctly in both KB and VR — unlike the Player-root transform used before.
///
/// NOTE: volume caps at 1.0, so it can never exceed the clip's own recorded level. If the
/// clip is quiet this will still be quiet — route it through an AudioMixer group with gain to
/// truly amplify it (or use a louder/normalised clip).
public class EnemyProximityMusic : MonoBehaviour
{
    [Header("Clip")]
    [SerializeField] AudioClip musicClip;

    [Header("Distance")]
    [Tooltip("Distance at which music becomes audible.")]
    [SerializeField] float startDistance = 60f;
    [Tooltip("Distance at which music reaches full volume/pitch.")]
    [SerializeField] float peakDistance  = 5f;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] float maxVolume = 1f;

    [Header("Pitch — gives the 'escalating tension' feel")]
    [SerializeField] float minPitch = 0.85f;
    [SerializeField] float maxPitch = 1.3f;

    [Header("Smoothing — lower = slower / more atmospheric")]
    [SerializeField] float smoothing = 4f;

    AudioSource _src;
    Transform   _ears;

    void Start()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip         = musicClip;
        _src.loop         = true;
        _src.playOnAwake  = false;
        _src.spatialBlend = 0f;     // 2D — full, uniform loudness
        _src.volume       = 0f;
        _src.pitch        = minPitch;
        _src.priority     = 32;     // guard against voice-stealing

        if (musicClip != null)
            _src.Play();
    }

    // The active AudioListener = where the player actually hears from (correct in KB and VR).
    Transform Ears()
    {
        var l = FindObjectOfType<AudioListener>();
        if (l != null) return l.transform;
        var p = GameObject.FindWithTag("Player");
        return p != null ? p.transform : null;
    }

    void Update()
    {
        if (_src == null) return;

        if (_ears == null || !_ears.gameObject.activeInHierarchy) _ears = Ears();
        if (_ears == null) return;

        float dist = Vector3.Distance(transform.position, _ears.position);

        // 0 at startDistance, 1 at peakDistance — linear so it gets loud across the approach,
        // not only at point-blank range.
        float t = 1f - Mathf.Clamp01((dist - peakDistance) / Mathf.Max(startDistance - peakDistance, 0.01f));

        float targetVolume = t * maxVolume;
        float targetPitch  = Mathf.Lerp(minPitch, maxPitch, t);

        _src.volume = Mathf.Lerp(_src.volume, targetVolume, Time.deltaTime * smoothing);
        _src.pitch  = Mathf.Lerp(_src.pitch,  targetPitch,  Time.deltaTime * smoothing);
    }
}
