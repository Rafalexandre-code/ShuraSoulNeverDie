using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 4, part 2 screens: the Tea House and its six guests, the ways to walk the road
    /// (Modos de jogo), the end of a road that ends, and the enemy scroll in the Library.
    /// </summary>
    public sealed partial class GameUI
    {
        int teaGuest;
        string teaThanks;

        // ── The Tea House ────────────────────────────────────────────────────────────

        void DrawTeaHouse()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            listCount = 0;
            TempleHeader(Strings.TeaTitle, Strings.TeaSubtitle);
            var left = new Rect(VW * 0.5f - 900f, 158f, 560f, 800f);
            var right = new Rect(left.xMax + 30f, 158f, 1210f, 800f);
            kit.PaperPanel(left);
            kit.PaperPanel(right);

            GuestDef[] guests = TeaGuests.All;
            float rowH = (left.height - 60f) / guests.Length;
            for (int i = 0; i < guests.Length; i++)
            {
                GuestDef g = guests[i];
                int index = i;
                var r = new Rect(left.x + 30f, left.y + 30f + i * rowH, left.width - 60f, rowH - 10f);
                bool sel = AddSlot(r, () => ClaimOrTalk(index));
                if (sel && teaGuest != i)
                {
                    teaGuest = i;
                    teaThanks = null;
                }
                PaperSelect(r, sel);
                DrawGuestArt(new Rect(r.x, r.y + 4f, r.height - 8f, r.height - 8f), g, 1f);
                FitLabel(new Rect(r.x + r.height + 4f, r.y + 6f, r.width - r.height - 60f, 40f), g.name, itemLeftStyle, sel ? UiKit.Shu : UiKit.Ink);
                string state = m.MissionDone(g.id) ? Strings.TeaDone : TeaGuests.Ready(m, g) ? Strings.TeaClaim + "!" : TeaGuests.Progress(m, g) + " / " + g.goal;
                Color sc = m.MissionDone(g.id) ? UiKit.GoldInk : TeaGuests.Ready(m, g) ? UiKit.Shu : UiKit.InkSoft;
                FitLabel(new Rect(r.x + r.height + 4f, r.y + 48f, r.width - r.height - 20f, 28f), state, smallStyle, sc);
                if (TeaGuests.Ready(m, g)) kit.Dab(new Rect(r.xMax - 34f, r.y + 14f, 22f, 22f), UiKit.Shu);
            }

            DrawGuestPage(right, guests[Mathf.Clamp(teaGuest, 0, guests.Length - 1)], m);
            BackSlot(left.yMax + 22f);
            ControlsLine(Strings.TeaControls);
        }

        void DrawGuestArt(Rect r, GuestDef g, float alpha)
        {
            kit.Enso(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), UiKit.OnPaper(g.color).WithAlpha(0.6f * alpha), g.name.Length * 23f);
            CharacterArt art = PixelArt.Character(g.art);
            if (art != null) DrawSpriteTinted(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.08f, r.width * 0.76f, r.height * 0.76f), art.idle[(int)ArtDir.Right], new Color(1f, 1f, 1f, alpha));
            else Label(r, g.kanji, templeKanjiSmallStyle, UiKit.Ink.WithAlpha(alpha));
        }

        void DrawGuestPage(Rect r, GuestDef g, MetaProgress m)
        {
            var portrait = new Rect(r.x + 50f, r.y + 50f, 260f, 260f);
            DrawGuestArt(portrait, g, 1f);
            Label(new Rect(portrait.x, portrait.yMax + 4f, portrait.width, 80f), g.kanji, templeKanjiSmallStyle, UiKit.OnPaper(g.color));
            float x = portrait.xMax + 50f, w = r.xMax - x - 50f;
            FitLabel(new Rect(x, r.y + 40f, w, 70f), g.name, headerStyle, UiKit.Ink);
            Label(new Rect(x, r.y + 114f, w, 60f), g.role, hintSmallStyle, UiKit.InkSoft);

            // What they say: a bubble with a tail toward them.
            string line = teaThanks ?? TeaGuests.Line(m, g, game.LastLegacy);
            var bubble = new Rect(x - 10f, r.y + 176f, w + 20f, 190f);
            DrawRect(bubble, UiKit.Ink.WithAlpha(0.06f));
            DrawRect(new Rect(bubble.x, bubble.y, 6f, bubble.height), UiKit.OnPaper(g.color));
            Label(new Rect(bubble.x + 30f, bubble.y + 18f, bubble.width - 50f, bubble.height - 30f), "“" + line + "”", jiseiStyle, UiKit.Ink);

            // The mission.
            float y = r.y + 420f;
            kit.Underline(new Rect(r.x + 60f, y, r.width - 120f, 14f), UiKit.Ink.WithAlpha(0.4f));
            Label(new Rect(r.x, y + 20f, r.width, 28f), Strings.TeaMission, capsCenterStyle, UiKit.InkSoft);
            Label(new Rect(r.x + 80f, y + 56f, r.width - 160f, 70f), g.mission, bodyCenterStyle, UiKit.Ink);
            int progress = TeaGuests.Progress(m, g);
            var bar = new Rect(r.x + 200f, y + 138f, r.width - 400f, 18f);
            kit.Bar(bar, progress / (float)g.goal, UiKit.OnPaper(g.color), UiKit.Ink.WithAlpha(0.15f));
            Label(new Rect(bar.x, bar.yMax + 4f, bar.width, 26f), progress + " / " + g.goal, smallStyleCenter, UiKit.InkSoft);
            Label(new Rect(r.x + 80f, y + 196f, r.width - 160f, 60f), string.Format(Strings.TeaReward, g.reward), bodyCenterStyle, UiKit.GoldInk);

            var claim = new Rect(r.center.x - 200f, y + 272f, 400f, 58f);
            if (m.MissionDone(g.id)) Label(claim, Strings.TeaDone, itemStyle, UiKit.GoldInk);
            else if (TeaGuests.Ready(m, g))
            {
                bool cs = AddSlot(claim, () => ClaimOrTalk(Array.IndexOf(TeaGuests.All, g)));
                PaperSelect(claim, cs);
                Label(claim, Strings.TeaClaim, itemStyle, UiKit.Shu);
            }
        }

        void ClaimOrTalk(int index)
        {
            GuestDef g = TeaGuests.All[index];
            teaGuest = index;
            if (game.ClaimMission(g)) teaThanks = g.thanks;
        }

        // ── Modes ────────────────────────────────────────────────────────────────────

        static readonly RunMode[] ModeOrder = { RunMode.Journey, RunMode.Daily, RunMode.Tower, RunMode.Master, RunMode.Pilgrimage };

        void DrawModes()
        {
            EnsureTempleStyles();
            MetaProgress m = game.Meta;
            BeginSlots(game.GoToMainMenu);
            listCount = 0;
            TempleHeader(Strings.ModesTitle, Strings.ModesSubtitle);
            const float cw = 330f, ch = 470f, gap = 22f;
            float x0 = VW * 0.5f - (ModeOrder.Length * cw + (ModeOrder.Length - 1) * gap) * 0.5f;
            for (int i = 0; i < ModeOrder.Length; i++)
            {
                RunMode mode = ModeOrder[i];
                var r = new Rect(x0 + i * (cw + gap), 170f, cw, ch);
                bool sel = AddSlot(r, () => game.ChooseMode(mode));
                bool open = game.ModeOpen(mode);
                bool triedToday = mode == RunMode.Daily && GameModes.DailyTried(m, DateTime.Now);
                float lift = sel ? -8f * CombatMath.EaseOutCubic(selAnim) : 0f;
                r.y += lift;
                if (sel) kit.Stroke(new Rect(r.x - 14f, r.y - 10f, r.width + 28f, r.height + 20f), UiKit.Shu.WithAlpha(0.9f), CombatMath.EaseOutCubic(selAnim));
                kit.PaperPanel(r, open && !triedToday ? 1f : 0.72f);
                Color ink = open ? UiKit.Ink : UiKit.InkSoft.WithAlpha(0.5f);
                kit.Enso(new Rect(r.center.x - 70f, r.y + 22f, 140f, 140f), UiKit.Shu.WithAlpha(open ? 0.3f : 0.1f), i * 50f);
                Label(new Rect(r.x, r.y + 26f, r.width, 130f), GameModes.Kanji(mode), templeKanjiStyle, ink);
                FitLabel(new Rect(r.x + 16f, r.y + 168f, r.width - 32f, 46f), GameModes.Name(mode), templeNameStyle, sel ? UiKit.Shu : ink);
                Label(new Rect(r.x + 26f, r.y + 222f, r.width - 52f, 130f), GameModes.Text(mode), hintSmallStyle, UiKit.Ink);
                string info = null;
                switch (mode)
                {
                    case RunMode.Daily:
                        info = triedToday ? Strings.DailyTodayDone : string.Format(Strings.DailyToday, GameModes.DateKey(DateTime.Now));
                        break;
                    case RunMode.Tower:
                        info = string.Format(Strings.ModeBest, m.towerBest + " / " + GameModes.TowerFloors);
                        break;
                    case RunMode.Master:
                        if (!open) info = Strings.ModeLockedShort;
                        break;
                    case RunMode.Pilgrimage:
                        info = string.Format(Strings.ModeBest, m.pilgrimageBest + " / " + GameModes.PilgrimageRooms) + "\n" + string.Format(Strings.ModeEndings, EndingsSeen(m));
                        break;
                    default:
                        info = string.Format(Strings.ModeBest, m.bestRoom);
                        break;
                }
                if (info != null) Label(new Rect(r.x + 20f, r.y + 360f, r.width - 40f, 70f), info, smallStyleCenter, open ? UiKit.Shu : UiKit.InkSoft);
                if (mode == game.PendingMode) kit.Seal(new Rect(r.xMax - 56f, r.y - 20f, 70f, 70f), 0.95f);
            }

            // The daily ranking: your days, the deepest first.
            var rank = new Rect(VW * 0.5f - 560f, 668f, 1120f, 300f);
            kit.PaperPanel(rank);
            Label(new Rect(rank.x, rank.y + 18f, rank.width, 28f), Strings.DailyRanking, capsCenterStyle, UiKit.InkSoft);
            List<DailyResult> days = GameModes.Ranking(m);
            if (days.Count == 0) Label(new Rect(rank.x, rank.y + 110f, rank.width, 40f), Strings.DailyRankingEmpty, bodyCenterStyle, UiKit.InkSoft);
            for (int i = 0; i < days.Count && i < 6; i++)
            {
                DailyResult d = days[i];
                Label(new Rect(rank.x + 80f, rank.y + 56f + i * 38f, 60f, 34f), (i + 1) + ".", bodyStyle, i == 0 ? UiKit.Shu : UiKit.InkSoft);
                Label(new Rect(rank.x + 140f, rank.y + 56f + i * 38f, rank.width - 220f, 34f), string.Format(Strings.DailyRow, d.date, d.depth, d.ecos), bodyStyle, UiKit.Ink);
            }
            BackSlot(rank.yMax + 18f);
            ControlsLine(Strings.ModesControls);
            FlushSlotClick();
        }

        static int EndingsSeen(MetaProgress m)
        {
            int n = 0;
            foreach (string e in m.endings)
            {
                if (e == GameModes.EndingLiberation || e == GameModes.EndingShura || e == GameModes.EndingWheel) n++;
            }
            return n;
        }

        // ── The end of a road that ends ──────────────────────────────────────────────

        void DrawVictory()
        {
            EnsureTempleStyles();
            LegacyReport l = game.LastLegacy;
            string ending = l != null ? l.ending : "";
            bool tower = ending == "torre";
            string title = tower ? Strings.VictoryTowerTitle : GameModes.EndingTitle(ending);
            DrawEnd(title, Strings.VictorySubtitle);
            float w = Mathf.Min(540f, (VW - 760f) * 0.5f - 40f);
            if (w < 360f) return;
            var r = new Rect((VW - 760f) * 0.5f - 30f - w, 280f, w, 530f);
            kit.PaperPanel(r);
            Label(new Rect(r.x, r.y + 30f, r.width, 50f), title, cardKindStyle, UiKit.Shu);
            Label(new Rect(r.x + 36f, r.y + 96f, r.width - 72f, r.height - 120f), tower ? Strings.VictoryTowerText : GameModes.EndingText(ending), jiseiStyle, UiKit.Ink);
        }

        // ── The enemy scroll (in the Library) ────────────────────────────────────────

        void DrawScroll(Rect r, EnemyDef e, MetaProgress m)
        {
            float y = r.y;
            kit.Underline(new Rect(r.x, y, r.width, 14f), UiKit.Ink.WithAlpha(0.4f));
            Label(new Rect(r.x, y + 18f, r.width, 26f), Strings.ScrollTitle, capsCenterStyle, UiKit.InkSoft);
            if (!m.MissionDone(TeaGuests.Warashi))
            {
                Label(new Rect(r.x, y + 54f, r.width, 60f), Strings.ScrollLocked, hintSmallStyle, UiKit.InkSoft);
                return;
            }
            EnemyScroll s = m.Scroll(e.id, false);
            y += 52f;
            for (int i = 0; i < EnemyScrolls.Challenges; i++)
            {
                float p = EnemyScrolls.ChallengeProgress(s, i);
                bool sealedOne = i == EnemyScrolls.Challenges - 1;
                kit.Dab(new Rect(r.x, y + 6f, 18f, 18f), p >= 1f ? UiKit.Shu : UiKit.Ink.WithAlpha(sealedOne ? 0.08f : 0.18f));
                FitLabel(new Rect(r.x + 30f, y, r.width - 140f, 30f), EnemyScrolls.ChallengeName(i), smallStyle, sealedOne ? UiKit.InkSoft : p >= 1f ? UiKit.Shu : UiKit.Ink);
                Label(new Rect(r.xMax - 100f, y, 100f, 30f), EnemyScrolls.ChallengeCount(s, i), smallRightStyle, UiKit.InkSoft);
                y += 34f;
            }
            if (s != null && s.done) Label(new Rect(r.x, y + 4f, r.width, 30f), Strings.ScrollDoneText, smallStyle, UiKit.GoldInk);
        }

        /// <summary>The finished scroll: the enemy brushed in ink, its legend, and the seal.</summary>
        void DrawSumiE(Rect r, EnemyDef e)
        {
            // A pale wash of the brush behind, the figure in ink tones, a bleed to one side.
            kit.Splat(3, new Rect(r.x - 30f, r.y - 10f, r.width + 60f, r.height + 20f), UiKit.Ink.WithAlpha(0.07f));
            kit.Enso(new Rect(r.x - 10f, r.y - 10f, r.width + 20f, r.height + 20f), UiKit.Ink.WithAlpha(0.35f), 200f);
            Sprite ink = PixelArt.InkPortrait(e.art);
            if (ink != null)
            {
                DrawSpriteTinted(new Rect(r.x + 9f, r.y + 6f, r.width, r.height), ink, new Color(1f, 1f, 1f, 0.22f));
                DrawSpriteTinted(r, ink, Color.white);
            }
            kit.Seal(new Rect(r.xMax - 40f, r.yMax - 60f, 66f, 66f), 0.95f);
            Label(new Rect(r.x, r.yMax + 2f, r.width, 34f), Strings.ScrollLegend, cardKindStyle, UiKit.Shu);
        }
    }
}
