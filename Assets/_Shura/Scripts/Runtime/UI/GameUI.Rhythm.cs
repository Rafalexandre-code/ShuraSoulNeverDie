using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A trilha de ritmo (experimento do diretor, no estilo de BPM): embaixo, no centro. Da
    /// esquerda chegam os golpes que miram você — uma barra azul (aparar), duas vermelhas
    /// (esquivar), duas laranja (Troca Perfeita), uma cinza tracejada (finta: espere) — e cada
    /// uma cruza o marcador central no instante ideal. Depois de um parry perfeito, uma barra
    /// dourada vem da direita: ataque quando ela cruzar o centro.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Quantos quadros antes do instante ideal a marca entra na trilha.</summary>
        const int RhythmLookAhead = 54;
        const float RhythmHalfWidth = 270f;

        static readonly Color RhythmParry = new Color(0.45f, 0.9f, 1f);
        static readonly Color RhythmSwap = new Color(1f, 0.6f, 0.2f);
        static readonly Color RhythmFeint = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        static readonly Color RhythmCursed = new Color(0.55f, 0.9f, 0.45f);

        void DrawRhythmTrack()
        {
            World w = game.World;
            PlayerController host = w?.Host;
            if (host == null || game.Run == null || !game.Settings.rhythmTrack || w.TutorialRoom) return;
            Rect fs = FullScreen;
            var c = new Vector2(fs.center.x, fs.yMax - 128f);
            float hw = RhythmHalfWidth;

            // A faixa de tinta e o marcador central.
            kit.Dab(new Rect(c.x - hw - 24f, c.y - 26f, hw * 2f + 48f, 52f), Color.black.WithAlpha(0.5f));
            DrawRect(new Rect(c.x - hw, c.y - 1f, hw * 2f, 2f), Color.white.WithAlpha(0.22f));
            // Marcas de compasso: um quarto da trilha cada (≈ 13 quadros).
            for (int i = 1; i <= 4; i++)
            {
                float dx = hw * i / 4f;
                DrawRect(new Rect(c.x - dx - 1f, c.y - 5f, 2f, 10f), Color.white.WithAlpha(0.18f));
                DrawRect(new Rect(c.x + dx - 1f, c.y - 5f, 2f, 10f), Color.white.WithAlpha(0.18f));
            }
            int lead = game.Config.feel.timingCueLeadFrames;
            float nearest = float.MaxValue;
            string nearestKey = null;

            foreach (Enemy e in w.Enemies)
            {
                if (e.IsGone || e.IsAlly || e.Target != host) continue;
                AttackDef a = e.CurrentAttack;
                if (a == null) continue;
                int toIdeal = e.FramesUntilActive - lead;
                if (toIdeal > RhythmLookAhead || toIdeal < -6) continue;
                float x = c.x - hw * Mathf.Clamp(toIdeal / (float)RhythmLookAhead, -0.08f, 1f);
                float alpha = Mathf.Clamp01((RhythmLookAhead - toIdeal) / 10f) * (toIdeal < 0 ? 0.4f : 1f);
                RingKind k = e.CurrentRingKind;
                switch (k)
                {
                    case RingKind.Danger:
                        RhythmBars(x, c.y, 2, Pal.danger, alpha);
                        break;
                    case RingKind.Spirit:
                        RhythmBars(x, c.y, 2, RhythmSwap, alpha);
                        break;
                    case RingKind.Feint:
                        RhythmBars(x, c.y, 1, RhythmFeint, alpha * 0.7f, true);
                        break;
                    case RingKind.Cursed:
                        RhythmBars(x, c.y, 1, RhythmCursed, alpha);
                        break;
                    default:
                        RhythmBars(x, c.y, 1, RhythmParry, alpha);
                        break;
                }
                if (toIdeal >= 0 && toIdeal < nearest)
                {
                    nearest = toIdeal;
                    nearestKey = k == RingKind.Danger ? game.Controls.Glyph(ControlAction.Dodge)
                               : k == RingKind.Spirit ? game.Controls.Glyph(ControlAction.Swap)
                               : k == RingKind.Feint ? Strings.RhythmWait
                               : game.Controls.Glyph(ControlAction.Parry);
                }
            }

            // A batida do contra-ataque, vinda da direita.
            if (w.RiposteDueTick >= 0)
            {
                int toBeat = w.RiposteDueTick - w.SimTick;
                float x = c.x + hw * Mathf.Clamp(toBeat / (float)World.RiposteDelayFrames, -0.08f, 1f);
                RhythmBars(x, c.y, 1, Pal.gold, 1f);
                if (toBeat >= 0 && toBeat < nearest)
                {
                    nearest = toBeat;
                    nearestKey = game.Controls.Glyph(ControlAction.Attack);
                }
            }

            // O marcador: duas linhas finas e um losango; acende com a última batida.
            int age = w.SimTick - w.LastBeatTick;
            Color mark = Color.white.WithAlpha(0.85f);
            string label = null;
            if (age >= 0 && age < 24)
            {
                switch (w.LastBeat)
                {
                    case RhythmBeat.Perfect: mark = RhythmParry; label = Strings.RhythmPerfect; break;
                    case RhythmBeat.Swap: mark = RhythmSwap; label = Strings.RhythmSwap; break;
                    case RhythmBeat.Dodge: mark = Pal.danger; label = Strings.RhythmDodge; break;
                    case RhythmBeat.Block: mark = UiKit.PaperDim; label = Strings.RhythmBlock; break;
                    case RhythmBeat.Hit: mark = Pal.danger.WithAlpha(0.6f); label = Strings.RhythmHit; break;
                    case RhythmBeat.RiposteOnBeat: mark = Pal.gold; label = Strings.PopupOnBeat; break;
                    case RhythmBeat.RiposteMissed: mark = UiKit.PaperDim; label = Strings.RhythmOffBeat; break;
                }
            }
            float glow = age >= 0 && age < 24 ? 1f - age / 24f : 0f;
            if (glow > 0f) kit.Dab(new Rect(c.x - 34f, c.y - 34f, 68f, 68f), mark.WithAlpha(0.35f * glow));
            DrawRect(new Rect(c.x - 13f, c.y - 24f, 2f, 48f), mark);
            DrawRect(new Rect(c.x + 11f, c.y - 24f, 2f, 48f), mark);
            DrawTex(new Rect(c.x - 5f, c.y - 36f, 10f, 10f), ShapeLibrary.Get(Shape.Diamond).texture, mark);
            if (label != null) Label(new Rect(c.x - 200f, c.y - 70f, 400f, 30f), label, capsCenterStyle, mark.WithAlpha(Mathf.Clamp01(glow * 1.5f)));
            else if (nearestKey != null) Label(new Rect(c.x - 200f, c.y + 26f, 400f, 26f), nearestKey, capsCenterStyle, UiKit.PaperDim.WithAlpha(0.8f));
        }

        /// <summary>Uma ou duas barras verticais de tinta numa posição da trilha.</summary>
        void RhythmBars(float x, float y, int count, Color c, float alpha, bool dashed = false)
        {
            const float h = 34f, wBar = 6f, gap = 6f;
            float total = count * wBar + (count - 1) * gap;
            for (int i = 0; i < count; i++)
            {
                float bx = x - total * 0.5f + i * (wBar + gap);
                if (!dashed)
                {
                    DrawRect(new Rect(bx, y - h * 0.5f, wBar, h), c.WithAlpha(c.a * alpha));
                    continue;
                }
                for (int s = 0; s < 4; s++) DrawRect(new Rect(bx, y - h * 0.5f + s * 9f, wBar, 5f), c.WithAlpha(c.a * alpha));
            }
        }
    }
}
