using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The road on screen (design doc §6): the route-map scroll between rooms, and in the
    /// room the hour, the weather, darkness and fog, the biome's rule, the challenge, the
    /// encounter's banner and what can be used, the coin purse and the curse.
    /// </summary>
    public sealed partial class GameUI
    {
        Texture2D holeTex;

        // ── Route map ────────────────────────────────────────────────────────────────

        void HandleRoute(InputService input)
        {
            int count = game.RouteOptions.Count;
            if (count == 0) return;
            menuIndex = Mathf.Clamp(menuIndex, 0, count - 1);
            int nav = input.NavY != 0 ? -input.NavY : input.NavX;
            if (nav != 0)
            {
                menuIndex = (menuIndex + nav + count) % count;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) game.VoteRoute(-1, menuIndex);
        }

        static string KalpaNames(RunState run)
        {
            var names = new string[run.KalpaRules.Count];
            for (int i = 0; i < names.Length; i++) names[i] = KalpaRules.Name(run.KalpaRules[i]);
            return string.Join(", ", names);
        }

        // ── In the room ──────────────────────────────────────────────────────────────

        /// <summary>Under the room's name: the door, the hour, the weather, the Kalpa and the biome's rule.</summary>
        void DrawRoadLine(RoomDef room)
        {
            World w = game.World;
            var parts = new List<string>();
            if (room.kind != RoomKind.Combat) parts.Add(RouteMap.KindName(room.kind));
            parts.Add(Biomes.TimeName(w.TimeOfDay) + "  " + DayClock.Format(w.Hour));
            if (w.Weather != Weather.Clear) parts.Add(Biomes.WeatherName(w.Weather));
            if (game.Run.Kalpa > 1) parts.Add("Kalpa " + KalpaRules.Numeral(game.Run.Kalpa));
            Label(new Rect(0, 132, VW, 30), string.Join("  ·  ", parts), smallStyleCenter, UiKit.PaperDim);
            FitLabel(new Rect(VW * 0.5f - 320f, 158, 640f, 30), Biomes.RuleText(w.BiomeRule), smallStyleCenter, UiKit.PaperDim.WithAlpha(0.75f));

            if (room.challenge != ChallengeKind.None)
            {
                string text = w.ChallengeFailed ? Strings.HudChallengeFailed : string.Format(Strings.HudChallenge, RouteMap.ChallengeName(room.challenge));
                if (room.challenge == ChallengeKind.Timed && !w.ChallengeFailed) text += "  ·  " + Mathf.CeilToInt(w.ChallengeTimeLeft) + " s";
                Label(new Rect(0, 190, VW, 40), text, cardKindStyle, w.ChallengeFailed ? Pal.danger : Pal.gold);
            }
            if (!string.IsNullOrEmpty(w.EncounterTitle))
            {
                var banner = new Rect(VW * 0.5f - 420f, 236f, 840f, 110f);
                kit.InkPanel(banner, 0.9f);
                Label(new Rect(banner.x, banner.y + 10f, banner.width, 44f), w.EncounterTitle, cardKindStyle, Pal.gold);
                Label(new Rect(banner.x + 40f, banner.y + 52f, banner.width - 80f, 50f), w.EncounterText ?? "", smallStyleCenter, UiKit.PaperText);
            }
        }

        /// <summary>Names, prices and the hold ring over what can be used in an encounter.</summary>
        void DrawOfferings()
        {
            Camera cam = game.CameraRig.Cam;
            World w = game.World;
            if (cam == null || w.Offerings.Count == 0) return;
            PlayerController me = w.Host;
            foreach (Offering o in w.Offerings)
            {
                bool available = o.AvailableTo(me);
                Vector2 g = WorldToGui(cam, o.Position + Vector2.up * (o.Exit ? 2.6f : 1.9f));
                Color c = o.Spent || !available ? UiKit.PaperDim.WithAlpha(0.6f) : o.Color;
                string title = o.Title + (o.Price > 0 ? "  ·  " + o.Price + " Mon" : "");
                Label(new Rect(g.x - 260f, g.y - 30f, 520f, 30f), title, smallStyleCenter, c);
                bool near = Vector2.Distance(me.Position, o.Position) <= (o.Exit ? 1.8f : 1.1f);
                if (near && !string.IsNullOrEmpty(o.Detail))
                {
                    var box = new Rect(g.x - 260f, g.y - 96f, 520f, 64f);
                    kit.InkPanel(box, 0.85f);
                    Label(new Rect(box.x + 14f, box.y + 6f, box.width - 28f, box.height - 12f), o.Detail + (available && !o.Exit ? "  (" + Strings.HudHold + ")" : ""), smallStyleCenter, UiKit.PaperText);
                }
                float prog = o.MaxProgress / (o.Exit ? World.ExitHoldSeconds : World.OfferingHoldSeconds);
                if (prog > 0f) kit.Bar(new Rect(g.x - 60f, g.y + 2f, 120f, 10f), prog, Pal.gold, Color.black.WithAlpha(0.6f));
            }
        }

        void DrawAllyLabels()
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            foreach (Enemy e in game.World.Enemies)
            {
                if (!e.IsAlly || e.IsGone) continue;
                Vector2 g = WorldToGui(cam, e.Position + Vector2.up * (e.Radius * 2.6f + 0.9f));
                Label(new Rect(g.x - 200f, g.y - 14f, 400f, 28f), Strings.HudAlly, smallStyleCenter, Pal.blessing);
            }
        }

        // ── Light and weather ────────────────────────────────────────────────────────

        /// <summary>The hour's tint, fog, the Abismo's darkness and falling rain, snow or leaves.</summary>
        void DrawEnvironment()
        {
            World w = game.World;
            Rect full = FullScreen;
            // The hour now lives in the light map and the image grade (PostFx).
            DrawWorldLighting(w);
            DrawSkyMoments(w.Hour);
            if (w.FogRange > 0f) DrawTex(full, vignette, new Color(0.75f, 0.78f, 0.82f, 0.5f));
            if (w.VisionRadius > 0f) DrawDarkness(w);
            DrawWeather(w.Weather, w.Wind);
        }

        void DrawDarkness(World w)
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            if (holeTex == null) holeTex = MakeHole();
            Vector2 center = WorldToGui(cam, w.Host.Position);
            float r = Vector2.Distance(center, WorldToGui(cam, w.Host.Position + Vector2.right * w.VisionRadius));
            var hole = new Rect(center.x - r, center.y - r, r * 2f, r * 2f);
            Rect full = FullScreen;
            Color dark = new Color(0f, 0f, 0.02f, 0.94f);
            DrawTex(hole, holeTex, dark);
            DrawRect(new Rect(full.x, full.y, full.width, hole.y - full.y), dark);
            DrawRect(new Rect(full.x, hole.yMax, full.width, full.yMax - hole.yMax), dark);
            DrawRect(new Rect(full.x, hole.y, hole.x - full.x, hole.height), dark);
            DrawRect(new Rect(hole.xMax, hole.y, full.xMax - hole.xMax, hole.height), dark);
        }

        static Texture2D MakeHole()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DarkHole" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        void DrawWeather(Weather weather, Vector2 wind)
        {
            Rect full = FullScreen;
            float time = Time.unscaledTime;
            switch (weather)
            {
                case Weather.Rain:
                case Weather.Storm:
                {
                    int count = weather == Weather.Storm ? 140 : 90;
                    Color c = new Color(0.75f, 0.85f, 1f, 0.35f);
                    for (int i = 0; i < count; i++)
                    {
                        float h1 = Hash01(i * 7 + 1), h2 = Hash01(i * 13 + 5);
                        float x = full.x + Mathf.Repeat(h1 * full.width + time * 180f, full.width);
                        float y = full.y + Mathf.Repeat(h2 * full.height + time * 1500f, full.height);
                        DrawRect(new Rect(x, y, 2f, 26f), c);
                    }
                    if (weather == Weather.Storm && Mathf.Repeat(time, 7f) < 0.08f) DrawRect(full, new Color(1f, 1f, 1f, 0.25f));
                    break;
                }
                case Weather.Snow:
                {
                    Color c = new Color(1f, 1f, 1f, 0.7f);
                    for (int i = 0; i < 110; i++)
                    {
                        float h1 = Hash01(i * 11 + 3), h2 = Hash01(i * 17 + 9);
                        float x = full.x + Mathf.Repeat(h1 * full.width + Mathf.Sin(time + i) * 30f, full.width);
                        float y = full.y + Mathf.Repeat(h2 * full.height + time * 90f * (0.6f + h1), full.height);
                        float s = 3f + 4f * h2;
                        kit.Dab(new Rect(x, y, s, s), c);
                    }
                    break;
                }
                case Weather.Wind:
                {
                    Color c = ColorUtil.Hex("#C9A66B", 0.75f);
                    var dir = new Vector2(wind.x, -wind.y);
                    for (int i = 0; i < 40; i++)
                    {
                        float h1 = Hash01(i * 5 + 2), h2 = Hash01(i * 19 + 4);
                        float x = full.x + Mathf.Repeat(h1 * full.width + dir.x * time * 420f, full.width);
                        float y = full.y + Mathf.Repeat(h2 * full.height + dir.y * time * 420f + Mathf.Sin(time * 3f + i) * 12f, full.height);
                        kit.Petal(new Rect(x, y, 14f, 9f), time * 200f + i * 40f, 0.8f);
                    }
                    break;
                }
                case Weather.FullMoon:
                    DrawRect(full, new Color(0.6f, 0.05f, 0.08f, 0.06f));
                    break;
            }
        }

        static float Hash01(int i)
        {
            uint x = (uint)i * 2654435761u;
            x ^= x >> 13;
            x *= 0x5bd1e995;
            x ^= x >> 15;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
}
