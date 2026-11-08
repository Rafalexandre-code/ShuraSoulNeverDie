using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    public class OnlineAndArenaTests
    {
        static readonly ArenaShape[] Shapes = { ArenaShape.Hall, ArenaShape.Octagon, ArenaShape.Cross, ArenaShape.Pillars, ArenaShape.Courtyard };

        // ── Arenas ───────────────────────────────────────────────────────────────

        [Test]
        public void EveryShape_FloorIsOneConnectedRegion()
        {
            foreach (ArenaShape shape in Shapes)
            {
                var a = new ArenaLayout(26, 15, shape);
                int total = 0;
                Vector2Int start = new Vector2Int(-1, -1);
                for (int y = 0; y < a.H; y++)
                {
                    for (int x = 0; x < a.W; x++)
                    {
                        if (!a.IsFloor(x, y)) continue;
                        total++;
                        if (start.x < 0) start = new Vector2Int(x, y);
                    }
                }
                Assert.Greater(total, 150, shape + " has too little floor");

                var seen = new HashSet<Vector2Int> { start };
                var open = new Queue<Vector2Int>();
                open.Enqueue(start);
                while (open.Count > 0)
                {
                    Vector2Int c = open.Dequeue();
                    foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        Vector2Int n = c + d;
                        if (a.IsFloor(n.x, n.y) && seen.Add(n)) open.Enqueue(n);
                    }
                }
                Assert.AreEqual(total, seen.Count, shape + " has floor nobody can walk to");
            }
        }

        [Test]
        public void Border_IsAlwaysWall()
        {
            foreach (ArenaShape shape in Shapes)
            {
                var a = new ArenaLayout(24, 13, shape);
                for (int x = 0; x < a.W; x++)
                {
                    Assert.IsFalse(a.IsFloor(x, 0), shape + " bottom edge open");
                    Assert.IsFalse(a.IsFloor(x, a.H - 1), shape + " top edge open");
                }
                for (int y = 0; y < a.H; y++)
                {
                    Assert.IsFalse(a.IsFloor(0, y), shape + " left edge open");
                    Assert.IsFalse(a.IsFloor(a.W - 1, y), shape + " right edge open");
                }
            }
        }

        [Test]
        public void Resolve_PushesCirclesBackOntoFloor()
        {
            var rng = new SeededRandom(7);
            foreach (ArenaShape shape in Shapes)
            {
                var a = new ArenaLayout(26, 15, shape);
                for (int i = 0; i < 200; i++)
                {
                    // Anywhere around and beyond the arena, including deep inside walls.
                    var p = new Vector2(rng.Range(-20f, 20f), rng.Range(-14f, 14f));
                    Vector2 r = a.Resolve(p, 0.35f);
                    Assert.IsTrue(a.IsFloorAt(r), shape + " left " + p + " off the floor at " + r);
                }
            }
        }

        [Test]
        public void RandomFloorPoint_HasClearance()
        {
            var rng = new SeededRandom(11);
            foreach (ArenaShape shape in Shapes)
            {
                var a = new ArenaLayout(26, 15, shape);
                for (int i = 0; i < 50; i++) Assert.IsTrue(a.Fits(a.RandomFloorPoint(rng, 0.5f), 0.5f), shape.ToString());
            }
        }

        [Test]
        public void Traps_FollowDepthAndStayAwayFromSpawn()
        {
            var spawn = Vector2.zero;
            var shallow = new ArenaLayout(26, 15, ArenaShape.Hall);
            shallow.PlaceTraps(new SeededRandom(3), 0, spawn);
            Assert.AreEqual(0, shallow.Traps.Count, "intro arena has no traps");

            var mid = new ArenaLayout(26, 15, ArenaShape.Hall);
            mid.PlaceTraps(new SeededRandom(3), 1, spawn);
            Assert.IsTrue(mid.Traps.Exists(t => t.type == TrapType.Spikes));
            Assert.IsFalse(mid.Traps.Exists(t => t.type != TrapType.Spikes), "fire and blades unlock deeper");

            var deep = new ArenaLayout(28, 16, ArenaShape.Courtyard);
            deep.PlaceTraps(new SeededRandom(3), 6, spawn);
            Assert.IsTrue(deep.Traps.Exists(t => t.type == TrapType.Blade));
            foreach (TrapDef t in deep.Traps)
            {
                foreach (Vector2Int c in t.cells)
                {
                    Assert.Greater(Vector2.Distance(deep.CellCenter(c.x, c.y), spawn), 2.5f, t.type + " right on the spawn");
                }
            }
        }

        [Test]
        public void Layout_IsDeterministicPerSeed()
        {
            var a = new ArenaLayout(26, 15, ArenaShape.Pillars);
            var b = new ArenaLayout(26, 15, ArenaShape.Pillars);
            a.PlaceTraps(new SeededRandom(42), 5, Vector2.zero);
            b.PlaceTraps(new SeededRandom(42), 5, Vector2.zero);
            Assert.AreEqual(a.Traps.Count, b.Traps.Count);
            for (int i = 0; i < a.Traps.Count; i++)
            {
                Assert.AreEqual(a.Traps[i].type, b.Traps[i].type);
                Assert.AreEqual(a.Traps[i].offset, b.Traps[i].offset);
                CollectionAssert.AreEqual(a.Traps[i].cells, b.Traps[i].cells);
            }
        }

        [Test]
        public void Steer_WalksAroundStatuesAndPillarsToAnyFloorPoint()
        {
            var rng = new SeededRandom(5);
            foreach (ArenaShape shape in Shapes)
            {
                var a = new ArenaLayout(26, 15, shape);
                for (int trip = 0; trip < 30; trip++)
                {
                    Vector2 p = a.RandomFloorPoint(rng, 0.4f);
                    Vector2 goal = a.RandomFloorPoint(rng, 0.4f);
                    // Walk like a fighter does: steer, move, slide off walls.
                    int ticks = 0;
                    while (Vector2.Distance(p, goal) > 0.6f && ticks++ < 1200)
                    {
                        p = a.Resolve(p + a.Steer(p, goal, 0.4f) * 4f * SimClock.Dt, 0.4f);
                    }
                    Assert.Less(ticks, 1200, shape + ": stuck going from " + p + " to " + goal);
                }
            }
        }

        // ── Party scaling ────────────────────────────────────────────────────────

        [Test]
        public void PartyScaling_SoloIsUnchanged()
        {
            Assert.AreEqual(1f, PartyScaling.AreaMult(1));
            Assert.AreEqual(5, PartyScaling.WaveSize(5, 1));
            Assert.AreEqual(1f, PartyScaling.EnemyHealth(1));
            Assert.AreEqual(1f, PartyScaling.GuardianHealth(1));
            Assert.AreEqual(1, PartyScaling.SimultaneousAttackers(1));
            Assert.AreEqual(40, PartyScaling.AttackGap(40, 1));
        }

        [Test]
        public void PartyScaling_GrowsWithPlayersAndIsCapped()
        {
            for (int n = 2; n <= PartyScaling.MaxPlayers; n++)
            {
                Assert.Greater(PartyScaling.AreaMult(n), PartyScaling.AreaMult(n - 1));
                Assert.GreaterOrEqual(PartyScaling.WaveSize(4, n), PartyScaling.WaveSize(4, n - 1));
                Assert.LessOrEqual(PartyScaling.WaveSize(40, n), 3 + 2 * n, "waves stay readable");
                Assert.LessOrEqual(PartyScaling.AttackGap(40, n), PartyScaling.AttackGap(40, n - 1));
            }
            Assert.LessOrEqual(PartyScaling.SimultaneousAttackers(8), 6);
            Assert.AreEqual(PartyScaling.AreaMult(8), PartyScaling.AreaMult(50), "more than 8 counts as 8");
        }

        // ── WebSocket frames ─────────────────────────────────────────────────────

        static byte[] MaskedClientFrame(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            byte[] mask = { 0x12, 0x9A, 0x3C, 0x77 };
            var bytes = new List<byte> { 0x81 };
            if (payload.Length < 126) bytes.Add((byte)(0x80 | payload.Length));
            else { bytes.Add(0x80 | 126); bytes.Add((byte)(payload.Length >> 8)); bytes.Add((byte)payload.Length); }
            bytes.AddRange(mask);
            for (int i = 0; i < payload.Length; i++) bytes.Add((byte)(payload[i] ^ mask[i & 3]));
            return bytes.ToArray();
        }

        [Test]
        public void WebSocket_DecodesMaskedClientText()
        {
            const string shortMsg = "{\"t\":\"join\",\"name\":\"Ação\"}";
            Assert.IsTrue(WebServer.TryDecodeClientFrame(MaskedClientFrame(shortMsg), out string a));
            Assert.AreEqual(shortMsg, a);

            string longMsg = "{\"t\":\"in\",\"pad\":\"" + new string('x', 400) + "\"}";
            Assert.IsTrue(WebServer.TryDecodeClientFrame(MaskedClientFrame(longMsg), out string b));
            Assert.AreEqual(longMsg, b);
        }

        [Test]
        public void WebSocket_ServerFrameHeaderMatchesLength()
        {
            byte[] small = WebServer.Frame("hi");
            Assert.AreEqual(0x81, small[0]);
            Assert.AreEqual(2, small[1]);

            byte[] mid = WebServer.Frame(new string('a', 1000));
            Assert.AreEqual(126, mid[1]);
            Assert.AreEqual(1000, (mid[2] << 8) | mid[3]);
            Assert.AreEqual(4 + 1000, mid.Length);

            byte[] big = WebServer.Frame(new string('b', 70000));
            Assert.AreEqual(127, big[1]);
            Assert.AreEqual(10 + 70000, big.Length);
        }
    }
}
