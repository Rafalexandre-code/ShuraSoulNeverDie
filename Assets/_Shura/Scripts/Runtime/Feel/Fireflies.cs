using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Fireflies over the wild ground: they wander on slow Perlin paths, rise and settle, and
    /// pulse — a brighter core in a faint halo. They only come out in the dark (dusk, night,
    /// a room whose lanterns were broken) and each one is a small light in the light map.
    /// </summary>
    public sealed class Fireflies : MonoBehaviour
    {
        const int Count = 46;

        /// <summary>0 = day (none), 1 = full night. Set each frame by the game.</summary>
        public static float Night;
        /// <summary>Where every lit firefly is, and how bright (for the light map).</summary>
        public static readonly List<Vector3> Lights = new List<Vector3>();

        readonly List<SpriteRenderer> cores = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> halos = new List<SpriteRenderer>();
        readonly List<Vector2> homes = new List<Vector2>();
        readonly List<float> seeds = new List<float>();
        float shown;
        static int lightsFrame = -1;

        public void Init(Rect area, int seed)
        {
            var rng = new System.Random(seed * 7919 + 13);
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);
            for (int i = 0; i < Count; i++)
            {
                var home = new Vector2(area.xMin + (float)rng.NextDouble() * area.width, area.yMin + (float)rng.NextDouble() * area.height);
                var halo = SpriteUtil.Make("FireflyGlow", transform, soft, new Color(0.75f, 1f, 0.45f, 0f), Sorting.Vfx - 10);
                halo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                var core = SpriteUtil.Make("Firefly", transform, soft, new Color(0.95f, 1f, 0.7f, 0f), Sorting.Vfx - 9);
                core.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
                homes.Add(home);
                seeds.Add((float)rng.NextDouble() * 100f);
                halos.Add(halo);
                cores.Add(core);
            }
        }

        void Update()
        {
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(Night), Time.deltaTime * 0.35f);
            float t = Time.time;
            if (lightsFrame != Time.frameCount)
            {
                lightsFrame = Time.frameCount;
                Lights.Clear();
            }
            for (int i = 0; i < cores.Count; i++)
            {
                float s = seeds[i];
                // A lazy wander around home, and a slow rise and fall.
                Vector2 drift = new Vector2(Mathf.PerlinNoise(s, t * 0.12f) - 0.5f, Mathf.PerlinNoise(t * 0.12f, s) - 0.5f) * 7f;
                float bob = 0.35f * Mathf.Sin(t * 0.9f + s);
                Vector3 p = homes[i] + drift + Vector2.up * bob;
                // Each blinks on its own rhythm: long glows, short dark.
                float pulse = Mathf.Clamp01(Mathf.Sin(t * (0.8f + 0.5f * Mathf.Repeat(s, 1f)) + s * 3f) * 1.4f + 0.2f);
                float a = shown * pulse;
                cores[i].transform.localPosition = p;
                halos[i].transform.localPosition = p;
                cores[i].color = new Color(0.95f, 1f, 0.7f, a);
                halos[i].color = new Color(0.7f, 1f, 0.4f, 0.35f * a);
                if (a > 0.05f) Lights.Add(new Vector3(transform.position.x + p.x, transform.position.y + p.y, a));
            }
        }

        void OnDisable() => Lights.Clear();
    }
}
