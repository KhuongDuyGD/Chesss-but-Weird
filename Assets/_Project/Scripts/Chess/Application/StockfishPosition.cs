using System;
using System.Collections.Generic;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    /// <summary>Immutable root FEN plus the accepted UCI moves that lead to the current position.</summary>
    public sealed class StockfishPosition
    {
        public string InitialFen { get; }
        public string Fen { get; }
        public IReadOnlyList<string> Moves { get; }
        public string UciCommand => "position fen " + InitialFen +
            (Moves.Count == 0 ? string.Empty : " moves " + string.Join(" ", Moves));

        private StockfishPosition(string initialFen, string fen, string[] moves)
        {
            InitialFen = initialFen; Fen = fen; Moves = Array.AsReadOnly(moves);
        }

        public static StockfishPosition FromFen(string fen)
        {
            string canonical = FenCodec.Write(FenCodec.Parse(fen));
            return new StockfishPosition(canonical, canonical, Array.Empty<string>());
        }

        public StockfishPosition Play(Move move)
        {
            if (!ClassicRules.TryApply(FenCodec.Parse(Fen), move, out var result))
                throw new ArgumentException("Cannot append an illegal move to the engine history.", nameof(move));
            var moves = new string[Moves.Count + 1];
            for (int i = 0; i < Moves.Count; i++) moves[i] = Moves[i];
            moves[moves.Length - 1] = StockfishMoveAdapter.ToUci(move);
            return new StockfishPosition(InitialFen, FenCodec.Write(result.State), moves);
        }
    }
}
