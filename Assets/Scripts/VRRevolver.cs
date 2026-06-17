using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// Attach to the Revolver root GameObject.
/// Requires a child empty named "Muzzle" pointing along the barrel (its forward = shot direction).
/// VR:  right-hand grip trigger to grab, index trigger to shoot, release grip to drop.
/// KB:  approach + look at gun → [E] to pick up, gun held at right-hand screen position.
[RequireComponent(typeof(Rigidbody))]
public class VRRevolver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform muzzleTransform;

    [Header("Grab (shared)")]
    [SerializeField] float grabRange = 0.15f;

    [Header("VR grip pose")]
    [SerializeField] Vector3 vrGripLocalPos = new Vector3(0f, -0.02f, -0.05f);
    [SerializeField] Vector3 vrGripLocalRot = new Vector3(0f, 0f, 0f);

    [Header("KB")]
    [SerializeField] float   kbPickupRange  = 2.2f;

    [Header("First-look dialogue")]
    [Tooltip("How far away the 'who leaves a gun here' line fires when you look at the gun. " +
             "Larger than the pickup range so it plays on sight, not only once you're right on top of it.")]
    [SerializeField] float   dialogueRange  = 6f;

    [Header("Shooting")]
    [SerializeField] float shotRange       = 50f;
    [SerializeField] float shotForce       = 120f;
    [SerializeField] float muzzleFlashTime = 0.06f;

    [Header("Bullet")]
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] float      bulletSpeed    = 12f;
    [SerializeField] float      bulletLifetime = 5f;
    [SerializeField] float      bulletScale    = 4f;

    [Header("Impact Mark")]
    [SerializeField] float impactMarkSize     = 0.08f;
    [SerializeField] float impactMarkLifetime = 20f;

    [Header("Haptics (VR)")]
    [SerializeField] float hapticFrequency = 0.8f;
    [SerializeField] float hapticAmplitude = 1.0f;
    [SerializeField] float hapticDuration  = 0.08f;

    [Header("Audio")]
    [SerializeField] AudioClip grabClip;
    [SerializeField] AudioClip shotClip;

    Rigidbody            _rb;
    AudioSource          _audio;
    Light                _muzzleLight;
    Camera               _cam;

    // VR state
    OVRInput.Controller  _vrHolder = OVRInput.Controller.None;

    Renderer[] _renderers;
    GameObject _crosshair;

    // KB state
    bool _kbHeld;

    // Cached bullet-hole decal texture (created once, reused for every mark)
    static Texture2D _bulletHoleTex;

    // Dialogue
    bool            _dialoguePlayed;
    bool            _dialoguePlaying;
    TextMeshProUGUI _dialogueTMP;
    // Distinct HintManager key so the gun dialogue doesn't overwrite the grab hint (which uses `this`).
    readonly object _dialogueKey = new object();

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
    public bool IsHeld => _vrHolder != OVRInput.Controller.None || _kbHeld;
    public static bool GunIsHeld { get; private set; }

