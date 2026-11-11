using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The full screens redrawn for clarity: the title menu, the route map (a real map of
    /// roads between doors), the settings page (sections, ink sliders, switches and a note on
    /// what each option does) and the credits.
    /// </summary>
    public sealed partial class GameUI
    {
        // ── Title menu ───────────────────────────────────────────────────────────────

        void DrawMainMenu()
        {
            kit.Logo(new Rect(90f, 30f, 600f, 306f), UiKit.Ink);
            kit.Seal(new Rect(640f, 250f, 86f, 86f), 0.95f);
            const float itemH = 66f;
            float listH = items.Count * itemH;
            var panel = new Rect(110f, 362f, 620f, listH + 150f);
            kit.PaperPanel(panel);
            Label(new Rect(panel.x + 30f, panel.y + 40f, panel.width - 60f, 34f), Strings.Tagline, taglineSmallStyle, UiKit.InkSoft);
            DrawMenuList(panel.y + 96f, itemH, 500f, panel.center.x, true);
            Label(new Rect(VW - 460, VH - 56, 420, 40), "Shura  ·  v" + Application.version, smallRightStyle, UiKit.PaperText.WithAlpha(0.7f));
        }

        // ── Route map ────────────────────────────────────────────────────────────────

        static RemoteSession.Door DoorOf(RouteNode n) => new RemoteSession.Door
        {
            kind = n.kind, glyph = RouteMap.KindGlyph(n.kind), text = RouteMap.Describe(n), kindText = RouteMap.KindText(n.kind),
            color = RouteMap.KindColor(n.kind), column = n.column,
        };

        readonly List<RemoteSession.Door> routeDoors = new List<RemoteSession.Door>();
        readonly List<List<RemoteSession.Door>> routeAhead = new List<List<RemoteSession.Door>>();

        void DrawRoute()
        {
            RunState run = game.Run;
            routeDoors.Clear();
            foreach (RouteNode n in game.RouteOptions) routeDoors.Add(DoorOf(n));
            routeAhead.Clear();
            int depth = game.RouteOptions.Count > 0 ? game.RouteOptions[0].depth : run.RoomIndex + 1;
            for (int d = 1; d < RouteMap.Lookahead; d++)
            {
                var layer = new List<RemoteSession.Door>();
                foreach (RouteNode n in RouteMap.Layer(run.Seed, depth + d)) layer.Add(DoorOf(n));
                routeAhead.Add(layer);
            }
            bool online = game.Net.Active && game.Net.JoinedCount > 0;
            string sub = string.Format(Strings.ToastDepth, run.RoomIndex + 2) + "  ·  Kalpa " + KalpaRules.Numeral(run.Kalpa) +
                         (run.KalpaRules.Count > 0 ? "  ·  " + KalpaNames(run) : "");
            string foot = online ? string.Format(Strings.RouteVotes, Mathf.CeilToInt(game.RouteTimeLeft)) : Strings.RouteSolo;
            int pick = DrawRouteMap(routeDoors, routeAhead, ref menuIndex, game.HostRouteVote, online ? (Func<int, int>)game.RouteVotesFor : null, sub, foot);
            if (pick >= 0) game.VoteRoute(-1, pick);
        }

        void DrawRemoteRoute()
        {
            RemoteSession r = Remote;
            RemoteSession.Snap s = r.Latest;
            float left = s != null && s.rt != null ? MiniJson.F(s.rt, 0) : 0f;
            Func<int, int> votes = i => s != null && s.rt != null ? MiniJson.I(s.rt, i + 1) : 0;
            int pick = DrawRouteMap(r.Doors, r.DoorsAhead, ref remoteIndex, r.MyVote, votes, Strings.RouteRemoteSub,
                                    string.Format(Strings.RouteVotes, Mathf.CeilToInt(left)));
            if (pick >= 0)
            {
                game.Audio.Play(Sfx.UiConfirm, 0.6f, 1.1f, 0f);
                r.Vote(pick);
            }
        }

        /// <summary>
        /// The road ahead, read left to right: you, the doors you can take now (big cards),
        /// then two more layers. Ink roads join each door to the ones it leads to; the roads
        /// from the highlighted door are painted vermilion. Returns a clicked door, or -1.
        /// </summary>
        int DrawRouteMap(List<RemoteSession.Door> doors, List<List<RemoteSession.Door>> ahead, ref int selected, int voted,
                         Func<int, int> votes, string subtitle, string footer)
        {
            DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.6f));
            var scroll = new Rect(70f, 36f, VW - 140f, VH - 72f);
            kit.PaperPanel(scroll, 0.98f);
            Heading(new Rect(0, 56, VW, 90), Strings.RouteTitle, UiKit.Ink, UiKit.Shu, 520f);
            Label(new Rect(0, 148, VW, 32), subtitle, smallStyleCenter, UiKit.InkSoft);
            int n = doors.Count;
            if (n == 0) return -1;
            selected = Mathf.Clamp(selected, 0, n - 1);

            // Rows by column, so the roads keep their shape from layer to layer.
            int minC = int.MaxValue, maxC = int.MinValue;
            foreach (RemoteSession.Door d in doors) { minC = Mathf.Min(minC, d.column); maxC = Mathf.Max(maxC, d.column); }
            foreach (List<RemoteSession.Door> layer in ahead)
                foreach (RemoteSession.Door d in layer) { minC = Mathf.Min(minC, d.column); maxC = Mathf.Max(maxC, d.column); }
            bool distinct = true;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++) if (doors[i].column == doors[j].column) distinct = false;
            const float centerY = 588f, rowGap = 214f;
            float mid = (minC + maxC) * 0.5f;
            float RowY(float c) => centerY + (c - mid) * rowGap;

            const float startX = 250f, cardX = 420f, cardW = 520f, cardH = 176f;
            float[] layerX = { 1210f, 1530f };
            var cards = new Rect[n];
            for (int i = 0; i < n; i++)
            {
                float y = distinct ? RowY(doors[i].column) : centerY + (i - (n - 1) * 0.5f) * rowGap;
                cards[i] = new Rect(cardX, y - cardH * 0.5f, cardW, cardH);
            }

            // Column headings.
            Label(new Rect(startX - 120f, 222f, 240f, 30f), Strings.RouteYou, capsCenterStyle, UiKit.Shu);
            Label(new Rect(cardX, 222f, cardW, 30f), Strings.RouteNow, capsCenterStyle, UiKit.Shu);
            for (int d = 0; d < ahead.Count && d < layerX.Length; d++)
                Label(new Rect(layerX[d] - 150f, 222f, 300f, 30f), d == 0 ? Strings.RouteThen : Strings.RouteLater, capsCenterStyle, UiKit.InkSoft);

            // Which nodes of the next layers the highlighted door leads to.
            var lit1 = new HashSet<int>();
            var lit2 = new HashSet<int>();
            if (ahead.Count > 0)
            {
                for (int k = 0; k < ahead[0].Count; k++) if (Mathf.Abs(ahead[0][k].column - doors[selected].column) <= 1) lit1.Add(k);
            }
            if (ahead.Count > 1)
            {
                for (int k = 0; k < ahead[1].Count; k++)
                    foreach (int a in lit1) if (Mathf.Abs(ahead[1][k].column - ahead[0][a].column) <= 1) lit2.Add(k);
            }

            // Roads first, under everything.
            var you = new Vector2(startX, centerY);
            for (int i = 0; i < n; i++) Road(you, new Vector2(cards[i].x - 22f, cards[i].center.y), i == selected ? 2 : 0);
            if (ahead.Count > 0)
            {
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < ahead[0].Count; k++)
                        if (Mathf.Abs(ahead[0][k].column - doors[i].column) <= 1)
                            Road(new Vector2(cards[i].xMax, cards[i].center.y), new Vector2(layerX[0] - 46f, RowY(ahead[0][k].column)), i == selected ? 2 : 0);
            }
            if (ahead.Count > 1)
            {
                for (int a = 0; a < ahead[0].Count; a++)
                    for (int k = 0; k < ahead[1].Count; k++)
                        if (Mathf.Abs(ahead[1][k].column - ahead[0][a].column) <= 1)
                            Road(new Vector2(layerX[0] + 46f, RowY(ahead[0][a].column)), new Vector2(layerX[1] - 40f, RowY(ahead[1][k].column)), lit1.Contains(a) ? 1 : 0);
            }

            // You: your own samurai on the road.
            kit.Dab(new Rect(startX - 70f, centerY - 70f, 140f, 140f), UiKit.Ink.WithAlpha(0.12f));
            kit.Enso(new Rect(startX - 66f, centerY - 66f, 132f, 132f), UiKit.Ink.WithAlpha(0.85f), 20f);
            CharacterArt art = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(game.Settings.playerArt, 0, PlayerController.PartyArt.Length - 1)]);
            if (art != null)
            {
                Sprite[] walk = art.walk[(int)ArtDir.Right];
                DrawSpriteTinted(new Rect(startX - 50f, centerY - 54f, 100f, 100f), walk[Mathf.FloorToInt(Time.unscaledTime * 6f) % walk.Length], Color.white);
            }

            // The doors you can take now.
            Event e = Event.current;
            int clicked = -1;
            for (int i = 0; i < n; i++)
            {
                RemoteSession.Door d = doors[i];
                Rect rect = cards[i];
                bool sel = i == selected;
                if (!game.Launch.Automated && rect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.Repaint && (e.mousePosition - lastMouse).sqrMagnitude > 1f) selected = i;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        selected = i;
                        clicked = i;
                    }
                }
                kit.InkPanel(rect, sel ? 1f : 0.7f);
                if (sel)
                {
                    float grow = CombatMath.EaseOutCubic(selAnim);
                    DrawRect(new Rect(rect.x - 18f, rect.y + 18f, 8f, (rect.height - 36f) * grow), UiKit.Shu);
                    kit.Underline(new Rect(rect.x + 168f, rect.yMax - 26f, (rect.width - 200f) * grow, 16f), UiKit.Shu);
                }
                var glyph = new Rect(rect.x + 22f, rect.center.y - 64f, 128f, 128f);
                kit.Enso(glyph, d.color.WithAlpha(sel ? 1f : 0.75f), i * 40f + (sel ? Time.unscaledTime * 25f : 0f));
                Label(new Rect(glyph.x, glyph.y + 6f, glyph.width, glyph.height - 12f), d.glyph, routeGlyphStyle, d.color);
                FitLabel(new Rect(rect.x + 168f, rect.y + 20f, rect.width - 190f, 44f), d.text, cardKindStyle, UiKit.PaperText);
                Label(new Rect(rect.x + 168f, rect.y + 70f, rect.width - 190f, 70f), d.kindText, routeTextStyle, UiKit.PaperDim);
                int v = votes != null ? votes(i) : 0;
                if (v > 0) Label(new Rect(rect.x + 168f, rect.yMax - 40f, rect.width - 190f, 30f), new string('●', v) + "  " + string.Format(Strings.RouteVoteCount, v), capsStyle, Pal.gold);
                if (voted == i) kit.Seal(new Rect(rect.xMax - 58f, rect.y - 24f, 74f, 74f), 0.95f);
            }

            // What lies further: small ink circles with the door's sign and name.
            for (int d = 0; d < ahead.Count && d < layerX.Length; d++)
            {
                for (int k = 0; k < ahead[d].Count; k++)
                {
                    RemoteSession.Door node = ahead[d][k];
                    bool lit = d == 0 ? lit1.Contains(k) : lit2.Contains(k);
                    var c = new Vector2(layerX[d], RowY(node.column));
                    float size = d == 0 ? 92f : 78f;
                    var circle = new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
                    kit.Dab(new Rect(circle.x + 8f, circle.y + 8f, size - 16f, size - 16f), node.color.WithAlpha(lit ? 0.75f : 0.35f));
                    kit.Enso(circle, (lit ? UiKit.Ink : UiKit.InkSoft).WithAlpha(lit ? 0.95f : 0.5f), k * 50f + d * 20f);
                    Label(new Rect(circle.x, circle.y + 4f, size, size - 8f), node.glyph, d == 0 ? routeNodeStyle : routeNodeSmallStyle, (lit ? UiKit.Ink : UiKit.InkSoft).WithAlpha(lit ? 1f : 0.6f));
                    Label(new Rect(c.x - 110f, circle.yMax + 2f, 220f, 28f), RouteMap.KindName(node.kind), capsCenterStyle, (lit ? UiKit.Ink : UiKit.InkSoft).WithAlpha(lit ? 1f : 0.65f));
                }
            }

            // How to choose, inside the scroll.
            kit.Underline(new Rect(VW * 0.5f - 480f, scroll.yMax - 96f, 960f, 14f), UiKit.Ink.WithAlpha(0.3f));
            Label(new Rect(0, scroll.yMax - 82f, VW, 34f), footer, smallStyleCenter, UiKit.InkSoft);
            return clicked;
        }

        /// <summary>An ink road between two points (2 = the chosen road, 1 = what it leads to, 0 = other roads).</summary>
        void Road(Vector2 from, Vector2 to, int strength)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float w = strength == 2 ? 7f : strength == 1 ? 5f : 3f;
            Color c = strength == 2 ? UiKit.Shu.WithAlpha(0.9f) : strength == 1 ? UiKit.Shu.WithAlpha(0.45f) : UiKit.Ink.WithAlpha(0.2f);
            UiKit.Rotated(from, angle, () => DrawRect(new Rect(from.x, from.y - w * 0.5f, len, w), c));
        }

        // ── Settings ─────────────────────────────────────────────────────────────────

        void DrawSettings()
        {
            if (game.SettingsFromPause) DimGame(1f);
            if (controlsOpen)
            {
                DrawControlsScreen();
                return;
            }
            var panel = new Rect(130f, 30f, 1660f, 1020f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 26f, panel.width, 90f), Strings.SettingsTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 520f);
            DrawSettingsList(new Rect(panel.x + 110f, panel.y + 140f, 860f, panel.height - 200f));

            // Right: the keys, then what the highlighted option does.
            var right = new Rect(panel.x + 1050f, panel.y + 150f, 500f, 470f);
            DrawControlsPanel(right, true, 1);
            var help = new Rect(right.x - 20f, panel.yMax - 330f, right.width + 40f, 230f);
            kit.InkPanel(help, 0.92f);
            if (menuIndex >= 0 && menuIndex < items.Count)
            {
                Label(new Rect(help.x + 40f, help.y + 26f, help.width - 80f, 30f), items[menuIndex].label.ToUpperInvariant(), capsStyle, Pal.gold);
                string text = settingHelp.TryGetValue(menuIndex, out string h) ? h : "";
                Label(new Rect(help.x + 40f, help.y + 62f, help.width - 80f, help.height - 80f), text, settingHelpStyle, UiKit.PaperText);
            }
            Label(new Rect(panel.x, panel.yMax - 54f, panel.width, 30f), Strings.SettingsControls, smallStyleCenter, UiKit.InkSoft);
        }

        readonly Dictionary<int, string> settingSections = new Dictionary<int, string>();
        readonly Dictionary<int, string> settingHelp = new Dictionary<int, string>();

        void Section(string title) => settingSections[items.Count] = title;

        void Help(string text) => settingHelp[items.Count - 1] = text;

        /// <summary>Sections of rows: the name on the left, an ink slider, a switch or a choice on the right.</summary>
        void DrawSettingsList(Rect area)
        {
            const float rowH = 46f, headH = 40f;
            float y = area.y;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int i = 0; i < items.Count; i++)
            {
                if (settingSections.TryGetValue(i, out string title))
                {
                    if (i > 0) y += 8f;
                    if (!string.IsNullOrEmpty(title))
                    {
                        Label(new Rect(area.x, y + 2f, area.width, 30f), title, capsStyle, UiKit.Shu);
                        kit.Underline(new Rect(area.x, y + 28f, area.width, 12f), UiKit.Ink.WithAlpha(0.3f));
                    }
                    y += headH;
                }
                MenuItem item = items[i];
                bool button = item.ratio == null && item.value == null;
                var row = new Rect(area.x, y, area.width, rowH - 4f);
                if (button)
                {
                    // Buttons sit side by side in one row.
                    bool second = i > 0 && items[i - 1].ratio == null && items[i - 1].value == null;
                    if (second) row.y -= rowH;
                    row.x = second ? area.x + area.width * 0.5f + 10f : area.x;
                    row.width = area.width * 0.5f - 10f;
                    if (!second) y += rowH;
                }
                else y += rowH;
                bool sel = i == menuIndex;
                var ctrl = new Rect(row.x + 450f, row.y, row.width - 470f, row.height);
                bool hover = !game.Launch.Automated && new Rect(row.x - 20f, row.y, row.width + (button ? 0f : 40f), row.height).Contains(e.mousePosition);
                if (hover && e.type == EventType.Repaint && mouseMoved) menuIndex = i;

                if (sel)
                {
                    float sw = button ? row.width + 40f : 470f;
                    kit.Stroke(new Rect(row.x - 30f, row.y - 6f, sw, row.height + 12f), UiKit.Shu.WithAlpha(0.92f), CombatMath.EaseOutCubic(selAnim));
                }
                Color name = sel ? UiKit.PaperText : UiKit.Ink;
                Color text = UiKit.Ink;
                Color soft = UiKit.InkSoft;
                if (button)
                {
                    Label(row, item.label, settingButtonStyle, name);
                }
                else
                {
                    Label(new Rect(row.x + 20f, row.y, 430f, row.height), item.label, settingLabelStyle, name);
                }

                if (item.ratio != null && item.toggle)
                {
                    // A switch: an ink circle, filled when on.
                    bool on = item.ratio() > 0.5f;
                    var dot = new Rect(ctrl.x, ctrl.center.y - 15f, 30f, 30f);
                    kit.Enso(dot, text.WithAlpha(0.9f));
                    if (on) kit.Dab(new Rect(dot.x + 7f, dot.y + 7f, 16f, 16f), UiKit.Shu);
                    Label(new Rect(dot.xMax + 14f, row.y, 220f, row.height), on ? Strings.On : Strings.Off, settingValueLeftStyle, on ? text : soft);
                }
                else if (item.ratio != null)
                {
                    // An ink slider; click or drag along it.
                    var bar = new Rect(ctrl.x, ctrl.center.y - 11f, ctrl.width - 110f, 22f);
                    float k = Mathf.Clamp01(item.ratio());
                    kit.Bar(bar, k, UiKit.Shu, UiKit.Ink.WithAlpha(0.22f));
                    float knob = sel ? 28f : 22f;
                    kit.Dab(new Rect(bar.x + bar.width * k - knob * 0.5f, bar.center.y - knob * 0.5f, knob, knob), UiKit.Ink);
                    Label(new Rect(bar.xMax + 10f, row.y, 100f, row.height), item.value(), settingValueStyle, text);
                    var grab = new Rect(bar.x - 12f, row.y, bar.width + 24f, row.height);
                    if (!game.Launch.Automated && item.setRatio != null && grab.Contains(e.mousePosition) &&
                        (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                    {
                        menuIndex = i;
                        item.setRatio(Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width));
                        e.Use();
                        continue;
                    }
                }
                else if (item.value != null)
                {
                    // A choice: ‹ value ›, the arrows step it.
                    Label(ctrl, "‹    " + item.value() + "    ›", settingValueCenterStyle, text);
                    if (!game.Launch.Automated && item.adjust != null && e.type == EventType.MouseDown && e.button == 0 && ctrl.Contains(e.mousePosition))
                    {
                        menuIndex = i;
                        item.adjust(e.mousePosition.x < ctrl.center.x ? -1 : 1);
                        game.Audio.Play(Sfx.UiMove, 0.5f, 1.1f, 0f);
                        e.Use();
                        continue;
                    }
                }

                bool clickActivates = item.ratio == null || item.toggle;
                if (hover && clickActivates && e.type == EventType.MouseDown && e.button == 0)
                {
                    menuIndex = i;
                    e.Use();
                    Activate(i);
                    return;
                }
            }
        }

        // ── Credits ──────────────────────────────────────────────────────────────────

        void DrawCredits()
        {
            var panel = new Rect(170f, 40f, 1580f, 1000f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 34f, panel.width, 90f), Strings.CreditsTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 420f);
            Label(new Rect(panel.x, panel.y + 128f, panel.width, 34f), "Shura  ·  v" + Application.version, taglineSmallStyle, UiKit.InkSoft);

            // Left: who made it.
            float lx = panel.x + 130f, lw = 600f, y = panel.y + 200f;
            y = CreditBlock(lx, y, lw, Strings.CreditDirectionTitle, Strings.CreditDirection, true);
            y = CreditBlock(lx, y, lw, Strings.CreditCodeTitle, Strings.CreditCode, false);
            y = CreditBlock(lx, y, lw, Strings.CreditEngineTitle, Strings.CreditEngine, false);
            CreditBlock(lx, y, lw, Strings.CreditInspirationTitle, Strings.CreditInspiration, false);

            // Right: what it is made of.
            DrawRect(new Rect(panel.x + 770f, panel.y + 210f, 2f, 620f), UiKit.Ink.WithAlpha(0.18f));
            float rx = panel.x + 840f, rw = 620f;
            y = panel.y + 200f;
            y = CreditBlock(rx, y, rw, Strings.CreditArtTitle, Strings.CreditArt, false);
            y = CreditBlock(rx, y, rw, Strings.CreditMusicTitle, Strings.CreditMusic, false);
            y = CreditBlock(rx, y, rw, Strings.CreditSoundTitle, Strings.CreditSound, false);
            y = CreditBlock(rx, y, rw, Strings.CreditFontsTitle, Strings.CreditFonts, false);
            Label(new Rect(rx, y, rw, 30f), Strings.CreditFull, creditSmallStyle, UiKit.InkSoft);

            kit.Seal(new Rect(panel.xMax - 170f, panel.yMax - 190f, 100f, 100f), 0.9f);
            DrawMenuList(panel.yMax - 120f, 70f, 360f, panel.center.x, true);
        }

        float CreditBlock(float x, float y, float w, string title, string body, bool big)
        {
            Label(new Rect(x, y, w, 30f), title, capsStyle, UiKit.Shu);
            GUIStyle style = big ? creditNameStyle : creditBodyStyle;
            float h = style.CalcHeight(new GUIContent(body), w);
            Label(new Rect(x, y + 32f, w, h), body, style, UiKit.Ink);
            return y + 32f + h + 30f;
        }

        GUIStyle routeGlyphStyle, routeNodeStyle, routeNodeSmallStyle, routeTextStyle, settingLabelStyle, settingValueStyle, settingValueLeftStyle,
                 settingValueCenterStyle, settingButtonStyle, settingHelpStyle, creditNameStyle, creditBodyStyle, creditSmallStyle;

        void EnsureScreenStyles(Font brush, Font text, Font bold)
        {
            routeGlyphStyle = MakeStyle(84, TextAnchor.MiddleCenter, brush);
            routeNodeStyle = MakeStyle(46, TextAnchor.MiddleCenter, brush);
            routeNodeSmallStyle = MakeStyle(38, TextAnchor.MiddleCenter, brush);
            routeTextStyle = MakeStyle(22, TextAnchor.UpperLeft, text);
            routeTextStyle.wordWrap = true;
            settingLabelStyle = MakeStyle(28, TextAnchor.MiddleLeft, bold);
            settingValueStyle = MakeStyle(25, TextAnchor.MiddleRight, bold);
            settingValueLeftStyle = MakeStyle(25, TextAnchor.MiddleLeft, bold);
            settingValueCenterStyle = MakeStyle(25, TextAnchor.MiddleCenter, bold);
            settingButtonStyle = MakeStyle(28, TextAnchor.MiddleCenter, bold);
            settingHelpStyle = MakeStyle(23, TextAnchor.UpperLeft, text);
            settingHelpStyle.wordWrap = true;
            creditNameStyle = MakeStyle(46, TextAnchor.UpperLeft, brush);
            creditNameStyle.wordWrap = true;
            creditBodyStyle = MakeStyle(24, TextAnchor.UpperLeft, text);
            creditBodyStyle.wordWrap = true;
            creditSmallStyle = MakeStyle(20, TextAnchor.UpperLeft, text, FontStyle.Italic);
        }
    }
}
