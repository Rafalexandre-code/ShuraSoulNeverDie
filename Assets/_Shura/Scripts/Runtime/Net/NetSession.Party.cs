using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The host's side of the online §9: what friends leave travels in the "join" message
    /// (and the host's comes back as "presence"); in the lobby, Blood Pacts and the vote for
    /// the group's Oath, shared with every guest as "lobbyx".
    /// </summary>
    public sealed partial class NetSession
    {
        /// <summary>The host's chosen pact partner (a guest's id; -1 nobody).</summary>
        public int HostPact = -1;
        /// <summary>The Oath the host votes for ("" none).</summary>
        public string HostOathVote = "";

        /// <summary>A friend's presence packet arrived with their join: kept in the host's save.</summary>
        void ReceivePresence(Remote r, string json)
        {
            PresencePacket pk = Presence.FromJson(json);
            if (pk != null)
            {
                pk.author = r.name;
                int approved = Presence.Merge(game.Meta, pk, game.Settings.playerName);
                Debug.Log("[Shura][Net] Presença de " + r.name + ": " + pk.messages.Count + " mensagens, " + pk.stains.Count + " manchas, " + pk.rivals.Count + " rivais");
                if (approved > 0) game.UI.Toast(string.Format(Strings.ToastInkApproved, approved, r.name), game.Config.palette.gold);
                game.PersistMeta();
            }
            // Ours goes back to them (a browser player has no save: it costs nothing).
            string mine = Presence.ToJson(Presence.Pack(game.Meta, game.Settings.playerName));
            sb.Clear();
            sb.Append("{\"t\":\"presence\",\"pk\":").Str(mine).Append('}');
            server.Send(r.id, sb.ToString());
        }

        // ── Blood Pacts ──────────────────────────────────────────────────────────────

        /// <summary>The ids that can be chosen for a pact by <paramref name="self"/> (0 = the host).</summary>
        List<int> PactChoices(int self)
        {
            var ids = new List<int> { -1 };
            if (self != 0) ids.Add(0);
            foreach (Remote r in remotes.Values)
            {
                if (r.joined && r.id != self) ids.Add(r.id);
            }
            return ids;
        }

        int PactWish(int id)
        {
            if (id == 0) return HostPact;
            return remotes.TryGetValue(id, out Remote r) && r.joined ? r.pact : -1;
        }

        /// <summary>A pact is sealed when both chose each other.</summary>
        public bool Mutual(int a, int b) => a != b && a >= 0 && b >= 0 && PactWish(a) == b && PactWish(b) == a;

        public string NameOf(int id)
        {
            if (id == 0) return game.Settings.playerName;
            return remotes.TryGetValue(id, out Remote r) ? r.name : "?";
        }

        public void CycleHostPact(int dir)
        {
            List<int> ids = PactChoices(0);
            int i = ids.IndexOf(HostPact);
            HostPact = ids[(i + (dir >= 0 ? 1 : ids.Count - 1)) % ids.Count];
            BroadcastLobbyExtra();
        }

        public string HostPactLabel()
        {
            if (HostPact < 0) return Strings.PactNobody;
            return Mutual(0, HostPact) ? string.Format(Strings.PactSealed, NameOf(HostPact)) : string.Format(Strings.PactOffered, NameOf(HostPact));
        }

        void ChoosePact(Remote r, int target)
        {
            r.pact = target == r.id || (target != 0 && target >= 0 && !remotes.ContainsKey(target)) ? -1 : target;
            BroadcastLobbyExtra();
        }

        /// <summary>The run began: every mutual pair is bound in the world.</summary>
        void SealPacts()
        {
            World w = game.World;
            w.ClearPacts();
            var ids = new List<int> { 0 };
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) ids.Add(r.id);
            }
            foreach (int a in ids)
            {
                foreach (int b in ids)
                {
                    if (a < b && Mutual(a, b))
                    {
                        w.SealPact(PlayerOf(a), PlayerOf(b));
                        Debug.Log("[Shura][Net] Pacto de Sangue: " + NameOf(a) + " e " + NameOf(b));
                    }
                }
            }
        }

        PlayerController PlayerOf(int id)
        {
            if (id == 0) return game.World.Host;
            return remotes.TryGetValue(id, out Remote r) ? r.player : null;
        }

        // ── The group's Oath ─────────────────────────────────────────────────────────

        /// <summary>Oaths a party may swear together (the sealed ones wait for the rivals).</summary>
        public static List<string> OathChoices()
        {
            var ids = new List<string> { "" };
            foreach (OathDef o in Oaths.All)
            {
                if (!o.sealedUntilRivals) ids.Add(o.id);
            }
            return ids;
        }

        public void CycleHostOath(int dir)
        {
            List<string> ids = OathChoices();
            int i = Mathf.Max(0, ids.IndexOf(HostOathVote ?? ""));
            HostOathVote = ids[(i + (dir >= 0 ? 1 : ids.Count - 1)) % ids.Count];
            BroadcastLobbyExtra();
        }

        void VoteOath(Remote r, string id)
        {
            r.oathVote = string.IsNullOrEmpty(id) || Oaths.Get(id) == null ? "" : id;
            BroadcastLobbyExtra();
        }

        /// <summary>The most voted Oath (the host's vote breaks a tie); "" when nobody voted.</summary>
        public string GroupOath(out int votes)
        {
            var count = new Dictionary<string, int>();
            void Add(string v)
            {
                if (string.IsNullOrEmpty(v)) return;
                count.TryGetValue(v, out int n);
                count[v] = n + 1;
            }
            Add(HostOathVote);
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) Add(r.oathVote);
            }
            string best = "";
            votes = 0;
            foreach (KeyValuePair<string, int> kv in count)
            {
                if (kv.Value > votes || (kv.Value == votes && kv.Key == HostOathVote))
                {
                    best = kv.Key;
                    votes = kv.Value;
                }
            }
            return best;
        }

        public static string OathLabel(string id, int votes)
        {
            OathDef o = string.IsNullOrEmpty(id) ? null : Oaths.Get(id);
            if (o == null) return Strings.GroupOathNone;
            return votes > 0 ? string.Format(Strings.GroupOathVotes, o.name, votes) : o.name;
        }

        /// <summary>The run starts: the party's Oath joins the host's own (at least its first level).</summary>
        public void ApplyGroupOath(OathSet set)
        {
            string id = GroupOath(out _);
            if (string.IsNullOrEmpty(id) || set == null) return;
            Debug.Log("[Shura][Net] Juramento do grupo: " + id);
            if (set.Level(id) < 1) set.Set(id, 1);
            game.UI.Toast(string.Format(Strings.ToastGroupOath, Oaths.Get(id).name), game.Config.palette.danger);
        }

        /// <summary>Pacts and the Oath's vote, for every guest's lobby.</summary>
        void BroadcastLobbyExtra()
        {
            if (server == null || !Active) return;
            string oath = GroupOath(out int votes);
            sb.Clear();
            sb.Append("{\"t\":\"lobbyx\",\"oath\":").Str(oath).Append(",\"votes\":").Int(votes).Append(",\"pacts\":[");
            bool first = true;
            var ids = new List<int> { 0 };
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) ids.Add(r.id);
            }
            foreach (int a in ids)
            {
                foreach (int b in ids)
                {
                    if (a >= b || !Mutual(a, b)) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Int(a).Append(',').Int(b);
                }
            }
            sb.Append("],\"wish\":[");
            first = true;
            foreach (int a in ids)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Int(a).Append(',').Int(PactWish(a));
            }
            sb.Append("]}");
            string text = sb.ToString();
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) server.Send(r.id, text);
            }
        }

        /// <summary>One line for the lobby: who is bound to whom, and the Oath.</summary>
        public string LobbyPartyLine()
        {
            var parts = new List<string>();
            var ids = new List<int> { 0 };
            foreach (Remote r in remotes.Values)
            {
                if (r.joined) ids.Add(r.id);
            }
            foreach (int a in ids)
            {
                foreach (int b in ids)
                {
                    if (a < b && Mutual(a, b)) parts.Add(NameOf(a) + " 血 " + NameOf(b));
                }
            }
            string oath = OathLabel(GroupOath(out int votes), votes);
            return string.Format(Strings.LobbyPartyLine, parts.Count > 0 ? string.Join("  ·  ", parts) : Strings.PactNone, oath);
        }

        void ForgetPacts(int id)
        {
            if (HostPact == id) HostPact = -1;
            foreach (Remote r in remotes.Values)
            {
                if (r.pact == id) r.pact = -1;
            }
            BroadcastLobbyExtra();
        }
    }
}
