using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The HUD of the combat layers (design doc §3): breath, the Shura meter, the guard,
    /// the gourd, the tool and its spirit papers, wounds — plus the two world overlays,
    /// the golden line of a directed finisher and the stillness of an Iai standoff.
    /// In Mushin all of it fades: the mind is empty, and so is the screen.
    /// </summary>
    public sealed partial class GameUI
    {

        /// <summary>What each deep elite carries, written over its head.</summary>
        void DrawAffixLabels()
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            foreach (Enemy e in game.World.Enemies)
            {
                // The element an enemy carries: a coloured mark that empties as it fades.
                if (!e.IsGone && e.ActiveElement != Element.None)
                {
                    Vector2 m = WorldToGui(cam, e.Position + Vector2.up * (e.Radius * 2.6f + 0.55f));
                    Color ec = Elements.Color(e.ActiveElement);
                    kit.Dab(new Rect(m.x - 13f, m.y - 13f, 26f, 26f), new Color(0f, 0f, 0f, 0.6f));
                    kit.Dab(new Rect(m.x - 9f, m.y - 9f, 18f, 18f), ec.WithAlpha(0.35f + 0.65f * e.ElementRatio));
                }
                if (e.IsGone || string.IsNullOrEmpty(e.AffixLabel)) continue;
                Vector2 g = WorldToGui(cam, e.Position + Vector2.up * (e.Radius * 2.6f + 0.9f));
                Label(new Rect(g.x - 260f, g.y - 14f, 520f, 28f), e.AffixLabel, smallStyleCenter, Pal.gold);
            }
        }

        /// <summary>Voto de Silêncio: the host's duelist fights with no interface at all.</summary>
        bool SilentHud
        {
            get
            {
                PlayerController p = game.World != null ? game.World.Host : null;
                return p != null && p.Build != null && p.Build.Modifiers.hideHud && !game.Tutorial.Active;
            }
        }

        /// <summary>Olho do Dragão: over every enemy, a sign of the blow it will throw next.</summary>
        void DrawDragonEye()
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            foreach (Enemy e in game.World.Enemies)
            {
                if (e.IsGone || e.IsAlly || e.NextMove == null || e.CurrentState != Enemy.State.Engage) continue;
                bool danger = false, spirit = false;
                foreach (AttackDef a in e.NextMove.steps)
                {
                    if (a.IsDangerous) danger = true;
                    if (a.kind == AttackKind.Spirit) spirit = true;
                }
                string sign = danger ? "危" : spirit ? "霊" : "受";
                Color c = danger ? Pal.danger : spirit ? ColorUtil.Hex("#B07CFF") : Pal.playerAccent;
                Vector2 g = WorldToGui(cam, e.RenderPosition + Vector2.up * (e.Radius * 2.6f + 1.45f));
                kit.Dab(new Rect(g.x - 20f, g.y - 20f, 40f, 40f), new Color(0f, 0f, 0f, 0.55f));
                Label(new Rect(g.x - 30f, g.y - 22f, 60f, 44f), sign, capsCenterStyle, c);
            }
        }

        /// <summary>Mushin: just one character in the corner, everything else gone.</summary>
        void DrawMushinHud()
        {
            float a = 0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f);
            Label(new Rect(30f, 20f, 300f, 80f), "無心", titleStyle, UiKit.PaperText.WithAlpha(a));
        }

        /// <summary>The golden line of a directed finisher, and where the duelist's cut is pointing.</summary>
        void DrawFinisherAim(PlayerController p)
        {
            Enemy e = p.AimTarget;
            Camera cam = game.CameraRig.Cam;
            if (e == null || cam == null) return;
            // The golden line: from the duelist to a point of the enemy's body (and a little past it).
            Vector2 to = p.AimPoint - p.Position;
            float len = to.magnitude;
            Vector2 dir = len > 1e-3f ? to / len : p.Facing;
            DottedLine(cam, p.Position, p.AimPoint + dir * 0.8f, Pal.gold, 0.28f, 10f);
            Vector2 hit = WorldToGui(cam, p.AimPoint);
            kit.Enso(new Rect(hit.x - 22f, hit.y - 22f, 44f, 44f), Pal.gold, Time.unscaledTime * 120f);
            // Where the blade is pointing now: white, then gold as it meets the line.
            float err = p.AimError;
            Color aim = err <= PlayerController.CutPerfectDegrees ? Pal.gold
                      : err <= PlayerController.CutLineDegrees ? Color.Lerp(Pal.gold, UiKit.PaperText, 0.5f) : UiKit.PaperText.WithAlpha(0.7f);
            DottedLine(cam, p.Position, p.Position + p.Facing * (len + 0.8f), aim, 0.4f, 6f);
            Vector2 g = WorldToGui(cam, e.Position + Vector2.up * 2f);
            Label(new Rect(g.x - 420f, g.y - 30f, 840f, 30f), Strings.HudCutAim, smallStyleCenter, Pal.gold);
            if (err <= PlayerController.CutPerfectDegrees) Label(new Rect(g.x - 200f, g.y - 64f, 400f, 36f), Strings.HudCutPerfect, cardKindStyle, Pal.gold);
        }

        void DottedLine(Camera cam, Vector2 from, Vector2 to, Color c, float spacing, float size)
        {
            float len = Vector2.Distance(from, to);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
            for (int i = 0; i <= n; i++)
            {
                Vector2 g = WorldToGui(cam, Vector2.Lerp(from, to, i / (float)n));
                DrawRect(new Rect(g.x - size * 0.5f, g.y - size * 0.5f, size, size), c);
            }
        }

        /// <summary>The Iai standoff: black bars, silence, one instruction.</summary>
        void DrawStandoffBars(bool advancing)
        {
            Rect full = FullScreen;
            float bar = 110f;
            DrawRect(new Rect(full.x, full.y, full.width, bar), Color.black.WithAlpha(0.92f));
            DrawRect(new Rect(full.x, full.yMax - bar, full.width, bar), Color.black.WithAlpha(0.92f));
            if (advancing)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 30f);
                Label(new Rect(0, full.yMax - bar + 20f, VW, 70f), Strings.HudStandoffNow, roomStyle, Pal.danger.WithAlpha(pulse));
            }
            else
            {
                string key = game.Controls.UsingGamepad ? "RB" : Strings.HudMouseLeft;
                Label(new Rect(0, full.yMax - bar + 24f, VW, 60f), string.Format(Strings.HudStandoffHold, key), smallStyleCenter, UiKit.PaperText);
            }
            Label(new Rect(0, full.y + 30f, VW, 60f), Strings.HudStandoffTitle, roomStyle, UiKit.PaperText.WithAlpha(0.85f));
        }
    }
}
