using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessButWeird
{
    /// <summary>
    /// Manages ARAM buff granting and tracking.
    /// Attach alongside ARAMChessGame.
    /// </summary>
    public class ARAMBuffManager : MonoBehaviour
    {
        // ───────────────────────────── Inspector ───────────────────────────────────

        [Header("Buff Pools")]
        public List<BuffDefinition> SilverBuffPool  = new();
        public List<BuffDefinition> GoldBuffPool    = new();
        public List<BuffDefinition> DiamondBuffPool = new();

        [Header("Phase Thresholds (full-move number)")]
        public int OpeningEnd  = 15;  // move 1‑15  → Opening
        public int MidgameEnd  = 40;  // move 16‑40 → Midgame
                                      // move 41+   → Endgame

        [Header("Buffs Granted Per Phase (each player)")]
        [Tooltip("1 guaranteed Silver + this many extra random buffs")]
        public int OpeningExtraBuffs  = 0;
        [Tooltip("1 guaranteed Gold + this many extra random buffs")]
        public int MidgameExtraBuffs  = 1;
        [Tooltip("1 guaranteed Diamond + this many extra random buffs")]
        public int EndgameExtraBuffs  = 2;

        [Header("Extra Buff Rarity Weights")]
        [Range(0, 1)] public float SilverWeight  = 0.60f;
        [Range(0, 1)] public float GoldWeight    = 0.30f;
        // Diamond weight = 1 - Silver - Gold

        // ───────────────────────────── Events ──────────────────────────────────────

        public event Action<ActiveBuff>  OnBuffGranted;
        public event Action<GamePhase>   OnPhaseChanged;

        // ───────────────────────────── State ───────────────────────────────────────

        private readonly List<ActiveBuff> _whiteBuffs = new();
        private readonly List<ActiveBuff> _blackBuffs = new();

        private GamePhase _currentPhase = GamePhase.Opening;
        private bool _openingDone, _midgameDone, _endgameDone;

        // ───────────────────────────── Public API ──────────────────────────────────

        public IReadOnlyList<ActiveBuff> GetBuffs(PieceColor color) =>
            color == PieceColor.White ? _whiteBuffs : _blackBuffs;

        public bool HasActiveBuff(PieceColor color, BuffEffectType effect, int currentMove)
        {
            var list = GetBuffs(color);
            foreach (var b in list)
                if (b.Definition.EffectType == effect && b.IsActive(currentMove))
                    return true;
            return false;
        }

        /// <summary>Mark a one-shot buff as consumed.</summary>
        public void ConsumeBuff(PieceColor color, BuffEffectType effect)
        {
            var list = color == PieceColor.White ? _whiteBuffs : _blackBuffs;
            foreach (var b in list)
            {
                if (b.Definition.EffectType == effect && !b.IsConsumed)
                {
                    b.IsConsumed = true;
                    Debug.Log($"[ARAM] {color} consumed buff: {b.Definition.BuffName}");
                    return;
                }
            }
        }

        /// <summary>Call this from ARAMChessGame after every move.</summary>
        public void OnMoveMade(ChessGame game)
        {
            int move = game.MoveNumber;

            // Expire old buffs
            PruneExpired(_whiteBuffs, move);
            PruneExpired(_blackBuffs, move);

            // Detect phase change
            GamePhase newPhase = game.GetCurrentPhase();
            if (newPhase != _currentPhase)
            {
                _currentPhase = newPhase;
                OnPhaseChanged?.Invoke(newPhase);
                Debug.Log($"[ARAM] Phase changed → {newPhase}");
            }

            // Grant phase buffs (once per phase)
            TryGrantPhaseBuffs(move);
        }

        // ───────────────────────────── Private ─────────────────────────────────────

        private void TryGrantPhaseBuffs(int move)
        {
            switch (_currentPhase)
            {
                case GamePhase.Opening when !_openingDone:
                    GrantPhase(move, BuffRarity.Silver, OpeningExtraBuffs);
                    _openingDone = true;
                    break;

                case GamePhase.Midgame when !_midgameDone:
                    GrantPhase(move, BuffRarity.Gold, MidgameExtraBuffs);
                    _midgameDone = true;
                    break;

                case GamePhase.Endgame when !_endgameDone:
                    GrantPhase(move, BuffRarity.Diamond, EndgameExtraBuffs);
                    _endgameDone = true;
                    break;
            }
        }

        private void GrantPhase(int move, BuffRarity guaranteed, int extraCount)
        {
            foreach (var color in new[] { PieceColor.White, PieceColor.Black })
            {
                // 1 guaranteed buff of the phase rarity
                var main = RandomBuff(guaranteed);
                if (main != null) Grant(main, color, move);

                // Extra random buffs (weighted rarity)
                for (int i = 0; i < extraCount; i++)
                {
                    var extra = RandomBuffWeighted();
                    if (extra != null) Grant(extra, color, move);
                }
            }
        }

        private void Grant(BuffDefinition def, PieceColor color, int move)
        {
            var active = new ActiveBuff
            {
                Definition    = def,
                Owner         = color,
                AppliedOnMove = move,
                IsConsumed    = false
            };
            (color == PieceColor.White ? _whiteBuffs : _blackBuffs).Add(active);
            OnBuffGranted?.Invoke(active);
            Debug.Log($"[ARAM] {color} → {def.Rarity} buff: \"{def.BuffName}\"");
        }

        private BuffDefinition RandomBuff(BuffRarity rarity)
        {
            var pool = rarity switch
            {
                BuffRarity.Silver  => SilverBuffPool,
                BuffRarity.Gold    => GoldBuffPool,
                BuffRarity.Diamond => DiamondBuffPool,
                _                  => SilverBuffPool
            };
            return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
        }

        private BuffDefinition RandomBuffWeighted()
        {
            float r = Random.value;
            if (r < SilverWeight)                    return RandomBuff(BuffRarity.Silver);
            if (r < SilverWeight + GoldWeight)       return RandomBuff(BuffRarity.Gold);
            return RandomBuff(BuffRarity.Diamond);
        }

        private static void PruneExpired(List<ActiveBuff> list, int move) =>
            list.RemoveAll(b => b.IsExpired(move));
    }
}