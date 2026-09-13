using System;
using System.Collections.Generic;
using ChessButWeird.Domain;
using UnityEngine;

namespace ChessButWeird.Application
{
    /// <summary>
    /// Owns the Unity ARAM runtime boundary. ChessGame submits lifecycle and
    /// move queries here while AramBuffRuntime remains the compatibility
    /// implementation for draft, markers, HUD and wire payloads.
    /// </summary>
    public sealed class AramMatchCoordinator : IDisposable
    {
        public AramMatchCoordinator(GameObject owner)
        {
            if (!owner)
                throw new ArgumentNullException(nameof(owner));

            Runtime = owner.GetComponent<AramBuffRuntime>();
            if (!Runtime)
                Runtime = owner.AddComponent<AramBuffRuntime>();
        }

        public AramBuffRuntime Runtime { get; }
        public bool IsActive => Runtime != null && Runtime.IsActive;
        public bool IsSelectingSetupTargets => Runtime != null && Runtime.IsSelectingSetupTargets;
        public AramRules DomainRules => Runtime != null ? Runtime.DomainRules : AramRules.BuiltIn;

        public void BeginMatch(ChessGame owner) => Runtime?.BeginMatch(owner);

        public void BeginNetworkMatch(
            ChessGame owner,
            PieceTeam localTeam,
            string seed,
            BackendAramStatePayload serverState) =>
            Runtime?.BeginNetworkMatch(owner, localTeam, seed, serverState);

        public void EndMatch() => Runtime?.EndMatch();

        public void OnTurnStarted(PieceTeam team) => Runtime?.OnTurnStarted(team);

        public bool TryHandleSetupPieceClick(ChessPiece piece) =>
            Runtime != null && Runtime.TryHandleSetupPieceClick(piece);

        public void AddCandidateMoves(ChessPiece piece, List<Vector2Int> moves, ChessPiece[,] board) =>
            Runtime?.AddCandidateMoves(piece, moves, board);

        public bool SuppressesStandardMovement(ChessPiece piece) =>
            Runtime != null && Runtime.SuppressesStandardMovement(piece);

        public bool IsLegalAramMove(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board) =>
            Runtime != null && Runtime.IsLegalAramMove(piece, from, destination, board);

        public bool CanAramPieceAttackSquare(ChessPiece piece, Vector2Int square, ChessPiece[,] board) =>
            Runtime != null && Runtime.CanAramPieceAttackSquare(piece, square, board);

        public bool AllowsStrongFortressCastle(PieceTeam team) =>
            Runtime != null && Runtime.AllowsStrongFortressCastle(team);

        public void OnMoveAccepted(ChessPiece piece, Vector2Int from, Vector2Int destination, ChessPiece[,] board) =>
            Runtime?.OnMoveAccepted(piece, from, destination, board);

        public bool TryGetQueenExplosion(
            ChessPiece capturedPiece,
            Vector2Int center,
            ChessPiece movingPiece,
            ChessPiece[,] board,
            List<ChessPiece> victims) =>
            Runtime != null && Runtime.TryGetQueenExplosion(capturedPiece, center, movingPiece, board, victims);

        public bool WouldQueenExplode(ChessPiece piece) =>
            Runtime != null && Runtime.WouldQueenExplode(piece);

        public void ApplyNetworkState(BackendAramStatePayload serverState) =>
            Runtime?.ApplyNetworkState(serverState);

        public BackendAramStatePayload CaptureNetworkState() =>
            Runtime != null ? Runtime.CaptureNetworkState() : null;

        public void Dispose()
        {
            EndMatch();
        }
    }
}
