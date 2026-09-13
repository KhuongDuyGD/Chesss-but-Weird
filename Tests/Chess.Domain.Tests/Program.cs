using ChessButWeird.Domain;
using ChessButWeird.Application;

int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    count++;
    Console.WriteLine("PASS " + name);
}
Move MoveOf(string from, string to, PieceKind? promotion = null) => new(FenCodec.ParseSquare(from), FenCodec.ParseSquare(to), promotion);
long Perft(MatchState state, int depth)
{
    if (depth == 0) return 1;
    long total = 0;
    foreach (var move in ClassicRules.LegalMoves(state))
    {
        if (!ClassicRules.TryApply(state, move, out var result)) throw new Exception("Generated illegal move");
        total += Perft(result.State, depth - 1);
    }
    return total;
}
var initial = FenCodec.Parse(FenCodec.InitialPosition);
Check(FenCodec.Write(initial) == FenCodec.InitialPosition, "Initial FEN round trip");
Check(Perft(initial, 1) == 20, "Initial perft 1 = 20");
Check(Perft(initial, 2) == 400, "Initial perft 2 = 400");
Check(Perft(initial, 3) == 8902, "Initial perft 3 = 8902");
Check(FenCodec.Write(initial) == FenCodec.InitialPosition, "Simulation leaves source untouched");
Check(ClassicRules.TryApply(initial, MoveOf("e2", "e4"), out var e4) &&
    FenCodec.Write(e4.State) == "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", "Double pawn step and turn");
Check(!ClassicRules.TryApply(initial, MoveOf("e7", "e5"), out _), "Wrong side rejected");
Check(!ClassicRules.TryApply(initial, MoveOf("a1", "a4"), out _), "Blocked rook rejected");
var castle = FenCodec.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
Check(ClassicRules.TryApply(castle, MoveOf("e1", "g1"), out var castled) && castled.Changes.Count == 2 &&
    castled.State.Board.GetPiece(new Square(5, 0)).Kind == PieceKind.Rook, "Castle moves both pieces");
