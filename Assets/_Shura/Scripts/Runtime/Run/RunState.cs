using System.Collections.Generic;

namespace Shura
{
    /// <summary>
    /// Everything the party shares in one run: seed, rooms, depth, ascension, Ecos and what
    /// the enemies learn. Each duelist's own build (weapons, items, rewards, Flow) lives in a
    /// <see cref="PlayerBuild"/>; the host's is <see cref="Main"/>, and the members below that
    /// read like player state are shortcuts to it (the HUD, solo play and tests use them).
    /// </summary>
    public sealed class RunState
    {
        public readonly ulong Seed;
        public readonly SeededRandom Rng;
        /// <summary>Permanent upgrades the run started with (may be null in tests).</summary>
        public readonly MetaProgress Meta;
        readonly FlowTuning flowTuning;

        /// <summary>What the enemies learn about the party during this run.</summary>
        public readonly PlayerProfile Profile = new PlayerProfile();
        readonly List<RoomDef> rooms = new List<RoomDef>();
        readonly List<PlayerBuild> builds = new List<PlayerBuild>();
        /// <summary>Automatic growth: one level per room cleared (for everyone).</summary>
        public int Ascension;
        public int ElitesKilled;

        public bool Banked;
        public int EcosBanked;
        /// <summary>Extra Ecos earned this run (head cuts, rivals, events).</summary>
        public int BonusEcos;
        /// <summary>Multiplier on the Ecos this run banks (oaths raise it).</summary>
        public float EcosMult = 1f;

        // ── The road (design doc §6) ────────────────────────────────────────────────
        /// <summary>Bushidō (+) or Shura (−): mercy, gifts and honour against cruelty (§8.3).</summary>
        public int Karma;
        /// <summary>1 + gates passed: each Kalpa adds a permanent rule.</summary>
        public int Kalpa = 1;
        public readonly List<KalpaRule> KalpaRules = new List<KalpaRule>();
        /// <summary>The wounded ronin walks with the party this many more rooms.</summary>
        public int AllyRooms;
        /// <summary>The door the party came through (null before the first choice).</summary>
        public RouteNode CurrentNode;

        public bool HasRule(KalpaRule r) => KalpaRules.Contains(r);

        /// <summary>The doors open from where the party stands.</summary>
        public List<RouteNode> NextDoors()
        {
            if (CurrentNode == null || CurrentNode.depth != RoomIndex) return RouteMap.Layer(Seed, RoomIndex + 1);
            return RouteMap.Next(Seed, CurrentNode);
        }

        /// <summary>Walks through a door: the room behind it replaces the default one.</summary>
        public RoomDef Choose(RouteNode node)
        {
            GetRoom(node.depth);
            rooms[node.depth] = RoomGenerator.Generate(Seed, node.depth, node);
            CurrentNode = node;
            return rooms[node.depth];
        }
        public int RoomIndex;
        public int RoomsCleared;
        public float Elapsed;
        /// <summary>The day passing over the journey (starts at dawn).</summary>
        public readonly DayClock Clock = new DayClock();

        /// <summary>The host's build.</summary>
        public PlayerBuild Main { get; }
        public IReadOnlyList<PlayerBuild> Builds => builds;

        public RunState(ulong seed, FlowTuning flow, MetaProgress meta = null)
        {
            Seed = seed;
            Rng = new SeededRandom(seed);
            Meta = meta;
            flowTuning = flow;
            Main = new PlayerBuild(this, Rng, flow);
            builds.Add(Main);
        }

        /// <summary>A new duelist's build (a teammate joining the run): own rewards, own luck.</summary>
        public PlayerBuild CreateBuild()
        {
            ulong salt = 0x9E3779B97F4A7C15UL * (ulong)(builds.Count + 1);
            var build = new PlayerBuild(this, new SeededRandom(Seed ^ salt), flowTuning);
            builds.Add(build);
            return build;
        }

        /// <summary>Rooms are generated on demand from the seed: the run never ends.</summary>
        public RoomDef GetRoom(int depth)
        {
            while (rooms.Count <= depth) rooms.Add(RoomGenerator.Generate(Seed, rooms.Count));
            return rooms[depth];
        }

        public void Ascend()
        {
            Ascension++;
            RecomputeModifiers();
        }

        /// <summary>Every build: ascension and meta upgrades apply to all.</summary>
        public void RecomputeModifiers()
        {
            foreach (PlayerBuild b in builds) b.RecomputeModifiers();
        }

        // ── Shortcuts to the host's build ───────────────────────────────────────────────

        public const int BagCapacity = PlayerBuild.BagCapacity;
        public FlowMeter Flow => Main.Flow;
        public List<RewardDef> Owned => Main.Owned;
        public List<string> PendingTechniques => Main.PendingTechniques;
        public WeaponDef[] Weapons => Main.Weapons;
        public int ActiveWeapon { get => Main.ActiveWeapon; set => Main.ActiveWeapon = value; }
        public WeaponDef CurrentWeapon => Main.CurrentWeapon;
        public WeaponDef OtherWeapon => Main.OtherWeapon;
        public Item[] Equipped => Main.Equipped;
        public List<Item> Bag => Main.Bag;
        public List<RewardDef> Effects => Main.Effects;
        public PlayerModifiers Modifiers => Main.Modifiers;
        public int PerfectStreak { get => Main.PerfectStreak; set => Main.PerfectStreak = value; }
        public int BestStreak { get => Main.BestStreak; set => Main.BestStreak = value; }
        public int PerfectParries { get => Main.PerfectParries; set => Main.PerfectParries = value; }
        public int PerfectDodges { get => Main.PerfectDodges; set => Main.PerfectDodges = value; }
        public int Counters { get => Main.Counters; set => Main.Counters = value; }
        public int Finishers { get => Main.Finishers; set => Main.Finishers = value; }
        public int SwapParries { get => Main.SwapParries; set => Main.SwapParries = value; }
        public int TechniquesAbsorbed { get => Main.TechniquesAbsorbed; set => Main.TechniquesAbsorbed = value; }

        public bool AddItem(Item item) => Main.AddItem(item);
        public void Equip(Item item) => Main.Equip(item);
        public void Unequip(ItemSlot slot) => Main.Unequip(slot);
        public int RegisterPerfect() => Main.RegisterPerfect();
        public void BreakStreak() => Main.BreakStreak();
        public bool Owns(string id) => Main.Owns(id);
        public bool CanAbsorb(string techniqueId) => Main.CanAbsorb(techniqueId);
        public void AddPendingTechnique(string techniqueId) => Main.AddPendingTechnique(techniqueId);
        public void Acquire(RewardDef reward) => Main.Acquire(reward);
        public List<RewardDef> RollChoices(int count, int depth = 0) => Main.RollChoices(count, depth);
    }
}
