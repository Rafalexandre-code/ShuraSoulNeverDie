using System;
using UnityEngine;

namespace Shura
{
    /// <summary>The PvP duel and the courtyard on this machine (§9.6, §9.7).</summary>
    public sealed partial class Game
    {
        public DuelSession Duel { get; private set; }
        public DuelResult LastDuel { get; private set; }
        public int DuelsFinished { get; private set; }
        Mode duelReturn = Mode.Lobby;
        bool duelReported;

        /// <summary>The host announced a duel this machine plays in.</summary>
        public void BeginDuel(DuelStart start)
        {
            if (start == null || Duel != null) return;
            int myRating = Pvp.Current(Meta, DateTime.UtcNow).rating;
            bool guest = Remote.Joined;
            Action<string> direct = guest ? (Action<string>)Remote.SendRaw : text => Net.SendDuel(start.TheirId, text);
            // Tests: hold every message back for a while, like a long road between the two machines.
            Action<string> send = Launch.Lag > 0 ? text => lagged.Enqueue((Time.realtimeSinceStartup + Launch.Lag / 1000f, text, direct)) : direct;
            Duel = new DuelSession(start, send, myRating);
            duelReturn = guest ? Mode.Remote : Mode.Lobby;
            duelReported = false;
            LastDuel = null;
            Controls.Buffer.Clear();
            UI.CourtMenuOpen = false;
            SetMode(Mode.Duel);
            Debug.Log("[Shura][Duelo] começa contra " + start.TheirName + " (lado " + (start.me == 0 ? "A" : "B") + ")");
        }

        readonly System.Collections.Generic.Queue<(float at, string text, Action<string> send)> lagged =
            new System.Collections.Generic.Queue<(float, string, Action<string>)>();

        void TickDuel()
        {
            while (lagged.Count > 0 && lagged.Peek().at <= Time.realtimeSinceStartup)
            {
                var m = lagged.Dequeue();
                m.send(m.text);
            }
            if (Duel == null)
            {
                SetMode(duelReturn);
                return;
            }
            Duel.Update(Time.unscaledDeltaTime, SampleDuelInput, () => Controls.Buffer.Age());
            if (Duel.Outcome != DuelOutcome.None && !duelReported) ReportDuel();
            bool skip = !Launch.Automated && Duel.SinceEnd > 2f && (Controls.Submit || Controls.Cancel);
            if (Duel.Outcome != DuelOutcome.None && (Duel.SinceEnd > 6f || skip)) EndDuel();
        }

        ushort SampleDuelInput(DuelState s, int me)
        {
            if (Launch.Automated) return DuelBot.Think(s, me, Duel.Mine, Duel.Theirs, me * 13 + 7);
            Vector2 m = Controls.Move;
            int mx = m.x > 0.35f ? 1 : m.x < -0.35f ? -1 : 0;
            int my = m.y > 0.35f ? 1 : m.y < -0.35f ? -1 : 0;
            return DuelInput.Pack(mx, my,
                Controls.Buffer.Consume(BufferedAction.Attack),
                Controls.Buffer.Consume(BufferedAction.Swap),
                Controls.Buffer.Consume(BufferedAction.Dodge),
                Controls.Buffer.Consume(BufferedAction.Parry),
                Controls.ParryHeld);
        }

        /// <summary>The duel is decided: the ladder moves (never on a desync or a lost connection) and the courtyard hears of it.</summary>
        void ReportDuel()
        {
            duelReported = true;
            DuelStart st = Duel.Start;
            DuelOutcome o = Duel.Outcome;
            string line = "";
            if (o == DuelOutcome.Won || o == DuelOutcome.Lost || o == DuelOutcome.Draw)
            {
                int outcome = o == DuelOutcome.Won ? 1 : o == DuelOutcome.Draw ? 2 : 0;
                LastDuel = Pvp.Record(Meta, DateTime.UtcNow, st.TheirName, Duel.TheirRating, outcome, Duel.Mine.weaponId, Duel.Theirs.weaponId);
                SaveMeta();
                DuelState s = Duel.Net.State;
                int myRounds = st.me == 0 ? s.a.rounds : s.b.rounds, theirRounds = st.me == 0 ? s.b.rounds : s.a.rounds;
                // One line for everyone, told by the winner (or by A in a draw).
                if (o == DuelOutcome.Won) line = string.Format(Strings.DuelLineWon, Settings.playerName, st.TheirName, myRounds, theirRounds);
                else if (o == DuelOutcome.Draw && st.me == 0) line = string.Format(Strings.DuelLineDraw, st.nameA, st.nameB);
            }
            Debug.Log("[Shura][Duelo] fim: " + o + " frames=" + Duel.Net.Frame + " rollbacks=" + Duel.Net.Rollbacks + " (" + Duel.Net.RolledBackFrames +
                      " frames) esperas=" + Duel.Net.Stalls + " dessincronia=" + Duel.Net.Desynced + " tempo=" + Duel.Elapsed.ToString("0.0") + "s ranking=" + (LastDuel != null ? LastDuel.ratingAfter.ToString() : "-"));
            if (Remote.Joined)
                Remote.SendRaw("{\"t\":\"dend\",\"to\":" + st.TheirId + ",\"a\":" + Json.Quote(line) + "}");
            else Net.DuelEnded(0, st.TheirId, line);
        }

        void EndDuel()
        {
            Duel = null;
            DuelsFinished++;
            SetMode(Net.Active || Remote.Joined ? duelReturn : Mode.MainMenu);
        }

        /// <summary>The opponent left the room mid-duel.</summary>
        public void OpponentLeftDuel(int id) => Duel?.OpponentLeft(id);

        /// <summary>The host's own walk in the courtyard this frame (nothing while a menu is open).</summary>
        CourtInput HostCourtInput()
        {
            var none = new CourtInput { kamae = -1 };
            if (CurrentMode != Mode.Lobby || UI.CourtMenuOpen) return none;
            if (Launch.Automated) return BotCourtInput(Net.Court.Get(0));
            return new CourtInput
            {
                move = Controls.Move,
                attack = Controls.Buffer.Consume(BufferedAction.Attack),
                parry = Controls.Buffer.Consume(BufferedAction.Parry),
                interact = Net.HostInteractEdge(Controls.InteractHeld),
                kamae = Controls.TakeKamae(),
            };
        }

        float botCourtTimer;

        /// <summary>Automated duel test: walk into the circle and ask for a duel.</summary>
        CourtInput BotCourtInput(Courtyard.Walker me)
        {
            var i = new CourtInput { kamae = -1 };
            if (me == null || !Launch.DuelTest || DuelsFinished > 0) return i;
            Vector2 to = Courtyard.DuelCircle + Vector2.left * 0.8f - me.pos;
            if (to.magnitude > 0.5f) i.move = to.normalized;
            botCourtTimer += Time.unscaledDeltaTime;
            if (Courtyard.In(me.pos, Courtyard.DuelCircle, Courtyard.CircleRadius) && !me.challenging && botCourtTimer > 1f)
            {
                botCourtTimer = 0f;
                i.interact = true;
            }
            return i;
        }
    }
}
