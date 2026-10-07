using System.Linq;
using UnityEngine;

public partial class ChessGame
{
    internal int PrisonerCount(PieceTeam capturer, PieceType kind)
    {
        if (dotNetState?.captures != null)
            return dotNetState.captures.Count(p => p.capturedBy == capturer.ToString() && p.kind == kind.ToString());
        return GetCapturedPieces(capturer).Count(p => p == kind);
    }
    internal ChessPiece CreatePrisonerPiece(PieceType kind, PieceTeam victim, Transform prisonRoot, Vector3 pad)
    {
        var piece = CreateCosmeticPiece(kind, victim, Vector2Int.zero);
        piece.transform.SetParent(prisonRoot, true);
        piece.name = victim + " " + kind + " prisoner";
        piece.Initialize(victim, new Vector2Int(-1, -1), victim == PieceTeam.White ? 1 : -1);
        piece.enabled = false;
        foreach (var collider in piece.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var child in piece.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2; // Ignore Raycast
        Bounds bounds = default; bool found = false;
        foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        float tile = Vector3.Distance(chessboard.GetTileCenterWorld(Vector2Int.zero), chessboard.GetTileCenterWorld(Vector2Int.right));
        if (found)
        {
            float fit = Mathf.Min(1f, tile * .53f / Mathf.Max(.001f, bounds.size.x), tile * .70f / Mathf.Max(.001f, bounds.size.z), tile * .70f / Mathf.Max(.001f, bounds.size.y));
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - piece.transform.position;
            piece.transform.localScale *= fit;
            piece.transform.position = pad - bottom * fit;
        }
        else piece.transform.position = pad;
        ChessModelRendering.ApplySettings(piece.gameObject);
        return piece;
    }
}
