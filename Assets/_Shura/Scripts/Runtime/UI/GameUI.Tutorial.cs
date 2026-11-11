using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The dojo on screen: the lesson scroll at the top (number, name, what to do, the buttons
    /// as key caps and how far along you are), the golden marks on the floor, an arrow over
    /// the master, the "well done" seal between lessons, and — when the world stops for a
    /// timed press — the frozen moment with the one button it waits for.
    /// </summary>
    public sealed partial class GameUI
    {
        GUIStyle tutorialTitleStyle, tutorialTextStyle, tutorialNowStyle, tutorialKeyStyle, tutorialBigKeyStyle;

        void EnsureTutorialStyles(Font brush, Font text, Font bold)
        {
            tutorialTitleStyle = MakeStyle(46, TextAnchor.MiddleLeft, brush);
            tutorialTextStyle = MakeStyle(23, TextAnchor.UpperLeft, text);
            tutorialNowStyle = MakeStyle(132, TextAnchor.MiddleCenter, brush);
            tutorialKeyStyle = MakeStyle(22, TextAnchor.MiddleCenter, bold);
            tutorialBigKeyStyle = MakeStyle(40, TextAnchor.MiddleCenter, bold);
        }

        /// <summary>Drawn on the HUD canvas, over the duel and under the menus.</summary>
        void DrawTutorial()
        {
            Tutorial t = game.Tutorial;
            if (!t.Active) return;
            Camera cam = game.CameraRig.Cam;
            float time = Time.unscaledTime;

            // Marks on the floor: the next one pulses, the ones reached fade.
            if (cam != null)
            {
                for (int i = 0; i < t.Marks.Count; i++)
                {
                    bool done = i < t.MarkIndex, next = i == t.MarkIndex;
                    Vector2 g = WorldToGui(cam, t.Marks[i]);
                    float size = next ? 96f + 12f * Mathf.Sin(time * 5f) : 70f;
                    Color c = done ? Pal.blessing.WithAlpha(0.35f) : Pal.gold.WithAlpha(next ? 0.95f : 0.4f);
                    if (next) kit.Dab(new Rect(g.x - size * 0.6f, g.y - size * 0.6f, size * 1.2f, size * 1.2f), Pal.gold.WithAlpha(0.16f + 0.06f * Mathf.Sin(time * 5f)));
                    kit.Enso(new Rect(g.x - size * 0.5f, g.y - size * 0.5f, size, size), c, time * (next ? 90f : 0f) + i * 40f);
                    if (next) Label(new Rect(g.x - 100f, g.y - size * 0.5f - 34f, 200f, 28f), Strings.TutorialHere, capsCenterStyle, Pal.gold);
                    if (done) kit.Dab(new Rect(g.x - 9f, g.y - 9f, 18f, 18f), Pal.blessing.WithAlpha(0.6f));
                }
                // A small arrow keeps the master in sight.
                Enemy partner = t.Partner;
                if (partner != null && t.Current != Tutorial.Step.Duel)
                {
                    Vector2 g = WorldToGui(cam, partner.RenderPosition + Vector2.up * (partner.Radius * 2.6f + 1.25f));
                    float bob = 6f * Mathf.Sin(time * 4f);
                    Label(new Rect(g.x - 120f, g.y - 44f + bob, 240f, 26f), EnemyDefs.Sensei.displayName.ToUpperInvariant(), capsCenterStyle, Pal.gold.WithAlpha(0.9f));
                    Label(new Rect(g.x - 30f, g.y - 22f + bob, 60f, 26f), "▼", capsCenterStyle, Pal.gold.WithAlpha(0.9f));
                }
            }

            if (t.Current == Tutorial.Step.Done)
            {
                DrawTutorialDone();
                return;
            }

            // The lesson scroll.
            string text = t.Text;
            const float w = 980f;
            float textW = w - 330f;
            float textH = tutorialTextStyle.CalcHeight(new GUIContent(text), textW);
            bool stances = t.Current == Tutorial.Step.Stances;
            float h = Mathf.Max(150f, 96f + textH + (stances ? 118f : 0f));
            var card = new Rect(VW * 0.5f - w * 0.5f, 10f, w, h);
            // The lesson must always be readable: it only thins a little over a fighter.
            Panel(8, card, 0.62f);
            DrawRect(new Rect(card.x + 12f, card.y + 8f, card.width - 24f, card.height - 14f), new Color(0.05f, 0.04f, 0.05f, 0.74f));
            kit.InkPanel(card, 0.9f);
            DrawRect(new Rect(card.x + 30f, card.y + 14f, 6f, card.height - 28f), UiKit.Shu.WithAlpha(0.9f));
            Label(new Rect(card.x + 54f, card.y + 14f, 300f, 26f), string.Format(Strings.TutorialLesson, (int)t.Current + 1, Tutorial.LessonCount), capsStyle, Pal.gold);
            Label(new Rect(card.x + 54f, card.y + 36f, textW, 54f), t.Title, tutorialTitleStyle, UiKit.PaperText);
            Label(new Rect(card.x + 54f, card.y + 92f, textW, textH), text, tutorialTextStyle, UiKit.PaperText.WithAlpha(0.92f));

            // Right: the buttons and the progress.
            float rx = card.xMax - 252f;
            string[] keys = t.Keys;
            float kx = rx;
            float ky = card.y + 26f;
            foreach (string k in keys)
            {
                float kw = Mathf.Max(46f, tutorialKeyStyle.CalcSize(new GUIContent(k)).x + 26f);
                if (kx + kw > card.xMax - 24f)
                {
                    kx = rx;
                    ky += 52f;
                }
                KeyCap(new Rect(kx, ky, kw, 44f), k, tutorialKeyStyle, 1f + 0.05f * Mathf.Sin(time * 5f));
                kx += kw + 8f;
            }
            float py = Mathf.Max(ky + 58f, card.y + 92f);
            if (t.Goal > 1 && !stances)
            {
                for (int i = 0; i < t.Goal; i++)
                {
                    var pip = new Rect(rx + i * 40f, py, 34f, 34f);
                    bool on = i < t.Progress;
                    kit.Enso(pip, (on ? Pal.gold : UiKit.PaperDim).WithAlpha(on ? 1f : 0.5f), i * 60f);
                    if (on) kit.Dab(new Rect(pip.x + 9f, pip.y + 9f, 16f, 16f), Pal.gold);
                }
            }
            if (stances) DrawTutorialStances(new Rect(card.x + 54f, card.y + 100f + textH, card.width - 108f, 110f), t);
            UiKit.Fade = 1f;

            if (t.Flash > 0f) DrawTutorialWellDone(t.Flash);
            if (t.Frozen) DrawTutorialFreeze(t);
        }

        /// <summary>The four guards as a checklist: the one held is lit, the ones tried are ticked.</summary>
        void DrawTutorialStances(Rect r, Tutorial t)
        {
            string[] lines = { Strings.TutorialStanceJodan, Strings.TutorialStanceChudan, Strings.TutorialStanceGedan, Strings.TutorialStanceWaki };
            int held = (int)game.World.Host.Stance;
            for (int i = 0; i < 4; i++)
            {
                var row = new Rect(r.x + (i % 2) * (r.width * 0.5f), r.y + (i / 2) * 52f, r.width * 0.5f - 12f, 46f);
                bool tried = (t.StanceMask & (1 << i)) != 0;
                if (i == held) DrawRect(row, Pal.gold.WithAlpha(0.14f));
                kit.Enso(new Rect(row.x + 6f, row.y + 8f, 30f, 30f), (tried ? Pal.blessing : UiKit.PaperDim).WithAlpha(tried ? 1f : 0.5f), i * 50f);
                if (tried) kit.Dab(new Rect(row.x + 14f, row.y + 16f, 14f, 14f), Pal.blessing);
                FitLabel(new Rect(row.x + 46f, row.y, row.width - 52f, row.height), lines[i], smallStyle, i == held ? Pal.gold : tried ? UiKit.PaperText : UiKit.PaperDim);
            }
        }

        void KeyCap(Rect r, string key, GUIStyle style, float pulse)
        {
            var k = new Rect(r.center.x - r.width * pulse * 0.5f, r.center.y - r.height * pulse * 0.5f, r.width * pulse, r.height * pulse);
            DrawRect(new Rect(k.x, k.y + 4f, k.width, k.height), UiKit.InkSoft);
            DrawRect(k, UiKit.PaperText.WithAlpha(0.96f));
            DrawRect(new Rect(k.x, k.yMax - 4f, k.width, 4f), UiKit.InkSoft.WithAlpha(0.6f));
            Label(k, key, style, UiKit.Ink);
        }

        void DrawTutorialWellDone(float left)
        {
            float k = Mathf.Clamp01(1f - left / 1.25f);
            float a = k < 0.15f ? k / 0.15f : k > 0.75f ? Mathf.Clamp01((1f - k) / 0.25f) : 1f;
            float grow = 1f + 0.5f * (1f - CombatMath.EaseOutCubic(Mathf.Clamp01(k / 0.2f)));
            float size = 150f * grow;
            var c = new Vector2(VW * 0.5f, VH * 0.42f);
            kit.Seal(new Rect(c.x - 330f - size * 0.5f + 75f, c.y - size * 0.5f, size, size), 0.95f * a);
            kit.Stroke(new Rect(c.x - 250f, c.y - 52f, 640f, 104f), UiKit.Ink.WithAlpha(0.85f * a), Mathf.Clamp01(k / 0.25f));
            Label(new Rect(c.x - 250f, c.y - 44f, 640f, 88f), Strings.TutorialWellDone, headerStyle, Pal.gold.WithAlpha(a));
        }

        /// <summary>The frozen moment: everything dims but the one thing to do.</summary>
        void DrawTutorialFreeze(Tutorial t)
        {
            // Everything dims but the two fighters and the ring between them.
            Enemy foe = t.Partner;
            Vector2 focus = foe != null ? (foe.RenderPosition + game.World.Host.RenderPosition) * 0.5f : game.World.Host.RenderPosition;
            DrawDarknessAt(focus, 3.4f, new Color(0f, 0f, 0.02f, 0.72f));
            float time = Time.unscaledTime;
            float pulse = 1f + 0.06f * Mathf.Sin(time * 9f);
            bool danger = t.FreezeAction == BufferedAction.Dodge;
            Color c = danger ? Pal.danger : t.FreezeAction == BufferedAction.Swap ? Pal.gold : Pal.playerAccent;
            float y = VH * 0.56f;
            tutorialNowStyle.fontSize = Mathf.RoundToInt(132f * pulse);
            Label(new Rect(0f, y, VW, 150f), Strings.TutorialNow, tutorialNowStyle, c);
            string key = t.FreezeKey;
            float kw = Mathf.Max(120f, tutorialBigKeyStyle.CalcSize(new GUIContent(key)).x + 60f);
            KeyCap(new Rect(VW * 0.5f - kw * 0.5f, y + 160f, kw, 76f), key, tutorialBigKeyStyle, pulse);
            Label(new Rect(0f, y + 250f, VW, 40f), t.FreezeVerb, toastStyle, UiKit.PaperText);
            Label(new Rect(0f, y + 294f, VW, 30f), Strings.TutorialFrozenNote, smallStyleCenter, UiKit.PaperDim);
        }

        void DrawTutorialDone()
        {
            DimGame(0.9f);
            var panel = new Rect(VW * 0.5f - 520f, VH * 0.5f - 190f, 1040f, 380f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 44f, panel.width, 90f), Strings.TutorialDoneTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 560f);
            Label(new Rect(panel.x + 80f, panel.y + 170f, panel.width - 160f, 100f), Strings.TutorialDoneText, bodyCenterStyle, UiKit.Ink);
            Label(new Rect(panel.x + 80f, panel.yMax - 90f, panel.width - 160f, 34f), Strings.TutorialPausedHint, smallStyleCenter, UiKit.InkSoft);
            kit.Seal(new Rect(panel.xMax - 150f, panel.y + 30f, 96f, 96f), 0.95f);
        }
    }
}
