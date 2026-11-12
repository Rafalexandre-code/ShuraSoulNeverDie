using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The story told by the place itself (design doc §8.7): crows come down on the bodies
    /// when a room falls silent, foxes watch from the edges where the Kitsune serves her tea,
    /// and sakura petals fall harder around a duelist in Mushin. Purely visual; it lives on
    /// the arena and dies with it.
    /// </summary>
    public sealed class Wildlife : MonoBehaviour
    {
        sealed class Critter
        {
            public SpriteRenderer sr;
            public bool fox;
            public Vector2 pos;
            public Vector2 from;
            public Vector2 target;
            public float t;
            public float phase;
            /// <summary>0 arriving, 1 settled, 2 leaving.</summary>
            public int state;
            public float alpha = 1f;
        }

        sealed class Petal
        {
            public SpriteRenderer sr;
            public Vector2 pos;
            public Vector2 vel;
            public float spin;
            public float life;
        }

        World world;
        readonly List<Critter> critters = new List<Critter>();
        readonly List<Petal> petals = new List<Petal>();
        float petalTimer;
        int seed;

        static readonly Dictionary<char, Color> CrowPal = new Dictionary<char, Color>
        {
            { 'o', ColorUtil.Hex("#0E0B12") }, { 'k', ColorUtil.Hex("#2B2633") }, { 'h', ColorUtil.Hex("#4A4258") },
            { 'e', ColorUtil.Hex("#E8E0C0") }, { 'b', ColorUtil.Hex("#6A5F4A") },
        };

        static readonly Dictionary<char, Color> FoxPal = new Dictionary<char, Color>
        {
            { 'o', ColorUtil.Hex("#2A1810") }, { 'F', ColorUtil.Hex("#D9792B") }, { 'L', ColorUtil.Hex("#F2D7B0") },
            { 'W', ColorUtil.Hex("#FFF4E0") }, { 'e', ColorUtil.Hex("#1A1010") },
        };

        static readonly Dictionary<char, Color> PetalPal = new Dictionary<char, Color>
        {
            { 'p', ColorUtil.Hex("#FFC4D6") }, { 'P', ColorUtil.Hex("#FF8FB4") },
        };

        static Sprite CrowSitting => PixelArt.FromGrid("crow_sit", new[]
        {
            ".......oo...",
            "......okko..",
            ".....okkeoo.",
            ".....okkkkbb",
            "..oo.okkkko.",
            ".okhookkkko.",
            "okhhkkkkko..",
            ".ookkkkkoo..",
            "...o..o.....",
        }, CrowPal, new Vector2(0.5f, 0f));

        static Sprite CrowPecking => PixelArt.FromGrid("crow_peck", new[]
        {
            "............",
            "............",
            "..oo...oo...",
            ".okhoookko..",
            "okhhkkkkkeo.",
            ".ookkkkkkkbb",
            "...okkkkko..",
            "....ookoo...",
            "...o..o.....",
        }, CrowPal, new Vector2(0.5f, 0f));

        static Sprite CrowFlying => PixelArt.FromGrid("crow_fly", new[]
        {
            "ko..........ok",
            "okko......okko",
            ".okhko..okhko.",
            "..okkkookkko..",
            "....okkkkeobb.",
            ".....okkko....",
            "......oo......",
        }, CrowPal, new Vector2(0.5f, 0.5f));

        static Sprite FoxA => PixelArt.FromGrid("fox_a", new[]
        {
            ".........o.o.",
            "........oFoFo",
            "........oFFFo",
            ".......oFFeFFe",
            ".......oFLLLo.",
            "..oo...oFFFo..",
            ".oFFo.oFFFFo..",
            "oFWWFooFFFFo..",
            "oFWFFFFFFLLo..",
            ".ooFFFFFFLLo..",
            "...oFFoFFoo...",
            "...oo.oo......",
        }, FoxPal, new Vector2(0.5f, 0f));

        static Sprite FoxB => PixelArt.FromGrid("fox_b", new[]
        {
            ".........o.o.",
            "........oFoFo",
            "........oFFFo",
            ".......oFFeFFe",
            ".......oFLLLo.",
            ".......oFFFo..",
            ".oo...oFFFFo..",
            "oWWoooFFFFFo..",
            "oWFFFFFFFLLo..",
            ".ooFFFFFFLLo..",
            "...oFFoFFoo...",
            "...oo.oo......",
        }, FoxPal, new Vector2(0.5f, 0f));

        static Sprite PetalSprite => PixelArt.FromGrid("petal", new[] { "pP.", "PpP", ".Pp" }, PetalPal, new Vector2(0.5f, 0.5f));

        public static Wildlife Attach(Transform arena, World w, int seed)
        {
            var wl = arena.gameObject.AddComponent<Wildlife>();
            wl.world = w;
            wl.seed = seed;
            return wl;
        }

        float Rand(int i) => Mathf.Repeat(Mathf.Sin((seed + i * 12.9898f) * 78.233f) * 43758.547f, 1f);

        /// <summary>The room fell silent: up to three crows come for the dead.</summary>
        public void CallCrows(List<Vector2> bodies, Rect view)
        {
            if (bodies == null || bodies.Count == 0) return;
            int n = Mathf.Min(3, bodies.Count);
            for (int i = 0; i < n; i++)
            {
                Vector2 spot = bodies[(int)(Rand(i) * bodies.Count) % bodies.Count] + new Vector2(Rand(i + 7) - 0.5f, Rand(i + 9) - 0.5f) * 0.8f;
                bool left = Rand(i + 3) < 0.5f;
                var c = new Critter
                {
                    sr = SpriteUtil.Make("Crow", transform, CrowFlying, Color.white, Sorting.Overlay - 20),
                    target = spot,
                    from = new Vector2(left ? view.xMin - 2f : view.xMax + 2f, view.yMax + 1f + Rand(i + 11) * 3f),
                    phase = Rand(i + 5) * 6f,
                    t = -0.5f * i,
                };
                c.pos = c.from;
                c.sr.flipX = !left;
                critters.Add(c);
            }
        }

        /// <summary>Foxes sit at the edges of the Kitsune's tea, watching.</summary>
        public void Foxes(Rect floor)
        {
            Vector2[] spots =
            {
                new Vector2(floor.xMin + 1.2f, floor.yMax - 1.4f),
                new Vector2(floor.xMax - 1.4f, floor.yMax - 1.2f),
                new Vector2(floor.xMin + 1.6f, floor.yMin + 1.4f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var c = new Critter
                {
                    fox = true,
                    sr = SpriteUtil.Make("Fox", transform, FoxA, Color.white, -Mathf.RoundToInt(spots[i].y * 20f)),
                    pos = spots[i],
                    target = spots[i],
                    state = 1,
                    phase = Rand(i + 21) * 6f,
                };
                c.sr.flipX = spots[i].x > floor.center.x;
                c.sr.transform.position = c.pos;
                critters.Add(c);
            }
        }

        bool PlayerNear(Vector2 p, float r)
        {
            foreach (PlayerController q in world.Players)
            {
                if (q.IsAlive && (q.RenderPosition - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        void Update()
        {
            if (world == null) return;
            float dt = Time.deltaTime;
            for (int i = critters.Count - 1; i >= 0; i--)
            {
                Critter c = critters[i];
                c.phase += dt;
                if (c.fox) TickFox(c, dt);
                else TickCrow(c, dt);
                if (c.alpha <= 0f)
                {
                    Destroy(c.sr.gameObject);
                    critters.RemoveAt(i);
                }
            }
            TickPetals(dt);
        }

        void TickCrow(Critter c, float dt)
        {
            switch (c.state)
            {
                case 0:
                    c.t += dt * 0.45f;
                    if (c.t < 0f) return;
                    float k = Mathf.Clamp01(c.t);
                    // A long glide down, with a last flap before landing.
                    c.pos = Vector2.Lerp(c.from, c.target, 1f - (1f - k) * (1f - k)) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 1.4f;
                    c.sr.sprite = CrowFlying;
                    c.sr.transform.localScale = new Vector3(1f, Mathf.Sin(c.phase * 14f) > 0f ? 1f : 0.7f, 1f);
                    if (k >= 1f)
                    {
                        c.state = 1;
                        c.sr.transform.localScale = Vector3.one;
                    }
                    break;
                case 1:
                    // Hops and pecks; anyone close sends it back up.
                    c.sr.sprite = Mathf.Repeat(c.phase, 1.6f) < 0.35f ? CrowPecking : CrowSitting;
                    if (Mathf.Repeat(c.phase, 3.1f) < dt) c.sr.flipX = !c.sr.flipX;
                    if (PlayerNear(c.pos, 2.4f))
                    {
                        c.state = 2;
                        c.from = c.pos;
                        c.target = c.pos + new Vector2(c.sr.flipX ? -9f : 9f, 7f);
                        c.t = 0f;
                    }
                    break;
                default:
                    c.t += dt * 0.8f;
                    c.pos = Vector2.Lerp(c.from, c.target, c.t);
                    c.sr.sprite = CrowFlying;
                    c.sr.transform.localScale = new Vector3(1f, Mathf.Sin(c.phase * 18f) > 0f ? 1f : 0.65f, 1f);
                    c.alpha = 1f - Mathf.Clamp01((c.t - 0.6f) / 0.4f);
                    break;
            }
            c.sr.transform.position = c.pos;
            c.sr.sortingOrder = c.state == 1 ? -Mathf.RoundToInt(c.pos.y * 20f) : Sorting.Overlay - 20;
            c.sr.color = new Color(1f, 1f, 1f, c.alpha);
        }

        void TickFox(Critter c, float dt)
        {
            if (c.state == 1)
            {
                c.sr.sprite = Mathf.Repeat(c.phase, 1.2f) < 0.6f ? FoxA : FoxB;
                if (PlayerNear(c.pos, 3f))
                {
                    c.state = 2;
                    c.from = c.pos;
                    c.target = c.pos + new Vector2(c.sr.flipX ? 6f : -6f, 0.5f);
                    c.t = 0f;
                }
            }
            else
            {
                // It slips away, low and quick, and is gone.
                c.t += dt * 1.6f;
                c.pos = Vector2.Lerp(c.from, c.target, c.t);
                c.sr.flipX = c.target.x < c.from.x;
                c.alpha = 1f - Mathf.Clamp01((c.t - 0.3f) / 0.5f);
            }
            c.sr.transform.position = c.pos;
            c.sr.color = new Color(1f, 1f, 1f, c.alpha);
        }

        void TickPetals(float dt)
        {
            PlayerController host = world.Host;
            bool mushin = host != null && host.IsAlive && host.InMushin;
            if (mushin)
            {
                petalTimer += dt * 22f;
                while (petalTimer >= 1f && petals.Count < 90)
                {
                    petalTimer -= 1f;
                    float a = Random.value * Mathf.PI * 2f;
                    var p = new Petal
                    {
                        sr = SpriteUtil.Make("Petal", transform, PetalSprite, Color.white, Sorting.Overlay - 15),
                        pos = host.RenderPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(1.5f, 7f) + Vector2.up * 3f,
                        vel = new Vector2(Random.Range(-0.6f, 0.9f), Random.Range(-1.6f, -0.8f)),
                        spin = Random.Range(-240f, 240f),
                    };
                    petals.Add(p);
                }
            }
            else petalTimer = 0f;
            for (int i = petals.Count - 1; i >= 0; i--)
            {
                Petal p = petals[i];
                p.life += dt;
                p.pos += (p.vel + new Vector2(Mathf.Sin(p.life * 3f + i) * 0.7f, 0f)) * dt;
                p.sr.transform.position = p.pos;
                p.sr.transform.rotation = Quaternion.Euler(0f, 0f, p.spin * p.life);
                p.sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(2.6f - p.life));
                if (p.life >= 2.6f)
                {
                    Destroy(p.sr.gameObject);
                    petals.RemoveAt(i);
                }
            }
        }
    }
}
