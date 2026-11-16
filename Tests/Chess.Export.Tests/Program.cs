using ChessButWeird.Application;
using ChessButWeird.Domain;
using System.Text.Json;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var fixtures = new List<object>();
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "Logs/match-export-tests");
Directory.CreateDirectory(output);
Move Uci(string value) => StockfishMoveAdapter.TryParseUci(value, out var move) ? move : throw new Exception(value);
MatchExportRecord Record(string start, IEnumerable<string> uci, string result = "1/2-1/2", string mode = "Classic", MatchNotationRules rules = MatchNotationRules.Standard)
{
    var state = FenCodec.Parse(start); var moves = uci.Select(Uci).ToArray();
    foreach (var move in moves) { if (!ClassicRules.TryApply(state, move, out var next)) throw new Exception("Bad fixture: " + StockfishMoveAdapter.ToUci(move)); state = next.State; }
    return new MatchExportRecord(rules, mode, true, start, FenCodec.Write(state), moves, true, "Alice", "Bob", result, new DateTime(2026, 11, 16));
}
void ExportCase(string name, MatchExportRecord record, string expectedSan = null)
{
    var pgn = MatchExportService.Export(record, ChessExportKind.Pgn);
    var fen = MatchExportService.Export(record, ChessExportKind.Fen);
    Check(pgn.Success && fen.Success, name + ": " + pgn.Error + fen.Error);
    if (expectedSan != null) Check(pgn.Text.Contains(expectedSan), name + " SAN " + expectedSan);
    var positions = new List<string> { FenCodec.Write(FenCodec.Parse(record.InitialFen)) };
    var position = FenCodec.Parse(record.InitialFen);
    foreach (Move move in record.Moves)
    {
        ClassicRules.TryApply(position, move, out var next); position = next.State;
        positions.Add(FenCodec.Write(position));
    }
    fixtures.Add(new { name, pgn = pgn.Text, fen = fen.Text, uci = record.Moves.Select(StockfishMoveAdapter.ToUci).ToArray(), positions });
    File.WriteAllText(Path.Combine(output, name + ".pgn"), pgn.Text);
    File.WriteAllText(Path.Combine(output, name + ".fen"), fen.Text);
}
var opening = new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5a4", "g8f6", "e1g1" };
ExportCase("ordinary-castling", Record(FenCodec.InitialPosition, opening), "5. O-O");
ExportCase("black-checkmate", Record(FenCodec.InitialPosition, new[] { "f2f3", "e7e5", "g2g4", "d8h4" }, "0-1"), "Qh4# 0-1");
ExportCase("white-checkmate", Record(FenCodec.InitialPosition, new[] { "e2e4", "e7e5", "f1c4", "b8c6", "d1h5", "g8f6", "h5f7" }, "1-0"), "Qxf7#");
ExportCase("both-castles", Record("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", new[] { "e1c1", "e8g8" }), "1. O-O-O O-O");
foreach (string promotion in new[] { "q", "r", "b", "n" })
    ExportCase("promotion-" + promotion, Record("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", new[] { "a7a8" + promotion }), "a8=" + promotion.ToUpperInvariant());
ExportCase("capture-underpromotion", Record("1r2k3/P7/8/8/8/8/8/4K3 w - - 0 1", new[] { "a7b8n" }), "axb8=N");
ExportCase("en-passant", Record("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 17", new[] { "e5d6" }), "17. exd6");
ExportCase("black-custom-root", Record("4k3/8/8/8/8/8/8/4K3 b - - 17 42", new[] { "e8e7" }), "42... Ke7");
ExportCase("file-disambiguation", Record("4k3/8/8/8/8/8/8/1N2KN2 w - - 0 1", new[] { "b1d2" }), "Nbd2");
ExportCase("rank-disambiguation", Record("4k3/8/8/8/8/R7/8/R3K3 w - - 0 1", new[] { "a3a2" }), "R3a2");
ExportCase("square-disambiguation", Record("4k3/8/8/8/8/Q7/8/Q1Q1K3 w - - 0 1", new[] { "a1c3" }), "Qa1c3");
ExportCase("pinned-alternative", Record("1k2r3/8/8/8/8/8/R3R3/4K3 w - - 0 1", new[] { "a2d2" }), "1. Rd2");
ExportCase("initial-position", Record(FenCodec.InitialPosition, Array.Empty<string>()));
ExportCase("stalemate", Record("7k/5K2/6Q1/8/8/8/8/8 b - - 0 21", Array.Empty<string>()));
ExportCase("resignation", Record(FenCodec.InitialPosition, opening, "1-0"));
ExportCase("timeout", Record(FenCodec.InitialPosition, opening, "0-1"));

