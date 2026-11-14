using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>WP-00: as feature flags do redesenho são completas, reversíveis e partem do design novo.</summary>
    public class FeatureFlagTests
    {
        [TearDown]
        public void Restore() => Feature.Reset();

        static readonly string[] Planned =
        {
            "Stamina", "LocalWounds", "KiSealsBase", "KamaeSwitch", "CursedRing", "DoubleRingType", "FlowTimeScale",
            "ShuraMeterBase", "LegacyToolsAndKeys", "NamedLinks45", "StatLoot", "ItemClans", "Blessings", "ForgeHandles",
            "ExtraWeapons", "AgeStats", "SenseiStats", "ParryWindowModifiers", "RealtimeDayClock", "StormWeather",
            "InfiniteRunDefault", "ExtraEnemies", "AiReadsExtended", "RivalHierarchy", "BrowserClient", "PartyMode8",
            "BloodPact", "AsyncPresence", "PvpRanked", "LegacyModes", "LegacyTemple", "LegacyCurrencies", "ThirdWeapon",
        };

        [Test]
        public void EveryPlannedFlagExists_AndNoOther()
        {
            var names = new HashSet<string>(Feature.Names);
            CollectionAssert.AreEquivalent(Planned, names);
            Assert.AreEqual(Planned.Length, Feature.Count);
        }

        [Test]
        public void TheDefaultIsTheNewDesign_OnlyTheThirdWeaponStaysOn()
        {
            Feature.Reset();
            foreach (string n in Planned) Assert.AreEqual(n == "ThirdWeapon", Feature.Get(n), n);
            Assert.IsFalse(Feature.Stamina);
            Assert.IsTrue(Feature.ThirdWeapon);
        }

        [Test]
        public void AllOn_IsTheOldGame_AndResetReturns()
        {
            Feature.SetAll(true);
            foreach (string n in Planned) Assert.IsTrue(Feature.Get(n), n);
            Assert.IsTrue(Feature.Stamina && Feature.RivalHierarchy && Feature.StatLoot);
            Feature.Reset();
            Assert.IsFalse(Feature.Stamina);
        }

        [Test]
        public void APartialJson_ChangesOnlyItsKeys()
        {
            Feature.Apply("{\"Stamina\":true,\"PvpRanked\":true}");
            Assert.IsTrue(Feature.Stamina);
            Assert.IsTrue(Feature.PvpRanked);
            Assert.IsFalse(Feature.LocalWounds);
            Assert.IsTrue(Feature.ThirdWeapon);
            Feature.Set("ThirdWeapon", false);
            Assert.IsFalse(Feature.ThirdWeapon);
            Assert.IsTrue(Feature.DefaultOf("ThirdWeapon"));
        }
    }
}
