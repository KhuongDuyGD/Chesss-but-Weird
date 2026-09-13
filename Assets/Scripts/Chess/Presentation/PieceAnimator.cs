using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns piece motion only. The match remains responsible for input and turn state.</summary>
internal sealed class PieceAnimator
{
    private readonly MonoBehaviour owner;
    private readonly Dictionary<ChessPiece, Coroutine> running = new Dictionary<ChessPiece, Coroutine>();

    public PieceAnimator(MonoBehaviour owner) { this.owner = owner; }

    public void Play(ChessPiece piece, Vector3 target, float duration, float arcHeight)
    {
        if (!piece) return;
        Cancel(piece);
        if (duration <= 0f)
        {
            piece.transform.position = target;
            return;
        }
        piece.Destroyed += HandleDestroyed;
        running[piece] = owner.StartCoroutine(Animate(piece, target, duration, arcHeight));
    }

    public void Cancel(ChessPiece piece)
    {
        if (ReferenceEquals(piece, null)) return;
        if (running.TryGetValue(piece, out Coroutine routine))
        {
            if (owner && routine != null) owner.StopCoroutine(routine);
            running.Remove(piece);
        }
        piece.Destroyed -= HandleDestroyed;
    }

    public void CancelAll()
    {
        foreach (var entry in running)
        {
            if (owner && entry.Value != null) owner.StopCoroutine(entry.Value);
            entry.Key.Destroyed -= HandleDestroyed;
        }
        running.Clear();
    }

    private void HandleDestroyed(ChessPiece piece) { Cancel(piece); }

    private IEnumerator Animate(ChessPiece piece, Vector3 target, float duration, float arcHeight)
    {
        Vector3 start = piece.transform.position;
        float elapsed = 0f;
        while (piece && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float eased = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(start, target, eased);
            position.y += Mathf.Sin(eased * Mathf.PI) * arcHeight;
            piece.transform.position = position;
            yield return null;
        }
        if (piece) piece.transform.position = target;
        running.Remove(piece);
        piece.Destroyed -= HandleDestroyed;
    }
}
