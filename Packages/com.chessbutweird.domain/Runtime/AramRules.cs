using System;

namespace ChessButWeird.Domain
{
    [Flags]
    public enum AramBuffs
    {
        None = 0, CommandantPawn = 1, StrongFortress = 2, FreestyleLeap = 4,
        Doppelganger = 8, SuicideBomber = 16, FlyingThunderGod = 32
    }

    /// <summary>Per-piece projection of match-owned buff state. No scene object references.</summary>
    public readonly struct AramPieceContext
    {
        public readonly AramBuffs Buffs;
        public readonly bool IsCommandantPawn, IsSwappedKnight, IsSwappedBishop, IsOriginalQueen, CanTeleport;
        public AramPieceContext(AramBuffs buffs, bool commandant = false, bool swappedKnight = false,
            bool swappedBishop = false, bool originalQueen = false, bool canTeleport = false)
        { Buffs = buffs; IsCommandantPawn = commandant; IsSwappedKnight = swappedKnight;
          IsSwappedBishop = swappedBishop; IsOriginalQueen = originalQueen; CanTeleport = canTeleport; }
        public bool Has(AramBuffs buff) => (Buffs & buff) != 0;
    }

    public interface IAramMovementBuff
    {
        bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext context, bool attack)
            where TBoard : IReadOnlyBoard;
    }

    public sealed class CommandantPawnBuff : IAramMovementBuff
    {
        public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext c, bool attack)
            where TBoard : IReadOnlyBoard => !attack && c.Has(AramBuffs.CommandantPawn) && c.IsCommandantPawn &&
                piece.Kind == PieceKind.Pawn && to.File == from.File && to.Rank - from.Rank == piece.Forward * 2 &&
                board.GetPiece(new Square(from.File, from.Rank + piece.Forward)).IsEmpty && board.GetPiece(to).IsEmpty;
    }

    public sealed class FreestyleLeapBuff : IAramMovementBuff
    {
        public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext c, bool attack)
            where TBoard : IReadOnlyBoard => c.Has(AramBuffs.FreestyleLeap) && piece.Kind == PieceKind.Knight &&
                (MovementRules.IsKnightPattern(from, to) ||
                 (Math.Abs(to.File - from.File) == 2 && Math.Abs(to.Rank - from.Rank) == 2));
    }

    public sealed class DoppelgangerBuff : IAramMovementBuff
    {
        public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext c, bool attack)
            where TBoard : IReadOnlyBoard
        {
            if (!c.Has(AramBuffs.Doppelganger)) return false;
            if (c.IsSwappedKnight) return Math.Abs(to.File - from.File) == Math.Abs(to.Rank - from.Rank) && MovementRules.IsPathClear(board, from, to);
            return c.IsSwappedBishop && MovementRules.IsKnightPattern(from, to);
        }
    }

    public sealed class FlyingThunderGodBuff : IAramMovementBuff
    {
        // ARAM-V1 wire contract. Keep these values aligned with the current Spring backend.
        public const int MaximumUses = 5;
        public const int CooldownTurns = 5;
        public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext c, bool attack)
            where TBoard : IReadOnlyBoard => !attack && c.Has(AramBuffs.FlyingThunderGod) && c.IsOriginalQueen &&
                c.CanTeleport && board.GetPiece(to).IsEmpty;
    }

    /// <summary>Movement extension registry. Simulation and live queries call the same policies.</summary>
    public sealed class AramRules
    {
        public static readonly AramRules BuiltIn = new AramRules(new CommandantPawnBuff(), new FreestyleLeapBuff(),
            new DoppelgangerBuff(), new FlyingThunderGodBuff());
        private readonly IAramMovementBuff[] movement;
        public AramRules(params IAramMovementBuff[] movement)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            this.movement = (IAramMovementBuff[])movement.Clone();
            foreach (var buff in this.movement) if (buff == null) throw new ArgumentException("Null buff.", nameof(movement));
        }
        public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext context, bool attack = false)
            where TBoard : IReadOnlyBoard
        {
            if (piece.IsEmpty || !from.IsValid || !to.IsValid || from == to) return false;
            PieceState target = board.GetPiece(to);
            if (!attack && !target.IsEmpty && target.Team == piece.Team) return false;
            foreach (var buff in movement) if (buff.Allows(board, piece, from, to, context, attack)) return true;
            return false;
        }
        public static bool SuppressesStandardMovement(AramPieceContext context) =>
            context.Has(AramBuffs.Doppelganger) && (context.IsSwappedKnight || context.IsSwappedBishop);
    }

    public static class StrongFortressBuff
    {
        public static bool WaivesCastleAttackChecks(AramBuffs buffs) => (buffs & AramBuffs.StrongFortress) != 0;
    }

    public static class SuicideBomberBuff
    {
        public static bool IsVictim(PieceState victim, int capturingPieceId, Square center, Square square) =>
            !victim.IsEmpty && victim.Kind != PieceKind.King && victim.Id != capturingPieceId && center != square &&
            Math.Abs(center.File - square.File) <= 1 && Math.Abs(center.Rank - square.Rank) <= 1;
    }
}