var throughCheck = FenCodec.Parse("k4r2/8/8/8/8/8/8/4K2R w K - 0 1");
Check(!ClassicRules.TryApply(throughCheck, MoveOf("e1", "g1"), out _), "Cannot castle through check");
var pinned = FenCodec.Parse("k3r3/8/8/8/8/8/4R3/4K3 w - - 0 1");
Check(!ClassicRules.TryApply(pinned, MoveOf("e2", "d2"), out _), "Pinned piece cannot expose king");
var ep = FenCodec.Parse("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
Check(ClassicRules.TryApply(ep, MoveOf("e5", "d6"), out var captured) &&
    captured.State.Board.GetPiece(FenCodec.ParseSquare("d5")).IsEmpty, "En passant removes adjacent pawn");
var epPin = FenCodec.Parse("k7/8/8/4KPpr/8/8/8/8 w - g6 0 1");
Check(!ClassicRules.TryApply(epPin, MoveOf("f5", "g6"), out _), "En passant cannot expose king to rook");
var promotion = FenCodec.Parse("4k3/P7/8/8/8/8/8/4K3 w - - 0 1");
Check(!ClassicRules.TryApply(promotion, MoveOf("a7", "a8"), out _), "Promotion choice required before commit");
foreach (var kind in new[] { PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight })
{
    Check(ClassicRules.TryApply(promotion, MoveOf("a7", "a8", kind), out var promoted) &&
        promoted.State.Board.GetPiece(new Square(0, 7)).Id == promotion.Board.GetPiece(new Square(0, 6)).Id &&
        promoted.State.Board.GetPiece(new Square(0, 7)).Kind == kind, "Promotion preserves ID: " + kind);
}
Check(!ClassicRules.TryApply(promotion, MoveOf("a7", "a8", PieceKind.King), out _), "King promotion rejected");
var normalSession = MatchSession.CreateClassic(Team.White, new BoardOrientation(Team.White));
string sessionBeforeRejected = FenCodec.Write(normalSession.Snapshot);
Check(!normalSession.TryApply(FenCodec.ParseSquare("e2"), FenCodec.ParseSquare("e5"), out _),
    "Session rejects illegal move atomically");
Check(FenCodec.Write(normalSession.Snapshot) == sessionBeforeRejected, "Rejected session move preserves state");
Check(normalSession.TryApply(FenCodec.ParseSquare("e2"), FenCodec.ParseSquare("e4"), out var sessionE4) &&
    sessionE4.State.Board.GetPiece(FenCodec.ParseSquare("e4")).Id ==
        normalSession.GetPiece(FenCodec.ParseSquare("e4")).Id,
    "Session commits canonical move with stable piece ID");
var detachedSessionSnapshot = normalSession.Snapshot;
detachedSessionSnapshot.Board.SetPiece(FenCodec.ParseSquare("e4"), default);
sessionE4.State.Board.SetPiece(FenCodec.ParseSquare("e4"), default);
Check(!normalSession.GetPiece(FenCodec.ParseSquare("e4")).IsEmpty, "Session snapshots cannot mutate owned state");
var reverseSession = MatchSession.CreateClassic(Team.Black, new BoardOrientation(Team.Black));
Check(reverseSession.GetPiece(FenCodec.ParseSquare("e2")).Team == Team.Black &&
    reverseSession.GetPiece(FenCodec.ParseSquare("e2")).Forward == 1,
    "Reversed session projects black pawn forward direction");
Check(reverseSession.GetPiece(FenCodec.ParseSquare("e7")).Team == Team.White &&
    reverseSession.GetPiece(FenCodec.ParseSquare("e7")).Forward == -1,
    "Reversed session projects white pawn forward direction");
Check(reverseSession.TryApply(FenCodec.ParseSquare("e2"), FenCodec.ParseSquare("e4"), out var reverseE4) &&
    reverseE4.Move.From == FenCodec.ParseSquare("e2") && reverseSession.Turn == Team.White,
    "Reversed session maps black first move without FEN round trip");
var castleState = new MatchState { Turn = Team.White };
castleState.Board.SetPiece(FenCodec.ParseSquare("e1"), new PieceState(1, PieceKind.King, Team.White));
castleState.Board.SetPiece(FenCodec.ParseSquare("h1"), new PieceState(2, PieceKind.Rook, Team.White));
castleState.Board.SetPiece(FenCodec.ParseSquare("a8"), new PieceState(3, PieceKind.King, Team.Black));
var castleSession = MatchSession.FromSnapshot(castleState, new BoardOrientation(Team.White));
Check(castleSession.TryApply(FenCodec.ParseSquare("e1"), FenCodec.ParseSquare("g1"), out var castledSession) &&
    castledSession.IsCastle && castleSession.GetPiece(FenCodec.ParseSquare("f1")).Kind == PieceKind.Rook,
    "Session applies castling through mapped change set");
var reverseCastleState = new MatchState { Turn = Team.Black };
reverseCastleState.Board.SetPiece(FenCodec.ParseSquare("e8"), new PieceState(11, PieceKind.King, Team.Black));
reverseCastleState.Board.SetPiece(FenCodec.ParseSquare("h8"), new PieceState(12, PieceKind.Rook, Team.Black));
reverseCastleState.Board.SetPiece(FenCodec.ParseSquare("a1"), new PieceState(13, PieceKind.King, Team.White));
var reverseCastleSession = MatchSession.FromSnapshot(reverseCastleState, new BoardOrientation(Team.Black));
Check(reverseCastleSession.TryApply(FenCodec.ParseSquare("e1"), FenCodec.ParseSquare("g1"), out var reverseCastled) &&
    reverseCastled.IsCastle && reverseCastleSession.GetPiece(FenCodec.ParseSquare("f1")).Kind == PieceKind.Rook,
    "Reversed session derives castling home rank from orientation");
var enPassantState = new MatchState { Turn = Team.White, EnPassantTarget = FenCodec.ParseSquare("d6") };
enPassantState.Board.SetPiece(FenCodec.ParseSquare("e5"), new PieceState(21, PieceKind.Pawn, Team.White, true));
enPassantState.Board.SetPiece(FenCodec.ParseSquare("d5"), new PieceState(22, PieceKind.Pawn, Team.Black, true));
enPassantState.Board.SetPiece(FenCodec.ParseSquare("e1"), new PieceState(23, PieceKind.King, Team.White));
enPassantState.Board.SetPiece(FenCodec.ParseSquare("a8"), new PieceState(24, PieceKind.King, Team.Black));
var enPassantSession = MatchSession.FromSnapshot(enPassantState, new BoardOrientation(Team.White));
Check(enPassantSession.TryApply(FenCodec.ParseSquare("e5"), FenCodec.ParseSquare("d6"), out var enPassantResult) &&
    enPassantResult.IsCapture && enPassantSession.GetPiece(FenCodec.ParseSquare("d5")).IsEmpty,
    "Session applies en passant and removes the passed pawn");
var reverseEnPassantSession = MatchSession.FromSnapshot(enPassantState, new BoardOrientation(Team.Black));
Check(reverseEnPassantSession.TryApply(FenCodec.ParseSquare("e4"), FenCodec.ParseSquare("d3"), out var reverseEnPassantResult) &&
    reverseEnPassantResult.IsCapture && reverseEnPassantSession.GetPiece(FenCodec.ParseSquare("d4")).IsEmpty,
    "Reversed session maps en passant coordinates");
var promotionSessionState = new MatchState { Turn = Team.White };
promotionSessionState.Board.SetPiece(FenCodec.ParseSquare("a7"), new PieceState(31, PieceKind.Pawn, Team.White, true));
promotionSessionState.Board.SetPiece(FenCodec.ParseSquare("e1"), new PieceState(32, PieceKind.King, Team.White));
promotionSessionState.Board.SetPiece(FenCodec.ParseSquare("e8"), new PieceState(33, PieceKind.King, Team.Black));
var promotionSession = MatchSession.FromSnapshot(promotionSessionState, new BoardOrientation(Team.White));
Check(promotionSession.TryApply(FenCodec.ParseSquare("a7"), FenCodec.ParseSquare("a8"), PieceKind.Knight, out var promotionResult) &&
    promotionResult.State.Board.GetPiece(FenCodec.ParseSquare("a8")).Id == 31 &&
    promotionSession.GetPiece(FenCodec.ParseSquare("a8")).Kind == PieceKind.Knight,
    "Session preserves ID through promotion");
var coordinatorPromotionState = new MatchState { Turn = Team.White };
coordinatorPromotionState.Board.SetPiece(FenCodec.ParseSquare("a7"), new PieceState(41, PieceKind.Pawn, Team.White, true));
coordinatorPromotionState.Board.SetPiece(FenCodec.ParseSquare("e1"), new PieceState(42, PieceKind.King, Team.White));
coordinatorPromotionState.Board.SetPiece(FenCodec.ParseSquare("e8"), new PieceState(43, PieceKind.King, Team.Black));
var coordinator = ClassicMatchCoordinator.FromSnapshot(coordinatorPromotionState, new BoardOrientation(Team.White));
Check(coordinator.Submit(MoveOf("a7", "a8"), out _) == ClassicMoveStatus.PromotionRequired &&
    coordinator.HasPendingPromotion && coordinator.GetPiece(FenCodec.ParseSquare("a7")).Kind == PieceKind.Pawn,
    "Coordinator holds promotion command before choice");
string coordinatorPendingFen = FenCodec.Write(coordinator.Snapshot);
Check(coordinator.Submit(MoveOf("a7", "a8", PieceKind.King), out _) == ClassicMoveStatus.Rejected &&
    coordinator.HasPendingPromotion && FenCodec.Write(coordinator.Snapshot) == coordinatorPendingFen,
    "Coordinator rejects king promotion without clearing pending command");
Check(coordinator.Submit(MoveOf("a7", "a8", PieceKind.Pawn), out _) == ClassicMoveStatus.Rejected &&
    coordinator.HasPendingPromotion && FenCodec.Write(coordinator.Snapshot) == coordinatorPendingFen,
    "Coordinator rejects pawn promotion without changing state");
Check(coordinator.Submit(new Move(FenCodec.ParseSquare("a7"), FenCodec.ParseSquare("a8"), (PieceKind)99), out _) == ClassicMoveStatus.Rejected &&
    coordinator.HasPendingPromotion && FenCodec.Write(coordinator.Snapshot) == coordinatorPendingFen,
    "Coordinator rejects unknown promotion without changing state");
Check(coordinator.Submit(MoveOf("a7", "a8", PieceKind.Knight), out var coordinatorPromotion) == ClassicMoveStatus.Applied &&
    !coordinator.HasPendingPromotion && coordinatorPromotion.State.Board.GetPiece(FenCodec.ParseSquare("a8")).Kind == PieceKind.Knight &&
    coordinator.GetPiece(FenCodec.ParseSquare("a8")).Id == 41,
    "Coordinator commits promotion command with stable ID");
Check(StockfishMoveAdapter.TryParseUci("e2e4", out var stockfishMove) &&
    stockfishMove.From == FenCodec.ParseSquare("e2") && stockfishMove.To == FenCodec.ParseSquare("e4") &&
    !stockfishMove.Promotion.HasValue,
    "Stockfish adapter maps a normal move to Domain");
Check(StockfishMoveAdapter.TryParseUci("a7a8n", out var stockfishPromotion) &&
    stockfishPromotion.Promotion == PieceKind.Knight &&
    StockfishMoveAdapter.TryParseUci(" a7a8n ", out _) &&
    !StockfishMoveAdapter.TryParseUci("a7a8k", out _),
    "Stockfish adapter maps and validates promotion commands");
var networkSession = new NetworkMatchSession();
networkSession.Begin("match-1", "CLASSIC", FenCodec.InitialPosition);
Check(networkSession.TryQueueCommand("move-1", "match-1") &&
    !networkSession.TryQueueCommand("move-1", "match-1"),
    "Network session deduplicates pending request IDs");
Check(networkSession.TryAcceptResult("move-1", "match-1", 1, "after-one") &&
    !networkSession.TryAcceptResult("move-1", "match-1", 1, "after-one"),
    "Network session accepts one authoritative result and rejects its duplicate");
Check(!networkSession.TryAcceptResult("old", "match-1", 0, "stale") &&
    networkSession.TryQueueCommand("move-2", "match-1") &&
    networkSession.RejectCommand("move-2", "match-1"),
    "Network session rejects stale result and tracks rejection");
Check(networkSession.ApplySnapshot("match-1", "CLASSIC", "after-two", 2, true) &&
    networkSession.ConfirmedMoveNumber == 2 && networkSession.PendingCommandCount == 0 &&
    !networkSession.ApplySnapshot("match-1", "CLASSIC", "older", 1, true),
    "Network session replaces state only with a current or newer snapshot");
Check(networkSession.CanApplySnapshot("match-1", 2, true) &&
    !networkSession.CanApplySnapshot("foreign-match", 3, true) &&
    !networkSession.CanApplySnapshot("match-1", 1, true),
    "Network session validates snapshot identity and sequence without mutation");
var foreignSnapshotBefore = networkSession.GetSnapshot();
Check(networkSession.TryQueueCommand("pending-preflight", "match-1") &&
    !networkSession.CanAcceptResult("pending-preflight", "match-1", 1) &&
    !networkSession.CanAcceptResult("pending-preflight", "foreign", 3) &&
    networkSession.CanAcceptResult("pending-preflight", "match-1", 3) &&
    networkSession.PendingCommandCount == 1 && networkSession.AuthoritativeFen == "after-two",
    "Move preflight rejects stale and foreign results without touching pending commands");
Check(!networkSession.ApplySnapshot("foreign-match", "CLASSIC", "foreign", 3, true) &&
    networkSession.MatchId == foreignSnapshotBefore.MatchId &&
    networkSession.AuthoritativeFen == foreignSnapshotBefore.Fen &&
    networkSession.ConfirmedMoveNumber == foreignSnapshotBefore.MoveNumber,
    "Network session ignores a foreign snapshot without mutating state");
var inactiveSession = new NetworkMatchSession();
Check(inactiveSession.ApplySnapshot("inactive-match", "CLASSIC", "terminal", 4, false) &&
    inactiveSession.HasSnapshot && !inactiveSession.IsActive && inactiveSession.IsCompleted &&
    inactiveSession.GetSnapshot().IsActive == false,
    "Network session preserves initial inactive snapshot state");
Check(!inactiveSession.ApplySnapshot("inactive-match", "CLASSIC", "reopened", 5, true) &&
    !inactiveSession.IsActive && inactiveSession.AuthoritativeFen == "terminal",
    "Network session does not reactivate a terminal snapshot");
Check(networkSession.ApplySnapshot("match-1", "CLASSIC", "terminal-two", 3, false) &&
    networkSession.IsCompleted && !networkSession.IsActive &&
    !networkSession.ApplySnapshot("match-1", "CLASSIC", "reopened-two", 4, true) &&
    networkSession.AuthoritativeFen == "terminal-two",
    "Network session keeps terminal state after completion");
var resultRecorder = new MatchResultRecorder();
resultRecorder.Begin("match-result-1");
int resultWrites = 0;
Check(resultRecorder.TryRecord(() => resultWrites++) &&
    !resultRecorder.TryRecord(() => resultWrites++) && resultWrites == 1,
    "Match result recorder writes one result per match identity");
resultRecorder.Begin("match-result-2");
Check(resultRecorder.TryRecord(() => resultWrites++) && resultWrites == 2,
    "Match result recorder opens a new lifecycle for a new match");
var lobbySession = new NetworkLobbySession();
lobbySession.Configure("ARAM");
lobbySession.SetRoomCode("ROOM-7");
lobbySession.SetReady(true, 1);
lobbySession.BeginMatch("match-7", "ARAM");
Check(!lobbySession.IsPanelVisible && lobbySession.IsMatchActive &&
    lobbySession.RoomCode == "ROOM-7" && lobbySession.MatchId == "match-7" &&
    lobbySession.GameMode == "ARAM" && lobbySession.LocalReady,
    "Lobby session carries room and match lifecycle state");
lobbySession.CompleteMatch();
lobbySession.Reset(true);
Check(!lobbySession.IsMatchActive && lobbySession.RoomCode == string.Empty &&
    lobbySession.GameMode == NetworkLobbySession.ClassicGameMode,
    "Lobby session reset preserves a clean new-room boundary");
var mate = FenCodec.Parse("7k/6Q1/5K2/8/8/8/8/8 b - - 0 1");
Check(ClassicRules.IsInCheck(mate.Board, mate.Turn) && ClassicRules.LegalMoves(mate).Count == 0, "Checkmate");
var stale = FenCodec.Parse("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1");
Check(!ClassicRules.IsInCheck(stale.Board, stale.Turn) && ClassicRules.LegalMoves(stale).Count == 0, "Stalemate");
Check(!MovementRules.IsPathClear(initial.Board, new Square(0, 0), new Square(2, 3)), "Nonlinear path rejected without looping");
try { FenCodec.Parse("8/8/8/8/8/8/8/8 w - - -1 1"); throw new Exception("Malformed FEN accepted"); }
catch (FormatException) { Check(true, "Malformed FEN rejected"); }
var buffBoard = new BoardState();
var knight = new PieceState(1, PieceKind.Knight, Team.White);
var origin = new Square(3, 3);
buffBoard.SetPiece(origin, knight);
var leap = new AramPieceContext(AramBuffs.FreestyleLeap);
Check(AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(5, 5), leap), "Freestyle 2x2 leap");
Check(!AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(4, 4), leap), "Freestyle rejects 1x1 leap");
var swap = new AramPieceContext(AramBuffs.Doppelganger, swappedKnight: true);
Check(AramRules.SuppressesStandardMovement(swap) &&
    AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(6, 6), swap), "Doppelganger replaces movement");
