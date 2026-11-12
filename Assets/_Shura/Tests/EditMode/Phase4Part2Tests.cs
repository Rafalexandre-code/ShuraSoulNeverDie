using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 4, part 2: the Tea House, the Karma's weight, the scrolls, the layers, the modes.</summary>
    public class Phase4Part2Tests
    {
        // ── The Tea House ──

        [Test]
        public void Guests_AreSix_EachWithAMissionThatCanBeClaimedOnce()
        {
            Assert.AreEqual(6, TeaGuests.All.Length);
            var meta = new MetaProgress();
            GuestDef fox = TeaGuests.Get(TeaGuests.Kohaku);
            Assert.IsFalse(TeaGuests.TryClaim(meta, fox));
            meta.AddStat(TeaGuests.StatFoxes, 2);
            Assert.IsTrue(TeaGuests.Ready(meta, fox));
            Assert.AreEqual(fox.ready, TeaGuests.Line(meta, fox, null), "a ready mission comes first");
            Assert.IsTrue(TeaGuests.TryClaim(meta, fox));
            Assert.IsFalse(TeaGuests.TryClaim(meta, fox), "only once");
            Assert.IsTrue(meta.MissionDone(TeaGuests.Kohaku));
            MetaProgress copy = MetaProgress.FromJson(meta.ToJson());
            Assert.IsTrue(copy.MissionDone(TeaGuests.Kohaku));
            Assert.AreEqual(2, copy.Stat(TeaGuests.StatFoxes));
        }

        [Test]
        public void Guests_SpeakOfTheLastLife_AndOfTheKarma()
        {
            var meta = new MetaProgress();
            GuestDef zato = TeaGuests.Get(TeaGuests.Zato);
            var fell = new LegacyReport { died = true, disciple = new Disciple { name = "Ren", cause = "Oni Vermelho" } };
            StringAssert.Contains("Oni Vermelho", TeaGuests.Line(meta, zato, fell));
            var home = new LegacyReport { aged = true, disciple = new Disciple { name = "Aoi", age = 45 } };
            StringAssert.Contains("Aoi", TeaGuests.Line(meta, zato, home));
            meta.karma = 60;
            Assert.AreEqual(zato.bushido, TeaGuests.Line(meta, zato, null));
            meta.karma = -60;
            Assert.AreEqual(zato.shura, TeaGuests.Line(meta, zato, null));
        }

        [Test]
        public void TheMonk_GivesTwoAltarPoints_AndTheSmith_TheLegendaryGuards()
        {
            var meta = new MetaProgress();
            int before = AltarTree.PointsLimit(meta);
            meta.missions.Add(TeaGuests.Monk);
            Assert.AreEqual(before + TeaGuests.MonkPoints, AltarTree.PointsLimit(meta));

            TsubaDef ryujin = WeaponForge.Tsuba("ryujin");
            Assert.IsTrue(ryujin.legendary);
            Assert.IsFalse(WeaponForge.Available(ryujin, meta));
            meta.missions.Add(TeaGuests.Smith);
            Assert.IsTrue(WeaponForge.Available(ryujin, meta));
            Assert.IsTrue(WeaponForge.Available(WeaponForge.Tsuba("lua"), new MetaProgress()));
        }

        // ── Karma ──

        [Test]
        public void Karma_ColoursTheRelics()
        {
            var pool = new List<string>(RewardIds.Relics);
            int defensive = 0, aggressive = 0;
            var rng = new SeededRandom(5);
            for (int i = 0; i < 2000; i++)
            {
                string r = pool[KarmaRules.PickRelic(pool, rng, 80)];
                if (KarmaRules.IsDefensive(r)) defensive++;
                if (KarmaRules.IsAggressive(r)) aggressive++;
            }
            Assert.Greater(defensive, aggressive * 1.6f, "Bushidō leans to defensive relics");
        }

        [Test]
        public void Karma_TiltsTheRoadsEvents_AndNeutralLeavesThemAlone()
        {
            try
            {
                KarmaRules.RoadKarma = 0;
                KarmaRules.HonorDuels = false;
                for (ulong h = 0; h < 50; h++) Assert.AreEqual((int)(h % 8UL), KarmaRules.Event(h, 8));
                KarmaRules.RoadKarma = 90;
                int muramasa = 0;
                for (ulong h = 0; h < 4000; h++)
                {
                    if (KarmaRules.Event(h, 8) == 6) muramasa++;
                }
                Assert.Less(muramasa, 4000 / 8, "a Bushidō soul meets the Muramasa less often");
            }
            finally
            {
                KarmaRules.RoadKarma = 0;
            }
        }

        // ── The scrolls ──

        [Test]
        public void Scroll_CompletesWithItsFourOpenChallenges_AndPaysFragments()
        {
            var meta = new MetaProgress();
            const string id = "ronin";
            for (int i = 0; i < EnemyScrolls.SignatureGoal; i++) Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.SignatureParry));
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.Finisher, "katana"));
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.Finisher, "katana"), "the same weapon twice counts once");
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.Finisher, "naginata"));
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.Finisher, "tessen"));
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.CleanKill));
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.MasterAnswer), "four of five");
            Assert.IsTrue(EnemyScrolls.Record(meta, id, ScrollEvent.RivalDefeat), "the vengeance finishes it");
            Assert.IsTrue(EnemyScrolls.Done(meta, id));
            Assert.AreEqual(EnemyScrolls.FragmentReward, meta.fragments);
            Assert.IsFalse(EnemyScrolls.Record(meta, id, ScrollEvent.CleanKill), "paid only once");
            Assert.AreEqual(1f, EnemyScrolls.ChallengeProgress(meta.Scroll(id, false), 4));
        }

        [Test]
        public void FinishedScroll_GivesTheTechniqueAsItsVariant()
        {
            EnemyDef source = null;
            foreach (EnemyDef e in EnemyDefs.All)
            {
                if (!string.IsNullOrEmpty(e.techniqueId) && RewardRegistry.Create(e.techniqueId) is TechniqueDef)
                {
                    source = e;
                    break;
                }
            }
            Assume.That(source, Is.Not.Null);
            var meta = new MetaProgress();
            meta.Scroll(source.id, true).done = true;
            var run = new RunState(3, new FlowTuning(), meta);
            run.Acquire(RewardRegistry.Create(source.techniqueId));
            TechniqueDef t = null;
            foreach (RewardDef r in run.Owned)
            {
                if (r.Id == source.techniqueId) t = r as TechniqueDef;
            }
            Assert.IsNotNull(t);
            Assert.AreEqual(2, t.Level);
        }

        [Test]
        public void Legends_AreStableAndNameTheEnemy()
        {
            EnemyDef e = EnemyDefs.All[0];
            Assert.AreEqual(EnemyScrolls.Legend(e), EnemyScrolls.Legend(e));
            StringAssert.Contains(e.displayName, EnemyScrolls.Legend(e));
        }

        // ── The layers ──

        [Test]
        public void Layers_OpenWithTheRuns()
        {
            Assert.IsFalse(Unlocks.Open(0, Feature.Seals));
            Assert.IsTrue(Unlocks.Open(1, Feature.Gourd));
            Assert.IsFalse(Unlocks.Open(2, Feature.Kamae));
            Assert.IsTrue(Unlocks.Open(3, Feature.Temple));
            Assert.IsTrue(Unlocks.Open(6, Feature.ShuraMeter));
            Assert.IsFalse(Unlocks.Open(9, Feature.Oaths));
            Assert.IsTrue(Unlocks.Open(10, Feature.Lineage));
            CollectionAssert.AreEquivalent(new[] { Feature.Kamae, Feature.Tools, Feature.Temple }, Unlocks.Opened(2, 3));
            Assert.IsTrue(new RunState(1, new FlowTuning()).Has(Feature.Fusions), "a run with no layer set has everything");
        }

        [Test]
        public void BeforeTheLineageOpens_TheFirstBodyNeitherAgesNorDies()
        {
            var meta = new MetaProgress { disciple = Lineage.First() };
            var run = new RunState(2, new FlowTuning(), meta) { RoomsCleared = 3 };
            LegacyReport r = Legacy.Bank(meta, run, new Lineage.Facts { killer = "Oni" }, true, false);
            Assert.IsFalse(r.died);
            Assert.IsNotNull(meta.disciple);
            Assert.AreEqual(0, meta.cemetery.Count);
        }

        [Test]
        public void Bank_CountsSteelAndParriesForTheGuests()
        {
            var meta = new MetaProgress();
            var run = new RunState(2, new FlowTuning(), meta) { ElitesKilled = 2, PerfectParries = 7 };
            Legacy.Bank(meta, run, default, false);
            Assert.AreEqual(2, meta.Stat(TeaGuests.StatSteel));
            Assert.AreEqual(7, meta.Stat(TeaGuests.StatParries));
        }

        // ── The modes ──

        [Test]
        public void Daily_SameSeedForTheDay_OneTry_AndARanking()
        {
            var day = new DateTime(2026, 11, 12);
            Assert.AreEqual(GameModes.DailySeed(day), GameModes.DailySeed(day));
            Assert.AreNotEqual(GameModes.DailySeed(day), GameModes.DailySeed(day.AddDays(1)));
            var meta = new MetaProgress();
            Assert.IsFalse(GameModes.DailyTried(meta, day));
            GameModes.RecordDaily(meta, day, 9, 300);
            GameModes.RecordDaily(meta, day.AddDays(-1), 14, 500);
            Assert.IsTrue(GameModes.DailyTried(meta, day.AddDays(-1)));
            List<DailyResult> rank = GameModes.Ranking(meta);
            Assert.AreEqual(14, rank[0].depth, "the deepest day first");
        }

        [Test]
        public void Tower_IsOnlyDuels_AndPilgrimageHasAnEnding()
        {
            var run = new RunState(4, new FlowTuning()) { Mode = RunMode.Tower };
            List<RouteNode> doors = run.NextDoors();
            Assert.AreEqual(1, doors.Count);
            Assert.AreEqual(RoomKind.Duel, doors[0].kind);
            Assert.AreEqual(100, GameModes.Goal(RunMode.Tower));
            Assert.AreEqual(50, GameModes.Goal(RunMode.Pilgrimage));
            Assert.AreEqual(0, GameModes.Goal(RunMode.Journey));
            Assert.AreEqual(GameModes.EndingLiberation, GameModes.EndingFor(80));
            Assert.AreEqual(GameModes.EndingShura, GameModes.EndingFor(-70));
            Assert.AreEqual(GameModes.EndingWheel, GameModes.EndingFor(10));
            Assert.Greater(GameModes.EcosMult(RunMode.Master), 1f);
        }

        // ── The story in the objects ──

        [Test]
        public void Items_RememberWhoseTheyWere()
        {
            Item item = ItemGenerator.RollAt(new SeededRandom(8), Rarity.Rare, null);
            string a = Lore.Item(item);
            StringAssert.StartsWith("Pertenceu a ", a);
            Assert.AreEqual(a, Lore.Item(item));
        }
    }
}
