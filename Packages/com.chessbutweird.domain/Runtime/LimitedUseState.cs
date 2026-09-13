using System;

namespace ChessButWeird.Domain
{
    /// <summary>Copyable per-match charges and cooldown, independent of the buff's presentation.</summary>
    public readonly struct LimitedUseState
    {
        public readonly int MaximumUses, Uses, Cooldown;
        public bool CanUse => Uses < MaximumUses && Cooldown == 0;
        public LimitedUseState(int maximumUses, int uses = 0, int cooldown = 0)
        {
            if (maximumUses < 1) throw new ArgumentOutOfRangeException(nameof(maximumUses));
            MaximumUses = maximumUses;
            Uses = Math.Min(maximumUses, Math.Max(0, uses));
            Cooldown = Math.Max(0, cooldown);
        }
        public LimitedUseState Tick() => new LimitedUseState(MaximumUses, Uses, Math.Max(0, Cooldown - 1));
        public bool TryConsume(int cooldown, out LimitedUseState next)
        {
            next = this;
            if (!CanUse) return false;
            next = new LimitedUseState(MaximumUses, Uses + 1, cooldown);
            return true;
        }
    }
}
