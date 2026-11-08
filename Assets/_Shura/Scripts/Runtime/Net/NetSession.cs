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
        const float SnapshotInterval = 1f / 30f;
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
            public readonly NetInput input = new NetInput();
            public PlayerController player;
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
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────────────

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

        public void Update(float dt)
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

            PublishRoom();
            pingTimer += dt;
            if (pingTimer >= PingInterval)
            {
                pingTimer = 0f;
                server.Broadcast("{\"t\":\"ping\",\"c\":" + (Time.realtimeSinceStartupAsDouble * 1000.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "}");
            }

            snapTimer += dt;
            float interval = game.CurrentMode == Game.Mode.Playing ? SnapshotInterval : IdleSnapshotInterval;
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
            string name = (m.name ?? string.Empty).Trim();
            if (name.Length == 0) name = "Ronin " + r.id;
            if (name.Length > 16) name = name.Substring(0, 16);
            r.name = name;
            r.weaponA = WeaponDefs.Exists(m.a) ? m.a : "katana";
            r.weaponB = WeaponDefs.Exists(m.b) && m.b != r.weaponA ? m.b : (r.weaponA == "montante" ? "katana" : "montante");
            r.slot = FreeSlot();
            r.joined = true;
            r.player = game.World.AddPlayer(r.name, r.input, WeaponDefs.Get(r.weaponA), WeaponDefs.Get(r.weaponB), r.slot);
            r.player.NetId = r.id;
            game.OnPartyChanged();

            sb.Clear();
            sb.Append("{\"t\":\"welcome\",\"id\":").Int(r.id).Append(",\"art\":").Str(PlayerController.PartyArt[r.slot]).Append('}');
            server.Send(r.id, sb.ToString());
            if (roomMessage != null) server.Send(r.id, roomMessage);
            game.UI.Toast(string.Format(Strings.NetJoined, r.name), game.Config.palette.gold);
            Debug.Log("[Shura][Net] Entrou: " + r.name + " (" + r.address + ") com " + r.weaponA + "+" + r.weaponB);
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
            sb.Append("\"}}");
            roomMessage = sb.ToString();
            server.Broadcast(roomMessage);
        }

        // ── Snapshot ────────────────────────────────────────────────────────────────────

        string ModeName()
        {
            switch (game.CurrentMode)
            {
                case Game.Mode.Playing: return "play";
                case Game.Mode.Paused: return "pause";
                case Game.Mode.Reward: return "reward";
                case Game.Mode.Equipment: return "equip";
                case Game.Mode.GameOver: return "over";
                default: return "lobby";
            }
        }

        string BuildSnapshot()
        {
            World w = game.World;
            RunState run = game.Run;
            sb.Clear();
            sb.Append("{\"t\":\"s\",\"m\":").Str(ModeName()).Append(",\"k\":").Int(w.TrapTick).Append(",\"rv\":").Int(w.ArenaVersion);
            if (run != null && w.CurrentRoom != null)
            {
                sb.Append(",\"room\":").Str(string.Format(Strings.HudRoom, run.RoomIndex + 1, w.CurrentRoom.name));
                sb.Append(",\"wave\":").Str(string.Format(Strings.HudWave, Mathf.Max(1, w.WaveIndex + 1), w.CurrentRoom.waves.Length));
                sb.Append(",\"fl\":").Int(run.Flow.Level).Append(",\"fp\":").Num(run.Flow.ProgressInLevel)
                  .Append(",\"st\":").Int(run.PerfectStreak).Append(",\"cb\":").Bool(w.ActiveEnemyCount > 0)
                  .Append(",\"guard\":").Bool(w.CurrentRoom.guardian);
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
                  .Str(p.Weapon.name).Append(',').Str(p.OtherWeapon.name).Append(',').Int(Mathf.RoundToInt(p.Health)).Append(',').Int(Mathf.RoundToInt(p.MaxHealth))
                  .Append(',').Int(p.LatencyTicks).Append(']');
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
                  .Bool(e.IsGone).Append(']');
            }
            sb.Append("],\"R\":[");
            for (int i = 0; i < w.Projectiles.Count; i++)
            {
                Projectile pr = w.Projectiles[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Num(pr.Position.x).Append(',').Num(pr.Position.y).Append(',').Num(pr.Direction.x).Append(',').Num(pr.Direction.y).Append(',')
                  .Bool(pr.Reflected).Append(',').Bool(pr.IsArrow).Append(',').Str(pr.Art).Append(']');
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
                sb.Append('[').Num(it.Position.x).Append(',').Num(it.Position.y).Append(',').Hex(Rarities.Color(it.Item.rarity)).Append(',').Int((int)it.Item.rarity).Append(']');
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
