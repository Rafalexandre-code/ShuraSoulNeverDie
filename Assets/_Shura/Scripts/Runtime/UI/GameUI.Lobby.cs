using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The online room, for the host and for a guest: what you can do on the left; on the
    /// right the eight places of the party (who is in, ready or still choosing, and their
    /// ping) and how friends get in — through Steam, or by address on the same network. The
    /// host's Steam invite opens a friends list inside the game (no Steam overlay needed).
    /// </summary>
    public sealed partial class GameUI
    {
        struct PartySlot
        {
            public string art, name, weapons, tag;
            public Color tagColor;
            public float ping;
            public bool you;
        }

        readonly List<PartySlot> partySlots = new List<PartySlot>();

        // ── Host ─────────────────────────────────────────────────────────────────────

        void DrawLobby()
        {
            NetSession net = game.Net;
            if (!CourtMenuOpen && !invitesOpen)
            {
                DrawCourtHost();
                return;
            }
            SteamLink st = game.Steam;

            int joined = 0, ready = 0;
            foreach (NetSession.Remote r in net.Remotes)
            {
                if (!r.joined) continue;
                joined++;
                if (r.ready) ready++;
            }
            string state = joined == 0 ? Strings.LobbyAlone
                         : ready == joined ? Strings.LobbyAllReady
                         : string.Format(Strings.LobbyWaitingReady, joined - ready);
            DrawRoomMenu(Strings.LobbyTitle, state, joined > 0 && ready == joined ? UiKit.Ai : UiKit.InkSoft);

            var right = new Rect(720f, 36f, 1150f, 1008f);
            kit.PaperPanel(right);

            // The party.
            partySlots.Clear();
            partySlots.Add(new PartySlot
            {
                art = PlayerController.PartyArt[game.Settings.playerArt], name = game.Settings.playerName, you = true,
                weapons = WeaponDefs.Get(game.Settings.Loadout(game.Settings.weaponA)).name + "  ·  " + WeaponDefs.Get(game.Settings.Loadout(game.Settings.weaponB)).name,
                tag = Strings.SlotHost, tagColor = UiKit.Shu, ping = -1f,
            });
            foreach (NetSession.Remote r in net.Remotes)
            {
                if (!r.joined) continue;
                partySlots.Add(new PartySlot
                {
                    art = PlayerController.PartyArt[r.art], name = r.name,
                    weapons = WeaponDefs.Get(r.weaponA).name + "  ·  " + WeaponDefs.Get(r.weaponB).name,
                    tag = r.ready ? Strings.SlotReady : Strings.SlotChoosing, tagColor = r.ready ? UiKit.Ai : UiKit.InkSoft, ping = r.rtt,
                });
            }
            Label(new Rect(right.x + 70f, right.y + 46f, 700f, 32f), string.Format(Strings.LobbyPartyCount, partySlots.Count, PartyScaling.MaxPlayers), capsStyle, UiKit.Shu);
            DrawPartySlots(new Rect(right.x + 60f, right.y + 90f, right.width - 120f, 340f));
            FitLabel(new Rect(right.x + 70f, right.y + 440f, right.width - 140f, 30f), net.LobbyPartyLine(), smallStyle, UiKit.InkSoft);

            // How friends get in.
            float y = right.y + 484f;
            Label(new Rect(right.x + 70f, y, 700f, 32f), Strings.LobbyHowTitle, capsStyle, UiKit.Shu);
            kit.Underline(new Rect(right.x + 60f, y + 28f, right.width - 120f, 14f), UiKit.Ink.WithAlpha(0.3f));
            float cardW = (right.width - 150f) * 0.5f;
            DrawSteamWay(new Rect(right.x + 60f, y + 56f, cardW, 420f), st);
            DrawAddressWay(new Rect(right.x + 90f + cardW, y + 56f, cardW, 420f), net);

            if (invitesOpen) DrawInvites();
        }

        /// <summary>The left page of a room: title, the menu, and one line on where things stand.</summary>
        void DrawRoomMenu(string title, string state, Color stateColor)
        {
            kit.Logo(new Rect(70f, 22f, 380f, 194f), UiKit.Ink);
            const float itemH = 62f;
            var left = new Rect(70f, 226f, 600f, 150f + items.Count * itemH + 130f);
            kit.PaperPanel(left);
            Heading(new Rect(left.x, left.y + 24f, left.width, 80f), title, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 380f);
            DrawMenuList(left.y + 130f, itemH, 520f, left.center.x, true);
            kit.Underline(new Rect(left.x + 60f, left.yMax - 128f, left.width - 120f, 12f), UiKit.Ink.WithAlpha(0.25f));
            Label(new Rect(left.x + 60f, left.yMax - 108f, left.width - 120f, 80f), state, lobbyStateStyle, stateColor);
        }

        void DrawSteamWay(Rect r, SteamLink st)
        {
            DrawRect(r, UiKit.Ink.WithAlpha(0.05f));
            Color dot = st.Hosting ? Pal.blessing : st.Ready ? Pal.gold : Pal.danger;
            kit.Dab(new Rect(r.x + 24f, r.y + 26f, 18f, 18f), dot);
            Label(new Rect(r.x + 52f, r.y + 18f, r.width - 70f, 32f), Strings.LobbyWaySteam, capsStyle, UiKit.Ink);
            string status = !st.Ready ? Strings.SteamOff : st.Hosting ? string.Format(Strings.LobbySteamOpen, SteamName(st)) : Strings.SteamOpening;
            Label(new Rect(r.x + 24f, r.y + 58f, r.width - 48f, 70f), status, lobbyNoteStyle, st.Hosting ? UiKit.Ai : st.Ready ? UiKit.InkSoft : UiKit.Shu);
            if (!st.Ready) return;
            Label(new Rect(r.x + 24f, r.y + 136f, r.width - 48f, 270f), string.Format(Strings.LobbySteamSteps, game.Settings.playerName), lobbyNoteStyle, UiKit.Ink);
        }

        static string SteamName(SteamLink st) => st.Ready ? Steamworks.SteamFriends.GetPersonaName() : "";

        void DrawAddressWay(Rect r, NetSession net)
        {
            DrawRect(r, UiKit.Ink.WithAlpha(0.05f));
            Label(new Rect(r.x + 24f, r.y + 18f, r.width - 48f, 32f), Strings.LobbyWayLan, capsStyle, UiKit.Ink);
            Label(new Rect(r.x + 24f, r.y + 58f, r.width - 48f, 70f), Strings.LobbyLanSteps, lobbyNoteStyle, UiKit.InkSoft);
            Event e = Event.current;
            float y = r.y + 140f;
            int shown = 0;
            for (int i = 0; i < net.Urls.Count && shown < 3; i++)
            {
                bool local = net.UrlLabels[i] == "Este computador";
                if (local && net.Urls.Count > 1) continue;
                string address = ShortAddress(net.Urls[i]);
                var row = new Rect(r.x + 16f, y, r.width - 32f, 64f);
                bool hover = !game.Launch.Automated && row.Contains(e.mousePosition);
                if (hover) DrawRect(row, UiKit.Shu.WithAlpha(0.12f));
                Label(new Rect(row.x + 10f, row.y + 2f, row.width - 20f, 24f), net.UrlLabels[i].ToUpperInvariant(), capsStyle, UiKit.Shu);
                FitLabel(new Rect(row.x + 10f, row.y + 24f, row.width - 20f, 38f), address, urlStyle, UiKit.Ink);
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    e.Use();
                    GUIUtility.systemCopyBuffer = address;
                    Toast(string.Format(Strings.LobbyCopied, address), Pal.gold);
                    game.Audio.Play(Sfx.UiConfirm, 0.6f, 1.2f, 0f);
                }
                y += 72f;
                shown++;
            }
            Label(new Rect(r.x + 24f, r.yMax - 46f, r.width - 48f, 30f), Strings.LobbyCopyHint, creditSmallStyle, UiKit.InkSoft);
        }

        /// <summary>"http://192.168.0.10:7777" → "192.168.0.10" (the port only when it is not the usual one).</summary>
        static string ShortAddress(string url)
        {
            string a = url.StartsWith("http://") ? url.Substring(7) : url;
            string usual = ":" + NetSession.DefaultPort;
            return a.EndsWith(usual) ? a.Substring(0, a.Length - usual.Length) : a;
        }

        /// <summary>Eight places, two by two: who is in, their blades, ready or not, their ping; the rest wait empty.</summary>
        void DrawPartySlots(Rect area)
        {
            const float gapX = 24f, gapY = 14f;
            float w = (area.width - gapX) * 0.5f;
            float h = (area.height - gapY * 3f) / 4f;
            for (int i = 0; i < PartyScaling.MaxPlayers; i++)
            {
                var r = new Rect(area.x + (i % 2) * (w + gapX), area.y + (i / 2) * (h + gapY), w, h);
                if (i >= partySlots.Count)
                {
                    DrawRect(r, UiKit.Ink.WithAlpha(0.03f));
                    kit.Enso(new Rect(r.x + 18f, r.center.y - 28f, 56f, 56f), UiKit.Ink.WithAlpha(0.15f), i * 45f);
                    Label(new Rect(r.x + 96f, r.y, r.width - 110f, r.height), Strings.SlotEmpty, lobbyEmptyStyle, UiKit.InkSoft.WithAlpha(0.45f));
                    continue;
                }
                PartySlot s = partySlots[i];
                DrawRect(r, (s.you ? UiKit.Shu : UiKit.Ink).WithAlpha(s.you ? 0.08f : 0.06f));
                DrawRect(new Rect(r.x, r.y, 5f, r.height), s.tagColor.WithAlpha(0.85f));
                CharacterArt a = PixelArt.Character(s.art);
                if (a != null)
                {
                    Sprite[] walk = a.walk[(int)ArtDir.Down];
                    DrawSprite(new Rect(r.x + 14f, r.center.y - 36f, 72f, 72f), walk[Mathf.FloorToInt(Time.unscaledTime * 5f + i) % walk.Length]);
                }
                FitLabel(new Rect(r.x + 96f, r.y + 2f, r.width - 300f, 42f), s.you ? string.Format(Strings.LobbyHostName, s.name) : s.name, weaponHudStyle, UiKit.Ink);
                FitLabel(new Rect(r.x + 98f, r.y + 42f, r.width - 230f, 30f), s.weapons, smallStyle, UiKit.InkSoft);
                FitLabel(new Rect(r.xMax - 200f, r.y + 10f, 184f, 28f), s.tag, capsRightStyle, s.tagColor);
                if (s.ping >= 0f)
                {
                    float ms = s.ping * 1000f;
                    Label(new Rect(r.xMax - 140f, r.yMax - 34f, 124f, 28f), Mathf.RoundToInt(ms) + " ms", capsRightStyle,
                          ms < 80f ? UiKit.Ai : ms < 150f ? UiKit.GoldInk : UiKit.Shu);
                }
            }
        }

        // ── Guest ────────────────────────────────────────────────────────────────────

        void DrawRemoteLobby()
        {
            RemoteSession r = Remote;
            if (!CourtMenuOpen && r.CourtWalkers != null && r.Mode == "lobby")
            {
                DrawCourtGuest();
                return;
            }
            RemoteSession.Snap s = r.Latest;
            bool over = r.Mode == "over";
            DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.3f));
            string host = "?";
            if (s.players.TryGetValue(0, out List<object> h)) host = MiniJson.S(h, 1);
            DrawRoomMenu(over ? Strings.DeathTitle : string.Format(Strings.RemoteLobbyTitle, host),
                         r.Ready ? Strings.RemoteWaitHost : Strings.RemoteReadyHint, r.Ready ? UiKit.Ai : UiKit.InkSoft);

            var right = new Rect(720f, 36f, 1150f, 1008f);
            kit.PaperPanel(right);
            partySlots.Clear();
            if (s.P != null)
            {
                foreach (object o in s.P)
                {
                    if (!(o is List<object> p)) continue;
                    int id = MiniJson.I(p, 0);
                    bool me = id == r.MyId, isHost = id == 0, isReady = MiniJson.B(p, 32);
                    partySlots.Add(new PartySlot
                    {
                        art = MiniJson.S(p, 2), name = MiniJson.S(p, 1), you = me, weapons = MiniJson.S(p, 24) + "  ·  " + MiniJson.S(p, 25),
                        tag = isHost ? Strings.SlotHost : isReady ? Strings.SlotReady : Strings.SlotChoosing,
                        tagColor = isHost ? UiKit.Shu : isReady ? UiKit.Ai : UiKit.InkSoft, ping = me ? r.Ping : -1f,
                    });
                }
            }
            Label(new Rect(right.x + 70f, right.y + 46f, 700f, 32f), string.Format(Strings.LobbyPartyCount, partySlots.Count, PartyScaling.MaxPlayers), capsStyle, UiKit.Shu);
            DrawPartySlots(new Rect(right.x + 60f, right.y + 90f, right.width - 120f, 340f));
            FitLabel(new Rect(right.x + 70f, right.y + 440f, right.width - 140f, 30f), r.LobbyPartyLine(), smallStyle, UiKit.InkSoft);

            float y = right.y + 484f;
            Label(new Rect(right.x + 70f, y, 700f, 32f), Strings.RemoteHowTitle, capsStyle, UiKit.Shu);
            kit.Underline(new Rect(right.x + 60f, y + 28f, right.width - 120f, 14f), UiKit.Ink.WithAlpha(0.3f));
            var card = new Rect(right.x + 60f, y + 56f, right.width - 120f, 420f);
            DrawRect(card, UiKit.Ink.WithAlpha(0.05f));
            float ms = r.Ping * 1000f;
            kit.Dab(new Rect(card.x + 24f, card.y + 26f, 18f, 18f), ms < 80f ? Pal.blessing : ms < 150f ? Pal.gold : Pal.danger);
            Label(new Rect(card.x + 52f, card.y + 18f, card.width - 70f, 32f), r.Status + "  ·  " + Mathf.RoundToInt(ms) + " ms", capsStyle, UiKit.Ink);
            Label(new Rect(card.x + 24f, card.y + 70f, card.width - 48f, 300f), Strings.RemoteHowSteps, lobbyStepStyle, UiKit.Ink);
        }

        // ── Steam invites, inside the game ───────────────────────────────────────────

        bool invitesOpen;
        int inviteIndex;
        float inviteRefresh;
        List<SteamLink.FriendInfo> friends = new List<SteamLink.FriendInfo>();

        /// <summary>Screenshot tours: show or hide the invite list.</summary>
        public void DevInvites(bool open)
        {
            if (open && !invitesOpen) OpenInvites();
            invitesOpen = open;
        }

        void OpenInvites()
        {
            invitesOpen = true;
            inviteIndex = 0;
            friends = game.Steam.Friends();
            inviteRefresh = Time.unscaledTime;
            if (!game.Steam.Hosting) Toast(Strings.InviteNoLobby, Pal.gold);
        }

        void HandleInvites(InputService input)
        {
            int n = friends.Count;
            if (input.Cancel)
            {
                invitesOpen = false;
                return;
            }
            if (n == 0) return;
            if (input.NavY != 0)
            {
                inviteIndex = (inviteIndex - input.NavY + n) % n;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) InviteAt(inviteIndex);
        }

        void InviteAt(int i)
        {
            if (i < 0 || i >= friends.Count) return;
            SteamLink.FriendInfo f = friends[i];
            if (!game.Steam.Hosting)
            {
                Toast(Strings.InviteNoLobby, Pal.danger);
                game.Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                return;
            }
            bool ok = game.Steam.InviteFriend(f.id);
            Toast(ok ? string.Format(Strings.InviteSent, f.name) : string.Format(Strings.InviteFailed, f.name), ok ? Pal.gold : Pal.danger);
            game.Audio.Play(ok ? Sfx.UiConfirm : Sfx.Denied, 0.7f, 1.1f, 0f);
        }

        void DrawInvites()
        {
            if (Time.unscaledTime - inviteRefresh > 3f)
            {
                friends = game.Steam.Friends();
                inviteRefresh = Time.unscaledTime;
            }
            DimGame(0.85f);
            var panel = new Rect(VW * 0.5f - 600f, 50f, 1200f, 980f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 28f, panel.width, 90f), Strings.InviteTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 480f);
            Label(new Rect(panel.x + 90f, panel.y + 128f, panel.width - 180f, 80f), Strings.InviteHint, lobbyNoteStyle, UiKit.InkSoft);

            int n = friends.Count;
            inviteIndex = Mathf.Clamp(inviteIndex, 0, Mathf.Max(0, n - 1));
            var list = new Rect(panel.x + 80f, panel.y + 220f, panel.width - 160f, panel.height - 330f);
            if (n == 0)
            {
                Label(new Rect(list.x, list.y + 40f, list.width, 40f), Strings.InviteNoFriends, bodyCenterStyle, UiKit.InkSoft);
            }
            const float rowH = 64f;
            int visible = Mathf.FloorToInt(list.height / rowH);
            int first = Mathf.Clamp(inviteIndex - visible / 2, 0, Mathf.Max(0, n - visible));
            Event e = Event.current;
            if (!game.Launch.Automated && e.type == EventType.ScrollWheel && list.Contains(e.mousePosition) && n > 0)
            {
                inviteIndex = Mathf.Clamp(inviteIndex + (e.delta.y > 0 ? 1 : -1), 0, n - 1);
                e.Use();
            }
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int k = 0; k < visible && first + k < n; k++)
            {
                int i = first + k;
                SteamLink.FriendInfo f = friends[i];
                var row = new Rect(list.x, list.y + k * rowH, list.width, rowH - 6f);
                bool sel = i == inviteIndex;
                bool hover = !game.Launch.Automated && row.Contains(e.mousePosition);
                if (hover && e.type == EventType.Repaint && mouseMoved) inviteIndex = i;
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    e.Use();
                    inviteIndex = i;
                    InviteAt(i);
                    return;
                }
                if (sel) kit.Stroke(new Rect(row.x - 30f, row.y - 6f, row.width + 60f, row.height + 12f), UiKit.Shu.WithAlpha(0.9f), CombatMath.EaseOutCubic(selAnim));
                else if (k % 2 == 0) DrawRect(row, UiKit.Ink.WithAlpha(0.04f));
                Texture2D avatar = game.Steam.Avatar(f.id);
                var pic = new Rect(row.x + 14f, row.center.y - 22f, 44f, 44f);
                if (avatar != null) DrawTex(pic, avatar, f.online ? Color.white : new Color(1f, 1f, 1f, 0.45f));
                else kit.Dab(pic, UiKit.Ink.WithAlpha(0.25f));
                Color name = sel ? UiKit.PaperText : f.online ? UiKit.Ink : UiKit.InkSoft;
                FitLabel(new Rect(row.x + 76f, row.y, 460f, row.height), f.name, settingLabelStyle, name);
                string status = f.inShura ? Strings.FriendInShura : !f.online ? Strings.FriendOffline : f.away ? Strings.FriendAway : Strings.FriendOnline;
                Color statusColor = sel ? UiKit.PaperText : f.inShura ? UiKit.Ai : f.online ? UiKit.InkSoft : UiKit.InkSoft.WithAlpha(0.6f);
                Label(new Rect(row.x + 560f, row.y, 260f, row.height), status, settingValueLeftStyle, statusColor);
                bool done = game.Steam.WasInvited(f.id);
                Label(new Rect(row.xMax - 230f, row.y, 210f, row.height), done ? Strings.InviteDone : Strings.InviteAction, settingValueStyle,
                      sel ? UiKit.PaperText : done ? UiKit.Ai : UiKit.Shu);
            }
            if (n > visible) Label(new Rect(list.x, list.yMax - 4f, list.width, 28f), (first + 1) + "–" + Mathf.Min(n, first + visible) + " / " + n, capsCenterStyle, UiKit.InkSoft);
            Label(new Rect(panel.x, panel.yMax - 76f, panel.width, 30f), Strings.InviteControls, smallStyleCenter, UiKit.InkSoft);
        }

        GUIStyle lobbyStateStyle, lobbyNoteStyle, lobbyStepStyle, lobbyEmptyStyle;

        void EnsureLobbyStyles(Font brush, Font text, Font bold)
        {
            lobbyStateStyle = MakeStyle(24, TextAnchor.MiddleCenter, text, FontStyle.Italic);
            lobbyNoteStyle = MakeStyle(22, TextAnchor.UpperLeft, text);
            lobbyStepStyle = MakeStyle(24, TextAnchor.UpperLeft, text);
            lobbyEmptyStyle = MakeStyle(24, TextAnchor.MiddleLeft, text, FontStyle.Italic);
        }
    }
}
