using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>The PvP duel (§9.6): its rules, and rollback netcode agreeing under a bad network.</summary>
    public class DuelTests
    {
        static DuelSide Side(string weapon = "katana", int kamae = 1) => DuelSide.From("x", WeaponDefs.Get(weapon), kamae, 0);

        static ushort Move(int mx) => DuelInput.Pack(mx, 0, false, false, false, false, false);

        /// <summary>Walks the two duelists into reach (A from the left, B from the right) and returns the state.</summary>
        static DuelState Close(DuelSide a, DuelSide b)
        {
            DuelState s = DuelSim.Start();
            for (int i = 0; i < 400 && (s.pause > 0 || s.b.x - s.a.x > 1300); i++) DuelSim.Step(ref s, Move(1), Move(-1), a, b);
            return s;
        }

        [Test]
        public void APerfectParry_StaggersTheAttacker_AndCracksTheirPosture()
        {
            DuelSide a = Side(), b = Side();
            DuelState s = Close(a, b);
            DuelSim.Step(ref s, DuelInput.Pack(0, 0, true, false, false, false, false), 0, a, b);
            Assert.AreEqual(DuelAct.Windup, s.a.act);
            int impact = a.combo[0].windup;
            for (int i = 1; i < impact - 2; i++) DuelSim.Step(ref s, 0, 0, a, b);
            // Parry a couple of frames before the blade arrives.
            DuelSim.Step(ref s, 0, DuelInput.Pack(0, 0, false, false, false, true, true), a, b);
            for (int i = 0; i < 6; i++) DuelSim.Step(ref s, 0, DuelInput.Pack(0, 0, false, false, false, false, true), a, b);
            Assert.AreEqual(DuelAct.Stagger, s.a.act);
            Assert.AreEqual(DuelSim.PerfectParryPosture, s.a.posture, 10);
            Assert.AreEqual(DuelSim.Health, s.b.health, "a perfect parry costs nothing");
        }

        [Test]
        public void TheRedBlow_CannotBeParried_AndBreaksAGuard()
        {
            DuelSide a = Side("montante"), b = Side();
            DuelState s = Close(a, b);
            DuelSim.Step(ref s, DuelInput.Pack(0, 0, false, true, false, false, false), 0, a, b);
            Assert.IsTrue(s.a.red);
            int impact = a.red.windup;
            for (int i = 1; i < impact - 2; i++) DuelSim.Step(ref s, 0, 0, a, b);
            DuelSim.Step(ref s, 0, DuelInput.Pack(0, 0, false, false, false, true, true), a, b);
            for (int i = 0; i < 6; i++) DuelSim.Step(ref s, 0, DuelInput.Pack(0, 0, false, false, false, false, true), a, b);
            Assert.AreNotEqual(DuelAct.Stagger, s.a.act, "no parry for the red blow");
            Assert.Greater(s.b.posture, 300, "a guard pays dearly");
        }

        [Test]
        public void ABrokenPosture_InvitesTheFinisher_AndTwoRoundsWin()
        {
            DuelSide a = Side(), b = Side();
            DuelState s = Close(a, b);
            s.b.posture = DuelSim.PostureMax - 1;
            // Hit until B breaks, then once more.
            for (int i = 0; i < 400 && s.lastRound < 0; i++)
            {
                bool swing = s.a.act == DuelAct.Free;
                DuelSim.Step(ref s, DuelInput.Pack(0, 0, swing, false, false, false, false), 0, a, b);
            }
            Assert.AreEqual(0, s.lastRound);
            Assert.AreEqual(1, s.a.rounds);
            Assert.AreEqual(-1, s.winner);
            for (int i = 0; i < 200 && s.pause > 0; i++) DuelSim.Step(ref s, 0, 0, a, b);
            Assert.AreEqual(DuelSim.Health, s.b.health, "a new round, whole again");
            Assert.AreEqual(2, s.round);
        }

        [Test]
        public void TheSameInputs_GiveTheSameDuel()
        {
            DuelSide a = Side("naginata", 0), b = Side("kanabo", 2);
            uint Run()
            {
                var rng = new SeededRandom(99);
                DuelState s = DuelSim.Start();
                for (int i = 0; i < 3000; i++) DuelSim.Step(ref s, RandomInput(rng), RandomInput(rng), a, b);
                return DuelSim.Hash(s);
            }
            Assert.AreEqual(Run(), Run());
        }

        static ushort RandomInput(SeededRandom rng)
        {
            int mx = rng.Range(-1, 2), my = rng.Range(-1, 2);
            float r = rng.NextFloat();
            return DuelInput.Pack(mx, my, r < 0.06f, r > 0.98f, r > 0.94f && r < 0.96f, r > 0.9f && r < 0.94f, rng.NextFloat() < 0.3f);
        }

        /// <summary>
        /// Two machines over a bad network (0–9 frames of delay, messages out of order): both sides
        /// end on exactly the state of a reference duel played with the real inputs, with rollbacks
        /// along the way and no desync.
        /// </summary>
        [Test]
        public void Rollback_AgreesWithTheTruth_OverABadNetwork([Values(1, 7, 42)] int seed)
        {
            DuelSide sa = Side("katana", 1), sb = Side("montante", 2);
            var a = new DuelRollback(sa, sb, 0);
            var b = new DuelRollback(sa, sb, 1);
            var net = new List<(int at, bool toB, bool hash, int frame, uint value)>();
            var rng = new SeededRandom((ulong)seed);
            var logA = new Dictionary<int, ushort>();
            var logB = new Dictionary<int, ushort>();
            int tick = 0;
            a.SendInput = (f, v) => { logA[f] = v; net.Add((tick + rng.Range(0, 10), true, false, f, v)); };
            b.SendInput = (f, v) => { logB[f] = v; net.Add((tick + rng.Range(0, 10), false, false, f, v)); };
            a.SendHash = (f, h) => net.Add((tick + rng.Range(0, 10), true, true, f, h));
            b.SendHash = (f, h) => net.Add((tick + rng.Range(0, 10), false, true, f, h));
            var inA = new SeededRandom((ulong)seed * 3 + 1);
            var inB = new SeededRandom((ulong)seed * 5 + 2);
            const int Frames = 1800;

            void Deliver(bool all)
            {
                // Messages arrive when their time comes, shuffled among those due together.
                for (int i = net.Count - 1; i >= 0; i--)
                {
                    var m = net[i];
                    if (!all && m.at > tick) continue;
                    net.RemoveAt(i);
                    DuelRollback to = m.toB ? b : a;
                    if (m.hash) to.ReceiveHash(m.frame, m.value);
                    else to.Receive(m.frame, (ushort)m.value);
                }
            }

            for (tick = 0; tick < Frames * 3 && (a.Frame < Frames || b.Frame < Frames); tick++)
            {
                Deliver(false);
                if (a.Frame < Frames) a.Advance(RandomInput(inA));
                if (b.Frame < Frames) b.Advance(RandomInput(inB));
            }
            Deliver(true);
            a.Settle();
            b.Settle();
            Assert.AreEqual(Frames, a.Frame);
            Assert.AreEqual(Frames, b.Frame);
            Assert.IsFalse(a.Desynced || b.Desynced, "desync at " + a.DesyncFrame + "/" + b.DesyncFrame);
            Assert.Greater(a.Rollbacks + b.Rollbacks, 0, "the network was bad enough to need rollbacks");

            // The truth: the duel played in lockstep with what each side really pressed.
            DuelState truth = DuelSim.Start();
            for (int f = 0; f < Frames; f++)
            {
                ushort ia = logA.TryGetValue(f, out ushort va) ? va : (ushort)0;
                ushort ib = logB.TryGetValue(f, out ushort vb) ? vb : (ushort)0;
                DuelSim.Step(ref truth, ia, ib, sa, sb);
            }
            Assert.AreEqual(DuelSim.Hash(truth), DuelSim.Hash(a.State), "side A");
            Assert.AreEqual(DuelSim.Hash(truth), DuelSim.Hash(b.State), "side B");
        }

        [Test]
        public void TheLadder_MovesByElo_AndANewMonthStartsAnew()
        {
            Assert.AreEqual(16, Pvp.Change(1000, 1000, 1f));
            Assert.AreEqual(-16, Pvp.Change(1000, 1000, 0f));
            Assert.AreEqual(0, Pvp.Change(1000, 1000, 0.5f));
            var m = new MetaProgress();
            var nov = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
            Pvp.Record(m, nov, "Haru", 1000, 1, "katana", "kanabo");
            Assert.AreEqual(1016, m.pvp.rating);
            Assert.AreEqual(1, m.pvp.wins);
            Assert.AreEqual("Novembro de 2026", Pvp.SeasonName(m.pvp.season));
            PvpRecord dec = Pvp.Current(m, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(Pvp.StartRating, dec.rating);
            Assert.AreEqual(0, dec.wins);
            StringAssert.StartsWith("2026-11: 1016", dec.past[0]);
        }
    }
}
