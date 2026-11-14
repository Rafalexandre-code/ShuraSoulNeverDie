using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>Phase 5, part 1: the rivals of the Shuradō, the adaptive AI 2.0.</summary>
    public class Phase5Tests
    {
        static EnemyDef Ronin => EnemyDefs.Get("ronin");

        static Rival Kill(MetaProgress meta, EnemyDef by, string rivalId = null, int ecos = 100, string technique = null, ulong seed = 1) =>
            Rivals.OnPlayerKilled(meta, by, rivalId, 0, "Haru", "Katana", technique, 0, ecos, seed);

        // ── Rivals ──

        [Test]
        public void TheKiller_BecomesARival_WithAName_YourEcos_ATraitAndAScar()
        {
            var meta = new MetaProgress();
            Rival r = Kill(meta, Ronin, technique: "Lâmina Paciente");
            Assert.IsNotNull(r);
            StringAssert.Contains("Haru", r.name);
            StringAssert.Contains(Ronin.displayName, r.name);
            Assert.AreEqual(100, r.ecos);
            Assert.AreEqual("tecnica", r.trait);
            Assert.AreEqual("Lâmina Paciente", r.traitName);
            Assert.IsNotEmpty(r.scar);
            Assert.AreEqual(1, meta.rivals.Count);
            Assert.IsNull(Kill(meta, null), "the road itself makes no rival");

            Rival armed = Kill(meta, EnemyDefs.Get("lanceiro"), seed: 9);
            Assert.AreEqual("arma", armed.trait);
            Assert.AreEqual("Katana", armed.traitName);
        }

        [Test]
        public void ARivalThatKillsAgain_Rises_ToGeneralOfItsBiome()
        {
            var meta = new MetaProgress();
            Rival r = Kill(meta, Ronin);
            Kill(meta, Ronin, r.id, 50);
            Assert.AreEqual(1, r.rank);
            Assert.AreEqual(150, r.ecos);
            Kill(meta, Ronin, r.id, 0);
            Assert.AreEqual(2, r.rank);
            Kill(meta, Ronin, r.id, 0);
            Assert.IsTrue(r.General);
            StringAssert.Contains("General", r.name);
            Assert.AreEqual(2, Rivals.Guards(r));

            // The seat is taken: another three-star captain of the same biome stays a captain.
            Rival other = Kill(meta, EnemyDefs.Get("lanceiro"), seed: 5);
            other.rank = 2;
            Rivals.Promote(meta, other);
            Assert.IsFalse(other.General);
        }

        [Test]
        public void Vengeance_ReturnsTheEcosWithHalfAgain_AndUnsettlesTheGeneral()
        {
            var meta = new MetaProgress();
            Rival general = Kill(meta, Ronin, seed: 3);
            general.rank = Rivals.GeneralRank;
            Rival captain = Kill(meta, EnemyDefs.Get("lanceiro"), ecos: 80, seed: 4);
            Vengeance v = Rivals.Defeat(meta, captain.id);
            Assert.AreEqual(120, v.ecos);
            Assert.AreEqual(EnemyDefs.Get("lanceiro").techniqueId, v.technique);
            Assert.IsNull(Rivals.Find(meta, captain.id));
            Assert.AreEqual(1, meta.fallenRivals.Count);
            Assert.IsTrue(general.furious);
            Assert.AreEqual(1, Rivals.Guards(general));
        }

        [Test]
        public void AFullRoster_ForgetsItsWeakestCaptain()
        {
            var meta = new MetaProgress();
            for (int i = 0; i < Rivals.MaxCaptains; i++) Kill(meta, Ronin, seed: (ulong)(100 + i)).kills = 2 + i;
            meta.rivals[3].kills = 1;
            string weakest = meta.rivals[3].id;
            Kill(meta, Ronin, seed: 999);
            Assert.AreEqual(Rivals.MaxCaptains, meta.rivals.Count);
            Assert.IsNull(Rivals.Find(meta, weakest));
        }

        [Test]
        public void Rivals_WaitInLairsOnTheRouteMap()
        {
            var meta = new MetaProgress();
            Rival r = Kill(meta, Ronin);
            try
            {
                Rivals.Roster.Clear();
                Assert.IsNull(Rivals.LairAt(7, 6, 0, out _), "no rivals, no lairs");
                Rivals.Roster.Add(r);
                RouteNode lair = null;
                ulong seed = 0;
                for (ulong s = 1; s < 400 && lair == null; s++)
                {
                    for (int depth = 3; depth < 20 && lair == null; depth++)
                    {
                        foreach (RouteNode n in RouteMap.Layer(s, depth))
                        {
                            if (n.kind != RoomKind.RivalLair) continue;
                            lair = n;
                            seed = s;
                        }
                    }
                }
                Assert.IsNotNull(lair, "some door hides the rival");
                Assert.AreEqual(r.id, lair.rivalId);
                RoomDef room = RoomGenerator.Generate(seed, lair.depth, lair);
                Assert.AreEqual(RoomKind.RivalLair, room.kind);
                Assert.IsTrue(room.standoff);
                Assert.AreEqual(r.id, room.waves[0][0].rivalId);
                Assert.IsTrue(room.waves[0][0].elite);
                StringAssert.Contains(r.surname, room.name);
            }
            finally
            {
                Rivals.Roster.Clear();
            }
        }

        [Test]
        public void Rivals_RoundTrip_AndOpenWithTheSeventhRun()
        {
            var meta = new MetaProgress();
            Kill(meta, Ronin);
            meta.readings.Add(ReadingMemory.Of(new AdaptiveTraits { delay = 0.5f }));
            MetaProgress copy = MetaProgress.FromJson(meta.ToJson());
            Assert.AreEqual(1, copy.rivals.Count);
            Assert.AreEqual(meta.rivals[0].name, copy.rivals[0].name);
            Assert.AreEqual(1, copy.readings.Count);
            Assert.IsFalse(Unlocks.Open(5, Unlockable.Rivals));
            Assert.IsTrue(Unlocks.Open(6, Unlockable.Rivals));
            var oath = new MetaProgress();
            oath.SetOath(Oaths.EternalRival, 1);
            Assert.AreEqual(1, OathSet.From(oath).Level(Oaths.EternalRival), "the Eternal Rival can be sworn now");
        }

        // ── The adaptive AI 2.0 ──

        [Test]
        public void NewReadings_FillTheirMeter_AndCanBeForgotten()
        {
            var profile = new PlayerProfile();
            for (int i = 0; i < 60 * 10; i++)
            {
                profile.Tick(1f / 60f, true);
                if (i % 30 == 0)
                {
                    profile.OnBackAttack();
                    profile.OnTool();
                }
            }
            AdaptiveTraits t = profile.Traits(1f);
            Assert.That(t.Level(ReadingId.Flank), Is.InRange(1, 3));
            Assert.That(t.Level(ReadingId.Tool), Is.InRange(1, 3));
            Assert.Contains(ReadingId.Flank, t.Active());
            profile.Forget(ReadingId.Flank);
            Assert.AreEqual(0, profile.Traits(1f).Level(ReadingId.Flank));
            Assert.AreEqual(0, new AdaptiveTraits { strength = 1f, swap = 0.2f }.Level(ReadingId.Swap));
            Assert.AreEqual(3, new AdaptiveTraits { strength = 1f, swap = 0.9f }.Level(ReadingId.Swap));
        }

        [Test]
        public void TheShuraRemembers_AFloorFromTheLastRuns()
        {
            var list = new List<ReadingMemory>
            {
                ReadingMemory.Of(new AdaptiveTraits { delay = 1f, heal = 0.5f }),
                ReadingMemory.Of(new AdaptiveTraits { delay = 0.5f }),
            };
            AdaptiveTraits avg = ReadingMemory.Average(list);
            Assert.AreEqual(0.75f, avg.delay, 1e-4f);
            Assert.AreEqual(0.25f, avg.heal, 1e-4f);
            var profile = new PlayerProfile { Baseline = avg };
            Assert.AreEqual(0.6f, profile.Traits(1f).delay, 1e-4f, "80% of the memory, before any fight");
        }
    }
}
