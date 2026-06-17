using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// Attach to Pickup_Truck.
// - Player near truck WITH key   → win ending: black screen, title, engine sound, quit.
// - Player near truck WITHOUT key → one-time dialogue hint.
public class TruckInteraction : MonoBehaviour
{
    [SerializeField] float       triggerRange   = 3.5f;
    [SerializeField] AudioClip   engineClip;
    [SerializeField] float       fadeInDuration = 1.5f;
    [SerializeField] float       holdDuration   = 2.5f;  // black screen hold before fade out
    [SerializeField] float       fadeOutDuration = 2f;

    public static bool WinSequenceActive { get; private set; }

    Camera _cam;
    bool   _ending;
    bool   _noKeyHintShown;

    void Start()
    {
        _cam = FindObjectOfType<PlayerLook>()?.cam ?? Camera.main;
    }

    void Update()
    {
        if (_ending || _cam == null) return;

        float dist = Vector3.Distance(
            new Vector3(_cam.transform.position.x, transform.position.y, _cam.transform.position.z),
            transform.position);

        if (dist > triggerRange)
        {
            HintManager.Instance?.Hide(this);
            return;
        }

        if (TapedKey.KeyCollected)
        {
            HintManager.Instance?.Hide(this);
            _ending = true;
            WinSequenceActive = true;
            FindObjectOfType<EnemyAI>()?.StopChase();
            StartCoroutine(WinEnding());
        }
        else
        {
            if (!_noKeyHintShown)
            {
                _noKeyHintShown = true;
                StartCoroutine(NoKeyDialogue());
            }
        }
    }

    IEnumerator NoKeyDialogue()
    {
        HintManager.Instance?.Show(this, "<color=#CC3300>Dammit, I don't have the keys!!</color>", 3);
        yield return new WaitForSeconds(3.5f);
        HintManager.Instance?.Hide(this);
        _noKeyHintShown = false; // allow showing again if player walks away and back
    }

    IEnumerator WinEnding()
    {
        // Build full-screen black overlay + title
        var canvasGO = new GameObject("WinCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        canvasGO.AddComponent<CanvasScaler>();

        // VR: ScreenSpaceOverlay is invisible in the headset; make it head-locked.
        if (VRUI.IsVR) VRUI.MakeHeadLocked(canvas);

        var group         = canvasGO.AddComponent<CanvasGroup>();
        group.alpha       = 0f;
        group.blocksRaycasts = true;

        // Black background
        var bg  = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = Color.black;
        var bgRT    = bgImg.rectTransform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var title   = titleGO.AddComponent<TextMeshProUGUI>();
        title.text             = "Did I manage to escape?\nOr am I lost again?";
        title.fontSize         = 34f;
        title.fontStyle        = FontStyles.Italic;
        title.alignment        = TextAlignmentOptions.Center;
        title.characterSpacing = 6f;
        title.lineSpacing      = 18f;
        title.color            = new Color(0.72f, 0.70f, 0.76f);
        var tRT = title.rectTransform;
        tRT.anchorMin        = new Vector2(0.1f, 0.42f);
        tRT.anchorMax        = new Vector2(0.9f, 0.62f);
        tRT.offsetMin        = tRT.offsetMax = Vector2.zero;

        // Play engine sound 2D
        AudioSource engineSrc = null;
        if (engineClip != null)
        {
            var audioGO  = new GameObject("EngineAudio");
            engineSrc    = audioGO.AddComponent<AudioSource>();
            engineSrc.clip         = engineClip;
            engineSrc.spatialBlend = 0f;
            engineSrc.loop         = false;
            engineSrc.playOnAwake  = false;
            engineSrc.Play();
        }

        // Fade IN to black
        yield return Fade(group, 0f, 1f, fadeInDuration);

        // Hold
        yield return new WaitForSecondsRealtime(holdDuration);

        // Fade OUT (screen goes fully dark, world disappears)
        yield return Fade(group, 1f, 0f, fadeOutDuration);
        // Keep black — reuse bgImg opacity
        group.alpha = 1f;

        yield return new WaitForSecondsRealtime(1f);

        Quit();
    }

    IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