void Awake()
    {
        _rb    = GetComponent<Rigidbody>();
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 1f;
        _audio.playOnAwake  = false;

        _rb.isKinematic = true;
        _rb.useGravity  = false;

        if (muzzleTransform == null)
            muzzleTransform = transform.Find("Muzzle");

        var playerLook = FindObjectOfType<PlayerLook>();
        _cam = playerLook?.cam ?? Camera.main;

        _renderers = GetComponentsInChildren<Renderer>(true);

        BuildMuzzleLight();
        BuildCrosshair();
    }

    // ── Muzzle flash light ────────────────────────────────────────────────────

    void BuildMuzzleLight()
    {
        if (muzzleTransform == null) return;
        var go = new GameObject("MuzzleFlashLight");
        go.transform.SetParent(muzzleTransform, false);
        _muzzleLight           = go.AddComponent<Light>();
        _muzzleLight.type      = LightType.Point;
        _muzzleLight.color     = new Color(1f, 0.85f, 0.4f);
        _muzzleLight.intensity = 0f;
        _muzzleLight.range     = 3f;
    }

    // ── Dialogue UI ───────────────────────────────────────────────────────────

    void BuildDialogueUI()
    {
        Canvas canvas = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return;

        var go = new GameObject("Revolver_DialogueText");
        go.transform.SetParent(canvas.transform, false);
        _dialogueTMP = go.AddComponent<TextMeshProUGUI>();

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) _dialogueTMP.font = font;

        _dialogueTMP.text          = "Who leaves a gun here? Oh yeah right, we are in the USA...";
        _dialogueTMP.fontSize      = 28f;
        _dialogueTMP.color         = new Color(0.657f, 0.721f, 0.741f, 0f);
        _dialogueTMP.alignment     = TextAlignmentOptions.Center;
        _dialogueTMP.raycastTarget = false;

        var rt = _dialogueTMP.rectTransform;
        rt.anchorMin        = new Vector2(0.1f, 0f);
        rt.anchorMax        = new Vector2(0.9f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta        = new Vector2(0f, 60f);
        go.SetActive(false);
    }

    // ── Crosshair ─────────────────────────────────────────────────────────────

    void BuildCrosshair()
    {
        Canvas canvas = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return;

        var go = new GameObject("Revolver_Crosshair");
        go.transform.SetParent(canvas.transform, false);
        var img = go.AddComponent<Image>();
        img.color         = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(6f, 6f);
        rt.anchoredPosition = Vector2.zero;
        go.SetActive(false);
        _crosshair = go;
    }

    IEnumerator PlayDialogue()
    {
        // Route through HintManager so it shows in BOTH keyboard (screen) and VR
        // (world-space) — the old self-built ScreenSpaceOverlay text was invisible in VR.
        _dialoguePlaying = true;
        HintManager.Instance?.Show(_dialogueKey,
            "Who leaves a gun here? Oh yeah right, we are in the USA...", 4);
        yield return new WaitForSeconds(3.5f);
        HintManager.Instance?.Hide(_dialogueKey);
        _dialoguePlaying = false;
    }

    IEnumerator FadeDialogue(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = _dialogueTMP.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            _dialogueTMP.color = c;
            yield return null;
        }
        c.a = to;
        _dialogueTMP.color = c;
    }

    // ── Update ────────────────────────────────────────────────────────────────

void Update()
    {
        if (_cam == null)
        {
            var playerLook = FindObjectOfType<PlayerLook>();
            _cam = playerLook?.cam ?? Camera.main;
        }

        if (!_dialoguePlayed) CheckFirstApproach();

        if (IsVR)
            UpdateVR();
        else
            UpdateKeyboard();
    }

    // Fires once when the player is near and roughly facing the gun, regardless of mode.
void CheckFirstApproach()
    {
        if (_cam == null) return;
        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > dialogueRange) return;

        Vector3 dir = (transform.position - _cam.transform.position).normalized;
        if (Vector3.Dot(dir, _cam.transform.forward) < 0.6f) return;

        _dialoguePlayed = true;
        StartCoroutine(PlayDialogue());
    }

    // ── VR ────────────────────────────────────────────────────────────────────

    void UpdateVR()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null) return;

        if (_vrHolder == OVRInput.Controller.None)
        {
            bool rClose = hand.RightHand != null
                && Vector3.Distance(hand.RightHand.position, transform.position) <= grabRange;
            bool lClose = hand.LeftHand != null
                && Vector3.Distance(hand.LeftHand.position, transform.position) <= grabRange;

            if (rClose || lClose) ShowHint("Grip to grab revolver");
            else                  HideHint();

            if (rClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
                VRGrab(hand.RightHand, OVRInput.Controller.RTouch);
            else if (lClose && OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
                VRGrab(hand.LeftHand, OVRInput.Controller.LTouch);
        }
        else
        {
            HideHint();
            if (OVRInput.GetUp(OVRInput.Button.PrimaryHandTrigger, _vrHolder))
                VRDrop();
            else if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, _vrHolder))
                Shoot();
        }
    }

void VRGrab(Transform handTransform, OVRInput.Controller controller)
    {
        _vrHolder = controller;
        GunIsHeld = true;
        _rb.isKinematic = true;
        transform.SetParent(handTransform);
        transform.localPosition = vrGripLocalPos;
        transform.localRotation = Quaternion.Euler(vrGripLocalRot);
        if (grabClip != null) _audio.PlayOneShot(grabClip);
        // Fire the "who leaves a gun here" line the moment the gun is grabbed in VR —
        // the approach dot-check often fails while the gun is in hand (it's beside you,
        // not in your line of sight), so the line would silently never play.
        if (!_dialoguePlayed)
        {
            _dialoguePlayed = true;
            StartCoroutine(PlayDialogue());
        }
        // Gun layer not used in VR — IK drives the arm
    }

