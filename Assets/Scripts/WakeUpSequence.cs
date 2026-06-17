using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.UI;
using TMPro;

public class WakeUpSequence : MonoBehaviour
{
    private InputManager inputManager;
    private Animator playerAnimator;
    private Image topEyelid;
    private Image bottomEyelid;
    private TextMeshProUGUI wakeText;
    private TextMeshProUGUI skipHint;
    private Canvas canvas;

    private Transform headBone;
    private Transform camTransform;
    private bool trackHeadPosition = false;
    private Vector3 camOriginalLocalPos;
    private Quaternion camOriginalLocalRot;

    private Coroutine wakeCoroutine;

    bool isVR;

    void Awake()
    {
        isVR = UnityEngine.XR.XRSettings.enabled;

        if (isVR)
        {
            // Build a world-space canvas locked to the VR camera so eyelids
            // and text are visible in the headset (ScreenSpaceOverlay is invisible in VR).
            Transform vrCam = Camera.main?.transform;
            if (vrCam == null)
            {
                var rig = FindObjectOfType<OVRCameraRig>();
                if (rig != null) vrCam = rig.centerEyeAnchor;
            }

            var go = new GameObject("WakeUp_VRCanvas");
            if (vrCam != null) go.transform.SetParent(vrCam, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one * 0.001f;

            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();
            ((RectTransform)go.transform).sizeDelta = new Vector2(2000f, 2000f);
            canvas = c;
        }
        else
        {
            canvas = FindObjectOfType<Canvas>();
        }

        topEyelid    = CreateEyelid("WakeUp_TopEyelid", true);
        bottomEyelid = CreateEyelid("WakeUp_BottomEyelid", false);
        wakeText     = CreateWakeText();
        skipHint     = CreateSkipHint();
        SetEyelidHeight(1f);
    }

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        inputManager   = player.GetComponent<InputManager>();
        playerAnimator = player.GetComponent<Animator>();

        inputManager.enabled = false;
        playerAnimator.SetBool("isWalking", false);
        playerAnimator.SetBool("isRunning", false);

        if (isVR)
        {
            // In VR the headset drives the camera, so the lying->standing "get up"
            // choreography can't be followed by the view. We also do NOT play the
            // StandingUp clip: until body-tracking data becomes valid (first seconds),
            // the visible pose falls back to the Animator state, and StandingUp's
            // end pose leaves the body looking crooked. Start straight in the normal
            // standing Idle so the fallback pose is clean; the Movement SDK
            // retargeting then takes over and drives the body from tracking.
            playerAnimator.speed = 1f;
            playerAnimator.Play("Idle", 0, 0f);
            wakeCoroutine = StartCoroutine(VRWakeCoroutine());
            return;
        }

        foreach (var rig in player.GetComponentsInChildren<Rig>(includeInactive: true))
            rig.weight = 0f;

        playerAnimator.speed = 0f;
        wakeCoroutine = StartCoroutine(WakeUpCoroutine());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
            Skip();
        if (isVR && (OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Two)))
            Skip();
    }

    void Skip()
    {
        if (wakeCoroutine != null) { StopCoroutine(wakeCoroutine); wakeCoroutine = null; }

        trackHeadPosition = false;
        playerAnimator.speed = 1f;
        playerAnimator.Play("StandingUp", 0, 0.95f);

        if (camTransform != null)
        {
            camTransform.localPosition = camOriginalLocalPos;
            camTransform.localRotation = camOriginalLocalRot;
        }

        SetEyelidHeight(0f);
        inputManager.enabled = true;

        if (topEyelid != null)  Destroy(topEyelid.gameObject);
        if (bottomEyelid != null) Destroy(bottomEyelid.gameObject);
        if (wakeText != null)   Destroy(wakeText.gameObject);
        if (skipHint != null)   Destroy(skipHint.gameObject);
        Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (!trackHeadPosition || headBone == null || camTransform == null) return;

        camTransform.rotation = Quaternion.LookRotation(headBone.forward, Vector3.up);
        camTransform.position = headBone.position + camTransform.forward * 0.1f;
    }

    Image CreateEyelid(string goName, bool isTop)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        if (isTop)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 2000f);
        return img;
    }

    TextMeshProUGUI CreateWakeText()
    {
        var go = new GameObject("WakeUp_Text");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var tmp = go.AddComponent<TextMeshProUGUI>();

        var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fontAsset != null)
            tmp.font = fontAsset;

        tmp.text = "W-Where am I?";
        tmp.fontSize = isVR ? 120f : 30f;
        tmp.color = new Color(0.657f, 0.721f, 0.741f, 0f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        var rt = tmp.rectTransform;
        if (isVR)
        {
            // Centre the text in the VR world-space canvas
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -200f);
            rt.sizeDelta = new Vector2(0f, 300f);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 50f);
            rt.sizeDelta = new Vector2(0f, 60f);
        }

        go.SetActive(false);
        return tmp;
    }

    TextMeshProUGUI CreateSkipHint()
    {
        var go = new GameObject("WakeUp_SkipHint");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var tmp = go.AddComponent<TextMeshProUGUI>();

        var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fontAsset != null)
            tmp.font = fontAsset;

        tmp.text = isVR ? "Press A to skip" : "Space / Right-click to skip";
        tmp.fontSize = isVR ? 80f : 16f;
        tmp.color = new Color(1f, 1f, 1f, 0.4f);
        tmp.alignment = isVR ? TextAlignmentOptions.Center : TextAlignmentOptions.BottomRight;
        tmp.raycastTarget = false;

        var rt = tmp.rectTransform;
        if (isVR)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -400f);
            rt.sizeDelta = new Vector2(0f, 150f);
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            rt.sizeDelta = new Vector2(350f, 30f);
        }

        return tmp;
    }

    Transform FindBoneByName(Transform root, string boneName)
    {
        if (root.name.IndexOf(boneName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return root;
        foreach (Transform child in root)
        {
            var result = FindBoneByName(child, boneName);
            if (result != null) return result;
        }
        return null;
    }

    void SetEyelidHeight(float amount)
    {
        float canvasH = canvas.GetComponent<RectTransform>().rect.height;
        float h = canvasH * amount * 0.5f;
        topEyelid.rectTransform.sizeDelta = new Vector2(0f, h);
        bottomEyelid.rectTransform.sizeDelta = new Vector2(0f, h);
    }

    IEnumerator AnimateEyelids(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetEyelidHeight(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetEyelidHeight(to);
    }

    IEnumerator FadeText(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color color = wakeText.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            wakeText.color = color;
            yield return null;
        }
        color.a = to;
        wakeText.color = color;
    }

    // Simple, robust VR intro: no camera choreography, no dependence on the
    // get-up animation. Just blink the eyes open from black, show the line, go.
    IEnumerator VRWakeCoroutine()
    {
        trackHeadPosition = false;

        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(AnimateEyelids(1f, 0f, 0.2f));
        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(AnimateEyelids(0f, 1f, 0.15f));
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(AnimateEyelids(1f, 0f, 1.0f));

        if (skipHint != null) Destroy(skipHint.gameObject);

        wakeText.gameObject.SetActive(true);
        yield return StartCoroutine(FadeText(0f, 1f, 0.6f));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeText(1f, 0f, 0.6f));

        inputManager.enabled = true;

        if (topEyelid != null)    Destroy(topEyelid.gameObject);
        if (bottomEyelid != null) Destroy(bottomEyelid.gameObject);
        if (wakeText != null)     Destroy(wakeText.gameObject);
        Destroy(gameObject);
    }

    IEnumerator WakeUpCoroutine()
    {
        yield return null;

        headBone = playerAnimator.GetBoneTransform(HumanBodyBones.Head)
                   ?? FindBoneByName(playerAnimator.transform, "head");
        camTransform = playerAnimator.transform.Find("CameraTest");

        if (camTransform != null)
        {
            camOriginalLocalPos = camTransform.localPosition;
            camOriginalLocalRot = camTransform.localRotation;
        }

        trackHeadPosition = true;

        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(AnimateEyelids(1f, 0f, 0.18f));
        yield return new WaitForSeconds(0.08f);
        yield return StartCoroutine(AnimateEyelids(0f, 1f, 0.14f));
        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(AnimateEyelids(1f, 0f, 0.24f));
        yield return new WaitForSeconds(0.14f);
        yield return StartCoroutine(AnimateEyelids(0f, 1f, 0.14f));
        yield return new WaitForSeconds(0.55f);

        yield return StartCoroutine(AnimateEyelids(1f, 0f, 0.28f));
        yield return new WaitForSeconds(0.22f);
        yield return StartCoroutine(AnimateEyelids(0f, 1f, 0.14f));
        yield return new WaitForSeconds(0.3f);

        playerAnimator.Play("StandingUp", 0, 0f);
        playerAnimator.speed = 1f;
        yield return StartCoroutine(AnimateEyelids(1f, 0f, 1.1f));

        yield return new WaitUntil(() =>
        {
            var info = playerAnimator.GetCurrentAnimatorStateInfo(0);
            return !info.IsName("StandingUp") || info.normalizedTime >= 0.92f;
        });

        trackHeadPosition = false;
        if (camTransform != null)
        {
            camTransform.localPosition = camOriginalLocalPos;
            camTransform.localRotation = camOriginalLocalRot;
        }

        if (skipHint != null) Destroy(skipHint.gameObject);

        wakeText.gameObject.SetActive(true);
        yield return StartCoroutine(FadeText(0f, 1f, 0.6f));
        yield return new WaitForSeconds(3.5f);
        yield return StartCoroutine(FadeText(1f, 0f, 0.6f));

        inputManager.enabled = true;

        Destroy(topEyelid.gameObject);
        Destroy(bottomEyelid.gameObject);
        Destroy(wakeText.gameObject);
        Destroy(gameObject);
    }
}