Check(!AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(5, 4), swap), "Swapped knight loses L move");
buffBoard.SetPiece(new Square(4, 4), new PieceState(2, PieceKind.Pawn, Team.Black));
Check(!AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(6, 6), swap), "Swapped bishop movement blocked");
Check(AramRules.BuiltIn.Allows(buffBoard, knight, origin, new Square(5, 5),
    new AramPieceContext(AramBuffs.Doppelganger | AramBuffs.FreestyleLeap, swappedKnight: true)),
    "Existing combination policy: freestyle leap survives swap");
var pawn = new PieceState(3, PieceKind.Pawn, Team.White, true);
var commandant = new AramPieceContext(AramBuffs.CommandantPawn, commandant: true);
Check(AramRules.BuiltIn.Allows(buffBoard, pawn, origin, new Square(3, 5), commandant), "Commandant moved pawn double step");
Check(!AramRules.BuiltIn.Allows(buffBoard, pawn, origin, new Square(3, 5), commandant, attack: true), "Commandant forward move is not attack");
var queen = new PieceState(4, PieceKind.Queen, Team.White);
var teleport = new AramPieceContext(AramBuffs.FlyingThunderGod, originalQueen: true, canTeleport: true);
Check(AramRules.BuiltIn.Allows(buffBoard, queen, origin, new Square(1, 6), teleport), "Teleport to empty square");
Check(!AramRules.BuiltIn.Allows(buffBoard, queen, origin, new Square(4, 4), teleport), "Teleport cannot capture");
Check(!AramRules.BuiltIn.Allows(buffBoard, queen, origin, new Square(1, 6), teleport, attack: true), "Teleport does not attack empty square");
Check(!AramRules.BuiltIn.Allows(buffBoard, queen, origin, new Square(1, 6),
    new AramPieceContext(AramBuffs.FlyingThunderGod, originalQueen: true)), "Teleport unavailable during cooldown");