void VRDrop()
    {
        var hand = VRPhysicsHand.Instance;
        Vector3 releaseVel = (_vrHolder == OVRInput.Controller.RTouch)
            ? (hand != null ? hand.RightVelocity : Vector3.zero)
            : (hand != null ? hand.LeftVelocity  : Vector3.zero);

        transform.SetParent(null);
        _rb.isKinematic = false;
        _rb.useGravity   = true;
        _rb.velocity     = releaseVel;
        _vrHolder = OVRInput.Controller.None;
        GunIsHeld = false;
        HideHint();
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

void UpdateKeyboard()
    {
        if (_kbHeld)
        {
            if (Input.GetMouseButtonDown(0))
                Shoot();
            else if (Input.GetMouseButtonDown(1))
                KBDrop();
            return;
        }

        if (_cam == null) return;
        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > kbPickupRange) { HideHint(); return; }

        Vector3 dir = (transform.position - _cam.transform.position).normalized;
        if (Vector3.Dot(dir, _cam.transform.forward) < 0.6f) { HideHint(); return; }

        if (_dialoguePlaying) { HideHint(); return; }

        ShowHint("[E] Pick up revolver");
        if (Input.GetKeyDown(KeyCode.E))
            KBGrab();
    }

void KBGrab()
    {
        _kbHeld = true;
        GunIsHeld = true;
        _rb.isKinematic = true;
        // Parent to camera so the gun follows the player; renderers off so it stays invisible.
        transform.SetParent(_cam.transform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        SetRenderersVisible(false);
        _crosshair?.SetActive(true);
        if (grabClip != null) _audio.PlayOneShot(grabClip);
        HideHint();
        StartCoroutine(ShowHintTimed("[LMB] Shoot    [RMB] Drop", 3f));
    }

void KBDrop()
    {
        _kbHeld = false;
        GunIsHeld = false;
        transform.SetParent(null);
        _rb.isKinematic = false;
        _rb.useGravity  = true;
        SetRenderersVisible(true);
        _crosshair?.SetActive(false);
        HideHint();
    }

IEnumerator ShowHintTimed(string msg, float duration)
    {
        ShowHint(msg);
        yield return new WaitForSeconds(duration);
        HideHint();
    }


    // ── Shoot (shared) ────────────────────────────────────────────────────────

void Shoot()
    {
        if (shotClip != null) _audio.PlayOneShot(shotClip);
        if (_vrHolder != OVRInput.Controller.None) StartCoroutine(HapticCoroutine());
        SpawnBullet();

        Ray ray;
        if (_kbHeld)
        {
            ray = new Ray(_cam.transform.position, _cam.transform.forward);
        }
        else
        {
            if (muzzleTransform == null) return;
            StartCoroutine(MuzzleFlash());
            ray = new Ray(muzzleTransform.position, muzzleTransform.forward);
        }

        RaycastHit hit = default;
        bool gotHit = false;

        if (_kbHeld)
        {
            // RaycastAll to skip the gun's own colliders (gun is parented to camera at origin)
            var hits = Physics.RaycastAll(ray, shotRange);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.rigidbody == _rb || h.transform.IsChildOf(transform)) continue;
                hit = h; gotHit = true; break;
            }
        }
        else
        {
            gotHit = Physics.Raycast(ray, out hit, shotRange);
        }

        if (gotHit)
        {
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(ray.direction * shotForce, hit.point, ForceMode.Impulse);
            hit.collider.SendMessageUpwards("OnShot", hit, SendMessageOptions.DontRequireReceiver);
            if (hit.collider.GetComponentInParent<EnemyAI>() == null)
                SpawnImpactMark(hit);
        }
    }

    void SpawnImpactMark(RaycastHit hit)
    {
        // Cylinder disk — this geometry is confirmed visible in this scene.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "BulletMark";
        Destroy(go.GetComponent<Collider>());

        go.transform.position   = hit.point + hit.normal * 0.015f;
        go.transform.rotation   = Quaternion.FromToRotation(Vector3.up, hit.normal);
        go.transform.localScale = new Vector3(impactMarkSize, 0.003f, impactMarkSize);
        go.transform.SetParent(hit.transform, true);

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // Keep the same opaque Standard material that worked — only change the look.
        var mat = mr.material;
        mat.mainTexture = BulletHoleTex();
        mat.color = Color.white;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.06f, 0.025f, 0.005f));
        mr.material = mat;

        Destroy(go, impactMarkLifetime);
    }

    // Opaque bullet-hole texture for the cylinder disk cap.
    // Near-black centre, dark-brown scorch ring, medium-brown outer — no transparency needed
    // because the cylinder mesh itself is already circular.
    static Texture2D BulletHoleTex()
    {
        if (_bulletHoleTex != null) return _bulletHoleTex;

        const int S = 64;
        _bulletHoleTex = new Texture2D(S, S, TextureFormat.RGB24, false);
        _bulletHoleTex.filterMode = FilterMode.Bilinear;
        var px = new Color32[S * S];
        var centre = new Vector2(S * 0.5f, S * 0.5f);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / (S * 0.5f);
            byte r, g, b;
            if (d < 0.22f)
            {
                r = 4;  g = 2;  b = 1;                                         // hole — near black
            }
            else if (d < 0.55f)
            {
                float t = (d - 0.22f) / 0.33f;
                r = (byte)Mathf.RoundToInt(Mathf.Lerp(10f, 32f, t));
                g = (byte)(r / 2);
                b = (byte)(r / 5);                                              // scorch ring — dark brown
            }
            else
            {
                float t = (d - 0.55f) / 0.45f;
                r = (byte)Mathf.RoundToInt(Mathf.Lerp(32f, 55f, t));
                g = (byte)(r / 2);
                b = (byte)(r / 5);                                              // outer scorch — medium brown
            }
            px[y * S + x] = new Color32(r, g, b, 255);
        }

        _bulletHoleTex.SetPixels32(px);
        _bulletHoleTex.Apply();
        return _bulletHoleTex;
    }

    IEnumerator MuzzleFlash()
    {
        if (_muzzleLight == null) yield break;
        _muzzleLight.intensity = 8f;
        yield return new WaitForSeconds(muzzleFlashTime);
        _muzzleLight.intensity = 0f;
    }

    void SpawnBullet()
    {
        if (bulletPrefab == null) return;

        Vector3 spawnPos, spawnDir;
        if (_kbHeld)
        {
            spawnDir = _cam.transform.forward;
            spawnPos = _cam.transform.position + spawnDir * 0.5f;
        }
        else
        {
            if (muzzleTransform == null) return;
            spawnDir = muzzleTransform.forward;
            spawnPos = muzzleTransform.position + spawnDir * 0.04f;
        }

        // LP_Bullet tip is at +Y in parent space; Rx(90°) maps +Y → +Z so the tip faces spawnDir.
        var rot = Quaternion.LookRotation(spawnDir) * Quaternion.Euler(90f, 0f, 0f);
        var go  = Instantiate(bulletPrefab, spawnPos, rot);
        go.transform.localScale = Vector3.one * bulletScale;

        // Layer 2 = "Ignore Raycast" — shells must not block the shooting raycast.
        go.layer = 2;
        foreach (Transform child in go.transform) child.gameObject.layer = 2;

        // Collider must exist before Rigidbody so Continuous mode has a shape to sweep.
        var col    = go.AddComponent<SphereCollider>();
        col.radius = 0.01f;

        var rb  = go.AddComponent<Rigidbody>();
        rb.mass                   = 0.015f;
        rb.drag                   = 0.05f;
        rb.useGravity             = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Bullet.Start() applies velocity next frame after PhysX registers the body.
        var b         = go.AddComponent<Bullet>();
        b.launchVelocity = spawnDir * bulletSpeed;
        b.lifetime       = bulletLifetime;
    }

    IEnumerator HapticCoroutine()
    {
        var hand = VRPhysicsHand.Instance;
        if (hand == null) yield break;
        float t = 0f;
        while (t < hapticDuration)
        {
            hand.SetHaptic(_vrHolder, hapticFrequency, hapticAmplitude);
            t += Time.deltaTime;
            yield return null;
        }
        hand.StopHaptic(_vrHolder);
    }

    // ── Hint helpers ──────────────────────────────────────────────────────────

    void ShowHint(string msg) => HintManager.Instance?.Show(this, msg, 2);
    void HideHint()           => HintManager.Instance?.Hide(this);

    void SetRenderersVisible(bool visible)
    {
        if (_renderers == null) return;
        foreach (var r in _renderers) r.enabled = visible;
    }
}
