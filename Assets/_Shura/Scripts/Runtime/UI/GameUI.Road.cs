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
            if (input.NavX != 0)
            {
                menuIndex = (menuIndex + input.NavX + count) % count;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) game.VoteRoute(-1, menuIndex);
        }

        void DrawRoute()
        {
            DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.55f));
            var scroll = new Rect(110f, 60f, VW - 220f, VH - 130f);
            kit.PaperPanel(scroll, 0.97f);
            Heading(new Rect(0, 80, VW, 90), Strings.RouteTitle, UiKit.Ink, UiKit.Shu, 520f);
            RunState run = game.Run;
            string sub = string.Format(Strings.ToastDepth, run.RoomIndex + 2) + "  ·  Kalpa " + KalpaRules.Numeral(run.Kalpa) +
                         (run.KalpaRules.Count > 0 ? "  ·  " + KalpaNames(run) : "");
            Label(new Rect(0, 170, VW, 34), sub, smallStyleCenter, UiKit.InkSoft);

            IReadOnlyList<RouteNode> options = game.RouteOptions;
            int n = options.Count;
            const float gap = 40f;
            float cardW = Mathf.Min(420f, (scroll.width - 160f - gap * (n - 1)) / Mathf.Max(1, n));
            float cardH = 330f;
            float startX = (VW - (n * cardW + (n - 1) * gap)) * 0.5f;
            float top = 230f;
            Event e = Event.current;
            bool online = game.Net.Active && game.Net.JoinedCount > 0;
            for (int i = 0; i < n; i++)
            {
                RouteNode node = options[i];
                var rect = new Rect(startX + i * (cardW + gap), top, cardW, cardH);
                bool selected = i == menuIndex;
                if (!game.Launch.Automated && rect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.Repaint && (e.mousePosition - lastMouse).sqrMagnitude > 1f) menuIndex = i;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        game.VoteRoute(-1, i);
                        return;
                    }
                }
                Color kc = RouteMap.KindColor(node.kind);
                kit.InkPanel(rect, selected ? 0.98f : 0.86f);
                kit.Enso(new Rect(rect.center.x - 70f, rect.y + 22f, 140f, 140f), kc.WithAlpha(selected ? 1f : 0.7f), i * 40f + (selected ? Time.unscaledTime * 25f : 0f));
                Label(new Rect(rect.x, rect.y + 52f, rect.width, 84f), RouteMap.KindGlyph(node.kind), titleStyle, kc);
                FitLabel(new Rect(rect.x + 20f, rect.y + 172f, rect.width - 40f, 44f), RouteMap.Describe(node), cardKindStyle, UiKit.PaperText);
                Label(new Rect(rect.x + 26f, rect.y + 220f, rect.width - 52f, 90f), RouteMap.KindText(node.kind), smallStyleCenter, UiKit.PaperDim);
                if (online)
                {
                    int votes = game.RouteVotesFor(i);
                    Label(new Rect(rect.x, rect.yMax - 40f, rect.width, 32f), votes > 0 ? new string('●', votes) : "", smallStyleCenter, Pal.gold);
                }
                if (game.HostRouteVote == i) kit.Seal(new Rect(rect.xMax - 64f, rect.y - 22f, 78f, 78f), 0.95f);
            }

            // What lies further ahead: two more layers, small.
            int depth = n > 0 ? options[0].depth : run.RoomIndex + 1;
            float ay = top + cardH + 40f;
            for (int d = 1; d < RouteMap.Lookahead; d++)
            {
                List<RouteNode> layer = RouteMap.Layer(run.Seed, depth + d);
                float w = 230f;
                float sx = (VW - (layer.Count * w + (layer.Count - 1) * 20f)) * 0.5f;
                FitLabel(new Rect(sx - 300f, ay + 8f, 280f, 30f), string.Format(Strings.ToastDepth, depth + d + 1), smallRightStyle, UiKit.InkSoft);
                for (int i = 0; i < layer.Count; i++)
                {
                    var r = new Rect(sx + i * (w + 20f), ay, w, 46f);
                    kit.Stroke(r, RouteMap.KindColor(layer[i].kind).WithAlpha(0.55f));
                    FitLabel(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 34f), RouteMap.KindName(layer[i].kind), smallStyleCenter, UiKit.Ink);
                }
                ay += 62f;
            }
            string foot = online ? string.Format(Strings.RouteVotes, Mathf.CeilToInt(game.RouteTimeLeft)) : Strings.RouteSolo;
            Label(new Rect(0, VH - 120f, VW, 36f), foot, bodyCenterStyle, UiKit.InkSoft);
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
            parts.Add(Biomes.TimeName(w.TimeOfDay));
            if (w.Weather != Weather.Clear) parts.Add(Biomes.WeatherName(w.Weather));
            if (game.Run.Kalpa > 1) parts.Add("Kalpa " + KalpaRules.Numeral(game.Run.Kalpa));
            Label(new Rect(0, 132, VW, 30), string.Join("  ·  ", parts), smallStyleCenter, UiKit.PaperDim);
            Label(new Rect(VW * 0.5f - 500f, 158, 1000f, 30), Biomes.RuleText(w.BiomeRule), smallStyleCenter, UiKit.PaperDim.WithAlpha(0.75f));

            if (room.challenge != ChallengeKind.None)
            {
                string text = w.ChallengeFailed ? Strings.HudChallengeFailed : string.Format(Strings.HudChallenge, RouteMap.ChallengeName(room.challenge));
                if (room.challenge == ChallengeKind.Timed && !w.ChallengeFailed) text += "  ·  " + Mathf.CeilToInt(w.ChallengeTimeLeft) + " s";
                Label(new Rect(0, 190, VW, 40), text, cardKindStyle, w.ChallengeFailed ? Pal.danger : Pal.gold);
            }
            if (!string.IsNullOrEmpty(w.EncounterTitle))
            {
                var banner = new Rect(VW * 0.5f - 560f, VH - 350f, 1120f, 110f);
                kit.InkPanel(banner, 0.9f);
                Label(new Rect(banner.x, banner.y + 10f, banner.width, 44f), w.EncounterTitle, cardKindStyle, Pal.gold);
                Label(new Rect(banner.x + 40f, banner.y + 52f, banner.width - 80f, 50f), w.EncounterText ?? "", smallStyleCenter, UiKit.PaperText);
            }
        }

        /// <summary>Purse and curse, under the Ki block.</summary>
        void DrawPurse(PlayerController p, float x, float y)
        {
            PlayerBuild b = p.Build;
            if (b == null) return;
            string s = string.Format(Strings.HudMon, b.Mon);
            if (b.CurseRooms > 0) s += "   ·   " + RunCurses.Name(b.Curse) + " (" + b.CurseRooms + ")";
            if (b.Muramasa) s += "   ·   Muramasa";
            Label(new Rect(x, y, 460f, 26f), s, smallStyle, b.CurseRooms > 0 ? ColorUtil.Hex("#B06CFF") : Pal.gold);
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
            switch (w.TimeOfDay)
            {
                case TimeOfDay.Afternoon: DrawRect(full, new Color(1f, 0.75f, 0.4f, 0.05f)); break;
                case TimeOfDay.Dusk: DrawRect(full, new Color(0.95f, 0.45f, 0.25f, 0.12f)); break;
                case TimeOfDay.Night: DrawRect(full, new Color(0.05f, 0.08f, 0.25f, 0.3f)); break;
                case TimeOfDay.Dawn: DrawRect(full, new Color(1f, 0.6f, 0.75f, 0.08f)); break;
            }
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
