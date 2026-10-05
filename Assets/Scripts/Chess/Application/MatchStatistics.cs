using System;
using System.Collections.Generic;
using System.Linq;
using ChessButWeird.Domain;

namespace ChessButWeird.Application
{
    /// <summary>Presentation ledger. Never derives captures from missing starting pieces.</summary>
    public sealed class MatchStatistics
    {
        public sealed class Entry
        {
            public string Id { get; }
            public int MoveNumber { get; }
            public Team Actor { get; }
            public string Text { get; }
            public bool IsMove { get; }
            public bool Pending { get; }
            public PieceKind? Captured { get; }
            public Entry(string id, int number, Team actor, string text, bool isMove = true,
                bool pending = false, PieceKind? captured = null)
            {
                Id = id ?? string.Empty; MoveNumber = number; Actor = actor;
                Text = text ?? string.Empty; IsMove = isMove; Pending = pending; Captured = captured;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly IReadOnlyList<Entry> readOnlyEntries;
        public IReadOnlyList<Entry> Entries => readOnlyEntries;
        public int Revision { get; private set; }
        public bool HistoryComplete { get; private set; } = true;
        public bool CapturesComplete { get; private set; } = true;
        public int ConfirmedMoveNumber { get; private set; }
        public int MoveCount => entries.Count(e => e.IsMove);

        public MatchStatistics() { readOnlyEntries = entries.AsReadOnly(); }
        public void Reset(bool complete = true)
        {
            entries.Clear(); ConfirmedMoveNumber = 0;
            HistoryComplete = CapturesComplete = complete; Revision++;
        }
        public void AddLocal(Team actor, string text, bool pending, PieceKind? captured)
        {
            int number = Math.Max(ConfirmedMoveNumber, entries.Where(e => e.IsMove).Select(e => e.MoveNumber).DefaultIfEmpty(0).Max()) + 1;
            entries.Add(new Entry("local-" + Revision, number, actor, text, true, pending, captured));
            if (!pending) ConfirmedMoveNumber = number;
            Revision++;
        }
        public void AddEvent(Team actor, string text, PieceKind? captured = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            entries.Add(new Entry("event-" + Revision, ConfirmedMoveNumber, actor, text, false, false, captured)); Revision++;
        }
        public void ReviseLastMove(string text)
        {
            int index = entries.FindLastIndex(e => e.IsMove);
            if (index < 0 || entries[index].Text == text) return;
            Entry e = entries[index];
            entries[index] = new Entry(e.Id, e.MoveNumber, e.Actor, text, true, e.Pending, e.Captured);
            Revision++;
        }
        public void RejectPending()
        {
            if (entries.RemoveAll(e => e.Pending) > 0) Revision++;
        }
        public void Confirm(int number, Team actor, string text, PieceKind? captured, bool capturesKnown)
        {
            if (number <= ConfirmedMoveNumber) return;
            RejectPending();
            if (number != ConfirmedMoveNumber + 1) HistoryComplete = CapturesComplete = false;
            if (!capturesKnown) CapturesComplete = false;
            entries.Add(new Entry("server-" + number, number, actor, text, true, false, captured));
            ConfirmedMoveNumber = number; Revision++;
        }
        public void ObserveSnapshot(int number)
        {
            RejectPending();
            if (number > ConfirmedMoveNumber)
            {
                HistoryComplete = CapturesComplete = false;
                ConfirmedMoveNumber = number; Revision++;
            }
        }
        public void Restore(IEnumerable<Entry> history, int confirmedNumber, bool historyComplete, bool capturesComplete)
        {
            var incoming = history.ToList();
            if (ConfirmedMoveNumber == confirmedNumber && HistoryComplete == historyComplete && CapturesComplete == capturesComplete &&
                entries.Count == incoming.Count && entries.Zip(incoming, (a,b) => a.Id == b.Id && a.MoveNumber == b.MoveNumber &&
                    a.Actor == b.Actor && a.Text == b.Text && a.IsMove == b.IsMove && a.Pending == b.Pending && a.Captured == b.Captured).All(same => same)) return;
            entries.Clear(); entries.AddRange(incoming);
            ConfirmedMoveNumber = confirmedNumber;
            HistoryComplete = historyComplete; CapturesComplete = capturesComplete; Revision++;
        }
        public IEnumerable<PieceKind> CapturedBy(Team actor) => entries.Where(e => e.Actor == actor && e.Captured.HasValue).Select(e => e.Captured.Value);

        /// <summary>Capture in an ordinary Classic move, including en passant. Requires its pre-move FEN.</summary>
        public static bool TryClassicCapture(string fen, string from, string to, out Team actor, out PieceKind? captured)
        {
            actor = Team.White; captured = null;
            try
            {
                var state = FenCodec.Parse(fen);
                var source = state.Board.GetPiece(FenCodec.ParseSquare(from));
                var targetSquare = FenCodec.ParseSquare(to);
                var target = state.Board.GetPiece(targetSquare);
                if (source.IsEmpty) return false;
                actor = source.Team;
                if (!target.IsEmpty && target.Team != actor) captured = target.Kind;
                else if (source.Kind == PieceKind.Pawn && from[0] != to[0] && target.IsEmpty)
                {
                    if (targetSquare != state.EnPassantTarget) return false;
                    var adjacent = state.Board.GetPiece(new Square(targetSquare.File, FenCodec.ParseSquare(from).Rank));
                    if (adjacent.IsEmpty || adjacent.Team == actor || adjacent.Kind != PieceKind.Pawn) return false;
                    captured = PieceKind.Pawn;
                }
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is IndexOutOfRangeException)
            { return false; }
        }
    }
}
