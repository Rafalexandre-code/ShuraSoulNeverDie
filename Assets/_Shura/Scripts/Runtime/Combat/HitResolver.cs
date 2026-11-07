using UnityEngine;

namespace Shura
{
    public enum HitOutcome
    {
        Hit,
        Blocked,
        PerfectParry,
        /// <summary>Troca Perfeita: swapping weapons inside the window parries and triggers a Swap Strike.</summary>
        SwapParry,
        PerfectDodge,
        CounterThrust,
        Ignored,
    }

    /// <summary>What a player strike did to an enemy.</summary>
    public enum StrikeResult
    {
        None,
        Deflected,
        Hit,
    }

    /// <summary>A swing or projectile reaching the player.</summary>
    public struct IncomingAttack
    {
        public Enemy attacker;
        public AttackDef attack;
        /// <summary>Where the blow comes from (the attacker, or just behind an arrow).</summary>
        public Vector2 source;
        public bool signature;
        public bool projectile;
        /// <summary>Damage/posture multiplier from depth (0 is treated as 1).</summary>
        public float powerMult;
    }

    /// <summary>Snapshot of everything about the defender that decides how an enemy swing lands.</summary>
    public struct DefenderState
    {
        public bool dodgeIFrames;
        public bool hurtInvulnerable;
        public bool parryWindow;
        public bool guarding;
        /// <summary>Inside a weapon swap's parry window. Unlike guarding, a swap has no block fallback.</summary>
        public bool swapWindow;
        public Vector2 facing;
        public Vector2 toAttacker;
        public float parryArcDegrees;
        public Vector2 dodgeDirection;
        public float counterDot;
    }

    /// <summary>
    /// The single source of truth for "what happens when this swing touches the player".
    /// Pure and deterministic so the rules can be unit-tested frame by frame.
    /// </summary>
    public static class HitResolver
    {
        public static HitOutcome Resolve(AttackDef attack, in DefenderState d)
        {
            if (d.dodgeIFrames)
            {
                if (attack.kind == AttackKind.Thrust && IsDodgingToward(d)) return HitOutcome.CounterThrust;
                return HitOutcome.PerfectDodge;
            }

            if (d.hurtInvulnerable) return HitOutcome.Ignored;

            // Parry is directional: a swing from outside the guarded arc (e.g. from behind) lands.
            if (attack.Parryable && CombatMath.WithinArc(d.facing, d.toAttacker, d.parryArcDegrees))
            {
                if (d.swapWindow) return HitOutcome.SwapParry;
                if (d.parryWindow) return HitOutcome.PerfectParry;
                if (d.guarding) return HitOutcome.Blocked;
            }

            return HitOutcome.Hit;
        }

        static bool IsDodgingToward(in DefenderState d)
        {
            if (d.dodgeDirection.sqrMagnitude < 0.01f || d.toAttacker.sqrMagnitude < 1e-6f) return false;
            return Vector2.Dot(d.dodgeDirection.normalized, d.toAttacker.normalized) >= d.counterDot;
        }
    }
}
