using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Online over the internet through Steam (Spacewar, AppID 480: anyone with Steam can use it).
    ///
    /// The host's game opens a Steam lobby and listens for Steam P2P connections. A guest's
    /// game finds the lobby (or accepts an invite), connects, and plays in its own window: the
    /// native client (<see cref="RemoteSession"/>) draws the host's duel and sends its input.
    ///
    /// Latency comes first:
    ///   * ICE is enabled, so two PCs talk directly whenever their NATs allow it; Valve's relay
    ///     is only the fallback (it never needs open ports or a VPN).
    ///   * Nothing waits for Nagle: every message leaves at once.
    ///   * Snapshots and input go unreliable (a newer one replaces a lost one, and nothing
    ///     queues behind a retransmission); everything else is reliable.
    ///   * Large texts (snapshots) are deflated: 4–5× fewer bytes, fewer fragments to lose.
    ///
    /// Wire format: one Steam message per frame, first byte the kind —
    /// 'O' open (guest → host), 'T' text, 'Z' deflated text, 'C' close.
    /// </summary>
    public sealed class SteamLink
    {
        public const uint SpacewarAppId = 480;
        const string LobbyKey = "shura";
        const int CompressAbove = 600;

        public struct LobbyInfo
        {
            public CSteamID id;
            public string host;
            public int players;
            public int max;
            public string version;
        }

        public bool Ready { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public bool Hosting => lobby.IsValid() && hostServer != null;
        public bool Searching { get; private set; }
        public bool GuestConnected { get; private set; }
        public string JoinedHost { get; private set; }
        public readonly List<LobbyInfo> Lobbies = new List<LobbyInfo>();
        /// <summary>Tests: a guest may join a lobby of its own account (two copies on one PC).</summary>
        public bool AllowSelf { get; set; }
        /// <summary>Tests: the host also listens on 127.0.0.1 (Steam will not connect an account to itself P2P).</summary>
        public bool ListenLocal { get; set; }
        const ushort LocalTestPort = 27020;

        /// <summary>The lobby list or the connection changed: menus rebuild.</summary>
        public event Action Changed;
        /// <summary>Guest: the tunnel to the host is up (send "join" now).</summary>
        public event Action GuestOpened;
        /// <summary>Guest: a text message from the host.</summary>
        public event Action<string> GuestText;
        /// <summary>Guest: the host is gone.</summary>
        public event Action GuestLost;

        Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        CallResult<LobbyCreated_t> lobbyCreated;
        CallResult<LobbyMatchList_t> lobbyList;
        CallResult<LobbyEnter_t> lobbyEnter;
        CSteamID pendingJoin = CSteamID.Nil;

        // Host side.
        CSteamID lobby = CSteamID.Nil;
        HSteamListenSocket listen = HSteamListenSocket.Invalid;
        HSteamListenSocket listenLocal = HSteamListenSocket.Invalid;
        WebServer hostServer;
        readonly Dictionary<HSteamNetConnection, Peer> peers = new Dictionary<HSteamNetConnection, Peer>();

        // Guest side.
        HSteamNetConnection guest = HSteamNetConnection.Invalid;
        CSteamID guestLobby = CSteamID.Nil;

        static readonly IntPtr[] Messages = new IntPtr[64];

        sealed class Peer : WebServer.IPipe
        {
            public SteamLink link;
            public HSteamNetConnection conn;
            public string name;
            public int pipe = -1;

            public void Deliver(string text) => link.SendText(conn, text);
            public void Close() => SendRaw(conn, new[] { (byte)'C' }, true);
        }

        // ── Lifecycle ───────────────────────────────────────────────────────────────────

        public bool Init()
        {
            try
            {
                EnsureAppIdFile();
                Ready = SteamAPI.Init();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shura][Steam] " + e.Message);
                Ready = false;
            }
            if (!Ready)
            {
                Status = Strings.SteamOff;
                Debug.Log("[Shura][Steam] Steam indisponível; o online pela rede local continua funcionando.");
                return false;
            }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            // Direct connections whenever the two networks allow it (lower ping than the relay).
            SetGlobalInt(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable, Constants.k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_All);
            statusCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatus);
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r => pendingJoin = r.m_steamIDLobby);
            lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyList = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
            lobbyEnter = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
            // Launched from a Steam invite while closed: "+connect_lobby <id>".
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id)) pendingJoin = new CSteamID(id);
            }
            Status = string.Format(Strings.SteamReady, SteamFriends.GetPersonaName());
            Debug.Log("[Shura][Steam] Steam pronta como " + SteamFriends.GetPersonaName());
            return true;
        }

        static void SetGlobalInt(ESteamNetworkingConfigValue key, int value)
        {
            IntPtr p = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(p, value);
                SteamNetworkingUtils.SetConfigValue(key, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero,
                                                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, p);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        /// <summary>Outside Steam, the API finds the app id in steam_appid.txt next to the game.</summary>
        static void EnsureAppIdFile()
        {
            foreach (string dir in new[] { Directory.GetCurrentDirectory(), Path.GetDirectoryName(Application.dataPath) })
            {
                try
                {
                    string path = Path.Combine(dir, "steam_appid.txt");
                    if (!File.Exists(path)) File.WriteAllText(path, SpacewarAppId.ToString());
                }
                catch
                {
                    // Read-only folder: Steam may still find it next to the executable.
                }
            }
        }

        public void Update()
        {
            if (!Ready) return;
            SteamAPI.RunCallbacks();
            foreach (Peer p in peers.Values) Pump(p.conn, data => OnHostMessage(p, data));
            if (guest != HSteamNetConnection.Invalid) Pump(guest, OnGuestMessage);
        }

        /// <summary>A Steam invite (or "+connect_lobby") waiting to be answered by the menus.</summary>
        public bool TakeJoinRequest(out CSteamID id)
        {
            id = pendingJoin;
            pendingJoin = CSteamID.Nil;
            return id.IsValid();
        }

        public void Shutdown()
        {
            if (!Ready) return;
            StopHost();
            LeaveGuest();
            SteamAPI.Shutdown();
            Ready = false;
        }

        // ── Host ────────────────────────────────────────────────────────────────────────

        public void Host(WebServer server)
        {
            if (!Ready || server == null || Hosting) return;
            hostServer = server;
            listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            if (ListenLocal)
            {
                var addr = new SteamNetworkingIPAddr();
                addr.Clear();
                addr.m_port = LocalTestPort;
                listenLocal = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, null);
            }
            lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, PartyScaling.MaxPlayers));
            Status = Strings.SteamOpening;
        }

        void OnLobbyCreated(LobbyCreated_t r, bool failure)
        {
            if (failure || r.m_eResult != EResult.k_EResultOK)
            {
                Status = Strings.SteamLobbyFailed;
                return;
            }
            lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, LobbyKey, "1");
            SteamMatchmaking.SetLobbyData(lobby, "host", SteamFriends.GetPersonaName());
            SteamMatchmaking.SetLobbyData(lobby, "ver", Application.version);
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobby.m_SteamID);
            SteamFriends.SetRichPresence("status", "Shura — duelo aberto");
            Status = Strings.SteamHosting;
            Debug.Log("[Shura][Steam] Sala aberta na Steam: " + lobby.m_SteamID);
            Changed?.Invoke();
        }

        public void Invite()
        {
            if (lobby.IsValid()) SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        }

        public void StopHost()
        {
            if (!Ready) return;
            foreach (Peer p in peers.Values)
            {
                if (p.pipe >= 0) hostServer?.DetachPipe(p.pipe);
                SteamNetworkingSockets.CloseConnection(p.conn, 0, "host closed", false);
            }
            peers.Clear();
            if (listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listen);
            listen = HSteamListenSocket.Invalid;
            if (listenLocal != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listenLocal);
            listenLocal = HSteamListenSocket.Invalid;
            if (lobby.IsValid()) SteamMatchmaking.LeaveLobby(lobby);
            lobby = CSteamID.Nil;
            hostServer = null;
            SteamFriends.ClearRichPresence();
            Status = string.Format(Strings.SteamReady, SteamFriends.GetPersonaName());
        }

        void OnHostMessage(Peer p, byte[] data)
        {
            if (data.Length == 0 || hostServer == null) return;
            switch ((char)data[0])
            {
                case 'O':
                    if (p.pipe >= 0) hostServer.DetachPipe(p.pipe);
                    p.pipe = hostServer.AttachPipe(p, "Steam · " + p.name);
                    break;
                case 'T':
                    if (p.pipe >= 0) hostServer.PipeMessage(p.pipe, Encoding.UTF8.GetString(data, 1, data.Length - 1));
                    break;
                case 'C':
                    if (p.pipe >= 0) hostServer.DetachPipe(p.pipe);
                    p.pipe = -1;
                    break;
            }
        }

        // ── Guest ───────────────────────────────────────────────────────────────────────

        public void RefreshLobbies()
        {
            if (!Ready) return;
            Lobbies.Clear();
            Searching = true;
            SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKey, "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            lobbyList.Set(SteamMatchmaking.RequestLobbyList());
            Changed?.Invoke();
        }

        void OnLobbyList(LobbyMatchList_t r, bool failure)
        {
            Searching = false;
            Lobbies.Clear();
            if (!failure)
            {
                for (int i = 0; i < r.m_nLobbiesMatching; i++)
                {
                    CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                    Lobbies.Add(new LobbyInfo
                    {
                        id = id,
                        host = SteamMatchmaking.GetLobbyData(id, "host"),
                        players = SteamMatchmaking.GetNumLobbyMembers(id),
                        max = SteamMatchmaking.GetLobbyMemberLimit(id),
                        version = SteamMatchmaking.GetLobbyData(id, "ver"),
                    });
                }
            }
            Changed?.Invoke();
        }

        public void Join(CSteamID id)
        {
            if (!Ready) return;
            LeaveGuest();
            Status = Strings.SteamJoining;
            lobbyEnter.Set(SteamMatchmaking.JoinLobby(id));
            Changed?.Invoke();
        }

        void OnLobbyEnter(LobbyEnter_t r, bool failure)
        {
            if (failure || r.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status = Strings.SteamJoinFailed;
                GuestLost?.Invoke();
                Changed?.Invoke();
                return;
            }
            guestLobby = new CSteamID(r.m_ulSteamIDLobby);
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(guestLobby);
            if (owner == SteamUser.GetSteamID() && !AllowSelf) return;
            JoinedHost = SteamMatchmaking.GetLobbyData(guestLobby, "host");
            if (owner == SteamUser.GetSteamID())
            {
                // Test copy on the host's own account: same sockets API, over loopback.
                var addr = new SteamNetworkingIPAddr();
                addr.Clear();
                addr.SetIPv6LocalHost(LocalTestPort);
                guest = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, null);
            }
            else
            {
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID(owner);
                guest = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
            }
            Debug.Log("[Shura][Steam] conectando a " + owner.m_SteamID + " → " + guest.m_HSteamNetConnection);
            Status = string.Format(Strings.SteamConnecting, JoinedHost);
            Changed?.Invoke();
        }

        /// <summary>Guest → host. Input goes unreliable and immediate; everything else reliable.</summary>
        public void SendToHost(string text)
        {
            if (guest == HSteamNetConnection.Invalid || !GuestConnected) return;
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            var buf = new byte[utf8.Length + 1];
            buf[0] = (byte)'T';
            Buffer.BlockCopy(utf8, 0, buf, 1, utf8.Length);
            bool input = text.StartsWith("{\"t\":\"in\"", StringComparison.Ordinal);
            SendRaw(guest, buf, !input);
        }

        void OnGuestMessage(byte[] data)
        {
            if (data.Length == 0) return;
            switch ((char)data[0])
            {
                case 'T':
                    GuestText?.Invoke(Encoding.UTF8.GetString(data, 1, data.Length - 1));
                    break;
                case 'Z':
                    GuestText?.Invoke(Inflate(data, 1));
                    break;
            }
        }

        public void LeaveGuest()
        {
            if (!Ready) return;
            if (guest != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(guest, 0, "guest left", false);
            guest = HSteamNetConnection.Invalid;
            if (guestLobby.IsValid()) SteamMatchmaking.LeaveLobby(guestLobby);
            guestLobby = CSteamID.Nil;
            GuestConnected = false;
            Status = string.Format(Strings.SteamReady, SteamFriends.GetPersonaName());
        }

        // ── Connections ─────────────────────────────────────────────────────────────────

        void OnConnectionStatus(SteamNetConnectionStatusChangedCallback_t cb)
        {
            HSteamNetConnection conn = cb.m_hConn;
            SteamNetConnectionInfo_t info = cb.m_info;
            bool ours = (listen != HSteamListenSocket.Invalid && info.m_hListenSocket == listen) ||
                        (listenLocal != HSteamListenSocket.Invalid && info.m_hListenSocket == listenLocal);
            Debug.Log("[Shura][Steam] conexão " + conn.m_HSteamNetConnection + ": " + info.m_eState + (ours ? " (anfitrião)" : conn == guest ? " (convidado)" : "") +
                      (string.IsNullOrEmpty(info.m_szConnectionDescription) ? "" : " " + info.m_szConnectionDescription) +
                      (string.IsNullOrEmpty(info.m_szEndDebug) ? "" : " — " + info.m_szEndDebug));
            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (ours) SteamNetworkingSockets.AcceptConnection(conn);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (ours)
                    {
                        CSteamID who = info.m_identityRemote.GetSteamID();
                        peers[conn] = new Peer { link = this, conn = conn, name = SteamFriends.GetFriendPersonaName(who) };
                        Debug.Log("[Shura][Steam] Conectou: " + peers[conn].name);
                    }
                    else if (conn == guest)
                    {
                        GuestConnected = true;
                        Status = string.Format(Strings.SteamConnected, JoinedHost);
                        SendRaw(guest, new[] { (byte)'O' }, true);
                        GuestOpened?.Invoke();
                        Changed?.Invoke();
                    }
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (peers.TryGetValue(conn, out Peer p))
                    {
                        if (p.pipe >= 0) hostServer?.DetachPipe(p.pipe);
                        peers.Remove(conn);
                        Debug.Log("[Shura][Steam] Saiu: " + p.name);
                    }
                    SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                    if (conn == guest)
                    {
                        guest = HSteamNetConnection.Invalid;
                        GuestConnected = false;
                        Status = Strings.SteamLost;
                        GuestLost?.Invoke();
                        Changed?.Invoke();
                    }
                    break;
            }
        }

        static void Pump(HSteamNetConnection conn, Action<byte[]> handle)
        {
            int n = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, Messages, Messages.Length);
            for (int i = 0; i < n; i++)
            {
                SteamNetworkingMessage_t m = Marshal.PtrToStructure<SteamNetworkingMessage_t>(Messages[i]);
                var data = new byte[m.m_cbSize];
                Marshal.Copy(m.m_pData, data, 0, m.m_cbSize);
                SteamNetworkingMessage_t.Release(Messages[i]);
                handle(data);
            }
        }

        /// <summary>Host → guest: snapshots unreliable and immediate, the rest reliable; big texts deflated.</summary>
        void SendText(HSteamNetConnection conn, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            byte[] buf;
            if (utf8.Length > CompressAbove)
            {
                byte[] packed = Deflate(utf8);
                buf = new byte[packed.Length + 1];
                buf[0] = (byte)'Z';
                Buffer.BlockCopy(packed, 0, buf, 1, packed.Length);
            }
            else
            {
                buf = new byte[utf8.Length + 1];
                buf[0] = (byte)'T';
                Buffer.BlockCopy(utf8, 0, buf, 1, utf8.Length);
            }
            // A snapshot with no moments in it may be lost (the next one replaces it); one that
            // carries hits, blood or popups goes reliable so nobody misses a parry's flash.
            bool lossy = text.StartsWith("{\"t\":\"s\"", StringComparison.Ordinal) && text.EndsWith("\"F\":[]}", StringComparison.Ordinal) && buf.Length < 400 * 1024;
            SendRaw(conn, buf, !lossy);
        }

        static byte[] Deflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true)) z.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        static string Inflate(byte[] data, int offset)
        {
            using (var input = new MemoryStream(data, offset, data.Length - offset))
            using (var z = new DeflateStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(z, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        static void SendRaw(HSteamNetConnection conn, byte[] buf, bool reliable)
        {
            GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                int flags = reliable ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;
                SteamNetworkingSockets.SendMessageToConnection(conn, h.AddrOfPinnedObject(), (uint)buf.Length, flags, out long _);
            }
            finally
            {
                h.Free();
            }
        }
    }
}
