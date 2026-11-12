using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>Phase 4, part 1: the temple, the currencies, the Altar's tree, the oaths and the lineage.</summary>
    public class Phase4Tests
    {
        static RunState Run(MetaProgress meta = null, ulong seed = 7) => new RunState(seed, new FlowTuning(), meta);

        static Lineage.Facts Facts(string killer = "Ronin") => new Lineage.Facts
        {
            biome = "Bosque Selvagem", time = TimeOfDay.Night, weather = Weather.Rain, killer = killer, weapon = "Katana", depth = 4, seed = 99,
        };

        // ── Save ──

        [Test]
        public void OldSaves_LoadWithTheNewFieldsEmpty()
        {
            const string v1 = "{\"version\":1,\"ecos\":120,\"runs\":4,\"upgrades\":[{\"id\":\"vigor\",\"level\":2}]}";
            MetaProgress p = MetaProgress.FromJson(v1);
            Assert.AreEqual(120, p.ecos);
            Assert.AreEqual(2, p.GetLevel("vigor"));
            Assert.AreEqual(0, p.steel);
            Assert.IsNull(p.disciple);
            Assert.AreEqual(0, p.cemetery.Count);
            Assert.AreEqual(MetaProgress.CurrentVersion, p.version);
        }

        [Test]
        public void NewFields_RoundTrip_AndUnknownIdsAreDropped()
        {
            var meta = new MetaProgress { steel = 3, fragments = 2, petals = 9, karma = 150 };
            meta.buildings.Add(Temple.Forge);
            meta.buildings.Add("pagode_inexistente");
            meta.nodes.Add(AltarTree.Diamond);
            meta.SetOath(Oaths.Hunger, 1);
            meta.SetOath(Oaths.EternalRival, 1);
            meta.disciple = new Disciple { name = "Ren", age = 41, generation = 3 };
            meta.disciple.traits.Add("mao_pesada");
            meta.disciple.traits.Add("traco_inventado");
            meta.cemetery.Add(new Disciple { name = "Haru", age = 30, jisei = "a\nb\nc" });
            MetaProgress copy = MetaProgress.FromJson(meta.ToJson());
            Assert.AreEqual(3, copy.steel);
            Assert.AreEqual(100, copy.karma, "Karma is clamped to ±100");
            CollectionAssert.AreEqual(new[] { Temple.Forge }, copy.buildings);
            Assert.IsTrue(copy.HasNode(AltarTree.Diamond));
            Assert.AreEqual(1, copy.OathLevel(Oaths.Hunger));
            Assert.AreEqual(0, copy.OathLevel(Oaths.EternalRival), "sealed until the Rivals");
            Assert.AreEqual("Ren", copy.disciple.name);
            CollectionAssert.AreEqual(new[] { "mao_pesada" }, copy.disciple.traits);
            Assert.AreEqual("a\nb\nc", copy.cemetery[0].jisei);
        }

        // ── Temple ──

        [Test]
        public void Temple_AltarStands_TheRestIsRebuiltWithEcosAndSteel()
        {
            var meta = new MetaProgress { ecos = 1000, steel = 1 };
            Assert.IsTrue(Temple.IsBuilt(meta, Temple.Altar));
            BuildingDef dojo = Temple.Get(Temple.Dojo);
            Assert.IsFalse(Temple.TryBuild(meta, dojo), "two steel needed");
            meta.steel = 2;
            Assert.IsTrue(Temple.TryBuild(meta, dojo));
            Assert.AreEqual(1000 - dojo.ecos, meta.ecos);
            Assert.AreEqual(0, meta.steel);
            Assert.IsTrue(Temple.IsBuilt(meta, Temple.Dojo));
            Assert.IsFalse(Temple.TryBuild(meta, dojo), "only once");
            meta.steel = 99;
            Assert.IsFalse(Temple.TryBuild(meta, Temple.Get(Temple.TeaHouse)), "its guests have not arrived");
        }

        // ── Altar ──

        [Test]
        public void Altar_PointsAreLimited_SoAProfileIsChosen()
        {
            var meta = new MetaProgress { ecos = 100000 };
            int bought = 0;
            foreach (MetaUpgradeDef def in MetaUpgrades.All)
            {
                while (meta.TryBuy(def)) bought++;
            }
            Assert.AreEqual(AltarTree.BasePoints, bought, "the tree stops at the point limit");
            Assert.AreEqual(0, AltarTree.PointsFree(meta));
            meta.runs = 300;
            Assert.AreEqual(AltarTree.MaxPoints, AltarTree.PointsLimit(meta));
        }

        [Test]
        public void Altar_BranchEnd_NeedsLevelsFragmentsAndTwoPoints_AndResetRefunds()
        {
            var meta = new MetaProgress { ecos = 100000, fragments = 10 };
            AltarBranch blade = AltarTree.Branches[1];
            Assert.IsFalse(AltarTree.TryOpen(meta, blade), "no levels in the branch yet");
            for (int i = 0; i < blade.required; i++) Assert.IsTrue(meta.TryBuy(MetaUpgrades.Get(i < 4 ? "lamina" : "olhar")));
            int ecosBefore = meta.ecos;
            Assert.IsTrue(AltarTree.TryOpen(meta, blade));
            Assert.AreEqual(ecosBefore - blade.node.ecos, meta.ecos);
            Assert.AreEqual(10 - blade.node.fragments, meta.fragments);
            Assert.AreEqual(blade.required + AltarTree.NodePoints, AltarTree.PointsUsed(meta));
            var run = Run(meta);
            Assert.IsTrue(run.Modifiers.thirdWeapon);

            meta.ResetAltar();
            Assert.AreEqual(100000, meta.ecos, "every Eco comes back");
            Assert.AreEqual(10, meta.fragments);
            Assert.AreEqual(0, AltarTree.PointsUsed(meta));
        }

        [Test]
        public void DiamondBody_IgnoresTheFirstWound()
        {
            var meta = new MetaProgress();
            meta.nodes.Add(AltarTree.Diamond);
            Assert.IsTrue(Run(meta).Main.IgnoreFirstWound);
            Assert.IsFalse(Run(new MetaProgress()).Main.IgnoreFirstWound);
        }

        // ── Oaths ──

        [Test]
        public void Oaths_AddCalor_ThatMultipliesEcosAndPetals()
        {
            var meta = new MetaProgress();
            meta.SetOath(Oaths.FrailFlesh, 2);
            meta.SetOath(Oaths.Hunger, 1);
            int heat = Oaths.Heat(meta);
            Assert.AreEqual(2 * 1 + 2, heat);
            Assert.AreEqual(1.4f, Oaths.EcosMult(heat), 1e-4f);
            Assert.Greater(Oaths.Petals(heat, 10, 1), Oaths.Petals(0, 10, 1));
            Assert.AreEqual(0, Oaths.Petals(heat, 0, 0), "nothing for a journey that never left");

            var run = Run();
            run.Oaths = OathSet.From(meta);
            run.RecomputeModifiers();
            Assert.AreEqual(heat, run.Oaths.Heat);
            Assert.AreEqual(0.6f, run.Modifiers.maxHealthMult, 1e-4f, "−20% per level");
            Assert.IsTrue(run.Modifiers.noRoomHeal);
        }

        [Test]
        public void Oaths_SilentRing_AndSingleBladeII()
        {
            var set = new OathSet();
            set.Set(Oaths.SilentRing, 3);
            set.Set(Oaths.SingleBlade, 2);
            var m = new PlayerModifiers();
            set.Apply(m);
            Assert.AreEqual(0.6f, m.silentRingChance, 1e-4f);
            Assert.IsTrue(m.noSwapParry);
            Assert.AreEqual(3 * 1 + 2 * 2, set.Heat);
            StringAssert.Contains("Anel Silencioso III", set.Summary());
        }

        [Test]
        public void BloodPath_AddsWaves_BeforeTheGuardian()
        {
            var waves = new[]
            {
                new[] { new EnemySpawn("ronin"), new EnemySpawn("lanceiro") },
                new[] { new EnemySpawn("ronin", true) },
            };
            var room = new RoomDef("Sala", 5, Biomes.All[0], 20f, 12f, true, waves);
            RoomDef bloody = OathRules.WithExtraWaves(room, 2);
            Assert.AreEqual(4, bloody.waves.Length);
            Assert.IsTrue(bloody.waves[3][0].elite, "the Guardian still comes last");
            foreach (EnemySpawn s in bloody.waves[1]) Assert.IsFalse(s.elite);
            Assert.AreSame(room, OathRules.WithExtraWaves(room, 0));
            room.kind = RoomKind.Duel;
            Assert.AreSame(room, OathRules.WithExtraWaves(room, 2), "a duel stays one against one");
        }

        // ── Lineage ──

        [Test]
        public void Age_ChangesBodyAndMind()
        {
            var young = new MetaProgress { disciple = new Disciple { name = "A", age = 20 } };
            var prime = new MetaProgress { disciple = new Disciple { name = "B", age = 35 } };
            var elder = new MetaProgress { disciple = new Disciple { name = "C", age = 60 } };
            var my = new PlayerModifiers();
            var mp = new PlayerModifiers();
            var me = new PlayerModifiers();
            Lineage.Apply(young, my);
            Lineage.Apply(prime, mp);
            Lineage.Apply(elder, me);
            Assert.AreEqual(-1, my.perfectParryFrameBonus);
            Assert.Greater(my.moveSpeedMult, 1f);
            Assert.AreEqual(0, mp.perfectParryFrameBonus);
            Assert.AreEqual(1f, mp.moveSpeedMult, 1e-4f);
            Assert.AreEqual(2, me.perfectParryFrameBonus);
            Assert.Less(me.moveSpeedMult, 1f);
            Assert.Greater(me.ringWarningMult, 1f, "rings appear earlier");
            Assert.AreEqual(AgeBand.Retired, Lineage.Band(70));
        }

        [Test]
        public void Candidates_AreThree_WithOneOrTwoTraits_AndTheSameForTheSameSeed()
        {
            var a = Lineage.Candidates(42, 2, "tec");
            var b = Lineage.Candidates(42, 2, "tec");
            Assert.AreEqual(3, a.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(a[i].name, b[i].name);
                Assert.That(a[i].traits.Count, Is.InRange(1, 2));
                Assert.That(a[i].age, Is.InRange(18, 40));
                Assert.AreEqual(2, a[i].generation);
                Assert.AreEqual("tec", a[i].technique);
                Assert.AreEqual(a[i].traits.Count, new System.Collections.Generic.HashSet<string>(a[i].traits).Count, "no trait twice");
            }
        }

        [Test]
        public void Death_BeforeDepth10_BuriesTheDisciple_WithAPoem_AndOffersHeirs()
        {
            var meta = new MetaProgress { disciple = Lineage.First() };
            var run = Run(meta);
            run.RoomsCleared = 4;
            run.Karma = 2;
            run.Main.CelestialSteel = 1;
            run.ElitesKilled = 2;
            run.Main.SoulFragments = 3;
            LegacyReport r = Legacy.Bank(meta, run, Facts(), true);
            Assert.IsTrue(r.died);
            Assert.IsNull(meta.disciple);
            Assert.AreEqual(1, meta.cemetery.Count);
            Assert.AreEqual("Ronin", meta.cemetery[0].cause);
            Assert.AreEqual(3, meta.cemetery[0].jisei.Split('\n').Length, "a haiku has three lines");
            Assert.AreEqual(3, meta.heirs.Count);
            Assert.AreEqual(2, meta.heirs[0].generation);
            Assert.AreEqual(3, meta.steel, "the pouch and one per elite");
            Assert.AreEqual(3, meta.fragments);
            Assert.AreEqual(2 * Legacy.KarmaWeight, meta.karma);

            Assert.IsTrue(Legacy.ChooseHeir(meta, 1));
            Assert.IsNotNull(meta.disciple);
            Assert.AreEqual(0, meta.heirs.Count);
        }

        [Test]
        public void Quitting_DoesNotKill_AndDepth10_AgesFiveYears_UntilTheSenseiRetires()
        {
            var meta = new MetaProgress { disciple = new Disciple { name = "Aoi", age = 60 } };
            var run = Run(meta);
            run.RoomsCleared = 3;
            Legacy.Bank(meta, run, Facts(), false);
            Assert.AreEqual("Aoi", meta.disciple.name, "a quit is not a death");
            Assert.AreEqual(60, meta.disciple.age);

            run = Run(meta);
            run.RoomsCleared = 12;
            LegacyReport r = Legacy.Bank(meta, run, Facts(), true);
            Assert.IsTrue(r.aged);
            Assert.IsFalse(r.died, "past depth 10 the disciple comes home");
            Assert.AreEqual(65, meta.disciple.age);

            run = Run(meta);
            run.RoomsCleared = 15;
            r = Legacy.Bank(meta, run, Facts(), true);
            Assert.IsTrue(r.retired);
            Assert.IsNull(meta.disciple);
            Assert.IsTrue(meta.cemetery[0].retired);
            Assert.AreEqual(1, Lineage.Senseis(meta));
            var m = new PlayerModifiers();
            Lineage.Apply(meta, m);
            Assert.AreEqual(6f, m.maxHealthBonus, 1e-4f, "the Sensei teaches the next ones");
        }

        [Test]
        public void Heir_InheritsTheMostRaisedTechnique()
        {
            var meta = new MetaProgress { disciple = Lineage.First() };
            var run = Run(meta);
            string id = EnemyDefs.All[1].techniqueId;
            Assume.That(id, Is.Not.Null.And.Not.Empty);
            run.Acquire(RewardRegistry.Create(id));
            Assert.AreEqual(id, Legacy.MostUsedTechnique(run.Main));
            run.RoomsCleared = 1;
            Legacy.Bank(meta, run, Facts(), true);
            Assert.AreEqual(id, meta.heirs[0].technique);
        }

        [Test]
        public void Jisei_IsTheSameForTheSameFacts_AndSpeaksOfTheKiller()
        {
            string a = Lineage.Jisei(Facts("Oni"));
            Assert.AreEqual(a, Lineage.Jisei(Facts("Oni")));
            bool mentioned = false;
            for (ulong s = 0; s < 20 && !mentioned; s++)
            {
                Lineage.Facts f = Facts("Oni");
                f.seed = s;
                mentioned = Lineage.Jisei(f).Contains("Oni");
            }
            Assert.IsTrue(mentioned, "the poem names who struck the blow");
        }

        [Test]
        public void Traits_ShapeTheBody()
        {
            var meta = new MetaProgress { disciple = new Disciple { name = "X", age = 35 } };
            meta.disciple.traits.Add("mao_pesada");
            meta.disciple.traits.Add("olhar_noturno");
            var m = new PlayerModifiers();
            Lineage.Apply(meta, m);
            Assert.AreEqual(20f, m.maxPostureBonus, 1e-4f);
            Assert.Less(m.moveSpeedMult, 1f);
            Assert.IsTrue(m.nightSight);
        }

        // ── Currencies ──

        [Test]
        public void AbsorbingATechniqueAgain_LeavesASoulFragment()
        {
            var run = Run();
            string id = EnemyDefs.All[1].techniqueId;
            Assume.That(id, Is.Not.Null.And.Not.Empty);
            run.Main.AddPendingTechnique(id);
            Assert.AreEqual(0, run.Main.SoulFragments);
            run.Main.AddPendingTechnique(id);
            Assert.AreEqual(1, run.Main.SoulFragments);
        }

        [Test]
        public void Garden_BuysAndWearsABladeColour()
        {
            var meta = new MetaProgress { petals = 30 };
            BladeColorDef sakura = Garden.Color("sakura");
            Assert.IsTrue(Garden.TryBuyColor(meta, sakura));
            Assert.AreEqual(30 - sakura.petals, meta.petals);
            Assert.AreEqual("sakura", meta.bladeColor);
            Assert.IsFalse(Garden.TryBuyColor(meta, sakura), "already owned");
            var m = new PlayerModifiers();
            meta.ApplyTo(m);
            Assert.AreEqual(sakura.color, m.bladeColor);
        }

        [Test]
        public void LevelsCode_CarriesTheNodesAndTheBody()
        {
            var meta = new MetaProgress { disciple = new Disciple { name = "R", age = 55 } };
            meta.nodes.Add(AltarTree.Clairvoyance);
            meta.disciple.traits.Add("pes_leves");
            MetaProgress guest = MetaProgress.FromLevelsCode(meta.LevelsCode());
            Assert.IsTrue(guest.HasNode(AltarTree.Clairvoyance));
            Assert.AreEqual(55, guest.disciple.age);
            CollectionAssert.AreEqual(new[] { "pes_leves" }, guest.disciple.traits);
        }
    }
}
