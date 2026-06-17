using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ChessPieceSetup
{
    [MenuItem("Tools/Setup Chess Pieces")]
    static void Setup()
    {
        var chessSet = GameObject.Find("Chess Set");
        if (chessSet == null) { Debug.LogError("[ChessPieceSetup] 'Chess Set' not found in scene."); return; }

        SetupBoard(chessSet.transform.Find("LowPolyConcrete"));

        var onBoard = chessSet.transform.Find("on_board");
        if (onBoard != null)
        {
            foreach (Transform child in onBoard)
                SetupPiece(child);
        }

        foreach (Transform child in chessSet.transform)
        {
            if (child.name == "LowPolyConcrete" || child.name == "on_board") continue;
            SetupPiece(child);
        }

        EditorUtility.SetDirty(chessSet);
        EditorSceneManager.MarkSceneDirty(chessSet.scene);
        Debug.Log("[ChessPieceSetup] Done. Save the scene to persist changes.");
    }

    static void SetupBoard(Transform board)
    {
        if (board == null) return;
        foreach (MeshFilter mf in board.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<MeshCollider>() != null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }
    }

    static void SetupPiece(Transform piece)
    {
        foreach (MeshFilter mf in piece.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<MeshCollider>() != null) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
        }

        var rb = piece.GetComponent<Rigidbody>();
        if (rb == null) rb = piece.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.02f;
        rb.drag = 2f;
        rb.angularDrag = 2f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (piece.GetComponent<GrabbableChessPiece>() == null)
            piece.gameObject.AddComponent<GrabbableChessPiece>();
    }
}
