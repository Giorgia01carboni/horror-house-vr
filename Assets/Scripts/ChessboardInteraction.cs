using System.Collections;
using UnityEngine;
using TMPro;

public class ChessboardInteraction : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] float triggerRange   = 2.2f;

    [Header("Camera view")]
    [SerializeField] float cameraHeight   = 0.40f;
    [SerializeField] float transitionTime = 0.5f;

    // Auto-found
    PlayerLook  _playerLook;
    PlayerMotor _playerMotor;
    Camera      _cam;
    Transform   _board;   // LowPolyConcrete

    enum State { Idle, TransitionIn, Inspecting, TransitionOut }
    State _state = State.Idle;

    bool _dialoguePlayed;
    bool _dialoguePlaying;

    // Camera transition (shared in/out)
    Vector3    _startPos, _targetPos;
    Quaternion _startRot, _targetRot;
    Transform  _savedCamParent;
    Vector3    _savedCamLocalPos;
    Quaternion _savedCamLocalRot;
    float      _transT;

    // Piece selection (KB inspect mode)
    GrabbableChessPiece _selected;
    Rigidbody           _selectedRb;
    const float         SelectRaise   = 0.05f;
    float               _boardSurfaceY;
    bool                _enigmaSolved;

    // Board reset snapshot
    struct PieceSnapshot
    {
        public Transform  t;
        public Transform  parent;
        public Vector3    localPos;
        public Quaternion localRot;
    }
    PieceSnapshot[] _pieceSnapshots;

    // Dialogue UI
    TextMeshProUGUI _dialogueTMP;
    TextMeshProUGUI _qHintText;
    const string DialogueLine = "Yeah, I'm sure I can find a decent guy to play with me right now...";

    bool IsVR => OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
              || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    public static bool IsInspecting { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _playerLook  = FindObjectOfType<PlayerLook>();
        _playerMotor = FindObjectOfType<PlayerMotor>();
        _cam         = _playerLook?.cam ?? Camera.main;
        _board       = transform.Find("LowPolyConcrete");

        BuildDialogueUI();
        CapturePieceSnapshots();
    }

    void CapturePieceSnapshots()
    {
        var pieces = GetComponentsInChildren<GrabbableChessPiece>(includeInactive: true);
        _pieceSnapshots = new PieceSnapshot[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            var t = pieces[i].transform;
            _pieceSnapshots[i] = new PieceSnapshot
            {
                t        = t,
                parent   = t.parent,
                localPos = t.localPosition,
                localRot = t.localRotation,
            };
        }
    }

    void RestorePieces()
    {
        if (_pieceSnapshots == null) return;
        StartCoroutine(RestorePiecesCoroutine());
    }

    IEnumerator RestorePiecesCoroutine()
    {
        // Pass 1: freeze everything and reposition
        foreach (var s in _pieceSnapshots)
        {
            if (s.t == null) continue;
            var rb = s.t.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            if (s.t.parent != s.parent) s.t.SetParent(s.parent, worldPositionStays: false);
            s.t.localPosition = s.localPos;
            s.t.localRotation = s.localRot;
        }

        yield return new WaitForFixedUpdate(); // let physics register new positions

        // Pass 2: sync Rigidbody internals and release
        foreach (var s in _pieceSnapshots)
        {
            if (s.t == null) continue;
            var rb = s.t.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position    = s.t.position;
                rb.rotation    = s.t.rotation;
                rb.isKinematic = false;
            }
        }
    }

    void Update()
    {
        switch (_state)
        {
            case State.Idle:          UpdateIdle();           break;
            case State.TransitionIn:  UpdateTransitionIn();   break;
            case State.Inspecting:    UpdateInspecting();     break;
            case State.TransitionOut: UpdateTransitionOut();  break;
        }
    }

    // ── Idle ─────────────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        if (_cam == null) return;

        Vector3 boardPos = _board != null ? _board.position : transform.position;

        Vector3 camXZ = new Vector3(_cam.transform.position.x, boardPos.y, _cam.transform.position.z);
        float   dist  = Vector3.Distance(camXZ, boardPos);

        if (dist > triggerRange)
        {
            HintManager.Instance?.Hide(this);
            return;
        }

        Ray  ray       = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool lookingAt = Physics.Raycast(ray, out RaycastHit hit, triggerRange + 2f)
                      && (hit.transform == transform || hit.transform.IsChildOf(transform));

        if (!_dialoguePlayed && lookingAt)
        {
            _dialoguePlayed = true;
            StartCoroutine(PlayDialogue());
            return;
        }

        if (_dialoguePlayed && !_dialoguePlaying && lookingAt && !IsVR && !VRRevolver.GunIsHeld)
        {
            HintManager.Instance?.Show(this, "[E] Play chess", 2);
            if (Input.GetKeyDown(KeyCode.E))
                BeginShift();
        }
        else
        {
            HintManager.Instance?.Hide(this);
        }
    }

    // ── Camera shift in ───────────────────────────────────────────────────────

    void BeginShift()
    {
        if (_cam == null || _board == null) return;
        HintManager.Instance?.Hide(this);
        SetPlayerFrozen(true);

        _savedCamParent   = _cam.transform.parent;
        _savedCamLocalPos = _cam.transform.localPosition;
        _savedCamLocalRot = _cam.transform.localRotation;
        _cam.transform.SetParent(null, worldPositionStays: true);

        _startPos = _cam.transform.position;
        _startRot = _cam.transform.rotation;

        Vector3 surface = _board.position;
        surface.y      += 0.012f;
        _boardSurfaceY  = surface.y;

        _targetPos = new Vector3(surface.x, surface.y + cameraHeight, surface.z);
        _targetRot = Quaternion.Euler(90f, _board.eulerAngles.y, 0f);

        _transT = 0f;
        _state  = State.TransitionIn;
    }

    void UpdateTransitionIn()
    {
        _transT += Time.deltaTime / Mathf.Max(transitionTime, 0.01f);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_transT));

        _cam.transform.position = Vector3.Lerp(_startPos, _targetPos, t);
        _cam.transform.rotation = Quaternion.Slerp(_startRot, _targetRot, t);

        if (_transT >= 1f)
        {
            IsInspecting     = true;
            _state           = State.Inspecting;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
            if (_qHintText != null) _qHintText.gameObject.SetActive(true);
        }
    }

    // ── Inspecting ────────────────────────────────────────────────────────────

    void UpdateInspecting()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            DeselectPiece();
            BeginExit();
            return;
        }

        if (_enigmaSolved) return;

        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    void HandleClick()
    {
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

        // Try to hit a piece first — exclude the selected piece (it's raised, sits closer to camera)
        GrabbableChessPiece hitPiece = null;
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 10f))
        {
            var candidate = hit.collider.GetComponentInParent<GrabbableChessPiece>();
            if (candidate != _selected) hitPiece = candidate;
        }

        if (hitPiece != null)
        {
            if (_selected == null)
            {
                SelectPiece(hitPiece);
            }
            else if (hitPiece == _selected)
            {
                DeselectPiece();
            }
            else
            {
                // Different piece clicked while one is already selected
                if (_selected.name == "rookHighPoly" && hitPiece.name == "KingHighPoly 2")
                {
                    TriggerKBEnigma(_selected, hitPiece);
                }
                else
                {
                    // Move selected piece onto the target's square, bump the target away
                    Vector3 bumpDir = hitPiece.transform.position - _selected.transform.position;
                    bumpDir.y = 0f;
                    if (bumpDir.sqrMagnitude > 0.001f) bumpDir.Normalize();
                    else bumpDir = Vector3.forward;

                    // Use the target piece's Y so the pivot lands at the correct board height
                    Vector3 dest   = new Vector3(hitPiece.transform.position.x, hitPiece.transform.position.y, hitPiece.transform.position.z);
                    var savedPiece = _selected;
                    var savedRb    = _selectedRb;
                    _selected      = null;
                    _selectedRb    = null;
                    TeleportPiece(savedPiece, savedRb, dest);

                    var targetRb = hitPiece.GetComponent<Rigidbody>();
                    if (targetRb != null)
                    {
                        targetRb.isKinematic = false;
                        targetRb.WakeUp();
                        targetRb.AddForce(bumpDir * 1.5f + Vector3.up * 0.2f, ForceMode.Impulse);
                    }
                }
            }
            return;
        }

        // No piece hit — move selected piece to board plane
        if (_selected != null)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, _boardSurfaceY, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 dest   = ray.GetPoint(enter);
                var savedPiece = _selected;
                var savedRb    = _selectedRb;
                _selected      = null;
                _selectedRb    = null;
                TeleportPiece(savedPiece, savedRb, new Vector3(dest.x, _boardSurfaceY, dest.z));
            }
        }
    }

    void TeleportPiece(GrabbableChessPiece piece, Rigidbody rb, Vector3 dest)
    {
        piece.transform.position = dest;
        if (rb != null)
        {
            rb.isKinematic        = false;
            rb.position           = dest;
            rb.velocity           = Vector3.zero;
            rb.angularVelocity    = Vector3.zero;
        }
    }

    void SelectPiece(GrabbableChessPiece piece)
    {
        DeselectPiece();
        _selected   = piece;
        _selectedRb = piece.GetComponent<Rigidbody>();
        if (_selectedRb != null) _selectedRb.isKinematic = true;
        _selected.transform.position += Vector3.up * SelectRaise;
    }

    void DeselectPiece()
    {
        if (_selected == null) return;
        _selected.transform.position -= Vector3.up * SelectRaise;
        if (_selectedRb != null) _selectedRb.isKinematic = false;
        _selected   = null;
        _selectedRb = null;
    }

    void TriggerKBEnigma(GrabbableChessPiece rook, GrabbableChessPiece king)
    {
        _enigmaSolved = true;

        // Lower rook and place it at the king's XZ
        _selected   = null;
        _selectedRb = null;
        if (_selectedRb != null) _selectedRb.isKinematic = false; // already cleared above
        rook.transform.position = new Vector3(
            king.transform.position.x,
            _boardSurfaceY,
            king.transform.position.z);
        var rookRb = rook.GetComponent<Rigidbody>();
        if (rookRb != null) rookRb.isKinematic = false;

        // Knock the king over with a physics impulse
        var kingRb = king.GetComponent<Rigidbody>();
        if (kingRb != null)
        {
            kingRb.isKinematic = false;
            kingRb.WakeUp();
            kingRb.AddForce(new Vector3(
                Random.Range(-0.3f, 0.3f), 0.8f, Random.Range(-0.3f, 0.3f)),
                ForceMode.Impulse);
            kingRb.AddTorque(new Vector3(
                Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * 2f,
                ForceMode.Impulse);
        }

        FindObjectOfType<ChessEnigma>()?.TriggerKB();
        StartCoroutine(AutoExitAfter(1.5f));
    }

    IEnumerator AutoExitAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginExit();
    }

    // ── Camera shift out ──────────────────────────────────────────────────────

    void BeginExit()
    {
        if (!_enigmaSolved) RestorePieces();
        if (_qHintText != null) _qHintText.gameObject.SetActive(false);
        HintManager.Instance?.Hide(this);

        IsInspecting     = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        _startPos = _cam.transform.position;
        _startRot = _cam.transform.rotation;

        if (_savedCamParent != null)
        {
            _targetPos = _savedCamParent.TransformPoint(_savedCamLocalPos);
            _targetRot = _savedCamParent.rotation * _savedCamLocalRot;
        }
        else
        {
            _targetPos = _startPos;
            _targetRot = _startRot;
        }

        _transT = 0f;
        _state  = State.TransitionOut;
    }

    void UpdateTransitionOut()
    {
        _transT += Time.deltaTime / Mathf.Max(transitionTime, 0.01f);
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_transT));

        _cam.transform.position = Vector3.Lerp(_startPos, _targetPos, t);
        _cam.transform.rotation = Quaternion.Slerp(_startRot, _targetRot, t);

        if (_transT >= 1f)
        {
            if (_savedCamParent != null)
            {
                _cam.transform.SetParent(_savedCamParent, worldPositionStays: true);
                _cam.transform.localPosition = _savedCamLocalPos;
                _cam.transform.localRotation = _savedCamLocalRot;
            }
            SetPlayerFrozen(false);
            _state = State.Idle;
        }
    }

    // ── Dialogue ─────────────────────────────────────────────────────────────

    IEnumerator PlayDialogue()
    {
        _dialoguePlaying = true;

        // VR: the overlay TMP is invisible in the headset, so route the line through the
        // world-space HintManager (same path as every other VR-visible hint).
        if (IsVR)
        {
            HintManager.Instance?.Show(this, DialogueLine, 3);
            yield return new WaitForSeconds(2.5f);
            HintManager.Instance?.Hide(this);
            _dialoguePlaying = false;
            yield break;
        }

        if (_dialogueTMP == null) { _dialoguePlaying = false; yield break; }
        _dialogueTMP.gameObject.SetActive(true);

        yield return StartCoroutine(FadeDialogue(0f, 1f, 0.4f));
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(FadeDialogue(1f, 0f, 0.6f));

        _dialogueTMP.gameObject.SetActive(false);
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

    // ── UI ───────────────────────────────────────────────────────────────────

    void BuildDialogueUI()
    {
        Canvas canvas = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { canvas = c; break; }
        if (canvas == null) return;

        var go = new GameObject("Chess_DialogueText");
        go.transform.SetParent(canvas.transform, false);

        _dialogueTMP = go.AddComponent<TextMeshProUGUI>();

        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) _dialogueTMP.font = font;

        _dialogueTMP.text          = DialogueLine;
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

        // Q hint anchored to bottom-left corner
        var qGo = new GameObject("Chess_QHint");
        qGo.transform.SetParent(canvas.transform, false);
        _qHintText = qGo.AddComponent<TextMeshProUGUI>();
        if (font != null) _qHintText.font = font;
        _qHintText.text          = "[Q] Exit chess";
        _qHintText.fontSize      = 18f;
        _qHintText.color         = Color.white;
        _qHintText.alignment     = TextAlignmentOptions.BottomLeft;
        _qHintText.raycastTarget = false;
        var qRt = _qHintText.rectTransform;
        qRt.anchorMin        = Vector2.zero;
        qRt.anchorMax        = Vector2.zero;
        qRt.pivot            = Vector2.zero;
        qRt.anchoredPosition = new Vector2(20f, 20f);
        qRt.sizeDelta        = new Vector2(220f, 30f);
        qGo.SetActive(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void SetPlayerFrozen(bool frozen)
    {
        if (_playerLook  != null) _playerLook.LookEnabled  = !frozen;
        if (_playerMotor != null) _playerMotor.MoveEnabled = !frozen;
    }
}
