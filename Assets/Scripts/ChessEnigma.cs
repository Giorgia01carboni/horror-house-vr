using System.Collections;
using UnityEngine;

// Attach to KingHighPoly 2.
// Enigma solved when rookHighPoly strikes this piece while held or thrown.
public class ChessEnigma : MonoBehaviour
{
    [SerializeField] AudioClip bellSound;
    [SerializeField] float minKnockVelocity = 0.2f;
    [SerializeField] DoorHandler mysteryDoor;

    bool _solved;

    public void TriggerKB()
    {
        if (_solved) return;
        _solved = true;
        StartCoroutine(SolveEnigma());
    }

    void OnCollisionEnter(Collision col)
    {
        if (_solved) return;
        if (col.gameObject.name != "rookHighPoly") return;

        var rook = col.gameObject.GetComponent<GrabbableChessPiece>();
        bool rookIsWeapon = rook != null
            && (rook.IsHeld || col.relativeVelocity.magnitude >= minKnockVelocity);
        if (!rookIsWeapon) return;

        _solved = true;
        StartCoroutine(SolveEnigma());
    }

IEnumerator SolveEnigma()
    {
        if (bellSound != null)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip         = bellSound;
            src.spatialBlend = 0f;
            src.volume       = 1f;

            var reverb = gameObject.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Cave;

            src.Play();
            yield return new WaitForSeconds(bellSound.length + 0.4f);
            src.Play();

            // Let most of the second bell ring, then fade out over 2 seconds
            float fadeOut = 2f;
            yield return new WaitForSeconds(Mathf.Max(0f, bellSound.length - fadeOut));

            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                if (src != null) src.volume = Mathf.Lerp(1f, 0f, t / fadeOut);
                yield return null;
            }

            if (src    != null) { src.Stop(); Destroy(src); }
            if (reverb != null) Destroy(reverb);
        }

        FindObjectOfType<doorOpener>()?.CrackOpen();
        mysteryDoor?.DisableTrigger();
    }
}
