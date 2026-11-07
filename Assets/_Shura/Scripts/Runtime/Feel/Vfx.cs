using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Pooled sprite particles. Runs on unscaled time so sparks keep flying during hit-stop,
    /// which sells the impact. Cosmetic randomness uses UnityEngine.Random (never the run RNG).
    /// </summary>
    public sealed class Vfx : MonoBehaviour
    {
        const int Capacity = 640;
        const int DecalCapacity = 700;

        struct Particle
        {
            public bool alive;
            public bool stretch;
            /// <summary>Fades in and out (sine) instead of only out.</summary>
            public bool pulse;
            /// <summary>Blood: leaves a stain on the floor where it lands.</summary>
            public bool stains;
            public float stainSize;
            public Color stainColor;
            public Vector2 pos;
            public Vector2 vel;
            public float life;
            public float maxLife;
            public float size0;
            public float size1;
            public float aspect;
            public float drag;
            public float rot;
            public float rotVel;
            public Color c0;
            public Color c1;
        }

        struct Decal
        {
            public bool alive;
            public float age;
            public float growTime;
            public Vector3 targetScale;
        }

        SpriteRenderer[] renderers;
        Particle[] parts;
        int cursor;
        SpriteRenderer[] decals;
        Decal[] decalData;
        int decalCursor;

        /// <summary>Player setting: when off, blood becomes sparks and nothing stains the floor.</summary>
        public bool BloodEnabled { get; set; } = true;
        /// <summary>Stains are kept inside the arena: blood that flies past a wall pools at its edge.</summary>
        public Rect StainBounds { get; set; } = new Rect(-1000f, -1000f, 2000f, 2000f);
        Sprite pixel, pixelLeft, soft, ring, circle, star;

        public bool Paused { get; set; }

        public void Init()
        {
            renderers = new SpriteRenderer[Capacity];
            parts = new Particle[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                renderers[i] = SpriteUtil.Make("Particle", transform, null, Color.white, Sorting.Vfx);
                renderers[i].enabled = false;
            }
            pixel = ShapeLibrary.Get(Shape.Pixel);
            pixelLeft = ShapeLibrary.Get(Shape.PixelLeft);
            soft = ShapeLibrary.Get(Shape.SoftCircle);
            ring = ShapeLibrary.Get(Shape.Ring);
            circle = ShapeLibrary.Get(Shape.Circle);
            star = ShapeLibrary.Get(Shape.Star);

            var decalRoot = new GameObject("Decals").transform;
            decalRoot.SetParent(transform, false);
            decals = new SpriteRenderer[DecalCapacity];
            decalData = new Decal[DecalCapacity];
            for (int i = 0; i < DecalCapacity; i++)
            {
                decals[i] = SpriteUtil.Make("Stain", decalRoot, ShapeLibrary.Splat(i), Color.white, Sorting.Floor + 6);
                decals[i].enabled = false;
            }
        }

        // ── Blood ───────────────────────────────────────────────────────────────────────

        /// <summary>A spray of droplets along dir; each one stains the floor where it lands.</summary>
        public void Blood(Vector2 pos, Vector2 dir, int count, float speed, float spreadDegrees, Color color)
        {
            if (!BloodEnabled)
            {
                Sparks(pos, dir, Mathf.Max(2, count / 3), Color.white, speed * 0.7f, spreadDegrees);
                return;
            }
            float baseAngle = dir.sqrMagnitude > 1e-6f ? CombatMath.AngleDegrees(dir) : Random.Range(0f, 360f);
            Color dark = Color.Lerp(color, Color.black, 0.35f);
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + Random.Range(-spreadDegrees, spreadDegrees)) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.3f, 1.2f);
                float size = Random.Range(0.1f, 0.24f);
                int idx = Spawn(pixel, pos + Random.insideUnitCircle * 0.1f, v, Random.Range(0.2f, 0.5f), size, size * 0.5f, color, dark, 3.5f, true);
                parts[idx].stains = true;
                parts[idx].stainSize = Random.Range(0.12f, 0.34f) * Mathf.Lerp(0.7f, 1.3f, size / 0.24f);
                parts[idx].stainColor = dark.WithAlpha(0.85f);
            }
        }

        /// <summary>A pool that spreads on the floor over a moment.</summary>
        public void BloodPool(Vector2 pos, float size, Color color)
        {
            if (!BloodEnabled) return;
            AddDecal(pos, size, Color.Lerp(color, Color.black, 0.45f).WithAlpha(0.9f), 0.9f, 1f, Random.Range(0f, 360f));
        }

        /// <summary>An elongated smear in a direction (dragged bodies, flung blood).</summary>
        public void BloodStreak(Vector2 pos, Vector2 dir, float length, Color color)
        {
            if (!BloodEnabled) return;
            AddDecal(pos + dir * length * 0.5f, length, Color.Lerp(color, Color.black, 0.4f).WithAlpha(0.85f), 0.2f, 0.28f, CombatMath.AngleDegrees(dir));
        }

        /// <summary>A piece of a slain body: tumbles away and leaves a stain where it stops.</summary>
        public void Gib(Sprite sprite, Vector2 pos, Vector2 velocity, float size, float aspect, Color color, Color blood)
        {
            int idx = Spawn(sprite, pos, velocity, Random.Range(0.8f, 1.1f), size, size * 0.9f, color, Color.Lerp(color, Color.black, 0.5f),
                            2.6f, false, Random.Range(0f, 360f), Random.Range(-540f, 540f), aspect);
            if (!BloodEnabled) return;
            parts[idx].stains = true;
            parts[idx].stainSize = size * 1.2f;
            parts[idx].stainColor = Color.Lerp(blood, Color.black, 0.4f).WithAlpha(0.85f);
        }

        public void ClearDecals()
        {
            if (decals == null) return;
            for (int i = 0; i < DecalCapacity; i++)
            {
                decalData[i].alive = false;
                decals[i].enabled = false;
            }
        }

        void AddDecal(Vector2 pos, float size, Color color, float growTime, float aspect, float rotation)
        {
            pos = new Vector2(Mathf.Clamp(pos.x, StainBounds.xMin, StainBounds.xMax), Mathf.Clamp(pos.y, StainBounds.yMin, StainBounds.yMax));
            int i = decalCursor;
            decalCursor = (decalCursor + 1) % DecalCapacity;
            SpriteRenderer r = decals[i];
            r.enabled = true;
            r.color = color;
            r.transform.position = new Vector3(pos.x, pos.y, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            decalData[i] = new Decal
            {
                alive = true,
                age = 0f,
                growTime = Mathf.Max(0.01f, growTime),
                targetScale = new Vector3(size, size * aspect, 1f),
            };
            r.transform.localScale = decalData[i].targetScale * 0.2f;
        }

        /// <summary>The flash where two blades meet: two crossed sparkles that burst and fade.</summary>
        public void ClashStar(Vector2 pos, float size, Color color)
        {
            float rot = Random.Range(0f, 45f);
            Spawn(star, pos, Vector2.zero, 0.2f, size * 0.4f, size, Color.white, color.WithAlpha(0f), 0f, false, rot);
            Spawn(star, pos, Vector2.zero, 0.14f, size * 0.3f, size * 0.7f, Color.white, Color.white.WithAlpha(0f), 0f, false, rot + 45f);
            Spawn(soft, pos, Vector2.zero, 0.16f, size * 0.5f, size * 1.4f, color.WithAlpha(0.8f), color.WithAlpha(0f));
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
            {
                parts[i].alive = false;
                renderers[i].enabled = false;
            }
        }

        // ── Effects ─────────────────────────────────────────────────────────────────────

        public void Sparks(Vector2 pos, Vector2 dir, int count, Color color, float speed, float spreadDegrees = 55f)
        {
            float baseAngle = dir.sqrMagnitude > 1e-6f ? CombatMath.AngleDegrees(dir) : Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + Random.Range(-spreadDegrees, spreadDegrees)) * Mathf.Deg2Rad;
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.5f, 1.2f);
                Spawn(pixel, pos, v, Random.Range(0.16f, 0.34f), 0.17f, 0.04f, color, color.WithAlpha(0f), 6f, true);
            }
        }

        public void Ring(Vector2 pos, float fromRadius, float toRadius, Color color, float life)
        {
            Spawn(ring, pos, Vector2.zero, life, fromRadius * 2f, toRadius * 2f, color, color.WithAlpha(0f));
        }

        public void Burst(Vector2 pos, int count, Color color, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.3f, 1f);
                float size = Random.Range(0.12f, 0.26f);
                Spawn(pixel, pos, v, Random.Range(0.3f, 0.6f), size, 0f, color, color.WithAlpha(0f), 3f, false,
                      Random.Range(0f, 360f), Random.Range(-360f, 360f));
            }
        }

        public void Dust(Vector2 pos, Vector2 dir)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 v = -dir * Random.Range(0.5f, 2f) + Random.insideUnitCircle * 0.8f;
                var c = new Color(0.75f, 0.72f, 0.8f, 0.35f);
                Spawn(soft, pos + Random.insideUnitCircle * 0.15f, v, Random.Range(0.25f, 0.45f), 0.35f, 0.8f, c, c.WithAlpha(0f), 4f);
            }
        }

        public void Footstep(Vector2 pos, Vector2 dir)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 v = dir * Random.Range(0.3f, 0.8f) + Random.insideUnitCircle * 0.3f;
                var c = new Color(0.7f, 0.66f, 0.75f, 0.22f);
                Spawn(soft, pos + Random.insideUnitCircle * 0.08f, v, Random.Range(0.3f, 0.5f), 0.22f, 0.5f, c, c.WithAlpha(0f), 3f);
            }
        }

        /// <summary>Motes drifting upward: used for spawning (materialize) and dying (dissolve).</summary>
        public void Rising(Vector2 pos, float radius, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 start = pos + Random.insideUnitCircle * radius;
                var v = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2.6f));
                Spawn(pixel, start, v, Random.Range(0.5f, 0.9f), Random.Range(0.08f, 0.16f), 0f,
                      Color.Lerp(color, Color.white, 0.4f), color.WithAlpha(0f), 1f);
            }
        }

        /// <summary>Ambient ash/ember drifting through the arena.</summary>
        public void Ember(Vector2 pos, Color color)
        {
            var v = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.25f, 0.8f));
            float size = Random.Range(0.04f, 0.09f);
            Color dim = Color.Lerp(color, new Color(0.5f, 0.2f, 0.1f), 0.5f);
            Spawn(pixel, pos, v, Random.Range(2.5f, 4.5f), size, size * 0.4f, color, dim,
                  0f, false, Random.Range(0f, 360f), Random.Range(-90f, 90f), 1f, true);
        }

        public void Afterimage(Vector2 pos, float radius, Color color)
        {
            Spawn(circle, pos, Vector2.zero, 0.22f, radius * 2f, radius * 1.8f, color.WithAlpha(0.45f), color.WithAlpha(0f));
        }

        public void Line(Vector2 origin, Vector2 dir, float length, float width, Color color, float life)
        {
            float angle = CombatMath.AngleDegrees(dir);
            Spawn(pixelLeft, origin, Vector2.zero, life, width, width * 0.2f, color.WithAlpha(0.9f), color.WithAlpha(0f),
                  0f, false, angle, 0f, length / Mathf.Max(0.01f, width));
        }

        public void Stream(Vector2 from, Vector2 to, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float life = Random.Range(0.35f, 0.7f);
                Vector2 start = from + Random.insideUnitCircle * 0.5f;
                Vector2 v = (to - start) / life + Random.insideUnitCircle * 2f;
                Spawn(pixel, start, v, life, 0.16f, 0.05f, color, Color.white.WithAlpha(0f), 0f, true);
            }
        }

        // ── Pool ────────────────────────────────────────────────────────────────────────

        int Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1,
                  float drag = 0f, bool stretch = false, float rot = 0f, float rotVel = 0f, float aspect = 1f, bool pulse = false)
        {
            int i = Alloc();
            SpriteRenderer r = renderers[i];
            r.sprite = sprite;
            r.enabled = true;
            parts[i] = new Particle
            {
                alive = true,
                stretch = stretch,
                pulse = pulse,
                pos = pos,
                vel = vel,
                life = life,
                maxLife = life,
                size0 = size0,
                size1 = size1,
                aspect = aspect,
                drag = drag,
                rot = rot,
                rotVel = rotVel,
                c0 = c0,
                c1 = c1,
            };
            Apply(i, 0f);
            return i;
        }

        int Alloc()
        {
            for (int n = 0; n < Capacity; n++)
            {
                int i = (cursor + n) % Capacity;
                if (parts[i].alive) continue;
                cursor = (i + 1) % Capacity;
                return i;
            }
            // Pool exhausted: recycle the oldest slot rather than allocate.
            int recycled = cursor;
            cursor = (cursor + 1) % Capacity;
            return recycled;
        }

        void Update()
        {
            if (parts == null) return;
            float dt = Paused ? 0f : Time.unscaledDeltaTime;
            for (int i = 0; i < DecalCapacity; i++)
            {
                if (!decalData[i].alive || decalData[i].age >= decalData[i].growTime) continue;
                decalData[i].age += dt;
                float k = CombatMath.EaseOutCubic(decalData[i].age / decalData[i].growTime);
                decals[i].transform.localScale = decalData[i].targetScale * Mathf.Lerp(0.2f, 1f, k);
            }
            for (int i = 0; i < Capacity; i++)
            {
                if (!parts[i].alive) continue;
                ref Particle p = ref parts[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.alive = false;
                    renderers[i].enabled = false;
                    if (p.stains && BloodEnabled) AddDecal(p.pos, p.stainSize, p.stainColor, 0.12f, 1f, Random.Range(0f, 360f));
                    continue;
                }
                p.pos += p.vel * dt;
                if (p.drag > 0f) p.vel *= 1f / (1f + p.drag * dt);
                p.rot += p.rotVel * dt;
                Apply(i, 1f - p.life / p.maxLife);
            }
        }

        void Apply(int i, float t)
        {
            ref Particle p = ref parts[i];
            Transform tr = renderers[i].transform;
            float size = Mathf.Lerp(p.size0, p.size1, t);
            tr.position = new Vector3(p.pos.x, p.pos.y, 0f);
            if (p.stretch && p.vel.sqrMagnitude > 1e-4f)
            {
                float speed = p.vel.magnitude;
                tr.rotation = Quaternion.Euler(0f, 0f, CombatMath.AngleDegrees(p.vel));
                tr.localScale = new Vector3(size * (1f + speed * 0.06f), size * 0.35f, 1f);
            }
            else
            {
                tr.rotation = Quaternion.Euler(0f, 0f, p.rot);
                tr.localScale = new Vector3(size * p.aspect, size, 1f);
            }
            Color c = Color.Lerp(p.c0, p.c1, t);
            if (p.pulse) c.a *= Mathf.Sin(t * Mathf.PI);
            renderers[i].color = c;
        }
    }
}
