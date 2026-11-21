using System;

// Exact protocol tokens; no catalog registration, artwork approval or economic grant.
public static class CosmeticItemKinds
{
    public const string Set = "CHESS_PIECE_SKIN", Board = "BOARD_SKIN", Background = "BACKGROUND_SKIN";
    // Backend-only catalog records explicitly identified by the owner as having no Unity artwork.
    public static bool KnownMissingCode(string code) => code == "CHESS_SHADOW_KNIGHT" || code == "BOARD_VOLCANO" || code == "CHESS_DRAGON_RED";
    public static string Slot(PieceTeam team, PieceType piece) => team.ToString().ToUpperInvariant() + "_" + piece.ToString().ToUpperInvariant();
    public static string ItemType(PieceTeam team, PieceType piece) => "CHESS_" + Slot(team, piece) + "_SKIN";
    public static bool TryPiece(string type, out PieceTeam team, out PieceType piece)
    {
        foreach (PieceTeam side in new[] { PieceTeam.White, PieceTeam.Black })
            foreach (PieceType kind in Enum.GetValues(typeof(PieceType)))
                if (type == ItemType(side, kind)) { team = side; piece = kind; return true; }
        team = default; piece = default; return false;
    }
    public static bool IsPiece(string type) => type == Set || TryPiece(type, out _, out _);
    public static bool TrySlot(string slot, out PieceTeam team, out PieceType piece) => TryPiece("CHESS_" + slot + "_SKIN", out team, out piece);
}

[Serializable]
public sealed class PieceCosmeticChoice
{
    public string slot, skinId, itemId;
    public PieceCosmeticChoice Copy() => new PieceCosmeticChoice { slot = slot, skinId = skinId, itemId = itemId };
}
