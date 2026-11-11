using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 3 (design doc §4): road relics and weapon mastery.</summary>
    public class Phase3Tests
    {
        static RunState NewRun() => new RunState(1234UL, new FlowTuning());

        [Test]
        public void RoadRelics_AllExistAndChangeTheBuild()
        {
            string[] ids =
            {
                RoadRelics.HannyaMask, RoadRelics.BlankScroll, RoadRelics.StrawSandals, RoadRelics.VowOfSilence,
                RoadRelics.ExecutionerChain, RoadRelics.BrokenHourglass, RoadRelics.SixMonCoin, RoadRelics.DragonEye,
            };
            foreach (string id in ids) Assert.IsNotNull(RewardRegistry.Create(id), id);

            RunState run = NewRun();
            PlayerBuild b = run.Main;
            b.Acquire(RewardRegistry.Create(RoadRelics.StrawSandals));
            Assert.IsTrue(b.Modifiers.infiniteDodge);
            Assert.IsTrue(b.Modifiers.noBlock, "the sandals take the guard away");

            b.Acquire(RewardRegistry.Create(RoadRelics.HannyaMask));
            Assert.AreEqual(2f, b.Modifiers.shuraGainMult, 1e-4f);

            b.Acquire(RewardRegistry.Create(RoadRelics.SixMonCoin));
            Assert.AreEqual(0.5f, b.Modifiers.ecosMult, 1e-4f);
        }

        [Test]
        public void Techniques_RiseToThreeAndPickABranch()
        {
            RunState run = NewRun();
            PlayerBuild b = run.Main;
            b.AddPendingTechnique(RewardIds.MirrorThrust);
            b.Acquire(b.RollChoices(3)[0]);
            var t = (TechniqueDef)b.OwnedTechnique(RewardIds.MirrorThrust);
            Assert.AreEqual(1, t.Level);
            float wearAtOne = b.Modifiers.blockWearMult;
            Assert.Greater(wearAtOne, 1f, "level I pays the price");

            // Absorbed again: a level-II card that removes the price.
            Assert.IsTrue(b.CanAbsorb(RewardIds.MirrorThrust));
            b.AddPendingTechnique(RewardIds.MirrorThrust);
            List<RewardDef> second = b.RollChoices(3);
            Assert.IsInstanceOf<TechniqueLevelUp>(second[0]);
            b.Acquire(second[0]);
            Assert.AreEqual(2, t.Level);
            Assert.AreEqual(1f, b.Modifiers.blockWearMult, 1e-4f);
            Assert.AreEqual(1, b.Owned.Count, "still one technique, just stronger");

            // Level III: the two branches side by side.
            b.AddPendingTechnique(RewardIds.MirrorThrust);
            List<RewardDef> third = b.RollChoices(3);
            var full = third[0] as TechniqueLevelUp;
            var echo = third[1] as TechniqueLevelUp;
            Assert.IsNotNull(full);
            Assert.IsNotNull(echo);
            Assert.AreEqual(TechniqueDef.BranchFull, full.ChosenBranch);
            Assert.AreEqual(TechniqueDef.BranchEcho, echo.ChosenBranch);
            b.Acquire(echo);
            Assert.AreEqual(3, t.Level);
            Assert.AreEqual(TechniqueDef.BranchEcho, t.Branch);
            Assert.IsFalse(b.CanAbsorb(RewardIds.MirrorThrust), "III is the top");
            StringAssert.Contains("III", t.Name);
        }

        [Test]
        public void EveryTechnique_HasMarks_AndEveryFusionHasItsHalves()
        {
            foreach (string id in RewardIds.Techniques)
            {
                var t = RewardRegistry.Create(id) as TechniqueDef;
                Assert.IsNotNull(t, id);
                Assert.That(t.MarkSet.Length, Is.InRange(1, 2), id);
            }
            Assert.AreEqual(8, Fusions.All.Length);
            foreach (string id in Fusions.All)
            {
                Fusions.Parts(id, out string a, out string b);
                Assert.IsNotNull(RewardRegistry.Create(a), id);
                Assert.IsNotNull(RewardRegistry.Create(b), id);
                RewardDef f = Fusions.Create(id);
                Assert.IsNotNull(f, id);
                Assert.AreEqual(id, f.Id);
                Assert.IsNotEmpty(f.Gain);
            }
        }

        [Test]
        public void Fusion_IsOfferedOnlyWithBothHalves()
        {
            RunState run = NewRun();
            PlayerBuild b = run.Main;
            b.Acquire(RewardRegistry.Create(RewardIds.Bulwark));
            Assert.IsEmpty(Fusions.Available(b));
            b.Acquire(RewardRegistry.Create(RewardIds.StubbornBones));
            CollectionAssert.Contains(Fusions.Available(b), Fusions.LivingWall);

            bool offered = false;
            for (int i = 0; i < 40 && !offered; i++)
            {
                foreach (RewardDef r in b.RollChoices(3)) offered |= r.Id == Fusions.LivingWall;
            }
            Assert.IsTrue(offered, "a rare card, but it shows up");

            b.Acquire(Fusions.Create(Fusions.LivingWall));
            Assert.AreEqual(Fusions.LivingWall, b.Owned[0].Id, "fusions act first");
            Assert.IsEmpty(Fusions.Available(b));
        }

        [Test]
        public void Items_FromLegendaryUp_CarryAUniqueRule()
        {
            var rng = new SeededRandom(77UL);
            for (int i = 0; i < 30; i++)
            {
                Item low = ItemGenerator.RollAt(rng, Rarity.Epic, null);
                Assert.AreEqual(UniqueAffix.None, low.unique);
                Item high = ItemGenerator.RollAt(rng, Rarity.Legendary, null);
                Assert.AreNotEqual(UniqueAffix.None, high.unique);
                StringAssert.Contains("ÚNICO", high.Gain);
            }
        }

        [Test]
        public void ClanSets_PayAtTwoAndFourPieces()
        {
            RunState run = NewRun();
            PlayerBuild b = run.Main;
            var rng = new SeededRandom(5UL);
            float baseDodge = b.Modifiers.dodgeChargeBonus;
            for (int s = 0; s < 4; s++)
            {
                Item it = ItemGenerator.RollAt(rng, Rarity.Common, (ItemSlot)s);
                it.clan = Clan.Hattori;
                b.AddItem(it);
                if (s == 0) Assert.AreEqual(baseDodge, b.Modifiers.dodgeChargeBonus, 1e-4f, "one piece is just a crest");
                if (s == 1) Assert.AreEqual(baseDodge + 1, b.Modifiers.dodgeChargeBonus, 1e-4f, "two pieces: +1 dodge");
            }
            bool fourPiece = false;
            foreach (RewardDef e in b.Effects) fourPiece |= e is ClanSetEffect c && c.clan == Clan.Hattori;
            Assert.IsTrue(fourPiece, "four pieces: the shadow copy");
        }

        [Test]
        public void CorruptedItem_IsStrongerAndLosesItsFlaw()
        {
            var rng = new SeededRandom(9UL);
            Item it = ItemGenerator.RollAt(rng, Rarity.Epic, ItemSlot.Ring);
            it.unique = UniqueAffix.None;
            it.clan = Clan.None;
            it.flaw = Corruption.None;
            float before = it.affixes[0].value;
            ItemGenerator.Corrupt(it, rng);
            Assert.Greater(it.affixes[0].value, before);
            Assert.AreNotEqual(Corruption.None, it.flaw);
            StringAssert.StartsWith(Strings.CorruptedPrefix, it.Name);

            it.flaw = Corruption.Fragile;
            var m = new PlayerModifiers();
            it.Apply(m);
            Assert.Greater(m.damageTakenMult, 1f);
            it.flaw = Corruption.None;
            m = new PlayerModifiers();
            it.Apply(m);
            Assert.AreEqual(1f, m.damageTakenMult, 1e-4f);
        }

        [Test]
        public void Forge_FindsTriosAndEngravesOneAttribute()
        {
            var rng = new SeededRandom(3UL);
            var bag = new List<Item>
            {
                ItemGenerator.RollAt(rng, Rarity.Rare, null), ItemGenerator.RollAt(rng, Rarity.Common, null),
                ItemGenerator.RollAt(rng, Rarity.Rare, null), ItemGenerator.RollAt(rng, Rarity.Rare, null),
            };
            Assert.IsTrue(ItemIdentity.FindTrio(bag, out Rarity r));
            Assert.AreEqual(Rarity.Rare, r);
            bag.RemoveAt(0);
            Assert.IsFalse(ItemIdentity.FindTrio(bag, out _));

            Item it = ItemGenerator.RollAt(rng, Rarity.Mythic, null);
            var before = new List<AffixStat>();
            foreach (Affix a in it.affixes) before.Add(a.stat);
            ItemGenerator.Engrave(it, rng);
            int changed = 0;
            for (int i = 0; i < it.affixes.Count; i++) changed += it.affixes[i].stat != before[i] ? 1 : 0;
            Assert.AreEqual(1, changed, "a kanji changes exactly one attribute");
            Assert.Greater(ItemIdentity.SteelFrom(it), ItemIdentity.SteelFrom(bag[0]));
        }

        [Test]
        public void Mastery_LevelsFollowTheThresholds()
        {
            Assert.AreEqual(0, WeaponMastery.LevelFor(0));
            Assert.AreEqual(0, WeaponMastery.LevelFor(4));
            Assert.AreEqual(1, WeaponMastery.LevelFor(5));
            Assert.AreEqual(2, WeaponMastery.LevelFor(15));
            Assert.AreEqual(4, WeaponMastery.LevelFor(79));
            Assert.AreEqual(5, WeaponMastery.LevelFor(80));
            Assert.AreEqual(5, WeaponMastery.LevelFor(5000));
            Assert.AreEqual(-1, WeaponMastery.NextAt(5));
        }

        [Test]
        public void Mastery_GrowsPerWeaponAndAnnouncesOnlyTheStep()
        {
            var meta = new MetaProgress();
            for (int i = 0; i < 4; i++) Assert.AreEqual(0, meta.AddMasteryFinisher("katana"));
            Assert.AreEqual(1, meta.AddMasteryFinisher("katana"), "the fifth finisher reaches level I");
            Assert.AreEqual(0, meta.AddMasteryFinisher("katana"));
            Assert.AreEqual(6, meta.MasteryFinishers("katana"));
            Assert.AreEqual(0, meta.MasteryFinishers("montante"), "each weapon keeps its own count");
        }

        [Test]
        public void Mastery_SurvivesSaveAndTravelsToTheOnlineRoom()
        {
            var meta = new MetaProgress();
            for (int i = 0; i < 31; i++) meta.AddMasteryFinisher("naginata");
            MetaProgress loaded = MetaProgress.FromJson(meta.ToJson());
            Assert.AreEqual(31, loaded.MasteryFinishers("naginata"));

            MetaProgress guest = MetaProgress.FromLevelsCode(meta.LevelsCode());
            Assert.AreEqual(31, guest.MasteryFinishers("naginata"));
            Assert.AreEqual(3, WeaponMastery.LevelFor(guest.MasteryFinishers("naginata")));

            // Unknown weapons and junk are dropped.
            Assert.AreEqual(0, MetaProgress.FromLevelsCode("m.lixo:40,m.katana:x").MasteryFinishers("lixo"));
        }
    }
}
