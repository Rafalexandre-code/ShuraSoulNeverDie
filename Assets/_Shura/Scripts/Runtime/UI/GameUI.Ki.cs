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
                if (e.IsGone || string.IsNullOrEmpty(e.AffixLabel)) continue;
                Vector2 g = WorldToGui(cam, e.Position + Vector2.up * (e.Radius * 2.6f + 0.9f));
                Label(new Rect(g.x - 260f, g.y - 14f, 520f, 28f), e.AffixLabel, smallStyleCenter, Pal.gold);
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
