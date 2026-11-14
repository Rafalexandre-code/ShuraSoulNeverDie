using System.Collections.Generic;

namespace Shura
{
    /// <summary>The guest's side of the online §9: presence both ways, Blood Pacts and the Oath's vote.</summary>
    public sealed partial class RemoteSession
    {
        /// <summary>Who this guest offers a pact to (-1 nobody, 0 the host).</summary>
        public int MyPact { get; private set; } = -1;
        public string MyOathVote { get; private set; } = "";
        public string GroupOath { get; private set; } = "";
        public int GroupOathVotes { get; private set; }
        readonly List<int> sealedPacts = new List<int>();

        string PresenceJson() => Presence.ToJson(Presence.Pack(game.Meta, GuestName));

        void OnPresence(Dictionary<string, object> m)
        {
            PresencePacket pk = Presence.FromJson(MiniJson.S(m, "pk"));
            if (pk == null) return;
            int approved = Presence.Merge(game.Meta, pk, GuestName);
            UnityEngine.Debug.Log("[Shura][Remoto] Presença de " + pk.author + ": " + pk.messages.Count + " mensagens, " + pk.stains.Count + " manchas");
            if (approved > 0) game.UI.Toast(string.Format(Strings.ToastInkApproved, approved, pk.author), game.Config.palette.gold);
            game.PersistMeta();
        }

        void OnLobbyExtra(Dictionary<string, object> m)
        {
            GroupOath = MiniJson.S(m, "oath") ?? "";
            GroupOathVotes = MiniJson.I(m, "votes", 0);
            sealedPacts.Clear();
            if (m.TryGetValue("pacts", out object o) && o is List<object> list)
            {
                for (int i = 0; i < list.Count; i++) sealedPacts.Add(MiniJson.I(list, i));
            }
        }

        /// <summary>The other duelists in the room, by id, from the latest snapshot.</summary>
        List<int> OthersInRoom()
        {
            var ids = new List<int> { -1 };
            Snap s = Latest;
            if (s?.P == null) return ids;
            foreach (object o in s.P)
            {
                if (o is List<object> p && MiniJson.I(p, 0) != MyId) ids.Add(MiniJson.I(p, 0));
            }
            return ids;
        }

        string NameInRoom(int id)
        {
            Snap s = Latest;
            if (s?.P == null) return "?";
            foreach (object o in s.P)
            {
                if (o is List<object> p && MiniJson.I(p, 0) == id) return MiniJson.S(p, 1);
            }
            return "?";
        }

        public void CyclePact(int dir)
        {
            List<int> ids = OthersInRoom();
            int i = ids.IndexOf(MyPact);
            MyPact = ids[(i + (dir >= 0 ? 1 : ids.Count - 1)) % ids.Count];
            Send("{\"t\":\"pact\",\"i\":" + MyPact + "}");
        }

        public bool PactSealed()
        {
            for (int i = 0; i + 1 < sealedPacts.Count; i += 2)
            {
                if ((sealedPacts[i] == MyId && sealedPacts[i + 1] == MyPact) || (sealedPacts[i + 1] == MyId && sealedPacts[i] == MyPact)) return true;
            }
            return false;
        }

        public string PactLabel()
        {
            if (MyPact < 0) return Strings.PactNobody;
            string name = NameInRoom(MyPact);
            return PactSealed() ? string.Format(Strings.PactSealed, name) : string.Format(Strings.PactOffered, name);
        }

        public void CycleOath(int dir)
        {
            List<string> ids = NetSession.OathChoices();
            int i = System.Math.Max(0, ids.IndexOf(MyOathVote ?? ""));
            MyOathVote = ids[(i + (dir >= 0 ? 1 : ids.Count - 1)) % ids.Count];
            sb.Clear();
            sb.Append("{\"t\":\"oath\",\"a\":").Str(MyOathVote).Append('}');
            Send(sb.ToString());
        }

        public string LobbyPartyLine()
        {
            var parts = new List<string>();
            for (int i = 0; i + 1 < sealedPacts.Count; i += 2) parts.Add(NameInRoom(sealedPacts[i]) + " 血 " + NameInRoom(sealedPacts[i + 1]));
            return string.Format(Strings.LobbyPartyLine, parts.Count > 0 ? string.Join("  ·  ", parts) : Strings.PactNone, NetSession.OathLabel(GroupOath, GroupOathVotes));
        }
    }
}
