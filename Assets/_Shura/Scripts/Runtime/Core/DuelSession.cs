using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>The start of a duel as the host announced it, seen from one of the two duelists.</summary>
    public sealed class DuelStart
    {
        public int duel;
        public int idA, idB;
        public string nameA = "?", nameB = "?", weaponA = "katana", weaponB = "katana";
        public int artA, artB, kamaeA = 1, kamaeB = 1;
        /// <summary>0 when this machine is A, 1 when B.</summary>
        public int me;

        public int MyId => me == 0 ? idA : idB;
        public int TheirId => me == 0 ? idB : idA;
        public string TheirName => me == 0 ? nameB : nameA;

        public static DuelStart Parse(Dictionary<string, object> m, int myId)
        {
            if (m == null) return null;
            var s = new DuelStart
            {
                duel = MiniJson.I(m, "d"),
                idA = MiniJson.I(m, "a"), nameA = MiniJson.S(m, "an"), artA = MiniJson.I(m, "aa"), weaponA = MiniJson.S(m, "aw", "katana"), kamaeA = MiniJson.I(m, "ak", 1),
                idB = MiniJson.I(m, "b"), nameB = MiniJson.S(m, "bn"), artB = MiniJson.I(m, "ba"), weaponB = MiniJson.S(m, "bw", "katana"), kamaeB = MiniJson.I(m, "bk", 1),
            };
            s.me = myId == s.idA ? 0 : 1;
            return s;
        }
    }

    /// <summary>
    /// One PvP duel on this machine: the rollback session, the messages to and from the
    /// opponent (through the host), the clocks kept together, and how it ended.
    /// </summary>
    public sealed class DuelSession
    {
        public const float SilenceLimit = 6f;

        public readonly DuelStart Start;
        public readonly DuelSide A, B;
        public readonly DuelRollback Net;
        readonly Action<string> send;
        readonly StringBuilder sb = new StringBuilder(128);
        float acc;
        int skip;
        public float Silence { get; private set; }
        public float Elapsed { get; private set; }
        public int TheirRating { get; private set; } = -1;
        /// <summary>The result, once known (None while the duel lasts).</summary>
        public DuelOutcome Outcome { get; private set; } = DuelOutcome.None;
        public float SinceEnd { get; private set; }

        public DuelSession(DuelStart start, Action<string> sendText, int myRating)
        {
            Start = start;
            send = sendText;
            A = DuelSide.From(start.nameA, WeaponDefs.Get(start.weaponA), start.kamaeA, start.artA);
            B = DuelSide.From(start.nameB, WeaponDefs.Get(start.weaponB), start.kamaeB, start.artB);
            Net = new DuelRollback(A, B, start.me)
            {
                SendInput = (f, v) => Send("di", f, v, 0),
                SendHash = (f, h) => Send("dc", f, 0, h),
            };
            sb.Clear();
            sb.Append("{\"t\":\"dh\",\"d\":").Int(start.duel).Append(",\"to\":").Int(start.TheirId).Append(",\"rt\":").Int(myRating).Append('}');
            send(sb.ToString());
        }

        public DuelSide Mine => Start.me == 0 ? A : B;
        public DuelSide Theirs => Start.me == 0 ? B : A;

        void Send(string type, int frame, int input, uint hash)
        {
            sb.Clear();
            sb.Append("{\"t\":\"").Append(type).Append("\",\"d\":").Int(Start.duel).Append(",\"to\":").Int(Start.TheirId)
              .Append(",\"f\":").Int(frame).Append(",\"v\":").Int(input).Append(",\"g\":").Int(Net.Advantage);
            if (hash != 0) sb.Append(",\"h\":").Append(hash.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            send(sb.ToString());
        }

        /// <summary>A message for this duel arrived (input, fingerprint or greeting).</summary>
        public void Receive(int from, string type, int duel, int frame, int input, long hash, int rating, int advantage = 0)
        {
            if (duel != Start.duel || from != Start.TheirId) return;
            Silence = 0f;
            if (type == "di") Net.RemoteAdvantage = advantage;
            switch (type)
            {
                case "di":
                    Net.Receive(frame, (ushort)input);
                    break;
                case "dc":
                    Net.ReceiveHash(frame, (uint)hash);
                    break;
                case "dh":
                    TheirRating = rating;
                    break;
            }
        }

        /// <summary>A guest hears its duel through the host: the sender is always the opponent.</summary>
        public void ReceiveFromHost(string type, int duel, int frame, int input, long hash, int rating, int advantage) =>
            Receive(Start.TheirId, type, duel, frame, input, hash, rating, advantage);

        /// <summary>The opponent left the room.</summary>
        public void OpponentLeft(int id)
        {
            if (id == Start.TheirId && Outcome == DuelOutcome.None) Outcome = DuelOutcome.Disconnected;
        }

        /// <summary>One rendered frame: as many 60 Hz frames as are due, one input sampled for each.</summary>
        public void Update(float dt, Func<DuelState, int, ushort> sample, Action afterFrame)
        {
            Elapsed += dt;
            if (Outcome != DuelOutcome.None)
            {
                SinceEnd += dt;
                return;
            }
            Silence += dt;
            if (Silence > SilenceLimit)
            {
                Outcome = DuelOutcome.Disconnected;
                return;
            }
            acc = Mathf.Min(acc + dt, SimClock.Dt * 6f);
            int steps = 0;
            while (acc >= SimClock.Dt && steps < 4)
            {
                acc -= SimClock.Dt;
                steps++;
                // Ahead of the opponent: wait a frame now and then, so neither side rolls back for ever.
                if (Net.ShouldWait && ++skip % 3 == 0) continue;
                if (!Net.Advance(sample(Net.State, Start.me))) continue;
                afterFrame?.Invoke();
            }
            if (Net.Desynced) Outcome = DuelOutcome.Desync;
            else if (Net.Finished)
            {
                int w = Net.State.winner;
                Outcome = w == 2 ? DuelOutcome.Draw : w == Start.me ? DuelOutcome.Won : DuelOutcome.Lost;
            }
        }
    }

    public enum DuelOutcome
    {
        None,
        Won,
        Lost,
        Draw,
        /// <summary>The opponent stopped answering: no result, no rating.</summary>
        Disconnected,
        /// <summary>The two machines disagreed: no result, no rating.</summary>
        Desync,
    }

    /// <summary>A sparring partner for automated tests: closes in, swings, parries what it sees coming.</summary>
    public static class DuelBot
    {
        public static ushort Think(DuelState s, int me, DuelSide mine, DuelSide theirs, int salt)
        {
            DuelFighter f = me == 0 ? s.a : s.b, o = me == 0 ? s.b : s.a;
            int dx = o.x - f.x, dy = o.y - f.y;
            long dist = DuelSim.ISqrt((long)dx * dx + (long)dy * dy);
            int mx = 0, my = 0;
            if (dist > 1500)
            {
                mx = Math.Sign(dx);
                my = Math.Abs(dy) > 400 ? Math.Sign(dy) : 0;
            }
            bool parryPress = false, held = false, dodge = false, attack = false;
            if (o.act == DuelAct.Windup)
            {
                DuelSim.Blow(o, theirs, out DuelBlow b);
                int left = b.windup - o.frame;
                if (o.red && left <= 6) dodge = true;
                else if (!o.red && left == 3 + (s.frame + salt) % 3) parryPress = true;
                held = !o.red;
            }
            else if (f.act == DuelAct.Free && dist < 1900 && (s.frame + salt) % 23 == 0) attack = true;
            if (o.act == DuelAct.Stagger || o.act == DuelAct.Broken) attack = f.act == DuelAct.Free;
            if (!attack && f.act == DuelAct.Free && dist < 1900 && (s.frame + salt) % 97 == 0) return DuelInput.Pack(0, 0, false, true, false, false, false);
            return DuelInput.Pack(mx, my, attack, false, dodge, parryPress, held || parryPress);
        }
    }
}
