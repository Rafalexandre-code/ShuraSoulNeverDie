using System.Collections.Generic;

namespace Shura
{
    /// <summary>
    /// Limits how many enemies attack at once, with a minimum gap between attacks. Solo it is
    /// one at a time: you duel one opponent while the others pressure. In co-op the limit
    /// grows with the party (Capacity), so each player can have a duel of their own.
    /// </summary>
    public sealed class AttackCoordinator
    {
        readonly List<object> holders = new List<object>();
        int cooldown;

        public int MinGapFrames { get; set; }
        public int Capacity { get; set; } = 1;
        /// <summary>Any one active attacker (debug overlay).</summary>
        public object Holder => holders.Count > 0 ? holders[0] : null;
        public bool IsFull => holders.Count >= Capacity;

        public AttackCoordinator(int minGapFrames)
        {
            MinGapFrames = minGapFrames;
        }

        public bool TryAcquire(object who)
        {
            if (holders.Contains(who)) return true;
            if (holders.Count >= Capacity || cooldown > 0) return false;
            holders.Add(who);
            return true;
        }

        /// <summary>Ripostes ignore the gap but still respect the attacker limit.</summary>
        public bool ForceAcquire(object who)
        {
            if (holders.Contains(who)) return true;
            if (holders.Count >= Capacity) return false;
            holders.Add(who);
            return true;
        }

        public void Release(object who)
        {
            if (!holders.Remove(who)) return;
            cooldown = MinGapFrames;
        }

        public bool IsHeldBy(object who)
        {
            return holders.Contains(who);
        }

        public void Tick()
        {
            if (holders.Count < Capacity && cooldown > 0) cooldown--;
        }

        public void Reset()
        {
            holders.Clear();
            cooldown = 0;
        }
    }
}
