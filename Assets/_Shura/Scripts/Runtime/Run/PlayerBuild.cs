using System.Collections.Generic;

namespace Shura
{
    /// <summary>
    /// Everything that makes one duelist different from the others: their weapons, worn items
    /// and bag, the techniques, relics and blessings they picked, their Flow and streak, and
    /// their own numbers. In co-op every player has one; the run (seed, rooms, depth, Ecos)
    /// is shared.
    /// </summary>
    public sealed class PlayerBuild
    {
        public const int BagCapacity = 12;

        readonly RunState run;

        /// <summary>This player's draw of reward choices (the host's is the run's own, for reproducible runs).</summary>
        public readonly SeededRandom Rng;
        public readonly FlowMeter Flow;
        public readonly List<RewardDef> Owned = new List<RewardDef>();
        public readonly List<string> PendingTechniques = new List<string>();

        /// <summary>The two weapons carried this run; ActiveWeapon indexes the one in hand.</summary>
        public readonly WeaponDef[] Weapons = new WeaponDef[2];
        public int ActiveWeapon;
        public WeaponDef CurrentWeapon => Weapons[ActiveWeapon] ?? WeaponDefs.Katana;
        public WeaponDef OtherWeapon => Weapons[1 - ActiveWeapon] ?? WeaponDefs.Katana;

        /// <summary>Worn items, one per slot (null = empty).</summary>
        public readonly Item[] Equipped = new Item[4];
        public readonly List<Item> Bag = new List<Item>();
        /// <summary>Everything with combat hooks: rewards plus worn items.</summary>
        public readonly List<RewardDef> Effects = new List<RewardDef>();
        public PlayerModifiers Modifiers { get; private set; } = new PlayerModifiers();

        /// <summary>Perfect actions in a row without taking damage.</summary>
        public int PerfectStreak;
        public int BestStreak;
        public int PerfectParries;
        public int PerfectDodges;
        public int Counters;
        public int Finishers;
        public int SwapParries;
        public int TechniquesAbsorbed;

        public PlayerBuild(RunState owner, SeededRandom rng, FlowTuning flow)
        {
            run = owner;
            Rng = rng;
            Flow = new FlowMeter(flow.levelThresholds, flow.maxPoints);
            RecomputeModifiers();
        }

        /// <summary>Picks up a dropped item: worn at once if its slot is free, otherwise kept in the bag.</summary>
        /// <returns>true if it was equipped directly.</returns>
        public bool AddItem(Item item)
        {
            int slot = (int)item.slot;
            if (Equipped[slot] == null)
            {
                Equipped[slot] = item;
                RecomputeModifiers();
                return true;
            }
            Bag.Add(item);
            while (Bag.Count > BagCapacity) Bag.RemoveAt(0);
            return false;
        }

        /// <summary>Wears a bag item; whatever was in its slot goes back into the bag.</summary>
        public void Equip(Item item)
        {
            if (!Bag.Remove(item)) return;
            int slot = (int)item.slot;
            if (Equipped[slot] != null) Bag.Add(Equipped[slot]);
            Equipped[slot] = item;
            RecomputeModifiers();
        }

        public void Unequip(ItemSlot slot)
        {
            Item item = Equipped[(int)slot];
            if (item == null || Bag.Count >= BagCapacity) return;
            Equipped[(int)slot] = null;
            Bag.Add(item);
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
            run?.Meta?.ApplyTo(m);
            Effects.Clear();
            Effects.AddRange(Owned);
            foreach (Item item in Equipped)
            {
                if (item != null) Effects.Add(item);
            }
            int ascension = run != null ? run.Ascension : 0;
            m.maxHealthBonus += DifficultyRules.AscensionHealth * ascension;
            m.maxPostureBonus += DifficultyRules.AscensionPosture * ascension;
            m.damageMult *= 1f + DifficultyRules.AscensionDamage * ascension;
            for (int i = 0; i < Effects.Count; i++) Effects[i].Apply(m);
            Modifiers = m;
        }

        /// <summary>
        /// Absorbed techniques are guaranteed slots (up to two) — that is the payoff for
        /// mastering an enemy. From depth 2 one slot is a Blessing; remaining slots are seeded
        /// relics this player doesn't own yet, and Blessings fill in once the relics run out.
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
