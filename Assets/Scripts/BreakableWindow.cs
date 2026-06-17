using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class BreakableWindow : MonoBehaviour
{
    [SerializeField] private GameObject glassRoot;
    [SerializeField] private GameObject intactGlass;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource forestAmbience;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private TextMeshProUGUI enterText;
    [SerializeField] private Image blackOverlay;
    [SerializeField] private Vector3 teleportDestination;
    [SerializeField] private float interactDistance = 3f;
    // Negative = outside is on the -forward side of the wall (default for this window: outside is -Z).
    // Flip to positive in the Inspector if the hint appears on the wrong side.
    [SerializeField] private float outsideDotSign = -1f;
    [SerializeField] private float explosionForce = 5f;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float fadeDuration = 0.2f;

    private Transform player;
    private PlayerAudio playerAudio;
    private Rigidbody[] fragments;

    private bool broken;
    private bool teleporting;
    private bool entered;

    bool IsVR => UnityEngine.XR.XRSettings.enabled;

    void Start()
    {
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            playerAudio = playerGO.GetComponent<PlayerAudio>();
        }

        if (glassRoot != null)
        {
            fragments = glassRoot.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in fragments)
                rb.isKinematic = true;
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
        if (enterText != null) enterText.enabled = false;
        if (blackOverlay != null)
        {
            blackOverlay.color = new Color(0, 0, 0, 0);
            blackOverlay.raycastTarget = false;
        }
    }

    void Update()
    {
        if (player == null || teleporting || entered) return;

        float dot = Vector3.Dot(player.position - transform.position, transform.forward);
        bool playerIsOutside = dot * outsideDotSign >= 0f;
        float dist = Vector3.Distance(player.position, transform.position);
        bool inRange = dist <= interactDistance;

        if (!playerIsOutside)
        {
            HintManager.Instance?.Hide(this);
            return;
        }

        if (!broken)
        {
            if (inRange)
                HintManager.Instance?.Show(this, hintText != null ? hintText.text : "Mmm... That window...", 0);
            else
                HintManager.Instance?.Hide(this);
        }
        else if (!entered)
        {
            if (inRange)
            {
                // Same logic, only the prompt key differs: [A] on the right Quest controller in VR, [E] on keyboard.
                string enterMsg = IsVR ? "[A] Enter" : (enterText != null ? enterText.text : "[E] Enter");
                HintManager.Instance?.Show(this, enterMsg, 2);
            }
            else
            {
                HintManager.Instance?.Hide(this);
            }

            bool enterPressed = IsVR
                ? OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)
                : (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
            if (inRange && enterPressed)
                StartCoroutine(TeleportInside());
        }
    }

    void Break()
    {
        if (broken) return;
        broken = true;
        HintManager.Instance?.Hide(this);
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (intactGlass != null) intactGlass.SetActive(false);
        audioSource?.Play();

        if (fragments == null) return;
        foreach (var rb in fragments)
        {
            rb.transform.SetParent(null, true);
            rb.isKinematic = false;
            rb.AddExplosionForce(explosionForce, player != null ? player.position : transform.position,
                explosionRadius, 0.5f, ForceMode.Impulse);
        }
        if (glassRoot != null) glassRoot.SetActive(false);
    }

    IEnumerator TeleportInside()
    {
        teleporting = true;
        HintManager.Instance?.Hide(this);
        if (enterText != null) enterText.enabled = false;

        yield return StartCoroutine(Fade(0f, 1f));

        entered = true;
        playerAudio?.SetInsideHouse(true);
        HorrorFog.Instance?.SetInsideHouse(true);
        AmbientSoundManager.Instance?.SetInsideHouse(true);
        forestAmbience?.Stop();

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = teleportDestination;
        if (cc != null) cc.enabled = true;

        yield return StartCoroutine(Fade(1f, 0f));

        teleporting = false;
    }

    IEnumerator Fade(float from, float to)
    {
        if (blackOverlay == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            blackOverlay.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        blackOverlay.color = new Color(0, 0, 0, to);
    }

    public void TriggerBreak() => Break();
}
