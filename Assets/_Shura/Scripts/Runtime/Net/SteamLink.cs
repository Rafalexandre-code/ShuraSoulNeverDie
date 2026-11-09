using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Online over the internet through Steam (Spacewar, AppID 480: anyone with Steam can use it).
    ///
    /// The host's game opens a Steam lobby and listens for Steam P2P connections, which go
    /// through Valve's relay network: no VPN, no open ports, any NAT. A guest's game finds the
    /// lobby (or accepts an invite), connects, and starts a small local server that serves the
    /// browser client from its own files and tunnels only the duel's WebSocket (and the arena
    /// image) to the host. The guest plays in the browser window it opens — the same client the
    /// local-network online uses, so everything online already does works here too.
    ///
    /// Wire format: one Steam message per frame, first byte the kind —
    /// 'O' open (guest → host), 'T' text (both ways), 'C' close, 'P' arena image chunk.
    /// </summary>
    public sealed class SteamLink
    {
        public const uint SpacewarAppId = 480;
        const string LobbyKey = "shura";
        const int ProxyFirstPort = 7790;
        const int PngChunk = 300 * 1024;

        public struct LobbyInfo
        {
            public CSteamID id;
            public string host;
            public int players;
            public int max;
            public string version;
        }

        public bool Ready { get; private set; }
        /// <summary>Tests: a guest may join a lobby of its own account (two copies on one PC).</summary>
        public bool AllowSelf { get; set; }
        /// <summary>Tests: do not open the system browser for the guest.</summary>
        public bool NoBrowser { get; set; }
        /// <summary>Tests: the host also listens on 127.0.0.1 (Steam will not connect an account to itself P2P).</summary>
        public bool ListenLocal { get; set; }
        const ushort LocalTestPort = 27020;
        HSteamListenSocket listenLocal = HSteamListenSocket.Invalid;
        public string Status { get; private set; } = string.Empty;
        public bool Hosting => lobby.IsValid() && hostServer != null;
        public bool Searching { get; private set; }
        public bool GuestConnected { get; private set; }
        public string ProxyUrl { get; private set; }
        public string JoinedHost { get; private set; }
        public readonly List<LobbyInfo> Lobbies = new List<LobbyInfo>();
        /// <summary>The lobby list or the connection changed: menus rebuild.</summary>
        public event Action Changed;

        Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        CallResult<LobbyCreated_t> lobbyCreated;
        CallResult<LobbyMatchList_t> lobbyList;
        CallResult<LobbyEnter_t> lobbyEnter;
        CSteamID pendingJoin = CSteamID.Nil;

        // Host side.
        CSteamID lobby = CSteamID.Nil;
        HSteamListenSocket listen = HSteamListenSocket.Invalid;
        WebServer hostServer;
        readonly Dictionary<HSteamNetConnection, Peer> peers = new Dictionary<HSteamNetConnection, Peer>();

        // Guest side.
        HSteamNetConnection guest = HSteamNetConnection.Invalid;
        CSteamID guestLobby = CSteamID.Nil;
        WebServer proxy;
        byte[][] pngParts;
        int pngReceived;
        byte[] pendingPng;

        static readonly IntPtr[] Messages = new IntPtr[64];

        sealed class Peer : WebServer.IPipe
        {
            public SteamLink link;
            public HSteamNetConnection conn;
            public string name;
            public int pipe = -1;

            public void Deliver(string text) => link.SendText(conn, text);
            public void Close() => link.SendKind(conn, (byte)'C');
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
            foreach (Peer p in peers.Values)
            {
                Pump(p.conn, data => OnHostMessage(p, data));
            }
            if (guest != HSteamNetConnection.Invalid) Pump(guest, OnGuestMessage);
            PumpProxy();
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
            hostServer.DynamicChanged += OnDynamicChanged;
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
            if (hostServer != null) hostServer.DynamicChanged -= OnDynamicChanged;
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
                    // The arena image first, so the room message finds it waiting.
                    SendPng(p.conn, hostServer.Dynamic);
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

        void OnDynamicChanged(Dictionary<string, byte[]> files)
        {
            foreach (Peer p in peers.Values) SendPng(p.conn, files);
        }

        void SendPng(HSteamNetConnection conn, Dictionary<string, byte[]> files)
        {
            if (files == null || !files.TryGetValue("/arena.png", out byte[] png) || png == null) return;
            int parts = Mathf.Max(1, (png.Length + PngChunk - 1) / PngChunk);
            for (int i = 0; i < parts; i++)
            {
                int offset = i * PngChunk;
                int len = Mathf.Min(PngChunk, png.Length - offset);
                var buf = new byte[1 + 8 + len];
                buf[0] = (byte)'P';
                BitConverter.GetBytes(parts).CopyTo(buf, 1);
                BitConverter.GetBytes(i).CopyTo(buf, 5);
                Buffer.BlockCopy(png, offset, buf, 9, len);
                SendRaw(conn, buf, true);
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
            Debug.Log("[Shura][Steam] conectando P2P a " + owner.m_SteamID + " → " + guest.m_HSteamNetConnection);
            Status = string.Format(Strings.SteamConnecting, JoinedHost);
            Changed?.Invoke();
        }

        void StartProxy()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "web");
            for (int port = ProxyFirstPort; port < ProxyFirstPort + 10; port++)
            {
                var server = new WebServer(root);
                if (!server.Start(port)) continue;
                proxy = server;
                ProxyUrl = "http://127.0.0.1:" + port + "/";
                break;
            }
            if (proxy == null)
            {
                Status = Strings.SteamProxyFailed;
                return;
            }
            if (pendingPng != null) proxy.Dynamic = new Dictionary<string, byte[]> { { "/arena.png", pendingPng } };
            Application.runInBackground = true;
            OpenBrowser();
        }

        public void OpenBrowser()
        {
            if (!string.IsNullOrEmpty(ProxyUrl) && !NoBrowser) Application.OpenURL(ProxyUrl);
            Debug.Log("[Shura][Steam] Duelo local em " + ProxyUrl);
        }

        void PumpProxy()
        {
            if (proxy == null || guest == HSteamNetConnection.Invalid) return;
            while (proxy.Inbox.TryDequeue(out WebServer.Event e))
            {
                switch (e.kind)
                {
                    case WebServer.EventKind.Connected:
                        SendKind(guest, (byte)'O');
                        break;
                    case WebServer.EventKind.Message:
                        SendText(guest, e.text);
                        break;
                    case WebServer.EventKind.Disconnected:
                        if (proxy.LocalClientCount == 0) SendKind(guest, (byte)'C');
                        break;
                }
            }
        }

        void OnGuestMessage(byte[] data)
        {
            if (data.Length == 0) return;
            switch ((char)data[0])
            {
                case 'T':
                    proxy?.Broadcast(Encoding.UTF8.GetString(data, 1, data.Length - 1));
                    break;
                case 'P':
                {
                    if (data.Length < 9) return;
                    int parts = BitConverter.ToInt32(data, 1), index = BitConverter.ToInt32(data, 5);
                    if (index == 0 || pngParts == null || pngParts.Length != parts)
                    {
                        pngParts = new byte[parts][];
                        pngReceived = 0;
                    }
                    if (index < 0 || index >= parts) return;
                    var chunk = new byte[data.Length - 9];
                    Buffer.BlockCopy(data, 9, chunk, 0, chunk.Length);
                    if (pngParts[index] == null) pngReceived++;
                    pngParts[index] = chunk;
                    if (pngReceived < parts) return;
                    var all = new List<byte>();
                    foreach (byte[] p in pngParts) all.AddRange(p);
                    pendingPng = all.ToArray();
                    if (proxy != null) proxy.Dynamic = new Dictionary<string, byte[]> { { "/arena.png", pendingPng } };
                    pngParts = null;
                    break;
                }
            }
        }

        public void LeaveGuest()
        {
            if (!Ready) return;
            if (guest != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(guest, 0, "guest left", false);
            guest = HSteamNetConnection.Invalid;
            if (guestLobby.IsValid()) SteamMatchmaking.LeaveLobby(guestLobby);
            guestLobby = CSteamID.Nil;
            proxy?.Stop();
            proxy = null;
            ProxyUrl = null;
            GuestConnected = false;
            pendingPng = null;
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
                        StartProxy();
                        if (proxy != null) Status = string.Format(Strings.SteamConnected, JoinedHost);
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
                        proxy?.Stop();
                        proxy = null;
                        Status = Strings.SteamLost;
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

        void SendText(HSteamNetConnection conn, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            var buf = new byte[utf8.Length + 1];
            buf[0] = (byte)'T';
            Buffer.BlockCopy(utf8, 0, buf, 1, utf8.Length);
            // Snapshots go unreliable: a late one is worth nothing, and it must not hold the others back.
            bool snapshot = text.StartsWith("{\"t\":\"s\"", StringComparison.Ordinal) && buf.Length < 400 * 1024;
            SendRaw(conn, buf, !snapshot);
        }

        void SendKind(HSteamNetConnection conn, byte kind) => SendRaw(conn, new[] { kind }, true);

        static void SendRaw(HSteamNetConnection conn, byte[] buf, bool reliable)
        {
            GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                int flags = reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable;
                SteamNetworkingSockets.SendMessageToConnection(conn, h.AddrOfPinnedObject(), (uint)buf.Length, flags, out long _);
            }
            finally
            {
                h.Free();
            }
        }
    }
}
