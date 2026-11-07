using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Generates the placeholder arena: a pixel-art stone floor (point filtered, 16 px per
    /// unit) and walls. The floor gives the camera motion a visible reference.
    /// </summary>
    /// <summary>Animated arena props (braziers). Purely visual.</summary>
    public sealed class ArenaDecor
    {
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> cores = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> glows = new List<SpriteRenderer>();
        readonly List<float> seeds = new List<float>();

        public readonly List<Vector2> FlamePositions = new List<Vector2>();
        /// <summary>Generated per room; destroyed with the arena to avoid leaking textures.</summary>
        public Texture2D FloorTexture;
        public Color FlameColor = Color.white;

        public void AddBrazier(Vector2 flamePos, SpriteRenderer flame, SpriteRenderer core, SpriteRenderer glow)
        {
            FlamePositions.Add(flamePos);
            flames.Add(flame);
            cores.Add(core);
            glows.Add(glow);
            seeds.Add(flames.Count * 13.7f);
        }

        public void Animate(float time)
        {
            for (int i = 0; i < flames.Count; i++)
            {
                float n = Mathf.PerlinNoise(seeds[i], time * 3.2f);
                float m = Mathf.PerlinNoise(seeds[i] + 5f, time * 5.1f);
                flames[i].transform.localScale = new Vector3(0.34f + 0.06f * m, 0.5f + 0.18f * n, 1f);
                cores[i].transform.localScale = new Vector3(0.18f + 0.04f * n, 0.28f + 0.1f * m, 1f);
                float g = 3.6f + 0.5f * n;
                glows[i].transform.localScale = new Vector3(g, g, 1f);
                Color c = glows[i].color;
                c.a = 0.13f + 0.07f * n;
                glows[i].color = c;
            }
        }
    }

    public static class ArenaBuilder
    {
        /// <summary>Builds an arena of the room's size, painted with its biome.</summary>
        public static ArenaDecor Build(Transform parent, GameConfig config, float width, float height, Biome biome)
        {
            ArenaTuning a = config.arena;
            Palette pal = config.palette;

            Sprite floorSprite = BuildFloorSprite(width, height, a.pixelsPerUnit, biome);
            var floor = SpriteUtil.Make("Floor", parent, floorSprite, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;

            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float t = a.wallThickness;
            Sprite pixel = ShapeLibrary.Get(Shape.Pixel);

            MakeRect(parent, pixel, biome.wall, new Vector2(0f, hh + t * 0.5f), new Vector2(width + t * 2f, t), Sorting.Floor + 2);
            MakeRect(parent, pixel, biome.wall, new Vector2(0f, -hh - t * 0.5f), new Vector2(width + t * 2f, t), Sorting.Floor + 2);
            MakeRect(parent, pixel, biome.wall, new Vector2(-hw - t * 0.5f, 0f), new Vector2(t, height + t * 2f), Sorting.Floor + 2);
            MakeRect(parent, pixel, biome.wall, new Vector2(hw + t * 0.5f, 0f), new Vector2(t, height + t * 2f), Sorting.Floor + 2);

            const float edge = 0.06f;
            MakeRect(parent, pixel, biome.wallEdge, new Vector2(0f, hh + edge * 0.5f), new Vector2(width, edge), Sorting.Floor + 3);
            MakeRect(parent, pixel, biome.wallEdge, new Vector2(0f, -hh - edge * 0.5f), new Vector2(width, edge), Sorting.Floor + 3);
            MakeRect(parent, pixel, biome.wallEdge, new Vector2(-hw - edge * 0.5f, 0f), new Vector2(edge, height), Sorting.Floor + 3);
            MakeRect(parent, pixel, biome.wallEdge, new Vector2(hw + edge * 0.5f, 0f), new Vector2(edge, height), Sorting.Floor + 3);

            // A faint sigil in the center: the dueling ground.
            var sigil = SpriteUtil.Make("Sigil", parent, ShapeLibrary.Get(Shape.Ring), pal.gold.WithAlpha(0.06f), Sorting.Floor + 1);
            sigil.transform.localScale = new Vector3(5f, 5f, 1f);
            var sigilInner = SpriteUtil.Make("SigilInner", parent, ShapeLibrary.Get(Shape.DiamondOutline), pal.gold.WithAlpha(0.05f), Sorting.Floor + 1);
            sigilInner.transform.localScale = new Vector3(3.2f, 3.2f, 1f);

            var decor = new ArenaDecor { FloorTexture = floorSprite.texture, FlameColor = biome.flame };
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
            glow.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var shadow = SpriteUtil.Make("Shadow", root, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.5f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            shadow.transform.localScale = new Vector3(1.2f, 0.5f, 1f);
            var bowl = SpriteUtil.Make("Bowl", root, ShapeLibrary.Get(Shape.Hexagon), ColorUtil.Hex("#2E2836"), order);
            bowl.transform.localScale = new Vector3(0.75f, 0.55f, 1f);
            var rim = SpriteUtil.Make("Rim", root, ShapeLibrary.Get(Shape.Ring), ColorUtil.Hex("#5A4A3A"), order + 1);
            rim.transform.localScale = new Vector3(0.72f, 0.5f, 1f);
            var flame = SpriteUtil.Make("Flame", root, ShapeLibrary.Get(Shape.Diamond), biome.flame, order + 2);
            flame.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            var core = SpriteUtil.Make("FlameCore", root, ShapeLibrary.Get(Shape.Diamond), Color.Lerp(biome.flame, Color.white, 0.6f), order + 3);
            core.transform.localPosition = new Vector3(0f, 0.27f, 0f);

            decor.AddBrazier(pos + new Vector2(0f, 0.5f), flame, core, glow);
        }

        static void MakeRect(Transform parent, Sprite pixel, Color color, Vector2 center, Vector2 size, int order)
        {
            var sr = SpriteUtil.Make("Wall", parent, pixel, color, order);
            sr.transform.localPosition = center;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        static Sprite BuildFloorSprite(float width, float height, int pixelsPerUnit, Biome biome)
        {
            int ppu = Mathf.Max(4, pixelsPerUnit);
            int w = Mathf.RoundToInt(width * ppu);
            int h = Mathf.RoundToInt(height * ppu);
            int tile = ppu;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = y / tile;
                int offset = (row % 2) * (tile / 2);
                for (int x = 0; x < w; x++)
                {
                    int xo = x + offset;
                    int col = xo / tile;
                    bool mortar = y % tile == 0 || xo % tile == 0;
                    Color c = mortar ? biome.mortar : Color.Lerp(biome.floorA, biome.floorB, Hash(col, row));
                    float grain = 1f + (Hash(x * 7 + 3, y * 13 + 5) - 0.5f) * 0.08f;
                    // Darken toward the walls so the center reads as the focus.
                    float ex = Mathf.Abs(x / (float)w - 0.5f) * 2f;
                    float ey = Mathf.Abs(y / (float)h - 0.5f) * 2f;
                    float vignette = 1f - 0.25f * Mathf.Pow(Mathf.Max(ex, ey), 3f);
                    c *= grain * vignette;
                    c.a = 1f;
                    pixels[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ArenaFloor",
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
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
