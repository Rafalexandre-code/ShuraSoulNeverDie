using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>The road of the design-doc expansion (§6): route map, room kinds, biomes, weather, Kalpas, events, the purse.</summary>
    public class RoadTests
    {
        static RunState NewRun(ulong seed = 9) => new RunState(seed, new FlowTuning());

        // ── Route map ────────────────────────────────────────────────────────────────

        [Test]
        public void RouteMap_IsDeterministic()
        {
            for (int d = 0; d < 40; d++)
            {
                List<RouteNode> a = RouteMap.Layer(123, d), b = RouteMap.Layer(123, d);
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].kind, b[i].kind);
                    Assert.AreEqual(a[i].hint, b[i].hint);
                    Assert.AreEqual(a[i].column, b[i].column);
                }
            }
        }

        [Test]
        public void RouteMap_EveryDoorLeadsOn_AndEveryDoorCanBeReached()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                for (int d = 0; d < 60; d++)
                {
                    List<RouteNode> layer = RouteMap.Layer(seed, d);
                    Assert.That(layer.Count, Is.InRange(1, 3));
                    var reached = new HashSet<int>();
                    foreach (RouteNode n in layer)
                    {
                        List<RouteNode> next = RouteMap.Next(seed, n);
                        Assert.Greater(next.Count, 0, "a door with no way on");
                        foreach (RouteNode m in next) reached.Add(m.index);
                    }
                    Assert.AreEqual(RouteMap.Layer(seed, d + 1).Count, reached.Count, "an unreachable door at depth " + (d + 1));
                }
            }
        }

        [Test]
        public void RouteMap_GuardiansAndGatesStandAlone_AndEveryLayerHasAFight()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                for (int d = 1; d < 80; d++)
                {
                    List<RouteNode> layer = RouteMap.Layer(seed, d);
                    if (RoomGenerator.IsGuardianDepth(d))
                    {
                        Assert.AreEqual(1, layer.Count);
                        Assert.AreEqual(RoomKind.Guardian, layer[0].kind);
                        continue;
                    }
                    if (RouteMap.IsGateDepth(d))
                    {
                        Assert.AreEqual(RoomKind.Gate, layer[0].kind);
                        continue;
                    }
                    bool fight = false;
                    foreach (RouteNode n in layer) fight |= !n.IsEncounter;
                    Assert.IsTrue(fight, "depth " + d + " has no fight");
                }
            }
        }

        [Test]
        public void RouteMap_OverManyRuns_EveryKindOfDoorAppears()
        {
            var seen = new HashSet<RoomKind>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                for (int d = 1; d < 30; d++)
                {
                    foreach (RouteNode n in RouteMap.Layer(seed, d)) seen.Add(n.kind);
                }
            }
            foreach (RoomKind k in System.Enum.GetValues(typeof(RoomKind))) Assert.IsTrue(seen.Contains(k), "never seen: " + k);
        }

        // ── Rooms behind the doors ───────────────────────────────────────────────────

        [Test]
        public void Rooms_FollowTheirDoor()
        {
            for (int d = 3; d < 24; d++)
            {
                if (RoomGenerator.IsGuardianDepth(d)) continue;
                var duel = RoomGenerator.Generate(7, d, new RouteNode { depth = d, kind = RoomKind.Duel, hint = RewardHint.Technique });
                Assert.AreEqual(1, duel.waves.Length);
                Assert.AreEqual(1, duel.waves[0].Length, "a duel is one against one");
                Assert.IsTrue(duel.standoff, "a duel opens with a standoff");

                var shrine = RoomGenerator.Generate(7, d, new RouteNode { depth = d, kind = RoomKind.Shrine });
                Assert.AreEqual(0, shrine.waves.Length);
                Assert.IsTrue(shrine.IsEncounter);
                Assert.IsFalse(shrine.corrupted);

                if (d < 4) continue;
                var dispute = RoomGenerator.Generate(7, d, new RouteNode { depth = d, index = 1, kind = RoomKind.Dispute });
                bool yokai = false, human = false;
                foreach (EnemySpawn[] wave in dispute.waves)
                {
                    if (wave.Length < 2) continue;
                    foreach (EnemySpawn s in wave)
                    {
                        if (RoomGenerator.IsYokai(s.id)) yokai = true;
                        else human = true;
                    }
                }
                Assert.IsTrue(yokai && human, "a dispute brings two factions (depth " + d + ")");
            }
        }

        // ── Biomes, hour and weather ─────────────────────────────────────────────────

        [Test]
        public void Biomes_TenRules_YomiOnlyFromForty()
        {
            Assert.AreEqual(10, Biomes.All.Length);
            var rules = new HashSet<BiomeRule>();
            foreach (Biome b in Biomes.All) rules.Add(b.rule);
            Assert.AreEqual(10, rules.Count);
            Assert.AreEqual(BiomeRule.Wild, Biomes.ForDepth(5).rule, "the Wild Wood comes early");
            for (int d = 0; d < 40; d++) Assert.AreNotEqual(BiomeRule.Yomi, Biomes.ForDepth(d).rule, "Yomi before 40 at " + d);
            Assert.AreEqual(BiomeRule.Yomi, Biomes.ForDepth(45).rule);
        }

        [Test]
        public void Weather_IsSeeded_AndTheFullMoonOnlyRisesAtNight()
        {
            int nonClear = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                for (int d = 0; d < 45; d++)
                {
                    RouteNode node = RouteMap.Default(seed, d);
                    RoomDef a = RoomGenerator.Generate(seed, d, node), b = RoomGenerator.Generate(seed, d, node);
                    Assert.AreEqual(a.weather, b.weather);
                    Assert.AreEqual(Biomes.TimeAt(d), a.time);
                    if (a.weather == Weather.FullMoon) Assert.AreEqual(TimeOfDay.Night, a.time);
                    if (a.weather != Weather.Clear) nonClear++;
                }
            }
            Assert.Greater(nonClear, 100);
        }

        [Test]
        public void TimeOfDay_AdvancesEveryThreeRooms()
        {
            Assert.AreEqual(TimeOfDay.Morning, Biomes.TimeAt(0));
            Assert.AreEqual(TimeOfDay.Morning, Biomes.TimeAt(2));
            Assert.AreEqual(TimeOfDay.Afternoon, Biomes.TimeAt(3));
            Assert.AreEqual(TimeOfDay.Night, Biomes.TimeAt(9));
            Assert.AreEqual(TimeOfDay.Morning, Biomes.TimeAt(15));
        }

        // ── Kalpas ───────────────────────────────────────────────────────────────────

        [Test]
        public void Kalpa_NumbersClimbSlowerPastTheFirstCycle()
        {
            Assert.AreEqual(10f, DifficultyRules.ScalingDepth(10), 1e-4f);
            Assert.AreEqual(25f, DifficultyRules.ScalingDepth(25), 1e-4f);
            Assert.Less(DifficultyRules.ScalingDepth(50), 50f);
            Assert.Greater(DifficultyRules.EnemyHealthMult(50), DifficultyRules.EnemyHealthMult(49));
        }

        [Test]
        public void Kalpa_DescendAddsRules_TwoFromKalpaFive()
        {
            RunState run = NewRun();
            var rng = new SeededRandom(3);
            KalpaRules.Descend(run, KalpaRule.EnemySwap, rng);
            Assert.AreEqual(2, run.Kalpa);
            Assert.IsTrue(run.HasRule(KalpaRule.EnemySwap));
            KalpaRules.Descend(run, KalpaRule.EnemyFlow, rng);
            KalpaRules.Descend(run, KalpaRule.SwiftBlades, rng);
            Assert.AreEqual(3, run.KalpaRules.Count);
            KalpaRules.Descend(run, KalpaRule.EnemyAbsorb, rng);
            Assert.AreEqual(5, run.Kalpa);
            Assert.AreEqual(5, run.KalpaRules.Count, "Kalpa V brings two rules");
            Assert.LessOrEqual(KalpaRules.Offer(run, rng).Count, 1);
        }

        // ── The party's purse, curses and rewards ────────────────────────────────────

        [Test]
        public void Rewards_DoorHintShapesTheChoices()
        {
            RunState run = NewRun(41);
            List<RewardDef> relics = run.Main.RollChoices(3, 5, RewardHint.Relic);
            foreach (RewardDef r in relics) Assert.AreNotEqual(RewardKind.Blessing, r.Kind);
            List<RewardDef> items = run.Main.RollChoices(3, 5, RewardHint.Item);
            bool item = false;
            foreach (RewardDef r in items) item |= r is Item;
            Assert.IsTrue(item);
        }

        [Test]
        public void Grant_PutsItemsInSlots_NotInTheOwnedList()
        {
            RunState run = NewRun();
            Item it = ItemGenerator.RollAt(new SeededRandom(1), Rarity.Rare, ItemSlot.Ring);
            run.Main.Grant(it);
            Assert.AreSame(it, run.Main.Equipped[(int)ItemSlot.Ring]);
            Assert.IsFalse(run.Main.Owned.Contains(it));
        }

        [Test]
        public void Curses_WearOffRoomByRoom()
        {
            RunState run = NewRun();
            run.Main.ApplyCurse(RunCurse.Fragile, 2);
            Assert.Greater(run.Main.Modifiers.damageTakenMult, 1f);
            run.Main.TickCurse();
            Assert.IsTrue(run.Main.Cursed(RunCurse.Fragile));
            run.Main.TickCurse();
            Assert.IsFalse(run.Main.Cursed(RunCurse.Fragile));
            Assert.AreEqual(1f, run.Main.Modifiers.damageTakenMult, 1e-4f);
        }

        [Test]
        public void HonorDuel_SilencesTechniquesAndItems()
        {
            RunState run = NewRun();
            run.Main.Grant(ItemGenerator.RollAt(new SeededRandom(2), Rarity.Legendary, ItemSlot.Amulet));
            Assert.Greater(run.Main.Effects.Count, 0);
            run.Main.HonorBound = true;
            run.Main.RecomputeModifiers();
            Assert.AreEqual(0, run.Main.Effects.Count);
        }

        [Test]
        public void Items_RollAtKeepsRarityAndSlot_AndPricesRise()
        {
            var rng = new SeededRandom(5);
            for (int r = 0; r < Rarities.Count; r++)
            {
                Item it = ItemGenerator.RollAt(rng, (Rarity)r, ItemSlot.Bracer);
                Assert.AreEqual((Rarity)r, it.rarity);
                Assert.AreEqual(ItemSlot.Bracer, it.slot);
                if (r > 0) Assert.GreaterOrEqual(World.ItemPrice((Rarity)r), World.ItemPrice((Rarity)(r - 1)));
            }
        }

        [Test]
        public void Choose_ReplacesTheRoomWithTheOneBehindTheDoor()
        {
            RunState run = NewRun(17);
            var node = new RouteNode { depth = 3, index = 2, column = 2, kind = RoomKind.Merchant };
            RoomDef room = run.Choose(node);
            Assert.AreSame(room, run.GetRoom(3));
            Assert.AreEqual(RoomKind.Merchant, run.GetRoom(3).kind);
            Assert.AreSame(node, run.CurrentNode);
        }

        [Test]
        public void PartyScaling_KeepsWhatTheDoorDecided()
        {
            RoomDef merchant = RoomGenerator.Generate(5, 6, new RouteNode { depth = 6, kind = RoomKind.Merchant });
            merchant.weather = Weather.Rain;
            RoomDef scaled = PartyScaling.Scale(merchant, 3);
            Assert.AreEqual(RoomKind.Merchant, scaled.kind);
            Assert.AreEqual(Weather.Rain, scaled.weather);
            Assert.IsTrue(scaled.IsEncounter);

            RoomDef duel = RoomGenerator.Generate(5, 7, new RouteNode { depth = 7, kind = RoomKind.Duel });
            RoomDef scaledDuel = PartyScaling.Scale(duel, 4);
            Assert.IsTrue(scaledDuel.standoff);
            Assert.AreEqual(1, scaledDuel.waves[0].Length, "a duel stays one against one");
        }

        // ── Events ───────────────────────────────────────────────────────────────────

        [Test]
        public void Events_EightWithWords_AndTheKitsuneExists()
        {
            Assert.AreEqual(8, RunEvents.Count);
            for (int i = 0; i < RunEvents.Count; i++)
            {
                Assert.IsNotEmpty(RunEvents.Title(i));
                Assert.IsNotEmpty(RunEvents.Text(i));
            }
            Assert.IsTrue(EnemyDefs.TryGet("kitsune", out EnemyDef fox));
            Assert.IsFalse(System.Array.Exists(EnemyDefs.All, d => d == fox), "the kitsune is never placed by rooms");
        }
    }
}
