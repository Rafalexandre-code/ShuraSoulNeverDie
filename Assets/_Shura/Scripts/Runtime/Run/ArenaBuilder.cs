using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Animated arena props (lantern light). Purely visual.</summary>
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

        /// <summary>A light source; <paramref name="frames"/> may be null for static art.</summary>
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
    /// Builds each arena as one pixel-art image from the Ninja Adventure tilesets: a temple
    /// floor that changes with the biome (ash cobblestone, moss and ice temple tiles, blood
    /// brick, abyss stone), framed by the pack's walls, with nobori banners along the back
    /// wall; paper lanterns light the corners. 16 px per unit, point filtered.
    /// </summary>
    public static class ArenaBuilder
    {
        const int Tile = PixelArt.PixelsPerUnit;
        const int SideMargin = Tile;
        const int BottomMargin = Tile;
        const int TopMargin = Tile * 2;
        const string FloorSheet = "tiles_floor";
        const string WallSheet = "tiles_wall";
        const string HouseSheet = "tiles_house";

        /// <summary>Which tiles paint a biome (tile coordinates from the sheets' top-left).</summary>
        sealed class Style
        {
            public RectInt floor;
            public Vector2Int frame;
            public Color tint;
        }

        static Style StyleFor(Biome biome)
        {
            switch (biome.suffix)
            {
                case "do Musgo":
                    return new Style { floor = new RectInt(15, 6, 4, 4), frame = new Vector2Int(5, 6), tint = new Color(0.78f, 0.86f, 0.8f) };
                case "do Sangue":
                    return new Style { floor = new RectInt(4, 6, 4, 4), frame = new Vector2Int(5, 0), tint = new Color(0.78f, 0.45f, 0.42f) };
                case "do Abismo":
                    return new Style { floor = new RectInt(12, 13, 1, 1), frame = new Vector2Int(0, 6), tint = new Color(0.6f, 0.55f, 0.92f) };
                case "do Gelo":
                    return new Style { floor = new RectInt(15, 0, 4, 4), frame = new Vector2Int(0, 0), tint = new Color(0.66f, 0.8f, 0.98f) };
                default:
                    return new Style { floor = new RectInt(12, 13, 1, 1), frame = new Vector2Int(0, 6), tint = new Color(0.85f, 0.82f, 0.86f) };
            }
        }

        public static ArenaDecor Build(Transform parent, GameConfig config, float width, float height, Biome biome)
        {
            Sprite arena = BuildArenaSprite(width, height, biome);
            var floor = SpriteUtil.Make("Arena", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            var decor = new ArenaDecor { FloorTexture = arena.texture, FlameColor = biome.flame };
            float bx = hw - 1.0f;
            float by = hh - 0.9f;
            foreach (Vector2 corner in new[] { new Vector2(-bx, by), new Vector2(bx, by), new Vector2(-bx, -by), new Vector2(bx, -by) })
            {
                AddLantern(parent, decor, corner, biome);
            }
            return decor;
        }

        /// <summary>A tall paper lantern (andon) whose light flickers on the floor.</summary>
        static void AddLantern(Transform parent, ArenaDecor decor, Vector2 pos, Biome biome)
        {
            var root = new GameObject("Lantern").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            int order = -Mathf.RoundToInt(pos.y * 20f);

            var pool = SpriteUtil.Make("LightPool", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.08f), Sorting.Floor + 1);
            pool.transform.localScale = new Vector3(7f, 6f, 1f);
            var glow = SpriteUtil.Make("Glow", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.16f), Sorting.Shadow + 2);
            glow.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var shadow = SpriteUtil.Make("Shadow", root, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.5f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            shadow.transform.localScale = new Vector3(1f, 0.4f, 1f);

            Sprite lantern = PixelArt.Region("tiles_element", 0, 5, 1, 2, new Vector2(0.5f, 0f));
            var body = SpriteUtil.Make("Andon", root, lantern != null ? lantern : ShapeLibrary.Get(Shape.Capsule), Color.white, order);
            // The paper glows in the biome's light colour.
            var paper = SpriteUtil.Make("Paper", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.45f), order + 1);
            paper.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            paper.transform.localScale = new Vector3(0.7f, 0.9f, 1f);

            decor.AddBrazier(pos + new Vector2(0f, 1.2f), body, null, glow);
        }

        // ── Arena image ─────────────────────────────────────────────────────────────────

        static Sprite BuildArenaSprite(float width, float height, Biome biome)
        {
            int fw = Mathf.Max(Tile, Mathf.RoundToInt(width * Tile));
            int fh = Mathf.Max(Tile, Mathf.RoundToInt(height * Tile));
            int cw = fw + SideMargin * 2;
            int ch = fh + BottomMargin + TopMargin;

            Texture2D floorSheet = PixelArt.Texture(FloorSheet);
            Texture2D wallSheet = PixelArt.Texture(WallSheet);
            if (floorSheet == null || wallSheet == null || !floorSheet.isReadable || !wallSheet.isReadable) return FallbackFloor(width, height, biome);

            Style style = StyleFor(biome);
            var canvas = new Color32[cw * ch];
            var voidColor = (Color32)Color.Lerp(biome.mortar, Color.black, 0.7f);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = voidColor;

            Color32[] floorPx = floorSheet.GetPixels32();
            Color32[] wallPx = wallSheet.GetPixels32();
            var floorRect = new RectInt(SideMargin, BottomMargin, fw, fh);

            // Floor: the biome's tile pattern, repeated from the arena's top-left corner.
            int tilesW = Mathf.CeilToInt(fw / (float)Tile);
            int tilesH = Mathf.CeilToInt(fh / (float)Tile);
            for (int ty = 0; ty < tilesH; ty++)
            {
                for (int tx = 0; tx < tilesW; tx++)
                {
                    int sx = style.floor.x + tx % style.floor.width;
                    int sy = style.floor.y + ty % style.floor.height;
                    // Slight per-tile brightness drift so large floors never look stamped.
                    float drift = 1f + (Hash(tx, ty) - 0.5f) * 0.08f;
                    Blit(canvas, cw, ch, floorPx, floorSheet.width, floorSheet.height, sx, sy,
                         SideMargin + tx * Tile, BottomMargin + fh - (ty + 1) * Tile, style.tint * drift, floorRect);
                }
            }
            Vignette(canvas, cw, floorRect);

            // Wall frame around the floor (the frame's corners and edges, tile-sized).
            var full = new RectInt(0, 0, cw, ch);
            int ox = style.frame.x;
            int oy = style.frame.y;
            int topY = BottomMargin + fh;
            for (int x = SideMargin; x < SideMargin + fw; x += Tile)
            {
                Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox + 1, oy, x, topY, style.tint, full);
                Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox + 1, oy + 4, x, 0, style.tint, full);
            }
            for (int y = BottomMargin; y < BottomMargin + fh; y += Tile)
            {
                Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox, oy + 1, 0, y, style.tint, full);
                Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox + 4, oy + 1, SideMargin + fw, y, style.tint, full);
            }
            Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox, oy, 0, topY, style.tint, full);
            Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox + 4, oy, SideMargin + fw, topY, style.tint, full);
            Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox, oy + 4, 0, 0, style.tint, full);
            Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, ox + 4, oy + 4, SideMargin + fw, 0, style.tint, full);

            // Nobori banners hang along the back wall.
            Texture2D house = PixelArt.Texture(HouseSheet);
            if (house != null && house.isReadable)
            {
                Color32[] housePx = house.GetPixels32();
                int count = Mathf.Max(2, fw / (Tile * 5));
                for (int i = 0; i < count; i++)
                {
                    int bx = SideMargin + Mathf.RoundToInt((i + 0.5f) * fw / (float)count) - Tile / 2;
                    int bannerColumn = 22 + Random.Range(0, 7);
                    Blit(canvas, cw, ch, housePx, house.width, house.height, bannerColumn, 20, bx, topY + Tile, Color.white, full);
                    Blit(canvas, cw, ch, housePx, house.width, house.height, bannerColumn, 21, bx, topY, Color.white, full);
                }
            }

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

        /// <summary>
        /// Copies one 16 px tile (column, row counted from the sheet's top-left) into the canvas
        /// at pixel (dx, dy) from the canvas's bottom-left: alpha-tested, tinted, clipped.
        /// </summary>
        static void Blit(Color32[] canvas, int cw, int ch, Color32[] sheet, int sheetW, int sheetH, int tileX, int tileY, int dx, int dy, Color tint, RectInt clip)
        {
            int sx0 = tileX * Tile;
            int sy0 = sheetH - (tileY + 1) * Tile;
            if (sx0 < 0 || sy0 < 0 || sx0 + Tile > sheetW) return;
            for (int y = 0; y < Tile; y++)
            {
                int ty = dy + y;
                if (ty < clip.yMin || ty >= clip.yMax || ty < 0 || ty >= ch) continue;
                for (int x = 0; x < Tile; x++)
                {
                    int tx = dx + x;
                    if (tx < clip.xMin || tx >= clip.xMax || tx < 0 || tx >= cw) continue;
                    Color32 s = sheet[(sy0 + y) * sheetW + sx0 + x];
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
                    float k = 1f - 0.35f * Mathf.Pow(Mathf.Max(ex, ey), 3f);
                    Color32 c = canvas[y * cw + x];
                    canvas[y * cw + x] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
                }
            }
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
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
