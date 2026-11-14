using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>WP-03: o núcleo do combate depois dos cortes (janela constante, kamae por arma, selos como técnica).</summary>
    public class CoreCombatRedesignTests
    {
        [TearDown]
        public void Restore() => Feature.Reset();

        /// <summary>Tudo o que a build pode juntar num PlayerModifiers.</summary>
        static PlayerModifiers EverythingAtOnce()
        {
            var m = new PlayerModifiers();
            foreach (string id in RewardIds.Techniques)
            {
                for (int level = 1; level <= 3; level++)
                {
                    foreach (int branch in new[] { 0, TechniqueDef.BranchFull, TechniqueDef.BranchEcho })
                    {
                        if (!(RewardRegistry.Create(id) is TechniqueDef t)) continue;
                        t.Level = level;
                        t.Branch = level >= 3 ? branch : 0;
                        t.Apply(m);
                    }
                }
            }
            foreach (string id in RewardIds.Relics) RewardRegistry.Create(id)?.Apply(m);
            foreach (string id in RewardIds.Blessings) RewardRegistry.Create(id)?.Apply(m);
            foreach (string id in RoadRelics.All) RewardRegistry.Create(id)?.Apply(m);
            foreach (TsubaDef t in WeaponForge.Tsubas) t.apply?.Invoke(m);
            foreach (MetaUpgradeDef u in MetaUpgrades.All) u.Apply(m, u.maxLevel);
            foreach (TraitDef t in Lineage.Traits) t.apply?.Invoke(m);
            return m;
        }

        [Test]
        public void NoBuild_ChangesTheParryWindow()
        {
            var t = new PlayerTuning();
            PlayerModifiers all = EverythingAtOnce();
            var extreme = new PlayerModifiers { perfectParryFrameBonus = 40 };
            var negative = new PlayerModifiers { perfectParryFrameBonus = -40 };
            foreach (PlayerModifiers m in new[] { all, extreme, negative, new PlayerModifiers() })
            {
                foreach (int kamae in new[] { -2, 0, 2, 3 })
                {
                    foreach (int age in new[] { -1, 0, 2 })
                    {
                        foreach (bool exhausted in new[] { false, true })
                            Assert.AreEqual(t.perfectParryFrames, ParryWindow.Frames(t, m, kamae, age, 0, exhausted), "window must stay constant");
                    }
                }
            }
            // A assistência de acessibilidade (WP-17) é a única exceção.
            Assert.AreEqual(t.perfectParryFrames + 3, ParryWindow.Frames(t, all, 0, 0, 3, false));
            // Com a flag da v0.15.0 a varredura muda a janela — o teste acima não é vazio.
            Feature.Set("ParryWindowModifiers", true);
            Assert.AreNotEqual(t.perfectParryFrames, ParryWindow.Frames(t, extreme, 0, 0, 0, false));
            Assert.AreNotEqual(t.perfectParryFrames, ParryWindow.Frames(t, new PlayerModifiers(), 0, 0, 0, true));
        }

        [Test]
        public void TheLateForgiveness_OnlyForRingsThatWereReadable()
        {
            var t = new PlayerTuning();
            Assert.AreEqual(t.parryLateForgivenessFrames, ParryWindow.Forgiveness(t, new AttackDef { windup = 40 }, 36));
            Assert.AreEqual(0, ParryWindow.Forgiveness(t, new AttackDef { windup = 20 }, 36), "a ring seen for 20 f gives no forgiveness");
            Assert.AreEqual(0, ParryWindow.Forgiveness(t, null, 36));
        }

        [Test]
        public void EachWeapon_HasItsOwnGuard()
        {
            Assert.AreEqual(Kamae.Chudan, PlayerController.GuardOf(WeaponDefs.Get("katana")));
            Assert.AreEqual(Kamae.Jodan, PlayerController.GuardOf(WeaponDefs.Get("montante")));
            Assert.AreEqual(Kamae.Chudan, PlayerController.GuardOf(WeaponDefs.Get("naginata")));
            Assert.AreEqual(Kamae.Waki, PlayerController.GuardOf(WeaponDefs.Get("gemeas")));
            Assert.AreEqual(Kamae.Gedan, PlayerController.GuardOf(WeaponDefs.Get("manoplas")));
            Assert.AreEqual(Kamae.Waki, PlayerController.GuardOf(WeaponDefs.Get("kusarigama")));
            Assert.AreEqual(Kamae.Gedan, PlayerController.GuardOf(WeaponDefs.Get("tessen")));
            Assert.AreEqual(Kamae.Jodan, PlayerController.GuardOf(WeaponDefs.Get("shakujo")));
            Assert.AreEqual(Kamae.Jodan, PlayerController.GuardOf(WeaponDefs.Get("kanabo")), "the Oni aspect of the Montante");
            Assert.AreEqual(Kamae.Waki, PlayerController.GuardOf(WeaponDefs.Get("wakizashi")), "the Shadow aspect of the Twin Blades");
        }

        [Test]
        public void TheKiSeals_BecameTheOnmyojisTechnique()
        {
            Assert.AreEqual(RewardIds.InnerSeal, EnemyDefs.Onmyoji.Teaches);
            var t = RewardRegistry.Create(RewardIds.InnerSeal) as TechniqueDef;
            Assert.IsNotNull(t);
            Assert.AreEqual(EnemyDefs.Onmyoji.displayName, t.SourceEnemy);
            Feature.Set("KiSealsBase", true);
            Assert.AreEqual(RewardIds.ExplosiveSeal, EnemyDefs.Onmyoji.Teaches, "the old technique comes back with the old seals");
        }

        [Test]
        public void ThePlannedNumbers_LiveInTheData()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            string json = Balance.ToJson(config);
            foreach (string key in new[] { "parryLateForgivenessFrames", "forgivenessMinRingFrames", "mikiriPosture", "mikiriDisarmFrames", "recoverySpeedPerLevel" })
                StringAssert.Contains("\"" + key + "\"", json, key);
            Object.DestroyImmediate(config);
        }
    }
}
