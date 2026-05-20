// Requires Mirror (https://mirror-networking.com/) installed via Package Manager
// Add Mirror from GitHub: https://github.com/MirrorNetworking/Mirror
// Menu → Window → Package Manager → + → Add from git URL:
//   https://github.com/MirrorNetworking/Mirror.git

using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace ChessButWeird.Networking
{
    // ═══════════════════════════════════════════════════════════════════════════
    // ChessNetworkManager – replace the default NetworkManager on the scene
    // ═══════════════════════════════════════════════════════════════════════════

    public class ChessNetworkManager : NetworkManager
    {
        public static ChessNetworkManager Instance { get; private set; }

        public UnityEvent OnHostStarted    = new();
        public UnityEvent OnClientJoined   = new();
        public UnityEvent OnClientDropped  = new();

        public override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        /// <summary>Start as LAN host (server + local client).</summary>
        public void HostGame()
        {
            Debug.Log("[Net] Starting host…");
            StartHost();
        }

        /// <summary>Join a LAN game by IP address.</summary>
        public void JoinGame(string ip)
        {
            networkAddress = ip;
            Debug.Log($"[Net] Joining {ip}…");
            StartClient();
        }

        public void LeaveGame()
        {
            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkClient.isConnected)                    StopClient();
            else if (NetworkServer.active)                          StopServer();
        }

        public override void OnStartHost()      { base.OnStartHost();      OnHostStarted.Invoke(); }
        public override void OnClientConnect()  { base.OnClientConnect();  OnClientJoined.Invoke(); }
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            OnClientDropped.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // NetworkedChessGame – attach on the same GameObject as ChessGame / ARAMChessGame
    // The server is authoritative; clients send [Command]s and receive [SyncVar] updates.
    // ═══════════════════════════════════════════════════════════════════════════

    public class NetworkedChessGame : NetworkBehaviour
    {
        // ── Synced state ─────────────────────────────────────────────────────────

        /// <summary>Full FEN string kept in sync by the server.</summary>
        [SyncVar(hook = nameof(OnFENChanged))]
        private string _syncedFEN = ChessBoard.StartFEN;

        /// <summary>Which color the local player controls (assigned by server).</summary>
        [SyncVar]
        public PieceColor LocalPlayerColor = PieceColor.White;

        [SyncVar]
        public bool GameStarted = false;

        // ── References ───────────────────────────────────────────────────────────

        private ChessGame _game;

        // ── Events (local) ───────────────────────────────────────────────────────

        public UnityEvent<string> OnRemoteBoardUpdated = new();

        // ── Unity ────────────────────────────────────────────────────────────────

        private void Awake() => _game = GetComponent<ChessGame>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            _game.InitGame();
            _syncedFEN = _game.Board.ToFEN();
            GameStarted = true;
            Debug.Log("[Net] Server: game initialised.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // If we joined as client and FEN is already set, sync immediately
            if (!string.IsNullOrEmpty(_syncedFEN))
                ApplyRemoteFEN(_syncedFEN);
        }

        // ── Commands (client → server) ────────────────────────────────────────────

        /// <summary>Client asks server to apply a move.</summary>
        [Command]
        public void CmdRequestMove(string fromAlg, string toAlg, int promotionInt)
        {
            if (!GameStarted) return;

            var from  = Square.FromAlgebraic(fromAlg);
            var to    = Square.FromAlgebraic(toAlg);
            var promo = (PieceType)promotionInt;

            // Validate color authority (prevent spoofing)
            if (_game.Board.ActiveColor != LocalPlayerColor) return;

            bool ok = _game.TryMakeMove(from, to, promo);
            if (ok)
            {
                _syncedFEN = _game.Board.ToFEN();
                Debug.Log($"[Net] Server: move {fromAlg}{toAlg} accepted. FEN synced.");
            }
        }

        // ── SyncVar hook (runs on all clients when _syncedFEN changes) ────────────

        private void OnFENChanged(string _, string newFEN) => ApplyRemoteFEN(newFEN);

        private void ApplyRemoteFEN(string fen)
        {
            if (isServer) return; // server already has the correct state
            _game.Board.LoadFEN(fen);
            OnRemoteBoardUpdated.Invoke(fen);
            Debug.Log($"[Net] Client: board updated from server.");
        }

        // ── Convenience wrapper ───────────────────────────────────────────────────

        /// <summary>
        /// Call from the UI/input handler. Works on both server-host and pure client.
        /// </summary>
        public void SendMove(Square from, Square to, PieceType promotion = PieceType.Queen)
        {
            if (isServer)
            {
                // Host: apply directly
                _game.TryMakeMove(from, to, promotion);
                _syncedFEN = _game.Board.ToFEN();
            }
            else
            {
                // Client: request via command
                CmdRequestMove(from.ToAlgebraic(), to.ToAlgebraic(), (int)promotion);
            }
        }
    }
}