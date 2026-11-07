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
