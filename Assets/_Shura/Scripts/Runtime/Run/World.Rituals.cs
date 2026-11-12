using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 4, part 2 in the arena: the rituals (prayer before statues and lanterns, the bow
    /// before the standoff), the enemy scrolls' deeds, the layers that open with the runs, and
    /// the wildlife that tells the place's story.
    /// </summary>
    public sealed partial class World
    {
        Wildlife wildlife;
        readonly List<Vector2> deathSpots = new List<Vector2>();
        readonly HashSet<int> prayedHere = new HashSet<int>();
        readonly Dictionary<PlayerController, int> prayerHold = new Dictionary<PlayerController, int>();
        readonly Dictionary<PlayerController, bool> interactWas = new Dictionary<PlayerController, bool>();
        readonly HashSet<PlayerController> bowed = new HashSet<PlayerController>();
        const int PrayerFrames = 50;
        const float PrayerReach = 1.9f;

        /// <summary>Whether a system has opened for this run (the dojo opens everything).</summary>
        public bool Has(Feature f) => Run == null || TutorialRoom || Run.Has(f);

        void BeginRitualsForRoom()
        {
            deathSpots.Clear();
            prayedHere.Clear();
            prayerHold.Clear();
            bowed.Clear();
            wildlife = arenaRoot != null ? Wildlife.Attach(arenaRoot, this, CurrentRoom != null ? (int)(CurrentRoom.layoutSeed & 0x7FFFFFFF) : 0) : null;
            // The Kitsune's tea: foxes watch from the edges.
            if (CurrentRoom != null && CurrentRoom.kind == RoomKind.Event && CurrentRoom.eventId == 1) wildlife?.Foxes(Layout.FloorBounds);
        }

        void OnRoomClearedRituals()
        {
            wildlife?.CallCrows(deathSpots, Bounds);
        }

        void TickRituals()
        {
            foreach (PlayerController p in Players)
            {
                bool held = p.Input != null && p.Input.InteractHeld;
                interactWas.TryGetValue(p, out bool was);
                interactWas[p] = held;
                if (!p.IsAlive || p.Input == null) continue;
                if (held && !was && Standoff.Active) TryBow(p);
                TickPrayer(p, held);
            }
        }

        // ── The bow (§8.5) ───────────────────────────────────────────────────────────

        void TryBow(PlayerController p)
        {
            if (bowed.Contains(p) || Standoff.Leader == null) return;
            bowed.Add(p);
            p.Bow();
            Fx.Popup(Strings.PopupBow, p.Position + Vector2.up * 1.4f, ColorUtil.Hex("#7FC4FF"));
            if (Run != null && !TutorialRoom)
            {
                Run.Karma++;
                Run.Meta?.AddStat(TeaGuests.StatBows, 1);
            }
        }

        /// <summary>The draw came while the leader was still bowing: a coward's blow.</summary>
        public void OnCowardDraw(PlayerController p)
        {
            Fx.Popup(Strings.PopupCoward, p.Position + Vector2.up * 1.4f, Config.palette.danger);
            p.AddShura(10f);
            if (Run != null && !TutorialRoom) Run.Karma--;
        }

        // ── Prayer at statues and lanterns (§8.5) ─────────────────────────────────────

        void TickPrayer(PlayerController p, bool held)
        {
            if (!held || ActiveEnemyCount > 0 || Standoff.Active || Decor == null || FallenNear(p) != null || OfferingNear(p.Position))
            {
                prayerHold[p] = 0;
                return;
            }
            int target = PrayerTarget(p.Position, out Vector2 at);
            if (target < 0 || prayedHere.Contains(target))
            {
                prayerHold[p] = 0;
                return;
            }
            prayerHold.TryGetValue(p, out int frames);
            frames++;
            prayerHold[p] = frames;
            if (frames == 1) Fx.Popup(Strings.PopupPraying, p.Position + Vector2.up * 1.3f, Config.palette.uiDim);
            if (frames < PrayerFrames) return;
            prayerHold[p] = 0;
            prayedHere.Add(target);
            // Incense rises, and the place says a line of its story.
            Fx.Emit(FxType.Kamae, at + Vector2.up * 0.6f, Vector2.up, 0f, ColorUtil.Hex("#E8E0D0"));
            Fx.Toast(Strings.PrayerLine(Run != null ? (int)(Run.Seed % 97) + Depth * 3 + target : target), ColorUtil.Hex("#E8E0D0"));
            if (Run != null && !TutorialRoom) Run.Meta?.AddStat(TeaGuests.StatPrayers, 1);
        }

        bool OfferingNear(Vector2 at)
        {
            foreach (Offering o in Offerings)
            {
                if (!o.Spent && (o.Position - at).sqrMagnitude < 2.4f * 2.4f) return true;
            }
            return false;
        }

        /// <summary>The nearest standing lantern (its index) or statue (1000 + index) in reach; −1 if none.</summary>
        int PrayerTarget(Vector2 at, out Vector2 where)
        {
            int best = -1;
            float bestD = PrayerReach * PrayerReach;
            where = at;
            for (int i = 0; i < Decor.Lanterns.Count; i++)
            {
                if (BrokenLanterns.Contains(i)) continue;
                float d = (Decor.Lanterns[i] - at).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                    where = Decor.Lanterns[i];
                }
            }
            for (int i = 0; i < Decor.Statues.Count; i++)
            {
                if (BrokenStatues.Contains(i)) continue;
                Vector4 s = Decor.Statues[i];
                Vector2 c = Layout.CellCenter((int)s.x, (int)s.y) + Vector2.one * ((s.z - 1f) * 0.5f);
                float reach = PrayerReach + s.z * 0.5f;
                float d = (c - at).sqrMagnitude;
                if (d < reach * reach && d < bestD + s.z * s.z)
                {
                    bestD = d;
                    best = 1000 + i;
                    where = c;
                }
            }
            return best;
        }

        // ── The enemy scrolls (§7.6) ─────────────────────────────────────────────────

        public void RecordScroll(Enemy e, ScrollEvent deed, string weapon = null)
        {
            if (e == null || e.IsAlly || Run?.Meta == null || TutorialRoom) return;
            if (!EnemyScrolls.Record(Run.Meta, e.Def.id, deed, weapon)) return;
            if (!Run.Meta.MissionDone(TeaGuests.Warashi)) return;
            Fx.Toast(string.Format(Strings.ToastScrollDone, e.Def.displayName, EnemyScrolls.FragmentReward), Config.palette.gold);
        }

        void NoteDeathForRituals(Enemy enemy)
        {
            if (enemy.IsAlly) return;
            deathSpots.Add(enemy.Position);
            if (!enemy.DamagedPlayer) RecordScroll(enemy, ScrollEvent.CleanKill);
        }
    }
}
