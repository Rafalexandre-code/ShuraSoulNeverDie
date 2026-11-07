using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    public enum Shape
    {
        Circle,
        Ring,
        Square,
        Triangle,
        Hexagon,
        Diamond,
        DiamondOutline,
        SoftCircle,
        Pixel,
        PixelLeft,
        /// <summary>Weapons are 1 unit long, pivot at the grip (left), pointing along +X.</summary>
        Blade,
        Spear,
        Staff,
        Axe,
        Arrow,
        /// <summary>Square sprite centered on the grip (bows are held, not swung from a hilt).</summary>
        Bow,
        Pentagon,
        Octagon,
        Capsule,
        Shield,
        /// <summary>Four-point sparkle used for blade-on-blade clashes.</summary>
        Star,
        Greatsword,
        Glaive,
        Sickle,
        Gauntlet,
    }

    /// <summary>
    /// Placeholder sprites rasterized at startup from signed-distance shapes. Every sprite is
    /// 1 world unit wide at scale 1, so entity code scales them by real-world size and the
    /// final pixel art can replace them without touching gameplay.
    /// </summary>
    public static class ShapeLibrary
    {
        const int Resolution = 64;
        const int WedgeResolution = 128;

        static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        static readonly Vector2[] TriangleVerts = { new Vector2(0.95f, 0f), new Vector2(-0.7f, 0.8f), new Vector2(-0.7f, -0.8f) };
        static readonly Vector2[] DiamondVerts = { new Vector2(0.95f, 0f), new Vector2(0f, 0.95f), new Vector2(-0.95f, 0f), new Vector2(0f, -0.95f) };
        static readonly Vector2[] HexagonVerts = BuildRegularPolygon(6, 0.95f);
        static readonly Vector2[] PentagonVerts = BuildRegularPolygon(5, 0.95f);
        static readonly Vector2[] OctagonVerts = BuildRegularPolygon(8, 0.95f);

        static float RoundedRect(Vector2 p, float halfX, float halfY, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - (halfX - radius), Mathf.Abs(p.y) - (halfY - radius));
            Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        public static Sprite Get(Shape shape)
        {
            int key = (int)shape;
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

            switch (shape)
            {
                case Shape.Pixel:
                    sprite = Solid(new Vector2(0.5f, 0.5f));
                    break;
                case Shape.PixelLeft:
                    sprite = Solid(new Vector2(0f, 0.5f));
                    break;
                case Shape.Blade:
                    sprite = RasterizeLong(BladeTexel);
                    break;
                case Shape.Spear:
                    sprite = RasterizeLong(SpearTexel);
                    break;
                case Shape.Staff:
                    sprite = RasterizeLong(StaffTexel);
                    break;
                case Shape.Axe:
                    sprite = RasterizeLong(AxeTexel);
                    break;
                case Shape.Arrow:
                    sprite = RasterizeLong(ArrowTexel);
                    break;
                case Shape.Greatsword:
                    sprite = RasterizeLong(GreatswordTexel);
                    break;
                case Shape.Glaive:
                    sprite = RasterizeLong(GlaiveTexel);
                    break;
                case Shape.Sickle:
                    sprite = RasterizeLong(SickleTexel);
                    break;
                case Shape.Gauntlet:
                    sprite = RasterizeLong(GauntletTexel);
                    break;
                default:
                    sprite = Rasterize(Resolution, p => Coverage(shape, p, Resolution));
                    break;
            }
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A circular sector pointing along +X, used for swing and parry arcs.</summary>
        public static Sprite Wedge(float degrees)
        {
            int deg = Mathf.Clamp(Mathf.RoundToInt(degrees), 1, 360);
            int key = 1000 + deg;
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            float half = deg * 0.5f * Mathf.Deg2Rad;
            sprite = Rasterize(WedgeResolution, p => WedgeCoverage(p, half, WedgeResolution));
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A crescent band (ring sector) used as the slash trail of a swing.</summary>
        public static Sprite ArcBand(float degrees)
        {
            int deg = Mathf.Clamp(Mathf.RoundToInt(degrees), 1, 360);
            int key = 2000 + deg;
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            float half = deg * 0.5f * Mathf.Deg2Rad;
            sprite = Rasterize(WedgeResolution, p => ArcBandCoverage(p, half, WedgeResolution));
            Cache[key] = sprite;
            return sprite;
        }

        static float ArcBandCoverage(Vector2 p, float halfAngle, int res)
        {
            float len = p.magnitude;
            float ang = Mathf.Abs(Mathf.Atan2(p.y, p.x));
            float band = Aa(Mathf.Abs(len - 0.82f) - 0.15f, res);
            float edgeFade = Mathf.Clamp01((halfAngle - ang) / Mathf.Max(0.05f, halfAngle * 0.35f));
            // Brightest at the outer rim, like the tip of the blade cutting the air.
            float rim = Mathf.Clamp01((len - 0.67f) / 0.3f);
            return band * edgeFade * (0.3f + 0.7f * rim * rim);
        }

        // Long sprites: u = 0..1 along the weapon, v = -1..1 across. RGB carries baked shading.
        const int LongWidth = 128;
        const int LongHeight = 32;
        const float LongPixelV = 2f / LongHeight;

        static Color BladeTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u < 0.04f) return Shade(0.55f, AaV(0.32f - av));                       // pommel
            if (u < 0.22f) return Shade(0.4f + 0.1f * Mathf.Sin(u * 90f), AaV(0.2f - av)); // wrapped grip
            if (u < 0.26f) return Shade(0.8f, AaV(0.75f - av));                         // guard
            float half = 0.42f * (1f - Smooth(0.78f, 1f, u));
            float shade = v > 0.1f ? 1f : 0.82f;                                         // bevel highlight
            return Shade(shade, AaV(half - av));
        }

        static Color SpearTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u < 0.76f) return Shade(0.55f, AaV(0.13f - av));                        // shaft
            if (u < 0.79f) return Shade(0.75f, AaV(0.3f - av));                         // binding
            float half = 0.55f * (1f - (u - 0.79f) / 0.21f);
            return Shade(v > 0f ? 1f : 0.85f, AaV(half - av));                          // blade tip
        }

        static Color StaffTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u < 0.07f || u > 0.93f) return Shade(0.95f, AaV(0.42f - av));          // weighted ends
            if (Mathf.Abs(u - 0.5f) < 0.025f) return Shade(0.85f, AaV(0.3f - av));     // central ring
            return Shade(0.62f, AaV(0.17f - av));
        }

        static Color AxeTexel(float u, float v)
        {
            Color shaft = Shade(0.5f, u < 0.9f ? AaV(0.12f - Mathf.Abs(v)) : 0f);
            if (u < 0.58f || u > 0.98f) return shaft;
            // Crescent blade on one side of the haft.
            float top = 0.95f * Mathf.Sin(Mathf.PI * (u - 0.58f) / 0.4f);
            float head = v > 0f ? AaV(Mathf.Min(top - v, v + 0.05f)) : 0f;
            return head > shaft.a ? Shade(v > top * 0.75f ? 1f : 0.85f, head) : shaft;
        }

        static Color ArrowTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u > 0.8f) return Shade(1f, AaV(0.38f * (1f - (u - 0.8f) / 0.2f) - av));     // head
            if (u < 0.16f) return Shade(0.9f, AaV(0.32f - av) * (u > 0.03f ? 1f : 0f));    // fletching
            return Shade(0.7f, AaV(0.07f - av));                                            // shaft
        }

        static Color GreatswordTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u < 0.03f) return Shade(0.6f, AaV(0.36f - av));                             // pommel
            if (u < 0.2f) return Shade(0.38f + 0.12f * Mathf.Sin(u * 110f), AaV(0.18f - av)); // long grip
            if (u < 0.25f) return Shade(0.75f, AaV(0.95f - av));                             // wide guard
            float half = 0.62f * (1f - Smooth(0.85f, 1f, u));
            float fuller = av < 0.08f ? 0.7f : (v > 0f ? 1f : 0.82f);                       // central groove
            return Shade(fuller, AaV(half - av));
        }

        static Color GlaiveTexel(float u, float v)
        {
            Color shaft = Shade(0.5f, u < 0.66f ? AaV(0.11f - Mathf.Abs(v)) : 0f);
            if (u < 0.64f) return shaft;
            if (u < 0.67f) return Shade(0.75f, AaV(0.32f - Mathf.Abs(v)));                  // collar
            // Curved single-edged blade: its centre line bends upward toward the tip.
            float t = (u - 0.67f) / 0.33f;
            float centre = 0.35f * t * t;
            float half = 0.4f * (1f - t * 0.85f);
            float d = Mathf.Abs(v - centre);
            return Shade(v > centre ? 1f : 0.82f, AaV(half - d));
        }

        static Color SickleTexel(float u, float v)
        {
            Color handle = Shade(0.45f, u < 0.5f ? AaV(0.13f - Mathf.Abs(v)) : 0f);
            if (u < 0.45f) return handle;
            // Hooked blade sweeping back from the top of the handle.
            float x = (u - 0.45f) / 0.55f;
            float centre = 0.75f * Mathf.Sin(x * Mathf.PI * 0.9f);
            float half = 0.22f * (1f - x * 0.8f);
            float blade = AaV(half - Mathf.Abs(v - centre));
            return blade > handle.a ? Shade(v > centre ? 1f : 0.85f, blade) : handle;
        }

        static Color GauntletTexel(float u, float v)
        {
            float av = Mathf.Abs(v);
            if (u < 0.35f) return Shade(0.5f, AaV(0.38f - av));                              // cuff
            if (u < 0.42f) return Shade(0.7f, AaV(0.48f - av));                              // wrist band
            // Fist with knuckle ridges.
            float knuckles = u > 0.82f && Mathf.Repeat(v * 3.2f + 0.5f, 1f) < 0.18f ? 0.65f : 0.9f;
            float shape = Mathf.Min(0.62f - av, (1f - u) * 2.2f);
            return Shade(knuckles, AaV(shape));
        }

        /// <summary>Irregular blood splat, pivot centred. A few variants so floors never look stamped.</summary>
        public static Sprite Splat(int variant)
        {
            int v = Mathf.Abs(variant) % 4;
            int key = 3000 + v;
            if (Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            var rng = new System.Random(911 + v * 37);
            float[] phase = new float[5];
            float[] amp = new float[5];
            for (int i = 0; i < 5; i++)
            {
                phase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
                amp[i] = 0.05f + (float)rng.NextDouble() * 0.08f;
            }
            var drops = new Vector3[6];
            for (int i = 0; i < drops.Length; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 0.62f + (float)rng.NextDouble() * 0.3f;
                drops[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.04f + (float)rng.NextDouble() * 0.07f);
            }
            sprite = Rasterize(Resolution, p =>
            {
                float ang = Mathf.Atan2(p.y, p.x);
                float radius = 0.5f;
                for (int i = 0; i < 5; i++) radius += amp[i] * Mathf.Sin(ang * (i + 2) + phase[i]);
                float coverage = Aa(p.magnitude - radius, Resolution);
                foreach (Vector3 d in drops)
                {
                    coverage = Mathf.Max(coverage, Aa(Vector2.Distance(p, new Vector2(d.x, d.y)) - d.z, Resolution));
                }
                return coverage;
            });
            Cache[key] = sprite;
            return sprite;
        }

        static float AaV(float sdfInside) => Mathf.Clamp01(0.5f + sdfInside / LongPixelV);
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        static Color Shade(float brightness, float alpha) => new Color(brightness, brightness, brightness, alpha);

        static Sprite RasterizeLong(Func<float, float, Color> texel)
        {
            var tex = new Texture2D(LongWidth, LongHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ShuraWeapon",
            };
            var pixels = new Color32[LongWidth * LongHeight];
            for (int y = 0; y < LongHeight; y++)
            {
                for (int x = 0; x < LongWidth; x++)
                {
                    float u = (x + 0.5f) / LongWidth;
                    float v = (y + 0.5f) / LongHeight * 2f - 1f;
                    pixels[y * LongWidth + x] = texel(u, v);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, LongWidth, LongHeight), new Vector2(0f, 0.5f), LongWidth);
        }

        /// <summary>Full-screen darkening toward the corners; alpha only.</summary>
        public static Texture2D VignetteTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ShuraVignette",
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                    byte a = (byte)(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d)) * 255f);
                    pixels[y * size + x] = new Color32(0, 0, 0, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static float Coverage(Shape shape, Vector2 p, int res)
        {
            switch (shape)
            {
                case Shape.Circle: return Aa(p.magnitude - 0.94f, res);
                case Shape.Ring: return Aa(Mathf.Abs(p.magnitude - 0.88f) - 0.07f, res);
                case Shape.Square: return Aa(RoundedBox(p, 0.82f, 0.08f), res);
                case Shape.Triangle: return Aa(Polygon(p, TriangleVerts), res);
                case Shape.Hexagon: return Aa(Polygon(p, HexagonVerts), res);
                case Shape.Diamond: return Aa(Polygon(p, DiamondVerts), res);
                case Shape.DiamondOutline: return Aa(Mathf.Abs(Polygon(p, DiamondVerts) + 0.12f) - 0.12f, res);
                case Shape.Pentagon: return Aa(Polygon(p, PentagonVerts), res);
                case Shape.Octagon: return Aa(Polygon(p, OctagonVerts), res);
                case Shape.Capsule: return Aa(RoundedRect(p, 0.94f, 0.58f, 0.5f), res);
                case Shape.Shield: return Aa(RoundedRect(p, 0.5f, 0.94f, 0.3f), res);
                case Shape.Star:
                {
                    float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                    float d1 = (ax / 0.97f + ay / 0.2f - 1f) * 0.18f;
                    float d2 = (ax / 0.2f + ay / 0.97f - 1f) * 0.18f;
                    return Aa(Mathf.Min(d1, d2), res);
                }
                case Shape.Bow:
                {
                    float len = p.magnitude;
                    float ang = Mathf.Abs(Mathf.Atan2(p.y, p.x));
                    float limb = ang < 70f * Mathf.Deg2Rad ? Aa(Mathf.Abs(len - 0.75f) - 0.07f, res) : 0f;
                    float stringLine = Mathf.Abs(p.y) < 0.7f ? Aa(Mathf.Abs(p.x - 0.26f) - 0.02f, res) * 0.8f : 0f;
                    return Mathf.Max(limb, stringLine);
                }
                case Shape.SoftCircle:
                {
                    float t = 1f - Mathf.Clamp01(p.magnitude);
                    return Mathf.Pow(t, 1.8f);
                }
                default: return 0f;
            }
        }

        static float WedgeCoverage(Vector2 p, float halfAngle, int res)
        {
            float len = p.magnitude;
            float ang = Mathf.Abs(Mathf.Atan2(p.y, p.x));
            float radial = len - 0.97f;
            float angular = ang <= halfAngle
                ? -len * Mathf.Sin(Mathf.Min(halfAngle - ang, Mathf.PI * 0.5f))
                : len * Mathf.Sin(Mathf.Min(ang - halfAngle, Mathf.PI * 0.5f));
            // Brighter toward the rim reads as the blade's path rather than a flat slice.
            return Aa(Mathf.Max(radial, angular), res) * (0.25f + 0.75f * len);
        }

        static float Aa(float sdf, int res)
        {
            return Mathf.Clamp01(0.5f - sdf * res * 0.5f);
        }

        static float RoundedBox(Vector2 p, float half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - half, Mathf.Abs(p.y) - half);
            Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>Signed distance (exact inside, close enough outside) for a convex CCW polygon.</summary>
        static float Polygon(Vector2 p, Vector2[] verts)
        {
            float d = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 a = verts[i];
                Vector2 b = verts[(i + 1) % verts.Length];
                Vector2 e = b - a;
                Vector2 n = new Vector2(e.y, -e.x).normalized;
                d = Mathf.Max(d, Vector2.Dot(p - a, n));
            }
            return d;
        }

        static Vector2[] BuildRegularPolygon(int sides, float radius)
        {
            var verts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                verts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            return verts;
        }

        static Sprite Rasterize(int res, Func<Vector2, float> coverage)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ShuraShape",
            };
            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    var p = new Vector2((x + 0.5f) / res * 2f - 1f, (y + 0.5f) / res * 2f - 1f);
                    byte a = (byte)(Mathf.Clamp01(coverage(p)) * 255f);
                    pixels[y * res + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        }

        static Sprite Solid(Vector2 pivot)
        {
            const int size = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ShuraPixel",
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), pivot, size);
        }
    }
}
