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
        static readonly string[] WoundShort = { "braço", "perna", "tronco", "olhos" };

        /// <summary>The second HUD block, under the weapons. Returns false when Mushin hides the HUD.</summary>
        void DrawKiHud(PlayerController p, float x, float y)
        {
            const float w = 440f;
            Color dim = UiKit.PaperDim;

            // Breath.
            float st = p.Stamina / PlayerController.MaxStamina;
            Color sc = p.Exhausted
                ? Color.Lerp(Pal.danger, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f))
                : Pal.breath;
            Label(new Rect(x, y, 120f, 24f), Strings.HudBreath, capsStyle, p.Exhausted ? Pal.danger : dim);
            kit.Bar(new Rect(x + 120f, y + 4f, w - 112f, 16f), st, sc, Color.black.WithAlpha(0.7f));

            // Shura.
            float sh = p.Build != null ? p.Build.Shura / ShuraRules.Max : 0f;
            int level = p.ShuraLevel;
            Color shc = p.InAsura ? Color.Lerp(Pal.shu, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : Pal.shu;
            Label(new Rect(x, y + 28f, 120f, 24f), Strings.HudShura, capsStyle, level > 0 ? Pal.shu : dim);
            kit.Bar(new Rect(x + 120f, y + 32f, w - 112f, 16f), sh, shc, Color.black.WithAlpha(0.7f));
            if (level > 0) Label(new Rect(x + 120f, y + 26f, w - 120f, 24f), ShuraRules.LevelName(level), smallRightStyle, UiKit.PaperText);

            // Guard · gourd · tool · wounds.
            float ry = y + 58f;
            Label(new Rect(x, ry, 200f, 26f), PlayerController.KamaeName(p.Stance), capsStyle, Pal.playerAccent);
            float gx = x + 120f;
            int sips = p.Sips;
            int maxSips = p.MaxSips;
            for (int i = 0; i < maxSips; i++)
            {
                var r = new Rect(gx + i * 22f, ry + 3f, 18f, 18f);
                kit.Dab(r, Color.black.WithAlpha(0.6f));
                if (i < sips) kit.Dab(new Rect(r.x + 2f, r.y + 2f, 14f, 14f), Pal.blessing);
            }
            string tool = p.Build != null ? PlayerController.ToolNames[(int)p.Build.Tool] : string.Empty;
            int papers = p.Build != null ? p.Build.SpiritPapers : 0;
            float tx = gx + maxSips * 22f + 12f;
            Label(new Rect(tx, ry, 400f, 26f), tool + "  ·  " + string.Format(Strings.HudPapers, papers), smallStyle, papers > 0 ? UiKit.PaperText : dim);

            int mask = p.WoundMask;
            if (mask != 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) parts.Add(WoundShort[i]);
                int bandages = p.Build != null ? p.Build.Bandages : 0;
                Label(new Rect(x, ry + 26f, w, 26f), string.Format(Strings.HudWounds, string.Join(", ", parts), bandages), smallStyle, Pal.danger);
            }
            else if (p.CycleFramesLeft > 0)
            {
                Label(new Rect(x, ry + 26f, w, 26f), Strings.PopupCycle, smallStyle, Pal.gold);
            }
            else if (p.Charge > 0)
            {
                Label(new Rect(x, ry + 26f, w, 26f), string.Format(Strings.PopupCharge, p.Charge), smallStyle, p.Weapon.trail);
            }
        }

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
            float a = p.AimLineAngle * Mathf.Deg2Rad;
            var line = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DottedLine(cam, e.Position - line * 2.6f, e.Position + line * 2.6f, Pal.gold, 0.32f, 9f);
            DottedLine(cam, p.Position, p.Position + p.Facing * 3.2f, UiKit.PaperText.WithAlpha(0.8f), 0.4f, 6f);
            Vector2 g = WorldToGui(cam, e.Position + Vector2.up * 1.8f);
            Label(new Rect(g.x - 300f, g.y - 30f, 600f, 30f), Strings.HudCutAim, smallStyleCenter, Pal.gold);
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
        void DrawStandoff(Standoff s)
        {
            Rect full = FullScreen;
            float bar = 110f;
            DrawRect(new Rect(full.x, full.y, full.width, bar), Color.black.WithAlpha(0.92f));
            DrawRect(new Rect(full.x, full.yMax - bar, full.width, bar), Color.black.WithAlpha(0.92f));
            if (s.Advancing)
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