// Canonical examples from PGN/FEN specification sections 16.1.3 and 16.1.4.
void ExpectedFen(string name, string start, string[] commands, string expected)
{
    var record = Record(start, commands);
    var result = MatchExportService.Export(record, ChessExportKind.Fen);
    Check(result.Success && result.Text == expected, name + " exact six-field FEN");
    Check(FenCodec.Write(StandardChessNotation.ParsePosition(result.Text)) == expected, name + " canonical round trip");
    ExportCase(name, record);
}
ExpectedFen("spec-initial", FenCodec.InitialPosition, Array.Empty<string>(), FenCodec.InitialPosition);
ExpectedFen("spec-e4", FenCodec.InitialPosition, new[] { "e2e4" }, "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
ExpectedFen("spec-e4-c5", FenCodec.InitialPosition, new[] { "e2e4", "c7c5" }, "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2");
ExpectedFen("spec-e4-c5-nf3", FenCodec.InitialPosition, new[] { "e2e4", "c7c5", "g1f3" }, "rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2");
ExpectedFen("spec-custom-counters", "4k3/8/8/8/8/8/4P3/4K3 w - - 5 39", Array.Empty<string>(), "4k3/8/8/8/8/8/4P3/4K3 w - - 5 39");
ExpectedFen("rights-rook-return", "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", new[] { "h1h2", "h8h7", "h2h1", "h7h8" }, "r3k2r/8/8/8/8/8/8/R3K2R w Qq - 4 3");
ExpectedFen("rights-rook-captured", "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", new[] { "a1a8" }, "R3k2r/8/8/8/8/8/8/4K2R b Kk - 0 1");
ExpectedFen("rights-castled", "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", new[] { "e1g1", "e8c8" }, "2kr3r/8/8/8/8/8/8/R4RK1 w - - 2 2");
ExpectedFen("ep-expires", "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 17", new[] { "e1e2" }, "4k3/8/8/3pP3/8/8/4K3/8 b - - 1 17");
ExpectedFen("ep-capture-counters", "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 17", new[] { "e5d6" }, "4k3/8/3P4/8/8/8/8/4K3 b - - 0 17");
ExpectedFen("promotion-counters", "4k3/P7/8/8/8/8/8/4K3 w - - 14 42", new[] { "a7a8n" }, "N3k3/8/8/8/8/8/8/4K3 b - - 0 42");
ExpectedFen("black-turn-counters", "4k3/8/8/8/8/8/8/4K3 b - - 17 42", new[] { "e8e7" }, "8/4k3/8/8/8/8/8/4K3 w - - 18 43");

foreach (string malformed in new[] {
    "4k3/8/8/8/8/8/8/4K3 w - - 0 1", "4k3/8/8/8/8/8/4K3/8 w KK - 0 1",
    "4k3/8/8/8/8/8/8/4K3 w - - +0 1", "4k3/8/8/8/8/8/8/4K3 w - - 0 -1",
    "4k3/8/8/3pP3/8/8/8/4K3 w - d6 1 17", "4k3/3p4/8/3pP3/8/8/8/4K3 w - d6 0 17",
    "8/8/8/8/8/8/4k3/4K3 w - - 0 1" })
    Check(!MatchExportService.Export(new MatchExportRecord(MatchNotationRules.Standard, "Classic", true,
        null, malformed, null, false, "A", "B", "1-0"), ChessExportKind.Fen).Success, "Malformed FEN blocked: " + malformed);

var valid = Record(FenCodec.InitialPosition, opening);
foreach (string mode in new[] { "Chaos", "CHAOS", "Aram", "ARAM", "ChaosNormal", "ChaosRanked" }) foreach (var kind in new[] { ChessExportKind.Pgn, ChessExportKind.Fen })
{
    var chaos = new MatchExportRecord(MatchNotationRules.Standard, mode, true, valid.InitialFen, valid.FinalFen, valid.Moves, true, "A", "B", "1-0");
    Check(!MatchExportPolicy.ShowNotation(chaos) && !MatchExportService.Export(chaos, kind).Success, "Chaos identifier blocks direct " + kind + ": " + mode);
}
foreach (var rules in new[] { MatchNotationRules.Chaos, MatchNotationRules.Unsupported })
    Check(!MatchExportService.Export(new MatchExportRecord(rules, "Classic", true, valid.InitialFen, valid.FinalFen, valid.Moves, true, "A", "B", "1-0"), ChessExportKind.Fen).Success, "Unapproved rules rejected: " + rules);
Check(!MatchExportService.Export(null, ChessExportKind.Pgn).Success, "Missing session rejected");
Check(!MatchExportService.Export(new MatchExportRecord(MatchNotationRules.Standard, "Classic", false, valid.InitialFen, valid.FinalFen, valid.Moves, true, "A", "B", "1-0"), ChessExportKind.Pgn).Success, "Unfinished session rejected");
var incomplete = new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, valid.InitialFen, valid.FinalFen, valid.Moves, false, "A", "B", "1-0");
Check(!MatchExportService.Export(incomplete, ChessExportKind.Pgn).Success && MatchExportService.Export(incomplete, ChessExportKind.Fen).Success, "Incomplete history blocks PGN, preserves FEN");
foreach (IEnumerable<Move> moves in new[] { valid.Moves.Take(3), valid.Moves.Concat(new[] { Uci("a1a8") }), new[] { Uci("e2e5") } })
    Check(!MatchExportService.Export(new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, valid.InitialFen, valid.FinalFen, moves, true, "A", "B", "1-0"), ChessExportKind.Pgn).Success, "Truncated/illegal history rejected");
