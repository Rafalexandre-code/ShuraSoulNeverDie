using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Animated arena props (braziers). Purely visual.</summary>
    public sealed class ArenaDecor
    {
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
        readonly List<Sprite[]> flameFrames = new List<Sprite[]>();
        readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        readonly List<float> seeds = new List<float>();

        public readonly List<Vector2> FlamePositions = new List<Vector2>();
        /// <summary>Generated per room; destroyed with the arena to avoid leaking textures.</summary>
        public Texture2D FloorTexture;
        public Color FlameColor = Color.white;

        public void AddBrazier(Vector2 flamePos, SpriteRenderer flame, Sprite[] frames, SpriteRenderer glow)
        {
            FlamePositions.Add(flamePos);
            flames.Add(flame);
            flameFrames.Add(frames);
            glows.Add(glow);
            seeds.Add(flames.Count * 13.7f);
        }

        public void Animate(float time)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                float n = Mathf.PerlinNoise(seeds[i], time * 3.2f);
                Sprite[] frames = flameFrames[i];
                if (frames != null && frames.Length > 0)
                {
                    int f = Mathf.FloorToInt(time * 9f + seeds[i]) % frames.Length;
                    flames[i].sprite = frames[f];
                }
                float g = 3.6f + 0.5f * n;
                glows[i].transform.localScale = new Vector3(g, g, 1f);
                Color c = glows[i].color;
                c.a = 0.13f + 0.07f * n;
                glows[i].color = c;
            }
        }
    }

    /// <summary>
    /// Builds each arena as one pixel-art image from 0x72's DungeonTileset II: stone floor
    /// tiles (mostly clean, some cracked, the odd skull), a brick back wall with banners, and
    /// wall caps around the other sides, all tinted toward the room's biome. 16 px per unit,
    /// point filtered, the same density as the characters.
    /// </summary>
    public static class ArenaBuilder
    {
        const int Tile = PixelArt.PixelsPerUnit;
        const int SideMargin = Tile;
        const int BottomMargin = Tile;
        const int TopMargin = Tile * 2;

        static readonly string[] Banners = { "wall_banner_red", "wall_banner_blue", "wall_banner_green", "wall_banner_yellow" };

        public static ArenaDecor Build(Transform parent, GameConfig config, float width, float height, Biome biome)
        {
            Sprite arena = BuildArenaSprite(width, height, biome);
            var floor = SpriteUtil.Make("Arena", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            var decor = new ArenaDecor { FloorTexture = arena.texture, FlameColor = biome.flame };
            float bx = hw - 1.2f;
            float by = hh - 1.0f;
            foreach (Vector2 corner in new[] { new Vector2(-bx, by), new Vector2(bx, by), new Vector2(-bx, -by), new Vector2(bx, -by) })
            {
                AddBrazier(parent, decor, corner, biome);
            }
            return decor;
        }

        static void AddBrazier(Transform parent, ArenaDecor decor, Vector2 pos, Biome biome)
        {
            var root = new GameObject("Brazier").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            int order = -Mathf.RoundToInt(pos.y * 20f);

            var pool = SpriteUtil.Make("LightPool", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.07f), Sorting.Floor + 1);
            pool.transform.localScale = new Vector3(7f, 6f, 1f);
            var glow = SpriteUtil.Make("Glow", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.16f), Sorting.Shadow + 2);
            glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var shadow = SpriteUtil.Make("Shadow", root, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.5f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            shadow.transform.localScale = new Vector3(1.1f, 0.45f, 1f);

            SpriteUtil.Make("Bowl", root, PixelArt.Brazier(), Color.white, order);
            Sprite[] frames = PixelArt.Flames(biome.flame);
            var flame = SpriteUtil.Make("Flame", root, frames[0], Color.white, order + 1);
            flame.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            decor.AddBrazier(pos + new Vector2(0f, 0.8f), flame, frames, glow);
        }

        // ── Arena image ─────────────────────────────────────────────────────────────────

        static Sprite BuildArenaSprite(float width, float height, Biome biome)
        {
            int fw = Mathf.Max(Tile, Mathf.RoundToInt(width * Tile));
            int fh = Mathf.Max(Tile, Mathf.RoundToInt(height * Tile));
            int cw = fw + SideMargin * 2;
            int ch = fh + BottomMargin + TopMargin;
            var canvas = new Color32[cw * ch];
            var voidColor = (Color32)Color.Lerp(biome.mortar, Color.black, 0.75f);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = voidColor;

            Texture2D floorRef = PixelArt.Texture("floor_1");
            Texture2D wallRef = PixelArt.Texture("wall_mid");
            if (floorRef == null || wallRef == null || !floorRef.isReadable) return FallbackFloor(width, height, biome);

            Color floorTint = TintToward(floorRef, Color.Lerp(biome.floorA, biome.floorB, 0.5f), 0.55f);
            Color wallTint = TintToward(wallRef, biome.wall, 0.45f);
            var floorRect = new RectInt(SideMargin, BottomMargin, fw, fh);

            // Floor: mostly clean slabs, some cracked or broken ones.
            for (int ty = 0; ty * Tile < fh; ty++)
            {
                for (int tx = 0; tx * Tile < fw; tx++)
                {
                    Blit(canvas, cw, ch, PickFloor(), SideMargin + tx * Tile, BottomMargin + ty * Tile, floorTint, false, floorRect);
                }
            }
            // A few skulls of those who came before.
            int skulls = Random.Range(0, 3);
            for (int i = 0; i < skulls; i++)
            {
                int x = SideMargin + Random.Range(Tile, Mathf.Max(Tile + 1, fw - Tile * 2));
                int y = BottomMargin + Random.Range(Tile, Mathf.Max(Tile + 1, fh - Tile * 2));
                Blit(canvas, cw, ch, "skull", x, y, floorTint, false, floorRect);
            }
            Vignette(canvas, cw, floorRect);

            // Back wall: brick face with banners, capped by the wall top.
            int faceY = BottomMargin + fh;
            int capY = faceY + Tile;
            var full = new RectInt(0, 0, cw, ch);
            for (int x = 0; x < cw; x += Tile)
            {
                string face = "wall_mid";
                float r = Random.value;
                bool inner = x >= SideMargin && x + Tile <= SideMargin + fw;
                if (inner && r < 0.14f) face = Banners[Random.Range(0, Banners.Length)];
                else if (inner && r < 0.2f) face = Random.value < 0.5f ? "wall_hole_1" : "wall_hole_2";
                Blit(canvas, cw, ch, face, x, faceY, wallTint, false, full);
                Blit(canvas, cw, ch, "wall_top_mid", x, capY, wallTint, false, full);
            }
            // Side walls and the front wall: only their caps are visible from above.
            for (int y = BottomMargin; y < faceY + Tile; y += Tile)
            {
                // The edge line hugs the floor: right side of the left column and vice versa.
                Blit(canvas, cw, ch, "wall_edge_mid_right", 0, y, wallTint, false, full);
                Blit(canvas, cw, ch, "wall_edge_mid_left", SideMargin + fw, y, wallTint, false, full);
            }
            for (int x = SideMargin; x < SideMargin + fw; x += Tile)
            {
                Blit(canvas, cw, ch, "wall_top_mid", x, 0, wallTint, true, full);
            }
            Blit(canvas, cw, ch, "wall_edge_bottom_right", 0, 0, wallTint, true, full);
            Blit(canvas, cw, ch, "wall_edge_bottom_left", SideMargin + fw, 0, wallTint, true, full);

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Arena",
            };
            tex.SetPixels32(canvas);
            tex.Apply(false, true);
            var pivot = new Vector2((SideMargin + fw * 0.5f) / cw, (BottomMargin + fh * 0.5f) / ch);
            return Sprite.Create(tex, new Rect(0, 0, cw, ch), pivot, Tile);
        }

        static string PickFloor()
        {
            // Mostly clean slabs so fighters and blood read clearly; damage is the exception.
            float r = Random.value;
            if (r < 0.84f) return "floor_1";
            if (r < 0.9f) return "floor_2";
            if (r < 0.95f) return "floor_3";
            return "floor_" + Random.Range(4, 9);
        }

        /// <summary>Per-channel multiplier that moves a tile's average colour toward the biome's.</summary>
        static Color TintToward(Texture2D reference, Color target, float amount)
        {
            Color32[] px = reference.GetPixels32();
            float r = 0f, g = 0f, b = 0f;
            int n = 0;
            foreach (Color32 p in px)
            {
                if (p.a < 128) continue;
                r += p.r;
                g += p.g;
                b += p.b;
                n++;
            }
            if (n == 0) return Color.white;
            var avg = new Color(r / n / 255f, g / n / 255f, b / n / 255f);
            var mult = new Color(
                Mathf.Clamp(target.r / Mathf.Max(0.02f, avg.r), 0.5f, 1.8f),
                Mathf.Clamp(target.g / Mathf.Max(0.02f, avg.g), 0.5f, 1.8f),
                Mathf.Clamp(target.b / Mathf.Max(0.02f, avg.b), 0.5f, 1.8f));
            return Color.Lerp(Color.white, mult, amount);
        }

        /// <summary>Copies a tile into the canvas (alpha-tested, tinted, clipped).</summary>
        static void Blit(Color32[] canvas, int cw, int ch, string name, int dx, int dy, Color tint, bool flipY, RectInt clip)
        {
            Texture2D tex = PixelArt.Texture(name);
            if (tex == null || !tex.isReadable) return;
            Color32[] src = tex.GetPixels32();
            int w = tex.width;
            int h = tex.height;
            for (int y = 0; y < h; y++)
            {
                int ty = dy + y;
                if (ty < clip.yMin || ty >= clip.yMax || ty < 0 || ty >= ch) continue;
                int sy = flipY ? h - 1 - y : y;
                for (int x = 0; x < w; x++)
                {
                    int tx = dx + x;
                    if (tx < clip.xMin || tx >= clip.xMax || tx < 0 || tx >= cw) continue;
                    Color32 s = src[sy * w + x];
                    if (s.a < 128) continue;
                    canvas[ty * cw + tx] = new Color32(
                        (byte)Mathf.Clamp(s.r * tint.r, 0f, 255f),
                        (byte)Mathf.Clamp(s.g * tint.g, 0f, 255f),
                        (byte)Mathf.Clamp(s.b * tint.b, 0f, 255f), 255);
                }
            }
        }

        /// <summary>Darkens the floor toward the walls so the centre reads as the dueling ground.</summary>
        static void Vignette(Color32[] canvas, int cw, RectInt floor)
        {
            for (int y = floor.yMin; y < floor.yMax; y++)
            {
                float ey = Mathf.Abs((y - floor.yMin) / (float)floor.height - 0.5f) * 2f;
                for (int x = floor.xMin; x < floor.xMax; x++)
                {
                    float ex = Mathf.Abs((x - floor.xMin) / (float)floor.width - 0.5f) * 2f;
                    float k = 1f - 0.3f * Mathf.Pow(Mathf.Max(ex, ey), 3f);
                    Color32 c = canvas[y * cw + x];
                    canvas[y * cw + x] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
                }
            }
        }

        /// <summary>Plain stone if the tileset is missing (never expected in a build).</summary>
        static Sprite FallbackFloor(float width, float height, Biome biome)
        {
            int w = Mathf.RoundToInt(width * Tile);
            int h = Mathf.RoundToInt(height * Tile);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "ArenaFallback" };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = biome.floorA;
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Tile);
        }
    }
}
