using System;

namespace ChessButWeird.Domain
{
    /// <summary>
    /// Maps the board coordinates shown by the client to canonical chess coordinates.
    /// A Black-facing local board is rank-flipped; the pieces keep their canonical teams
    /// and movement rules inside the domain.
    /// </summary>
    public readonly struct BoardOrientation : IEquatable<BoardOrientation>
    {
        public readonly Team FrontTeam;

        public BoardOrientation(Team frontTeam)
        {
            FrontTeam = frontTeam;
        }

        public bool IsRankFlipped => FrontTeam == Team.Black;

        public Square ToCanonical(Square runtime)
        {
            if (!runtime.IsValid || !IsRankFlipped)
                return runtime;
            return new Square(runtime.File, 7 - runtime.Rank);
        }

        public Square ToRuntime(Square canonical)
        {
            if (!canonical.IsValid || !IsRankFlipped)
                return canonical;
            return new Square(canonical.File, 7 - canonical.Rank);
        }

        public int ToRuntimeForward(int canonicalForward) => IsRankFlipped ? -canonicalForward : canonicalForward;

        public bool Equals(BoardOrientation other) => FrontTeam == other.FrontTeam;
        public override bool Equals(object obj) => obj is BoardOrientation other && Equals(other);
        public override int GetHashCode() => (int)FrontTeam;
        public static bool operator ==(BoardOrientation left, BoardOrientation right) => left.Equals(right);
        public static bool operator !=(BoardOrientation left, BoardOrientation right) => !left.Equals(right);
    }
}