foreach (string fen in new[] { "", "8/8/8/8/8/8/8/8 w - - 0 1", "4k3/8/8/8/8/8/8/4K3 w K - 0 1", "4k3/8/8/8/8/8/8/4K3 w - e6 0 1", "4k3/8/8/8/8/8/8/4K2P w - - 0 1", "4k3/8/8/8/8/8/4K3/8 w - - -1 1", "4k3/8/8/8/8/8/4K3/8 w - - 0 0", "4k3/8/8/8/8/8/4K3/8 x - - 0 1", "4k3/8/8/8/8/8/8/4K3 w - - 0", "4k3/8/8/8/8/8/8/4K3 w - - 2147483647 1", "4k3/4R3/8/8/8/8/8/4K3 w - - 0 1" })
    Check(!MatchExportService.Export(new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, null, fen, null, false, null, null, "1-0"), ChessExportKind.Fen).Success, "Invalid FEN rejected: " + fen);
Check(MatchExportService.Export(Record(FenCodec.InitialPosition, new[] { "e2e4" }), ChessExportKind.Fen).Text.EndsWith("b KQkq e3 0 1"), "FEN retains EP even without an available capture");
var escaped = new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, valid.InitialFen, valid.FinalFen, valid.Moves, true, "Alice \"Queen\"\\\n[Result \"0-1\"]", "Nguyễn 😀", "1/2-1/2");
ExportCase("escaped-names", escaped);
Check(MatchExportService.Export(escaped, ChessExportKind.Pgn).Text.Contains("\\\"Queen\\\"\\\\"), "PGN names escape quotes, slashes and newlines");
var unknown = new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, valid.InitialFen, valid.FinalFen, valid.Moves, true, null, "", "1/2-1/2");
Check(MatchExportService.Export(unknown, ChessExportKind.Pgn).Text.Contains("[Date \"????.??.??\"]") && MatchExportService.Export(unknown, ChessExportKind.Pgn).Text.Contains("[White \"?\"]"), "Unknown metadata uses PGN unknown markers");
var wrongResult = Record("7k/5K2/6Q1/8/8/8/8/8 b - - 0 21", Array.Empty<string>(), "1-0");
Check(!MatchExportService.Export(wrongResult, ChessExportKind.Pgn).Success, "Stalemate cannot claim a winning result");
var reversed = ClassicMatchCoordinator.CreateClassic(Team.Black, new BoardOrientation(Team.Black));
Check(reversed.TryApply(FenCodec.ParseSquare("e2"), FenCodec.ParseSquare("e4"), out _), "Black-facing local move accepted");
Check(StockfishMoveAdapter.ToUci(reversed.AcceptedMoves[0]) == "e7e5", "Export journal uses canonical coordinates");
ExportCase("black-first-local", new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, FenCodec.Write(reversed.InitialSnapshot), FenCodec.Write(reversed.Snapshot), reversed.AcceptedMoves, true, "White", "Black", "1/2-1/2"), "1... e5");
var pending = ClassicMatchCoordinator.FromSnapshot(FenCodec.Parse("4k3/P7/8/8/8/8/8/4K3 w - - 0 1"), new BoardOrientation(Team.White));
pending.Submit(Uci("a7a8"), out _);
Check(pending.AcceptedMoves.Count == 0, "Uncommitted promotion never enters export journal");
pending.Submit(Uci("a7a8n"), out _);
Check(pending.AcceptedMoves.Count == 1 && pending.AcceptedMoves[0].Promotion == PieceKind.Knight, "Promotion choice is journaled once");
var random = new Random(160_11_2026);
for (int seed = 0; seed < 24; seed++)
{
    var state = FenCodec.Parse(FenCodec.InitialPosition); var moves = new List<Move>();
    for (int ply = 0; ply < 80; ply++)
    {
        var legal = ClassicRules.LegalMoves(state); if (legal.Count == 0) break;
        var move = legal[random.Next(legal.Count)]; moves.Add(move); ClassicRules.TryApply(state, move, out var next); state = next.State;
    }
    string score = ClassicRules.LegalMoves(state).Count == 0 && ClassicRules.IsInCheck(state.Board, state.Turn) ? state.Turn == Team.White ? "0-1" : "1-0" : "1/2-1/2";
    ExportCase("random-" + seed.ToString("00"), new MatchExportRecord(MatchNotationRules.Standard, "Classic", true, FenCodec.InitialPosition, FenCodec.Write(state), moves, true, "A", "B", score));
}
File.WriteAllText(Path.Combine(output, "fixtures.json"), JsonSerializer.Serialize(fixtures));
Console.WriteLine($"{checks} export checks passed; {fixtures.Count} independent-parser fixtures written to {output}.");
