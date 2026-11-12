using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Iai standoff (design doc §3.8): some rooms open with the duelists facing the wave's
    /// leader. Hold Attack and release the instant he truly advances — he feints 0 to 3 times
    /// first (more with depth). A clean draw kills him (at full Flow, up to three more fall);
    /// releasing early, or never, costs 30 health before the fight begins.
    /// </summary>
    public sealed class Standoff
    {
        public const int AdvanceWindow = 14;
        const float Penalty = 30f;

        readonly World world;
        readonly HashSet<PlayerController> failed = new HashSet<PlayerController>();
        int feintsLeft;
        int timer;
        int advanceFrame;
        SeededRandom rng;

        public bool Active { get; private set; }
        /// <summary>The leader is still bowing (§8.5): drawing now is a coward's blow.</summary>
        public int BowFrames { get; private set; }
        public const int BowLength = 60;
        public bool Advancing { get; private set; }
        public Enemy Leader { get; private set; }
        /// <summary>0..1 how far into the true advance (for the bot and the browser).</summary>
        public float AdvanceProgress => Advancing ? advanceFrame / (float)AdvanceWindow : 0f;

        public Standoff(World owner)
        {
            world = owner;
        }

        public void Begin(Enemy leader, int feints, SeededRandom random)
        {
            Leader = leader;
            rng = random;
            feintsLeft = feints;
            timer = random.Range(70, 120) + BowLength;
            BowFrames = BowLength;
            leader.StandoffBow();
            world.Fx.Popup(Strings.PopupFoeBows, leader.Position + Vector2.up * 1.4f, Color.white);
            Advancing = false;
            advanceFrame = 0;
            failed.Clear();
            Active = true;
            world.Fx.Emit(FxType.Standoff, leader.Position, Vector2.up, 1f, Color.white);
            world.HintOnce(HintId.Standoff);
        }

        public void Tick()
        {
            if (!Active) return;
            if (Leader == null || Leader.IsGone)
            {
                End();
                return;
            }
            if (BowFrames > 0)
            {
                BowFrames--;
                if (BowFrames % 20 == 0 && BowFrames > 0) Leader.StandoffBow();
            }
            if (!Advancing)
            {
                if (--timer > 0) return;
                if (feintsLeft > 0)
                {
                    // A twitch of the hand on the hilt: not yet.
                    feintsLeft--;
                    timer = rng.Range(45, 95);
                    Leader.StandoffTwitch();
                    world.Fx.Emit(FxType.StandoffFeint, Leader.Position, Leader.Facing, 1f, Color.white);
                    return;
                }
                Advancing = true;
                advanceFrame = 0;
                world.Fx.Emit(FxType.StandoffAdvance, Leader.Position, Leader.Facing, 1f, Color.white);
                return;
            }
            advanceFrame++;
            PlayerController target = world.NearestAlivePlayer(Leader.Position);
            if (target != null) Leader.StandoffLunge(target.Position);
            if (advanceFrame <= AdvanceWindow) return;
            // Nobody drew in time: everyone still waiting pays.
            foreach (PlayerController p in world.Players)
            {
                if (p.IsAlive && !failed.Contains(p)) Punish(p);
            }
            End();
        }

        /// <summary>A duelist let go of the hilt.</summary>
        public void OnRelease(PlayerController p)
        {
            if (!Active || failed.Contains(p)) return;
            if (BowFrames > 0) world.OnCowardDraw(p);
            if (Advancing && advanceFrame <= AdvanceWindow)
            {
                Win(p);
                return;
            }
            Punish(p);
            // Everyone drew too early: the leader charges.
            bool anyLeft = false;
            foreach (PlayerController q in world.Players)
            {
                if (q.IsAlive && !failed.Contains(q)) anyLeft = true;
            }
            if (!anyLeft) End();
        }

        void Punish(PlayerController p)
        {
            failed.Add(p);
            p.StandoffCut(Penalty);
        }

        void Win(PlayerController p)
        {
            Enemy leader = Leader;
            world.IaiKill(p, leader);
            // At full Flow the draw carries on into up to three more.
            if (p.Build != null && p.Build.Flow.Level >= p.Build.Flow.MaxLevel)
            {
                int extra = 0;
                foreach (Enemy e in world.Enemies)
                {
                    if (extra >= 3 || e == leader || e.IsGone || !e.HeldInSpawn) continue;
                    if (Vector2.Distance(e.Position, p.Position) > 9f) continue;
                    world.IaiKill(p, e);
                    extra++;
                }
            }
            End();
        }

        void End()
        {
            Active = false;
            BowFrames = 0;
            Advancing = false;
            foreach (Enemy e in world.Enemies) e.HeldInSpawn = false;
            Leader = null;
        }
    }
}
