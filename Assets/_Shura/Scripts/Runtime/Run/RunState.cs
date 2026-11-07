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

        public PlayerModifiers Modifiers { get; private set; } = new PlayerModifiers();
        public int RoomIndex;
        public int RoomsCleared;
        public int PerfectParries;
        public int PerfectDodges;
        public int Counters;
        public int Finishers;
        public int TechniquesAbsorbed;
        public float Elapsed;

        public RunState(ulong seed, FlowTuning flow)
        {
            Seed = seed;
            Rng = new SeededRandom(seed);
            Flow = new FlowMeter(flow.levelThresholds, flow.maxPoints);
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
            if (reward == null || Owns(reward.Id)) return;
            Owned.Add(reward);
            PendingTechniques.Remove(reward.Id);
            RecomputeModifiers();
        }

        public void RecomputeModifiers()
        {
            var m = new PlayerModifiers();
            for (int i = 0; i < Owned.Count; i++) Owned[i].Apply(m);
            Modifiers = m;
        }

        /// <summary>
        /// Absorbed techniques are guaranteed slots (up to two) — that is the payoff for
        /// mastering an enemy. Remaining slots are seeded random relics you don't own yet.
        /// </summary>
        public List<RewardDef> RollChoices(int count)
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

            while (choices.Count < count && pool.Count > 0)
            {
                int index = Rng.Range(0, pool.Count);
                RewardDef r = RewardRegistry.Create(pool[index]);
                pool.RemoveAt(index);
                if (r != null) choices.Add(r);
            }
            return choices;
        }
    }
}
