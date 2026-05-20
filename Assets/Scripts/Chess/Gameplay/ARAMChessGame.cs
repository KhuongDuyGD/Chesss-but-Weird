using UnityEngine;
using UnityEngine.Events;

namespace ChessButWeird
{
    /// <summary>
    /// ARAM Chess mode.
    /// Extends ChessGame by hooking into ExecuteMove to trigger phase/buff logic.
    /// Attach alongside ARAMBuffManager on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(ARAMBuffManager))]
    public class ARAMChessGame : ChessGame
    {
        // ───────────────────────────── Inspector ───────────────────────────────────

        [Header("ARAM Events")]
        public UnityEvent<ActiveBuff> OnBuffGranted  = new();
        public UnityEvent<GamePhase>  OnPhaseChanged = new();

        // ───────────────────────────── State ───────────────────────────────────────

        public ARAMBuffManager BuffManager { get; private set; }

        // ───────────────────────────── Unity ───────────────────────────────────────

        protected override void Start()
        {
            BuffManager = GetComponent<ARAMBuffManager>();

            BuffManager.OnBuffGranted  += buff  => OnBuffGranted.Invoke(buff);
            BuffManager.OnPhaseChanged += phase => OnPhaseChanged.Invoke(phase);

            base.Start(); // calls InitGame()
        }

        // ───────────────────────────── Override ────────────────────────────────────

        protected override void ExecuteMove(Move move)
        {
            base.ExecuteMove(move); // applies move, checks game over, fires events
            BuffManager.OnMoveMade(this);
        }

        // ───────────────────────────── Public helpers ───────────────────────────────

        /// <summary>
        /// Check and consume a buff for the moving side (call before applying a move
        /// that relies on a specific buff effect).
        /// </summary>
        public bool UseBuffIfAvailable(PieceColor color, BuffEffectType effect)
        {
            if (!BuffManager.HasActiveBuff(color, effect, MoveNumber)) return false;
            BuffManager.ConsumeBuff(color, effect);
            return true;
        }
    }
}