using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Chess/Cosmetics/Piece Skin")]
public sealed class PieceSkinData : ScriptableObject
{
    public string skinId = CosmeticSelection.LowPolyId;
    public string displayName = "Tazji's Low Poly Chess Set";
    public AssetReferenceSprite preview;
    public AssetReferenceGameObject pawn;
    public AssetReferenceGameObject rook;
    public AssetReferenceGameObject knight;
    public AssetReferenceGameObject bishop;
    public AssetReferenceGameObject queen;
    public AssetReferenceGameObject king;
    [Header("Black side overrides (optional)")]
    public AssetReferenceGameObject blackPawn;
    public AssetReferenceGameObject blackRook;
    public AssetReferenceGameObject blackKnight;
    public AssetReferenceGameObject blackBishop;
    public AssetReferenceGameObject blackQueen;
    public AssetReferenceGameObject blackKing;
    public bool applyTeamColor = true;
    public Color whiteColor = new Color(0.94f, 0.91f, 0.82f);
    public Color blackColor = new Color(0.18f, 0.21f, 0.27f);
    public List<PieceSkinTuning> tuning = new List<PieceSkinTuning>();

    public AssetReferenceGameObject GetPrefab(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return pawn;
            case PieceType.Rook: return rook;
            case PieceType.Knight: return knight;
            case PieceType.Bishop: return bishop;
            case PieceType.Queen: return queen;
            case PieceType.King: return king;
            default: return null;
        }
    }

    public AssetReferenceGameObject GetPrefab(PieceType type, PieceTeam team)
    {
        if (team == PieceTeam.Black)
        {
            AssetReferenceGameObject reference = null;
            switch (type)
            {
                case PieceType.Pawn: reference = blackPawn; break;
                case PieceType.Rook: reference = blackRook; break;
                case PieceType.Knight: reference = blackKnight; break;
                case PieceType.Bishop: reference = blackBishop; break;
                case PieceType.Queen: reference = blackQueen; break;
                case PieceType.King: reference = blackKing; break;
            }
            if (reference != null && reference.RuntimeKeyIsValid()) return reference;
        }
        return GetPrefab(type);
    }

    public PieceSkinPieceTuning GetTuning(PieceType type)
    {
        if (tuning != null)
            foreach (var item in tuning)
                if (item.pieceType == type)
                    return new PieceSkinPieceTuning(item.rotationEuler, item.heightMultiplier, item.maxFootprintMultiplier);
        return PieceSkinPieceTuning.Default;
    }
}

[Serializable]
public struct PieceSkinTuning
{
    public PieceType pieceType;
    public Vector3 rotationEuler;
    [Min(0.01f)] public float heightMultiplier;
    [Min(0.01f)] public float maxFootprintMultiplier;
}
