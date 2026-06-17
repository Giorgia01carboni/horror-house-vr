using System.Collections;
using UnityEngine;

// Attach to the painting GameObject.
// When the player gets within triggerRange, a ghost whisper plays from just behind
// the player's head — 3D positioned so turning around reveals nothing there.
public class PaintingGhostSound : MonoBehaviour
{
    [SerializeField] AudioClip ghostClip;
    [SerializeField] float triggerRange  = 4f;
    [SerializeField] float clipDuration  = 6f;   // how many seconds to play
    [SerializeField] float fadeDuration  = 1.8f;  // fade-out at the end
    [SerializeField] float behindOffset  = 0.9f;  // metres behind the player's head

    Transform _head;   // the player's "ear": the active AudioListener (falls back to the camera)
    bool      _triggered;

    void Start()
    {
        ResolveHead();
    }

    // Anchor everything to the ACTIVE AudioListener, not whatever Camera.main happens to be.
    // In this scene the listener lives on CenterEyeAnchor; spawning the whisper anywhere else
    // (e.g. a stray PlayerLook.cam) puts the 3D sound far from the ear and it's never heard.
    void ResolveHead()
    {
        var listener = FindObjectOfType<AudioListener>();
        if (listener != null) { _head = listener.transform; return; }

        var cam = FindObjectOfType<PlayerLook>()?.cam ?? Camera.main;
        if (cam == null)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null) cam = rig.centerEyeAnchor.GetComponent<Camera>();
        }
        _head = cam != null ? cam.transform : null;
    }

    void Update()
    {
        if (_triggered || ghostClip == null) return;
        if (_head == null) { ResolveHead(); if (_head == null) return; }

        float dist = Vector3.Distance(transform.position, _head.position);
        if (dist <= triggerRange)
        {
            _triggered = true;
            StartCoroutine(PlayBehindPlayer());
        }
    }

    IEnumerator PlayBehindPlayer()
    {
        // Spawn a hidden AudioSource right behind the player's head
        var go  = new GameObject("GhostWhisper");
        var src = go.AddComponent<AudioSource>();

        src.clip         = ghostClip;
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.minDistance  = 0.5f;   // full volume out to here so it's clearly heard
        src.maxDistance  = 5f;
        src.dopplerLevel = 0f;
        src.priority     = 0;
        src.volume       = 1f;
        src.playOnAwake  = false;
        src.loop         = false;

        // Place it directly behind the player's ear (the AudioListener) and keep it there
        go.transform.SetParent(_head);
        go.transform.localPosition = Vector3.back * behindOffset;
        go.transform.localRotation = Quaternion.identity;

        src.Play();

        float elapsed   = 0f;
        float fadeStart = clipDuration - fadeDuration;

        while (elapsed < clipDuration)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= fadeStart)
            {
                float t = (elapsed - fadeStart) / fadeDuration;
                src.volume = Mathf.Lerp(1f, 0f, t);
            }

            yield return null;
        }

        src.Stop();
        Destroy(go);
    }
}
