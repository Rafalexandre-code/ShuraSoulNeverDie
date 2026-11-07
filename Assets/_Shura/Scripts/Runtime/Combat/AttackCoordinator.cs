namespace Shura
{
    /// <summary>
    /// Only one enemy may attack at a time, with a minimum gap between attacks. This keeps
    /// fights against groups readable: you duel one opponent while the others pressure.
    /// </summary>
    public sealed class AttackCoordinator
    {
        object holder;
        int cooldown;

        public int MinGapFrames { get; set; }
        public object Holder => holder;

        public AttackCoordinator(int minGapFrames)
        {
            MinGapFrames = minGapFrames;
        }

        public bool TryAcquire(object who)
        {
            if (holder == who) return true;
            if (holder != null || cooldown > 0) return false;
            holder = who;
            return true;
        }

        /// <summary>Ripostes ignore the gap but still respect another active attacker.</summary>
        public bool ForceAcquire(object who)
        {
            if (holder != null && holder != who) return false;
            holder = who;
            return true;
        }

        public void Release(object who)
        {
            if (holder != who) return;
            holder = null;
            cooldown = MinGapFrames;
        }

        public bool IsHeldBy(object who)
        {
            return holder == who;
        }

        public void Tick()
        {
            if (holder == null && cooldown > 0) cooldown--;
        }

        public void Reset()
        {
            holder = null;
            cooldown = 0;
        }
    }
}
