using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Generates the placeholder arena: a pixel-art stone floor (point filtered, 16 px per
    /// unit) and walls. The floor gives the camera motion a visible reference.
    /// </summary>
    public static class ArenaBuilder
    {
        public static void Build(Transform parent, GameConfig config)
        {
            ArenaTuning a = config.arena;
            Palette pal = config.palette;

            var floor = SpriteUtil.Make("Floor", parent, BuildFloorSprite(a, pal), Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;

            float hw = a.width * 0.5f;
            float hh = a.height * 0.5f;
            float t = a.wallThickness;
            Sprite pixel = ShapeLibrary.Get(Shape.Pixel);

            MakeRect(parent, pixel, pal.wall, new Vector2(0f, hh + t * 0.5f), new Vector2(a.width + t * 2f, t), Sorting.Floor + 2);
            MakeRect(parent, pixel, pal.wall, new Vector2(0f, -hh - t * 0.5f), new Vector2(a.width + t * 2f, t), Sorting.Floor + 2);
            MakeRect(parent, pixel, pal.wall, new Vector2(-hw - t * 0.5f, 0f), new Vector2(t, a.height + t * 2f), Sorting.Floor + 2);
            MakeRect(parent, pixel, pal.wall, new Vector2(hw + t * 0.5f, 0f), new Vector2(t, a.height + t * 2f), Sorting.Floor + 2);

            const float edge = 0.06f;
            MakeRect(parent, pixel, pal.wallEdge, new Vector2(0f, hh + edge * 0.5f), new Vector2(a.width, edge), Sorting.Floor + 3);
            MakeRect(parent, pixel, pal.wallEdge, new Vector2(0f, -hh - edge * 0.5f), new Vector2(a.width, edge), Sorting.Floor + 3);
            MakeRect(parent, pixel, pal.wallEdge, new Vector2(-hw - edge * 0.5f, 0f), new Vector2(edge, a.height), Sorting.Floor + 3);
            MakeRect(parent, pixel, pal.wallEdge, new Vector2(hw + edge * 0.5f, 0f), new Vector2(edge, a.height), Sorting.Floor + 3);

            // A faint sigil in the center: the dueling ground.
            var sigil = SpriteUtil.Make("Sigil", parent, ShapeLibrary.Get(Shape.Ring), pal.gold.WithAlpha(0.06f), Sorting.Floor + 1);
            sigil.transform.localScale = new Vector3(5f, 5f, 1f);
            var sigilInner = SpriteUtil.Make("SigilInner", parent, ShapeLibrary.Get(Shape.DiamondOutline), pal.gold.WithAlpha(0.05f), Sorting.Floor + 1);
            sigilInner.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
        }

        static void MakeRect(Transform parent, Sprite pixel, Color color, Vector2 center, Vector2 size, int order)
        {
            var sr = SpriteUtil.Make("Wall", parent, pixel, color, order);
            sr.transform.localPosition = center;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        static Sprite BuildFloorSprite(ArenaTuning a, Palette pal)
        {
            int ppu = Mathf.Max(4, a.pixelsPerUnit);
            int w = Mathf.RoundToInt(a.width * ppu);
            int h = Mathf.RoundToInt(a.height * ppu);
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
                    Color c = mortar ? pal.mortar : Color.Lerp(pal.floorA, pal.floorB, Hash(col, row));
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
