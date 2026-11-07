using UnityEngine;

namespace Shura
{
    public enum HitOutcome
    {
        Hit,
        Blocked,
        PerfectParry,
        PerfectDodge,
        CounterThrust,
        Ignored,
    }

    /// <summary>Snapshot of everything about the defender that decides how an enemy swing lands.</summary>
    public struct DefenderState
    {
        public bool dodgeIFrames;
        public bool hurtInvulnerable;
        public bool parryWindow;
        public bool guarding;
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
