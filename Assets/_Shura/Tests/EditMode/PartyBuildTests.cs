using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>In co-op every duelist is their own character: nothing of one build leaks into another.</summary>
    public class PartyBuildTests
    {
        static RunState NewRun() => new RunState(1234UL, new FlowTuning());

        [Test]
        public void Rewards_BelongToWhoPickedThem()
        {
            RunState run = NewRun();
            PlayerBuild host = run.Main;
            PlayerBuild mate = run.CreateBuild();

            mate.Acquire(RewardRegistry.Create(RewardIds.HungryBlade));
            Assert.IsTrue(mate.Owns(RewardIds.HungryBlade));
            Assert.IsFalse(host.Owns(RewardIds.HungryBlade), "the host did not pick it");
            Assert.Greater(mate.Modifiers.damageMult, host.Modifiers.damageMult);
            Assert.Less(mate.Modifiers.maxHealthBonus, host.Modifiers.maxHealthBonus);
        }

        [Test]
        public void Items_BagsAndWeapons_ArePersonal()
        {
            RunState run = NewRun();
            PlayerBuild host = run.Main;
            PlayerBuild mate = run.CreateBuild();
            var rng = new SeededRandom(9);
            Item item = ItemGenerator.Roll(rng, 5, true);

            Assert.IsTrue(mate.AddItem(item), "an empty slot wears the item at once");
            Assert.AreSame(item, mate.Equipped[(int)item.slot]);
            Assert.IsNull(host.Equipped[(int)item.slot]);
            Assert.IsTrue(mate.Effects.Contains(item));
            Assert.IsFalse(host.Effects.Contains(item));

            host.Weapons[0] = WeaponDefs.Get("katana");
            mate.Weapons[0] = WeaponDefs.Get("naginata");
            Assert.AreNotSame(host.CurrentWeapon, mate.CurrentWeapon);
        }

        [Test]
        public void FlowStreakAndTechniques_ArePersonal()
        {
            RunState run = NewRun();
            PlayerBuild host = run.Main;
            PlayerBuild mate = run.CreateBuild();
            mate.Flow.Add(1000f);
            mate.RegisterPerfect();
            mate.AddPendingTechnique(RewardIds.KunaiFan);
            Assert.AreEqual(0, host.Flow.Level);
            Assert.AreEqual(0, host.PerfectStreak);
            Assert.IsTrue(host.CanAbsorb(RewardIds.KunaiFan), "the teammate's absorbed technique is not the host's");
            Assert.Contains(RewardIds.KunaiFan, mate.PendingTechniques);
        }

        [Test]
        public void EveryoneDrawsTheirOwnCards_AndAscensionLiftsAll()
        {
            RunState run = NewRun();
            PlayerBuild host = run.Main;
            PlayerBuild mate = run.CreateBuild();
            string HostCards() => string.Join(",", host.RollChoices(3, 1).ConvertAll(r => r.Id));
            string MateCards() => string.Join(",", mate.RollChoices(3, 1).ConvertAll(r => r.Id));
            bool differs = false;
            for (int i = 0; i < 6 && !differs; i++) differs = HostCards() != MateCards();
            Assert.IsTrue(differs, "two players should not always see the same cards");

            float before = mate.Modifiers.maxHealthBonus;
            run.Ascend();
            Assert.Greater(mate.Modifiers.maxHealthBonus, before, "ascension is the party's");
            Assert.AreEqual(host.Modifiers.maxHealthBonus, mate.Modifiers.maxHealthBonus);
        }

        [Test]
        public void RunShortcuts_StillReachTheHost()
        {
            RunState run = NewRun();
            run.Acquire(RewardRegistry.Create(RewardIds.IronGuard));
            Assert.IsTrue(run.Main.Owns(RewardIds.IronGuard));
            Assert.AreSame(run.Main.Flow, run.Flow);
            Assert.AreSame(run.Main.Equipped, run.Equipped);
        }
    }
}
