using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Online co-op over a LAN or Radmin VPN. The host's game is the server: it runs the one
    /// true simulation, serves the browser client (StreamingAssets/web) and the arena image,
    /// takes each browser player's keys as a <see cref="NetInput"/> driving their own
    /// <see cref="PlayerController"/>, and streams the state back ~30 times a second together
    /// with every hit, parry and drop of blood. Up to 8 players (host + 7).
    /// </summary>
    public sealed class NetSession
    {
        public const int DefaultPort = 7777;
        /// <summary>45 a second: less waiting between what the host sees and what the guests see.</summary>
        const float SnapshotInterval = 1f / 45f;
        const float IdleSnapshotInterval = 0.1f;
        const float PingInterval = 1f;

        public sealed class Remote
        {
            public int id;
            public string name = "?";
            public string weaponA = "katana";
            public string weaponB = "montante";
            public string address;
            public bool joined;
            public float rtt = 0.05f;
            public int slot;
            /// <summary>Look picked in the lobby (index into PlayerController.PartyArt).</summary>
            public int art;
            public bool ready;
            public readonly NetInput input = new NetInput();
            public PlayerController player;
            /// <summary>This player's own reward cards after a room (empty once they picked).</summary>
            public readonly List<RewardDef> choices = new List<RewardDef>();
            /// <summary>Cards still to take (a dispute gives two).</summary>
            public int picksLeft = 1;
        }

        readonly Game game;
        readonly FeedbackTap tap;
        readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
        readonly StringBuilder sb = new StringBuilder(16384);
        WebServer server;
        float snapTimer;
        float pingTimer;
        int sentRoomVersion = -1;
        string roomMessage;
        int snapshotSeq;

        public bool Active { get; private set; }
        public string Error { get; private set; }
        public int Port { get; private set; }
        /// <summary>Links to give the other players: the Radmin VPN one first when there is one.</summary>
        public readonly List<string> Urls = new List<string>();
        public readonly List<string> UrlLabels = new List<string>();
        public IEnumerable<Remote> Remotes => remotes.Values;
        public int JoinedCount
        {
            get
            {
                int n = 0;
                foreach (Remote r in remotes.Values)
                {
                    if (r.joined) n++;
                }
                return n;
            }
        }

        public NetSession(Game owner, FeedbackTap feedbackTap)
        {
            game = owner;
            tap = feedbackTap;
            game.World.RemoteItemCollected += OnRemoteItemCollected;
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────────────

        /// <summary>The HTTP/WebSocket server (Steam tunnels attach their players to it).</summary>
        public WebServer Server => server;

        public bool Start(int port)
        {
            if (Active) return true;
            string root = Path.Combine(Application.streamingAssetsPath, "web");
            server = new WebServer(root);
            Port = port;
            if (!server.Start(port))
            {
                Error = server.Error;
                Debug.LogWarning("[Shura][Net] Não foi possível abrir a porta " + port + ": " + Error);
                return false;
            }
            Active = true;
            Error = null;
            tap.Recording = true;
            sentRoomVersion = -1;
            // The game must keep running (and serving) while the host's window is in the background.
            Application.runInBackground = true;
            CollectUrls();
            Debug.Log("[Shura][Net] Sala aberta: " + string.Join("  ", Urls));
            return true;
        }

        public void Stop()
        {
            if (!Active) return;
            foreach (Remote r in remotes.Values)
            {
                if (r.player != null) game.World.RemovePlayer(r.player);
            }
            remotes.Clear();
            server.Stop();
            server = null;
            Active = false;
            tap.Recording = false;
            Application.runInBackground = game.Launch.Automated;
        }

        void CollectUrls()
        {
            Urls.Clear();
            UrlLabels.Clear();
            var radmin = new List<string>();
            var lan = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    bool isRadmin = (ni.Description + ni.Name).ToLowerInvariant().Contains("radmin");
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        // Radmin VPN hands out 26.x.x.x addresses.
                        if (isRadmin || ip.StartsWith("26.")) radmin.Add(ip);
                        else lan.Add(ip);
                    }
                }
            }
            catch
            {
                // Some systems hide their adapters; localhost still works.
            }
            foreach (string ip in radmin)
            {
                Urls.Add("http://" + ip + ":" + Port);
                UrlLabels.Add("Radmin VPN");
            }
            foreach (string ip in lan)
            {
                Urls.Add("http://" + ip + ":" + Port);
                UrlLabels.Add("Rede local");
            }
            Urls.Add("http://localhost:" + Port);
            UrlLabels.Add("Este computador");
        }

        // ── Per frame ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Takes every message that arrived (joins, input, picks). Called before the frame's
        /// simulation, so a guest's press acts this frame and not the next one.
        /// </summary>
        public void Receive()
        {
            if (!Active) return;
            while (server.Inbox.TryDequeue(out WebServer.Event ev))
            {
                switch (ev.kind)
                {
                    case WebServer.EventKind.Connected:
                        remotes[ev.client] = new Remote { id = ev.client, address = ev.address };
                        break;
                    case WebServer.EventKind.Message:
                        if (remotes.TryGetValue(ev.client, out Remote r)) Handle(r, ev.text);
                        break;
                    case WebServer.EventKind.Disconnected:
                        Disconnect(ev.client);
                        break;
                }
            }
        }

        public void Update(float dt)
        {
            if (!Active) return;
            Receive();
            PublishRoom();
            pingTimer += dt;
            if (pingTimer >= PingInterval)
            {
                pingTimer = 0f;
                server.Broadcast("{\"t\":\"ping\",\"c\":" + (Time.realtimeSinceStartupAsDouble * 1000.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "}");
            }

            snapTimer += dt;
            float interval = game.CurrentMode == Game.Mode.Playing || game.WorldKeepsRunning ? SnapshotInterval : IdleSnapshotInterval;
            if (snapTimer < interval) return;
            // Keep the remainder so frame jitter does not slow the stream below 30/s.
            snapTimer = Mathf.Min(snapTimer - interval, interval);
            server.Broadcast(BuildSnapshot());
        }

        /// <summary>Remote presses age on simulation ticks, like the host's own buffer.</summary>
        public void AgeInputs()
        {
            foreach (Remote r in remotes.Values) r.input.Buffer.Age();
        }

        void Handle(Remote r, string text)
        {
            NetIn m;
            try
            {
                m = JsonUtility.FromJson<NetIn>(text);
            }
            catch
            {
                return;
            }
            if (m == null) return;
            switch (m.t)
            {
                case "join":
                    Join(r, m);
                    break;
                case "in":
                    if (r.joined) r.input.Apply(m);
                    break;
                case "loadout":
                    if (r.joined) ChangeLoadout(r, m);
                    break;
                case "ready":
                    if (r.joined && m.rd >= 0) r.ready = m.rd != 0;
                    break;
                case "pick":
                    if (r.joined) PickReward(r, m.i);
                    break;
                case "vote":
                    if (r.joined) game.VoteRoute(r.id, m.i);
                    break;
                case "equip":
                    if (r.joined && r.player?.Build != null && m.i >= 0 && m.i < r.player.Build.Bag.Count)
                    {
                        r.player.Build.Equip(r.player.Build.Bag[m.i]);
                        r.player.ApplyStats(false);
                        SendInventory(r);
                    }
                    break;
                case "unequip":
                    if (r.joined && r.player?.Build != null && m.i >= 0 && m.i < 4)
                    {
                        r.player.Build.Unequip((ItemSlot)m.i);
                        r.player.ApplyStats(false);
                        SendInventory(r);
                    }
                    break;
                case "inv":
                    if (r.joined) SendInventory(r);
                    break;
                case "gping":
                    // A native guest measures its own ping (it predicts its duelist that far ahead).
                    server.Send(r.id, "{\"t\":\"gpong\",\"c\":" + m.c.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}");
                    break;
                case "pong":
                    double now = Time.realtimeSinceStartupAsDouble * 1000.0;
                    r.rtt = Mathf.Clamp((float)(now - m.c) / 1000f, 0.001f, 1f);
                    if (r.player != null)
                    {
                        // The client draws ~1 snapshot behind; enemies hold their blow that much longer.
                        float ticks = (r.rtt + SnapshotInterval) * SimClock.TickRate * Mathf.Max(0.5f, game.EffectiveTempo);
                        r.player.LatencyTicks = Mathf.Clamp(Mathf.RoundToInt(ticks), 0, 14);
                    }
                    break;
            }
        }

        void Join(Remote r, NetIn m)
        {
            if (r.joined) return;
            if (game.World.Players.Count >= PartyScaling.MaxPlayers)
            {
                server.Send(r.id, "{\"t\":\"full\"}");
                return;
            }
            r.name = GameSettings.CleanName(m.name, "Ronin " + r.id);
            r.weaponA = WeaponDefs.Exists(m.a) ? m.a : "katana";
            r.weaponB = WeaponDefs.Exists(m.b) && m.b != r.weaponA ? m.b : (r.weaponA == "montante" ? "katana" : "montante");
            r.slot = FreeSlot();
            r.art = m.art >= 0 && m.art < PlayerController.PartyArt.Length ? m.art : r.slot;
            r.joined = true;
            r.player = game.World.AddPlayer(r.name, r.input, WeaponDefs.Get(r.weaponA), WeaponDefs.Get(r.weaponB), r.art);
            r.player.NetId = r.id;
            game.OnPartyChanged();

            sb.Clear();
            sb.Append("{\"t\":\"welcome\",\"id\":").Int(r.id).Append(",\"art\":").Int(r.art).Append('}');
            server.Send(r.id, sb.ToString());
            if (roomMessage != null) server.Send(r.id, roomMessage);
            game.UI.Toast(string.Format(Strings.NetJoined, r.name), game.Config.palette.gold);
            Debug.Log("[Shura][Net] Entrou: " + r.name + " (" + r.address + ") com " + r.weaponA + "+" + r.weaponB);
        }

        /// <summary>A run starts: everyone is "choosing" again for the next time the room opens.</summary>
        public void OnRunStarted()
        {
            foreach (Remote r in remotes.Values)
            {
                r.ready = false;
                r.choices.Clear();
            }
        }

        /// <summary>Name, weapons and look can change whenever no run is under way.</summary>
        void ChangeLoadout(Remote r, NetIn m)
        {
            if (!string.IsNullOrEmpty(m.name)) r.name = GameSettings.CleanName(m.name, r.name);
            if (m.art >= 0 && m.art < PlayerController.PartyArt.Length) r.art = m.art;
            if (r.player == null) return;
            r.player.DisplayName = r.name;
            r.player.SetArt(r.art);
            // Weapons change between runs: in the room, or on the "death teaches" screen.
            if (game.Run != null && game.CurrentMode != Game.Mode.GameOver) return;
            if (WeaponDefs.Exists(m.a)) r.weaponA = m.a;
            if (WeaponDefs.Exists(m.b) && m.b != r.weaponA) r.weaponB = m.b;
            if (r.weaponB == r.weaponA) r.weaponB = r.weaponA == "montante" ? "katana" : "montante";
            r.player.SetLoadout(WeaponDefs.Get(r.weaponA), WeaponDefs.Get(r.weaponB));
        }

        // ── Personal rewards and equipment ──────────────────────────────────────────────

        /// <summary>After a room: every browser player gets their own cards, from their own build.</summary>
        public void OfferRewards(int depth, RewardHint hint = RewardHint.Any, int picks = 1)
        {
            foreach (Remote r in remotes.Values)
            {
                r.choices.Clear();
                if (!r.joined || r.player?.Build == null) continue;
                PlayerBuild b = r.player.Build;
                r.picksLeft = picks;
                r.choices.AddRange(b.RollChoices(3 + picks - 1 + b.Modifiers.extraRewardChoices, depth, hint));
                SendChoices(r);
            }
        }

        void SendChoices(Remote r)
        {
            {
                sb.Clear();
                sb.Append("{\"t\":\"rewards\",\"list\":[");
                for (int i = 0; i < r.choices.Count; i++)
                {
                    RewardDef c = r.choices[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"n\":").Str(c.Name).Append(",\"g\":").Str(c.Gain).Append(",\"c\":").Str(c.Cost)
                      .Append(",\"k\":").Int((int)c.Kind).Append(",\"src\":").Str(c.SourceEnemy ?? "").Append('}');
                }
                sb.Append("]}");
                server.Send(r.id, sb.ToString());
            }
        }

        // ── Route map ───────────────────────────────────────────────────────────────────

        /// <summary>The doors ahead, for everyone to vote on (plus two more layers to read ahead).</summary>
        public void OfferRoute(IReadOnlyList<RouteNode> options)
        {
            RunState run = game.Run;
            sb.Clear();
            sb.Append("{\"t\":\"route\",\"o\":[");
            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendNode(options[i]);
            }
            sb.Append("],\"ahead\":[");
            int depth = options.Count > 0 ? options[0].depth : run.RoomIndex + 1;
            for (int d = 1; d < RouteMap.Lookahead; d++)
            {
                if (d > 1) sb.Append(',');
                sb.Append('[');
                List<RouteNode> layer = RouteMap.Layer(run.Seed, depth + d);
                for (int i = 0; i < layer.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendNode(layer[i]);
                }
                sb.Append(']');
            }
            sb.Append("]}");
            server.Broadcast(sb.ToString());
        }

        void AppendNode(RouteNode n)
        {
            sb.Append('[').Int((int)n.kind).Append(',').Str(RouteMap.KindGlyph(n.kind)).Append(',').Str(RouteMap.Describe(n)).Append(',')
              .Str(RouteMap.KindText(n.kind)).Append(',').Hex(RouteMap.KindColor(n.kind)).Append(',').Int(n.column).Append(']');
        }

        public void OnRouteChosen()
        {
            server.Broadcast("{\"t\":\"routed\"}");
        }

        /// <summary>Names of the browser players still choosing their reward.</summary>
        public List<string> ChoosingNames()
        {
            var names = new List<string>();
            foreach (Remote r in remotes.Values)
            {
                if (r.joined && r.choices.Count > 0) names.Add(r.name);
            }
            return names;
        }

        /// <summary>Time is up: whoever has not chosen takes their first card.</summary>
        public void AutoPickRemaining()
        {
            foreach (Remote r in remotes.Values)
            {
                if (r.choices.Count > 0) PickReward(r, 0);
            }
        }

        void PickReward(Remote r, int index)
        {
            if (index < 0 || index >= r.choices.Count || r.player?.Build == null) return;
            RewardDef reward = r.choices[index];
            r.choices.RemoveAt(index);
            if (--r.picksLeft <= 0) r.choices.Clear();
            r.player.Build.Grant(reward);
            r.player.ApplyStats(false);
            if (reward.HealOnPick > 0f) r.player.Heal(reward.HealOnPick);
            sb.Clear();
            sb.Append("{\"t\":\"picked\",\"n\":").Str(reward.Name).Append('}');
            server.Send(r.id, sb.ToString());
            SendInventory(r);
            if (r.choices.Count > 0) SendChoices(r);
        }

        void OnRemoteItemCollected(PlayerController who, Item item, bool equipped)
        {
            foreach (Remote r in remotes.Values)
            {
                if (r.player != who) continue;
                sb.Clear();
                sb.Append("{\"t\":\"toast\",\"text\":").Str((equipped ? "Equipado: " : "Na mochila: ") + item.Name + " (" + Rarities.Name(item.rarity) + ")")
                  .Append(",\"c\":").Hex(Rarities.Color(item.rarity)).Append('}');
                server.Send(r.id, sb.ToString());
                SendInventory(r);
            }
        }

        /// <summary>The player's own worn items, bag, rewards and numbers (for their equipment screen).</summary>
        void SendInventory(Remote r)
        {
            PlayerBuild b = r.player?.Build;
            if (b == null) return;
            sb.Clear();
            sb.Append("{\"t\":\"inv\",\"eq\":[");
            for (int i = 0; i < b.Equipped.Length; i++)
            {
                if (i > 0) sb.Append(',');
                AppendItem(b.Equipped[i]);
            }
            sb.Append("],\"slots\":[");
            for (int i = 0; i < b.Equipped.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Str(ItemGenerator.SlotName((ItemSlot)i));
            }
            sb.Append("],\"bag\":[");
            for (int i = 0; i < b.Bag.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendItem(b.Bag[i]);
            }
            sb.Append("],\"own\":[");
            bool first = true;
            foreach (RewardDef o in b.Owned)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[').Str(o.Name).Append(',').Int((int)o.Kind).Append(']');
            }
            PlayerController p = r.player;
            sb.Append("],\"st\":[").Int(Mathf.RoundToInt(p.MaxHealth)).Append(',').Int(Mathf.RoundToInt(p.Posture.Max)).Append(',')
              .Int(Mathf.RoundToInt(p.DamageDealtMult * 100f)).Append(',').Int(p.MaxDodgeCharges).Append(',').Int(p.PerfectParryFrames).Append("]}");
            server.Send(r.id, sb.ToString());
        }

        void AppendItem(Item item)
        {
            if (item == null)
            {
                sb.Append("null");
                return;
            }
            sb.Append("{\"n\":").Str(item.Name).Append(",\"r\":").Str(Rarities.Name(item.rarity)).Append(",\"rc\":").Hex(Rarities.Color(item.rarity))
              .Append(",\"s\":").Int((int)item.slot).Append(",\"g\":").Str(item.Gain).Append('}');
        }

        int FreeSlot()
        {
            for (int slot = 1; slot < PartyScaling.MaxPlayers; slot++)
            {
                bool used = false;
                foreach (Remote r in remotes.Values)
                {
                    if (r.joined && r.slot == slot) used = true;
                }
                if (!used) return slot;
            }
            return 1;
        }

        void Disconnect(int id)
        {
            if (!remotes.TryGetValue(id, out Remote r)) return;
            remotes.Remove(id);
            r.input.Release();
            if (r.player != null)
            {
                game.World.RemovePlayer(r.player);
                game.UI.Toast(string.Format(Strings.NetLeft, r.name), game.Config.palette.uiDim);
                Debug.Log("[Shura][Net] Saiu: " + r.name);
            }
            game.OnPartyChanged();
        }

        // ── Room (sent when the arena changes) ──────────────────────────────────────────

        void PublishRoom()
        {
            World w = game.World;
            if (w.ArenaVersion == sentRoomVersion || w.Decor == null || w.Layout == null) return;
            sentRoomVersion = w.ArenaVersion;
            byte[] png = w.ArenaPng ?? (w.Decor.FloorTexture != null && w.Decor.FloorTexture.isReadable ? w.Decor.FloorTexture.EncodeToPNG() : null);
            server.Dynamic = new Dictionary<string, byte[]> { { "/arena.png", png ?? new byte[0] } };

            ArenaDecor d = w.Decor;
            ArenaLayout l = w.Layout;
            sb.Clear();
            sb.Append("{\"t\":\"room\",\"v\":").Int(w.ArenaVersion)
              .Append(",\"pw\":").Int(d.PixelSize.x).Append(",\"ph\":").Int(d.PixelSize.y)
              .Append(",\"px\":").Num(d.PixelPivot.x).Append(",\"py\":").Num(d.PixelPivot.y)
              .Append(",\"moss\":").Bool(d.Moss)
              .Append(",\"flame\":").Hex(d.FlameColor);
            sb.Append(",\"lan\":[");
            for (int i = 0; i < d.Lanterns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(d.Lanterns[i].x).Append(',').Num(d.Lanterns[i].y).Append(']');
            }
            sb.Append("],\"sta\":[");
            for (int i = 0; i < d.Statues.Count; i++)
            {
                Vector4 s = d.Statues[i];
                Vector2 baseCenter = l.Origin + new Vector2(s.x + s.z * 0.5f, s.y);
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(baseCenter.x).Append(',').Num(baseCenter.y).Append(',').Num(s.z).Append(',').Int((int)s.w).Append(',')
                  .Num(l.Origin.x + s.x).Append(']');
            }
            sb.Append("],\"trap\":[");
            for (int i = 0; i < l.Traps.Count; i++)
            {
                TrapDef t = l.Traps[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Int((int)t.type).Append(',').Num(t.origin.x).Append(',').Num(t.origin.y).Append(',')
                  .Num(t.direction.x).Append(',').Num(t.direction.y).Append(',').Num(t.length).Append(",[");
                for (int c = 0; c < t.cells.Count; c++)
                {
                    Vector2 cc = l.CellCenter(t.cells[c].x, t.cells[c].y);
                    if (c > 0) sb.Append(',');
                    sb.Num(cc.x).Append(',').Num(cc.y);
                }
                sb.Append("]]");
            }
            // Walkable cells, bottom row first ("1" floor, "0" wall or statue), for path-finding clients.
            sb.Append("],\"grid\":{\"w\":").Int(l.W).Append(",\"h\":").Int(l.H)
              .Append(",\"ox\":").Num(l.Origin.x).Append(",\"oy\":").Num(l.Origin.y).Append(",\"c\":\"");
            for (int y = 0; y < l.H; y++)
            {
                for (int x = 0; x < l.W; x++) sb.Append(l.IsFloor(x, y) ? '1' : '0');
            }
            sb.Append("\"}");
            // The native client rebuilds the very same arena from these (statues, lanterns, traps).
            RoomDef cr = w.CurrentRoom;
            if (cr != null)
            {
                sb.Append(",\"ld\":[").Num(cr.width).Append(',').Num(cr.height).Append(',').Int((int)cr.shape).Append(',').Int(cr.depth).Append(',')
                  .Int(System.Array.IndexOf(Biomes.All, cr.biome)).Append(',').Str(cr.layoutSeed.ToString()).Append(',').Bool(cr.IsEncounter).Append(']');
            }
            sb.Append('}');
            roomMessage = sb.ToString();
            server.Broadcast(roomMessage);
        }

        // ── Snapshot ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The road: weather, hour and biome rule; encounter banner and offerings; zones,
        /// props and broken statues/lanterns; the challenge; route votes.
        /// </summary>
        void AppendRoad(World w, RunState run)
        {
            if (run == null || w.CurrentRoom == null) return;
            RoomDef room = w.CurrentRoom;
            sb.Append(",\"env\":[").Int((int)w.Weather).Append(',').Int((int)w.TimeOfDay).Append(',').Int((int)w.BiomeRule).Append(',')
              .Num(w.VisionRadius).Append(',').Num(w.RingRange).Append(',').Num(w.FogRange).Append(',').Num(w.Wind.x).Append(',').Num(w.Wind.y).Append(',')
              .Str(Biomes.WeatherName(w.Weather)).Append(',').Str(Biomes.TimeName(w.TimeOfDay)).Append(',').Str(Biomes.RuleText(w.BiomeRule)).Append(',')
              .Int(run.Kalpa).Append(',').Int((int)room.kind).Append(',').Str(RouteMap.KindName(room.kind)).Append(']');
            if (room.challenge != ChallengeKind.None)
            {
                sb.Append(",\"ch\":[").Str(RouteMap.ChallengeName(room.challenge)).Append(',').Bool(w.ChallengeFailed).Append(',').Num(w.ChallengeTimeLeft, 1).Append(']');
            }
            if (!string.IsNullOrEmpty(w.EncounterTitle))
            {
                sb.Append(",\"enc\":[").Str(w.EncounterTitle).Append(',').Str(w.EncounterText ?? "").Append(']');
            }
            if (w.Offerings.Count > 0)
            {
                sb.Append(",\"O\":[");
                for (int i = 0; i < w.Offerings.Count; i++)
                {
                    Offering o = w.Offerings[i];
                    if (i > 0) sb.Append(',');
                    sb.Append('[').Num(o.Position.x).Append(',').Num(o.Position.y).Append(',').Str(o.Sheet ?? "").Append(',')
                      .Int(o.Tile.x).Append(',').Int(o.Tile.y).Append(',').Int(o.Tile.width).Append(',').Int(o.Tile.height).Append(',')
                      .Str(o.Character ?? "").Append(',').Str(o.Title).Append(',').Str(o.Detail ?? "").Append(',').Int(o.Price).Append(',')
                      .Num(o.MaxProgress / (o.Exit ? World.ExitHoldSeconds : World.OfferingHoldSeconds)).Append(',').Bool(o.Spent).Append(',').Bool(o.Exit).Append(',')
                      .Hex(o.Color).Append(",[");
                    bool first = true;
                    foreach (PlayerController u in o.UsedBy)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Int(u == w.Host ? 0 : u.NetId);
                    }
                    sb.Append("],").Bool(o.Repeatable).Append(']');
                }
                sb.Append(']');
            }
            if (w.Zones.Count > 0)
            {
                sb.Append(",\"Z\":[");
                for (int i = 0; i < w.Zones.Count; i++)
                {
                    Zone z = w.Zones[i];
                    if (i > 0) sb.Append(',');
                    sb.Append('[').Int((int)z.Kind).Append(',').Num(z.Position.x).Append(',').Num(z.Position.y).Append(',').Num(z.Radius).Append(']');
                }
                sb.Append(']');
            }
            if (w.Props.Count > 0)
            {
                sb.Append(",\"PR\":[");
                bool first = true;
                foreach (Prop p in w.Props)
                {
                    if (p.Gone) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('[').Int((int)p.Kind).Append(',').Num(p.Position.x).Append(',').Num(p.Position.y).Append(',').Num(p.Angle, 0).Append(']');
                }
                sb.Append(']');
            }
            if (w.BrokenStatues.Count > 0 || w.BrokenLanterns.Count > 0)
            {
                sb.Append(",\"SB\":[");
                for (int i = 0; i < w.BrokenStatues.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Int(w.BrokenStatues[i]);
                }
                sb.Append("],\"LB\":[");
                for (int i = 0; i < w.BrokenLanterns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Int(w.BrokenLanterns[i]);
                }
                sb.Append(']');
            }
            if (game.CurrentMode == Game.Mode.Route)
            {
                sb.Append(",\"rt\":[").Num(game.RouteTimeLeft, 0);
                for (int i = 0; i < game.RouteOptions.Count; i++) sb.Append(',').Int(game.RouteVotesFor(i));
                sb.Append(']');
            }
        }

        string ModeName()
        {
            switch (game.CurrentMode)
            {
                case Game.Mode.Playing: return "play";
                case Game.Mode.Paused when game.WorldKeepsRunning: return "play";
                case Game.Mode.Equipment when game.WorldKeepsRunning: return "play";
                case Game.Mode.Settings when game.WorldKeepsRunning: return "play";
                case Game.Mode.Paused: return "pause";
                case Game.Mode.Reward: return "reward";
                case Game.Mode.Route: return "route";
                case Game.Mode.Equipment: return "equip";
                case Game.Mode.GameOver: return "over";
                default: return "lobby";
            }
        }

        /// <summary>In the lobby the host's pair is the one saved in their settings.</summary>
        string WeaponNameOf(PlayerController p, int which)
        {
            if (p == game.World.Host && game.Run == null)
            {
                return WeaponDefs.Get(which == 0 ? game.Settings.weaponA : game.Settings.weaponB).name;
            }
            return which == 0 ? p.Weapon.name : p.OtherWeapon.name;
        }

        string WeaponIdOf(PlayerController p, int which)
        {
            if (p == game.World.Host && game.Run == null) return which == 0 ? game.Settings.weaponA : game.Settings.weaponB;
            return which == 0 ? p.Weapon.id : p.OtherWeapon.id;
        }

        bool ReadyOf(PlayerController p)
        {
            foreach (Remote r in remotes.Values)
            {
                if (r.player == p) return r.ready;
            }
            return false;
        }

        string BuildSnapshot()
        {
            World w = game.World;
            RunState run = game.Run;
            sb.Clear();
            sb.Append("{\"t\":\"s\",\"m\":").Str(ModeName()).Append(",\"k\":").Int(w.TrapTick).Append(",\"rv\":").Int(w.ArenaVersion);
            // Sequence and the host's clock: native guests drop stale snapshots and interpolate on the host's timeline.
            sb.Append(",\"q\":").Int(++snapshotSeq).Append(",\"ht\":").Append((Time.realtimeSinceStartupAsDouble * 1000.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            if (run != null && w.CurrentRoom != null)
            {
                sb.Append(",\"room\":").Str(string.Format(Strings.HudRoom, run.RoomIndex + 1, w.CurrentRoom.name));
                sb.Append(",\"wave\":").Str(w.CurrentRoom.waves.Length == 0 ? "" : string.Format(Strings.HudWave, Mathf.Max(1, w.WaveIndex + 1), w.CurrentRoom.waves.Length));
                sb.Append(",\"fl\":").Int(run.Flow.Level).Append(",\"fp\":").Num(run.Flow.ProgressInLevel)
                  .Append(",\"st\":").Int(run.PerfectStreak).Append(",\"cb\":").Bool(w.ActiveEnemyCount > 0)
                  .Append(",\"guard\":").Bool(w.CurrentRoom.guardian)
                  .Append(",\"ec\":").Int(MetaRules.EcosForRun(run)).Append(",\"fm\":").Int(run.Flow.MaxLevel);
            }
            AppendRoad(w, run);
            // The Iai standoff: [active, advancing, leader x, leader y].
            if (w.Standoff.Active && w.Standoff.Leader != null)
            {
                sb.Append(",\"so\":[1,").Bool(w.Standoff.Advancing).Append(',').Num(w.Standoff.Leader.Position.x).Append(',').Num(w.Standoff.Leader.Position.y).Append(']');
            }

            sb.Append(",\"P\":[");
            for (int i = 0; i < w.Players.Count; i++)
            {
                PlayerController p = w.Players[i];
                if (i > 0) sb.Append(',');
                Color g = p.NetGlow;
                sb.Append('[').Int(p == w.Host ? 0 : p.NetId).Append(',').Str(p.DisplayName).Append(',').Str(PlayerController.PartyArt[p.ArtIndex]).Append(',')
                  .Num(p.RenderPosition.x).Append(',').Num(p.RenderPosition.y).Append(',').Num(p.Facing.x).Append(',').Num(p.Facing.y).Append(',')
                  .Int((int)p.NetDir).Append(',').Int(p.NetPose).Append(',').Int(p.NetFrame).Append(',')
                  .Num(p.NetWeaponAngle, 1).Append(',').Num(p.NetWeaponReach).Append(',').Num(p.NetWeaponLength).Append(',').Bool(p.NetWeaponVisible).Append(',')
                  .Str(p.Weapon.art).Append(',').Num(g.a).Append(',').Hex(g).Append(',')
                  .Num(p.MaxHealth > 0f ? p.Health / p.MaxHealth : 0f).Append(',').Num(p.Posture.Ratio).Append(',')
                  .Num(p.DodgeCharges, 2).Append(',').Int(p.MaxDodgeCharges).Append(',').Bool(p.IsAlive).Append(',')
                  .Num(w.ReviveProgress(p)).Append(',').Num(p.NetTint.a).Append(',')
                  .Str(WeaponNameOf(p, 0)).Append(',').Str(WeaponNameOf(p, 1)).Append(',').Int(Mathf.RoundToInt(p.Health)).Append(',').Int(Mathf.RoundToInt(p.MaxHealth))
                  .Append(',').Int(p.LatencyTicks).Append(',');
                // Each duelist's own Flow, streak, lobby readiness and look.
                PlayerBuild b = p.Build;
                sb.Int(b != null ? b.Flow.Level : 0).Append(',').Num(b != null ? b.Flow.ProgressInLevel : 0f).Append(',')
                  .Int(b != null ? b.PerfectStreak : 0).Append(',').Bool(p == w.Host || ReadyOf(p)).Append(',').Int(p.ArtIndex).Append(',');
                // The combat layers: [34] breath … [53] bandages.
                Vector2 aimPoint = p.AimingCut ? p.AimPoint : Vector2.zero;
                sb.Num(p.Stamina / PlayerController.MaxStamina).Append(',').Bool(p.Exhausted).Append(',')
                  .Num(b != null ? b.Shura / ShuraRules.Max : 0f).Append(',').Int(p.ShuraLevel).Append(',').Int((int)p.Stance).Append(',')
                  .Int(p.Sips).Append(',').Int(p.MaxSips).Append(',').Int(b != null ? (int)b.Tool : 0).Append(',').Int(b != null ? b.SpiritPapers : 0).Append(',')
                  .Int(p.WoundMask).Append(',').Bool(p.InMushin).Append(',').Bool(p.InAsura).Append(',').Bool(p.AimingCut).Append(',')
                  .Num(p.AimLineAngle, 1).Append(',').Num(aimPoint.x).Append(',').Num(aimPoint.y).Append(',')
                  .Bool(p.RingsHidden).Append(',').Num(p.RingScale).Append(',').Int(p.Charge).Append(',').Int(b != null ? b.Bandages : 0).Append(',')
                  // [54] Mon, [55] curse, [56] curse rooms, [57] in spores.
                  .Int(b != null ? b.Mon : 0).Append(',').Str(b != null && b.CurseRooms > 0 ? RunCurses.Name(b.Curse) : "").Append(',')
                  .Int(b != null ? b.CurseRooms : 0).Append(',').Bool(w.InZone(ZoneKind.Spores, p.Position)).Append(',')
                  // [58] the other blade's art, [59] state, [60] moving (the HUD portrait's pose).
                  .Str(b != null ? b.OtherWeapon.art : "").Append(',').Int((int)p.CurrentState).Append(',').Bool(p.Velocity.sqrMagnitude > 0.5f).Append(',')
                  // [61] blade id, [62] the other blade's id, [63] Links' cycle, [64] Muramasa (the native HUD's belt and status).
                  .Str(WeaponIdOf(p, 0)).Append(',').Str(WeaponIdOf(p, 1)).Append(',').Bool(p.CycleFramesLeft > 0).Append(',').Bool(b != null && b.Muramasa).Append(']');
            }
            sb.Append("],\"E\":[");
            bool first = true;
            foreach (Enemy e in w.Enemies)
            {
                if (e.CurrentState == Enemy.State.Dead) continue;
                if (!first) sb.Append(',');
                first = false;
                Color g = e.NetGlow;
                sb.Append('[').Int(e.NetUid).Append(',').Str(e.Def.art).Append(',')
                  .Num(e.RenderPosition.x).Append(',').Num(e.RenderPosition.y).Append(',')
                  .Int((int)e.NetDir).Append(',').Int(e.NetPose).Append(',').Int(e.NetFrame).Append(',').Num(e.NetScale).Append(',').Num(e.NetFeetY).Append(',')
                  .Num(e.NetWeaponAngle, 1).Append(',').Num(e.NetWeaponReach).Append(',').Num(e.NetWeaponLength).Append(',').Bool(e.NetWeaponVisible).Append(',')
                  .Str(e.Def.weaponArt).Append(',').Num(g.a).Append(',').Hex(g).Append(',').Num(e.NetTint.a).Append(',')
                  .Num(e.HealthRatio).Append(',').Num(e.Posture.Ratio).Append(',').Bool(e.IsElite).Append(',').Bool(e.IsBroken).Append(',').Bool(e.Mastered).Append(',')
                  .Int(e.NetTeleType).Append(',').Num(e.NetTeleRange).Append(',').Num(e.NetTeleWidth).Append(',').Num(e.Facing.x).Append(',').Num(e.Facing.y).Append(',')
                  .Num(e.NetTeleAlpha).Append(',').Bool(e.NetDanger).Append(',').Int(e.NetRing).Append(',').Num(e.NetRingSize).Append(',').Num(e.NetRingProgress).Append(',')
                  .Bool(e.IsGone).Append(',');
                // [33] ring kind, [34] seals, [35] veiled, [36] affixes, [37] winded.
                sb.Int(e.NetRingKind).Append(',').Int(e.Seals).Append(',').Bool(e.Veiled).Append(',').Str(e.AffixLabel).Append(',').Bool(e.Winded > 0).Append(',')
                  // [38] ally, [39] frozen, [40] enemy Mushin.
                  .Bool(e.IsAlly).Append(',').Bool(e.Frozen).Append(',').Bool(e.InKalpaMushin).Append(']');
            }
            sb.Append("],\"R\":[");
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                Projectile pr = w.Projectiles[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(pr.Position.x).Append(',').Num(pr.Position.y).Append(',').Num(pr.Direction.x).Append(',').Num(pr.Direction.y).Append(',')
                  .Bool(pr.Reflected).Append(',').Bool(pr.IsArrow).Append(',').Str(pr.Art).Append(',')
                  // [7] size, [8] art angle, [9] spin, [10] speed, [11] colour, [12] passed the player.
                  .Num(pr.Size).Append(',').Num(pr.ArtAngle, 1).Append(',').Num(pr.Spin, 0).Append(',').Num(pr.Speed, 1).Append(',')
                  .Hex(pr.View != null ? pr.View.color : Color.white).Append(',').Bool(pr.PassedPlayer).Append(']');
            }
            sb.Append("],\"B\":[");
            for (int i = 0; i < w.Blasts.Count; i++)
            {
                EnemyBlast bl = w.Blasts[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(bl.Position.x).Append(',').Num(bl.Position.y).Append(',').Num(bl.Radius).Append(',')
                  .Num(bl.Progress).Append(',').Int((int)bl.Kind).Append(',').Hex(bl.Color).Append(']');
            }
            sb.Append("],\"I\":[");
            for (int i = 0; i < w.Pickups.Count; i++)
            {
                ItemPickup it = w.Pickups[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(it.Position.x).Append(',').Num(it.Position.y).Append(',').Hex(Rarities.Color(it.Item.rarity)).Append(',').Int((int)it.Item.rarity).Append(',')
                  .Int((int)it.Item.slot).Append(',').Int((int)it.Item.element).Append(']');
            }
            sb.Append("],\"T\":[");
            if (w.Layout != null)
            {
                for (int i = 0; i < w.Layout.Traps.Count; i++)
                {
                    TrapDef t = w.Layout.Traps[i];
                    if (i > 0) sb.Append(',');
                    if (t.type == TrapType.Blade) sb.Num(Mathf.Repeat(t.BladeAngle(w.TrapTick), 360f), 1);
                    else sb.Int(t.PhaseAt(w.TrapTick));
                }
            }
            sb.Append(']');
            if (game.CurrentMode == Game.Mode.Reward)
            {
                sb.Append(",\"rw\":[");
                IReadOnlyList<RewardDef> choices = game.RewardChoices;
                for (int i = 0; i < choices.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Str(choices[i].Name);
                }
                sb.Append(']');
            }
            sb.Append(",\"F\":[").Append(tap.Take()).Append("]}");
            return sb.ToString();
        }
    }
}
