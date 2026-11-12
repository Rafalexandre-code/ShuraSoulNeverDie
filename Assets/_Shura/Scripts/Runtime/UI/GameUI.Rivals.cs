using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Phase 5, part 1 screens: the Rivals' Wall in the temple, the rival's banner in the
    /// arena, and the reading meter (0 to 3 marks per reading).
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Generals first (by biome), then captains (most kills first).</summary>
        static List<Rival> WallOrder(MetaProgress m)
        {
            var list = new List<Rival>(m.rivals);
            list.Sort((a, b) =>
            {
                if (a.General != b.General) return a.General ? -1 : 1;
                if (a.General) return a.biome.CompareTo(b.biome);
                if (a.rank != b.rank) return b.rank.CompareTo(a.rank);
                return b.kills.CompareTo(a.kills);
            });
            return list;
        }

        void DrawRivalWall()
        {
            MetaProgress m = game.Meta;
            BeginSlots(BackToHall);
            TempleHeader(Strings.RivalWallTitle, Strings.RivalWallSubtitle);
            var left = new Rect(VW * 0.5f - 900f, 160f, 640f, 800f);
            var right = new Rect(left.xMax + 30f, 160f, 1130f, 800f);
            kit.PaperPanel(left);
            kit.PaperPanel(right);
            List<Rival> rivals = WallOrder(m);
            if (rivals.Count == 0)
            {
                listCount = 0;
                Label(new Rect(right.x + 60f, right.center.y - 40f, right.width - 120f, 80f), Strings.RivalWallEmpty, bodyCenterStyle, UiKit.InkSoft);
            }
            else
            {
                DrawListRows(new Rect(left.x + 30f, left.y + 40f, left.width - 60f, left.height - 110f), rivals.Count, 76f, (r, i, sel) =>
                {
                    Rival rv = rivals[i];
                    Color ink = sel ? UiKit.Shu : UiKit.Ink;
                    Label(new Rect(r.x + 4f, r.y, 60f, r.height), rv.General ? "将" : "仇", templeKanjiSmallStyle, rv.General ? UiKit.Shu : UiKit.InkSoft);
                    FitLabel(new Rect(r.x + 70f, r.y + 2f, r.width - 80f, 36f), rv.surname + "  ·  " + Rivals.RankName(rv), itemLeftStyle, ink);
                    string what = EnemyDefs.TryGet(rv.enemyId, out EnemyDef d) ? d.displayName : rv.enemyId;
                    FitLabel(new Rect(r.x + 70f, r.y + 38f, r.width - 80f, 28f), what + "  ·  " + string.Format(Strings.RivalKills, rv.kills), smallStyle, UiKit.InkSoft);
                });
                DrawRivalPage(right, rivals[Mathf.Clamp(listIndex, 0, rivals.Count - 1)]);
            }
            Label(new Rect(left.x, left.yMax - 58f, left.width, 30f), string.Format(Strings.RivalAvenged, m.fallenRivals.Count), smallStyleCenter, UiKit.GoldInk);
            BackSlot(left.yMax + 22f);
            ControlsLine(Strings.TempleListControls);
        }

        void DrawRivalPage(Rect r, Rival rv)
        {
            EnemyDef def = EnemyDefs.TryGet(rv.enemyId, out EnemyDef d) ? d : null;
            var portrait = new Rect(r.x + 60f, r.y + 60f, 280f, 280f);
            kit.Splat(1, new Rect(portrait.x - 30f, portrait.y - 20f, portrait.width + 60f, portrait.height + 40f), UiKit.Shu.WithAlpha(rv.General ? 0.35f : 0.18f));
            CharacterArt art = def != null ? PixelArt.Character(def.art) : null;
            if (art != null) DrawSpriteTinted(portrait, art.idle[(int)ArtDir.Right], Color.white);
            Label(new Rect(portrait.x, portrait.yMax + 6f, portrait.width, 40f), Rivals.RankName(rv), cardKindStyle, UiKit.Shu);

            float x = portrait.xMax + 50f, w = r.xMax - x - 50f;
            float y = r.y + 50f;
            Label(new Rect(x, y, w, 110f), rv.name, cardNameLeftStyle, UiKit.Ink);
            y += cardNameLeftStyle.CalcHeight(new GUIContent(rv.name), w) + 14f;
            void Line(string text, Color c)
            {
                float h = bodyStyle.CalcHeight(new GUIContent(text), w);
                Label(new Rect(x, y, w, h), text, bodyStyle, c);
                y += h + 8f;
            }
            Line(string.Format(Strings.RivalBiome, Biomes.All[Mathf.Clamp(rv.biome, 0, Biomes.All.Length - 1)].name), UiKit.InkSoft);
            Line(string.Format(Strings.RivalKills, rv.kills), UiKit.Ink);
            Line(string.Format(Strings.RivalEcos, rv.ecos, Mathf.RoundToInt(rv.ecos * Rivals.VengeanceMult)), UiKit.GoldInk);
            Line(Rivals.TraitText(rv), UiKit.Shu);
            Line(Rivals.ScarText(rv), UiKit.Ai);
            if (rv.General) Line(string.Format(Strings.RivalGuards, Rivals.Guards(rv)), UiKit.InkSoft);
            if (rv.furious) Line(Strings.RivalFurious, UiKit.Shu);

            var quote = new Rect(r.x + 60f, Mathf.Max(r.y + 470f, y + 16f), r.width - 120f, 150f);
            DrawRect(quote, UiKit.Ink.WithAlpha(0.06f));
            DrawRect(new Rect(quote.x, quote.y, 6f, quote.height), UiKit.Shu);
            Label(new Rect(quote.x + 30f, quote.y + 20f, quote.width - 50f, quote.height - 30f), "“" + Rivals.Taunt(rv, 0) + "”", jiseiStyle, UiKit.Ink);
            Label(new Rect(r.x + 60f, quote.yMax + 30f, r.width - 120f, 40f), Strings.RivalWhere, bodyCenterStyle, UiKit.InkSoft);
        }

        // ── In the arena ─────────────────────────────────────────────────────────────

        /// <summary>The rival's name and health across the top while it lives.</summary>
        void DrawRivalBanner()
        {
            Enemy e = game.World?.RivalInRoom;
            if (e == null || e.IsGone) return;
            var bar = new Rect(VW * 0.5f - 360f, 232f, 720f, 22f);
            Label(new Rect(0, bar.y - 40f, VW, 36f), e.RivalName, cardKindStyle, Pal.shu);
            kit.Bar(bar, Mathf.Clamp01(e.Health / Mathf.Max(1f, e.MaxHealth)), Pal.shu, Color.black.WithAlpha(0.7f));
            kit.Bar(new Rect(bar.x, bar.yMax + 4f, bar.width, 8f), e.Posture.Ratio, Pal.gold, Color.black.WithAlpha(0.6f));
        }

        /// <summary>"ATRASO ●●○": a reading and its marks (the third invites a counter-reading).</summary>
        static string ReadingWithMarks(ReadingId id, int level)
        {
            return Strings.ReadingName(id) + " " + new string('●', level) + new string('○', 3 - level);
        }
    }
}
