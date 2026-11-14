using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 5, part 1 in the arena: the rivals of the Shuradō (§5.1), morale and surrender
    /// (§5.7) and the adaptive AI 2.0 that the player can bait (§5.6).
    /// </summary>
    public sealed partial class World
    {
        /// <summary>The rival alive in this room (for the HUD's banner), if any.</summary>
        public Enemy RivalInRoom { get; private set; }

        // ── Rivals ───────────────────────────────────────────────────────────────────

        Enemy CreateRival(EnemySpawn spawn, Vector2 at)
        {
            Rival r = Rivals.Find(Run?.Meta, spawn.rivalId);
            EnemyDef def = EnemyDefs.Get(spawn.id);
            if (r == null) return CreateEnemy(def, at, true, null);
            bool eternal = Run.Oaths.Level(Oaths.EternalRival) > 0;
            int rank = Rivals.EffectiveRank(r, eternal);
            var go = new GameObject("Rival_" + def.id);
            go.transform.SetParent(root, false);
            var enemy = go.AddComponent<Enemy>();
            EnemyScaling scaling = EnemyScaling.For(Depth, true);
            scaling.health *= Rivals.HealthMult(rank) * PartyScaling.GuardianHealth(PartySize);
            scaling.posture *= Rivals.PostureMult(rank) * (r.trait == "tecnica" || eternal ? 1.25f : 1f);
            scaling.damage *= Rivals.DamageMult(rank) * (r.trait == "arma" || eternal ? 1.15f : 1f) * (r.furious ? 1.15f : 1f);
            if (r.trait == "arma" || eternal) scaling.idle *= 0.85f;
            Run.Meta.RecordEnemy(def.id, false);
            enemy.Init(this, def, at, Config, Run.Rng, scaling, null);
            enemy.MakeRival(r);
            Enemies.Add(enemy);
            RivalInRoom = enemy;
            // The scar you left: a wound that never closed.
            if (System.Enum.TryParse(r.scar, out WoundRegion scar)) enemy.Wound(scar);
            Fx.Toast(r.name, Config.palette.danger);
            if (!string.IsNullOrEmpty(r.foreign)) Fx.Toast(string.Format(Strings.RivalLent, r.foreign), Config.palette.uiText);
            Fx.Toast("“" + Rivals.Taunt(r, Depth) + "”", Config.palette.uiText);
            HintOnce(HintId.Rival);
            return enemy;
        }

        /// <summary>Vengeance (§5.1): the rival's Ecos come back with half again, and its technique is yours.</summary>
        void OnRivalDefeated(Enemy e)
        {
            if (Run?.Meta == null || string.IsNullOrEmpty(e.RivalId)) return;
            Rival r = Rivals.Find(Run.Meta, e.RivalId);
            string name = r != null ? r.name : e.Def.displayName;
            Vengeance v = Rivals.Defeat(Run.Meta, e.RivalId);
            Run.BonusEcos += v.ecos;
            if (!string.IsNullOrEmpty(v.technique))
            {
                foreach (PlayerController p in Players) p.Build?.AddPendingTechnique(v.technique);
            }
            RecordScroll(e, ScrollEvent.RivalDefeat);
            if (RivalInRoom == e) RivalInRoom = null;
            Fx.Toast(string.Format(Strings.ToastVengeance, name, v.ecos), Config.palette.gold);
            Fx.Emit(FxType.StreakMilestone, e.Position, Vector2.up, 10f, Config.palette.gold);
        }

        // ── Morale and surrender (§5.7) ──────────────────────────────────────────────

        /// <summary>
        /// The wave's leader fell to a finisher at full Flow: the humans left in the wave may
        /// kneel or run. Yokai, the dead and elites never yield.
        /// </summary>
        void CheckMorale(Enemy fallen)
        {
            if (Run == null || TutorialRoom || fallen == null || Player?.Build == null) return;
            if (Player.Build.Flow.Level < Player.Build.Flow.MaxLevel) return;
            if (!fallen.IsElite && fallen != waveLeader && !fallen.IsRival) return;
            var rng = new SeededRandom(Run.Seed ^ (ulong)(Depth * 977 + WaveIndex * 31 + 7));
            bool any = false;
            foreach (Enemy e in Enemies)
            {
                if (e == fallen || e.IsGone || e.IsAlly || e.IsElite || e.IsRival || e.Def.minion || e.Yielding) continue;
                if (RoomGenerator.IsYokai(e.Def.id)) continue;
                float roll = rng.NextFloat();
                if (roll < 0.5f) e.Kneel();
                else if (roll < 0.8f) e.Flee(FleeTarget(e.Position));
                else continue;
                any = true;
            }
            if (!any) return;
            Fx.Toast(Strings.ToastMoraleBroken, Config.palette.gold);
            HintOnce(HintId.Morale);
        }

        Vector2 FleeTarget(Vector2 from)
        {
            Rect b = Layout.FloorBounds;
            float left = from.x - b.xMin, right = b.xMax - from.x, down = from.y - b.yMin, up = b.yMax - from.y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            if (m == left) return new Vector2(b.xMin - 2f, from.y);
            if (m == right) return new Vector2(b.xMax + 2f, from.y);
            if (m == down) return new Vector2(from.x, b.yMin - 2f);
            return new Vector2(from.x, b.yMax + 2f);
        }

        /// <summary>The first enemy of each wave leads it (or the standoff's leader).</summary>
        Enemy waveLeader;

        /// <summary>Interact beside a kneeling enemy: spared (+Bushidō), it walks away.</summary>
        void TickSparing(PlayerController p, bool pressed)
        {
            if (!pressed) return;
            foreach (Enemy e in Enemies)
            {
                if (!e.Kneeling || e.IsGone || Vector2.Distance(e.Position, p.Position) > 2.2f + e.Radius) continue;
                e.Spare();
                Fx.Popup(Strings.PopupSpared, e.Position + Vector2.up * 1.3f, ColorUtil.Hex("#7FC4FF"));
                if (Run != null) Run.Karma++;
                return;
            }
        }

        /// <summary>A blow on someone kneeling: an execution (Shura).</summary>
        public void OnExecution(Enemy e)
        {
            Fx.Popup(Strings.PopupExecuted, e.Position + Vector2.up * 1.3f, Config.palette.danger);
            if (Run != null) Run.Karma -= 2;
            Player?.AddShura(8f);
        }

        /// <summary>Whoever is still kneeling when the room falls quiet is let go.</summary>
        void SpareTheKneeling()
        {
            foreach (Enemy e in Enemies)
            {
                if (!e.Kneeling || e.IsGone) continue;
                e.Spare();
                if (Run != null) Run.Karma++;
            }
        }

        // ── The adaptive AI 2.0 (§5.6) ───────────────────────────────────────────────

        /// <summary>
        /// Counter-reading: a reading at its third mark, and the player does exactly what it
        /// does not expect. The blow carries twice the posture and that reading is wiped.
        /// </summary>
        public float CounterReading(CounterAct act, Vector2 at)
        {
            if (Run == null || TutorialRoom) return 1f;
            foreach (ReadingId id in AdaptiveTraits.All)
            {
                if (Adaptation.Level(id) < 3 || !Counters(id, act)) continue;
                Run.Profile.Forget(id);
                RefreshAdaptation();
                Fx.Popup(Strings.PopupCounterRead, at + Vector2.up * 1.6f, Config.palette.adapted);
                Fx.Toast(string.Format(Strings.ToastCounterRead, Strings.ReadingName(id)), Config.palette.adapted);
                return 2f;
            }
            return 1f;
        }

        /// <summary>What answers each reading: the opposite of the habit it was built on.</summary>
        static bool Counters(ReadingId id, CounterAct act)
        {
            switch (id)
            {
                case ReadingId.Delay: return act == CounterAct.Parry;
                case ReadingId.Guard: return act == CounterAct.Parry;
                case ReadingId.Pressure: return act == CounterAct.FrontHit;
                case ReadingId.Track: return act == CounterAct.Parry;
                case ReadingId.Danger: return act == CounterAct.Dodge;
                case ReadingId.Flank: return act == CounterAct.FrontHit;
                case ReadingId.Tool: return act == CounterAct.FrontHit;
                case ReadingId.Swap: return act == CounterAct.Parry;
                default: return act == CounterAct.Finisher;
            }
        }

        /// <summary>Did this blow come from behind the enemy (for the FLANK reading)?</summary>
        void NoteBlowDirection(Enemy e, Vector2 hitDir)
        {
            if (Run == null || e.IsGone) return;
            if (Vector2.Dot(e.Facing, hitDir) > 0.35f) Run.Profile.OnBackAttack();
        }

        /// <summary>The TOOL reading: some enemies sidestep a thrown tool.</summary>
        bool SidestepsTool(Enemy e, Projectile p)
        {
            if (Run == null || !p.Interrupts || p.Owner != null || Adaptation.Tool <= 0f) return false;
            if (e.IsElite || e.Def.superArmor || Run.Rng.NextFloat() >= Adaptation.Tool * 0.6f) return false;
            Fx.Popup(Strings.PopupSidestep, e.Position + Vector2.up * 1.2f, Config.palette.adapted);
            e.Shove(CombatMath.Perpendicular(p.Direction), 0.8f, null);
            return true;
        }

        /// <summary>The HEAL reading: an empty gourd invites pressure.</summary>
        public float HealPressure(PlayerController target)
        {
            if (target == null || Adaptation.Heal <= 0f || target.Sips > 0) return 0f;
            return Adaptation.Heal;
        }

        /// <summary>"O Shura Lembra": the reading this run ends with, kept for the next ones.</summary>
        public AdaptiveTraits FinalReading => Adaptation;
    }

    /// <summary>The player's answers that a reading can be baited into.</summary>
    public enum CounterAct
    {
        Parry,
        Dodge,
        FrontHit,
        Finisher,
    }
}
