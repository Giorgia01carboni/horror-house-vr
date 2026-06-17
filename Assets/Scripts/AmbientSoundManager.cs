using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AmbientSoundEntry
{
    public AudioClip clip;
    [Tooltip("Seconds between each play.")]
    public float interval = 60f;
    [Range(0f, 1f)]
    public float volume = 0.35f;
}

public class AmbientSoundManager : MonoBehaviour
{
    public static AmbientSoundManager Instance { get; private set; }

    [SerializeField] private List<AmbientSoundEntry> sounds = new List<AmbientSoundEntry>();

    private AudioSource audioSource;
    private bool insideHouse;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        foreach (var entry in sounds)
            if (entry.clip != null)
                StartCoroutine(SoundLoop(entry));
    }

    IEnumerator SoundLoop(AmbientSoundEntry entry)
    {
        yield return new WaitForSeconds(entry.interval);
        while (true)
        {
            if (!insideHouse)
                audioSource.PlayOneShot(entry.clip, entry.volume);
            yield return new WaitForSeconds(entry.interval);
        }
    }

    public void SetInsideHouse(bool inside)
    {
        insideHouse = inside;
    }
}
