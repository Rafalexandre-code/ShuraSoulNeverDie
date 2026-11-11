using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The screens of a guest in a friend's duel, in this game's own window: the room between
    /// runs (ready, name, look, blades), the same battle HUD as at home fed by the host's
    /// snapshots, this guest's own reward cards, the vote for the next door, a menu that never
    /// stops the duel, and the equipment worn on the host's side.
    /// </summary>
    public sealed partial class GameUI
    {
        bool remoteMenu;
        bool remoteEquip;
        int remoteIndex;
        float logEpoch;

        RemoteSession Remote => game.Remote;

        /// <summary>Hints wait while a card, a door or a menu covers the duel.</summary>
        bool RemoteHintFree => !Remote.Between && Remote.Cards.Count == 0 && Remote.Doors.Count == 0 && !remoteMenu && !remoteEquip && Remote.Mode != "reward";

        /// <summary>New cards or doors arrived: the selection starts on the first.</summary>
        public void OnRemoteCards()
        {
            remoteIndex = 0;
            selAnim = 0f;
        }

        public void OnRemoteJoined()
        {
            remoteMenu = false;
            remoteEquip = false;
            remoteIndex = 0;
        }

        // ── Input ────────────────────────────────────────────────────────────────────

        void HandleRemote(InputService input)
        {
            RemoteSession r = Remote;
            if (!r.Joined) return;
            if (r.Between)
            {
                remoteMenu = remoteEquip = false;
                r.InputBlocked = true;
                HandleMenu(input);
                return;
            }
            bool cards = r.Cards.Count > 0;
            bool doors = r.Doors.Count > 0;
            r.InputBlocked = remoteMenu || remoteEquip || cards || doors;
            if (cards)
            {
                remoteMenu = remoteEquip = false;
                HandlePick(input, r.Cards.Count, r.Pick, true);
                return;
            }
            if (doors)
            {
                remoteMenu = remoteEquip = false;
                HandlePick(input, r.Doors.Count, r.Vote, false);
                return;
            }
            if (remoteEquip)
            {
                HandleRemoteEquip(input);
                return;
            }
            if (remoteMenu)
            {
                if (game.Controls.PausePressed)
                {
                    remoteMenu = false;
                    return;
                }
                HandleMenu(input);
                return;
            }
            if (game.Controls.PausePressed)
            {
                remoteMenu = true;
                menuIndex = 0;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            else if (game.Controls.EquipmentPressed)
            {
                OpenRemoteEquip();
            }
        }

        void HandlePick(InputService input, int count, System.Action<int> pick, bool cards)
        {
            remoteIndex = Mathf.Clamp(remoteIndex, 0, count - 1);
            int nav = input.NavX != 0 ? input.NavX : cards ? 0 : -input.NavY;
            if (nav != 0)
            {
                remoteIndex = (remoteIndex + nav + count) % count;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit)
            {
                game.Audio.Play(Sfx.UiConfirm, 0.7f, 1f, 0f);
                pick(remoteIndex);
            }
            if (cards && game.Controls.EquipmentPressed) OpenRemoteEquip();
        }

        void OpenRemoteEquip()
        {
            remoteEquip = true;
            remoteMenu = false;
            equipIndex = 0;
            game.Remote.InputBlocked = true;
            game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
        }

        void HandleRemoteEquip(InputService input)
        {
            RemoteSession r = Remote;
            int n = 4 + r.Bag.Count;
            if (input.NavY != 0)
            {
                equipIndex = (equipIndex - input.NavY + n) % n;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) ActivateRemoteEquip(equipIndex);
            else if (input.Cancel || game.Controls.EquipmentPressed) remoteEquip = false;
            equipIndex = Mathf.Clamp(equipIndex, 0, n - 1);
        }

        void ActivateRemoteEquip(int index)
        {
            RemoteSession r = Remote;
            if (index < 4)
            {
                if (r.Worn[index] != null) r.Unequip(index);
            }
            else if (index - 4 < r.Bag.Count) r.Equip(index - 4);
            game.Audio.Play(Sfx.Unsheathe, 0.5f, 1.3f, 0f);
        }

        void BuildRemoteMenu()
        {
            RemoteSession r = Remote;
            if (!r.Joined) return;
            if (r.Between)
            {
                items.Add(new MenuItem { label = r.Ready ? Strings.RemoteNotReady : Strings.RemoteReady, activate = r.ToggleReady });
                items.Add(new MenuItem
                {
                    label = Strings.LobbyName,
                    value = () => editingName ? nameBuffer + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "|" : " ")
                                : Remote.Me != null ? MiniJson.S(Remote.Me, 1) : game.Settings.playerName,
                    activate = BeginNameEdit,
                });
                items.Add(new MenuItem
                {
                    label = Strings.LobbyLook,
                    value = () => Strings.ArtName(game.Settings.playerArt),
                    activate = () => CycleGuestArt(1),
                    adjust = CycleGuestArt,
                });
                AddItem(Strings.LobbyWeapons, game.OpenArsenal);
                AddItem(Strings.Altar, game.OpenAltar);
                AddItem(Strings.Settings, game.OpenSettings);
                AddItem(Strings.RemoteLeave, game.LeaveRemote);
                return;
            }
            if (!remoteMenu) return;
            AddItem(Strings.Resume, () => remoteMenu = false);
            AddItem(Strings.Equipment, OpenRemoteEquip);
            AddItem(Strings.Settings, game.OpenSettings);
            AddItem(Strings.RemoteLeave, game.LeaveRemote);
        }

        void CycleGuestArt(int dir)
        {
            game.CycleHostArt(dir);
            Remote.SendLoadout();
        }

        // ── Drawing ──────────────────────────────────────────────────────────────────

        /// <summary>The duel itself on the HUD canvas: HUD, popups, the log and the ping.</summary>
        void DrawRemoteDuel()
        {
            RemoteSession r = Remote;
            if (r.Latest == null) return;
            DrawRemoteHud();
            DrawPopups();
            List<object> me = r.Me;
            if (me == null || !MiniJson.B(me, 44)) DrawLog();
            DrawPing();
        }

        /// <summary>The screens over the duel (menu canvas): the room between runs, cards, doors, menu, equipment.</summary>
        void DrawRemote()
        {
            RemoteSession r = Remote;
            if (!r.Joined || r.Latest == null)
            {
                DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.7f));
                Label(new Rect(0, VH * 0.5f - 30f, VW, 60f), r.Status, bodyCenterStyle, UiKit.PaperText);
                return;
            }
            if (r.Between)
            {
                DrawRemoteLobby();
                return;
            }
            if (r.Cards.Count > 0) DrawRemoteCards();
            else if (r.Doors.Count > 0) DrawRemoteRoute();
            else if (r.Mode == "reward") DrawRemoteWaiting();
            if (remoteEquip) DrawRemoteEquip();
            else if (remoteMenu) DrawRemoteMenu();
        }

        /// <summary>Top right, small: the round trip to the host.</summary>
        void DrawPing()
        {
            float ms = Remote.Ping * 1000f;
            Color c = ms < 80f ? Pal.blessing : ms < 150f ? Pal.gold : Pal.danger;
            Label(new Rect(VW - 330f, VH - 88f, 320f, 30f), string.Format(Strings.RemotePing, Mathf.RoundToInt(ms)), capsRightStyle, c.WithAlpha(0.85f));
        }

        void DrawRemoteHud()
        {
            RemoteSession r = Remote;
            RemoteSession.Snap s = r.Latest;
            List<object> me = r.Me;
            if (me == null) return;
            hud.FromRow(me);
            if (r.LockPosition.HasValue) DrawLockReticle(r.LockPosition.Value, 0.5f);
            DrawRemoteEnvironment(s);
            DrawRemoteOfferings(s);
            DrawRemoteLabels();
            if (s.so != null && MiniJson.B(s.so, 0)) DrawStandoffBars(MiniJson.B(s.so, 1));
            if (MiniJson.B(me, 46)) DrawRemoteFinisherAim(me);
            if (hud.mushin)
            {
                DrawMushinHud();
                Panel(5, PartyRect(s.P != null ? s.P.Count : 1));
                DrawRemoteParty(s);
                UiKit.Fade = 1f;
                return;
            }
            Panel(1, CardRect);
            DrawDuelistCard(hud);
            Panel(2, BeltRect);
            DrawBelt(hud);
            Panel(3, TitleRect);

            string room = MiniJson.S(s.d, "room");
            if (!string.IsNullOrEmpty(room))
            {
                bool guardian = MiniJson.I(s.d, "guard") != 0;
                Color rc = guardian ? Pal.gold : UiKit.PaperText;
                Label(new Rect(0, 8, VW, 60), room, roomStyle, rc);
                kit.Underline(new Rect(VW * 0.5f - 230f, 62f, 460f, 16f), rc.WithAlpha(0.55f));
                string wave = MiniJson.S(s.d, "wave");
                if (!string.IsNullOrEmpty(wave)) Label(new Rect(0, 76, VW, 30), wave, smallStyleCenter, UiKit.PaperDim);
                DrawRemoteRoadLine(s);
            }
            Panel(4, FlowRect);
            DrawFlow(hud.flow, Mathf.Max(1, MiniJson.I(s.d, "fm", 3)), MiniJson.F(me, 30), MiniJson.I(s.d, "ec"), MiniJson.I(me, 31));
            Panel(5, PartyRect(s.P != null ? s.P.Count : 1));
            DrawRemoteParty(s);
            UiKit.Fade = 1f;
        }

        /// <summary>The hour, the weather, the rule, the challenge and the encounter's banner, from the host's words.</summary>
        void DrawRemoteRoadLine(RemoteSession.Snap s)
        {
            List<object> env = s.env;
            if (env != null)
            {
                var parts = new List<string>();
                var kind = (RoomKind)MiniJson.I(env, 12);
                if (kind != RoomKind.Combat) parts.Add(MiniJson.S(env, 13));
                parts.Add(MiniJson.S(env, 9));
                if ((Weather)MiniJson.I(env, 0) != Weather.Clear) parts.Add(MiniJson.S(env, 8));
                int kalpa = MiniJson.I(env, 11);
                if (kalpa > 1) parts.Add("Kalpa " + KalpaRules.Numeral(kalpa));
                Label(new Rect(0, 132, VW, 30), string.Join("  ·  ", parts), smallStyleCenter, UiKit.PaperDim);
                FitLabel(new Rect(VW * 0.5f - 320f, 158, 640f, 30), MiniJson.S(env, 10), smallStyleCenter, UiKit.PaperDim.WithAlpha(0.75f));
            }
            if (s.ch != null)
            {
                bool failed = MiniJson.B(s.ch, 1);
                string text = failed ? Strings.HudChallengeFailed : string.Format(Strings.HudChallenge, MiniJson.S(s.ch, 0));
                float left = MiniJson.F(s.ch, 2);
                if (!failed && left > 0f) text += "  ·  " + Mathf.CeilToInt(left) + " s";
                Label(new Rect(0, 190, VW, 40), text, cardKindStyle, failed ? Pal.danger : Pal.gold);
            }
            if (s.enc != null)
            {
                var banner = new Rect(VW * 0.5f - 420f, 236f, 840f, 110f);
                kit.InkPanel(banner, 0.9f);
                Label(new Rect(banner.x, banner.y + 10f, banner.width, 44f), MiniJson.S(s.enc, 0), cardKindStyle, Pal.gold);
                Label(new Rect(banner.x + 40f, banner.y + 52f, banner.width - 80f, 50f), MiniJson.S(s.enc, 1), smallStyleCenter, UiKit.PaperText);
            }
        }

        void DrawRemoteEnvironment(RemoteSession.Snap s)
        {
            List<object> env = s.env;
            if (env == null) return;
            Rect full = FullScreen;
            // The hour now lives in the light map and the image grade (PostFx).
            DrawRemoteLighting((TimeOfDay)MiniJson.I(env, 1));
            if (MiniJson.F(env, 5) > 0f) DrawTex(full, vignette, new Color(0.75f, 0.78f, 0.82f, 0.5f));
            float vision = MiniJson.F(env, 3);
            if (vision > 0f && Remote.HasOwn) DrawDarknessAt(Remote.OwnPosition, vision);
            DrawWeather((Weather)MiniJson.I(env, 0), new Vector2(MiniJson.F(env, 6), MiniJson.F(env, 7)));
        }

        void DrawRemoteOfferings(RemoteSession.Snap s)
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null || s.O == null) return;
            RemoteSession r = Remote;
            foreach (object o in s.O)
            {
                if (!(o is List<object> row)) continue;
                var pos = new Vector2(MiniJson.F(row, 0), MiniJson.F(row, 1));
                bool exit = MiniJson.B(row, 13), spent = MiniJson.B(row, 12), repeatable = MiniJson.B(row, 16);
                bool usedByMe = false;
                List<object> used = MiniJson.L(row, 15);
                if (used != null) foreach (object u in used) usedByMe |= u is double d && (int)d == r.MyId;
                bool available = !spent && (repeatable || !usedByMe);
                Vector2 g = WorldToGui(cam, pos + Vector2.up * (exit ? 2.6f : 1.9f));
                Color c = spent || !available ? UiKit.PaperDim.WithAlpha(0.6f) : MiniJson.Color(MiniJson.S(row, 14), Pal.gold);
                int price = MiniJson.I(row, 10);
                string title = MiniJson.S(row, 8) + (price > 0 ? "  ·  " + price + " Mon" : "");
                Label(new Rect(g.x - 260f, g.y - 30f, 520f, 30f), title, smallStyleCenter, c);
                string detail = MiniJson.S(row, 9);
                bool near = r.HasOwn && Vector2.Distance(r.OwnPosition, pos) <= (exit ? 1.8f : 1.1f);
                if (near && !string.IsNullOrEmpty(detail))
                {
                    var box = new Rect(g.x - 260f, g.y - 96f, 520f, 64f);
                    kit.InkPanel(box, 0.85f);
                    Label(new Rect(box.x + 14f, box.y + 6f, box.width - 28f, box.height - 12f), detail + (available && !exit ? "  (" + Strings.HudHold + ")" : ""), smallStyleCenter, UiKit.PaperText);
                }
                float prog = MiniJson.F(row, 11);
                if (prog > 0f) kit.Bar(new Rect(g.x - 60f, g.y + 2f, 120f, 10f), prog, Pal.gold, Color.black.WithAlpha(0.6f));
            }
        }

        /// <summary>Over heads: teammates' names and health, allies, elite affixes, a winded enemy.</summary>
        void DrawRemoteLabels()
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            foreach (RemoteView.Puppet p in Remote.View.Enemies)
            {
                if (p.row == null || MiniJson.B(p.row, 32)) continue;
                if (MiniJson.B(p.row, 38))
                {
                    Vector2 g = WorldToGui(cam, p.pos + Vector2.up * (p.top + 0.55f));
                    Label(new Rect(g.x - 200f, g.y - 14f, 400f, 28f), Strings.HudAlly, smallStyleCenter, Pal.blessing);
                }
                string affix = MiniJson.S(p.row, 36);
                if (!string.IsNullOrEmpty(affix))
                {
                    Vector2 g = WorldToGui(cam, p.pos + Vector2.up * (p.top + 0.95f));
                    Label(new Rect(g.x - 260f, g.y - 14f, 520f, 28f), affix, smallStyleCenter, Pal.gold);
                }
                if (MiniJson.B(p.row, 37))
                {
                    Vector2 g = WorldToGui(cam, p.pos + Vector2.down * 0.5f);
                    Label(new Rect(g.x - 200f, g.y - 12f, 400f, 24f), Strings.RemoteWinded, capsCenterStyle, Pal.breath);
                }
            }
            foreach (RemoteView.Puppet p in Remote.View.Players)
            {
                if (p.isMe || p.row == null) continue;
                bool alive = MiniJson.B(p.row, 21);
                Vector2 g = WorldToGui(cam, p.pos + Vector2.up * 1.15f);
                Label(new Rect(g.x - 200f, g.y - 16f, 400f, 30f), MiniJson.S(p.row, 1), smallStyleCenter, alive ? UiKit.PaperText : Pal.danger);
                if (alive) kit.Bar(new Rect(g.x - 50f, g.y + 14f, 100f, 10f), MiniJson.F(p.row, 17), UiKit.ShuBright, Color.black.WithAlpha(0.7f));
            }
        }

        /// <summary>Teammates under the Flow panel, revive progress where they lie, and the "hold E" prompt.</summary>
        void DrawRemoteParty(RemoteSession.Snap s)
        {
            RemoteSession r = Remote;
            if (s.P == null || s.P.Count < 2) return;
            Camera cam = game.CameraRig.Cam;
            var panel = new Rect(VW - 470f, 240f, 460f, 34f + (s.P.Count - 1) * 40f);
            kit.InkPanel(new Rect(panel.x, panel.y - 10f, panel.width, panel.height + 20f), 0.8f);
            Label(new Rect(panel.x + 40f, panel.y, panel.width - 80f, 26f), string.Format(Strings.OnlineBadge, s.P.Count), capsRightStyle, Pal.gold);
            float y = panel.y + 30f;
            List<object> fallenNear = null;
            float nearest = World.ReviveRange;
            foreach (RemoteView.Puppet p in r.View.Players)
            {
                if (p.isMe || p.row == null) continue;
                bool alive = MiniJson.B(p.row, 21);
                Label(new Rect(panel.x + 40f, y, 170f, 32f), MiniJson.S(p.row, 1), smallStyle, alive ? UiKit.PaperText : UiKit.Shu);
                if (alive) kit.Bar(new Rect(panel.x + 200f, y + 4f, 220f, 22f), MiniJson.F(p.row, 17), UiKit.ShuBright, Color.black.WithAlpha(0.7f));
                else kit.Bar(new Rect(panel.x + 200f, y + 4f, 220f, 22f), MiniJson.F(p.row, 22), Pal.gold, Color.black.WithAlpha(0.7f));
                y += 40f;
                if (alive || cam == null) continue;
                Vector2 g = WorldToGui(cam, p.pos + Vector2.up * 1.3f);
                float t = MiniJson.F(p.row, 22);
                kit.Enso(new Rect(g.x - 34f, g.y - 34f, 68f, 68f), UiKit.PaperText.WithAlpha(0.35f), Time.unscaledTime * 40f);
                if (t > 0f) kit.Bar(new Rect(g.x - 60f, g.y + 34f, 120f, 18f), t, Pal.gold, Color.black.WithAlpha(0.7f));
                float d = r.HasOwn ? Vector2.Distance(r.OwnPosition, p.pos) : float.MaxValue;
                if (d < nearest)
                {
                    nearest = d;
                    fallenNear = p.row;
                }
            }
            bool meAlive = hud.alive;
            if (fallenNear != null && meAlive)
            {
                string text = string.Format(game.Controls.UsingGamepad ? Strings.ReviveHoldPad : Strings.ReviveHold, MiniJson.S(fallenNear, 1));
                float tw = toastStyle.CalcSize(new GUIContent(text)).x;
                kit.Stroke(new Rect(VW * 0.5f - tw * 0.5f - 80f, VH - 250f, tw + 160f, 70f), UiKit.Shu.WithAlpha(0.92f));
                Label(new Rect(0f, VH - 240f, VW, 48f), text, toastStyle, UiKit.PaperText);
            }
            if (!meAlive)
            {
                kit.InkPanel(new Rect(VW * 0.5f - 560f, VH - 260f, 1120f, 110f), 0.9f);
                Label(new Rect(VW * 0.5f - 520f, VH - 252f, 1040f, 90f), Strings.YouFell, hintStyleCenter, UiKit.PaperText);
            }
        }

        void DrawRemoteFinisherAim(List<object> me)
        {
            Camera cam = game.CameraRig.Cam;
            RemoteSession r = Remote;
            if (cam == null || !r.HasOwn) return;
            Vector2 from = r.OwnPosition;
            var point = new Vector2(MiniJson.F(me, 48), MiniJson.F(me, 49));
            Vector2 to = point - from;
            float len = to.magnitude;
            Vector2 dir = len > 1e-3f ? to / len : r.Aim;
            DottedLine(cam, from, point + dir * 0.8f, Pal.gold, 0.28f, 10f);
            Vector2 hit = WorldToGui(cam, point);
            kit.Enso(new Rect(hit.x - 22f, hit.y - 22f, 44f, 44f), Pal.gold, Time.unscaledTime * 120f);
            float err = Vector2.Angle(r.Aim, dir);
            Color aim = err <= PlayerController.CutPerfectDegrees ? Pal.gold
                      : err <= PlayerController.CutLineDegrees ? Color.Lerp(Pal.gold, UiKit.PaperText, 0.5f) : UiKit.PaperText.WithAlpha(0.7f);
            DottedLine(cam, from, from + r.Aim * (len + 0.8f), aim, 0.4f, 6f);
            Vector2 g = WorldToGui(cam, point + Vector2.up * 2f);
            Label(new Rect(g.x - 420f, g.y - 30f, 840f, 30f), Strings.HudCutAim, smallStyleCenter, Pal.gold);
            if (err <= PlayerController.CutPerfectDegrees) Label(new Rect(g.x - 200f, g.y - 64f, 400f, 36f), Strings.HudCutPerfect, cardKindStyle, Pal.gold);
        }

        // ── Cards, doors, waiting ────────────────────────────────────────────────────

        void DrawRemoteCards()
        {
            RemoteSession r = Remote;
            DimGame(1.2f);
            Heading(new Rect(0, 50, VW, 100), Strings.RewardTitle, UiKit.PaperText, UiKit.Shu, 620f);
            Label(new Rect(0, 160, VW, 40), Strings.RemoteRewardSubtitle, bodyCenterStyle, UiKit.PaperDim);
            List<RemoteSession.Card> choices = r.Cards;
            const float gap = 46f;
            int n = Mathf.Max(1, choices.Count);
            float cardW = Mathf.Min(470f, (VW - 160f - gap * (n - 1)) / n);
            float bodyW = cardW - 80f;
            float cardH = 440f;
            foreach (RemoteSession.Card c in choices) cardH = Mathf.Max(cardH, CardHeight(c, cardW, bodyW));
            float startX = (VW - (choices.Count * cardW + (choices.Count - 1) * gap)) * 0.5f;
            float top = Mathf.Min(236f, 790f - cardH);
            Event e = Event.current;
            for (int i = 0; i < choices.Count; i++)
            {
                RemoteSession.Card c = choices[i];
                bool selected = i == remoteIndex;
                float lift = selected ? -18f * CombatMath.EaseOutCubic(selAnim) : 0f;
                var rect = new Rect(startX + i * (cardW + gap), top + lift, cardW, cardH);
                if (!game.Launch.Automated && rect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.Repaint && (e.mousePosition - lastMouse).sqrMagnitude > 1f) remoteIndex = i;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        game.Audio.Play(Sfx.UiConfirm, 0.7f, 1f, 0f);
                        r.Pick(i);
                        return;
                    }
                }
                bool technique = c.kind == RewardKind.Technique, blessing = c.kind == RewardKind.Blessing, item = c.kind == RewardKind.Item;
                Color accent = item ? Pal.gold : technique ? UiKit.GoldInk : blessing ? UiKit.Ai : UiKit.Shu;
                kit.PaperPanel(rect, selected ? 1f : 0.93f);
                kit.Stroke(new Rect(rect.x + 30f, rect.y + 28f, rect.width - 60f, 64f), accent.WithAlpha(0.92f));
                Label(new Rect(rect.x, rect.y + 32f, cardW, 54f), item ? Strings.ItemCard : technique ? Strings.Technique : blessing ? Strings.Blessing : Strings.Relic, cardKindStyle, UiKit.PaperText);
                float nameH = cardNameStyle.CalcHeight(new GUIContent(c.name), cardW - 60f);
                Label(new Rect(rect.x + 30, rect.y + 100, cardW - 60, nameH), c.name, cardNameStyle, UiKit.Ink);
                float y = rect.y + 104 + nameH;
                if (!string.IsNullOrEmpty(c.source))
                {
                    Label(new Rect(rect.x, y, cardW, 30), string.Format(Strings.AbsorbedFrom, c.source), smallStyleCenter, UiKit.InkSoft);
                    y += 32;
                }
                y += 10;
                kit.Underline(new Rect(rect.x + 50f, y, cardW - 100f, 14f), UiKit.Ink.WithAlpha(0.35f));
                y += 18;
                float gainH = cardBodyStyle.CalcHeight(new GUIContent(c.gain), bodyW);
                Label(new Rect(rect.x + 40, y, bodyW, 28), Strings.Gain, capsStyle, accent);
                Label(new Rect(rect.x + 40, y + 28, bodyW, gainH), c.gain, cardBodyStyle, UiKit.Ink);
                y += 28 + gainH + 16;
                float costH = cardBodyStyle.CalcHeight(new GUIContent(c.cost), bodyW);
                Label(new Rect(rect.x + 40, y, bodyW, 28), Strings.Cost, capsStyle, UiKit.Shu);
                Label(new Rect(rect.x + 40, y + 28, bodyW, costH), c.cost, cardBodyStyle, UiKit.Ink);
                if (selected) kit.Seal(new Rect(rect.xMax - 64f, rect.y - 22f, 78f, 78f), 0.95f * CombatMath.EaseOutCubic(selAnim));
            }
            Label(new Rect(0, top + cardH + 12f, VW, 34f), Strings.EquipmentOpen, smallStyleCenter, Pal.gold);
            Label(new Rect(0, VH - 52f, VW, 40), Strings.RewardControls, bodyCenterStyle, UiKit.PaperDim);
        }

        float CardHeight(RemoteSession.Card c, float cardW, float bodyW)
        {
            float h = 104f + cardNameStyle.CalcHeight(new GUIContent(c.name), cardW - 60f);
            if (!string.IsNullOrEmpty(c.source)) h += 32f;
            h += 28f;
            h += 28f + cardBodyStyle.CalcHeight(new GUIContent(c.gain), bodyW) + 16f;
            h += 28f + cardBodyStyle.CalcHeight(new GUIContent(c.cost), bodyW) + 40f;
            return h;
        }

        void DrawRemoteWaiting()
        {
            var panel = new Rect(VW * 0.5f - 520f, 300f, 1040f, 300f);
            DimGame(0.8f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 40f, panel.width, 90f), Strings.PartyChoosingTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 520f);
            Label(new Rect(panel.x + 60f, panel.y + 170f, panel.width - 120f, 80f), Strings.RemoteWaitingParty, bodyCenterStyle, UiKit.Ink);
        }

        // ── Menu and equipment ───────────────────────────────────────────────────────

        void DrawRemoteMenu()
        {
            DimGame(0.45f);
            var panel = new Rect(560f, 140f, 800f, 760f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 40f, panel.width, 100f), Strings.PausedLive, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            Label(new Rect(panel.x, panel.y + 128f, panel.width, 34f), Strings.PausedLiveHint, smallStyleCenter, UiKit.Shu);
            DrawMenuList(panel.y + 200f, 80f, 560f, panel.center.x, true);
            DrawControlsPanel(new Rect(panel.x + 90f, panel.y + 520f, panel.width - 180f, 220f), true);
        }

        void DrawRemoteEquip()
        {
            RemoteSession r = Remote;
            DimGame(1.1f);
            Heading(new Rect(0, 12, VW, 96), Strings.EquipmentTitle, UiKit.PaperText, UiKit.Shu, 520f);
            Label(new Rect(0, 104, VW, 30), Strings.PausedLiveHint, smallStyleCenter, Pal.danger);
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;

            var slots = new Rect(160f, 150f, 760f, 340f);
            kit.PaperPanel(slots);
            Label(new Rect(slots.x + 50f, slots.y + 34f, 400f, 30f), Strings.EquipmentSlots, capsStyle, UiKit.Shu);
            for (int i = 0; i < 4; i++)
            {
                var row = new Rect(slots.x + 40f, slots.y + 70f + i * 62f, slots.width - 80f, 58f);
                if (RemoteEquipMouse(row, i, e, mouseMoved)) return;
                GearRow(row, r.Worn[i], r.SlotNames[i], i == equipIndex);
            }
            var bag = new Rect(160f, 506f, 760f, 504f);
            kit.PaperPanel(bag);
            Label(new Rect(bag.x + 50f, bag.y + 34f, 600f, 30f), Strings.EquipmentBag + "   " + string.Format(Strings.EquipmentBagCount, r.Bag.Count, RunState.BagCapacity), capsStyle, UiKit.Shu);
            for (int i = 0; i < r.Bag.Count && i < 12; i++)
            {
                var row = new Rect(bag.x + 40f, bag.y + 70f + i * 34f, bag.width - 80f, 32f);
                if (RemoteEquipMouse(row, 4 + i, e, mouseMoved)) return;
                GearRow(row, r.Bag[i], null, 4 + i == equipIndex);
            }
            if (r.Bag.Count == 0) Label(new Rect(bag.x + 60f, bag.y + 80f, bag.width - 120f, 30f), Strings.RemoteNoItems, cardBodyStyle, UiKit.InkSoft);

            // The selected item, the duelist's numbers and what they own.
            var info = new Rect(960f, 150f, 800f, 860f);
            kit.InkPanel(info, 0.92f);
            RemoteSession.Gear sel = equipIndex < 4 ? r.Worn[equipIndex] : equipIndex - 4 < r.Bag.Count ? r.Bag[equipIndex - 4] : null;
            float y = info.y + 40f;
            if (sel != null)
            {
                Label(new Rect(info.x + 50f, y, info.width - 100f, 44f), sel.name, cardNameLeftStyle, sel.color);
                Label(new Rect(info.x + 50f, y + 46f, info.width - 100f, 30f), sel.rarity, capsStyle, sel.color);
                float gh = cardBodyStyle.CalcHeight(new GUIContent(sel.gain), info.width - 100f);
                Label(new Rect(info.x + 50f, y + 84f, info.width - 100f, gh), sel.gain, cardBodyStyle, UiKit.PaperText);
                y += 100f + gh;
            }
            y += 20f;
            string[] names = { Strings.HudHealth, Strings.HudPosture, Strings.RemoteStatDamage, Strings.HudDodge, Strings.RemoteStatParry };
            for (int i = 0; i < names.Length; i++)
            {
                Label(new Rect(info.x + 50f, y, 360f, 30f), names[i], capsStyle, UiKit.PaperDim);
                Label(new Rect(info.x + 360f, y, 380f, 30f), i == 2 ? r.Stats[i] + "%" : r.Stats[i].ToString(), smallRightStyle, UiKit.PaperText);
                y += 32f;
            }
            y += 16f;
            if (r.Owned.Count > 0)
            {
                string owned = string.Join("  ·  ", r.Owned);
                float oh = cardBodyStyle.CalcHeight(new GUIContent(owned), info.width - 100f);
                Label(new Rect(info.x + 50f, y, info.width - 100f, Mathf.Min(oh, info.yMax - y - 20f)), owned, cardBodyStyle, Pal.gold);
            }
            Label(new Rect(0, VH - 52f, VW, 36f), Strings.RemoteEquipHint, smallStyleCenter, UiKit.PaperDim);
        }

        bool RemoteEquipMouse(Rect r, int index, Event e, bool mouseMoved)
        {
            if (game.Launch.Automated || !r.Contains(e.mousePosition)) return false;
            if (e.type == EventType.Repaint && mouseMoved) equipIndex = index;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                equipIndex = index;
                ActivateRemoteEquip(index);
                return true;
            }
            return false;
        }

        void GearRow(Rect r, RemoteSession.Gear g, string empty, bool selected)
        {
            if (selected) kit.Stroke(new Rect(r.x - 16f, r.y - 4f, r.width + 32f, r.height + 8f), UiKit.Ink.WithAlpha(0.85f));
            if (g == null)
            {
                Label(new Rect(r.x + 14f, r.y, r.width - 28f, r.height), (empty ?? "") + "  —", smallStyle, selected ? UiKit.PaperText : UiKit.InkSoft);
                return;
            }
            kit.Dab(new Rect(r.x + 4f, r.center.y - 8f, 16f, 16f), g.color);
            string text = (empty != null ? empty + ":  " : "") + g.name;
            FitLabel(new Rect(r.x + 30f, r.y, r.width - 44f, r.height), text, smallStyle, selected ? UiKit.PaperText : UiKit.Ink);
        }

        void DrawDarknessAt(Vector2 world, float radius) => DrawDarknessAt(world, radius, new Color(0f, 0f, 0.02f, 0.94f));

        void DrawDarknessAt(Vector2 world, float radius, Color dark)
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            if (holeTex == null) holeTex = MakeHole();
            Vector2 center = WorldToGui(cam, world);
            float r = Vector2.Distance(center, WorldToGui(cam, world + Vector2.right * radius));
            var hole = new Rect(center.x - r, center.y - r, r * 2f, r * 2f);
            Rect full = FullScreen;
            DrawTex(hole, holeTex, dark);
            DrawRect(new Rect(full.x, full.y, full.width, hole.y - full.y), dark);
            DrawRect(new Rect(full.x, hole.yMax, full.width, full.yMax - hole.yMax), dark);
            DrawRect(new Rect(full.x, hole.y, hole.x - full.x, hole.height), dark);
            DrawRect(new Rect(hole.xMax, hole.y, full.xMax - hole.xMax, hole.height), dark);
        }
    }
}
