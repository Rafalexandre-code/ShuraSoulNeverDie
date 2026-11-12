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
        /// <summary>Altar "Segunda Troca": the sheathed third weapon (null = only two).</summary>
        public WeaponDef Reserve;
        /// <summary>Fragmentos de Alma found this run (a technique absorbed again).</summary>
        public int SoulFragments;

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

        // ── Inner resources (design doc §3) ─────────────────────────────────────────
        /// <summary>The Shura meter, 0..100: only rises; meditation and Gedan finishers calm it.</summary>
        public float Shura;
        public float ShuraGainMult = 1f;
        /// <summary>Perfect actions in a row (at full Flow) that open Mushin.</summary>
        public int MushinStreak = 10;
        public bool MushinSurvivesHit;
        /// <summary>Spirit papers pay for tools.</summary>
        public int SpiritPapers = 6;
        public ToolKind Tool = ToolKind.Shuriken;
        public int Bandages = 1;
        public int FinishersForPaper;
        /// <summary>Aço Celeste: the smith's currency, from broken items.</summary>
        public int CelestialSteel;
        readonly ClanSetEffect[] clanSets = { null, new ClanSetEffect(Clan.Takeda), new ClanSetEffect(Clan.Hattori), new ClanSetEffect(Clan.Minamoto), new ClanSetEffect(Clan.Shimazu) };
        public int ExtraSips;
        public int ExtraSealsPerParry;
        public float SealDamageMult = 1f;
        public float HealMultExtra = 1f;
        /// <summary>Lineage: perfect-window frames from the disciple's age.</summary>
        public int AgeParryBonus;
        /// <summary>Altar "Corpo de Diamante": the first wound of each room is ignored.</summary>
        public bool IgnoreFirstWound;
        public bool FirstWoundIgnored;

        // ── The road (design doc §6) ────────────────────────────────────────────────
        /// <summary>Mon: the run's coin, spent at merchants, smiths, beggars and dice.</summary>
        public int Mon;
        /// <summary>A curse from a cursed treasure or a poisoned cup, and the rooms it still lasts.</summary>
        public RunCurse Curse;
        public int CurseRooms;
        /// <summary>The Muramasa blade: much more damage, and the Shura rises on its own.</summary>
        public bool Muramasa;
        /// <summary>The wandering smith's whetstone (+damage for the run).</summary>
        public float Sharpened;
        /// <summary>A duel of honour: no techniques, no items while it lasts.</summary>
        public bool HonorBound;
        /// <summary>The guard taken at the start of every room (changed at shrines).</summary>
        public Kamae DefaultStance = Kamae.Chudan;

        public bool Cursed(RunCurse c) => CurseRooms > 0 && Curse == c;

        /// <summary>Health to stand up with after a refused fall (Moeda de Seis Mon); 0 = just 1.</summary>
        public float PendingReviveHealth;

        /// <summary>Some relics only make sense in some builds (a blank scroll needs a technique to copy).</summary>
        bool CanOffer(string id) => !Owns(id) && (id != RoadRelics.BlankScroll || PendingTechniques.Count > 0);

        public void ApplyCurse(RunCurse c, int rooms)
        {
            Curse = c;
            CurseRooms = rooms;
            RecomputeModifiers();
        }

        /// <summary>A room was cleared: curses wear off one room at a time.</summary>
        public void TickCurse()
        {
            if (CurseRooms <= 0) return;
            if (--CurseRooms == 0) Curse = RunCurse.None;
            RecomputeModifiers();
        }

        /// <summary>A reward card taken: items go to the slots or the bag, the rest is owned.</summary>
        public void Grant(RewardDef reward)
        {
            if (reward is Item item)
            {
                AddItem(item);
                return;
            }
            Acquire(reward);
        }

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

        /// <summary>A technique not yet owned, or owned below level III (absorbing it again raises it).</summary>
        public bool CanAbsorb(string techniqueId)
        {
            if (string.IsNullOrEmpty(techniqueId) || PendingTechniques.Contains(techniqueId)) return false;
            TechniqueDef owned = OwnedTechnique(techniqueId);
            return owned != null ? owned.Level < TechniqueDef.MaxLevel : !Owns(techniqueId);
        }

        public TechniqueDef OwnedTechnique(string id)
        {
            for (int i = 0; i < Owned.Count; i++)
            {
                if (Owned[i].Id == id && Owned[i] is TechniqueDef t) return t;
            }
            return null;
        }

        /// <summary>The card a pending technique becomes: new, or the next level of the one owned (III takes a branch).</summary>
        public RewardDef TechniqueCard(string id, int branch = TechniqueDef.BranchFull)
        {
            TechniqueDef owned = OwnedTechnique(id);
            if (owned == null) return RewardRegistry.Create(id);
            if (owned.Level >= TechniqueDef.MaxLevel) return null;
            return new TechniqueLevelUp(owned, owned.Level + 1, owned.Level + 1 >= TechniqueDef.MaxLevel ? branch : 0);
        }

        public void AddPendingTechnique(string techniqueId)
        {
            // Taking the same piece of a soul again leaves a fragment of it.
            if (OwnedTechnique(techniqueId) != null || PendingTechniques.Contains(techniqueId)) SoulFragments++;
            if (!CanAbsorb(techniqueId)) return;
            PendingTechniques.Add(techniqueId);
            TechniquesAbsorbed++;
        }

        public void Acquire(RewardDef reward)
        {
            if (reward is TechniqueLevelUp up)
            {
                TechniqueDef t = OwnedTechnique(up.Target.Id);
                if (t == null || t.Level >= up.ToLevel) return;
                t.Level = up.ToLevel;
                t.Branch = up.ChosenBranch;
                PendingTechniques.Remove(t.Id);
                RecomputeModifiers();
                return;
            }
            if (reward == null || (!reward.Stackable && Owns(reward.Id))) return;
            // A finished enemy scroll: its technique comes already as its variant (level II).
            if (reward is TechniqueDef tech && tech.Level < 2 && run?.Meta != null)
            {
                EnemyDef source = EnemyScrolls.SourceOf(tech.Id);
                if (source != null && EnemyScrolls.Done(run.Meta, source.id)) tech.Level = 2;
            }
            // Fused techniques act first (Muralha Viva stands you up before the bones do).
            if (reward is FusionDef) Owned.Insert(0, reward);
            else Owned.Add(reward);
            PendingTechniques.Remove(reward.Id);
            RecomputeModifiers();
            reward.OnAcquired(this);
        }

        /// <summary>
        /// This duelist's own Altar das Almas (an online guest's); null = the run's, i.e. the host's.
        /// </summary>
        public MetaProgress OwnMeta;

        public void RecomputeModifiers()
        {
            var m = new PlayerModifiers();
            (OwnMeta ?? run?.Meta)?.ApplyTo(m);
            run?.Oaths.Apply(m);
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
            // A duel of honour: only the blade and the hand that holds it.
            if (HonorBound) Effects.Clear();
            else
            {
                // Clan sets: two pieces, a number; four pieces, a way of fighting.
                for (int c = 1; c < clanSets.Length; c++)
                {
                    int pieces = ItemIdentity.CountClan(Equipped, (Clan)c);
                    if (pieces >= 2) ItemIdentity.ApplyTwoPieces((Clan)c, m);
                    if (pieces >= 4) Effects.Add(clanSets[c]);
                }
            }
            for (int i = 0; i < Effects.Count; i++) Effects[i].Apply(m);
            // The forge's tsubas: both carried blades lend their guard's passive.
            foreach (WeaponDef w in Weapons)
            {
                if (w?.tsubaId != null) WeaponForge.Tsuba(w.tsubaId)?.apply(m);
            }
            if (Muramasa) m.damageMult *= 1.35f;
            m.damageMult *= 1f + Sharpened;
            if (Cursed(RunCurse.Fragile)) m.damageTakenMult *= 1.3f;
            if (Cursed(RunCurse.Clumsy)) m.dodgeChargeBonus -= 1;
            IgnoreFirstWound = m.ignoreFirstWound;
            Modifiers = m;
        }

        /// <summary>
        /// Absorbed techniques are guaranteed slots (up to two) — that is the payoff for
        /// mastering an enemy. From depth 2 one slot is a Blessing; remaining slots are seeded
        /// relics this player doesn't own yet, and Blessings fill in once the relics run out.
        /// </summary>
        /// <summary>The door's symbol tilts the draw: all relics, all blessings, or an item among them.</summary>
        public List<RewardDef> RollChoices(int count, int depth, RewardHint hint)
        {
            List<RewardDef> choices = RollChoices(count, depth);
            switch (hint)
            {
                case RewardHint.Relic:
                {
                    // Blessings give way to relics this player does not own.
                    var pool = new List<string>();
                    foreach (string id in RewardIds.Relics)
                    {
                        bool offered = false;
                        foreach (RewardDef c in choices) offered |= c.Id == id;
                        if (CanOffer(id) && !offered) pool.Add(id);
                    }
                    for (int i = 0; i < choices.Count && pool.Count > 0; i++)
                    {
                        if (choices[i].Kind != RewardKind.Blessing) continue;
                        int k = Rng.Range(0, pool.Count);
                        RewardDef r = RewardRegistry.Create(pool[k]);
                        pool.RemoveAt(k);
                        if (r != null) choices[i] = r;
                    }
                    break;
                }
                case RewardHint.Blessing:
                {
                    var blessings = new List<string>(RewardIds.Blessings);
                    for (int i = 0; i < choices.Count && blessings.Count > 0; i++)
                    {
                        if (choices[i].Kind != RewardKind.Relic) continue;
                        int k = Rng.Range(0, blessings.Count);
                        RewardDef b = RewardRegistry.Create(blessings[k]);
                        blessings.RemoveAt(k);
                        if (b != null) choices[i] = b;
                    }
                    break;
                }
                case RewardHint.Item when run != null && run.Mode == RunMode.Tower:
                    break;
                case RewardHint.Item:
                {
                    // An item card (a notch better than a floor drop) takes the last slot.
                    Item item = ItemGenerator.Roll(Rng, depth + 3, false);
                    if (choices.Count > 0 && choices[choices.Count - 1].Kind != RewardKind.Technique) choices[choices.Count - 1] = item;
                    else choices.Add(item);
                    break;
                }
            }
            return choices;
        }

        public List<RewardDef> RollChoices(int count, int depth = 0)
        {
            var choices = new List<RewardDef>();
            for (int i = 0; i < PendingTechniques.Count && choices.Count < 2; i++)
            {
                string id = PendingTechniques[i];
                TechniqueDef owned = OwnedTechnique(id);
                if (owned != null && owned.Level + 1 >= TechniqueDef.MaxLevel)
                {
                    // Level III: the two branches side by side (they need both technique slots).
                    if (choices.Count > 0) continue;
                    choices.Add(new TechniqueLevelUp(owned, TechniqueDef.MaxLevel, TechniqueDef.BranchFull));
                    choices.Add(new TechniqueLevelUp(owned, TechniqueDef.MaxLevel, TechniqueDef.BranchEcho));
                    continue;
                }
                RewardDef t = TechniqueCard(id);
                if (t != null) choices.Add(t);
            }

            var pool = new List<string>();
            foreach (string id in RewardIds.Relics)
            {
                if (CanOffer(id)) pool.Add(id);
            }

            int relicSlots = depth >= 2 ? count - 1 : count;
            int karma = run != null ? KarmaRules.Of(run) : 0;
            while (choices.Count < relicSlots && pool.Count > 0)
            {
                int index = KarmaRules.PickRelic(pool, Rng, karma);
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

            // A fused technique: a rare card once its two halves are owned.
            List<string> fusions = run != null && !run.Has(Feature.Fusions) ? new List<string>() : Fusions.Available(this);
            if (fusions.Count > 0 && Rng.NextFloat() < 0.4f)
            {
                RewardDef f = Fusions.Create(fusions[Rng.Range(0, fusions.Count)]);
                int slot = -1;
                for (int i = choices.Count - 1; i >= 0 && slot < 0; i--)
                {
                    if (choices[i].Kind != RewardKind.Technique) slot = i;
                }
                if (f != null)
                {
                    if (slot >= 0) choices[slot] = f;
                    else choices.Add(f);
                }
            }
            return choices;
        }
    }
}
