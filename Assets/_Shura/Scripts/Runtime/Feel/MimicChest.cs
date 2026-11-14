using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// O baú amaldiçoado é um mímico vivo. Base: "Chest Mimic" de joraxus (itch.io; uso comercial
    /// e modificação livres), recolorido em roxo, com a fechadura redesenhada como um olho de
    /// fenda e um quadro de piscar (Tools/art_mimic/pack.py). Por cima, desenhado aqui: sombra,
    /// aura que pulsa, fiapos de maldição subindo da boca, o brilho do olho e a maldição — um
    /// crânio em chamas roxas que a criatura cospe no jogador ao ser aberta.
    /// Tudo é só visual (relógio de tela); a regra (item e maldição) é aplicada pela sala.
    /// </summary>
    public sealed class MimicChest : MonoBehaviour
    {
        // A folha: células de 36×30 px, o corpo encostado na coluna 31 e no chão da célula.
        const int CellW = 36, CellH = 30;
        const float PivotX = 20.5f / CellW;
        static readonly int[] RowFrames = { 4, 8, 4, 9, 6, 4 };
        const int RowRest = 0, RowLick = 1, RowChew = 2, RowAttack = 3, RowBlink = 5;
        /// <summary>Quadro do bote em que a boca está mais aberta: a maldição sai aqui.</summary>
        const int SpitFrame = 3;
        /// <summary>Tempo de voo da maldição até o jogador, em segundos.</summary>
        const float BoltSeconds = 0.42f;
        /// <summary>O mímico é desenhado 25% maior que o pixel do mundo: uma criatura, não um móvel.</summary>
        const float BodyScale = 1.25f;

        static Sprite[][] frames;
        /// <summary>Quadro de tela do último bote (o tour de capturas fotografa a sequência).</summary>
        public static int LastDevourFrame { get; private set; }
        static readonly Color Aura = ColorUtil.Hex("#9B4DFF");
        static readonly Color EyeGlow = ColorUtil.Hex("#C4FF5C");
        static readonly Color CurseCore = ColorUtil.Hex("#E2B8FF");

        enum Mode { Rest, Fidget, Blink, Devour, Sated }

        SpriteRenderer body, aura, eye, shadow;
        readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly List<Fade> fades = new List<Fade>();
        readonly Queue<(Func<Vector2> target, Action hit)> pending = new Queue<(Func<Vector2>, Action)>();
        System.Random rng;
        Mode mode;
        int row, frame;
        float clock, frameTime, nextFidget, sated;
        bool facingRight, spat;
        Func<Vector2> target;
        Action onHit;

        sealed class Bolt
        {
            public SpriteRenderer sprite, glow;
            public Vector2 from;
            public Func<Vector2> to;
            public Action hit;
            public float t, trail;
        }

        sealed class Fade
        {
            public SpriteRenderer sprite;
            public float t, life, grow, alpha;
            public Vector2 drift;
        }

        static bool Load()
        {
            if (frames != null) return frames.Length > 0;
            var tex = Resources.Load<Texture2D>("Art/Extra/mimic_purple");
            if (tex == null)
            {
                frames = new Sprite[0][];
                return false;
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            frames = new Sprite[RowFrames.Length][];
            for (int r = 0; r < RowFrames.Length; r++)
            {
                frames[r] = new Sprite[RowFrames[r]];
                for (int i = 0; i < RowFrames[r]; i++)
                {
                    var rect = new Rect(i * CellW, tex.height - (r + 1) * CellH, CellW, CellH);
                    frames[r][i] = Sprite.Create(tex, rect, new Vector2(PivotX, 0f), PixelArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                }
            }
            return true;
        }

        /// <summary>Monta a criatura no lugar da arte da oferenda.</summary>
        public static MimicChest Dress(Transform parent, int order)
        {
            if (!Load()) return null;
            var m = parent.gameObject.AddComponent<MimicChest>();
            m.Build(order);
            return m;
        }

        void Build(int order)
        {
            rng = new System.Random(Mathf.RoundToInt(transform.position.x * 97f + transform.position.y * 31f));
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);
            shadow = SpriteUtil.Make("Sombra", transform, soft, Color.black.WithAlpha(0.45f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, -0.38f, 0f);
            shadow.transform.localScale = new Vector3(2.1f, 0.6f, 1f);
            aura = SpriteUtil.Make("Aura", transform, soft, Aura.WithAlpha(0.22f), Sorting.Ground + 3);
            aura.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            aura.transform.localScale = new Vector3(2.8f, 1.25f, 1f);
            body = SpriteUtil.Make("Mimico", transform, frames[RowRest][0], Color.white, order);
            body.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            eye = SpriteUtil.Make("Olho", transform, soft, EyeGlow.WithAlpha(0.35f), order + 1);
            eye.transform.localScale = Vector3.one * 0.38f;
            for (int i = 0; i < 3; i++)
            {
                SpriteRenderer w = SpriteUtil.Make("Fiapo", transform, soft, Aura.WithAlpha(0f), order + 1);
                w.transform.localScale = Vector3.one * 0.18f;
                wisps.Add(w);
            }
            nextFidget = 1.5f + (float)rng.NextDouble() * 2f;
            Play(RowRest, Mode.Rest);
        }

        /// <summary>
        /// Abrir o baú: a criatura vira para o jogador, abre a bocarra e cospe a maldição.
        /// <paramref name="hit"/> corre quando ela chega (o aviso e o efeito da maldição).
        /// </summary>
        public void Devour(Func<Vector2> who, Action hit)
        {
            if (mode == Mode.Devour)
            {
                pending.Enqueue((who, hit));
                return;
            }
            target = who;
            onHit = hit;
            spat = false;
            LastDevourFrame = Time.frameCount;
            facingRight = who().x > transform.position.x;
            Play(RowAttack, Mode.Devour);
        }

        void Play(int r, Mode m)
        {
            row = r;
            mode = m;
            frame = 0;
            frameTime = 0f;
            body.sprite = frames[row][0];
        }

        float FrameSeconds()
        {
            switch (mode)
            {
                case Mode.Devour: return frame == SpitFrame ? 0.16f : 0.07f;
                case Mode.Fidget: return 0.09f;
                case Mode.Blink: return 0.05f;
                case Mode.Sated: return 0.32f;
                default: return 0.2f;
            }
        }

        void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime;
            clock += dt;
            Animate(dt);
            Breathe();
            TickWisps();
            TickBolts(dt);
            TickFades(dt);
        }

        void Animate(float dt)
        {
            frameTime += dt;
            while (frameTime >= FrameSeconds())
            {
                frameTime -= FrameSeconds();
                frame++;
                if (mode == Mode.Devour && frame == SpitFrame && !spat) Spit();
                if (frame < frames[row].Length) continue;
                frame = 0;
                switch (mode)
                {
                    case Mode.Devour:
                        if (!spat) Spit();
                        if (pending.Count > 0)
                        {
                            (Func<Vector2> who, Action hit) = pending.Dequeue();
                            Devour(who, hit);
                            return;
                        }
                        sated = 3f;
                        Play(RowRest, Mode.Sated);
                        return;
                    case Mode.Fidget:
                    case Mode.Blink:
                        Play(RowRest, Mode.Rest);
                        return;
                }
            }
            body.sprite = frames[row][frame];
            body.flipX = facingRight;

            if (mode == Mode.Sated)
            {
                sated -= dt;
                if (sated <= 0f) Play(RowRest, Mode.Rest);
            }
            if (mode != Mode.Rest) return;
            nextFidget -= dt;
            if (nextFidget > 0f) return;
            double roll = rng.NextDouble();
            if (roll < 0.45) Play(RowBlink, Mode.Blink);
            else if (roll < 0.75) Play(RowLick, Mode.Fidget);
            else Play(RowChew, Mode.Fidget);
            // Às vezes olha para o outro lado.
            if (rng.NextDouble() < 0.2) facingRight = !facingRight;
            nextFidget = 1.6f + (float)rng.NextDouble() * 2.6f;
        }

        /// <summary>A respiração, a aura que pulsa e o brilho do olho (só com o baú fechado).</summary>
        void Breathe()
        {
            bool calm = mode == Mode.Rest || mode == Mode.Sated;
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * (mode == Mode.Sated ? 1.4f : 2.6f));
            float swell = calm ? 1f + 0.035f * pulse : 1f;
            body.transform.localScale = new Vector3(BodyScale / Mathf.Sqrt(swell), BodyScale * swell, 1f);
            float auraAlpha = mode == Mode.Devour ? 0.6f : mode == Mode.Sated ? 0.14f : 0.26f + 0.14f * pulse;
            aura.color = Aura.WithAlpha(auraAlpha);
            aura.transform.localScale = new Vector3(2.7f + 0.3f * pulse, 1.2f + 0.12f * pulse, 1f);
            bool eyeOpen = calm || mode == Mode.Fidget;
            // O olho: 6 px da borda esquerda do corpo, 10 px acima do chão (espelha com o corpo).
            float ex = (facingRight ? 1f : -1f) * 4.5f / PixelArt.PixelsPerUnit;
            eye.transform.localPosition = new Vector3(ex * BodyScale, -0.45f + 10f / PixelArt.PixelsPerUnit * swell * BodyScale, 0f);
            float flicker = 0.75f + 0.25f * Mathf.Sin(clock * 11f) * Mathf.Sin(clock * 3.7f);
            eye.color = EyeGlow.WithAlpha(eyeOpen ? (mode == Mode.Sated ? 0.12f : 0.32f) * flicker : 0f);
        }

        /// <summary>Três fiapos de maldição sobem da fresta da boca e se desfazem.</summary>
        void TickWisps()
        {
            for (int i = 0; i < wisps.Count; i++)
            {
                float speed = mode == Mode.Devour ? 1.6f : 0.55f;
                float t = Mathf.Repeat(clock * speed + i / (float)wisps.Count, 1f);
                float sway = Mathf.Sin((clock + i * 1.7f) * 3f) * 0.12f;
                float x = (i - 1) * 0.32f + sway;
                wisps[i].transform.localPosition = new Vector3(x, 0.2f + t * 1.1f, 0f);
                wisps[i].transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.07f, t);
                float a = mode == Mode.Sated ? 0f : Mathf.Sin(t * Mathf.PI) * 0.55f;
                wisps[i].color = Color.Lerp(Aura, CurseCore, 1f - t).WithAlpha(a);
            }
        }

        /// <summary>A maldição sai da boca aberta.</summary>
        void Spit()
        {
            spat = true;
            var mouth = (Vector2)transform.position + new Vector2((facingRight ? 1f : -1f) * 0.6f, 0.3f);
            var b = new Bolt { from = mouth, to = target, hit = onHit };
            b.glow = SpriteUtil.Make("BrilhoMaldicao", transform, ShapeLibrary.Get(Shape.SoftCircle), Aura.WithAlpha(0.6f), Sorting.Vfx);
            b.glow.transform.localScale = Vector3.one * 0.9f;
            b.sprite = SpriteUtil.Make("Maldicao", transform, CurseSprite(0), Color.white, Sorting.Vfx + 1);
            b.sprite.transform.position = mouth;
            bolts.Add(b);
            Burst(mouth, 0.3f, 1.6f, Aura.WithAlpha(0.7f));
            target = null;
            onHit = null;
        }

        void TickBolts(float dt)
        {
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = bolts[i];
                b.t += dt / BoltSeconds;
                Vector2 end = b.to != null ? b.to() + Vector2.up * 0.5f : b.from;
                float t = Mathf.Clamp01(b.t);
                // Um arco: sobe e cai no jogador, acelerando no fim.
                float e = t * t * (3f - 2f * t);
                Vector2 mid = (b.from + end) * 0.5f + Vector2.up * (1.2f + 0.15f * Vector2.Distance(b.from, end));
                Vector2 p = Vector2.Lerp(Vector2.Lerp(b.from, mid, e), Vector2.Lerp(mid, end, e), e);
                b.sprite.transform.position = p;
                b.sprite.sprite = CurseSprite(Mathf.FloorToInt(Time.time * 14f) % 3);
                b.sprite.flipX = end.x < b.from.x;
                b.glow.transform.position = p;
                b.glow.color = Aura.WithAlpha(0.45f + 0.2f * Mathf.Sin(Time.time * 30f));
                b.trail -= dt;
                if (b.trail <= 0f)
                {
                    b.trail = 0.025f;
                    Puff(p, 0.35f, 0.32f, Color.Lerp(Aura, CurseCore, 0.3f).WithAlpha(0.55f));
                }
                if (b.t < 1f) continue;
                Burst(end, 0.45f, 2.6f, Aura.WithAlpha(0.85f));
                Burst(end, 0.3f, 1.4f, CurseCore.WithAlpha(0.8f));
                b.hit?.Invoke();
                Destroy(b.sprite.gameObject);
                Destroy(b.glow.gameObject);
                bolts.RemoveAt(i);
            }
        }

        /// <summary>Um anel que se abre e some.</summary>
        void Burst(Vector2 at, float life, float grow, Color c)
        {
            SpriteRenderer s = SpriteUtil.Make("Estouro", transform, ShapeLibrary.Get(Shape.Ring), c, Sorting.Vfx);
            s.transform.position = at;
            s.transform.localScale = Vector3.one * 0.3f;
            fades.Add(new Fade { sprite = s, life = life, grow = grow, alpha = c.a });
        }

        /// <summary>Um tufo de fumaça do rastro.</summary>
        void Puff(Vector2 at, float life, float size, Color c)
        {
            SpriteRenderer s = SpriteUtil.Make("Rastro", transform, ShapeLibrary.Get(Shape.SoftCircle), c, Sorting.Vfx - 1);
            s.transform.position = at;
            s.transform.localScale = Vector3.one * size;
            fades.Add(new Fade { sprite = s, life = life, grow = -size * 0.6f, drift = new Vector2(0f, 0.4f), alpha = c.a });
        }

        void TickFades(float dt)
        {
            for (int i = fades.Count - 1; i >= 0; i--)
            {
                Fade f = fades[i];
                f.t += dt;
                float k = f.t / f.life;
                if (k >= 1f)
                {
                    Destroy(f.sprite.gameObject);
                    fades.RemoveAt(i);
                    continue;
                }
                Transform tr = f.sprite.transform;
                tr.localScale += Vector3.one * (f.grow * dt / f.life);
                tr.position += (Vector3)(f.drift * dt);
                Color c = f.sprite.color;
                c.a = f.alpha * (1f - k);
                f.sprite.color = c;
            }
        }

        // ── A maldição: um crânio em chamas roxas (desenhado para o jogo) ─────────────────

        static readonly Dictionary<char, Color> CursePalette = new Dictionary<char, Color>
        {
            { 'a', ColorUtil.Hex("#5A1E9E") },
            { 'b', ColorUtil.Hex("#9B4DFF") },
            { 'c', ColorUtil.Hex("#D9A8FF") },
            { 'W', ColorUtil.Hex("#F4ECFF") },
            { 'K', ColorUtil.Hex("#1C0629") },
            { 'G', ColorUtil.Hex("#C4FF5C") },
        };

        static readonly string[][] CurseFrames =
        {
            new[]
            {
                "....a......",
                "...ab..a...",
                "...abb.ab..",
                "..abcbbcb..",
                "..bcWWWcba.",
                ".abWWWWWba.",
                ".bWKGWKGWb.",
                ".bWWWKWWWb.",
                "..bWKWKWb..",
                "..abWWWba..",
                "...abbba...",
                "....aaa....",
            },
            new[]
            {
                "......a....",
                "..a..ba....",
                "..ba.bba...",
                "..bcbbcba..",
                ".abcWWWcb..",
                ".abWWWWWba.",
                ".bWKGWKGWb.",
                ".bWWWKWWWb.",
                "..bWKWKWb..",
                "..abWWWba..",
                "...abbba...",
                "....aaa....",
            },
            new[]
            {
                ".....a.....",
                "..a..bb.a..",
                "..bb.cb.b..",
                "..bcbccbb..",
                "..bcWWWcba.",
                ".abWWWWWba.",
                ".bWKGWKGWb.",
                ".bWWWKWWWb.",
                "..bWKWKWb..",
                "..abWWWba..",
                "...abbba...",
                "....aaa....",
            },
        };

        static Sprite CurseSprite(int i)
        {
            return PixelArt.FromGrid("curse_skull_" + i, CurseFrames[i], CursePalette, new Vector2(0.5f, 0.5f), PixelArt.PixelsPerUnit);
        }
    }
}
