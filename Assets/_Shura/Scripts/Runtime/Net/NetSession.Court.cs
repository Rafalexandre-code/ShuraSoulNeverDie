using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The host's courtyard (§9.7) and duel routing (§9.6): the courtyard runs here and goes to
    /// the guests as "court"; a duel's inputs ("di"), fingerprints ("dc") and greetings ("dh")
    /// pass through the host to whoever they are meant for — or stay, when the host duels.
    /// </summary>
    public sealed partial class NetSession
    {
        public readonly Courtyard Court = new Courtyard();
        float courtAcc, courtSend;
        int duelSeq;
        bool hostInteractWas;
        readonly StringBuilder csb = new StringBuilder(2048);
        readonly System.Collections.Generic.Dictionary<int, bool> interactWas = new System.Collections.Generic.Dictionary<int, bool>();

        /// <summary>One rendered frame of the courtyard: the walkers follow the party, step at 60 Hz, and the guests hear of it.</summary>
        public void TickCourt(float dt, CourtInput hostInput)
        {
            if (!Active) return;
            Court.Join(0, game.Settings.playerName, game.Settings.playerArt);
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) Court.Join(r.id, r.name, r.art);
            }
            Court.Walkers.RemoveAll(w => w.id != 0 && (!remotes.TryGetValue(w.id, out Remote rr) || !rr.joined));

            courtAcc = Mathf.Min(courtAcc + dt, 0.1f);
            bool first = true;
            while (courtAcc >= SimClock.Dt)
            {
                courtAcc -= SimClock.Dt;
                CourtInput host = hostInput;
                if (!first)
                {
                    host.attack = host.parry = host.interact = false;
                    host.kamae = -1;
                }
                first = false;
                Court.Tick(id => id == 0 ? host : GuestCourtInput(id), OnCourtSpot);
            }
            if (Court.PendingDuel is (int a, int b))
            {
                Court.PendingDuel = null;
                StartDuel(a, b);
            }
            courtSend += dt;
            if (courtSend >= 1f / 15f)
            {
                courtSend = 0f;
                BroadcastCourt();
            }
        }

        CourtInput GuestCourtInput(int id)
        {
            if (!remotes.TryGetValue(id, out Remote r)) return default;
            NetInput n = r.input;
            interactWas.TryGetValue(id, out bool was);
            bool now = n.InteractHeld;
            interactWas[id] = now;
            return new CourtInput
            {
                move = n.Move,
                attack = n.Buffer.Consume(BufferedAction.Attack),
                parry = n.Buffer.Consume(BufferedAction.Parry),
                interact = now && !was,
                kamae = n.TakeKamae(),
            };
        }

        /// <summary>The host's own press of Interact this frame (rising edge).</summary>
        public bool HostInteractEdge(bool held)
        {
            bool edge = held && !hostInteractWas;
            hostInteractWas = held;
            return edge;
        }

        /// <summary>What the courtyard's places do: the stone takes a vote, the tea house a line, the torii readiness.</summary>
        void OnCourtSpot(Courtyard.Walker w, string spot)
        {
            switch (spot)
            {
                case "juramento":
                    if (w.id == 0) CycleHostOath(1);
                    else if (remotes.TryGetValue(w.id, out Remote r))
                    {
                        System.Collections.Generic.List<string> ids = OathChoices();
                        int i = Mathf.Max(0, ids.IndexOf(r.oathVote ?? ""));
                        VoteOath(r, ids[(i + 1) % ids.Count]);
                    }
                    string vote = w.id == 0 ? HostOathVote : remotes.TryGetValue(w.id, out Remote rv) ? rv.oathVote : "";
                    Court.Say(OathLabel(vote, 0), w.pos + Vector2.up * 1.4f, 3);
                    break;
                case "cha":
                    Court.Say(Strings.CourtTeaLines[(w.id * 7 + Court.Walkers.Count + (int)(Time.unscaledTime * 3f)) % Strings.CourtTeaLines.Length], w.pos + Vector2.up * 1.4f, 0);
                    break;
                case "torii":
                    if (w.id != 0 && remotes.TryGetValue(w.id, out Remote rt))
                    {
                        rt.ready = !rt.ready;
                        Court.Say(rt.ready ? Strings.SlotReady : Strings.SlotChoosing, w.pos + Vector2.up * 1.4f, rt.ready ? 1 : 0);
                    }
                    else Court.Say(Strings.CourtHostStarts, w.pos + Vector2.up * 1.4f, 0);
                    break;
            }
        }

        void BroadcastCourt()
        {
            csb.Clear();
            csb.Append("{\"t\":\"court\",\"w\":[");
            for (int i = 0; i < Court.Walkers.Count; i++)
            {
                Courtyard.Walker w = Court.Walkers[i];
                if (i > 0) csb.Append(',');
                bool ready = w.id == 0 || (remotes.TryGetValue(w.id, out Remote r) && r.ready);
                csb.Append('[').Int(w.id).Append(',').Str(w.name).Append(',').Int(w.art).Append(',')
                   .Num(w.pos.x, 2).Append(',').Num(w.pos.y, 2).Append(',').Num(w.facing.x, 2).Append(',').Num(w.facing.y, 2).Append(',')
                   .Int(w.moving ? 1 : 0).Append(',').Int(w.swing).Append(',').Int(w.guard).Append(',').Int(w.sitting ? 1 : 0).Append(',')
                   .Int(w.kamae).Append(',').Int(w.challenging ? 1 : 0).Append(',').Int(w.dueling ? 1 : 0).Append(',').Int(ready ? 1 : 0).Append(']');
            }
            csb.Append("],\"p\":[");
            for (int i = 0; i < Court.Popups.Count; i++)
            {
                Courtyard.Popup p = Court.Popups[i];
                if (i > 0) csb.Append(',');
                csb.Append('[').Str(p.text).Append(',').Num(p.at.x, 2).Append(',').Num(p.at.y, 2).Append(',').Int(p.color).Append(',').Num(p.age, 2).Append(']');
            }
            csb.Append("]}");
            string text = csb.ToString();
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) server.Send(r.id, text);
            }
        }

        // ── Duels ────────────────────────────────────────────────────────────────────

        string WeaponOf(int id) => id == 0 ? game.Settings.Loadout(game.Settings.weaponA) : remotes.TryGetValue(id, out Remote r) ? r.weaponA : "katana";

        /// <summary>Two duelists asked in the circle: both get the same start (A is the first to have asked).</summary>
        void StartDuel(int a, int b)
        {
            Courtyard.Walker wa = Court.Get(a), wb = Court.Get(b);
            if (wa == null || wb == null) return;
            int duel = ++duelSeq;
            wa.dueling = wb.dueling = true;
            wa.pos = Courtyard.DuelCircle + Vector2.left * 1.1f;
            wb.pos = Courtyard.DuelCircle + Vector2.right * 1.1f;
            csb.Clear();
            csb.Append("{\"t\":\"duel\",\"d\":").Int(duel)
               .Append(",\"a\":").Int(a).Append(",\"an\":").Str(wa.name).Append(",\"aa\":").Int(wa.art).Append(",\"aw\":").Str(WeaponOf(a)).Append(",\"ak\":").Int(wa.kamae)
               .Append(",\"b\":").Int(b).Append(",\"bn\":").Str(wb.name).Append(",\"ba\":").Int(wb.art).Append(",\"bw\":").Str(WeaponOf(b)).Append(",\"bk\":").Int(wb.kamae)
               .Append('}');
            string text = csb.ToString();
            foreach (int id in new[] { a, b })
            {
                if (id == 0) game.BeginDuel(DuelStart.Parse(MiniJson.Parse(text) as System.Collections.Generic.Dictionary<string, object>, 0));
                else server.Send(id, text);
            }
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) server.Send(r.id, "{\"t\":\"toast\",\"text\":" + Json.Quote(string.Format(Strings.CourtDuelBegins, wa.name, wb.name)) + "}");
            }
            game.UI.Toast(string.Format(Strings.CourtDuelBegins, wa.name, wb.name), game.Config.palette.gold);
            Debug.Log("[Shura][Net] Duelo " + duel + ": " + wa.name + " (" + WeaponOf(a) + ") contra " + wb.name + " (" + WeaponOf(b) + ")");
        }

        /// <summary>A duel message from a guest: for the host's own duel, or passed on to the other guest.</summary>
        void RouteDuel(Remote from, NetIn m, string text)
        {
            if (m.to == 0) game.Duel?.Receive(from.id, m.t, m.d, m.f, m.v, m.h, m.rt, m.g);
            else if (m.to > 0 && remotes.TryGetValue(m.to, out Remote to) && to.joined) server.Send(to.id, text);
        }

        /// <summary>The host's own duel speaks to a guest.</summary>
        public void SendDuel(int to, string text)
        {
            if (remotes.TryGetValue(to, out Remote r) && r.joined) server.Send(r.id, text);
        }

        /// <summary>A duel ended (told by one of the two): back into the courtyard, and everyone hears who won.</summary>
        public void DuelEnded(int a, int b, string line)
        {
            foreach (int id in new[] { a, b })
            {
                Courtyard.Walker w = Court.Get(id);
                if (w != null) w.dueling = false;
            }
            if (string.IsNullOrEmpty(line)) return;
            Debug.Log("[Shura][Net] " + line);
            game.UI.Toast(line, game.Config.palette.gold);
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) server.Send(r.id, "{\"t\":\"toast\",\"text\":" + Json.Quote(line) + "}");
            }
        }
    }
}
