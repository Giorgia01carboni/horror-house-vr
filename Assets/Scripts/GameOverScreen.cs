using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    CanvasGroup _group;
    bool _active;
    GameObject _canvasGO;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Build();
    }

    void Build()
    {
        // Ensure EventSystem exists for button clicks
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var canvasGO = new GameObject("GameOverCanvas");
        canvasGO.transform.SetParent(transform);
        _canvasGO = canvasGO;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // VR: ScreenSpaceOverlay is invisible in the headset; make it head-locked.
        if (VRUI.IsVR) VRUI.MakeHeadLocked(canvas);

        _group                 = canvasGO.AddComponent<CanvasGroup>();
        _group.alpha           = 0f;
        _group.blocksRaycasts  = false;
        _group.interactable    = false;

        // Black background
        var bg   = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = Color.black;
        Stretch(bgImg.rectTransform);

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text      = "I've had a bad dream...";
        title.fontSize  = 58f;
        title.fontStyle = FontStyles.Italic;
        title.alignment = TextAlignmentOptions.Center;
        title.color     = new Color(0.88f, 0.84f, 0.9f);
        var tRT = title.rectTransform;
        tRT.anchorMin        = new Vector2(0.1f, 0.55f);
        tRT.anchorMax        = new Vector2(0.9f, 0.72f);
        tRT.offsetMin        = tRT.offsetMax = Vector2.zero;

        // Button
        var btnGO = new GameObject("WakeButton");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.12f, 0.10f, 0.14f);
        var bRT = btnImg.rectTransform;
        bRT.anchorMin = new Vector2(0.33f, 0.37f);
        bRT.anchorMax = new Vector2(0.67f, 0.47f);
        bRT.offsetMin = bRT.offsetMax = Vector2.zero;

        var btn = btnGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.20f, 0.30f);
        colors.pressedColor     = new Color(0.35f, 0.28f, 0.40f);
        btn.colors = colors;
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(Restart);

        var btnTextGO = new GameObject("Label");
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnText.text      = "I want to wake up.";
        btnText.fontSize  = 26f;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color     = new Color(0.88f, 0.84f, 0.9f);
        Stretch(btnText.rectTransform);

        canvasGO.SetActive(false);
    }

    public static void Show()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameOverScreen");
            go.AddComponent<GameOverScreen>();
        }
        Instance.StartCoroutine(Instance.FadeIn());
    }

    IEnumerator FadeIn()
    {
        if (_active) yield break;
        _active = true;

        Time.timeScale    = 0f;
        Cursor.lockState  = CursorLockMode.None;
        Cursor.visible    = true;

        var canvasGO = _canvasGO;
        canvasGO.SetActive(true);

        float t = 0f;
        while (t < 2f)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / 2f);
            yield return null;
        }
        _group.alpha          = 1f;
        _group.blocksRaycasts = true;
        _group.interactable   = true;
    }

    void Update()
    {
        if (!_active || _group.alpha < 1f) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            Restart();
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            Restart();
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
