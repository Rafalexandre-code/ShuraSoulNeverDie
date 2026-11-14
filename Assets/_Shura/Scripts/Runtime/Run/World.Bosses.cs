using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Lords of the biomes and the Ashura in the arena (design doc §5.2): each fight's
    /// own rule (the closing fire, the webs, the dark, the hands, the frozen ring, the three
    /// arms), the phases, the Breaking Point and what the fall of the King decides (§8.6).
    /// </summary>
    public sealed partial class World
    {
        /// <summary>The Lord alive in this room (for the HUD's banner), if any.</summary>
        public Enemy BossInRoom { get; private set; }
        /// <summary>Ō-Kyūketsuki's third phase: no rings, only the blade and the sound.</summary>
        public bool BossDarkness { get; private set; }

        float fireInset;
        Transform bossProps;
        struct WebSpot
        {
            public Vector2 at;
            public float until;
            public SpriteRenderer view;
        }

        readonly List<WebSpot> webs = new List<WebSpot>();
        readonly List<Enemy> bossMinions = new List<Enemy>();
        Enemy.State bossPrevState;
        int bossTick;
        const float WebRadius = 1.15f;

        static readonly Dictionary<char, Color> FlamePal = new Dictionary<char, Color>
        {
            { 'r', ColorUtil.Hex("#D7263D") }, { 'o', ColorUtil.Hex("#FF6A2A") }, { 'y', ColorUtil.Hex("#FFC857") }, { 'w', ColorUtil.Hex("#FFF4D6") },
        };

        static Sprite Flame(int frame) => PixelArt.FromGrid("boss_flame_" + frame, frame == 0
            ? new[] { "...o..", "..oyo.", ".oyyo.", ".oywyo", "royyor", ".roor." }
            : new[] { "..o...", ".oyo..", ".oyyo.", "oywyo.", "royyor", ".roor." },
            FlamePal, new Vector2(0.5f, 0f));

        static readonly Dictionary<char, Color> WebPal = new Dictionary<char, Color> { { 'w', new Color(0.92f, 0.92f, 1f, 0.75f) } };

        static Sprite WebSprite => PixelArt.FromGrid("boss_web", new[]
        {
            "w.....w.....w",
            ".w....w....w.",
            "..wwwwwwwww..",
            "..w...w...w..",
            "..w.wwwww.w..",
            "..w.w.w.w.w..",
            "wwwwwwwwwwwww",
            "..w.w.w.w.w..",
            "..w.wwwww.w..",
            "..w...w...w..",
            "..wwwwwwwww..",
            ".w....w....w.",
            "w.....w.....w",
        }, WebPal, new Vector2(0.5f, 0.5f), 7f);

        void ClearBossRoom()
        {
            BossInRoom = null;
            BossDarkness = false;
            fireInset = 0f;
            webs.Clear();
            bossMinions.Clear();
            bossProps = null;
        }

        /// <summary>A Lord enters: its name, its first words, and its arena's rule.</summary>
        void BeginBoss(Enemy e, BossDef b)
        {
            e.MakeBoss(b);
            BossInRoom = e;
            bossPrevState = e.CurrentState;
            Fx.Toast(b.name + ", " + b.title, Config.palette.danger);
            Fx.Toast("“" + b.phaseLines[0] + "”", Config.palette.uiText);
            HintOnce(HintId.Boss);
            SetUpBossArena(e, 1);
        }

        void SetUpBossArena(Enemy e, int phase)
        {
            if (arenaRoot == null) return;
            if (bossProps != null) Object.Destroy(bossProps.gameObject);
            bossProps = new GameObject("BossArena").transform;
            bossProps.SetParent(arenaRoot, false);
            switch (e.Boss.kind)
            {
                case BossKind.Fire:
                    // The fire closes in: each phase the ring of flames takes more of the floor.
                    fireInset = phase == 1 ? 0.6f : phase == 2 ? 2.2f : 3.6f;
                    BuildFireRing();
                    break;
                case BossKind.Web:
                    SpinWebs(2 + phase * 2, phase);
                    break;
                case BossKind.Colossus:
                    // Two hands rise beside the skull.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 at = Layout.NearestFloor(e.Position + new Vector2(i == 0 ? -3f : 3f, -1.2f), BossDefs.ColossusHand.radius);
                        bossMinions.Add(CreateEnemy(BossDefs.ColossusHand, at, false, e));
                    }
                    e.SkullClosed = true;
                    break;
                case BossKind.Ashura:
                    // The other two arms float beside the King.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 at = Layout.NearestFloor(e.Position + new Vector2(i == 0 ? -2.2f : 2.2f, 1.2f), BossDefs.AshuraArm.radius);
                        bossMinions.Add(CreateEnemy(BossDefs.AshuraArm, at, false, e));
                    }
                    if (phase == 3 && Host != null && Host.IsAlive) SpawnInnerAsura(Host);
                    break;
                case BossKind.Blood:
                    BossDarkness = phase == 3;
                    break;
            }
        }

        void BuildFireRing()
        {
            Rect r = Layout.FloorBounds;
            r.xMin += fireInset;
            r.yMin += fireInset;
            r.xMax -= fireInset;
            r.yMax -= fireInset;
            var points = new List<Vector2>();
            for (float x = r.xMin; x <= r.xMax; x += 0.9f)
            {
                points.Add(new Vector2(x, r.yMin));
                points.Add(new Vector2(x, r.yMax));
            }
            for (float y = r.yMin + 0.9f; y < r.yMax; y += 0.9f)
            {
                points.Add(new Vector2(r.xMin, y));
                points.Add(new Vector2(r.xMax, y));
            }
            int i = 0;
            foreach (Vector2 p in points)
            {
                SpriteRenderer f = SpriteUtil.Make("Flame", bossProps, Flame(i++ % 2), Color.white, -Mathf.RoundToInt(p.y * 20f) + 1);
                f.transform.position = p;
                f.transform.localScale = Vector3.one * 1.6f;
            }
        }

        void SpinWebs(int count, int phase)
        {
            webs.Clear();
            Rect b = Layout.FloorBounds;
            var rng = new SeededRandom((ulong)(Depth * 131 + phase * 17 + 3));
            for (int i = 0; i < count; i++)
            {
                Vector2 p = Layout.NearestFloor(new Vector2(rng.Range(b.xMin + 2f, b.xMax - 2f), rng.Range(b.yMin + 2f, b.yMax - 2f)), WebRadius);
                AddWeb(p, 100000f);
            }
        }

        /// <summary>Jorōgumo's webs bind: steps and dodges inside them shrink.</summary>
        public float WebMult(Vector2 at)
        {
            foreach (WebSpot w in webs)
            {
                if ((w.at - at).sqrMagnitude < WebRadius * WebRadius) return 0.5f;
            }
            return 1f;
        }

        void TickBosses()
        {
            Enemy b = BossInRoom;
            if (b == null || b.IsGone) return;
            bossTick++;
            switch (b.Boss.kind)
            {
                case BossKind.Fire:
                    if (bossTick % 30 != 0) break;
                    Rect r = Layout.FloorBounds;
                    r.xMin += fireInset;
                    r.yMin += fireInset;
                    r.xMax -= fireInset;
                    r.yMax -= fireInset;
                    foreach (PlayerController p in Players)
                    {
                        if (!p.IsAlive || r.Contains(p.Position)) continue;
                        Vector2 dir = (r.center - p.Position).normalized;
                        p.ReceiveHazard(7f, 4f, dir);
                        Fx.Emit(FxType.DotTick, p.Position, Vector2.up, 7f, ColorUtil.Hex("#FF6A2A"));
                    }
                    break;
                case BossKind.Colossus:
                {
                    bool closed = false;
                    foreach (Enemy h in bossMinions)
                    {
                        if (!h.IsGone) closed = true;
                    }
                    if (b.SkullClosed && !closed)
                    {
                        Fx.Toast(Strings.ToastSkullOpen, Config.palette.gold);
                        Fx.Emit(FxType.StreakMilestone, b.Position, Vector2.up, 6f, Config.palette.gold);
                    }
                    b.SkullClosed = closed;
                    break;
                }
                case BossKind.Ashura:
                    // Three arms: when the King strikes, the other two strike with him.
                    if (b.CurrentState == Enemy.State.Attacking && bossPrevState != Enemy.State.Attacking)
                    {
                        foreach (Enemy arm in bossMinions)
                        {
                            if (!arm.IsGone) arm.Provoke();
                        }
                    }
                    break;
            }
            bossPrevState = b.CurrentState;
        }

        /// <summary>The Lord rises into its next phase (or skips one at its Breaking Point).</summary>
        public void OnBossPhase(Enemy e, bool breakingPoint)
        {
            BossDef b = e.Boss;
            Fx.Emit(FxType.StreakMilestone, e.Position, Vector2.up, 8f, Config.palette.danger);
            if (breakingPoint) Fx.Toast(string.Format(Strings.ToastBreakingPoint, b.breakName), Config.palette.gold);
            Fx.Toast(string.Format(Strings.ToastBossPhase, b.name, e.Phase), Config.palette.danger);
            Fx.Toast("“" + b.phaseLines[Mathf.Clamp(e.Phase - 1, 0, b.phaseLines.Length - 1)] + "”", Config.palette.uiText);
            foreach (Enemy m in bossMinions)
            {
                if (!m.IsGone) m.Dispel();
            }
            bossMinions.Clear();
            SetUpBossArena(e, e.Phase);
        }

        /// <summary>Kagutsuchi scatters embers when parried; Jorōgumo's web tears on a Perfect Swap.</summary>
        public void OnBossParried(Enemy e, Vector2 clash, bool swap)
        {
            if (e == null || !e.IsBoss) return;
            if (e.Boss.kind == BossKind.Fire)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 at = Layout.NearestFloor(clash + Random.insideUnitCircle * 2.2f, 0.4f);
                    ScheduleEnemyBlast(e, at, 0.9f, 42, 10f, 6f, BlastKind.Fire, false);
                }
            }
            if (e.Boss.kind == BossKind.Web && swap) e.TearWeb();
        }

        /// <summary>Ō-Kyūketsuki drinks your Flow with every blow that lands.</summary>
        void OnBossHit(Enemy e, PlayerController p)
        {
            if (e == null || !e.IsBoss || e.Boss.kind != BossKind.Blood || p.Build == null) return;
            if (p.Build.Flow.Level > 0) DropFlow(1);
            e.Mend(e.MaxHealth * 0.03f, 0f);
            Fx.Popup(Strings.PopupFlowDrunk, p.Position + Vector2.up * 1.3f, ColorUtil.Hex("#B3123A"));
        }

        /// <summary>The Lord falls: its minions go with it, the exclusive item for whoever found its Breaking Point.</summary>
        void OnBossDefeated(Enemy e)
        {
            BossDef b = e.Boss;
            BossInRoom = null;
            BossDarkness = false;
            DispelMinionsOf(e);
            bossMinions.Clear();
            if (Run == null) return;
            Run.BossesKilled++;
            Fx.Toast(string.Format(Strings.ToastBossDown, b.name), Config.palette.gold);
            if (e.BrokeThrough)
            {
                foreach (PlayerController p in Players)
                {
                    if (p.Build == null) continue;
                    Item relic = BossRelic(b, p.Build.Rng);
                    bool eq = p.Build.AddItem(relic);
                    p.ApplyStats(false);
                    PlayerController before = actor;
                    actor = p;
                    Fx.ItemCollected(relic, eq, p.Position);
                    actor = before;
                }
            }
            if (b.kind == BossKind.Ashura) Run.PendingEnding = Endings.Decide(Run, Host);
        }

        /// <summary>The exclusive item: an ancestral piece named after the Lord.</summary>
        public static Item BossRelic(BossDef b, SeededRandom rng)
        {
            Item basis = ItemGenerator.RollAt(rng, Rarity.Ancestral, null);
            var item = new Item(basis.slot, basis.rarity, basis.element, b.relicName);
            item.affixes.AddRange(basis.affixes);
            item.unique = basis.unique != UniqueAffix.None ? basis.unique : (UniqueAffix)(1 + rng.Range(0, ItemIdentity.UniqueCount));
            return item;
        }
    }

    /// <summary>What the fall of the Ashura decides (design doc §8.6).</summary>
    public static class Endings
    {
        public const string Secret = "camelias";

        public static int FallenGenerals(MetaProgress meta)
        {
            int n = 0;
            if (meta?.fallenRivals == null) return 0;
            foreach (Rival r in meta.fallenRivals)
            {
                if (r.General) n++;
            }
            return n;
        }

        /// <summary>
        /// Tsubaki's mission and every General avenged: the secret ending. At Shura 100: the
        /// new Ashura. Karma above +60: liberation. Otherwise the wheel turns again.
        /// </summary>
        public static string Decide(RunState run, PlayerController host)
        {
            MetaProgress meta = run.Meta;
            if (meta != null && meta.MissionDone(TeaGuests.Tsubaki) && FallenGenerals(meta) >= Rivals.MaxGenerals) return Secret;
            if (host?.Build != null && host.Build.Shura >= ShuraRules.Max) return GameModes.EndingShura;
            return GameModes.EndingFor(KarmaRules.Of(run)) == GameModes.EndingLiberation ? GameModes.EndingLiberation : GameModes.EndingWheel;
        }
    }
}
