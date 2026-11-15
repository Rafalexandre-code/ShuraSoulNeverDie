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
            if (t.Current == Tutorial.Step.Prologue)
            {
                DrawPrologue(t);
                return;
            }
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
            if (t.Touring)
            {
                DrawHudTour(t);
                return;
            }
            if (t.SkipArmed) DrawSkipBanner();

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
            Label(new Rect(card.x + 54f, card.y + 14f, 300f, 26f), string.Format(Strings.TutorialLesson, Tutorial.PracticeIndex(t.Current), Tutorial.PracticeCount), capsStyle, Pal.gold);
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

        // ── The prologue ────────────────────────────────────────────────────────────────

        void DrawPrologue(Tutorial t)
        {
            float time = Time.unscaledTime;
            Rect full = FullScreen;
            switch (t.ProloguePhase)
            {
                case 0:
                {
                    // Black, then the place and the hour, then the world through the dark.
                    float a = 1f - Mathf.Clamp01((t.PhaseTime - 2.4f) / 1f);
                    DrawRect(full, new Color(0f, 0f, 0f, a));
                    float ta = Mathf.Clamp01(t.PhaseTime / 0.9f) * a;
                    Label(new Rect(0, VH * 0.5f - 90f, VW, 110f), Strings.PrologueTitle, tutorialNowStyle, Pal.gold.WithAlpha(ta));
                    Label(new Rect(0, VH * 0.5f + 80f, VW, 50f), Strings.PrologueSubtitle, taglineStyle, UiKit.PaperText.WithAlpha(ta * 0.9f));
                    break;
                }
                case 1:
                    DrawLetterbox(1f);
                    Label(new Rect(0, VH - 92f, VW, 50f), Strings.PrologueWalk, taglineStyle, UiKit.PaperText.WithAlpha(0.85f * Mathf.Clamp01(t.PhaseTime / 1.2f)));
                    break;
                default:
                    DrawPrologueTalk(t, time);
                    break;
            }
            if (t.SkipArmed) DrawSkipBanner();
        }

        void DrawLetterbox(float amount)
        {
            float h = 120f * amount;
            DrawRect(new Rect(0f, 0f, VW, h), Color.black);
            DrawRect(new Rect(0f, VH - h, VW, h), Color.black);
        }

        GUIStyle dialogueTextStyle, dialogueNameStyle;

        static readonly Color DialogueInk = new Color(0.055f, 0.04f, 0.07f, 0.96f);
        static readonly Color DialogueInkSoft = new Color(0.1f, 0.075f, 0.12f, 0.96f);

        /// <summary>
        /// A conversa do prólogo, no padrão de diálogo de RPG: a cena continua visível (escurecida,
        /// com faixas de cinema), uma caixa sólida embaixo com moldura dourada dupla, os dois
        /// duelistas em pixel art DE PERFIL nas molduras das pontas (o herói olhando para a direita,
        /// o mestre para a esquerda), quem fala aceso e com o ensō atrás, quem escuta na sombra, a
        /// placa com o nome sobre a moldura de quem fala e as palavras se escrevendo no meio.
        /// </summary>
        void DrawPrologueTalk(Tutorial t, float time)
        {
            Rect full = FullScreen;
            DrawRect(full, new Color(0.01f, 0.01f, 0.03f, 0.45f));
            DrawLetterbox(0.75f);
            if (dialogueTextStyle == null)
            {
                dialogueTextStyle = new GUIStyle(tutorialTextStyle) { fontSize = 30, alignment = TextAnchor.UpperLeft, wordWrap = true };
                dialogueNameStyle = new GUIStyle(tutorialTitleStyle) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            }
            int speaker = Strings.PrologueSpeaker(t.LineIndex);
            float into = Mathf.Clamp01(t.LineTime / 0.3f);
            Color heroInk = UiKit.Shu, masterInk = Pal.gold;
            Color speakerInk = speaker == 1 ? masterInk : heroInk;

            // A caixa: sólida, com moldura dupla e cantos marcados.
            const float boxH = 268f, portrait = 236f;
            var box = new Rect(150f, VH - boxH - 70f, VW - 300f, boxH);
            DrawRect(new Rect(box.x - 6f, box.y - 6f, box.width + 12f, box.height + 12f), Pal.gold.WithAlpha(0.85f));
            DrawRect(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), DialogueInk);
            DrawRect(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), Pal.gold.WithAlpha(0.35f));
            DrawRect(new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f), DialogueInk);
            foreach (Vector2 corner in new[] { new Vector2(box.x, box.y), new Vector2(box.xMax, box.y), new Vector2(box.x, box.yMax), new Vector2(box.xMax, box.yMax) })
                DrawRect(new Rect(corner.x - 9f, corner.y - 9f, 18f, 18f), Pal.gold);

            // Os retratos de perfil nas pontas da caixa.
            var heroSlot = new Rect(box.x + 16f, box.y + (boxH - portrait) * 0.5f, portrait, portrait);
            var masterSlot = new Rect(box.xMax - 16f - portrait, heroSlot.y, portrait, portrait);
            PlayerController hero = game.World != null ? game.World.Host : null;
            string heroArt = PlayerController.PartyArt[Mathf.Clamp(hero != null ? hero.ArtIndex : 0, 0, PlayerController.PartyArt.Length - 1)];
            DrawProfile(heroSlot, heroArt, ArtDir.Right, speaker == 0, into, time, heroInk);
            DrawProfile(masterSlot, EnemyDefs.Sensei.art, ArtDir.Left, speaker == 1, into, time, masterInk);

            // A placa do nome sobre a moldura de quem fala.
            Rect slot = speaker == 1 ? masterSlot : heroSlot;
            string name = speaker == 1 ? Strings.PrologueMaster : Strings.PrologueHero;
            var plate = new Rect(slot.center.x - 150f, box.y - 58f, 300f, 54f);
            DrawRect(new Rect(plate.x - 3f, plate.y - 3f, plate.width + 6f, plate.height + 6f), speakerInk.WithAlpha(0.9f));
            DrawRect(plate, DialogueInkSoft);
            Label(plate, name, dialogueNameStyle, speakerInk);

            // As palavras, entre os dois retratos.
            var text = new Rect(heroSlot.xMax + 34f, box.y + 30f, masterSlot.x - heroSlot.xMax - 68f, boxH - 76f);
            string line = Strings.PrologueText(t.LineIndex);
            int shown = Mathf.Clamp(Mathf.FloorToInt(t.LineTime * Strings.PrologueCharsPerSecond), 0, line.Length);
            Label(text, line.Substring(0, shown), dialogueTextStyle, UiKit.PaperText);
            if (shown >= line.Length)
            {
                float bob = Mathf.Sin(time * 6f) * 4f;
                Label(new Rect(text.xMax - 40f, box.yMax - 50f + bob, 40f, 36f), "▼", capsCenterStyle, speakerInk);
            }
            Label(new Rect(0f, VH - 46f, VW, 34f), Strings.PrologueKeys, smallStyleCenter, UiKit.PaperDim);

            Event e = Event.current;
            if (!game.Launch.Automated && e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                t.AdvanceLine();
            }
        }

        /// <summary>
        /// Um duelista de perfil numa moldura: a pixel art do jogo, ampliada sem borrar, respirando.
        /// Quem fala fica aceso, um pouco maior e com o ensō; quem escuta, na sombra.
        /// </summary>
        void DrawProfile(Rect slot, string artId, ArtDir dir, bool speaking, float into, float time, Color ink)
        {
            float lit = speaking ? Mathf.Lerp(0.45f, 1f, into) : Mathf.Lerp(1f, 0.38f, into);
            DrawRect(new Rect(slot.x - 3f, slot.y - 3f, slot.width + 6f, slot.height + 6f), ink.WithAlpha(speaking ? 0.85f : 0.3f));
            DrawRect(slot, DialogueInkSoft);
            if (speaking) kit.Enso(new Rect(slot.x + 18f, slot.y + 18f, slot.width - 36f, slot.height - 36f), ink.WithAlpha(0.5f * into), time * 8f);
            CharacterArt art = PixelArt.Character(artId);
            Sprite s = art != null ? art.idle[(int)dir] : null;
            if (s == null) return;
            // Respira devagar; quem fala balança de leve com as palavras.
            float breath = 1f + 0.02f * Mathf.Sin(time * 1.8f + slot.x);
            float talk = speaking ? Mathf.Abs(Mathf.Sin(time * 9f)) * 3f * into : 0f;
            float size = slot.width * (speaking ? 1.05f : 0.95f);
            var r = new Rect(slot.center.x - size * 0.5f, slot.center.y - size * 0.5f * breath - talk + 10f, size, size * breath);
            DrawSpriteTinted(r, s, new Color(lit, lit, lit * (speaking ? 1f : 1.12f), 1f));
        }

        void DrawSkipBanner()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
            var r = new Rect(VW * 0.5f - 560f, VH - 230f, 1120f, 64f);
            kit.InkPanel(r, 0.95f);
            Label(r, Strings.TutorialSkipArmed, bodyCenterStyle, Pal.gold.WithAlpha(pulse));
        }

        /// <summary>
        /// The tour of the screen: everything dims but one part of the HUD, framed in gold, and a
        /// dotted line runs from it to the scroll that says what it is and what it does.
        /// </summary>
        void DrawHudTour(Tutorial t)
        {
            float time = Time.unscaledTime;
            int i = t.TourIndex, n = Strings.TutorialTourCount;
            HudPart part = Strings.TutorialTourPart(i);
            Rect hole = default;
            bool has = part != HudPart.None && hudMarks.TryGetValue(part, out hole);
            float appear = Mathf.Clamp01(t.TourTime / 0.25f);

            // The dark, with a window over the part being shown.
            Color dim = new Color(0.01f, 0.01f, 0.03f, 0.74f * appear);
            Rect full = FullScreen;
            if (has)
            {
                var hRect = new Rect(hole.x - 8f, hole.y - 8f, hole.width + 16f, hole.height + 16f);
                DrawRect(new Rect(full.x, full.y, full.width, Mathf.Max(0f, hRect.y - full.y)), dim);
                DrawRect(new Rect(full.x, hRect.yMax, full.width, Mathf.Max(0f, full.yMax - hRect.yMax)), dim);
                DrawRect(new Rect(full.x, hRect.y, Mathf.Max(0f, hRect.x - full.x), hRect.height), dim);
                DrawRect(new Rect(hRect.xMax, hRect.y, Mathf.Max(0f, full.xMax - hRect.xMax), hRect.height), dim);
                // A gold frame that breathes.
                float g = 0.65f + 0.35f * Mathf.Sin(time * 4f);
                Color gold = Pal.gold.WithAlpha(g * appear);
                const float th = 4f;
                DrawRect(new Rect(hRect.x, hRect.y, hRect.width, th), gold);
                DrawRect(new Rect(hRect.x, hRect.yMax - th, hRect.width, th), gold);
                DrawRect(new Rect(hRect.x, hRect.y, th, hRect.height), gold);
                DrawRect(new Rect(hRect.xMax - th, hRect.y, th, hRect.height), gold);
                float o = 6f + 4f * Mathf.Sin(time * 4f);
                Color halo = Pal.gold.WithAlpha(0.25f * appear);
                DrawRect(new Rect(hRect.x - o, hRect.y - o, hRect.width + 2f * o, 2f), halo);
                DrawRect(new Rect(hRect.x - o, hRect.yMax + o - 2f, hRect.width + 2f * o, 2f), halo);
                DrawRect(new Rect(hRect.x - o, hRect.y - o, 2f, hRect.height + 2f * o), halo);
                DrawRect(new Rect(hRect.xMax + o - 2f, hRect.y - o, 2f, hRect.height + 2f * o), halo);
            }
            else DrawRect(full, dim);

            // The scroll: beside the part, on the side with more room.
            const float bw = 660f;
            string title = Strings.TutorialTourTitle(i), text = Strings.TutorialTourText(i);
            float textH = tutorialTextStyle.CalcHeight(new GUIContent(text), bw - 80f);
            float bh = 150f + textH + 70f;
            Rect box;
            if (!has) box = new Rect(VW * 0.5f - bw * 0.5f, VH * 0.5f - bh * 0.5f, bw, bh);
            else
            {
                bool right = hole.center.x < VW * 0.5f;
                float bx = right ? hole.xMax + 90f : hole.x - 90f - bw;
                if (bx < 20f || bx + bw > VW - 20f) bx = Mathf.Clamp(hole.center.x - bw * 0.5f, 20f, VW - 20f - bw);
                bool besideFits = bx >= hole.xMax + 40f || bx + bw <= hole.x - 40f;
                float by = besideFits ? hole.center.y - bh * 0.5f : (hole.center.y < VH * 0.5f ? hole.yMax + 70f : hole.y - 70f - bh);
                box = new Rect(bx, Mathf.Clamp(by, 20f, VH - 20f - bh), bw, bh);

                // The dotted line from the part to the scroll.
                Vector2 a = new Vector2(Mathf.Clamp(box.center.x, hole.x, hole.xMax), Mathf.Clamp(box.center.y, hole.y, hole.yMax));
                Vector2 b = new Vector2(Mathf.Clamp(a.x, box.x, box.xMax), Mathf.Clamp(a.y, box.y, box.yMax));
                float len = Vector2.Distance(a, b);
                int dots = Mathf.Max(2, Mathf.RoundToInt(len / 16f));
                for (int d = 0; d <= dots; d++)
                {
                    float f = d / (float)dots;
                    if (f > appear) break;
                    Vector2 q = Vector2.Lerp(a, b, f);
                    float s = d == 0 ? 14f : 6f;
                    kit.Dab(new Rect(q.x - s * 0.5f, q.y - s * 0.5f, s, s), Pal.gold.WithAlpha(0.9f));
                }
            }

            kit.PaperPanel(box);
            Label(new Rect(box.x + 40f, box.y + 30f, bw - 80f, 26f), string.Format(Strings.TutorialTourStep, i + 1, n), capsStyle, UiKit.Shu);
            FitLabel(new Rect(box.x + 40f, box.y + 58f, bw - 80f, 56f), title, tutorialTitleStyle, UiKit.Ink);
            Label(new Rect(box.x + 40f, box.y + 122f, bw - 80f, textH), text, tutorialTextStyle, UiKit.Ink);

            // Back and next.
            Event e = Event.current;
            var next = new Rect(box.xMax - 250f, box.yMax - 70f, 210f, 48f);
            var back = new Rect(box.x + 40f, box.yMax - 70f, 170f, 48f);
            kit.Stroke(next, UiKit.Shu.WithAlpha(0.95f));
            Label(next, i + 1 >= n ? Strings.TutorialTourLast : Strings.TutorialTourNext, itemStyle, UiKit.PaperText);
            if (i > 0) Label(back, Strings.TutorialTourBack, smallStyle, UiKit.InkSoft);
            if (!game.Launch.Automated && e.type == EventType.MouseDown && e.button == 0)
            {
                if (next.Contains(e.mousePosition))
                {
                    e.Use();
                    t.NextTourPage();
                }
                else if (i > 0 && back.Contains(e.mousePosition))
                {
                    e.Use();
                    t.PreviousTourPage();
                }
            }
            Label(new Rect(0f, VH - 46f, VW, 34f), Strings.TutorialTourKeys, smallStyleCenter, UiKit.PaperText.WithAlpha(0.85f));
            if (t.SkipArmed) DrawSkipBanner();
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
