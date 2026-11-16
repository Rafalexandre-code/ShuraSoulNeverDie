using UnityEngine;

namespace Shura
{
    /// <summary>
    /// WP-06: a leitura que nunca some. Um inimigo fora da tela que começa um golpe contra você
    /// ganha uma seta na borda, com a cor e a forma do anel daquele golpe (D-19: nenhum dano de
    /// fora da tela sem aviso). E no Mushin, quando o HUD some, as cargas de esquiva ficam sob os
    /// pés do samurai.
    /// </summary>
    public sealed partial class GameUI
    {
        const float OffscreenMargin = 64f;

        /// <summary>Quadro em que a última seta fora da tela foi desenhada (capturas automáticas).</summary>
        public static int LastOffscreenArrowFrame { get; private set; } = -1;

        void DrawOffscreenThreats()
        {
            World w = game.World;
            PlayerController host = w?.Host;
            Camera cam = game.CameraRig != null ? game.CameraRig.Cam : null;
            if (host == null || cam == null || !w.ShowTimingIndicator || host.RingsHidden) return;
            Rect fs = FullScreen;
            Vector2 c = fs.center;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f);
            foreach (Enemy e in w.Enemies)
            {
                if (e.IsGone || e.NetRing == 0 || (e.Target != null && e.Target != host)) continue;
                Vector2 g = WorldToGui(cam, e.RenderPosition + Vector2.up * e.Radius);
                bool inside = g.x > fs.xMin + 16f && g.x < fs.xMax - 16f && g.y > fs.yMin + 16f && g.y < fs.yMax - 16f;
                if (inside) continue;
                Vector2 d = g - c;
                if (d.sqrMagnitude < 1f) continue;
                float sx = (fs.width * 0.5f - OffscreenMargin) / Mathf.Max(1e-3f, Mathf.Abs(d.x));
                float sy = (fs.height * 0.5f - OffscreenMargin) / Mathf.Max(1e-3f, Mathf.Abs(d.y));
                Vector2 at = c + d * Mathf.Min(sx, sy);
                var kind = (RingKind)e.NetRingKind;
                Color col = e.RingColor(kind);
                // Fechando (2): a seta pisca mais forte, como o "kira" na ponta da arma.
                float a = e.NetRing == 2 ? 1f : 0.55f + 0.35f * e.NetRingProgress;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                DrawSpriteTinted(new Rect(at.x - 34f, at.y - 34f, 68f, 68f), ShapeLibrary.Get(Shape.SoftCircle), col.WithAlpha(0.35f * a * pulse));
                UiKit.Rotated(at, angle, () =>
                    DrawSpriteTinted(new Rect(at.x + 20f, at.y - 13f, 26f, 26f), ShapeLibrary.Get(Shape.Triangle), col.WithAlpha(a)));
                // A forma do anel (liso, losango, pontilhado, tracejado): lê-se também sem a cor.
                DrawSpriteTinted(new Rect(at.x - 22f, at.y - 22f, 44f, 44f), ShapeLibrary.RingPattern((int)kind), col.WithAlpha(a));
                if (Event.current.type == EventType.Repaint) LastOffscreenArrowFrame = Time.frameCount;
            }
        }

        /// <summary>Mushin: o HUD some, mas as cargas de esquiva continuam sob os pés.</summary>
        void DrawDodgePipsUnderfoot(PlayerController p)
        {
            Camera cam = game.CameraRig != null ? game.CameraRig.Cam : null;
            if (cam == null || p == null || !p.IsAlive) return;
            Vector2 g = WorldToGui(cam, p.RenderPosition + Vector2.down * 0.32f);
            int max = p.MaxDodgeCharges;
            float w = 14f, gap = 8f;
            float x0 = g.x - (max * w + (max - 1) * gap) * 0.5f;
            for (int i = 0; i < max; i++)
            {
                float fill = Mathf.Clamp01(p.DodgeCharges - i);
                var r = new Rect(x0 + i * (w + gap), g.y, w, w);
                DrawSpriteTinted(r, ShapeLibrary.Get(Shape.Ring), UiKit.PaperText.WithAlpha(0.7f));
                if (fill >= 1f) DrawSpriteTinted(r, ShapeLibrary.Get(Shape.Circle), Pal.playerAccent);
                else if (fill > 0f) DrawSpriteTinted(new Rect(r.center.x - w * 0.5f * fill, r.center.y - w * 0.5f * fill, w * fill, w * fill), ShapeLibrary.Get(Shape.Circle), Pal.playerAccent.WithAlpha(0.6f));
            }
        }
    }
}
