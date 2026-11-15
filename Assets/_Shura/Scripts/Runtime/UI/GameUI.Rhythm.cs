using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A trilha de ritmo (pedido do diretor, no estilo de jogo de ritmo): embaixo, no centro.
    /// Todo golpe que mira você é ANUNCIADO (Enemy.Intent): a nota entra pela borda esquerda no
    /// instante do anúncio e cruza a zona de acerto no momento exato de responder — nunca nasce
    /// em cima da hora. Um combo chega como uma fila de notas ligadas, cada uma na sua batida.
    /// Azul = aparar · duas vermelhas = esquivar · duas laranja = Troca Perfeita · tracejada =
    /// finta (espere) · verde = amaldiçoado. Depois de um parry perfeito, a batida dourada do
    /// contra-ataque vem da direita. Só visual: lê o plano dos inimigos.
    /// </summary>
    public sealed partial class GameUI
    {
        const float RhythmHalfWidth = 360f;
        /// <summary>Cada quanto (quadros) passa uma marca de compasso pela trilha.</summary>
        const int RhythmTickFrames = 15;

        static readonly Color RhythmParry = new Color(0.42f, 0.86f, 1f);
        static readonly Color RhythmSwap = new Color(1f, 0.58f, 0.18f);
        static readonly Color RhythmFeint = new Color(0.82f, 0.8f, 0.74f, 0.75f);
        static readonly Color RhythmCursed = new Color(0.55f, 0.92f, 0.45f);
        static readonly Color RhythmPlate = new Color(0.035f, 0.03f, 0.05f, 0.82f);

        readonly List<UpcomingBeat> rhythmBeats = new List<UpcomingBeat>();
        int rhythmLastBeatSeen = -1;
        float rhythmBurstAt = -10f;

        /// <summary>Quantos quadros a trilha mostra: exatamente a antecedência do anúncio, para toda nota entrar pela borda.</summary>
        int RhythmLookAhead => Mathf.Max(20, game.Config.feel.attackIntentMinLeadFrames - game.Config.feel.timingCueLeadFrames);

        Color RhythmColor(RingKind k)
        {
            switch (k)
            {
                case RingKind.Danger: return Pal.danger;
                case RingKind.Spirit: return RhythmSwap;
                case RingKind.Feint: return RhythmFeint;
                case RingKind.Cursed: return RhythmCursed;
                case RingKind.Group: return Color.white;
                default: return RhythmParry;
            }
        }

        void DrawRhythmTrack()
        {
            World w = game.World;
            PlayerController host = w?.Host;
            if (host == null || game.Run == null || !game.Settings.rhythmTrack || w.TutorialRoom) return;
            Rect fs = FullScreen;
            var c = new Vector2(fs.center.x, fs.yMax - 132f);
            float hw = RhythmHalfWidth;
            int look = RhythmLookAhead;
            int lead = game.Config.feel.timingCueLeadFrames;
            float sub = SimSubFrame();

            DrawRhythmPlate(c, hw, look, sub);

            // As notas: cada inimigo que mira você, com o combo inteiro.
            float nearest = float.MaxValue;
            RingKind nearestKind = RingKind.Parry;
            bool any = false;
            foreach (Enemy e in w.Enemies)
            {
                if (e.IsGone || e.IsAlly || e.Target != host) continue;
                e.UpcomingBeats(rhythmBeats);
                Vector2 prev = Vector2.zero;
                bool hasPrev = false;
                foreach (UpcomingBeat b in rhythmBeats)
                {
                    float toIdeal = b.framesToImpact - lead - sub;
                    if (toIdeal > look + 2f || toIdeal < -8f) { hasPrev = false; continue; }
                    float u = Mathf.Clamp(toIdeal / look, -0.12f, 1f);
                    var at = new Vector2(c.x - hw * u, c.y);
                    Color col = RhythmColor(b.kind);
                    // O combo: um fio ligando as notas do mesmo inimigo.
                    if (hasPrev) DrawRect(new Rect(at.x, c.y - 1f, prev.x - at.x, 2f), col.WithAlpha(0.35f));
                    RhythmNote(at, b.kind, col, u);
                    prev = at;
                    hasPrev = true;
                    any = true;
                    if (toIdeal >= -2f && toIdeal < nearest)
                    {
                        nearest = toIdeal;
                        nearestKind = b.kind;
                    }
                }
            }

            // A batida do contra-ataque, vinda da direita.
            if (w.RiposteDueTick >= 0)
            {
                float toBeat = w.RiposteDueTick - w.SimTick - sub;
                float u = Mathf.Clamp(toBeat / World.RiposteDelayFrames, -0.12f, 1f);
                var at = new Vector2(c.x + hw * u, c.y);
                RhythmTrail(at, Pal.gold, -1f, u);
                RhythmCapsule(at, Pal.gold, Mathf.Lerp(1.2f, 0.8f, u), 1f);
                if (toBeat >= -2f && toBeat < nearest)
                {
                    nearest = toBeat;
                    nearestKind = RingKind.Group;
                }
            }

            DrawRhythmJudge(c, w, any || w.RiposteDueTick >= 0, nearest, nearestKind, look);
        }

        /// <summary>Quanto da próxima simulação já passou (a nota anda liso entre os quadros).</summary>
        float SimSubFrame() => Mathf.Clamp01(game.Clock != null ? game.Clock.Alpha : 0f);

        /// <summary>A placa laqueada da trilha, os frisos dourados e as marcas de compasso correndo.</summary>
        void DrawRhythmPlate(Vector2 c, float hw, int look, float sub)
        {
            var plate = new Rect(c.x - hw - 40f, c.y - 30f, hw * 2f + 80f, 60f);
            kit.Dab(new Rect(plate.x - 30f, plate.y - 16f, plate.width + 60f, plate.height + 32f), Color.black.WithAlpha(0.45f));
            DrawRect(plate, RhythmPlate);
            // Frisos dourados que somem nas pontas.
            const int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
                float fade = Mathf.Sin(Mathf.PI * (t0 + t1) * 0.5f);
                float x0 = Mathf.Lerp(plate.x, plate.xMax, t0), x1 = Mathf.Lerp(plate.x, plate.xMax, t1);
                DrawRect(new Rect(x0, plate.y, x1 - x0, 2f), Pal.gold.WithAlpha(0.55f * fade));
                DrawRect(new Rect(x0, plate.yMax - 2f, x1 - x0, 2f), Pal.gold.WithAlpha(0.55f * fade));
            }
            // A pista e as marcas de compasso, que correm para o centro na velocidade das notas.
            DrawRect(new Rect(c.x - hw, c.y - 1f, hw * 2f, 2f), Color.white.WithAlpha(0.1f));
            float phase = Mathf.Repeat((game.World.SimTick + sub) / RhythmTickFrames, 1f);
            int marks = look / RhythmTickFrames + 1;
            for (int i = 0; i <= marks; i++)
            {
                float frames = (i + 1f - phase) * RhythmTickFrames;
                float u = frames / look;
                if (u > 1f) continue;
                float a = 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - u));
                DrawRect(new Rect(c.x - hw * u - 1f, c.y - 8f, 2f, 16f), Color.white.WithAlpha(a));
            }
        }

        /// <summary>Uma nota, com o desenho da resposta que ela pede.</summary>
        void RhythmNote(Vector2 at, RingKind kind, Color col, float u)
        {
            float appear = Mathf.Clamp01((1f - u) * 6f);
            float grow = Mathf.Lerp(1.22f, 0.78f, Mathf.Clamp01(u));
            float late = u < 0f ? 0.35f : 1f;
            RhythmTrail(at, col, 1f, u);
            switch (kind)
            {
                case RingKind.Danger:
                    RhythmCapsule(at + new Vector2(-7f, 0f), col, grow, appear * late);
                    RhythmCapsule(at + new Vector2(7f, 0f), col, grow, appear * late);
                    DrawTex(new Rect(at.x - 7f, at.y - 44f * grow, 14f, 14f), ShapeLibrary.Get(Shape.Diamond).texture, col.WithAlpha(appear * late));
                    break;
                case RingKind.Spirit:
                    DrawRect(new Rect(at.x - 8f, at.y - 2f, 16f, 4f), col.WithAlpha(0.8f * appear * late));
                    RhythmCapsule(at + new Vector2(-8f, 0f), col, grow, appear * late);
                    RhythmCapsule(at + new Vector2(8f, 0f), col, grow, appear * late);
                    break;
                case RingKind.Feint:
                    for (int s = 0; s < 4; s++)
                        DrawRect(new Rect(at.x - 3f, at.y - 20f * grow + s * 11f * grow, 6f, 6f * grow), col.WithAlpha(0.8f * appear * late));
                    break;
                case RingKind.Double:
                    RhythmCapsule(at, col, grow * 1.25f, 0.35f * appear * late);
                    RhythmCapsule(at, col, grow, appear * late);
                    break;
                default:
                    RhythmCapsule(at, col, grow, appear * late);
                    break;
            }
        }

        /// <summary>Uma cápsula de luz: halo macio, corpo na cor e um miolo branco.</summary>
        void RhythmCapsule(Vector2 at, Color col, float scale, float alpha)
        {
            float h = 40f * scale, wBody = 9f * scale;
            Texture soft = ShapeLibrary.Get(Shape.SoftCircle).texture;
            DrawTex(new Rect(at.x - 18f * scale, at.y - h * 0.75f, 36f * scale, h * 1.5f), soft, col.WithAlpha(0.45f * alpha));
            DrawRect(new Rect(at.x - wBody * 0.5f, at.y - h * 0.5f, wBody, h), col.WithAlpha(alpha));
            DrawRect(new Rect(at.x - wBody * 0.5f + 2f, at.y - h * 0.5f + 3f, Mathf.Max(1f, wBody - 4f), h - 6f), Color.Lerp(col, Color.white, 0.65f).WithAlpha(alpha));
        }

        /// <summary>O rastro de velocidade atrás da nota (dir = 1 vem da esquerda, -1 da direita).</summary>
        void RhythmTrail(Vector2 at, Color col, float dir, float u)
        {
            float len = 46f * Mathf.Clamp01(1f - u * 0.6f);
            const int parts = 6;
            for (int i = 0; i < parts; i++)
            {
                float t = i / (float)parts;
                float x = at.x - dir * (6f + len * t);
                DrawRect(new Rect(Mathf.Min(x, x - dir * len / parts), at.y - 3f * (1f - t), len / parts, 6f * (1f - t)), col.WithAlpha(0.28f * (1f - t)));
            }
        }

        /// <summary>A zona de acerto: ensō, a faixa da janela perfeita, a tecla e o lampejo da última batida.</summary>
        void DrawRhythmJudge(Vector2 c, World w, bool busy, float nearest, RingKind nearestKind, int look)
        {
            float hw = RhythmHalfWidth;
            // A janela perfeita, do tamanho real em quadros.
            int window = game.Config.player.perfectParryFrames;
            float ww = hw * window / look;
            Color soon = nearest < 10f ? RhythmColor(nearestKind) : Color.white;
            float near = nearest < 18f ? 1f - nearest / 18f : 0f;
            DrawRect(new Rect(c.x - ww * 0.5f, c.y - 24f, ww, 48f), soon.WithAlpha(0.08f + 0.18f * near));

            // O lampejo da batida: anel que se abre, faíscas e o nome saltando.
            int age = w.SimTick - w.LastBeatTick;
            if (w.LastBeatTick != rhythmLastBeatSeen && age >= 0 && age < 3)
            {
                rhythmLastBeatSeen = w.LastBeatTick;
                rhythmBurstAt = Time.unscaledTime;
            }
            float since = Time.unscaledTime - rhythmBurstAt;
            Color beatCol = BeatColor(w.LastBeat, out string label);
            if (since < 0.45f && label != null)
            {
                float k = since / 0.45f;
                float r = Mathf.Lerp(30f, 110f, Mathf.Sqrt(k));
                kit.Enso(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), beatCol.WithAlpha(0.8f * (1f - k)), k * 90f);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f * Mathf.Deg2Rad + rhythmBurstAt;
                    float d = Mathf.Lerp(10f, 70f, k) * (0.7f + 0.3f * Mathf.Sin(i * 2.3f));
                    var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * d;
                    DrawRect(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), beatCol.WithAlpha(1f - k));
                }
            }

            // O ensō e a linha de julgamento.
            float pulse = busy ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) * near : 0f;
            kit.Enso(new Rect(c.x - 30f, c.y - 30f, 60f, 60f), Color.Lerp(Pal.gold, soon, near).WithAlpha(0.7f + 0.3f * pulse), Time.unscaledTime * 12f);
            DrawRect(new Rect(c.x - 1.5f, c.y - 26f, 3f, 52f), Color.Lerp(Color.white, soon, near));
            DrawTex(new Rect(c.x - 6f, c.y - 40f, 12f, 12f), ShapeLibrary.Get(Shape.Diamond).texture, Pal.gold);

            if (since < 0.9f && label != null)
            {
                float pop = since < 0.12f ? Mathf.Lerp(1.6f, 1f, since / 0.12f) : 1f;
                float fade = Mathf.Clamp01((0.9f - since) / 0.3f);
                var lr = new Rect(c.x - 220f * pop, c.y - 88f - 16f * (pop - 1f), 440f * pop, 36f * pop);
                Label(lr, label, capsCenterStyle, beatCol.WithAlpha(fade));
            }
            else if (nearest < float.MaxValue)
            {
                // A tecla da próxima resposta, num botão sob o centro.
                string key = nearestKind == RingKind.Danger ? game.Controls.Glyph(ControlAction.Dodge)
                           : nearestKind == RingKind.Spirit ? game.Controls.Glyph(ControlAction.Swap)
                           : nearestKind == RingKind.Feint ? Strings.RhythmWait
                           : nearestKind == RingKind.Group ? game.Controls.Glyph(ControlAction.Attack)
                           : game.Controls.Glyph(ControlAction.Parry);
                Label(new Rect(c.x - 160f, c.y + 34f, 320f, 28f), key, capsCenterStyle, RhythmColor(nearestKind).WithAlpha(0.6f + 0.4f * near));
            }
        }

        Color BeatColor(RhythmBeat b, out string label)
        {
            switch (b)
            {
                case RhythmBeat.Perfect: label = Strings.RhythmPerfect; return RhythmParry;
                case RhythmBeat.Swap: label = Strings.RhythmSwap; return RhythmSwap;
                case RhythmBeat.Dodge: label = Strings.RhythmDodge; return Pal.danger;
                case RhythmBeat.Block: label = Strings.RhythmBlock; return UiKit.PaperDim;
                case RhythmBeat.Hit: label = Strings.RhythmHit; return Pal.danger.WithAlpha(0.7f);
                case RhythmBeat.RiposteOnBeat: label = Strings.PopupOnBeat; return Pal.gold;
                case RhythmBeat.RiposteMissed: label = Strings.RhythmOffBeat; return UiKit.PaperDim;
                default: label = null; return Color.white;
            }
        }
    }
}
