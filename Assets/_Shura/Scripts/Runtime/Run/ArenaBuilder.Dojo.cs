using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The dojo of the tutorial and the wild ground around the arenas: a wooden hall seen from
    /// above (plank floor, a lacquered rim, posts, red paper lanterns on the beams, sandbags, a
    /// sword stand, benches and barrels, the DOJO board over the back wall) standing in a forest
    /// at night (dark grass, trees and bushes from the Ninja Adventure nature set, fireflies).
    /// </summary>
    public static partial class ArenaBuilder
    {
        const string NatureSheet = "tiles_nature";
        const string ElementSheet = "tiles_element";
        /// <summary>How far the wild ground reaches past the arena, in units.</summary>
        const int WildMargin = 18;

        /// <summary>Trees and bushes of the nature sheet: tile x, y, width, height.</summary>
        static readonly RectInt[] Trees =
        {
            new RectInt(0, 0, 2, 2), new RectInt(2, 0, 2, 2), new RectInt(16, 0, 2, 2), new RectInt(18, 0, 2, 2),
            new RectInt(3, 18, 3, 3), new RectInt(0, 18, 3, 3), new RectInt(9, 18, 3, 3), new RectInt(4, 0, 2, 2),
        };
        static readonly RectInt[] Bushes =
        {
            new RectInt(0, 10, 1, 1), new RectInt(1, 10, 1, 1), new RectInt(4, 10, 1, 1), new RectInt(6, 10, 1, 1),
            new RectInt(2, 10, 1, 1), new RectInt(8, 10, 1, 1),
        };

        public static ArenaDecor BuildDojo(Transform parent, ArenaLayout layout, bool encodePng)
        {
            var decor = new ArenaDecor { FlameColor = new Color(1f, 0.55f, 0.3f), Moss = false };
            Sprite arena = BuildDojoSprite(layout, decor, encodePng);
            var floor = SpriteUtil.Make("Dojo", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;
            decor.FloorTexture = arena.texture;
            AddWilds(parent, layout, 7, 1f);
            AddDojoProps(parent, layout, decor);
            return decor;
        }

        // ── The hall ────────────────────────────────────────────────────────────────────

        static bool NearFloor(ArenaLayout layout, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (layout.IsFloor(x + dx, y + dy)) return true;
                }
            }
            return false;
        }

        static Color32 Wood(float r, float g, float b, float k) =>
            new Color32((byte)Mathf.Clamp(r * k, 0f, 255f), (byte)Mathf.Clamp(g * k, 0f, 255f), (byte)Mathf.Clamp(b * k, 0f, 255f), 255);

        static Sprite BuildDojoSprite(ArenaLayout layout, ArenaDecor decor, bool encodePng)
        {
            int cw = layout.W * Tile, ch = layout.H * Tile;
            var canvas = new Color32[cw * ch];
            for (int py = 0; py < ch; py++)
            {
                int cy = py / Tile;
                for (int px = 0; px < cw; px++)
                {
                    int cx = px / Tile;
                    if (layout.IsFloor(cx, cy))
                    {
                        // Boards five pixels wide, of uneven length, staggered row by row.
                        int board = py / 5;
                        int len = 38 + (int)(Hash(board, 7) * 30f);
                        int offset = (int)(Hash(board, 3) * len);
                        int along = px + offset;
                        int piece = along / len;
                        int inBoard = along % len;
                        float tone = 0.84f + 0.24f * Hash(board * 31 + piece, 11);
                        float grain = 0.05f * Mathf.Sin(px * 0.33f + Hash(board, piece) * 9f) + 0.04f * (Hash(px, py) - 0.5f);
                        float k = tone * (1f + grain);
                        Color32 c = Wood(150f, 94f, 52f, k);
                        if (py % 5 == 0) c = Wood(70f, 42f, 24f, 1f);                       // seam between boards
                        else if (inBoard == 0) c = Wood(84f, 50f, 28f, 1f);                 // butt joint
                        else if (py % 5 == 4) c = Wood(176f, 116f, 66f, tone);              // the lit edge of a board
                        if ((inBoard == 3 || inBoard == len - 3) && py % 5 == 2) c = Wood(56f, 40f, 32f, 1f); // nails
                        canvas[py * cw + px] = c;
                    }
                    else if (NearFloor(layout, cx, cy))
                    {
                        // The lacquered rim of the hall, with a post every few cells.
                        float grain = 0.06f * Mathf.Sin(py * 0.5f + px * 0.07f) + 0.05f * (Hash(px >> 1, py) - 0.5f);
                        Color32 c = Wood(70f, 38f, 24f, 1f + grain);
                        int lx = px % Tile, ly = py % Tile;
                        if (lx == 0 || ly == 0) c = Wood(36f, 20f, 14f, 1f);
                        if (cx % 3 == 0 && lx >= 5 && lx <= 10 && ly >= 5 && ly <= 10) c = Wood(112f, 66f, 38f, 1f + grain);
                        canvas[py * cw + px] = c;
                    }
                    else canvas[py * cw + px] = new Color32(0, 0, 0, 0);
                }
            }
            Vignette(canvas, cw, layout, new RectInt(0, 0, cw, ch));

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Dojo" };
            tex.SetPixels32(canvas);
            tex.Apply(false, false);
            if (encodePng) decor.Png = tex.EncodeToPNG();
            decor.PixelSize = new Vector2Int(cw, ch);
            decor.PixelPivot = -layout.Origin * Tile;
            var pivot = new Vector2(-layout.Origin.x / layout.W, -layout.Origin.y / layout.H);
            return Sprite.Create(tex, new Rect(0, 0, cw, ch), pivot, Tile);
        }

        static SpriteRenderer Prop(Transform parent, string name, Sprite s, Vector2 pos, float scale = 1f, int orderBias = 0)
        {
            if (s == null) return null;
            var r = SpriteUtil.Make(name, parent, s, Color.white, -Mathf.RoundToInt(pos.y * 20f) + orderBias);
            r.transform.localPosition = pos;
            r.transform.localScale = new Vector3(scale, scale, 1f);
            return r;
        }

        static void Solid(ArenaLayout layout, Sprite s, Vector2 pos, int rows)
        {
            if (s == null || !s.texture.isReadable) return;
            Rect foot = Footprint(s, pos, rows);
            foot.yMin -= 0.05f;
            layout.AddSolid(foot);
        }

        static void AddDojoProps(Transform parent, ArenaLayout layout, ArenaDecor decor)
        {
            Rect fb = layout.FloorBounds;
            Sprite pillar = PixelArt.Standing("prop_pillar");

            // Posts along the back and the front of the hall; red lanterns hang between the back ones.
            int posts = Mathf.Max(3, Mathf.RoundToInt(fb.width / 5.5f) + 1);
            var backPosts = new List<float>();
            for (int i = 0; i < posts; i++)
            {
                float x = Mathf.Lerp(fb.xMin + 0.5f, fb.xMax - 0.5f, i / (float)(posts - 1));
                backPosts.Add(x);
                var back = new Vector2(x, fb.yMax - 0.35f);
                Prop(parent, "Post", pillar, back, 1.2f);
                Solid(layout, pillar, back, 3);
                var front = new Vector2(x, fb.yMin + 0.05f);
                Prop(parent, "Post", pillar, front, 1.2f);
                Solid(layout, pillar, front, 3);
            }
            Sprite chochin = PixelArt.Image("prop_chochin");
            for (int i = 0; i + 1 < backPosts.Count; i++)
            {
                float x = (backPosts[i] + backPosts[i + 1]) * 0.5f;
                AddHangingLantern(parent, decor, chochin, new Vector2(x, fb.yMax - 0.9f));
                AddHangingLantern(parent, decor, chochin, new Vector2(x, fb.yMin + 0.6f));
            }

            // The DOJO board over the back wall, the sword stand and a notice board.
            Prop(parent, "DojoSign", PixelArt.Region(HouseSheet, 4, 4, 2, 1, new Vector2(0.5f, 0f)), new Vector2(fb.center.x, fb.yMax + 0.15f), 1.6f, 4000);
            Prop(parent, "SwordStand", PixelArt.Region(HouseSheet, 6, 4, 2, 1, new Vector2(0.5f, 0f)), new Vector2(fb.xMin + 3.5f, fb.yMax + 0.1f), 1.2f, 4000);
            Prop(parent, "SwordStand", PixelArt.Region(HouseSheet, 6, 4, 2, 1, new Vector2(0.5f, 0f)), new Vector2(fb.xMax - 3.5f, fb.yMax + 0.1f), 1.2f, 4000);

            // Sandbags on the sides, benches by the front, barrels and a water tub in the corners.
            Sprite bag = PixelArt.Region(ElementSheet, 0, 5, 1, 2, new Vector2(0.5f, 0f));
            foreach (Vector2 p in new[] { new Vector2(fb.xMin + 1.3f, fb.center.y + 1.6f), new Vector2(fb.xMax - 1.3f, fb.center.y + 1.6f) })
            {
                Prop(parent, "Sandbag", bag, p);
                Solid(layout, bag, p, 3);
            }
            Sprite bench = PixelArt.Region(ElementSheet, 11, 1, 3, 1, new Vector2(0.5f, 0f));
            foreach (Vector2 p in new[] { new Vector2(fb.xMin + 4.2f, fb.yMin + 0.5f), new Vector2(fb.xMax - 4.2f, fb.yMin + 0.5f) })
            {
                Prop(parent, "Bench", bench, p);
                Solid(layout, bench, p, 6);
            }
            Sprite barrel = PixelArt.Region(HouseSheet, 16, 15, 1, 1, new Vector2(0.5f, 0f));
            Sprite tub = PixelArt.Region(HouseSheet, 16, 13, 1, 1, new Vector2(0.5f, 0f));
            foreach (Vector2 p in new[] { new Vector2(fb.xMin + 0.7f, fb.yMin + 0.6f), new Vector2(fb.xMax - 0.7f, fb.yMin + 0.6f), new Vector2(fb.xMax - 0.7f, fb.yMax - 1.2f) })
            {
                Prop(parent, "Barrel", barrel, p);
                Solid(layout, barrel, p, 6);
            }
            var tubAt = new Vector2(fb.xMin + 0.7f, fb.yMax - 1.2f);
            Prop(parent, "Tub", tub, tubAt);
            Solid(layout, tub, tubAt, 6);
        }

        /// <summary>A red paper lantern hanging from the beam: it lights the hall (and can be cut down).</summary>
        static void AddHangingLantern(Transform parent, ArenaDecor decor, Sprite art, Vector2 feet)
        {
            var root = new GameObject("Chochin").transform;
            root.SetParent(parent, false);
            root.localPosition = feet;
            int order = -Mathf.RoundToInt(feet.y * 20f) + 3000;
            Color flame = new Color(1f, 0.5f, 0.28f);
            var pool = SpriteUtil.Make("LightPool", root, ShapeLibrary.Get(Shape.SoftCircle), flame.WithAlpha(0.1f), Sorting.Floor + 1);
            pool.transform.localScale = new Vector3(6f, 5f, 1f);
            var glow = SpriteUtil.Make("Glow", root, ShapeLibrary.Get(Shape.SoftCircle), flame.WithAlpha(0.2f), Sorting.Shadow + 2);
            glow.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            var body = SpriteUtil.Make("Paper", root, art != null ? art : ShapeLibrary.Get(Shape.Capsule), Color.white, order);
            body.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            body.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            decor.Lanterns.Add(feet);
            decor.LanternViews.Add(root.gameObject);
            decor.AddBrazier(feet + new Vector2(0f, 1.3f), body, null, glow);
        }

        // ── The wild ground ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Grass all around the arena, and trees and bushes beyond its walls (never inside):
        /// the room is a clearing. <paramref name="density"/> 0..1.5 thins or thickens the wood.
        /// </summary>
        public static void AddWilds(Transform parent, ArenaLayout layout, int seed, float density)
        {
            int gw = (layout.W + 2 * WildMargin) * Tile, gh = (layout.H + 2 * WildMargin) * Tile;
            var px = new Color32[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    float n = Hash((x >> 1) + seed, y >> 1);
                    float patch = Mathf.PerlinNoise(x * 0.012f + seed, y * 0.012f);
                    float k = 0.78f + 0.3f * patch + 0.1f * (n - 0.5f);
                    var c = new Color(0.2f * k, 0.36f * k, 0.18f * k);
                    if (n > 0.965f) c = new Color(0.36f, 0.54f, 0.26f);            // a blade catching light
                    else if (n < 0.02f) c = new Color(0.62f, 0.56f, 0.32f);         // a dry stalk
                    px[y * gw + x] = c;
                }
            }
            var tex = new Texture2D(gw, gh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Wilds" };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Vector2 origin = layout.Origin - new Vector2(WildMargin, WildMargin);
            var ground = SpriteUtil.Make("Wilds", parent, Sprite.Create(tex, new Rect(0, 0, gw, gh), Vector2.zero, Tile), Color.white, Sorting.Floor - 10);
            ground.transform.localPosition = origin;

            // Trees and bushes outside the walls, nearer ones smaller.
            Rect fb = layout.FloorBounds;
            Rect clear = new Rect(fb.xMin - 1.6f, fb.yMin - 1.4f, fb.width + 3.2f, fb.height + 3.4f);
            float step = 2.1f / Mathf.Max(0.3f, density);
            for (float y = origin.y + 1f; y < origin.y + layout.H + 2 * WildMargin - 1f; y += step)
            {
                for (float x = origin.x + 1f; x < origin.x + layout.W + 2 * WildMargin - 1f; x += step)
                {
                    int ix = Mathf.RoundToInt(x * 10f), iy = Mathf.RoundToInt(y * 10f);
                    float h = Hash(ix + seed * 17, iy);
                    var p = new Vector2(x + (Hash(ix, iy + 5) - 0.5f) * step * 0.9f, y + (Hash(ix + 9, iy) - 0.5f) * step * 0.9f);
                    if (clear.Contains(p)) continue;
                    float d = Mathf.Max(Mathf.Max(clear.xMin - p.x, p.x - clear.xMax), Mathf.Max(clear.yMin - p.y, p.y - clear.yMax));
                    if (d < 2.4f)
                    {
                        if (h > 0.55f) continue;
                        RectInt b = Bushes[(int)(Hash(ix, iy + 3) * Bushes.Length) % Bushes.Length];
                        Prop(parent, "Bush", PixelArt.Region(NatureSheet, b.x, b.y, b.width, b.height, new Vector2(0.5f, 0.1f)), p);
                        continue;
                    }
                    if (h > 0.62f * Mathf.Min(1.4f, density)) continue;
                    RectInt t = Trees[(int)(Hash(ix + 3, iy) * Trees.Length) % Trees.Length];
                    float scale = 1f + 0.25f * Hash(ix, iy + 11);
                    Prop(parent, "Tree", PixelArt.Region(NatureSheet, t.x, t.y, t.width, t.height, new Vector2(0.5f, 0.05f)), p, scale);
                }
            }

            parent.gameObject.AddComponent<Fireflies>().Init(new Rect(origin.x + 4f, origin.y + 4f, layout.W + 2 * WildMargin - 8f, layout.H + 2 * WildMargin - 8f), seed);
        }
    }
}
