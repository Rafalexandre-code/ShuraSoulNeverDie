using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The enemy's side of the road (design doc §6): the wounded ronin who fights beside the
    /// party, enemies frozen in the ice, the weak dead at dawn, and the Kalpa rules — enemies
    /// that chain moves, build their own Flow and inherit the strength of their fallen.
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>Fights for the party (the wounded ronin): strikes enemies, never duelists.</summary>
        public bool IsAlly { get; set; }
        /// <summary>Called once when it dies (event duels pay out here).</summary>
        public System.Action<Enemy> OnDefeated;

        int frozenFrames;
        public bool Frozen => frozenFrames > 0;

        int kalpaFlow;
        int kalpaMushinFrames;
        float inherited = 1f;
        bool chainedMove;

        /// <summary>Kalpa damage: enemy Mushin and inherited strength.</summary>
        public float KalpaPowerMult => (kalpaMushinFrames > 0 ? 1.3f : 1f) * inherited;
        public bool InKalpaMushin => kalpaMushinFrames > 0;

        // ── Ice ───────────────────────────────────────────────────────────────────────

        /// <summary>Encased in ice: untouchable until a duelist comes close or the ice gives.</summary>
        public void Freeze(int frames)
        {
            frozenFrames = frames;
            HeldInSpawn = true;
        }

        void Thaw()
        {
            if (frozenFrames <= 0) return;
            frozenFrames = 0;
            HeldInSpawn = world.Standoff.Active;
            world.Fx.Emit(FxType.SlamImpact, Position, Vector2.up, Radius * 2f);
            world.Fx.Popup(Strings.PopupThawed, Position + Vector2.up * 1.2f, Elements.Color(Element.Frost));
        }

        /// <summary>Dawn: the dead are weaker.</summary>
        public void Weaken(float mult)
        {
            MaxHealth *= mult;
            Health = Mathf.Min(Health, MaxHealth);
        }

        void TickRoad()
        {
            if (frozenFrames > 0)
            {
                frozenFrames--;
                PlayerController near = world.NearestAlivePlayer(Position);
                if (frozenFrames == 0 || (near != null && Vector2.Distance(near.Position, Position) < 2.6f))
                {
                    frozenFrames = 1;
                    Thaw();
                }
            }
            if (kalpaMushinFrames > 0) kalpaMushinFrames--;
            if (IsAlly) TickAllyTarget();
        }

        // ── Kalpa rules ──────────────────────────────────────────────────────────────

        /// <summary>A blow landed on a duelist: with the "enemy Flow" rule, three of them open Mushin.</summary>
        public void OnLandedHitKalpa()
        {
            if (world.Run == null || !world.Run.HasRule(KalpaRule.EnemyFlow)) return;
            if (++kalpaFlow < 3) return;
            kalpaFlow = 0;
            kalpaMushinFrames = 360;
            world.Fx.Popup(Strings.PopupEnemyMushin, Position + Vector2.up * 1.4f, pal.danger);
            world.Fx.Emit(FxType.Mushin, Position, Vector2.up, 1f, pal.danger);
        }

        /// <summary>Parried perfectly: the enemy's Flow drains.</summary>
        public void LoseKalpaFlow()
        {
            kalpaFlow = 0;
        }

        /// <summary>Kalpa "inheritance": a dying ally's strength passes on.</summary>
        public void Inherit()
        {
            inherited *= 1.2f;
            Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.25f);
            world.Fx.Popup(Strings.PopupInherited, Position + Vector2.up * 1.4f, pal.danger);
            world.Fx.Emit(FxType.Drain, Position, Vector2.up, 1f, pal.danger);
        }

        /// <summary>Kalpa "swapped blades": right after one move, another without a pause (once).</summary>
        int NextIdle(int normal)
        {
            if (world.Run != null && world.Run.HasRule(KalpaRule.EnemySwap) && !chainedMove && rng.NextFloat() < 0.5f)
            {
                chainedMove = true;
                return 3;
            }
            chainedMove = false;
            return normal;
        }

        int KalpaWindup(int windup)
        {
            if (world.Run != null && world.Run.HasRule(KalpaRule.SwiftBlades)) windup = Mathf.Max(6, Mathf.RoundToInt(windup * 0.88f));
            if (kalpaMushinFrames > 0) windup = Mathf.Max(6, Mathf.RoundToInt(windup * 0.85f));
            return windup;
        }

        // ── The ally ─────────────────────────────────────────────────────────────────

        void TickAllyTarget()
        {
            if (InfightTarget != null && !InfightTarget.IsGone && InfightTarget.CanBeHit) return;
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (Enemy e in world.Enemies)
            {
                if (e == this || e.IsAlly || e.IsGone || !e.CanBeHit) continue;
                float d = Vector2.Distance(e.Position, Position);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            InfightTarget = best;
            infightFrames = best != null ? 9999 : 0;
        }

        /// <summary>No foe in sight: walk beside the host. Returns true while there is nothing to fight.</summary>
        bool TickAllyIdle()
        {
            if (InfightTarget != null) return false;
            PlayerController host = world.Host;
            Vector2 to = host.Position - Position;
            float d = to.magnitude;
            Vector2 desired = d > 2.5f ? world.Layout.Steer(Position, host.Position, Radius) * Def.moveSpeed : Vector2.zero;
            Velocity = Vector2.Lerp(Velocity, desired, 0.15f);
            if (d > 0.01f) Facing = CombatMath.RotateTowards(Facing, to / d, et.turnDegreesPerSecond * SimClock.Dt);
            return true;
        }
    }
}
