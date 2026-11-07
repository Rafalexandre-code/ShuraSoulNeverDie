using NUnit.Framework;

namespace Shura.Tests
{
    public class MetaProgressTests
    {
        static MetaUpgradeDef Vigor => MetaUpgrades.Get("vigor");

        [Test]
        public void Buying_SpendsEcos_AndRespectsTheCap()
        {
            var meta = new MetaProgress { ecos = 10000 };
            int bought = 0;
            while (meta.TryBuy(Vigor)) bought++;
            Assert.AreEqual(Vigor.maxLevel, bought);
            Assert.AreEqual(Vigor.maxLevel, meta.GetLevel("vigor"));
            Assert.IsTrue(meta.IsMaxed(Vigor));
            Assert.Less(meta.ecos, 10000);
        }

        [Test]
        public void Buying_WithoutEnoughEcos_Fails()
        {
            var meta = new MetaProgress { ecos = Vigor.CostFor(0) - 1 };
            Assert.IsFalse(meta.TryBuy(Vigor));
            Assert.AreEqual(0, meta.GetLevel("vigor"));
            Assert.AreEqual(Vigor.CostFor(0) - 1, meta.ecos);
        }

        [Test]
        public void Costs_GrowWithLevel()
        {
            foreach (MetaUpgradeDef def in MetaUpgrades.All)
            {
                for (int l = 1; l < def.maxLevel; l++) Assert.Greater(def.CostFor(l), def.CostFor(l - 1), def.id);
            }
        }

        [Test]
        public void Upgrades_ApplyToRunModifiers()
        {
            var meta = new MetaProgress { ecos = 10000 };
            meta.TryBuy(Vigor);
            meta.TryBuy(MetaUpgrades.Get("olhar"));
            meta.TryBuy(MetaUpgrades.Get("visao"));
            var run = new RunState(1, new FlowTuning(), meta);
            Assert.AreEqual(10f, run.Modifiers.maxHealthBonus, 1e-3f);
            Assert.AreEqual(1, run.Modifiers.perfectParryFrameBonus);
            Assert.AreEqual(1, run.Modifiers.extraRewardChoices);
            Assert.AreEqual(4, run.RollChoices(3 + run.Modifiers.extraRewardChoices).Count);
        }

        [Test]
        public void Rewards_StackOnTopOfUpgrades()
        {
            var meta = new MetaProgress { ecos = 10000 };
            meta.TryBuy(Vigor);
            var run = new RunState(1, new FlowTuning(), meta);
            run.Acquire(RewardRegistry.Create(RewardIds.HungryBlade));
            Assert.AreEqual(10f - 15f, run.Modifiers.maxHealthBonus, 1e-3f);
        }

        [Test]
        public void Json_RoundTrips()
        {
            var meta = new MetaProgress { ecos = 500 };
            meta.TryBuy(Vigor);
            meta.RecordRun(42, 3, false);
            MetaProgress copy = MetaProgress.FromJson(meta.ToJson());
            Assert.AreEqual(meta.ecos, copy.ecos);
            Assert.AreEqual(1, copy.GetLevel("vigor"));
            Assert.AreEqual(1, copy.runs);
            Assert.AreEqual(3, copy.bestRoom);
        }

        [Test]
        public void Json_UnknownIdsDropped_LevelsClamped_NegativesFixed()
        {
            const string json = "{\"version\":1,\"ecos\":-5,\"upgrades\":[" +
                                "{\"id\":\"vigor\",\"level\":99},{\"id\":\"removida\",\"level\":2},{\"id\":\"vigor\",\"level\":1}]}";
            MetaProgress p = MetaProgress.FromJson(json);
            Assert.AreEqual(0, p.ecos);
            Assert.AreEqual(Vigor.maxLevel, p.GetLevel("vigor"));
            Assert.AreEqual(0, p.GetLevel("removida"));
            Assert.AreEqual(1, p.upgrades.Count);
        }

        [Test]
        public void Json_Garbage_Throws()
        {
            Assert.That(() => MetaProgress.FromJson("{isto nao e json"), Throws.Exception);
        }

        [Test]
        public void Ecos_RewardRoomsAndSkill()
        {
            var run = new RunState(1, new FlowTuning()) { RoomsCleared = 2, Finishers = 4, PerfectParries = 10 };
            int expected = MetaRules.RoomEcos(0) + MetaRules.RoomEcos(1) + 4 * MetaRules.EcosPerFinisher + 10 * MetaRules.EcosPerPerfectParry;
            Assert.AreEqual(expected, MetaRules.EcosForRun(run, false));
            Assert.AreEqual(expected + MetaRules.VictoryBonus, MetaRules.EcosForRun(run, true));
        }

        // ── Timing indicator ────────────────────────────────────────────────

        [Test]
        public void TimingCue_LandsInsideParryWindowAndDodgeIFrames()
        {
            var config = new PlayerTuning();
            var feel = new FeelTuning();
            Assert.IsTrue(TelegraphTiming.ParryAtCueIsPerfect(feel.timingCueLeadFrames, config.perfectParryFrames),
                          "Pressing parry when the ring closes must be a perfect parry");
            Assert.IsTrue(TelegraphTiming.DodgeAtCueIsInvulnerable(feel.timingCueLeadFrames, config.dodgeIFrameStart, config.dodgeIFrameEnd),
                          "Dodging when the ring closes must be invulnerable at impact");
        }

        [Test]
        public void TimingRing_ClosesBeforeImpact()
        {
            const int windup = 40, lead = 4, approach = 36;
            Assert.AreEqual(36, TelegraphTiming.CueFrame(windup, lead));
            Assert.AreEqual(0f, TelegraphTiming.ApproachProgress(0, windup, lead, approach), 1e-4f);
            Assert.AreEqual(1f, TelegraphTiming.ApproachProgress(36, windup, lead, approach), 1e-4f);
            Assert.IsTrue(TelegraphTiming.ApproachVisible(38, windup, lead, approach));
            Assert.IsFalse(TelegraphTiming.ApproachVisible(41, windup, lead, approach));
        }
    }
}
