using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Wild Wood (Bosque Selvagem): a clearing in a forest — grass and trodden earth, a
    /// hedge of bushes for walls, a few trees standing in the fight, and one or two shallow
    /// lakes you can wade into (they mirror whoever steps in and ripple under every step).
    /// Everything comes from the room's own seed, so every player sees the same clearing.
    /// </summary>
    public static partial class ArenaBuilder
    {
        public static ArenaDecor BuildWild(Transform parent, ArenaLayout layout, Biome biome, ulong seed, bool encodePng)
        {
            var decor = new ArenaDecor { FlameColor = biome.flame, Moss = true };
            var rng = new System.Random((int)(seed % 2000000000UL) + 31);
            PlaceLakes(layout, rng);
            Sprite arena = BuildWildSprite(layout, decor, (int)(seed % 9973UL), encodePng);
            var floor = SpriteUtil.Make("Clearing", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;
            decor.FloorTexture = arena.texture;
            AddWilds(parent, layout, (int)(seed % 997UL) + 3, 1.25f);
            AddLakeSurfaces(parent, layout);
            AddClearingTrees(parent, layout, rng);
            // A great tree over every thicket.
            foreach (RectInt b in layout.Statues)
            {
                Vector2 at = layout.Origin + new Vector2(b.center.x, b.yMin + 0.2f);
                RectInt t = b.width >= 3 ? new RectInt(3, 18, 3, 3) : Trees[rng.Next(4)];
                Prop(parent, "ThicketTree", PixelArt.Region(NatureSheet, t.x, t.y, t.width, t.height, new Vector2(0.5f, 0.05f)), at, b.width >= 3 ? 1.3f : 1.1f);
            }

            Rect fb = layout.FloorBounds;
            var corners = new[]
            {
                new Vector2(fb.xMin + 1.2f, fb.yMax - 1.0f), new Vector2(fb.xMax - 1.2f, fb.yMax - 1.0f),
                new Vector2(fb.xMin + 1.2f, fb.yMin + 0.9f), new Vector2(fb.xMax - 1.2f, fb.yMin + 0.9f),
            };
            foreach (Vector2 c in corners)
            {
                Vector2 at = layout.NearestFloor(c, 0.6f);
                if (layout.LakeDepth(at) > 0f) continue;
                AddLantern(parent, decor, layout, at, biome, true);
            }
            return decor;
        }

        /// <summary>One or two lakes, never on the middle of the clearing (where the fight starts).</summary>
        static void PlaceLakes(ArenaLayout layout, System.Random rng)
        {
            Rect fb = layout.FloorBounds;
            int count = fb.width * fb.height > 260f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float rx = 2.2f + (float)rng.NextDouble() * 1.8f;
                    float ry = rx * (0.55f + (float)rng.NextDouble() * 0.25f);
                    var c = new Vector2(Mathf.Lerp(fb.xMin + rx + 0.8f, fb.xMax - rx - 0.8f, (float)rng.NextDouble()),
                                        Mathf.Lerp(fb.yMin + ry + 0.8f, fb.yMax - ry - 0.8f, (float)rng.NextDouble()));
                    if (Vector2.Distance(c, fb.center) < rx + 2.5f) continue;
                    bool clash = false;
                    foreach (Vector4 l in layout.Lakes) clash |= Vector2.Distance(c, new Vector2(l.x, l.y)) < rx + l.z + 1.5f;
                    if (clash) continue;
                    layout.AddLake(c, new Vector2(rx, ry));
                    break;
                }
            }
        }

        static Sprite BuildWildSprite(ArenaLayout layout, ArenaDecor decor, int seed, bool encodePng)
        {
            int cw = layout.W * Tile, ch = layout.H * Tile;
            var canvas = new Color32[cw * ch];
            // Where the halls would raise a statue, the wood grows a thicket.
            var thicket = new HashSet<Vector2Int>();
            foreach (RectInt b in layout.Statues)
            {
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    for (int x = b.xMin; x < b.xMax; x++) thicket.Add(new Vector2Int(x, y));
                }
            }
            for (int py = 0; py < ch; py++)
            {
                int cy = py / Tile;
                for (int px = 0; px < cw; px++)
                {
                    int cx = px / Tile;
                    Vector2 world = layout.Origin + new Vector2((px + 0.5f) / Tile, (py + 0.5f) / Tile);
                    if (thicket.Contains(new Vector2Int(cx, cy))) canvas[py * cw + px] = HedgePixel(px, py, seed);
                    else if (layout.IsFloor(cx, cy))
                    {
                        float depth = layout.LakeDepth(world);
                        float shore = layout.LakeDepth(world, 0.45f);
                        canvas[py * cw + px] = depth > 0f ? WaterPixel(px, py, depth) : shore > 0f ? ShorePixel(px, py, shore) : GrassPixel(px, py, seed, world);
                    }
                    else if (NearFloor(layout, cx, cy)) canvas[py * cw + px] = HedgePixel(px, py, seed);
                    else canvas[py * cw + px] = new Color32(0, 0, 0, 0);
                }
            }
            var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Clearing" };
            tex.SetPixels32(canvas);
            tex.Apply(false, false);
            if (encodePng) decor.Png = tex.EncodeToPNG();
            decor.PixelSize = new Vector2Int(cw, ch);
            decor.PixelPivot = -layout.Origin * Tile;
            var pivot = new Vector2(-layout.Origin.x / layout.W, -layout.Origin.y / layout.H);
            return Sprite.Create(tex, new Rect(0, 0, cw, ch), pivot, Tile);
        }

        static Color32 Rgb(float r, float g, float b) => new Color32((byte)Mathf.Clamp(r * 255f, 0f, 255f), (byte)Mathf.Clamp(g * 255f, 0f, 255f), (byte)Mathf.Clamp(b * 255f, 0f, 255f), 255);

        static Color32 GrassPixel(int x, int y, int seed, Vector2 world)
        {
            float n = Hash((x >> 1) + seed, y >> 1);
            float patch = Mathf.PerlinNoise(world.x * 0.18f + seed, world.y * 0.18f);
            // Trodden earth where the clearing is used most.
            if (patch < 0.3f)
            {
                float k = 0.85f + 0.2f * n;
                return Rgb(0.42f * k, 0.31f * k, 0.2f * k);
            }
            float g = 0.82f + 0.3f * patch + 0.1f * (n - 0.5f);
            if (n > 0.97f) return Rgb(0.5f, 0.7f, 0.32f);
            if (n < 0.012f) return Rgb(0.9f, 0.85f, 0.55f);       // a tiny flower
            return Rgb(0.24f * g, 0.43f * g, 0.2f * g);
        }

        static Color32 WaterPixel(int x, int y, float depth)
        {
            // Deep in the middle, clear near the edge; a soft pixel ripple pattern.
            float d = Mathf.SmoothStep(0f, 1f, depth);
            float wave = Hash(x >> 2, (y + (x >> 3)) >> 1) * 0.05f;
            return Rgb(Mathf.Lerp(0.2f, 0.06f, d) + wave, Mathf.Lerp(0.42f, 0.2f, d) + wave, Mathf.Lerp(0.5f, 0.34f, d) + wave);
        }

        static Color32 ShorePixel(int x, int y, float shore)
        {
            // Wet sand and reeds at the rim.
            float n = Hash(x, y);
            if (n > 0.93f) return Rgb(0.36f, 0.5f, 0.22f);
            float k = 0.88f + 0.18f * n;
            return Rgb(0.46f * k, 0.4f * k, 0.28f * k);
        }

        static Color32 HedgePixel(int x, int y, int seed)
        {
            // Dense bushes: dark leaves with a few lit ones.
            float n = Hash((x >> 1) + seed * 3, (y >> 1) + 7);
            float blob = Mathf.PerlinNoise(x * 0.09f + seed, y * 0.09f);
            float k = 0.65f + 0.45f * blob;
            if (n > 0.9f) return Rgb(0.32f, 0.52f, 0.24f);
            return Rgb(0.1f * k, 0.24f * k, 0.12f * k);
        }

        /// <summary>The moving light on each lake: a mask of its shape, drawn with the water shader.</summary>
        static void AddLakeSurfaces(Transform parent, ArenaLayout layout)
        {
            Shader water = Resources.Load<Shader>("Shaders/ShuraWater");
            if (water == null || !water.isSupported) return;
            var mat = new Material(water);
            foreach (Vector4 l in layout.Lakes)
            {
                int w = Mathf.CeilToInt(l.z * 2f * Tile), h = Mathf.CeilToInt(l.w * 2f * Tile);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Clamp01((1f - r) * 6f);
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "LakeMask" };
                tex.SetPixels32(px);
                tex.Apply(false, false);
                var s = SpriteUtil.Make("LakeSurface", parent, Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Tile), Color.white, Sorting.Floor + 2);
                s.sharedMaterial = mat;
                s.transform.localPosition = new Vector2(l.x, l.y);
            }
        }

        /// <summary>A few trees stand in the clearing: they block blades and bodies at their trunks.</summary>
        static void AddClearingTrees(Transform parent, ArenaLayout layout, System.Random rng)
        {
            Rect fb = layout.FloorBounds;
            int want = 2 + rng.Next(3);
            int placed = 0;
            for (int attempt = 0; attempt < 40 && placed < want; attempt++)
            {
                var p = new Vector2(Mathf.Lerp(fb.xMin + 1.5f, fb.xMax - 1.5f, (float)rng.NextDouble()), Mathf.Lerp(fb.yMin + 1.2f, fb.yMax - 1.5f, (float)rng.NextDouble()));
                if (Vector2.Distance(p, fb.center) < 3.5f || layout.LakeDepth(p, 1.2f) > 0f || layout.InSolid(p)) continue;
                RectInt t = Trees[rng.Next(Trees.Length)];
                Sprite s = PixelArt.Region(NatureSheet, t.x, t.y, t.width, t.height, new Vector2(0.5f, 0.05f));
                if (s == null) continue;
                Prop(parent, "Tree", s, p);
                // The trunk only (the crown is above): a narrow box at the base.
                layout.AddSolid(new Rect(p.x - 0.35f, p.y - 0.1f, 0.7f, 0.45f));
                placed++;
            }
        }
    }
}
