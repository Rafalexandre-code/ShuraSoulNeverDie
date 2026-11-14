using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Animated arena props (lantern light) plus what browser players need to draw it.</summary>
    public sealed class ArenaDecor
    {
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
        readonly List<Sprite[]> flameFrames = new List<Sprite[]>();
        readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        readonly List<float> seeds = new List<float>();

        public readonly List<Vector2> FlamePositions = new List<Vector2>();
        /// <summary>Lantern feet positions (world).</summary>
        public readonly List<Vector2> Lanterns = new List<Vector2>();
        /// <summary>Statues: block in cells and which art (0 small shrine, 1 great lion).</summary>
        public readonly List<Vector4> Statues = new List<Vector4>();
        /// <summary>The sprites of each statue and lantern, so a blow can break them (§6.5).</summary>
        public readonly List<List<GameObject>> StatueViews = new List<List<GameObject>>();
        public readonly List<GameObject> LanternViews = new List<GameObject>();
        /// <summary>Generated per room; destroyed with the arena to avoid leaking textures.</summary>
        public Texture2D FloorTexture;
        public Color FlameColor = Color.white;
        /// <summary>The arena image as PNG (only when hosting browser players).</summary>
        public byte[] Png;
        /// <summary>Arena image size and where world (0,0) falls in it, in pixels from the bottom-left.</summary>
        public Vector2Int PixelSize;
        public Vector2 PixelPivot;
        public bool Moss;
        /// <summary>Sempre ao luar (o Bosque Selvagem): escuro azulado mínimo e vaga-lumes acesos.</summary>
        public bool Moonlit;

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
                if (glows[i] == null) continue;
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
    /// Builds each arena from its <see cref="ArenaLayout"/> as one pixel-art image from the
    /// Ninja Adventure tilesets: a temple floor that changes with the biome, walls that follow
    /// the outline (hall, octagon, cross…), nobori banners on the back wall; statues and
    /// paper lanterns stand as sprites so fighters walk in front of and behind them.
    /// 16 px per unit, point filtered.
    /// </summary>
    public static partial class ArenaBuilder
    {
        const int Tile = PixelArt.PixelsPerUnit;
        const string FloorSheet = "tiles_floor";
        const string WallSheet = "tiles_wall";
        const string HouseSheet = "tiles_house";

        /// <summary>Which tiles paint a biome (tile coordinates from the sheets' top-left).</summary>
        sealed class Style
        {
            public RectInt floor;
            public Vector2Int frame;
            public Color tint;
            public bool moss;
        }

        static Style StyleFor(Biome biome)
        {
            switch (biome.suffix)
            {
                case "do Musgo":
                    return new Style { floor = new RectInt(15, 6, 4, 4), frame = new Vector2Int(5, 6), tint = new Color(0.78f, 0.86f, 0.8f), moss = true };
                case "do Sangue":
                    return new Style { floor = new RectInt(4, 6, 4, 4), frame = new Vector2Int(5, 0), tint = new Color(0.78f, 0.45f, 0.42f) };
                case "do Abismo":
                    return new Style { floor = new RectInt(12, 13, 1, 1), frame = new Vector2Int(0, 6), tint = new Color(0.6f, 0.55f, 0.92f) };
                case "do Gelo":
                    return new Style { floor = new RectInt(15, 0, 4, 4), frame = new Vector2Int(0, 0), tint = new Color(0.66f, 0.8f, 0.98f) };
                case "do Bambuzal":
                    return new Style { floor = new RectInt(15, 6, 4, 4), frame = new Vector2Int(5, 6), tint = new Color(0.72f, 0.95f, 0.62f), moss = true };
                case "do Castelo":
                    return new Style { floor = new RectInt(0, 12, 4, 4), frame = new Vector2Int(5, 0), tint = new Color(0.82f, 0.62f, 0.45f) };
                case "do Rio Sanzu":
                    return new Style { floor = new RectInt(12, 13, 1, 1), frame = new Vector2Int(0, 6), tint = new Color(0.45f, 0.78f, 0.86f) };
                case "de Yomi":
                    return new Style { floor = new RectInt(15, 0, 4, 4), frame = new Vector2Int(0, 6), tint = new Color(0.62f, 0.58f, 0.72f) };
                default:
                    return new Style { floor = new RectInt(12, 13, 1, 1), frame = new Vector2Int(0, 6), tint = new Color(0.85f, 0.82f, 0.86f) };
            }
        }

        public static ArenaDecor Build(Transform parent, GameConfig config, ArenaLayout layout, Biome biome, bool encodePng, bool dojo = false, ulong seed = 0)
        {
            if (dojo) return BuildDojo(parent, layout, encodePng);
            if (biome.rule == BiomeRule.Wild) return BuildWild(parent, layout, biome, seed, encodePng);
            Style style = StyleFor(biome);
            var decor = new ArenaDecor { FlameColor = biome.flame, Moss = style.moss };
            Sprite arena = BuildArenaSprite(layout, biome, style, decor, encodePng);
            var floor = SpriteUtil.Make("Arena", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;
            decor.FloorTexture = arena.texture;

            foreach (RectInt block in layout.Statues) AddStatue(parent, layout, block, style.moss, decor);

            Rect fb = layout.FloorBounds;
            var corners = new[]
            {
                new Vector2(fb.xMin + 1.2f, fb.yMax - 1.0f), new Vector2(fb.xMax - 1.2f, fb.yMax - 1.0f),
                new Vector2(fb.xMin + 1.2f, fb.yMin + 0.9f), new Vector2(fb.xMax - 1.2f, fb.yMin + 0.9f),
            };
            foreach (Vector2 c in corners) AddLantern(parent, decor, layout, layout.NearestFloor(c, 0.6f), biome, style.moss);
            return decor;
        }

        /// <summary>Shrines on 2×2 pillars; the great lion (with columns) on the courtyard's centre.</summary>
        static void AddStatue(Transform parent, ArenaLayout layout, RectInt block, bool moss, ArenaDecor decor)
        {
            int row = moss ? 19 : 15;
            bool great = block.width >= 3;
            Vector2 baseCenter = layout.Origin + new Vector2(block.center.x, block.yMin);
            int order = -Mathf.RoundToInt(baseCenter.y * 20f);
            if (great)
            {
                Sprite lion = PixelArt.Region(HouseSheet, 1, row, 2, 3, new Vector2(0.5f, 0f));
                var s = SpriteUtil.Make("Lion", parent, lion, Color.white, order);
                s.transform.localPosition = baseCenter;
                Sprite column = PixelArt.Region(HouseSheet, 0, row, 1, 3, new Vector2(0.5f, 0f));
                var left = SpriteUtil.Make("Column", parent, column, Color.white, order);
                left.transform.localPosition = layout.Origin + new Vector2(block.xMin + 0.5f, block.yMin);
                var right = SpriteUtil.Make("Column", parent, column, Color.white, order);
                right.transform.localPosition = layout.Origin + new Vector2(block.xMax - 0.5f, block.yMin);
                decor.Statues.Add(new Vector4(block.xMin, block.yMin, block.width, 1));
                decor.StatueViews.Add(new List<GameObject> { s.gameObject, left.gameObject, right.gameObject });
            }
            else
            {
                // The orb shrine spans columns 3–4 of the sheet (column 4 cut it in half).
                Sprite shrine = PixelArt.Region(HouseSheet, 3, row, 2, 2, new Vector2(0.5f, 0f));
                var s = SpriteUtil.Make("Shrine", parent, shrine, Color.white, order);
                s.transform.localPosition = baseCenter;
                decor.Statues.Add(new Vector4(block.xMin, block.yMin, block.width, 0));
                decor.StatueViews.Add(new List<GameObject> { s.gameObject });
            }
        }

        /// <summary>
        /// The box a prop stands on, cut from its own pixels: the opaque columns of its lowest
        /// rows (the base), as deep as those rows are tall. One texel = 1/16 of a unit.
        /// </summary>
        public static Rect Footprint(Sprite s, Vector2 pivotWorld, int baseRows)
        {
            Texture2D tex = s.texture;
            Rect tr = s.textureRect;
            int x0 = Mathf.RoundToInt(tr.x), y0 = Mathf.RoundToInt(tr.y), w = Mathf.RoundToInt(tr.width);
            int minX = w, maxX = -1;
            for (int y = 0; y < baseRows; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (tex.GetPixel(x0 + x, y0 + y).a < 0.5f) continue;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }
            }
            if (maxX < 0) return new Rect(pivotWorld, Vector2.zero);
            float ppu = s.pixelsPerUnit;
            float left = pivotWorld.x + (minX - s.pivot.x) / ppu;
            float width = (maxX - minX + 1) / ppu;
            return new Rect(left, pivotWorld.y, width, baseRows / ppu);
        }

        /// <summary>A stone lantern (tōrō) whose fire flickers on the floor; you bump into its base.</summary>
        static void AddLantern(Transform parent, ArenaDecor decor, ArenaLayout layout, Vector2 pos, Biome biome, bool moss)
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

            Sprite lantern = PixelArt.Region(moss ? "prop_toro_moss" : "prop_toro", 0, 0, 1, 2, new Vector2(0.5f, 0f));
            var body = SpriteUtil.Make("Toro", root, lantern != null ? lantern : ShapeLibrary.Get(Shape.Capsule), Color.white, order);
            // The fire box's window glows; a soft halo breathes around it.
            var paper = SpriteUtil.Make("Paper", root, ShapeLibrary.Get(Shape.SoftCircle), biome.flame.WithAlpha(0.4f), order + 1);
            paper.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            paper.transform.localScale = new Vector3(0.55f, 0.45f, 1f);
            if (lantern != null && lantern.texture.isReadable)
            {
                // The base (the four lowest rows) is solid: duelists, enemies and arrows meet the stone.
                Rect foot = Footprint(lantern, pos, 4);
                foot.yMin -= 0.06f;
                layout.AddSolid(foot);
            }

            decor.Lanterns.Add(pos);
            decor.LanternViews.Add(root.gameObject);
            decor.AddBrazier(pos + new Vector2(0f, 1.2f), body, null, glow);
        }

        // ── Arena image ─────────────────────────────────────────────────────────────────

        static Sprite BuildArenaSprite(ArenaLayout layout, Biome biome, Style style, ArenaDecor decor, bool encodePng)
        {
            int cw = layout.W * Tile;
            int ch = layout.H * Tile;
            var canvas = new Color32[cw * ch];
            var voidColor = (Color32)Color.Lerp(biome.mortar, Color.black, 0.7f);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = voidColor;

            Texture2D floorSheet = PixelArt.Texture(FloorSheet);
            Texture2D wallSheet = PixelArt.Texture(WallSheet);
            bool art = floorSheet != null && wallSheet != null && floorSheet.isReadable && wallSheet.isReadable;
            Color32[] floorPx = art ? floorSheet.GetPixels32() : null;
            Color32[] wallPx = art ? wallSheet.GetPixels32() : null;
            var statueCells = new HashSet<Vector2Int>();
            foreach (RectInt b in layout.Statues)
            {
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    for (int x = b.xMin; x < b.xMax; x++) statueCells.Add(new Vector2Int(x, y));
                }
            }
            var full = new RectInt(0, 0, cw, ch);

            // Floor (also under statues, which stand on it as sprites).
            for (int y = 0; y < layout.H; y++)
            {
                for (int x = 0; x < layout.W; x++)
                {
                    bool isFloor = layout.IsFloor(x, y) || statueCells.Contains(new Vector2Int(x, y));
                    if (!isFloor) continue;
                    if (!art)
                    {
                        FillCell(canvas, cw, x, y, biome.floorA);
                        continue;
                    }
                    int ty = layout.H - 1 - y;
                    int sx = style.floor.x + x % style.floor.width;
                    int sy = style.floor.y + ty % style.floor.height;
                    float drift = 1f + (Hash(x, y) - 0.5f) * 0.08f;
                    Blit(canvas, cw, ch, floorPx, floorSheet.width, floorSheet.height, sx, sy, x * Tile, y * Tile, style.tint * drift, full);
                }
            }
            var floorRect = new RectInt(0, 0, cw, ch);
            Vignette(canvas, cw, layout, floorRect);

            // Walls follow the outline: each wall cell next to the floor takes the frame piece
            // whose stone faces that floor.
            int ox = style.frame.x, oy = style.frame.y;
            var bannerCandidates = new List<Vector2Int>();
            for (int y = 0; y < layout.H; y++)
            {
                for (int x = 0; x < layout.W; x++)
                {
                    if (layout.IsFloor(x, y) || statueCells.Contains(new Vector2Int(x, y))) continue;
                    bool n = Walkable(layout, statueCells, x, y + 1), s = Walkable(layout, statueCells, x, y - 1);
                    bool e = Walkable(layout, statueCells, x + 1, y), w = Walkable(layout, statueCells, x - 1, y);
                    bool ne = Walkable(layout, statueCells, x + 1, y + 1), nw = Walkable(layout, statueCells, x - 1, y + 1);
                    bool se = Walkable(layout, statueCells, x + 1, y - 1), sw = Walkable(layout, statueCells, x - 1, y - 1);
                    // An inner corner (a step of a diagonal, the notch of a cross): the sheet has no
                    // piece for it, so the horizontal and the vertical bands meet in a mitre.
                    if (art && (s ^ n) && (e ^ w))
                    {
                        var h = new Vector2Int(ox + 3, s ? oy : oy + 4);
                        var v = new Vector2Int(e ? ox : ox + 4, oy + 1);
                        BlitMitre(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, h, v, s, e, x * Tile, y * Tile, style.tint);
                        continue;
                    }
                    Vector2Int piece;
                    // The middle of the long edge (column 3) is seamless; column 1 ends in a joint.
                    if (s) piece = new Vector2Int(ox + 3, oy);
                    else if (n) piece = new Vector2Int(ox + 3, oy + 4);
                    else if (e) piece = new Vector2Int(ox, oy + 1);
                    else if (w) piece = new Vector2Int(ox + 4, oy + 1);
                    else if (se) piece = new Vector2Int(ox, oy);
                    else if (sw) piece = new Vector2Int(ox + 4, oy);
                    else if (ne) piece = new Vector2Int(ox, oy + 4);
                    else if (nw) piece = new Vector2Int(ox + 4, oy + 4);
                    else continue;
                    if (!art)
                    {
                        FillCell(canvas, cw, x, y, biome.wall);
                        continue;
                    }
                    Blit(canvas, cw, ch, wallPx, wallSheet.width, wallSheet.height, piece.x, piece.y, x * Tile, y * Tile, style.tint, full);
                    if (s && y >= layout.H / 2) bannerCandidates.Add(new Vector2Int(x, y));
                }
            }

            // Nobori banners hang along the back walls.
            Texture2D house = PixelArt.Texture(HouseSheet);
            if (house != null && house.isReadable && bannerCandidates.Count > 0)
            {
                Color32[] housePx = house.GetPixels32();
                int step = Mathf.Max(3, bannerCandidates.Count / Mathf.Max(2, layout.W / 6));
                for (int i = step / 2; i < bannerCandidates.Count; i += step)
                {
                    Vector2Int c = bannerCandidates[i];
                    int column = 22 + (int)(Hash(c.x, c.y) * 6.99f);
                    Blit(canvas, cw, ch, housePx, house.width, house.height, column, 21, c.x * Tile, c.y * Tile, Color.white, full);
                    Blit(canvas, cw, ch, housePx, house.width, house.height, column, 20, c.x * Tile, (c.y + 1) * Tile, Color.white, full);
                }
            }

            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Arena",
            };
            tex.SetPixels32(canvas);
            tex.Apply(false, false);
            if (encodePng) decor.Png = tex.EncodeToPNG();
            decor.PixelSize = new Vector2Int(cw, ch);
            decor.PixelPivot = -layout.Origin * Tile;
            var pivot = new Vector2(-layout.Origin.x / layout.W, -layout.Origin.y / layout.H);
            return Sprite.Create(tex, new Rect(0, 0, cw, ch), pivot, Tile);
        }

        static bool Walkable(ArenaLayout layout, HashSet<Vector2Int> statues, int x, int y) =>
            layout.IsFloor(x, y) && !statues.Contains(new Vector2Int(x, y));

        static void FillCell(Color32[] canvas, int cw, int x, int y, Color c)
        {
            var c32 = (Color32)c;
            for (int py = 0; py < Tile; py++)
            {
                for (int px = 0; px < Tile; px++) canvas[(y * Tile + py) * cw + x * Tile + px] = c32;
            }
        }

        /// <summary>
        /// Two band tiles cut along the diagonal: each pixel comes from the band whose floor edge
        /// is nearer, so the stone turns the corner without a seam.
        /// </summary>
        static void BlitMitre(Color32[] canvas, int cw, int ch, Color32[] sheet, int sheetW, int sheetH,
                              Vector2Int horizontal, Vector2Int vertical, bool floorBelow, bool floorRight, int dx, int dy, Color tint)
        {
            int hx = horizontal.x * Tile, hy = sheetH - (horizontal.y + 1) * Tile;
            int vx = vertical.x * Tile, vy = sheetH - (vertical.y + 1) * Tile;
            if (hx < 0 || hy < 0 || vx < 0 || vy < 0 || hx + Tile > sheetW || vx + Tile > sheetW) return;
            for (int y = 0; y < Tile; y++)
            {
                int ty = dy + y;
                if (ty < 0 || ty >= ch) continue;
                int toHorizontal = floorBelow ? y : Tile - 1 - y;
                for (int x = 0; x < Tile; x++)
                {
                    int tx = dx + x;
                    if (tx < 0 || tx >= cw) continue;
                    int toVertical = floorRight ? Tile - 1 - x : x;
                    Color32 c = toHorizontal <= toVertical ? sheet[(hy + y) * sheetW + hx + x] : sheet[(vy + y) * sheetW + vx + x];
                    if (c.a < 128) continue;
                    canvas[ty * cw + tx] = new Color32(
                        (byte)Mathf.Clamp(c.r * tint.r, 0f, 255f),
                        (byte)Mathf.Clamp(c.g * tint.g, 0f, 255f),
                        (byte)Mathf.Clamp(c.b * tint.b, 0f, 255f), 255);
                }
            }
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

        /// <summary>Darkens the floor toward its edges so the centre reads as the dueling ground.</summary>
        static void Vignette(Color32[] canvas, int cw, ArenaLayout layout, RectInt area)
        {
            Rect fb = layout.FloorBounds;
            float x0 = (fb.xMin - layout.Origin.x) * Tile, x1 = (fb.xMax - layout.Origin.x) * Tile;
            float y0 = (fb.yMin - layout.Origin.y) * Tile, y1 = (fb.yMax - layout.Origin.y) * Tile;
            for (int y = area.yMin; y < area.yMax; y++)
            {
                float ey = Mathf.Abs((y - y0) / Mathf.Max(1f, y1 - y0) - 0.5f) * 2f;
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    if (!layout.IsFloor(x / Tile, y / Tile)) continue;
                    float ex = Mathf.Abs((x - x0) / Mathf.Max(1f, x1 - x0) - 0.5f) * 2f;
                    float k = 1f - 0.35f * Mathf.Pow(Mathf.Clamp01(Mathf.Max(ex, ey)), 3f);
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
    }
}
