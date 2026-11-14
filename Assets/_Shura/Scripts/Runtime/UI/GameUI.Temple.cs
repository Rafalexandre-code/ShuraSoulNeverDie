using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Phase 4 screens (design doc §7): the ruined temple and its constructions, the
    /// Altar's tree, the Zen Garden's oaths and cosmetics, the Library, the Dojo, the
    /// Lineage's graveyard and the choice of an heir. Every button is a "slot": the
    /// arrows / stick move to the nearest slot in that direction, the mouse hovers and clicks.
    /// </summary>
    public sealed partial class GameUI
    {
        enum TempleView
        {
            Hall,
            Dojo,
            Library,
            Garden,
            Graveyard,
            TeaHouse,
            RivalWall,
        }

        sealed class UiSlot
        {
            public Rect r;
            public Action act;
            public Action<int> adjust;
        }

        TempleView templeView;
        readonly List<UiSlot> uiSlots = new List<UiSlot>();
        int slotIndex;
        int lastTempleSlot;
        Action slotBack;
        Action pendingSlotClick;
        /// <summary>The highlighted row of a long list (Library, Graveyard).</summary>
        int listIndex;
        int listCount;
        bool editingSeed;
        string seedBuffer = string.Empty;
        GUIStyle templeKanjiStyle, templeKanjiSmallStyle, jiseiStyle, templeNameStyle;

        // ── Slots: spatial navigation shared by every temple screen ──────────────────

        void BeginSlots(Action back)
        {
            uiSlots.Clear();
            slotBack = back;
        }

        /// <summary>Registers a button for this frame; returns whether it is the selected one.</summary>
        bool AddSlot(Rect r, Action act, Action<int> adjust = null)
        {
            int i = uiSlots.Count;
            uiSlots.Add(new UiSlot { r = r, act = act, adjust = adjust });
            Event e = Event.current;
            if (!game.Launch.Automated && !editingSeed && r.Contains(e.mousePosition))
            {
                if (e.type == EventType.Repaint && (e.mousePosition - lastMouse).sqrMagnitude > 1f) slotIndex = i;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    slotIndex = i;
                    e.Use();
                    pendingSlotClick = act ?? (() => { });
                }
            }
            return i == slotIndex;
        }

        /// <summary>A click lands after the frame is drawn (the action may change the screen).</summary>
        void FlushSlotClick()
        {
            if (pendingSlotClick == null) return;
            Action a = pendingSlotClick;
            pendingSlotClick = null;
            game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
            a();
        }

        void HandleSlots(InputService input)
        {
            if (editingSeed) return;
            int n = uiSlots.Count;
            if (input.NavY != 0 && listCount > 0 && ListScreen)
            {
                listIndex = (listIndex - input.NavY + listCount) % listCount;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
                if (input.Cancel) slotBack?.Invoke();
                return;
            }
            if (n == 0)
            {
                if (input.Cancel) slotBack?.Invoke();
                return;
            }
            slotIndex = Mathf.Clamp(slotIndex, 0, n - 1);
            UiSlot cur = uiSlots[slotIndex];
            if (input.NavX != 0 && cur.adjust != null) cur.adjust(input.NavX);
            else if (input.NavX != 0 || input.NavY != 0)
            {
                int next = SpatialStep(slotIndex, new Vector2(input.NavX, -input.NavY).normalized);
                if (next != slotIndex)
                {
                    slotIndex = next;
                    game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
                }
            }
            if (input.Submit)
            {
                game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
                cur.act?.Invoke();
            }
            else if (input.Cancel) slotBack?.Invoke();
        }

        /// <summary>The nearest slot in a direction (straight ahead counts more than sideways).</summary>
        int SpatialStep(int from, Vector2 dir)
        {
            Vector2 c = uiSlots[from].r.center;
            int best = from;
            float bestScore = float.MaxValue;
            for (int j = 0; j < uiSlots.Count; j++)
            {
                if (j == from) continue;
                Vector2 d = uiSlots[j].r.center - c;
                float along = Vector2.Dot(d, dir);
                if (along < 6f) continue;
                float across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                float score = along + across * 2.4f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = j;
                }
            }
            return best;
        }

        /// <summary>The Library and the Graveyard: up / down walk a long list, Esc goes back.</summary>
        bool ListScreen => game.CurrentMode == Game.Mode.Temple && (templeView == TempleView.Library || templeView == TempleView.Graveyard || templeView == TempleView.RivalWall);

        void EnsureTempleStyles()
        {
            if (templeKanjiStyle != null) return;
            templeKanjiStyle = MakeStyle(104, TextAnchor.MiddleCenter, kit.TextBold);
            templeKanjiSmallStyle = MakeStyle(46, TextAnchor.MiddleCenter, kit.TextBold);
            jiseiStyle = MakeStyle(34, TextAnchor.UpperCenter, kit.Brush);
            templeNameStyle = MakeStyle(36, TextAnchor.MiddleCenter, kit.Brush);
        }

        void SelectedStroke(Rect r, bool selected)
        {
            if (selected) kit.Stroke(new Rect(r.x - 16f, r.y - 8f, r.width + 32f, r.height + 16f), UiKit.Shu.WithAlpha(0.92f), CombatMath.EaseOutCubic(selAnim));
        }

        void ButtonSlot(Rect r, string label, Action act, bool enabled = true)
        {
            bool sel = AddSlot(r, act);
            SelectedStroke(r, sel);
            Label(r, label, itemStyle, sel || enabled ? UiKit.PaperText : UiKit.PaperDim);
        }

        /// <summary>The selection on paper: a vermilion wash and a bar on the left that grows in.</summary>
        void PaperSelect(Rect r, bool selected)
        {
            if (!selected) return;
            float grow = CombatMath.EaseOutCubic(selAnim);
            DrawRect(new Rect(r.x - 6f, r.y - 4f, r.width + 12f, r.height + 8f), UiKit.Shu.WithAlpha(0.1f));
            DrawRect(new Rect(r.x - 14f, r.center.y - r.height * 0.5f * grow, 6f, r.height * grow), UiKit.Shu);
        }

        void BackSlot(float y)
        {
            ButtonSlot(new Rect(VW * 0.5f - 200f, y, 400f, 62f), Strings.Back, slotBack);
        }

        void TempleHeader(string title, string subtitle)
        {
            DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.42f));
            Heading(new Rect(0, 8, VW, 96), title, UiKit.PaperText, UiKit.Shu.WithAlpha(0.9f));
            Label(new Rect(140f, 104f, VW - 280f, 40f), subtitle, taglineSmallStyle, UiKit.PaperText);
        }

        void ControlsLine(string text)
        {
            Label(new Rect(0, VH - 46f, VW, 32f), text, smallStyleCenter, UiKit.PaperDim);
        }

        // ── The hall ────────────────────────────────────────────────────────────────

        void OnTempleOpened()
        {
            templeView = TempleView.Hall;
            slotIndex = lastTempleSlot;
            editingSeed = false;
        }

        /// <summary>Screenshot tour: 0 hall, 1 dojo, 2 library, 3 garden, 4 graveyard.</summary>
        public void ShowTempleViewForShots(int v)
        {
            if (v == 0) BackToHall();
            else if ((TempleView)v != templeView) OpenTempleView((TempleView)v);
        }

        void OpenTempleView(TempleView v)
        {
            templeView = v;
            slotIndex = 0;
            listIndex = 0;
            listCount = 0;
        }

        void BackToHall()
        {
            editingSeed = false;
            templeView = TempleView.Hall;
            slotIndex = lastTempleSlot;
            listCount = 0;
        }

        void DrawTemple()
        {
            EnsureTempleStyles();
            switch (templeView)
            {
                case TempleView.Dojo: DrawDojo(); break;
                case TempleView.Library: DrawLibrary(); break;
                case TempleView.Garden: DrawGarden(); break;
                case TempleView.Graveyard: DrawGraveyard(); break;
                case TempleView.TeaHouse: DrawTeaHouse(); break;
                case TempleView.RivalWall: DrawRivalWall(); break;
                default: DrawTempleHall(); break;
            }
            FlushSlotClick();
        }

        void DrawTempleHall()
        {
            MetaProgress m = game.Meta;
            BeginSlots(game.CloseTemple);
            listCount = 0;
            TempleHeader(Strings.TempleTitle, Strings.TempleSubtitle);
            DrawCurrencies(new Rect(VW * 0.5f - 800f, 150f, 1600f, 56f), m);

            const float discW = 410f, cw = 330f, ch = 300f, gap = 18f;
            float total = discW + 36f + 4f * cw + 3f * gap;
            float x0 = VW * 0.5f - total * 0.5f;
            DrawDiscipleCard(new Rect(x0, 232f, discW, ch * 2f + gap), m);

            float gx = x0 + discW + 36f;
            BuildingDef selected = null;
            for (int i = 0; i < Temple.All.Length; i++)
            {
                BuildingDef b = Temple.All[i];
                int index = i;
                var r = new Rect(gx + (i % 4) * (cw + gap), 232f + (i / 4) * (ch + gap), cw, ch);
                bool sel = AddSlot(r, () => EnterBuilding(Temple.All[index], index));
                if (sel) selected = b;
                DrawBuildingCard(r, b, m, sel);
            }

            float by = 232f + 2f * ch + gap + 24f;
            if (selected != null)
            {
                Label(new Rect(140f, by, VW - 280f, 36f), selected.name + "  ·  " + (selected.comingSoon ? selected.soonText : selected.opens), bodyCenterStyle, UiKit.PaperText);
            }
            BackSlot(by + 52f);
            ControlsLine(Strings.TempleControls);
        }

        void DrawBuildingCard(Rect r, BuildingDef b, MetaProgress m, bool sel)
        {
            bool built = Temple.IsBuilt(m, b);
            float lift = sel ? -8f * CombatMath.EaseOutCubic(selAnim) : 0f;
            r.y += lift;
            if (sel) kit.Stroke(new Rect(r.x - 14f, r.y - 10f, r.width + 28f, r.height + 20f), UiKit.Shu.WithAlpha(0.9f), CombatMath.EaseOutCubic(selAnim));
            kit.PaperPanel(r, built ? 1f : 0.72f);
            if (sel) kit.Underline(new Rect(r.x + 40f, r.yMax - 34f, (r.width - 80f) * CombatMath.EaseOutCubic(selAnim), 14f), UiKit.Shu.WithAlpha(0.9f));
            Color ink = built ? UiKit.Ink : UiKit.InkSoft.WithAlpha(0.5f);
            if (built) kit.Enso(new Rect(r.center.x - 70f, r.y + 18f, 140f, 140f), UiKit.Shu.WithAlpha(0.28f), b.kanji.GetHashCode() % 360);
            Label(new Rect(r.x, r.y + 20f, r.width, 136f), b.kanji, templeKanjiStyle, ink);
            if (!built)
            {
                // Ruins: the sign is cracked and half buried.
                UiKit.Rotated(new Vector2(r.center.x, r.y + 88f), -32f, () => DrawRect(new Rect(r.center.x - 76f, r.y + 86f, 152f, 4f), UiKit.InkSoft.WithAlpha(0.5f)));
                UiKit.Rotated(new Vector2(r.center.x, r.y + 96f), 18f, () => DrawRect(new Rect(r.center.x - 50f, r.y + 94f, 100f, 3f), UiKit.InkSoft.WithAlpha(0.4f)));
            }
            FitLabel(new Rect(r.x + 14f, r.y + 158f, r.width - 28f, 44f), b.name, templeNameStyle, sel ? UiKit.Shu : built ? UiKit.Ink : UiKit.InkSoft);
            string state;
            Color stateColor;
            if (built)
            {
                state = Strings.TempleBuiltLabel;
                stateColor = UiKit.GoldInk;
            }
            else if (b.comingSoon)
            {
                state = Strings.TempleSoonLabel;
                stateColor = UiKit.InkSoft;
            }
            else
            {
                state = b.steel > 0 ? string.Format(Strings.TempleCostSteel, b.ecos, b.steel) : string.Format(Strings.TempleCost, b.ecos);
                stateColor = Temple.CanBuild(m, b) ? UiKit.Ink : UiKit.Shu;
            }
            FitLabel(new Rect(r.x + 14f, r.y + 204f, r.width - 28f, 30f), state, smallStyleCenter, stateColor);
            if (!built && !b.comingSoon) FitLabel(new Rect(r.x + 14f, r.y + 238f, r.width - 28f, 30f), Strings.TempleRebuild, capsCenterStyle, sel ? UiKit.Shu : UiKit.InkSoft);
            else if (built) FitLabel(new Rect(r.x + 14f, r.y + 238f, r.width - 28f, 30f), Strings.TempleEnter, capsCenterStyle, sel ? UiKit.Shu : UiKit.InkSoft);
        }

        void EnterBuilding(BuildingDef b, int index)
        {
            lastTempleSlot = index;
            MetaProgress m = game.Meta;
            if (!Temple.IsBuilt(m, b))
            {
                if (b.comingSoon)
                {
                    game.Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                    Toast(b.soonText, UiKit.PaperDim);
                }
                else game.TryBuild(b);
                return;
            }
            switch (b.id)
            {
                case Temple.Altar: game.OpenAltar(); break;
                case Temple.Forge: game.OpenArsenal(); break;
                case Temple.Dojo: OpenTempleView(TempleView.Dojo); break;
                case Temple.Library: OpenTempleView(TempleView.Library); break;
                case Temple.Garden: OpenTempleView(TempleView.Garden); break;
                case Temple.Graveyard: OpenTempleView(TempleView.Graveyard); break;
                case Temple.TeaHouse: OpenTempleView(TempleView.TeaHouse); break;
                case Temple.RivalWall: OpenTempleView(TempleView.RivalWall); break;
            }
        }

        /// <summary>The four persistent currencies and the Karma scale.</summary>
        void DrawCurrencies(Rect r, MetaProgress m)
        {
            kit.InkPanel(r, 0.8f);
            string[] labels = { m.ecos + " " + Strings.CurrencyEcos, m.steel + " " + Strings.CurrencySteel, m.fragments + " " + Strings.CurrencyFragments, m.petals + " " + Strings.CurrencyPetals };
            Color[] colors = { Pal.gold, ColorUtil.Hex("#9FE7FF"), ColorUtil.Hex("#C9A0FF"), ColorUtil.Hex("#FF9EC4") };
            float cw = (r.width - 420f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var cell = new Rect(r.x + 20f + i * cw, r.y, cw, r.height);
                kit.Dab(new Rect(cell.x, cell.center.y - 11f, 22f, 22f), colors[i]);
                FitLabel(new Rect(cell.x + 30f, cell.y, cw - 36f, cell.height), labels[i], smallStyle, colors[i]);
            }
            DrawKarma(new Rect(r.xMax - 390f, r.y + 8f, 370f, r.height - 16f), m.karma);
        }

        void DrawKarma(Rect r, int karma)
        {
            var bar = new Rect(r.x + 74f, r.center.y + 2f, r.width - 170f, 8f);
            DrawRect(bar, Color.black.WithAlpha(0.6f));
            float half = bar.width * 0.5f;
            float f = Mathf.Clamp(karma, -100, 100) / 100f;
            Color shura = Pal.shu, bushido = ColorUtil.Hex("#7FC4FF");
            if (f < 0f) DrawRect(new Rect(bar.center.x + f * half, bar.y, -f * half, bar.height), shura);
            else if (f > 0f) DrawRect(new Rect(bar.center.x, bar.y, f * half, bar.height), bushido);
            DrawRect(new Rect(bar.center.x - 1f, bar.y - 6f, 2f, bar.height + 12f), UiKit.PaperText.WithAlpha(0.7f));
            FitLabel(new Rect(r.x, bar.center.y - 12f, 68f, 24f), Strings.KarmaShura, capsStyle, shura);
            FitLabel(new Rect(bar.xMax + 8f, bar.center.y - 12f, r.xMax - bar.xMax - 8f, 24f), Strings.KarmaBushido, capsStyle, bushido);
            Label(new Rect(bar.x, bar.y - 22f, bar.width, 18f), string.Format(Strings.KarmaLabel, karma), capsCenterStyle, UiKit.PaperDim);
        }

        // ── The disciple ─────────────────────────────────────────────────────────────

        void DrawDiscipleArt(Rect r, Disciple d)
        {
            CharacterArt art = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(game.Settings.playerArt, 0, PlayerController.PartyArt.Length - 1)]);
            if (art == null) return;
            // The years show: the elder the body, the paler the ink.
            float age = d != null ? Mathf.InverseLerp(40f, 80f, d.age) : 0f;
            Color tint = Color.Lerp(Color.white, new Color(0.78f, 0.78f, 0.82f, 1f), age);
            kit.Dab(new Rect(r.x + r.width * 0.2f, r.yMax - r.height * 0.12f, r.width * 0.6f, r.height * 0.1f), new Color(0f, 0f, 0f, 0.35f));
            DrawSpriteTinted(r, art.idle[(int)ArtDir.Right], tint);
        }

        void DrawDiscipleCard(Rect r, MetaProgress m)
        {
            kit.PaperPanel(r);
            Label(new Rect(r.x, r.y + 18f, r.width, 30f), Strings.DiscipleLabel, capsCenterStyle, UiKit.InkSoft);
            Disciple d = m.disciple;
            float y = r.y + 54f;
            DrawDiscipleArt(new Rect(r.center.x - 80f, y, 160f, 160f), d);
            y += 168f;
            if (d == null)
            {
                Label(new Rect(r.x + 30f, y, r.width - 60f, 120f), m.heirs.Count > 0 ? Strings.HeirSubtitleAny : Strings.DiscipleNone, bodyCenterStyle, UiKit.Ink);
                return;
            }
            FitLabel(new Rect(r.x + 20f, y, r.width - 40f, 44f), d.name, templeNameStyle, UiKit.Ink);
            y += 44f;
            Label(new Rect(r.x + 20f, y, r.width - 40f, 28f), d.generation + "ª geração  ·  " + string.Format(Strings.HeirAge, d.age, Lineage.BandName(d.age)), smallStyleCenter, UiKit.Shu);
            y += 30f;
            float bh = hintSmallStyle.CalcHeight(new GUIContent(Lineage.BandText(d.age)), r.width - 52f);
            Label(new Rect(r.x + 26f, y, r.width - 52f, bh), Lineage.BandText(d.age), hintSmallStyle, UiKit.InkSoft);
            y += bh + 10f;
            y = DrawTraits(new Rect(r.x + 26f, y, r.width - 52f, 200f), d);
            if (!string.IsNullOrEmpty(d.technique))
            {
                RewardDef t = RewardRegistry.Create(d.technique);
                if (t != null)
                {
                    FitLabel(new Rect(r.x + 26f, y, r.width - 52f, 26f), string.Format(Strings.DiscipleInherits, t.Name), smallStyle, UiKit.GoldInk);
                    y += 28f;
                }
            }
            FitLabel(new Rect(r.x + 26f, y, r.width - 52f, 26f), string.Format(Strings.DiscipleJourneys, d.journeys), smallStyle, UiKit.InkSoft);
            y += 28f;
            if (!game.Settings.unlockAll && !Unlocks.Open(m.runs, Unlockable.Lineage))
            {
                Label(new Rect(r.x + 26f, y, r.width - 52f, 76f), string.Format(Strings.LineageLocked, Unlocks.RunsFor(Unlockable.Lineage)), hintSmallStyle, UiKit.Shu);
                return;
            }
            int senseis = Lineage.Senseis(m);
            if (senseis > 0) Label(new Rect(r.x + 26f, y, r.width - 52f, 52f), string.Format(Strings.DiscipleSenseis, senseis, 6 * senseis, 2 * senseis), hintSmallStyle, UiKit.InkSoft);
        }

        /// <summary>Each trait's name and what it does; returns the y below the last one.</summary>
        float DrawTraits(Rect r, Disciple d)
        {
            float y = r.y;
            if (d.traits == null || d.traits.Count == 0)
            {
                Label(new Rect(r.x, y, r.width, 26f), "Sem traços", smallStyle, UiKit.InkSoft);
                return y + 30f;
            }
            foreach (string id in d.traits)
            {
                TraitDef t = Lineage.Trait(id);
                if (t == null) continue;
                FitLabel(new Rect(r.x, y, r.width, 26f), t.name, smallStyle, UiKit.Ai);
                y += 26f;
                float h = hintSmallStyle.CalcHeight(new GUIContent(t.text), r.width);
                Label(new Rect(r.x, y, r.width, h), t.text, hintSmallStyle, UiKit.Ink);
                y += h + 10f;
            }
            return y;
        }

        // ── The Altar's tree ─────────────────────────────────────────────────────────

        void DrawAltar()
        {
            EnsureTempleStyles();
            MetaProgress m = game.Meta;
            BeginSlots(game.CloseAltar);
            listCount = 0;
            var panel = new Rect(VW * 0.5f - 920f, 12f, 1840f, 1056f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 22f, panel.width, 84f), Strings.AltarTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            Label(new Rect(panel.x + 80f, panel.y + 108f, panel.width - 160f, 34f), Strings.AltarTreeSubtitle, bodyCenterStyle, UiKit.InkSoft);
            int used = AltarTree.PointsUsed(m), limit = AltarTree.PointsLimit(m);
            string top = m.ecos + " " + Strings.CurrencyEcos + "   ·   " + m.fragments + " " + Strings.CurrencyFragments + "   ·   " + string.Format(Strings.AltarPoints, used, limit);
            Label(new Rect(panel.x, panel.y + 146f, panel.width, 44f), top, cardKindStyle, used >= limit ? UiKit.Shu : UiKit.GoldInk);
            Label(new Rect(panel.x, panel.y + 188f, panel.width, 24f), string.Format(Strings.AltarPointsHint, AltarTree.MaxPoints), capsCenterStyle, UiKit.InkSoft);

            string description = null;
            const float colW = 540f, colGap = 50f;
            float x0 = VW * 0.5f - (3f * colW + 2f * colGap) * 0.5f;
            for (int c = 0; c < AltarTree.Branches.Length; c++)
            {
                AltarBranch br = AltarTree.Branches[c];
                float cx = x0 + c * (colW + colGap);
                string d = DrawBranch(br, new Rect(cx, panel.y + 222f, colW, 690f), m);
                if (d != null) description = d;
            }

            float by = panel.y + 900f;
            kit.Underline(new Rect(VW * 0.5f - 520f, by, 1040f, 14f), UiKit.Ink.WithAlpha(0.4f));
            if (description != null) Label(new Rect(panel.x + 100f, by + 14f, panel.width - 200f, 34f), description, bodyCenterStyle, UiKit.Ink);
            var reset = new Rect(VW * 0.5f - 470f, by + 50f, 420f, 48f);
            bool rs = AddSlot(reset, game.ResetAltar);
            PaperSelect(reset, rs);
            Label(reset, Strings.AltarResetLabel, itemStyle, rs ? UiKit.Shu : UiKit.InkSoft);
            var back = new Rect(VW * 0.5f + 50f, by + 50f, 420f, 48f);
            bool bs = AddSlot(back, game.CloseAltar);
            PaperSelect(back, bs);
            Label(back, Strings.Back, itemStyle, bs ? UiKit.Shu : UiKit.Ink);
            if (rs) description = null;
            Label(new Rect(panel.x, by + 104f, panel.width, 24f), Strings.AltarTreeControls, capsCenterStyle, UiKit.InkSoft);
            FlushSlotClick();
        }

        /// <summary>One branch: its end on top, its roots below. Returns the selected node's text.</summary>
        string DrawBranch(AltarBranch br, Rect col, MetaProgress m)
        {
            string selectedText = null;
            Color bc = UiKit.OnPaper(br.color);
            Label(new Rect(col.x, col.y - 6f, 90f, 70f), br.kanji, templeKanjiSmallStyle, bc);
            Label(new Rect(col.x + 84f, col.y, col.width - 90f, 56f), br.name.ToUpperInvariant(), cardNameLeftStyle, bc);
            int levels = AltarTree.BranchLevels(m, br);

            // The trunk.
            float trunkX = col.x + 46f;
            DrawRect(new Rect(trunkX - 2f, col.y + 150f, 4f, col.height - 200f), bc.WithAlpha(0.45f));

            // The end of the branch: a rule.
            AltarNode node = br.node;
            bool owned = m.HasNode(node.id);
            bool ready = AltarTree.NodeReady(m, br);
            var nr = new Rect(col.x, col.y + 68f, col.width, 170f);
            bool sel = AddSlot(nr, () =>
            {
                if (m.HasNode(br.node.id)) return;
                game.TryOpenNode(br);
            });
            if (sel) selectedText = node.text;
            PaperSelect(nr, sel);
            DrawRect(nr, (owned ? br.color : UiKit.Ink).WithAlpha(owned ? 0.16f : 0.05f));
            var enso = new Rect(nr.x + 6f, nr.y + 40f, 84f, 84f);
            kit.Enso(enso, (owned ? bc : UiKit.InkSoft).WithAlpha(owned ? 1f : ready ? 0.7f : 0.3f), 30f);
            if (owned) kit.Dab(new Rect(enso.x + 24f, enso.y + 24f, 36f, 36f), bc);
            Color nameColor = sel ? UiKit.Shu : owned ? bc : UiKit.Ink;
            FitLabel(new Rect(nr.x + 104f, nr.y + 8f, nr.width - 112f, 44f), node.name, cardNameLeftStyle, nameColor);
            Label(new Rect(nr.x + 104f, nr.y + 54f, nr.width - 112f, 60f), node.text, hintSmallStyle, sel ? UiKit.Shu : UiKit.Ink);
            string state = owned ? Strings.AltarNodeOwned : string.Format(Strings.AltarNodeNeeds, br.required, node.ecos, node.fragments);
            Color stateColor = owned ? UiKit.GoldInk : AltarTree.CanOpen(m, br) ? UiKit.Ink : ready ? UiKit.InkSoft : UiKit.Shu.WithAlpha(0.8f);
            if (!owned) state += "   (" + Mathf.Min(levels, br.required) + "/" + br.required + ")";
            FitLabel(new Rect(nr.x + 104f, nr.y + 122f, nr.width - 112f, 30f), state, smallStyle, sel ? UiKit.Shu : stateColor);

            // The roots: the first upgrade at the bottom.
            int n = br.upgrades.Length;
            for (int k = 0; k < n; k++)
            {
                MetaUpgradeDef def = MetaUpgrades.Get(br.upgrades[k]);
                if (def == null) continue;
                var r = new Rect(col.x, col.y + 280f + (n - 1 - k) * 128f, col.width, 112f);
                MetaUpgradeDef captured = def;
                bool s = AddSlot(r, () => BuyUpgrade(captured));
                if (s) selectedText = def.description;
                DrawUpgradeNode(r, def, m, s, bc);
            }
            return selectedText;
        }

        void DrawUpgradeNode(Rect r, MetaUpgradeDef def, MetaProgress m, bool sel, Color bc)
        {
            int level = m.GetLevel(def.id);
            bool maxed = m.IsMaxed(def);
            PaperSelect(r, sel);
            DrawRect(r, UiKit.Ink.WithAlpha(0.05f));
            kit.Dab(new Rect(r.x + 28f, r.y + 38f, 36f, 36f), level > 0 ? bc : UiKit.Ink.WithAlpha(0.2f));
            Color ink = sel ? UiKit.Shu : UiKit.Ink;
            FitLabel(new Rect(r.x + 92f, r.y + 6f, r.width - 260f, 40f), def.name, itemLeftStyle, ink);
            for (int j = 0; j < def.maxLevel; j++)
            {
                var pr = new Rect(r.x + 94f + j * 30f, r.y + 50f, 24f, 24f);
                kit.Dab(pr, j < level ? UiKit.Shu : UiKit.Ink.WithAlpha(0.16f));
            }
            string effect = level > 0 ? def.EffectAt(level) : "—";
            if (!maxed) effect += "  ›  " + def.EffectAt(level + 1);
            FitLabel(new Rect(r.x + 92f, r.y + 78f, r.width - 100f, 28f), effect, smallStyle, sel ? UiKit.Shu : UiKit.InkSoft);
            string cost = maxed ? Strings.Max : string.Format(Strings.CostLabel, m.NextCost(def));
            Color costColor = maxed ? UiKit.GoldInk : m.CanBuy(def) ? UiKit.Ink : UiKit.Shu;
            Label(new Rect(r.xMax - 180f, r.y + 6f, 170f, 40f), cost, bodyRightStyle, sel ? UiKit.Shu : costColor);
        }

        void BuyUpgrade(MetaUpgradeDef def)
        {
            MetaProgress m = game.Meta;
            if (!m.IsMaxed(def) && AltarTree.PointsFree(m) < 1)
            {
                game.Audio.Play(Sfx.Denied, 0.7f, 1f, 0f);
                Toast(Strings.AltarNoPoints, Pal.danger);
                return;
            }
            game.TryBuyUpgrade(def);
        }

        // ── The Zen Garden ───────────────────────────────────────────────────────────

        void DrawGarden()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            listCount = 0;
            TempleHeader(Strings.GardenTitle, Strings.GardenSubtitle);

            // The oaths.
            var left = new Rect(VW * 0.5f - 900f, 156f, 940f, 762f);
            kit.PaperPanel(left);
            Label(new Rect(left.x, left.y + 14f, left.width, 34f), Strings.GardenOaths, capsCenterStyle, UiKit.InkSoft);
            string help = null;
            float y = left.y + 52f;
            bool oathsOpen = game.Settings.unlockAll || Unlocks.Open(m.runs, Unlockable.Oaths);
            if (!oathsOpen) Label(new Rect(left.x + 40f, left.y + 52f, left.width - 80f, 40f), string.Format(Strings.OathsLocked, Unlocks.RunsFor(Unlockable.Oaths)), bodyCenterStyle, UiKit.Shu);
            foreach (OathDef o in Oaths.All)
            {
                if (!oathsOpen) break;
                OathDef oath = o;
                var r = new Rect(left.x + 30f, y, left.width - 60f, 58f);
                bool sel = AddSlot(r, () => game.SetOath(oath, (m.OathLevel(oath.id) + 1) % (oath.maxLevel + 1)),
                                   dir => game.SetOath(oath, m.OathLevel(oath.id) + dir));
                DrawOathRow(r, o, m.OathLevel(o.id), sel);
                if (sel) help = o.sealedUntilRivals ? Strings.GardenSealed : o.levels[Mathf.Clamp(m.OathLevel(o.id) - 1, 0, o.maxLevel - 1)];
                y += 64f;
            }
            int heat = Oaths.Heat(m);
            Label(new Rect(left.x, left.yMax - 56f, left.width, 44f), string.Format(Strings.GardenHeat, heat, Oaths.EcosMult(heat)), cardKindStyle, heat > 0 ? UiKit.Shu : UiKit.InkSoft);

            // The seed, the blade colours, the ornaments.
            var right = new Rect(left.xMax + 30f, 156f, 830f, 762f);
            kit.PaperPanel(right);
            var seedRow = new Rect(right.x + 30f, right.y + 20f, right.width - 60f, 70f);
            bool ss = AddSlot(seedRow, BeginSeedEdit);
            PaperSelect(seedRow, ss);
            Label(new Rect(seedRow.x + 10f, seedRow.y, 330f, 40f), Strings.GardenSeed, itemLeftStyle, ss ? UiKit.Shu : UiKit.Ink);
            string seed = editingSeed ? seedBuffer + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "|" : " ") : string.IsNullOrEmpty(m.seed) ? Strings.GardenSeedRandom : m.seed;
            FitLabel(new Rect(seedRow.x + 350f, seedRow.y, seedRow.width - 360f, 40f), seed, bodyRightStyle, ss ? UiKit.Shu : UiKit.Ai);
            Label(new Rect(seedRow.x + 10f, seedRow.y + 40f, seedRow.width - 20f, 26f), Strings.GardenSeedHint, capsStyle, ss ? UiKit.InkSoft : UiKit.InkSoft);
            if (ss) help = Strings.GardenSeedHint;

            Label(new Rect(right.x, right.y + 104f, right.width, 40f), m.petals + " " + Strings.CurrencyPetals, cardKindStyle, UiKit.OnPaper(ColorUtil.Hex("#FF9EC4")));
            Label(new Rect(right.x, right.y + 150f, right.width, 28f), Strings.GardenColors, capsCenterStyle, UiKit.InkSoft);
            float cw = (right.width - 60f) / Garden.Colors.Length;
            for (int i = 0; i < Garden.Colors.Length; i++)
            {
                BladeColorDef c = Garden.Colors[i];
                var r = new Rect(right.x + 30f + i * cw + 6f, right.y + 184f, cw - 12f, 150f);
                bool sel = AddSlot(r, () => game.TryBuyColor(c));
                PaperSelect(r, sel);
                bool own = m.colors.Contains(c.id), worn = m.bladeColor == c.id;
                kit.Dab(new Rect(r.center.x - 34f, r.y + 8f, 68f, 68f), c.color);
                if (worn) kit.Enso(new Rect(r.center.x - 44f, r.y - 2f, 88f, 88f), UiKit.Ink, 10f);
                FitLabel(new Rect(r.x + 2f, r.y + 82f, r.width - 4f, 30f), c.name, smallStyleCenter, sel ? UiKit.Shu : UiKit.Ink);
                string price = worn ? Strings.GardenWorn : own ? Strings.GardenOwned : string.Format(Strings.GardenPrice, c.petals);
                FitLabel(new Rect(r.x + 2f, r.y + 112f, r.width - 4f, 26f), price, capsCenterStyle, worn ? UiKit.Shu : own ? UiKit.GoldInk : m.petals >= c.petals ? UiKit.Ink : UiKit.InkSoft);
            }

            Label(new Rect(right.x, right.y + 352f, right.width, 28f), Strings.GardenOrnaments, capsCenterStyle, UiKit.InkSoft);
            float ow = (right.width - 60f) / Garden.Ornaments.Length;
            for (int i = 0; i < Garden.Ornaments.Length; i++)
            {
                OrnamentDef o = Garden.Ornaments[i];
                var r = new Rect(right.x + 30f + i * ow + 6f, right.y + 386f, ow - 12f, 150f);
                bool sel = AddSlot(r, () => game.TryBuyOrnament(o));
                PaperSelect(r, sel);
                bool own = m.garden.Contains(o.id);
                Label(new Rect(r.x, r.y + 4f, r.width, 76f), o.kanji, templeKanjiSmallStyle, own ? UiKit.Ink : UiKit.InkSoft.WithAlpha(0.45f));
                FitLabel(new Rect(r.x + 2f, r.y + 82f, r.width - 4f, 30f), o.name, smallStyleCenter, sel ? UiKit.Shu : UiKit.Ink);
                FitLabel(new Rect(r.x + 2f, r.y + 112f, r.width - 4f, 26f), own ? Strings.GardenOwned : string.Format(Strings.GardenPrice, o.petals), capsCenterStyle,
                         own ? UiKit.GoldInk : m.petals >= o.petals ? UiKit.Ink : UiKit.InkSoft);
            }

            // The garden itself: raked sand and whatever was bought.
            var sand = new Rect(right.x + 40f, right.y + 552f, right.width - 80f, 176f);
            DrawRect(sand, ColorUtil.Hex("#E9DFC9").WithAlpha(0.85f));
            for (int k = 0; k < 7; k++) DrawRect(new Rect(sand.x + 10f, sand.y + 14f + k * 24f, sand.width - 20f, 2f), UiKit.InkSoft.WithAlpha(0.18f));
            int shown = 0;
            foreach (OrnamentDef o in Garden.Ornaments)
            {
                if (!m.garden.Contains(o.id)) continue;
                float gx = sand.x + 40f + shown * (sand.width - 120f) / 5f;
                float gy = sand.y + 14f + (shown % 2) * 64f;
                Label(new Rect(gx, gy, 90f, 90f), o.kanji, templeKanjiSmallStyle, UiKit.Ink);
                shown++;
            }

            if (help != null) Label(new Rect(140f, left.yMax + 6f, VW - 280f, 34f), help, bodyCenterStyle, UiKit.PaperText);
            BackSlot(left.yMax + 44f);
            ControlsLine(Strings.GardenControls);
        }

        void DrawOathRow(Rect r, OathDef o, int level, bool sel)
        {
            PaperSelect(r, sel);
            bool sealedOath = o.sealedUntilRivals;
            Color ink = sel ? UiKit.Shu : sealedOath ? UiKit.InkSoft.WithAlpha(0.6f) : UiKit.Ink;
            Label(new Rect(r.x, r.y, 64f, r.height), o.kanji, templeKanjiSmallStyle, level > 0 ? UiKit.Shu : ink);
            FitLabel(new Rect(r.x + 70f, r.y + 4f, 330f, 34f), o.name, itemLeftStyle, ink);
            Label(new Rect(r.x + 70f, r.y + 38f, 330f, 22f), "+" + o.heat + " Calor por nível", capsStyle, sel ? UiKit.InkSoft : UiKit.InkSoft);
            for (int j = 0; j < o.maxLevel; j++)
            {
                var pr = new Rect(r.x + 410f + j * 40f, r.y + 14f, 34f, 34f);
                kit.Enso(pr, (sel ? UiKit.Shu : UiKit.Ink).WithAlpha(0.6f), j * 50f);
                if (j < level) kit.Dab(new Rect(pr.x + 7f, pr.y + 7f, 20f, 20f), UiKit.Shu);
            }
            string text = sealedOath ? Strings.GardenSealed : level > 0 ? o.levels[level - 1] : "—";
            Label(new Rect(r.x + 540f, r.y, r.width - 540f, r.height), text, hintSmallStyle, ink);
        }

        void BeginSeedEdit()
        {
            editingSeed = true;
            seedBuffer = game.Meta.seed ?? string.Empty;
        }

        /// <summary>Keyboard text for the garden's seed (IMGUI key events, in OnGUI).</summary>
        void HandleSeedTyping()
        {
            if (!editingSeed) return;
            if (game.CurrentMode != Game.Mode.Temple || templeView != TempleView.Garden)
            {
                editingSeed = false;
                return;
            }
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                editingSeed = false;
                modeChangedFrame = Time.frameCount;
                game.SetGardenSeed(seedBuffer);
                game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                editingSeed = false;
                modeChangedFrame = Time.frameCount;
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (seedBuffer.Length > 0) seedBuffer = seedBuffer.Substring(0, seedBuffer.Length - 1);
            }
            else if (e.character != '\0' && !char.IsControl(e.character) && seedBuffer.Length < 24 && (char.IsLetterOrDigit(e.character) || e.character == ' ' || e.character == '-'))
            {
                seedBuffer += e.character;
            }
            e.Use();
        }

        // ── Long lists (Library, Graveyard) ──────────────────────────────────────────

        /// <summary>Rows of a list with a scrolling window; a click selects. Returns the first row drawn.</summary>
        void DrawListRows(Rect area, int count, float rowH, Action<Rect, int, bool> drawRow)
        {
            listCount = count;
            if (count == 0) return;
            listIndex = Mathf.Clamp(listIndex, 0, count - 1);
            int visible = Mathf.Max(1, Mathf.FloorToInt(area.height / rowH));
            int first = Mathf.Clamp(listIndex - visible / 2, 0, Mathf.Max(0, count - visible));
            Event e = Event.current;
            for (int i = first; i < Mathf.Min(count, first + visible); i++)
            {
                var r = new Rect(area.x, area.y + (i - first) * rowH, area.width, rowH - 6f);
                if (!game.Launch.Automated && r.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        listIndex = i;
                        e.Use();
                    }
                    else if (e.type == EventType.ScrollWheel)
                    {
                        listIndex = Mathf.Clamp(listIndex + (e.delta.y > 0 ? 1 : -1), 0, count - 1);
                        e.Use();
                    }
                }
                bool sel = i == listIndex;
                PaperSelect(r, sel);
                drawRow(r, i, sel);
            }
            if (first > 0) Label(new Rect(area.x, area.y - 26f, area.width, 22f), "▲", capsCenterStyle, UiKit.InkSoft);
            if (first + visible < count) Label(new Rect(area.x, area.yMax, area.width, 22f), "▼", capsCenterStyle, UiKit.InkSoft);
        }

        // ── The Graveyard ────────────────────────────────────────────────────────────

        void DrawGraveyard()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            TempleHeader(Strings.GraveyardTitle, Strings.GraveyardSubtitle);
            List<Disciple> tombs = m.cemetery;
            var left = new Rect(VW * 0.5f - 900f, 160f, 620f, 800f);
            var right = new Rect(left.xMax + 30f, 160f, 1150f, 800f);
            kit.PaperPanel(left);
            kit.PaperPanel(right);
            if (tombs.Count == 0)
            {
                listCount = 0;
                Label(new Rect(right.x + 40f, right.center.y - 40f, right.width - 80f, 80f), Strings.GraveyardEmpty, bodyCenterStyle, UiKit.InkSoft);
            }
            else
            {
                // The newest tomb first.
                DrawListRows(new Rect(left.x + 30f, left.y + 40f, left.width - 60f, left.height - 80f), tombs.Count, 76f, (r, i, sel) =>
                {
                    Disciple d = tombs[tombs.Count - 1 - i];
                    Color ink = sel ? UiKit.Shu : UiKit.Ink;
                    Label(new Rect(r.x + 4f, r.y, 60f, r.height), d.retired ? "師" : "墓", templeKanjiSmallStyle, d.retired ? UiKit.GoldInk : UiKit.Ink);
                    FitLabel(new Rect(r.x + 70f, r.y + 2f, r.width - 80f, 36f), d.name + "  ·  " + d.generation + "ª", itemLeftStyle, ink);
                    string fate = d.retired ? string.Format(Strings.GraveyardSensei, d.age) : string.Format(Strings.GraveyardFell, d.age, d.bestRoom);
                    FitLabel(new Rect(r.x + 70f, r.y + 38f, r.width - 80f, 28f), fate, smallStyle, sel ? UiKit.InkSoft : UiKit.InkSoft);
                });
                DrawTomb(right, tombs[tombs.Count - 1 - Mathf.Clamp(listIndex, 0, tombs.Count - 1)]);
            }
            BackSlot(left.yMax + 22f);
            ControlsLine(Strings.TempleListControls);
        }

        void DrawTomb(Rect r, Disciple d)
        {
            float y = r.y + 30f;
            Label(new Rect(r.x, y, r.width, 60f), d.name, headerStyle, UiKit.Ink);
            y += 70f;
            Label(new Rect(r.x, y, r.width, 30f), d.generation + "ª geração  ·  " + Lineage.TraitNames(d), smallStyleCenter, UiKit.Ai);
            y += 36f;
            string fate = d.retired ? string.Format(Strings.GraveyardSensei, d.age) : string.Format(Strings.GraveyardFell, d.age, d.bestRoom);
            Label(new Rect(r.x, y, r.width, 30f), fate, smallStyleCenter, d.retired ? UiKit.GoldInk : UiKit.Shu);
            y += 34f;
            if (!string.IsNullOrEmpty(d.cause))
            {
                Label(new Rect(r.x, y, r.width, 30f), string.Format(Strings.GraveyardKilledBy, d.cause), smallStyleCenter, UiKit.InkSoft);
                y += 34f;
            }
            WeaponDef w = string.IsNullOrEmpty(d.weapon) ? null : WeaponDefs.Exists(d.weapon) ? WeaponDefs.Get(d.weapon) : null;
            if (w != null)
            {
                var sw = new Rect(r.center.x - 240f, y + 6f, 480f, 70f);
                kit.Stroke(sw, UiKit.Ink.WithAlpha(0.85f));
                DrawWeaponArt(w, new Rect(sw.x + 80f, sw.y + 12f, sw.width - 160f, 46f), Color.white);
                Label(new Rect(r.x, sw.yMax + 6f, r.width, 30f), string.Format(Strings.GraveyardWeapon, w.name), smallStyleCenter, UiKit.Ink);
                y = sw.yMax + 46f;
            }
            if (!string.IsNullOrEmpty(d.jisei))
            {
                // The death poem, brushed in three lines with the seal beside it.
                var poem = new Rect(r.x + 120f, y + 20f, r.width - 240f, 210f);
                kit.Splat(2, new Rect(poem.center.x - 260f, poem.y - 10f, 520f, 230f), UiKit.Ink.WithAlpha(0.06f));
                Label(poem, d.jisei, jiseiStyle, UiKit.Ink);
                kit.Seal(new Rect(poem.xMax - 10f, poem.yMax - 90f, 74f, 74f), 0.9f);
            }
            else if (d.retired)
            {
                Label(new Rect(r.x + 80f, y + 40f, r.width - 160f, 80f), Lineage.BandText(d.age), bodyCenterStyle, UiKit.InkSoft);
            }
        }

        // ── The Library ──────────────────────────────────────────────────────────────

        static readonly string[] AnswerNames = { "Parry perfeito", "Contra-estocada", "Esquiva perfeita" };

        void DrawLibrary()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            TempleHeader(Strings.LibraryTitle, Strings.LibrarySubtitle);
            var left = new Rect(VW * 0.5f - 900f, 160f, 620f, 800f);
            var right = new Rect(left.xMax + 30f, 160f, 1150f, 800f);
            kit.PaperPanel(left);
            kit.PaperPanel(right);
            EnemyDef[] all = EnemyDefs.All;
            int scrolls = Strings.ScrollCount;
            DrawListRows(new Rect(left.x + 30f, left.y + 40f, left.width - 60f, left.height - 80f), all.Length + scrolls, 60f, (r, i, sel) =>
            {
                Color ink = sel ? UiKit.Shu : UiKit.Ink;
                if (i < all.Length)
                {
                    EnemyDef e = all[i];
                    int kills = m.Kills(e.id);
                    FitLabel(new Rect(r.x + 12f, r.y, r.width - 140f, r.height), kills < 0 ? Strings.LibraryUnknown : e.displayName, itemLeftStyle, kills < 0 ? UiKit.InkSoft : ink);
                    if (kills > 0) Label(new Rect(r.xMax - 130f, r.y, 120f, r.height), kills.ToString(), bodyRightStyle, sel ? UiKit.InkSoft : UiKit.GoldInk);
                }
                else
                {
                    int s = i - all.Length;
                    bool open = m.runs >= Strings.ScrollUnlockRuns(s);
                    FitLabel(new Rect(r.x + 12f, r.y, r.width - 24f, r.height), "巻  " + (open ? "Pergaminho " + (s + 1) : string.Format(Strings.LibraryScrollLocked, Strings.ScrollUnlockRuns(s))), itemLeftStyle,
                             open ? (sel ? UiKit.Shu : UiKit.Ai) : UiKit.InkSoft);
                }
            });
            int idx = Mathf.Clamp(listIndex, 0, all.Length + scrolls - 1);
            if (idx < all.Length) DrawBestiaryPage(right, all[idx], m.Kills(all[idx].id), m);
            else
            {
                int s = idx - all.Length;
                bool open = m.runs >= Strings.ScrollUnlockRuns(s);
                Label(new Rect(right.x, right.y + 40f, right.width, 40f), Strings.LibraryScrolls, capsCenterStyle, UiKit.InkSoft);
                Label(new Rect(right.x, right.y + 90f, right.width, 140f), "巻", templeKanjiStyle, open ? UiKit.Ai : UiKit.InkSoft.WithAlpha(0.4f));
                Label(new Rect(right.x + 120f, right.y + 260f, right.width - 240f, 300f), open ? Strings.Scroll(s) : string.Format(Strings.LibraryScrollLocked, Strings.ScrollUnlockRuns(s)), jiseiStyle, open ? UiKit.Ink : UiKit.InkSoft);
            }
            BackSlot(left.yMax + 22f);
            ControlsLine(Strings.TempleListControls);
        }

        void DrawBestiaryPage(Rect r, EnemyDef e, int kills, MetaProgress m)
        {
            bool known = kills >= 0;
            CharacterArt art = PixelArt.Character(e.art);
            var portrait = new Rect(r.x + 60f, r.y + 60f, 300f, 300f);
            bool legend = known && m.MissionDone(TeaGuests.Warashi) && EnemyScrolls.Done(m, e.id);
            if (legend) DrawSumiE(portrait, e);
            else
            {
                kit.Enso(new Rect(portrait.x - 20f, portrait.y - 20f, 340f, 340f), UiKit.Ink.WithAlpha(0.5f), 20f);
                if (art != null) DrawSpriteTinted(portrait, art.idle[(int)ArtDir.Right], known ? Color.white : new Color(0.05f, 0.05f, 0.06f, 0.85f));
            }
            if (known) DrawScroll(new Rect(r.x + 70f, r.y + 470f, r.width - 140f, 300f), e, m);
            if (legend) Label(new Rect(r.x + 60f, r.y + 400f, r.width - 120f, 64f), EnemyScrolls.Legend(e), hintSmallStyle, UiKit.InkSoft);
            float x = portrait.xMax + 60f, w = r.xMax - x - 50f;
            float y = r.y + 70f;
            Label(new Rect(x, y, w, 70f), known ? e.displayName : Strings.LibraryUnknown, headerStyle, UiKit.Ink);
            y += 84f;
            if (!known)
            {
                Label(new Rect(x, y, w, 40f), Strings.LibraryUnknownText, bodyStyle, UiKit.InkSoft);
                return;
            }
            Label(new Rect(x, y, w, 34f), string.Format(Strings.LibraryKills, kills), bodyStyle, UiKit.GoldInk);
            y += 42f;
            int moves = e.moves != null ? e.moves.Length : 0;
            Label(new Rect(x, y, w, 34f), string.Format(Strings.LibraryStats, Mathf.RoundToInt(e.maxHealth), Mathf.RoundToInt(e.maxPosture), moves), bodyStyle, UiKit.Ink);
            y += 42f;
            Label(new Rect(x, y, w, 34f), string.Format(Strings.LibraryAnswer, AnswerNames[Mathf.Clamp((int)e.signatureAnswer, 0, AnswerNames.Length - 1)]), bodyStyle, UiKit.Ai);
            y += 42f;
            if (!string.IsNullOrEmpty(e.Teaches))
            {
                RewardDef t = RewardRegistry.Create(e.Teaches);
                if (t != null)
                {
                    Label(new Rect(x, y, w, 34f), string.Format(Strings.LibraryTechnique, t.Name), bodyStyle, UiKit.Shu);
                    y += 40f;
                    Label(new Rect(x, y, w, 90f), t.Gain, hintSmallStyle, UiKit.Ink);
                    y += 90f;
                }
            }
            var traits = new List<string>();
            if (e.flying) traits.Add("voa");
            if (e.ethereal) traits.Add("etéreo");
            if (e.unarmed) traits.Add("desarmado");
            if (e.superArmor) traits.Add("não se abala");
            if (e.reassembles) traits.Add("se remonta");
            if (!string.IsNullOrEmpty(e.summonId)) traits.Add("invoca");
            if (e.clones) traits.Add("cria clones");
            traits.Insert(0, string.Format(Strings.LibraryFaction, Factions.Name(Factions.Of(e))));
            if (traits.Count > 0) Label(new Rect(x, y, w, 34f), string.Join("  ·  ", traits), smallStyle, UiKit.InkSoft);
        }

        // ── The Dojo ─────────────────────────────────────────────────────────────────

        void DrawDojo()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            listCount = 0;
            TempleHeader(Strings.DojoTitle, Strings.DojoSubtitle);
            var left = new Rect(VW * 0.5f - 900f, 160f, 1060f, 800f);
            var right = new Rect(left.xMax + 30f, 160f, 710f, 800f);
            kit.PaperPanel(left);
            kit.PaperPanel(right);

            var train = new Rect(left.x + 220f, left.y + 26f, left.width - 440f, 66f);
            bool ts = AddSlot(train, game.StartTutorial);
            PaperSelect(train, ts);
            Label(train, Strings.DojoTrain, itemStyle, ts ? UiKit.Shu : UiKit.Ink);

            Label(new Rect(left.x, left.y + 108f, left.width, 30f), Strings.DojoMastery, capsCenterStyle, UiKit.InkSoft);
            WeaponDef[] blades = WeaponDefs.All;
            float rowH = Mathf.Min(62f, (left.height - 170f) / Mathf.Max(1, blades.Length));
            for (int i = 0; i < blades.Length; i++)
            {
                var r = new Rect(left.x + 40f, left.y + 146f + i * rowH, left.width - 80f, rowH - 6f);
                DrawWeaponArt(blades[i], new Rect(r.x, r.y + 6f, 150f, rowH - 18f), Color.white);
                FitLabel(new Rect(r.x + 170f, r.y, 280f, rowH - 6f), blades[i].name, itemLeftStyle, UiKit.Ink);
                DrawMasteryRow(blades[i], new Rect(r.x + 470f, r.y + (rowH - 30f) * 0.5f, r.width - 480f, 24f));
            }

            Label(new Rect(right.x, right.y + 30f, right.width, 30f), Strings.DojoSenseis, capsCenterStyle, UiKit.InkSoft);
            float y = right.y + 74f;
            int n = 0;
            for (int i = m.cemetery.Count - 1; i >= 0 && y < right.yMax - 90f; i--)
            {
                Disciple d = m.cemetery[i];
                if (!d.retired) continue;
                n++;
                Label(new Rect(right.x + 30f, y, 70f, 70f), "師", templeKanjiSmallStyle, UiKit.GoldInk);
                FitLabel(new Rect(right.x + 110f, y + 2f, right.width - 140f, 36f), d.name + "  ·  " + d.generation + "ª geração", itemLeftStyle, UiKit.Ink);
                FitLabel(new Rect(right.x + 110f, y + 38f, right.width - 140f, 28f), Lineage.TraitNames(d) + "  ·  +6 vida, +2% dano", smallStyle, UiKit.InkSoft);
                y += 84f;
            }
            if (n == 0) Label(new Rect(right.x + 40f, y + 20f, right.width - 80f, 80f), Strings.DojoNoSenseis, bodyCenterStyle, UiKit.InkSoft);
            BackSlot(left.yMax + 22f);
            ControlsLine(Strings.TempleControls);
        }

        // ── The heir ─────────────────────────────────────────────────────────────────

        void DrawHeir()
        {
            EnsureTempleStyles();
            MetaProgress m = game.Meta;
            BeginSlots(game.GoToMainMenu);
            listCount = 0;
            LegacyReport last = game.LastLegacy;
            string sub = last?.disciple == null ? Strings.HeirSubtitleAny
                : last.retired ? string.Format(Strings.HeirSubtitleRetired, last.disciple.name)
                : last.died ? string.Format(Strings.HeirSubtitle, last.disciple.name) : Strings.HeirSubtitleAny;
            TempleHeader(Strings.HeirTitle, sub);
            List<Disciple> heirs = m.heirs;
            const float cw = 520f, gap = 40f;
            float x0 = VW * 0.5f - (heirs.Count * cw + (heirs.Count - 1) * gap) * 0.5f;
            for (int i = 0; i < heirs.Count; i++)
            {
                int index = i;
                Disciple d = heirs[i];
                var r = new Rect(x0 + i * (cw + gap), 190f, cw, 760f);
                bool sel = AddSlot(r, () => game.ChooseHeir(index));
                if (sel) r.y -= 10f * CombatMath.EaseOutCubic(selAnim);
                if (sel) kit.Stroke(new Rect(r.x - 16f, r.y - 10f, r.width + 32f, r.height + 20f), UiKit.Shu.WithAlpha(0.9f), CombatMath.EaseOutCubic(selAnim));
                kit.PaperPanel(r, sel ? 1f : 0.9f);
                DrawDiscipleArt(new Rect(r.center.x - 100f, r.y + 24f, 200f, 200f), d);
                FitLabel(new Rect(r.x + 20f, r.y + 230f, r.width - 40f, 54f), d.name, headerStyle, UiKit.Ink);
                Label(new Rect(r.x + 20f, r.y + 290f, r.width - 40f, 30f), string.Format(Strings.HeirAge, d.age, Lineage.BandName(d.age)) + "  ·  " + d.generation + "ª geração", smallStyleCenter, UiKit.Shu);
                float bh = hintSmallStyle.CalcHeight(new GUIContent(Lineage.BandText(d.age)), r.width - 80f);
                Label(new Rect(r.x + 40f, r.y + 330f, r.width - 80f, bh), Lineage.BandText(d.age), hintSmallStyle, UiKit.InkSoft);
                float y = DrawTraits(new Rect(r.x + 40f, r.y + 342f + bh, r.width - 80f, 220f), d);
                if (!string.IsNullOrEmpty(d.technique))
                {
                    RewardDef t = RewardRegistry.Create(d.technique);
                    if (t != null) FitLabel(new Rect(r.x + 40f, y + 6f, r.width - 80f, 28f), string.Format(Strings.DiscipleInherits, t.Name), smallStyle, UiKit.GoldInk);
                }
                if (sel) kit.Underline(new Rect(r.x + 60f, r.yMax - 40f, r.width - 120f, 14f), UiKit.Shu.WithAlpha(0.8f));
            }
            ControlsLine(Strings.HeirControls);
            FlushSlotClick();
        }

        // ── The end screen: what the journey left ────────────────────────────────────

        void DrawLegacyPanel(Rect r)
        {
            EnsureTempleStyles();
            LegacyReport l = game.LastLegacy;
            if (l == null) return;
            kit.PaperPanel(r);
            Label(new Rect(r.x, r.y + 30f, r.width, 26f), Strings.LegacyTitle, capsCenterStyle, UiKit.InkSoft);
            float y = r.y + 64f;
            void Line(string text, Color c)
            {
                FitLabel(new Rect(r.x + 30f, y, r.width - 60f, 30f), text, smallStyleCenter, c);
                y += 32f;
            }
            if (l.steel > 0) Line(string.Format(Strings.LegacySteel, l.steel), UiKit.OnPaper(ColorUtil.Hex("#9FE7FF")));
            if (l.fragments > 0) Line(string.Format(Strings.LegacyFragments, l.fragments), UiKit.OnPaper(ColorUtil.Hex("#C9A0FF")));
            if (l.petals > 0) Line(string.Format(Strings.LegacyPetals, l.petals), UiKit.OnPaper(ColorUtil.Hex("#FF9EC4")));
            if (l.karma != 0) Line(string.Format(Strings.LegacyKarma, l.karma), l.karma > 0 ? UiKit.Ai : UiKit.Shu);
            if (l.heat > 0) Line(string.Format(Strings.LegacyHeat, l.heat), UiKit.Shu);
            if (!string.IsNullOrEmpty(l.unlocked)) Line(string.Format(Strings.LegacyUnlocked, l.unlocked), UiKit.Shu);
            if (!string.IsNullOrEmpty(l.rival))
            {
                string t = string.Format(l.rivalPromoted ? Strings.LegacyRivalPromoted : Strings.LegacyRival, l.rival, l.rivalEcos);
                float h = hintSmallStyle.CalcHeight(new GUIContent(t), r.width - 48f);
                Label(new Rect(r.x + 24f, y, r.width - 48f, h), t, hintSmallStyle, UiKit.Shu);
                y += h + 6f;
            }
            y += 8f;
            Disciple d = l.disciple;
            if (d == null) return;
            string fate = l.died ? string.Format(Strings.LegacyDied, d.name, d.age)
                : l.retired ? string.Format(Strings.LegacyRetired, d.name, d.age)
                : l.aged ? string.Format(Strings.LegacyAged, d.name, d.age)
                : string.Format(Strings.LegacyTurnedBack, d.name);
            Label(new Rect(r.x + 24f, y, r.width - 48f, 60f), fate, hintSmallStyle, UiKit.Ink);
            y += 64f;
            if (l.died && !string.IsNullOrEmpty(l.jisei))
            {
                Label(new Rect(r.x + 16f, y, r.width - 32f, r.yMax - y - 10f), l.jisei, jiseiStyle, UiKit.Ink);
            }
        }
    }
}
