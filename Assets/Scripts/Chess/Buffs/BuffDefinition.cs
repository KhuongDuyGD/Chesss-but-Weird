using System;
using UnityEngine;

namespace ChessButWeird
{
    // ─────────────────────── Buff effect catalogue ─────────────────────────────

    public enum BuffRarity { Silver, Gold, Diamond }

    public enum BuffEffectType
    {
        // ── Silver ──────────────────────────────────────────────────────────────
        ShieldPiece,            // 1 quân được bảo vệ (không bị ăn 1 lần)
        ExtraMovementRange,     // +N ô di chuyển cho loại quân chỉ định
        SeeOpponentNextMove,    // Xem trước nước đi tiếp của đối thủ 1 lần

        // ── Gold ─────────────────────────────────────────────────────────────────
        ReviveLastCaptured,     // Hồi sinh quân vừa bị ăn lần gần nhất
        SwapTwoPieces,          // Hoán đổi vị trí 2 quân cùng màu
        EarlyPromotion,         // Tốt thăng cấp ở rank 5 thay vì rank 8

        // ── Diamond ──────────────────────────────────────────────────────────────
        GhostMove,              // Di chuyển xuyên quân khác 1 lần
        ExtraCaptureTurn,       // Ăn thêm 1 quân trong cùng lượt đi
        CopyLastCapturedAbility // Quân Vua sao chép khả năng di chuyển quân vừa bị ăn
    }

    // ─────────────────────── ScriptableObject definition ───────────────────────

    [CreateAssetMenu(fileName = "NewBuff", menuName = "Chess But Weird/Buff Definition")]
    public class BuffDefinition : ScriptableObject
    {
        [Header("Metadata")]
        public string         BuffName;
        [TextArea(2, 5)]
        public string         Description;
        public BuffRarity     Rarity;
        public BuffEffectType EffectType;
        public Sprite         Icon;

        [Header("Duration")]
        [Tooltip("-1 = permanent until game ends")]
        public int  DurationMoves = -1;

        [Header("Parameters")]
        [Tooltip("Generic integer parameter (e.g. extra range squares, quantity)")]
        public int  IntParam  = 1;
        [Tooltip("Target piece type for effects that restrict to one piece type. None = any.")]
        public PieceType TargetPieceType = PieceType.None;
    }

    // ─────────────────────── Runtime instance of a buff ────────────────────────

    [Serializable]
    public class ActiveBuff
    {
        public BuffDefinition Definition;
        public PieceColor     Owner;
        public int            AppliedOnMove;  // Full-move number when granted
        public bool           IsConsumed;     // One-shot buffs mark this after use

        public bool IsPermanent => Definition.DurationMoves < 0;

        public bool IsExpired(int currentMove) =>
            !IsPermanent && (currentMove - AppliedOnMove) >= Definition.DurationMoves;

        public bool IsActive(int currentMove) =>
            !IsConsumed && !IsExpired(currentMove);

        public override string ToString() =>
            $"[{Definition.Rarity}] {Definition.BuffName} ({Owner}, consumed={IsConsumed})";
    }
}