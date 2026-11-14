using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 5, part 2: the Lords of the biomes, the Ashura and the endings.</summary>
    public class Phase5Part2Tests
    {
        [Test]
        public void SixLords_EachWithThreePhases_AndInTheIndex()
        {
            Assert.AreEqual(6, BossDefs.All.Length);
            foreach (BossDef b in BossDefs.All)
            {
                Assert.AreEqual(BossDefs.Phases, b.phases.Length, b.id);
                Assert.AreEqual(BossDefs.Phases, b.phaseLines.Length, b.id);
                foreach (MoveDef[] phase in b.phases)
                {
                    Assert.Greater(phase.Length, 0, b.id);
                    bool signature = false;
                    foreach (MoveDef m in phase) signature |= m.isSignature;
                    Assert.IsTrue(signature, b.id + " needs a signature for its Breaking Point");
                }
                Assert.IsTrue(EnemyDefs.TryGet(b.body.id, out _), b.id);
                Assert.IsNotEmpty(b.breakName);
                Assert.IsNotEmpty(b.relicName);
            }
            Assert.IsTrue(EnemyDefs.TryGet(BossDefs.ColossusHand.id, out _));
            Assert.IsTrue(EnemyDefs.TryGet(BossDefs.AshuraArm.id, out _));
        }

        [Test]
        public void EachLord_RulesTheLastRoomOfItsBiome_AndTheAshuraEveryFifty()
        {
            Assert.AreEqual("kagutsuchi", BossDefs.ForDepth(4).id);
            Assert.AreEqual("jorogumo", BossDefs.ForDepth(14).id);
            Assert.AreEqual("o_kyuketsuki", BossDefs.ForDepth(19).id);
            Assert.AreEqual("gashadokuro_colossal", BossDefs.ForDepth(24).id);
            Assert.AreEqual("yuki_onna", BossDefs.ForDepth(29).id);
            Assert.AreEqual("ashura", BossDefs.ForDepth(49).id);
            Assert.AreEqual("ashura", BossDefs.ForDepth(99).id);
            Assert.IsNull(BossDefs.ForDepth(9), "the Wild Wood keeps an ordinary Guardian");
            Assert.IsNull(BossDefs.ForDepth(3), "only the last room of a biome");
        }

        [Test]
        public void TheThrone_NewcomersFirst_ThenTheLordAlone()
        {
            RoomDef room = RoomGenerator.Generate(7, 4, RouteMap.Default(7, 4));
            Assert.AreEqual("Trono de Kagutsuchi", room.name);
            EnemySpawn[] last = room.waves[room.waves.Length - 1];
            Assert.AreEqual(1, last.Length, "the Lord comes alone");
            Assert.AreEqual("kagutsuchi", last[0].id);
            Assert.IsTrue(last[0].elite);
            Assert.AreEqual(RoomGenerator.NewEnemiesAt(4).Count + 1, room.waves.Length, "newcomers first, each alone");
        }

        [Test]
        public void TheExclusiveItem_CarriesTheLordsName()
        {
            Item relic = World.BossRelic(BossDefs.All[0], new SeededRandom(4));
            Assert.AreEqual(Rarity.Ancestral, relic.rarity);
            StringAssert.Contains("Kagutsuchi", relic.Name);
            Assert.AreNotEqual(UniqueAffix.None, relic.unique);
        }

        [Test]
        public void TheKingsFall_DecidesTheEnding()
        {
            var meta = new MetaProgress();
            var run = new RunState(3, new FlowTuning(), meta);
            Assert.AreEqual(GameModes.EndingWheel, Endings.Decide(run, null));
            meta.karma = 80;
            Assert.AreEqual(GameModes.EndingLiberation, Endings.Decide(run, null));
            meta.missions.Add(TeaGuests.Tsubaki);
            for (int i = 0; i < Rivals.MaxGenerals; i++) meta.fallenRivals.Add(new Rival { id = "g" + i, rank = Rivals.GeneralRank });
            Assert.AreEqual(Endings.Secret, Endings.Decide(run, null));
            Assert.AreEqual("AS CAMÉLIAS CAEM INTEIRAS", GameModes.EndingTitle(Endings.Secret));
        }
    }
}
