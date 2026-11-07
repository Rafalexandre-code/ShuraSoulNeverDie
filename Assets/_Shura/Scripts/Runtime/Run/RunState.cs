using System.Collections.Generic;

namespace Shura
{
    /// <summary>Everything that belongs to one run: seed, rewards, Flow and statistics.</summary>
    public sealed class RunState
    {
        public readonly ulong Seed;
        public readonly SeededRandom Rng;
        public readonly FlowMeter Flow;
        public readonly List<RewardDef> Owned = new List<RewardDef>();
        public readonly List<string> PendingTechniques = new List<string>();
        /// <summary>Permanent upgrades the run started with (may be null in tests).</summary>
        public readonly MetaProgress Meta;

        /// <summary>What the enemies learn about the player during this run.</summary>
        public readonly PlayerProfile Profile = new PlayerProfile();
        readonly List<RoomDef> rooms = new List<RoomDef>();
        /// <summary>Automatic growth: one level per room cleared.</summary>
        public int Ascension;
        public int ElitesKilled;

        public PlayerModifiers Modifiers { get; private set; } = new PlayerModifiers();
        /// <summary>Perfect actions in a row without taking damage.</summary>
        public int PerfectStreak;
        public int BestStreak;
        public bool Banked;
        public int EcosBanked;
        public int RoomIndex;
        public int RoomsCleared;
        public int PerfectParries;
        public int PerfectDodges;
        public int Counters;
        public int Finishers;
        public int TechniquesAbsorbed;
        public float Elapsed;

        public RunState(ulong seed, FlowTuning flow, MetaProgress meta = null)
        {
            Seed = seed;
            Rng = new SeededRandom(seed);
            Flow = new FlowMeter(flow.levelThresholds, flow.maxPoints);
            Meta = meta;
            RecomputeModifiers();
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

        /// <summary>Returns the new streak length.</summary>
        public int RegisterPerfect()
        {
            PerfectStreak++;
            if (PerfectStreak > BestStreak) BestStreak = PerfectStreak;
            return PerfectStreak;
        }

        public void BreakStreak()
        {
            PerfectStreak = 0;
        }

        public bool Owns(string id)
        {
            for (int i = 0; i < Owned.Count; i++)
            {
                if (Owned[i].Id == id) return true;
            }
            return false;
        }

        public bool CanAbsorb(string techniqueId)
        {
            return !string.IsNullOrEmpty(techniqueId) && !Owns(techniqueId) && !PendingTechniques.Contains(techniqueId);
        }

        public void AddPendingTechnique(string techniqueId)
        {
            if (!CanAbsorb(techniqueId)) return;
            PendingTechniques.Add(techniqueId);
            TechniquesAbsorbed++;
        }

        public void Acquire(RewardDef reward)
        {
            if (reward == null || (!reward.Stackable && Owns(reward.Id))) return;
            Owned.Add(reward);
            PendingTechniques.Remove(reward.Id);
            RecomputeModifiers();
        }

        public void RecomputeModifiers()
        {
            var m = new PlayerModifiers();
            Meta?.ApplyTo(m);
            m.maxHealthBonus += DifficultyRules.AscensionHealth * Ascension;
            m.maxPostureBonus += DifficultyRules.AscensionPosture * Ascension;
            m.damageMult *= 1f + DifficultyRules.AscensionDamage * Ascension;
            for (int i = 0; i < Owned.Count; i++) Owned[i].Apply(m);
            Modifiers = m;
        }

        /// <summary>
        /// Absorbed techniques are guaranteed slots (up to two) — that is the payoff for
        /// mastering an enemy. From depth 2 one slot is a Blessing; remaining slots are seeded
        /// relics you don't own yet, and Blessings fill in once the relics run out.
        /// </summary>
        public List<RewardDef> RollChoices(int count, int depth = 0)
        {
            var choices = new List<RewardDef>();
            for (int i = 0; i < PendingTechniques.Count && choices.Count < 2; i++)
            {
                RewardDef t = RewardRegistry.Create(PendingTechniques[i]);
                if (t != null) choices.Add(t);
            }

            var pool = new List<string>();
            foreach (string id in RewardIds.Relics)
            {
                if (!Owns(id)) pool.Add(id);
            }

            int relicSlots = depth >= 2 ? count - 1 : count;
            while (choices.Count < relicSlots && pool.Count > 0)
            {
                int index = Rng.Range(0, pool.Count);
                RewardDef r = RewardRegistry.Create(pool[index]);
                pool.RemoveAt(index);
                if (r != null) choices.Add(r);
            }

            var blessings = new List<string>(RewardIds.Blessings);
            while (choices.Count < count && blessings.Count > 0)
            {
                int index = Rng.Range(0, blessings.Count);
                RewardDef b = RewardRegistry.Create(blessings[index]);
                blessings.RemoveAt(index);
                if (b != null) choices.Add(b);
            }
            return choices;
        }
    }
}
