using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 3, part 2 (design doc §4.1, §4.2, §4.5): the new arsenal, the forge, the soul.</summary>
    public class Phase3Part2Tests
    {
        [Test]
        public void NewWeapons_EachHasItsOwnWayOfFighting()
        {
            Assert.IsTrue(WeaponDefs.Tessen.deflects);
            Assert.AreEqual(WeaponPower.FoldedWind, WeaponDefs.Tessen.swapPower);
            Assert.Greater(WeaponDefs.Kanabo.stunOnHit, 0);
            Assert.IsTrue(WeaponDefs.Kanabo.noBlock, "the kanabō cannot block");
            Assert.Greater(WeaponDefs.Kanabo.combo[0].windup, WeaponDefs.Montante.combo[0].windup, "slower than the montante");
            Assert.IsTrue(WeaponDefs.Shakujo.exposes);
            Assert.Greater(WeaponDefs.Shakujo.spiritMult, 1f);
            Assert.AreEqual(WeaponPower.Exorcism, WeaponDefs.Shakujo.finisherPower);
            Assert.AreEqual(2f, WeaponDefs.Wakizashi.backstabMult, 1e-4f);
            Assert.That(WeaponDefs.Wakizashi.kunaiStep, Is.InRange(0, WeaponDefs.Wakizashi.combo.Length - 1));
            Assert.IsNotNull(ShapeLibrary.Get(Shape.Fan), "the war fan is drawn procedurally");
        }

        [Test]
        public void Forge_ComposesBladeGripAndTsuba()
        {
            string id = WeaponForge.Compose("katana", "naginata", "lua");
            Assert.AreEqual("katana+naginata+lua", id);
            Assert.IsTrue(WeaponDefs.Exists(id));
            WeaponDef w = WeaponDefs.Get(id);
            Assert.AreEqual("Nagamaki", w.name);
            Assert.AreEqual("katana", w.BaseId, "mastery follows the blade");
            Assert.AreSame(WeaponDefs.Katana.combo, w.combo, "the blade gives the combo");
            Assert.AreSame(WeaponDefs.Naginata.swapStrike, w.swapStrike, "the grip gives the swap strike");
            Assert.AreEqual(WeaponDefs.Naginata.finisherName, w.finisherName);
            Assert.AreEqual("lua", w.tsubaId);
            Assert.AreSame(w, WeaponDefs.Get(id), "built once");

            Assert.AreEqual("montante", WeaponForge.Compose("montante", "montante", WeaponForge.NoTsuba), "nothing changed: the plain blade");
            Assert.IsFalse(WeaponDefs.Exists("katana+lixo+lua"));
            Assert.AreEqual(13, WeaponForge.Tsubas.Length, "12 tsubas and the empty guard");
        }

        [Test]
        public void Forge_TsubaPassiveReachesTheBuild()
        {
            var run = new RunState(1UL, new FlowTuning());
            PlayerBuild b = run.Main;
            b.Weapons[0] = WeaponDefs.Katana;
            b.Weapons[1] = WeaponDefs.Montante;
            b.RecomputeModifiers();
            float before = b.Modifiers.maxPostureBonus;
            b.Weapons[0] = WeaponDefs.Get(WeaponForge.Compose("katana", "katana", "ferro"));
            b.RecomputeModifiers();
            Assert.AreEqual(before + 20f, b.Modifiers.maxPostureBonus, 1e-4f);
        }

        [Test]
        public void Settings_KeepTheForgedLoadoutPerBlade()
        {
            var s = new GameSettings();
            Assert.AreEqual("katana", s.Loadout("katana"));
            s.SetForge("katana", "wakizashi", "sol");
            Assert.AreEqual("katana+wakizashi+sol", s.Loadout("katana"));
            Assert.AreEqual("Daishō", WeaponDefs.Get(s.Loadout("katana")).name);
            s.SetForge("katana", "katana", WeaponForge.NoTsuba);
            Assert.AreEqual("katana", s.Loadout("katana"), "back to the plain blade");
        }

        [Test]
        public void Links_AreNamedForEveryPairAndForgedBlades()
        {
            string forged = WeaponForge.Compose("katana", "naginata", "lua");
            Assert.AreEqual(EloNames.For("katana", "kanabo"), EloNames.For(forged, "kanabo"), "a forged weapon links by its blade");
        }

        [Test]
        public void Soul_ComesFromATechniqueAtLevelThree()
        {
            var run = new RunState(2UL, new FlowTuning());
            PlayerBuild b = run.Main;
            b.Acquire(RewardRegistry.Create(RewardIds.EtherealBody));
            var t = b.OwnedTechnique(RewardIds.EtherealBody);
            EnemyDef soul = null;
            foreach (EnemyDef d in EnemyDefs.All)
            {
                if (d.techniqueId == RewardIds.EtherealBody) soul = d;
            }
            Assert.IsNotNull(soul, "the Onryō gives the Corpo Etéreo");
            Assert.IsTrue(soul.ethereal, "its soul is ethereal");
            t.Level = TechniqueDef.MaxLevel;
            Assert.AreEqual(TechniqueDef.MaxLevel, t.Level);
            Assert.AreEqual(BufferedAction.Soul, (BufferedAction)10);
            Assert.AreEqual(11, InputBuffer.Count);
        }
    }
}
