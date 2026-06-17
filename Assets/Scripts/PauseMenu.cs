using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    PlayerLook playerLook;
    InputManager inputManager;
    PlayerInteract playerInteract;

    Canvas canvas;
    GameObject mainPanel, sensPanel;
    Slider sensitivitySlider;
    TextMeshProUGUI sliderValueLabel;

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);

    void Start()
    {
        playerLook     = GetComponent<PlayerLook>()     ?? FindObjectOfType<PlayerLook>();
        inputManager   = GetComponent<InputManager>()   ?? FindObjectOfType<InputManager>();
        playerInteract = GetComponent<PlayerInteract>() ?? FindObjectOfType<PlayerInteract>();
        BuildUI();
        SetPaused(false);
    }

    void Update()
    {
        bool toggle = Input.GetKeyDown(KeyCode.Escape);
        if (IsVR) toggle |= OVRInput.GetDown(OVRInput.Button.Start);
        if (toggle) SetPaused(!IsPaused);
    }

    // ── Pause logic ─────────────────────────────────────────────────────────────

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        canvas.gameObject.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
        if (inputManager   != null) inputManager.enabled   = !paused;
        if (playerInteract != null) playerInteract.enabled = !paused;
        if (paused) ShowMain();
    }

    void ShowMain()
    {
        mainPanel.SetActive(true);
        sensPanel.SetActive(false);
    }

    void ShowSensitivity()
    {
        mainPanel.SetActive(false);
        sensPanel.SetActive(true);
        if (playerLook == null) return;
        float v = playerLook.xSensitivity;
        sensitivitySlider.SetValueWithoutNotify(v);
        sliderValueLabel.text = Mathf.RoundToInt(v).ToString();
    }

    // ── UI construction ──────────────────────────────────────────────────────────

    void BuildUI()
    {
        EnsureEventSystem();

        var cgo = new GameObject("PauseCanvas");
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        // VR: ScreenSpaceOverlay is invisible in the headset; make it head-locked.
        // (Resume still works by pressing Start again, handled in Update.)
        if (VRUI.IsVR) VRUI.MakeHeadLocked(canvas);

        // Dark overlay that blacks out the world
        var overlay = Img("Overlay", cgo.transform, new Color(0f, 0f, 0f, 0.82f));
        Stretch(overlay.rectTransform);

        // ── Main panel ───────────────────────────────────────────────────────────
        mainPanel = Panel(cgo.transform, 0.35f, 0.22f, 0.65f, 0.78f);

        var title = TMP(mainPanel.transform, "PAUSE", 80, FontStyles.Bold);
        Anch(title.rectTransform, 0.05f, 0.68f, 0.95f, 0.97f);
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = 12f;

        Btn(mainPanel.transform, "Resume",
            0.12f, 0.46f, 0.88f, 0.63f, () => SetPaused(false));
        Btn(mainPanel.transform, "Set Mouse Sensitivity",
            0.12f, 0.22f, 0.88f, 0.39f, ShowSensitivity);

        // ── Sensitivity panel ────────────────────────────────────────────────────
        sensPanel = Panel(cgo.transform, 0.35f, 0.22f, 0.65f, 0.78f);

        var sensTitle = TMP(sensPanel.transform, "Mouse Sensitivity", 42, FontStyles.Bold);
        Anch(sensTitle.rectTransform, 0.05f, 0.76f, 0.95f, 0.97f);
        sensTitle.alignment = TextAlignmentOptions.Center;

        sensitivitySlider = BuildSlider(sensPanel.transform, 0.06f, 0.52f, 0.78f, 0.68f);
        sensitivitySlider.minValue = 5f;
        sensitivitySlider.maxValue = 60f;
        float init = playerLook != null ? playerLook.xSensitivity : 30f;
        sensitivitySlider.SetValueWithoutNotify(init);

        sliderValueLabel = TMP(sensPanel.transform, init.ToString("0"), 34, FontStyles.Bold);
        Anch(sliderValueLabel.rectTransform, 0.80f, 0.52f, 0.98f, 0.68f);
        sliderValueLabel.alignment = TextAlignmentOptions.Center;

        sensitivitySlider.onValueChanged.AddListener(v =>
        {
            if (playerLook != null) playerLook.xSensitivity = playerLook.ySensitivity = v;
            sliderValueLabel.text = Mathf.RoundToInt(v).ToString();
        });

        Btn(sensPanel.transform, "Back",
            0.25f, 0.08f, 0.75f, 0.25f, ShowMain);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    Image Img(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    GameObject Panel(Transform parent, float x0, float y0, float x1, float y1)
    {
        var img = Img("Panel", parent, new Color(0.07f, 0.07f, 0.09f, 0.96f));
        Anch(img.rectTransform, x0, y0, x1, y1);
        return img.gameObject;
    }

    TextMeshProUGUI TMP(Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject(text);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        return tmp;
    }

    void Btn(Transform parent, string label, float x0, float y0, float x1, float y1, System.Action onClick)
    {
        var img = Img("Btn_" + label, parent, new Color(0.16f, 0.16f, 0.19f));
        Anch(img.rectTransform, x0, y0, x1, y1);

        var btn = img.gameObject.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor      = new Color(0.16f, 0.16f, 0.19f);
        cb.highlightedColor = new Color(0.28f, 0.28f, 0.33f);
        cb.pressedColor     = new Color(0.10f, 0.10f, 0.12f);
        cb.selectedColor    = cb.normalColor;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());

        var lbl = TMP(img.transform, label, 26, FontStyles.Normal);
        lbl.alignment = TextAlignmentOptions.Center;
        Stretch(lbl.rectTransform);
    }

    Slider BuildSlider(Transform parent, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        Anch(rect, x0, y0, x1, y1);
        var slider = go.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;

        // Track
        var bg = Img("BG", go.transform, new Color(0.18f, 0.18f, 0.20f));
        bg.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        bg.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

        // Fill area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        var faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.3f);
        faRect.anchorMax = new Vector2(1f, 0.7f);
        faRect.offsetMin = new Vector2(4f, 0f);
        faRect.offsetMax = new Vector2(-12f, 0f);

        var fill = Img("Fill", fillArea.transform, new Color(0.72f, 0.12f, 0.12f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        // Handle area
        var handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(go.transform, false);
        var haRect = handleArea.AddComponent<RectTransform>();
        haRect.anchorMin = Vector2.zero;
        haRect.anchorMax = Vector2.one;
        haRect.offsetMin = new Vector2(8f, 0f);
        haRect.offsetMax = new Vector2(-8f, 0f);

        var handle = Img("Handle", handleArea.transform, Color.white);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(22f, 0f);

        slider.fillRect      = fill.rectTransform;
        slider.handleRect    = handle.rectTransform;
        slider.targetGraphic = handle;

        return slider;
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void Anch(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
