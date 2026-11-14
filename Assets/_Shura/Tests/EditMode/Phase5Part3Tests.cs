using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 5, part 3: factions, formations and the twelve new enemies.</summary>
    public class Phase5Part3Tests
    {
        static readonly string[] NewOnes =
        {
            "kamaitachi", "rokurokubi", "aranha_menor", "nopperabo", "ittan_momen", "nurikabe",
            "omukade", "nue", "tsukumogami", "sacerdote", "zato_cego", "espelho",
        };

        [Test]
        public void TwelveNewEnemies_OnePerDepth_EachTeachingItsOwnTechnique()
        {
            Assert.AreEqual(36, EnemyDefs.All.Length);
            for (int i = 0; i < NewOnes.Length; i++)
            {
                Assert.IsTrue(EnemyDefs.TryGet(NewOnes[i], out EnemyDef d), NewOnes[i]);
                Assert.AreEqual(13 + i, RoomGenerator.UnlockDepth(d.id), d.id);
                CollectionAssert.AreEqual(new[] { d.id }, RoomGenerator.NewEnemiesAt(13 + i));
                var t = RewardRegistry.Create(d.techniqueId) as TechniqueDef;
                Assert.IsNotNull(t, d.id + " teaches " + d.techniqueId);
                Assert.AreEqual(d.displayName, t.SourceEnemy);
                bool signature = false;
                foreach (MoveDef m in d.moves) signature |= m.isSignature;
                Assert.IsTrue(signature, d.id + " has a signature");
            }
        }

        [Test]
        public void TheNewRules_AreInTheirDefinitions()
        {
            Assert.AreEqual(3, EnemyDefs.Omukade.segments);
            Assert.AreEqual(3, EnemyDefs.Nue.natures.Length);
            Assert.AreSame(EnemyDefs.Nue.natures[0], EnemyDefs.Nue.moves);
            Assert.IsTrue(EnemyDefs.Tsukumogami.onlyFinisher);
            Assert.IsTrue(EnemyDefs.ZatoCego.blind);
            Assert.IsTrue(EnemyDefs.Nopperabo.mimic);
            Assert.IsTrue(EnemyDefs.Espelho.mirror);
            bool web = false, seal = false, breath = false, push = false;
            foreach (EnemyDef d in EnemyDefs.All)
            {
                foreach (MoveDef m in d.moves)
                {
                    foreach (AttackDef a in m.steps)
                    {
                        web |= a.leavesWeb;
                        seal |= a.sealsTechniques;
                        breath |= a.drainsBreath;
                        push |= a.pushBack > 0f;
                    }
                }
            }
            Assert.IsTrue(web && seal && breath && push);
        }

        [Test]
        public void FourFactions_TheHealerMendsHerOwn_TheOnryoRaisesHumans()
        {
            Assert.AreEqual(EnemyFaction.Human, Factions.Of("ronin"));
            Assert.AreEqual(EnemyFaction.Yokai, Factions.Of("kappa"));
            Assert.AreEqual(EnemyFaction.Oni, Factions.Of("ciclope"));
            Assert.AreEqual(EnemyFaction.Dead, Factions.Of("onryo"));
            Assert.IsTrue(Factions.Hostile(EnemyDefs.Ronin, EnemyDefs.Kappa));
            Assert.IsFalse(Factions.Hostile(EnemyDefs.Ronin, EnemyDefs.Lanceiro));
            Assert.IsFalse(RoomGenerator.IsYokai("ronin"));
            Assert.IsTrue(RoomGenerator.IsYokai("esqueleto"), "the dead fight on the other side of a dispute");

            EnemyDef possessed = Factions.PossessedOf(EnemyDefs.Ronin);
            Assert.AreEqual("ronin", possessed.id, "drawn like the body it took");
            Assert.IsTrue(possessed.minion);
            Assert.AreEqual(EnemyDefs.Ronin.maxHealth * 0.5f, possessed.maxHealth, 1e-4f);
            Assert.IsFalse(EnemyDefs.Ronin.minion, "the original is untouched");
        }

        [Test]
        public void Formations_AppearInFights_WithEveryMemberUnlocked()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed < 400; seed++)
            {
                EnemySpawn[] wave = Formations.TryPick(seed, 14);
                if (wave == null) continue;
                var ids = new List<string>();
                foreach (EnemySpawn s in wave)
                {
                    Assert.LessOrEqual(RoomGenerator.UnlockDepth(s.id), 14);
                    ids.Add(s.id);
                }
                foreach (FormationDef f in Formations.All)
                {
                    var need = new List<string>(f.members);
                    foreach (string id in ids) need.Remove(id);
                    if (need.Count == 0) seen.Add(f.id);
                }
            }
            CollectionAssert.IsSupersetOf(seen, new[] { Formations.Phalanx, Formations.Ritual, Formations.Hunt, Formations.Bridge, Formations.Storm });
            Assert.IsNull(Formations.TryPick(3, 1) == null ? null : Formations.TryPick(3, 1).Length > 3 ? "too big" : null);
        }

        [Test]
        public void NewTechniques_PayAtLevelOne_AndGiveTheirGain()
        {
            var ear = (TechniqueDef)RewardRegistry.Create(RewardIds.InnerEar);
            var m = new PlayerModifiers();
            ear.Apply(m);
            Assert.AreEqual(1, m.perfectParryFrameBonus);
            Assert.AreEqual(-10f, m.maxHealthBonus, 1e-4f);
            ear.Level = 2;
            m = new PlayerModifiers();
            ear.Apply(m);
            Assert.AreEqual(0f, m.maxHealthBonus, 1e-4f, "no price from level II");

            var shell = (TechniqueDef)RewardRegistry.Create(RewardIds.SegmentedShell);
            m = new PlayerModifiers();
            shell.Apply(m);
            Assert.AreEqual(25f, m.maxPostureBonus, 1e-4f);
        }
    }
}
