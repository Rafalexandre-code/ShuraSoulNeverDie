using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>A patch of ground with a rule: spores, a blood pool, the corrupted mist, burning oil, thin ice.</summary>
    public enum ZoneKind
    {
        /// <summary>Musgo: inside, the timing rings cannot be seen.</summary>
        Spores,
        /// <summary>Sangue: standing in it feeds the Shura.</summary>
        BloodPool,
        /// <summary>Corrupted Zone: drains duelists' breath, strengthens yokai.</summary>
        Mist,
        /// <summary>Spilled lantern oil, burning: hurts whoever stands in it.</summary>
        Fire,
        /// <summary>Gelo: breaks under heavy blades.</summary>
        ThinIce,
    }

    public sealed class Zone
    {
        public ZoneKind Kind;
        public Vector2 Position;
        public float Radius;
        /// <summary>Frames left (-1 = the whole room).</summary>
        public int Frames = -1;
        public float Weight;
        public SpriteRenderer View;
    }

    /// <summary>Things in the arena a blade can break (design doc §6.5).</summary>
    public enum PropKind
    {
        Bamboo,
        Shoji,
        /// <summary>The Corrupted Zone's core stone.</summary>
        Core,
        /// <summary>A hole (darkness edges, broken ice): only drawn.</summary>
        Pit,
        /// <summary>A felled bamboo lying on the floor: only drawn.</summary>
        FallenBamboo,
    }

    public sealed class Prop
    {
        public PropKind Kind;
        public Vector2 Position;
        public Vector2Int Cell;
        public float Health;
        public bool Gone;
        public float Angle;
        public GameObject View;
    }

    /// <summary>
    /// The world's side of the road (design doc §6.3–6.5): each biome changes a rule, the
    /// weather and the hour change who comes and how blades behave, a corrupted zone may
    /// poison the room, and the arena itself can be cut, torn and broken.
    /// </summary>
    public sealed partial class World
    {
        // ── Knobs the room sets ───────────────────────────────────────────────────────
        public float HealMultWorld { get; private set; } = 1f;
        public float LifestealMultWorld { get; private set; } = 1f;
        public float MoveMultWorld { get; private set; } = 1f;
        public float FireDotMult { get; private set; } = 1f;
        public float LightningMult { get; private set; } = 1f;
        /// <summary>Abismo: how far a duelist sees (0 = no darkness).</summary>
        public float VisionRadius { get; private set; }
        /// <summary>Abismo: timing rings only show this close (0 = anywhere).</summary>
        public float RingRange { get; private set; }
        /// <summary>Fog: enemies fade beyond this distance (0 = no fog).</summary>
        public float FogRange { get; private set; }
        public Vector2 Wind { get; private set; }
        /// <summary>Yomi: every enemy is ethereal until a perfect answer.</summary>
        public bool AllEthereal { get; private set; }
        public Weather Weather { get; private set; }
        public TimeOfDay TimeOfDay { get; private set; }
        public BiomeRule BiomeRule { get; private set; }

        public readonly List<Zone> Zones = new List<Zone>();
        public readonly List<Prop> Props = new List<Prop>();
        /// <summary>Indices (in the room's decor) of statues and lanterns already broken.</summary>
        public readonly List<int> BrokenStatues = new List<int>();
        public readonly List<int> BrokenLanterns = new List<int>();
        readonly Dictionary<int, int> statueHits = new Dictionary<int, int>();
        Transform conditionRoot;
        int hazardTimer;
        SeededRandom envRng;

        void ClearConditions()
        {
            Zones.Clear();
            Props.Clear();
            BrokenStatues.Clear();
            BrokenLanterns.Clear();
            statueHits.Clear();
            if (conditionRoot != null) Object.Destroy(conditionRoot.gameObject);
            conditionRoot = null;
            HealMultWorld = LifestealMultWorld = MoveMultWorld = FireDotMult = LightningMult = 1f;
            VisionRadius = RingRange = FogRange = 0f;
            Wind = Vector2.zero;
            AllEthereal = false;
            StaminaRegenMult = 1f;
            DodgeDistanceMult = 1f;
            ShuraGainMult = 1f;
        }

        /// <summary>Called after the arena is built: rules, weather, hazards and props for this room.</summary>
        void ApplyConditions(RoomDef room)
        {
            ClearConditions();
            conditionRoot = new GameObject("Conditions").transform;
            conditionRoot.SetParent(root, false);
            envRng = new SeededRandom(RoomGenerator.Mix(room.layoutSeed, 4242));
            Weather = room.weather;
            TimeOfDay = room.time;
            BiomeRule = room.biome.rule;
            hazardTimer = envRng.Range(180, 300);
            bool fight = !room.IsEncounter;

            switch (room.biome.rule)
            {
                case BiomeRule.Embers:
                    FireDotMult *= 2f;
                    break;
                case BiomeRule.WetMoss:
                    DodgeDistanceMult = 1.3f;
                    if (fight) for (int i = 0; i < 3; i++) AddZone(ZoneKind.Spores, RandomOpenPoint(2.5f), 2.2f);
                    break;
                case BiomeRule.BloodMoon:
                    HealMultWorld = 0.5f;
                    LifestealMultWorld = 2f;
                    if (fight) for (int i = 0; i < 3; i++) AddZone(ZoneKind.BloodPool, RandomOpenPoint(1.5f), 1.3f);
                    break;
                case BiomeRule.Darkness:
                    VisionRadius = 5.5f;
                    RingRange = 5f;
                    if (fight) CarveEdges();
                    break;
                case BiomeRule.Cold:
                    StaminaRegenMult = 0.6f;
                    if (fight) for (int i = 0; i < 3; i++) AddZone(ZoneKind.ThinIce, CellCenterAt(RandomOpenPoint(1.5f)), 0.9f);
                    break;
                case BiomeRule.Bamboo:
                    if (fight) PlaceBamboo();
                    break;
                case BiomeRule.Castle:
                    if (fight) PlaceShoji();
                    break;
                case BiomeRule.River:
                    MoveMultWorld = 0.9f;
                    LightningMult *= 1.5f;
                    break;
                case BiomeRule.Yomi:
                    AllEthereal = fight;
                    break;
            }

            switch (Weather)
            {
                case Weather.Rain:
                    FireDotMult *= 0.5f;
                    LightningMult *= 1.5f;
                    break;
                case Weather.Wind:
                    Wind = CombatMath.FromAngle(envRng.Range(0f, 360f));
                    break;
                case Weather.Fog:
                    FogRange = 6f;
                    break;
                case Weather.FullMoon:
                    ShuraGainMult = 1.5f;
                    break;
            }

            if (room.corrupted) PlaceCorruption();
            BuildConditionViews();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        Vector2 RandomOpenPoint(float clearance)
        {
            for (int i = 0; i < 30; i++)
            {
                Vector2 p = Layout.RandomFloorPoint(envRng, clearance);
                if (Vector2.Distance(p, PlayerSpawn) > 4f) return p;
            }
            return Layout.RandomFloorPoint(envRng, clearance);
        }

        Vector2 CellCenterAt(Vector2 p)
        {
            Vector2Int c = Layout.CellAt(p);
            return Layout.CellCenter(c.x, c.y);
        }

        Zone AddZone(ZoneKind kind, Vector2 at, float radius, int frames = -1, float weight = 0f)
        {
            var z = new Zone { Kind = kind, Position = at, Radius = radius, Frames = frames, Weight = weight };
            Zones.Add(z);
            if (conditionRoot != null) BuildZoneView(z);
            return z;
        }

        public static Color ZoneColor(ZoneKind k)
        {
            switch (k)
            {
                case ZoneKind.Spores: return ColorUtil.Hex("#B8D46A");
                case ZoneKind.BloodPool: return ColorUtil.Hex("#8E0F1E");
                case ZoneKind.Mist: return ColorUtil.Hex("#8E44AD");
                case ZoneKind.Fire: return ColorUtil.Hex("#FF8C2B");
                default: return ColorUtil.Hex("#CFEFFF");
            }
        }

        void BuildZoneView(Zone z)
        {
            Color c = ZoneColor(z.Kind);
            float a = z.Kind == ZoneKind.Mist ? 0.28f : z.Kind == ZoneKind.BloodPool ? 0.75f : z.Kind == ZoneKind.ThinIce ? 0.35f : 0.4f;
            int order = z.Kind == ZoneKind.Spores || z.Kind == ZoneKind.Mist ? Sorting.Overlay - 30 : Sorting.Ground + 1;
            z.View = SpriteUtil.Make("Zone", conditionRoot, ShapeLibrary.Get(Shape.SoftCircle), c.WithAlpha(a), order);
            z.View.transform.position = z.Position;
            z.View.transform.localScale = Vector3.one * z.Radius * 2.4f;
        }

        public bool InZone(ZoneKind kind, Vector2 p)
        {
            foreach (Zone z in Zones)
            {
                if (z.Kind == kind && Vector2.Distance(z.Position, p) <= z.Radius) return true;
            }
            return false;
        }

        /// <summary>Whether the timing ring of an enemy at <paramref name="enemy"/> shows to a duelist at <paramref name="viewer"/>.</summary>
        public bool RingVisibleTo(Vector2 viewer, Vector2 enemy)
        {
            if (RingRange > 0f && Vector2.Distance(viewer, enemy) > RingRange) return false;
            if (InZone(ZoneKind.Spores, viewer)) return false;
            return true;
        }

        /// <summary>Full moon raises enemy damage; the corrupted mist feeds the yokai standing in it.</summary>
        public float EnemyDamageMultFor(Enemy e)
        {
            float m = Weather == Weather.FullMoon ? 1.1f : 1f;
            if (RoomGenerator.IsYokai(e.Def.id) && InZone(ZoneKind.Mist, e.Position)) m *= 1.25f;
            return m;
        }

        // ── Per tick ─────────────────────────────────────────────────────────────────

        void TickConditions()
        {
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                Zone z = Zones[i];
                if (z.Frames > 0 && --z.Frames == 0)
                {
                    if (z.View != null) Object.Destroy(z.View.gameObject);
                    Zones.RemoveAt(i);
                    continue;
                }
                TickZone(z);
            }

            // The dojo is a quiet place: no eruptions, no lightning.
            if (!roomActive || ActiveEnemyCount == 0 || TutorialRoom) return;
            if (--hazardTimer > 0) return;
            hazardTimer = envRng.Range(200, 320);
            // Ash eruptions and storm lightning: marked circles that go off after a warning.
            if (BiomeRule == BiomeRule.Embers)
            {
                Vector2 at = Layout.NearestFloor(NearHazardTarget(false) + Random2(2.5f), 0.4f);
                ScheduleEnemyBlast(null, at, 1.5f, 80, 16f, 10f, BlastKind.Fire, true);
            }
            if (Weather == Weather.Storm)
            {
                Vector2 at = Layout.NearestFloor(NearHazardTarget(true) + Random2(1.2f), 0.4f);
                ScheduleEnemyBlast(null, at, 1.2f, 70, 22f, 14f, BlastKind.Lightning, true);
            }
        }

        Vector2 Random2(float r) => new Vector2(envRng.Range(-r, r), envRng.Range(-r, r));

        /// <summary>A duelist to aim a hazard near; lightning prefers steel (anything but bare fists).</summary>
        Vector2 NearHazardTarget(bool steel)
        {
            PlayerController best = null;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive) continue;
                if (best == null) best = p;
                if (steel && p.Weapon.id != "manoplas" && envRng.NextFloat() < 0.7f) best = p;
            }
            return best != null ? best.Position : Bounds.center;
        }

        void TickZone(Zone z)
        {
            switch (z.Kind)
            {
                case ZoneKind.BloodPool:
                    foreach (PlayerController p in Players)
                    {
                        if (p.IsAlive && Vector2.Distance(p.Position, z.Position) <= z.Radius) p.AddShura(6f * SimClock.Dt);
                    }
                    break;
                case ZoneKind.Mist:
                    foreach (PlayerController p in Players)
                    {
                        if (p.IsAlive && Vector2.Distance(p.Position, z.Position) <= z.Radius) p.DrainBreath(12f * SimClock.Dt);
                    }
                    break;
                case ZoneKind.Fire:
                    if (TrapTick % 20 != 0) break;
                    foreach (PlayerController p in Players)
                    {
                        if (p.IsAlive && Vector2.Distance(p.Position, z.Position) <= z.Radius) p.ReceiveHazard(3f, 0f, Vector2.up);
                    }
                    foreach (Enemy e in Enemies)
                    {
                        if (e.CanBeHit && !e.Def.flying && Vector2.Distance(e.Position, z.Position) <= z.Radius) e.ApplyDot(6f, 1f, Element.Fire);
                    }
                    break;
                case ZoneKind.ThinIce:
                    // A heavy blade standing on thin ice for a second goes through it.
                    foreach (PlayerController p in Players)
                    {
                        bool heavy = p.Weapon.id == "montante" || p.Weapon.id == "kanabo";
                        bool on = p.IsAlive && heavy && Vector2.Distance(p.Position, z.Position) <= z.Radius;
                        z.Weight = on ? z.Weight + SimClock.Dt : Mathf.Max(0f, z.Weight - SimClock.Dt);
                        if (z.Weight < 1f) continue;
                        BreakIce(z, p);
                        break;
                    }
                    break;
            }
        }

        void BreakIce(Zone z, PlayerController victim)
        {
            Vector2Int c = Layout.CellAt(z.Position);
            Layout.MakePit(c.x, c.y);
            z.Frames = 1;
            AddProp(PropKind.Pit, Layout.CellCenter(c.x, c.y), c, 0f);
            Fx.Emit(FxType.SlamImpact, z.Position, Vector2.up, 1.4f);
            Fx.Popup(Strings.PopupIceBroke, z.Position + Vector2.up, ZoneColor(ZoneKind.ThinIce));
            if (victim != null)
            {
                victim.ReceiveHazard(12f, 15f, Vector2.up);
                victim.Teleport(Layout.NearestFloor(victim.Position + Vector2.left * 1.2f, victim.Radius));
            }
        }

        // ── Corrupted zone ───────────────────────────────────────────────────────────

        void PlaceCorruption()
        {
            Vector2 at = RandomOpenPoint(1.5f);
            AddZone(ZoneKind.Mist, at, 4.5f);
            AddProp(PropKind.Core, at, Layout.CellAt(at), 60f);
            HintOnce(HintId.Corruption);
        }

        // ── Darkness edges ───────────────────────────────────────────────────────────

        void CarveEdges()
        {
            // A few drops along the borders: shove enemies into them, mind your own step.
            int made = 0;
            for (int attempt = 0; attempt < 200 && made < 6; attempt++)
            {
                int x = envRng.Range(2, Layout.W - 2);
                int y = envRng.Range(2, Layout.H - 2);
                if (!Layout.IsFloor(x, y)) continue;
                bool border = !Layout.IsFloor(x + 1, y) || !Layout.IsFloor(x - 1, y) || !Layout.IsFloor(x, y + 1) || !Layout.IsFloor(x, y - 1);
                if (!border) continue;
                Vector2 c = Layout.CellCenter(x, y);
                if (Vector2.Distance(c, PlayerSpawn) < 4f) continue;
                Layout.MakePit(x, y);
                AddProp(PropKind.Pit, c, new Vector2Int(x, y), 0f);
                made++;
            }
        }

        // ── Bamboo and shōji ─────────────────────────────────────────────────────────

        void PlaceBamboo()
        {
            int clusters = envRng.Range(3, 6);
            for (int k = 0; k < clusters; k++)
            {
                Vector2 seed = RandomOpenPoint(2f);
                Vector2Int c = Layout.CellAt(seed);
                int n = envRng.Range(2, 4);
                for (int i = 0; i < n; i++)
                {
                    int x = c.x + (i % 2), y = c.y + (i / 2);
                    if (!Layout.IsOpen(x, y)) continue;
                    Vector2 at = Layout.CellCenter(x, y);
                    if (Vector2.Distance(at, PlayerSpawn) < 3f) continue;
                    Layout.SetBlocked(x, y, true);
                    AddProp(PropKind.Bamboo, at, new Vector2Int(x, y), 1f);
                }
            }
        }

        void PlaceShoji()
        {
            int screens = envRng.Range(2, 4);
            for (int k = 0; k < screens; k++)
            {
                Vector2Int c = Layout.CellAt(RandomOpenPoint(2.5f));
                bool vertical = envRng.NextFloat() < 0.6f;
                int len = envRng.Range(3, 5);
                for (int i = 0; i < len; i++)
                {
                    int x = c.x + (vertical ? 0 : i), y = c.y + (vertical ? i : 0);
                    if (!Layout.IsOpen(x, y)) continue;
                    Vector2 at = Layout.CellCenter(x, y);
                    if (Vector2.Distance(at, PlayerSpawn) < 3f) continue;
                    Layout.SetBlocked(x, y, true);
                    Prop p = AddProp(PropKind.Shoji, at, new Vector2Int(x, y), 1f);
                    p.Angle = vertical ? 90f : 0f;
                }
            }
        }

        Prop AddProp(PropKind kind, Vector2 at, Vector2Int cell, float health)
        {
            var p = new Prop { Kind = kind, Position = at, Cell = cell, Health = health };
            Props.Add(p);
            if (conditionRoot != null) BuildPropView(p);
            return p;
        }

        void BuildConditionViews()
        {
            foreach (Zone z in Zones)
            {
                if (z.View == null) BuildZoneView(z);
            }
            foreach (Prop p in Props)
            {
                if (p.View == null) BuildPropView(p);
            }
        }

        void BuildPropView(Prop p)
        {
            Sprite s;
            Color c = Color.white;
            int order = -Mathf.RoundToInt(p.Position.y * 20f);
            Vector3 offset = Vector3.zero;
            switch (p.Kind)
            {
                case PropKind.Bamboo:
                    s = PropArt.Bamboo();
                    offset = new Vector3(0f, -0.5f, 0f);
                    break;
                case PropKind.Shoji:
                    s = PropArt.Shoji();
                    offset = new Vector3(0f, -0.5f, 0f);
                    break;
                case PropKind.Core:
                    s = PixelArt.Region("tiles_dungeon", 2, 2, 1, 1, new Vector2(0.5f, 0f));
                    c = ColorUtil.Hex("#D9A6FF");
                    offset = new Vector3(0f, -0.4f, 0f);
                    break;
                case PropKind.FallenBamboo:
                    s = PropArt.FallenBamboo();
                    order = Sorting.Ground + 3;
                    break;
                default:
                    s = PropArt.Pit();
                    order = Sorting.Ground;
                    break;
            }
            if (s == null) return;
            SpriteRenderer r = SpriteUtil.Make(p.Kind.ToString(), conditionRoot, s, c, order);
            r.transform.position = (Vector3)p.Position + offset;
            if (p.Kind == PropKind.FallenBamboo) r.transform.rotation = Quaternion.Euler(0f, 0f, p.Angle);
            p.View = r.gameObject;
        }

        void RemoveProp(Prop p)
        {
            p.Gone = true;
            if (p.View != null) Object.Destroy(p.View);
            p.View = null;
            if (p.Kind == PropKind.Bamboo || p.Kind == PropKind.Shoji) Layout.SetBlocked(p.Cell.x, p.Cell.y, false);
        }

        /// <summary>A duelist's blow meets the arena: bamboo falls, paper tears, stone cracks, lanterns spill.</summary>
        void StrikeArena(Vector2 origin, Vector2 direction, HitShape shape, float damage)
        {
            if (Layout == null) return;
            for (int i = 0; i < Props.Count; i++)
            {
                Prop p = Props[i];
                if (p.Gone || p.Kind == PropKind.Pit || p.Kind == PropKind.FallenBamboo) continue;
                if (!CombatMath.Overlaps(shape, origin, direction, p.Position, 0.45f)) continue;
                switch (p.Kind)
                {
                    case PropKind.Bamboo:
                        FellBamboo(p, direction);
                        break;
                    case PropKind.Shoji:
                        RemoveProp(p);
                        Fx.Emit(FxType.EnemyBlocked, p.Position, direction, 1f);
                        Fx.Popup(Strings.PopupShojiTorn, p.Position + Vector2.up, Config.palette.uiText);
                        break;
                    case PropKind.Core:
                        p.Health -= damage;
                        Fx.Emit(FxType.HitEnemy, p.Position, direction, damage, ZoneColor(ZoneKind.Mist));
                        if (p.Health <= 0f) DestroyCore(p);
                        break;
                }
            }

            // Statues crack after three heavy blows.
            if (Decor != null && damage >= 12f)
            {
                for (int i = 0; i < Decor.Statues.Count; i++)
                {
                    if (BrokenStatues.Contains(i)) continue;
                    Vector4 s = Decor.Statues[i];
                    Vector2 center = Layout.Origin + new Vector2(s.x + s.z * 0.5f, s.y + s.z * 0.5f);
                    if (!CombatMath.Overlaps(shape, origin, direction, center, s.z * 0.6f)) continue;
                    statueHits.TryGetValue(i, out int hits);
                    statueHits[i] = ++hits;
                    Fx.Emit(FxType.EnemyBlocked, center, direction, 1f);
                    if (hits >= 3) BreakStatue(i);
                }
            }

            // Lanterns knocked over spill burning oil.
            if (Decor != null)
            {
                for (int i = 0; i < Decor.Lanterns.Count; i++)
                {
                    if (BrokenLanterns.Contains(i)) continue;
                    Vector2 at = Decor.Lanterns[i] + Vector2.up * 0.4f;
                    if (!CombatMath.Overlaps(shape, origin, direction, at, 0.4f)) continue;
                    BrokenLanterns.Add(i);
                    if (i < Decor.LanternViews.Count && Decor.LanternViews[i] != null) Object.Destroy(Decor.LanternViews[i]);
                    AddZone(ZoneKind.Fire, Decor.Lanterns[i] + direction * 0.8f, 1.6f, 300);
                    Fx.Emit(FxType.Explosion, Decor.Lanterns[i], Vector2.up, 1.2f, ZoneColor(ZoneKind.Fire));
                    Fx.Popup(Strings.PopupOilSpill, Decor.Lanterns[i] + Vector2.up * 1.5f, ZoneColor(ZoneKind.Fire));
                }
            }
        }

        void FellBamboo(Prop p, Vector2 direction)
        {
            RemoveProp(p);
            Vector2 dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.right;
            HitShape line = HitShape.Line(3.6f, 0.8f);
            foreach (Enemy e in Enemies)
            {
                if (!e.CanBeHit || e.IsAlly) continue;
                if (!CombatMath.Overlaps(line, p.Position, dir, e.Position, e.Radius)) continue;
                e.ReceiveHazard(22f, 25f, dir);
                e.Stun(30);
            }
            foreach (PlayerController q in Players)
            {
                if (q == actor || !q.IsAlive) continue;
                if (CombatMath.Overlaps(line, p.Position, dir, q.Position, q.Radius)) q.ReceiveHazard(12f, 10f, dir);
            }
            var fallen = AddProp(PropKind.FallenBamboo, p.Position + dir * 1.8f, p.Cell, 0f);
            fallen.Angle = CombatMath.AngleDegrees(dir);
            if (fallen.View != null) fallen.View.transform.rotation = Quaternion.Euler(0f, 0f, fallen.Angle);
            Fx.Emit(FxType.SlamImpact, p.Position + dir * 2f, dir, 2f);
            Fx.Popup(Strings.PopupTimber, p.Position + Vector2.up * 1.2f, ColorUtil.Hex("#B8D46A"));
            HintOnce(HintId.Arena);
        }

        void BreakStatue(int i)
        {
            BrokenStatues.Add(i);
            Vector4 s = Decor.Statues[i];
            if (i < Decor.StatueViews.Count)
            {
                foreach (GameObject go in Decor.StatueViews[i])
                {
                    if (go != null) Object.Destroy(go);
                }
            }
            var block = new RectInt((int)s.x, (int)s.y, (int)s.z, (int)s.z);
            for (int y = block.yMin; y < block.yMax; y++)
            {
                for (int x = block.xMin; x < block.xMax; x++) Layout.SetBlocked(x, y, false);
            }
            Vector2 center = Layout.Origin + new Vector2(s.x + s.z * 0.5f, s.y + s.z * 0.5f);
            Fx.Emit(FxType.Explosion, center, Vector2.up, 1.6f, ColorUtil.Hex("#C8C2B8"));
            Fx.Popup(Strings.PopupStatueBroke, center + Vector2.up * 1.5f, Config.palette.uiText);
            HintOnce(HintId.Arena);
        }

        void DestroyCore(Prop p)
        {
            RemoveProp(p);
            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                if (Zones[i].Kind != ZoneKind.Mist) continue;
                if (Zones[i].View != null) Object.Destroy(Zones[i].View.gameObject);
                Zones.RemoveAt(i);
            }
            Fx.Emit(FxType.Explosion, p.Position, Vector2.up, 2f, ZoneColor(ZoneKind.Mist));
            Fx.Toast(Strings.ToastCorruptionCleansed, Config.palette.blessing);
            AddFlow(10f);
        }

        // ── Snow, dawn and the frozen ────────────────────────────────────────────────

        /// <summary>Spawn-time touches: dawn weakens the dead, the cold freezes some, the bamboo hides ninjas above.</summary>
        void ConditionSpawn(Enemy e, bool elite)
        {
            if (TimeOfDay == TimeOfDay.Dawn && RoomGenerator.IsUndead(e.Def.id)) e.Weaken(0.75f);
            if (BiomeRule == BiomeRule.Cold && !elite && envRng != null && envRng.NextFloat() < 0.35f) e.Freeze(360);
            if (BiomeRule == BiomeRule.Bamboo && (e.Def.id == "kunoichi" || e.Def.id == "sombra") && envRng != null)
            {
                // Drops from the canopy right beside a duelist.
                PlayerController p = NearestAlivePlayer(e.Position);
                if (p != null)
                {
                    Vector2 at = Layout.NearestFloor(p.Position + CombatMath.FromAngle(envRng.Range(0f, 360f)) * 2.6f, e.Radius);
                    e.Teleport(at);
                    Fx.Emit(FxType.Smoke, at, Vector2.up, 0.8f, ColorUtil.Hex("#B8D46A"));
                }
            }
        }
    }

    /// <summary>Small procedural sprites for the new props (no tiles for bamboo or shōji in the pack).</summary>
    public static class PropArt
    {
        static Sprite bamboo, fallen, shoji, pit;

        public static Sprite Bamboo()
        {
            if (bamboo != null) return bamboo;
            string[] rows =
            {
                "....L...........",
                "...LLL....L.....",
                "....g....LLL....",
                "...hgh....g.....",
                "...hgh...hgh....",
                "...dDd...hgh....",
                "...hgh...hgh..L.",
                "...hgh...dDd.LLL",
                "...hgh...hgh..g.",
                "...hgh...hgh.hgh",
                "...dDd...hgh.hgh",
                "...hgh...hgh.dDd",
                "...hgh...dDd.hgh",
                "...hgh...hgh.hgh",
                "...hgh...hgh.hgh",
                "...dDd...hgh.hgh",
                "...hgh...hgh.dDd",
                "...hgh...dDd.hgh",
                "...hgh...hgh.hgh",
                "...hgh...hgh.hgh",
                "..ssssssssssssss",
            };
            var pal = new Dictionary<char, Color>
            {
                { 'g', ColorUtil.Hex("#7DB24A") }, { 'h', ColorUtil.Hex("#5C8A34") }, { 'd', ColorUtil.Hex("#3E6224") },
                { 'D', ColorUtil.Hex("#A7D46A") }, { 'L', ColorUtil.Hex("#9CCB5A") }, { 's', new Color(0f, 0f, 0f, 0.35f) },
            };
            bamboo = PixelArt.FromGrid("prop_bamboo", rows, pal, new Vector2(0.5f, 0f));
            return bamboo;
        }

        public static Sprite FallenBamboo()
        {
            if (fallen != null) return fallen;
            string[] rows =
            {
                "hgggdDgggghggdDggggh....",
                "hgggdDgggghggdDggggh.LL.",
                "ssssssssssssssssssss....",
            };
            var pal = new Dictionary<char, Color>
            {
                { 'g', ColorUtil.Hex("#6E9C40") }, { 'h', ColorUtil.Hex("#4E7A2C") }, { 'd', ColorUtil.Hex("#3E6224") },
                { 'D', ColorUtil.Hex("#94C25A") }, { 'L', ColorUtil.Hex("#9CCB5A") }, { 's', new Color(0f, 0f, 0f, 0.3f) },
            };
            fallen = PixelArt.FromGrid("prop_bamboo_fallen", rows, pal, new Vector2(0.5f, 0.5f));
            return fallen;
        }

        public static Sprite Shoji()
        {
            if (shoji != null) return shoji;
            string[] rows =
            {
                "wwwwwwwwwwwwwwww",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wwwwwwwwwwwwwwww",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wwwwwwwwwwwwwwww",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wwwwwwwwwwwwwwww",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wwwwwwwwwwwwwwww",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wpppwpppwpppwppw",
                "wwwwwwwwwwwwwwww",
                "ssssssssssssssss",
            };
            var pal = new Dictionary<char, Color>
            {
                { 'w', ColorUtil.Hex("#6B4A2B") }, { 'p', ColorUtil.Hex("#F1E8D6", 0.92f) }, { 's', new Color(0f, 0f, 0f, 0.35f) },
            };
            shoji = PixelArt.FromGrid("prop_shoji", rows, pal, new Vector2(0.5f, 0f));
            return shoji;
        }

        public static Sprite Pit()
        {
            if (pit != null) return pit;
            var rows = new string[16];
            for (int y = 0; y < 16; y++)
            {
                var sb = new System.Text.StringBuilder();
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 15 || y == 15;
                    bool lip = y == 15 || y == 14;
                    sb.Append(lip ? 'e' : edge ? 'r' : 'v');
                }
                rows[y] = sb.ToString();
            }
            var pal = new Dictionary<char, Color>
            {
                { 'v', new Color(0.02f, 0.02f, 0.04f, 1f) }, { 'r', new Color(0.1f, 0.09f, 0.14f, 1f) }, { 'e', new Color(0.2f, 0.18f, 0.26f, 1f) },
            };
            pit = PixelArt.FromGrid("prop_pit", rows, pal, new Vector2(0.5f, 0.5f));
            return pit;
        }
    }
}
