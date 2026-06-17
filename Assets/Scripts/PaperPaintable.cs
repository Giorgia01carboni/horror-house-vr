using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class PaperPaintable : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] Renderer        paperRenderer;
    [SerializeField] Texture2D       writingTexture;
    [SerializeField] GrabbablePencil pencil;
    [SerializeField] Camera          playerCamera;
    [Tooltip("Empty child GameObject positioned in front of the paper at the angle the camera should use while inspecting.")]
    [SerializeField] Transform       cameraViewPoint;

    [Header("Player (auto-found if left empty)")]
    [SerializeField] PlayerLook  playerLook;
    [SerializeField] PlayerMotor playerMotor;

    [Header("Paint")]
    [SerializeField] int   renderTextureSize = 512;
    [SerializeField] float brushSizeUV       = 0.03f;
    [SerializeField, Range(0f, 1f)] float brushSoftness = 0.55f;
    [SerializeField] Color paperColor  = new Color(0.95f, 0.92f, 0.85f);
    [SerializeField] Color pencilColor = new Color(0.22f, 0.20f, 0.18f);

    [Header("Interaction")]
    [SerializeField] float interactRange       = 1.5f;   // distance to show "Use paper" prompt
    [SerializeField] float pencilContactRadius = 0.012f; // VR tip-to-surface threshold (m)
    [SerializeField, Range(0.1f, 1f)] float revealThreshold = 0.55f;
    [SerializeField] float transitionDuration  = 0.45f;

    [Header("Sound")]
    [SerializeField] AudioClip pencilSound;

    [Header("Events")]
    public UnityEvent onRevealComplete;

    // ── State machine ─────────────────────────────────────────────────────────

    enum PaperState { Idle, TransitionIn, Inspecting, TransitionOut }
    PaperState _state = PaperState.Idle;

    // Camera transition
    float      _transitionT;
    Vector3    _startCamPos,   _targetCamPos;
    Quaternion _startCamRot,   _targetCamRot;
    Transform  _savedCamParent;
    Vector3    _savedCamLocalPos;
    Quaternion _savedCamLocalRot;

    // ── Runtime ───────────────────────────────────────────────────────────────

    RenderTexture _paintRT;
    RenderTexture _paintSwapRT;
    RenderTexture _composedRT;
    Material      _stampMat;
    Material      _composeMat;
    AudioSource   _audio;
    Collider      _col;

    float _coverage;
    bool  _revealFired;

    static readonly int ID_BrushUV       = Shader.PropertyToID("_BrushUV");
    static readonly int ID_BrushSize     = Shader.PropertyToID("_BrushSize");
    static readonly int ID_BrushSoftness = Shader.PropertyToID("_BrushSoftness");
    static readonly int ID_WritingTex    = Shader.PropertyToID("_WritingTex");
    static readonly int ID_PaperTex      = Shader.PropertyToID("_PaperTex");
    static readonly int ID_PaperColor    = Shader.PropertyToID("_PaperColor");
    static readonly int ID_PencilColor   = Shader.PropertyToID("_PencilColor");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _col   = GetComponent<Collider>();
        _audio = GetComponent<AudioSource>();
        _audio.clip        = pencilSound;
        _audio.loop        = true;
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;

        if (paperRenderer == null) paperRenderer = GetComponentInChildren<Renderer>();
        if (playerCamera  == null) playerCamera  = Camera.main;
        if (playerLook    == null) playerLook    = FindObjectOfType<PlayerLook>();
        if (playerMotor   == null) playerMotor   = FindObjectOfType<PlayerMotor>();

        if (pencilSound == null)
            Debug.LogWarning("PaperPaintable: Pencil Sound not assigned.", this);
        if (cameraViewPoint == null)
            Debug.LogWarning("PaperPaintable: Camera View Point not assigned — inspect mode won't work.", this);
    }

    void Start()
    {
        _stampMat  = new Material(Shader.Find("Hidden/PaperReveal_Stamp"));
        _composeMat = new Material(Shader.Find("Hidden/PaperReveal_Compose"));

        var maskDesc = new RenderTextureDescriptor(
            renderTextureSize, renderTextureSize, RenderTextureFormat.R8, 0);
        _paintRT     = CreateCleared(maskDesc, Color.black);
        _paintSwapRT = CreateCleared(maskDesc, Color.black);

        var compDesc = new RenderTextureDescriptor(
            renderTextureSize, renderTextureSize, RenderTextureFormat.ARGB32, 0);
        _composedRT = new RenderTexture(compDesc) { name = "PaperComposed" };

        _composeMat.SetTexture(ID_WritingTex, writingTexture);
        _composeMat.SetColor(ID_PaperColor,  paperColor);
        _composeMat.SetColor(ID_PencilColor, pencilColor);

        var mat = paperRenderer.material;
        Texture existingBase = mat.HasProperty("_BaseColorMap")
            ? mat.GetTexture("_BaseColorMap")
            : mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        if (existingBase != null)
            _composeMat.SetTexture(ID_PaperTex, existingBase);

        Graphics.Blit(_paintRT, _composedRT, _composeMat);

        if (mat.HasProperty("_BaseColorMap"))
            mat.SetTexture("_BaseColorMap", _composedRT);
        else
            mat.SetTexture("_MainTex", _composedRT);
    }

    void OnDestroy()
    {
        Destroy(_stampMat);
        Destroy(_composeMat);
        _paintRT?.Release();
        _paintSwapRT?.Release();
        _composedRT?.Release();

        // Safety: restore player state if scene is unloaded mid-inspect
        SetPlayerFrozen(false);
    }

    // ── Main update ───────────────────────────────────────────────────────────

    void Update()
    {
        bool isVR = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
                 || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

        if (isVR)
        {
            UpdateVRDrawing();
            return;
        }

        switch (_state)
        {
            case PaperState.Idle:          UpdateIdle();                     break;
            case PaperState.TransitionIn:  UpdateTransition(entering: true); break;
            case PaperState.Inspecting:    UpdateInspecting();               break;
            case PaperState.TransitionOut: UpdateTransition(entering: false); break;
        }
    }

    // ── VR drawing (no inspect mode needed) ──────────────────────────────────

    void UpdateVRDrawing()
    {
        if (pencil == null || !pencil.IsHeld || pencil.Tip == null)
        {
            StopSound();
            return;
        }

        // Paper_writable uses a flat, non-convex MeshCollider, and Collider.ClosestPoint
        // is unsupported on those (it returns the input point unchanged, so the gap would
        // always read 0). Project the pencil tip onto the paper plane instead, then require
        // the perpendicular gap to be within pencilContactRadius and the contact to land on
        // the sheet. WorldToUV stays the single source of truth for the in-plane mapping.
        if (TipOnPaper(pencil.Tip.position, out float gap) && gap <= pencilContactRadius)
        {
            Stamp(WorldToUV(pencil.Tip.position));
            PlaySound();
        }
        else
        {
            StopSound();
        }
    }

    // True when worldPoint lies over the paper's rectangular surface (not just near its
    // infinite plane). 'gap' is the perpendicular distance from the surface, in world metres.
    bool TipOnPaper(Vector3 worldPoint, out float gap)
    {
        gap = float.MaxValue;

        Bounds  b     = paperRenderer.localBounds;
        Vector3 size  = b.size;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 scale = transform.lossyScale;

        // Thinnest local axis is the surface normal; the other two span the page.
        int normalAxis;
        if (size.x <= size.y && size.x <= size.z)      normalAxis = 0;
        else if (size.y <= size.x && size.y <= size.z) normalAxis = 1;
        else                                            normalAxis = 2;

        switch (normalAxis)
        {
            case 0:
                gap = Mathf.Abs(local.x - b.center.x) * Mathf.Abs(scale.x);
                return local.y >= b.min.y && local.y <= b.max.y
                    && local.z >= b.min.z && local.z <= b.max.z;
            case 1:
                gap = Mathf.Abs(local.y - b.center.y) * Mathf.Abs(scale.y);
                return local.x >= b.min.x && local.x <= b.max.x
                    && local.z >= b.min.z && local.z <= b.max.z;
            default:
                gap = Mathf.Abs(local.z - b.center.z) * Mathf.Abs(scale.z);
                return local.x >= b.min.x && local.x <= b.max.x
                    && local.y >= b.min.y && local.y <= b.max.y;
        }
    }

    // ── Idle: wait for player with pencil to approach ─────────────────────────

    void UpdateIdle()
    {
        if (pencil == null || !pencil.IsHeld)
        {
            HideHint();
            return;
        }

        float dist = Vector3.Distance(playerCamera.transform.position, transform.position);
        if (dist <= interactRange && LookingAt())
        {
            ShowHint("[E] Use paper", 1);
            if (Input.GetKeyDown(KeyCode.E))
                EnterInspect();
        }
        else
        {
            HideHint();
        }
    }

    // ── Inspect mode entry / exit ─────────────────────────────────────────────

    void EnterInspect()
    {
        if (cameraViewPoint == null) return;

        SetPlayerFrozen(true);

        // Detach camera so the frozen player doesn't drag it
        _savedCamParent   = playerCamera.transform.parent;
        _savedCamLocalPos = playerCamera.transform.localPosition;
        _savedCamLocalRot = playerCamera.transform.localRotation;
        playerCamera.transform.SetParent(null, worldPositionStays: true);

        _startCamPos  = playerCamera.transform.position;
        _startCamRot  = playerCamera.transform.rotation;
        _targetCamPos = cameraViewPoint.position;
        _targetCamRot = cameraViewPoint.rotation;
        _transitionT  = 0f;

        HideHint();
        _state = PaperState.TransitionIn;
    }

    void ExitInspect()
    {
        StopSound();
        HideHint();

        // Transition back to where camera was
        _startCamPos  = playerCamera.transform.position;
        _startCamRot  = playerCamera.transform.rotation;

        // Saved parent is frozen → its world transform hasn't changed
        _targetCamPos = _savedCamParent != null
            ? _savedCamParent.TransformPoint(_savedCamLocalPos)
            : _savedCamLocalPos;
        _targetCamRot = _savedCamParent != null
            ? _savedCamParent.rotation * _savedCamLocalRot
            : _savedCamLocalRot;

        _transitionT = 0f;
        _state = PaperState.TransitionOut;
    }

    // ── Smooth camera transition ──────────────────────────────────────────────

    void UpdateTransition(bool entering)
    {
        _transitionT += Time.deltaTime / Mathf.Max(transitionDuration, 0.01f);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_transitionT));

        playerCamera.transform.position = Vector3.Lerp(_startCamPos, _targetCamPos, t);
        playerCamera.transform.rotation = Quaternion.Slerp(_startCamRot, _targetCamRot, t);

        if (_transitionT < 1f) return;

        if (entering)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            ShowHint("[LMB] Draw    [RMB] Stop", 2);
            _state = PaperState.Inspecting;
        }
        else
        {
            // Re-parent camera and restore exact local pose
            playerCamera.transform.SetParent(_savedCamParent, worldPositionStays: true);
            playerCamera.transform.localPosition = _savedCamLocalPos;
            playerCamera.transform.localRotation = _savedCamLocalRot;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;

            SetPlayerFrozen(false);
            _state = PaperState.Idle;
        }
    }

    // ── Inspecting: mouse draws on paper, RMB exits ───────────────────────────

    void UpdateInspecting()
    {
        if (Input.GetMouseButtonDown(1))
        {
            ExitInspect();
            return;
        }

        if (Input.GetMouseButton(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == _col)
            {
                Stamp(WorldToUV(hit.point));
                PlaySound();
                return;
            }
        }

        StopSound();
    }

    // ── Painting ──────────────────────────────────────────────────────────────

    void Stamp(Vector2 uv)
    {
        _stampMat.SetVector(ID_BrushUV,      new Vector4(uv.x, uv.y, 0, 0));
        _stampMat.SetFloat(ID_BrushSize,     brushSizeUV);
        _stampMat.SetFloat(ID_BrushSoftness, brushSoftness);
        Graphics.Blit(_paintRT, _paintSwapRT, _stampMat);
        (_paintRT, _paintSwapRT) = (_paintSwapRT, _paintRT);

        Graphics.Blit(_paintRT, _composedRT, _composeMat);

        float brushArea = Mathf.PI * brushSizeUV * brushSizeUV;
        _coverage = Mathf.Clamp01(_coverage + brushArea * (1f - _coverage * 0.85f));

        if (!_revealFired && _coverage >= revealThreshold)
        {
            _revealFired = true;
            onRevealComplete.Invoke();
        }
    }

    // ── UV projection ─────────────────────────────────────────────────────────

    Vector2 WorldToUV(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Bounds  b     = paperRenderer.localBounds;
        Vector3 size  = b.size;

        int normalAxis;
        if (size.x <= size.y && size.x <= size.z)      normalAxis = 0;
        else if (size.y <= size.x && size.y <= size.z) normalAxis = 1;
        else                                            normalAxis = 2;

        float u, v;
        switch (normalAxis)
        {
            case 0:
                u = Mathf.InverseLerp(b.min.y, b.max.y, local.y);
                v = Mathf.InverseLerp(b.min.z, b.max.z, local.z);
                break;
            case 1:
                u = Mathf.InverseLerp(b.min.x, b.max.x, local.x);
                v = Mathf.InverseLerp(b.min.z, b.max.z, local.z);
                break;
            default:
                u = Mathf.InverseLerp(b.min.x, b.max.x, local.x);
                v = Mathf.InverseLerp(b.min.y, b.max.y, local.y);
                break;
        }
        return new Vector2(u, v);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetPlayerFrozen(bool frozen)
    {
        if (playerLook  != null) playerLook.LookEnabled   = !frozen;
        if (playerMotor != null) playerMotor.MoveEnabled  = !frozen;
    }

    void PlaySound()  { if (!_audio.isPlaying) _audio.Play(); }
    void StopSound()  { if (_audio.isPlaying)  _audio.Stop(); }

    bool LookingAt()
    {
        if (playerCamera == null) return false;
        Vector3 dir = (transform.position - playerCamera.transform.position).normalized;
        return Vector3.Dot(dir, playerCamera.transform.forward) > 0.5f;
    }

    void ShowHint(string msg, int priority = 1)
    {
        if (HintManager.Instance != null) HintManager.Instance.Show(this, msg, priority);
    }

    void HideHint()
    {
        if (HintManager.Instance != null) HintManager.Instance.Hide(this);
    }

    static RenderTexture CreateCleared(RenderTextureDescriptor desc, Color clearColor)
    {
        var rt   = new RenderTexture(desc);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, clearColor);
        RenderTexture.active = prev;
        return rt;
    }
}