Check(StrongFortressBuff.WaivesCastleAttackChecks(AramBuffs.StrongFortress), "Fortress attack waiver");
Check(!SuicideBomberBuff.IsVictim(new PieceState(5, PieceKind.King, Team.White), 4, origin, new Square(4, 4)), "Explosion king immunity");
Check(!SuicideBomberBuff.IsVictim(queen, 4, origin, new Square(4, 4)), "Explosion capturing piece immunity");
Check(SuicideBomberBuff.IsVictim(pawn, 4, origin, new Square(4, 4)), "Explosion adjacent victim");
var none = new Square(-1, -1);
var explosionBoard = new BoardState();
explosionBoard.SetPiece(new Square(4, 0), new PieceState(1, PieceKind.King, Team.White));
explosionBoard.SetPiece(new Square(0, 7), new PieceState(2, PieceKind.King, Team.Black));
explosionBoard.SetPiece(new Square(4, 7), new PieceState(3, PieceKind.Rook, Team.Black));
explosionBoard.SetPiece(new Square(4, 3), new PieceState(4, PieceKind.Pawn, Team.White));
explosionBoard.SetPiece(new Square(3, 3), new PieceState(5, PieceKind.Queen, Team.Black));
explosionBoard.SetPiece(new Square(1, 2), new PieceState(6, PieceKind.Knight, Team.White));
var simulated = new MoveBoardView<BoardState>(explosionBoard, new Square(1, 2), new Square(3, 3),
    new Square(3, 3), none, none, true);
Check(simulated.GetPiece(new Square(4, 3)).IsEmpty && !explosionBoard.GetPiece(new Square(4, 3)).IsEmpty,
    "Explosion simulation removes blocker without changing source");
Check(KingSafetyRules.IsInCheck(simulated, Team.White, new NoBuffs()), "Explosion revealing rook check is unsafe");
Check(simulated.GetPiece(new Square(3, 3)).Id == 6, "Capturing piece survives simulated explosion");
var charges = new LimitedUseState(5);
Check(charges.TryConsume(5, out var cooling) && cooling.Uses == 1 && cooling.Cooldown == 5, "Charge consumption sets cooldown");
Check(!cooling.TryConsume(5, out _), "Cannot consume while cooling down");
for (int turn = 0; turn < 5; turn++) cooling = cooling.Tick();
Check(cooling.CanUse && cooling.Uses == 1, "Cooldown expires after five own turns");
Check(!new LimitedUseState(5, 5).CanUse, "Five-use cap survives expired cooldown");
Check(new LimitedUseState(5, 99, -3).Uses == 5 && new LimitedUseState(5, 99, -3).Cooldown == 0,
    "Restored charge state clamps invalid counters");
var extendedRules = new AramRules(new TestOneStepBuff());
Check(extendedRules.Allows(buffBoard, knight, origin, new Square(3, 4), default), "New movement behavior registers without controller changes");
var customSafetyBoard = new BoardState();
customSafetyBoard.SetPiece(new Square(4, 1), new PieceState(7, PieceKind.King, Team.White));
customSafetyBoard.SetPiece(new Square(4, 0), new PieceState(8, PieceKind.Pawn, Team.Black));
Check(KingSafetyRules.IsInCheck(customSafetyBoard, Team.White, new NoBuffs(), extendedRules),
    "Injected movement registry contributes to attack and king safety");
Check(!KingSafetyRules.IsInCheck(customSafetyBoard, Team.White, new NoBuffs(), AramRules.BuiltIn),
    "Built-in registry remains isolated from custom movement behavior");
Console.WriteLine($"{count} checks passed.");

sealed class TestOneStepBuff : IAramMovementBuff
{
    public bool Allows<TBoard>(TBoard board, PieceState piece, Square from, Square to, AramPieceContext context, bool attack)
        where TBoard : IReadOnlyBoard => to.File == from.File && to.Rank == from.Rank + 1;
}
