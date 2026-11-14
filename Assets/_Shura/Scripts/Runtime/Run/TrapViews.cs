using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// What traps look like (purely visual, driven by the room's trap clock), em estética samurai:
    /// - Takeyari: um fosso com moldura de madeira; no aviso as pontas de bambu tremem e
    ///   avermelham, e então brotam e se recolhem.
    /// - Oni de pedra: a estátua acorda (olhos e boca em brasa) e sopra fogo pela faixa de chão
    ///   chamuscado, que esquenta no aviso.
    /// - Katanas giratórias: duas lâminas presas a uma tsuba sobre um pedestal de pedra, com um
    ///   rastro do giro; o aço se suja de sangue a cada golpe que dão (sem círculo no chão).
    /// Arte: Resources/Art/Extra/traps_samurai.png (Tools/art_traps/gen_traps.py), a partir do
    /// "aMazing Maze Sprites" de TimberwolfGames; o fogo é do Ninja Adventure.
    /// </summary>
    public sealed class TrapViews
    {
        sealed class View
        {
            public TrapDef trap;
            public readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
            /// <summary>O chão chamuscado por onde o fogo passa.</summary>
            public readonly List<SpriteRenderer> lane = new List<SpriteRenderer>();
            /// <summary>O rastro de cada katana.</summary>
            public readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
            public SpriteRenderer glow, statue, hub;
        }

        static readonly Color TipsWarn = new Color(1f, 0.62f, 0.55f, 1f);
        static readonly Color LaneIdle = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color LaneWarn = new Color(1f, 0.7f, 0.5f, 1f);
        static readonly Color LaneHot = new Color(1f, 0.95f, 0.8f, 1f);
        /// <summary>Quadros de simulação de cada passo das estacas subindo e descendo.</summary>
        const int RiseTicksPerFrame = 2;

        readonly List<View> views = new List<View>();
        readonly Sprite[] stakes = new Sprite[6];
        readonly Sprite[] katana = new Sprite[4];
        readonly Sprite[] oni = new Sprite[2];
        readonly Sprite tsuba, plinth, scorch, hole, holeWall;
        /// <summary>Bordas da moldura do fosso: cima, baixo, esquerda, direita.</summary>
        readonly Sprite[] rim = new Sprite[4];
        readonly Sprite[] flame;

        static Sprite Cut(Texture2D tex, int x, int y, int w, int h, Vector2 pivot, float ppu = PixelArt.PixelsPerUnit)
        {
            return Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        public TrapViews(Transform parent, ArenaLayout layout)
        {
            var tex = Resources.Load<Texture2D>("Art/Extra/traps_samurai");
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                for (int i = 0; i < 6; i++) stakes[i] = Cut(tex, i * 16, 0, 16, 32, new Vector2(0.5f, 0.25f));
                // A katana mede uma unidade (escala = alcance), presa pelo pomo ao eixo.
                for (int i = 0; i < 4; i++) katana[i] = Cut(tex, i * 48, 32, 48, 8, new Vector2(0f, 0.5f), 48f);
                tsuba = Cut(tex, 0, 40, 16, 16, new Vector2(0.5f, 0.5f));
                plinth = Cut(tex, 16, 40, 16, 16, new Vector2(0.5f, 0.5f));
                scorch = Cut(tex, 32, 40, 16, 16, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < 2; i++) oni[i] = Cut(tex, i * 16, 56, 16, 32, new Vector2(0.5f, 0.12f));
                hole = Cut(tex, 0, 88, 16, 16, new Vector2(0.5f, 0.5f));
                holeWall = Cut(tex, 16, 88, 16, 16, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < 4; i++) rim[i] = Cut(tex, 32 + i * 16, 88, 16, 16, new Vector2(0.5f, 0.5f));
            }
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);
            flame = PixelArt.Strip("fx_flame", 25, 30, new Vector2(0.5f, 0.15f));

            foreach (TrapDef trap in layout.Traps)
            {
                var v = new View { trap = trap };
                switch (trap.type)
                {
                    case TrapType.Spikes:
                    {
                        // Um fosso só para o grupo de células: a moldura corre por fora.
                        var cells = new HashSet<Vector2Int>(trap.cells);
                        foreach (Vector2Int c in trap.cells)
                        {
                            Vector2 at = layout.CellCenter(c.x, c.y);
                            bool up = cells.Contains(c + Vector2Int.up);
                            var pit = SpriteUtil.Make("Fosso", parent, up ? hole : holeWall, Color.white, Sorting.Floor + 5);
                            pit.transform.localPosition = at;
                            Vector2Int[] sides = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                            for (int i = 0; i < 4; i++)
                            {
                                if (cells.Contains(c + sides[i])) continue;
                                var edge = SpriteUtil.Make("Moldura", parent, rim[i], Color.white, Sorting.Floor + 6);
                                edge.transform.localPosition = at;
                            }
                            var sr = SpriteUtil.Make("Takeyari", parent, stakes[0], Color.white, Sorting.Floor + 7);
                            sr.transform.localPosition = at;
                            v.parts.Add(sr);
                        }
                        break;
                    }
                    case TrapType.FlameJet:
                    {
                        // O chão chamuscado marca a faixa mesmo enquanto o oni dorme.
                        int plates = Mathf.Max(1, Mathf.RoundToInt(trap.length));
                        for (int i = 0; i < plates; i++)
                        {
                            Vector2 at = trap.origin + trap.direction * (0.6f + i * (trap.length - 0.2f) / plates);
                            var plate = SpriteUtil.Make("Chamuscado", parent, scorch, LaneIdle, Sorting.Floor + 5);
                            plate.transform.localPosition = at;
                            plate.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(trap.direction.y, trap.direction.x) * Mathf.Rad2Deg);
                            plate.transform.localScale = Vector3.one * 1.05f;
                            v.lane.Add(plate);
                        }
                        v.glow = SpriteUtil.Make("BrasaDoOni", parent, soft, new Color(1f, 0.5f, 0.2f, 0f), Sorting.Shadow + 6);
                        v.glow.transform.localPosition = trap.origin + trap.direction * 0.4f;
                        v.glow.transform.localScale = Vector3.one * 1.8f;
                        v.statue = SpriteUtil.Make("Oni", parent, oni[0], Color.white, -Mathf.RoundToInt(trap.origin.y * 20f) + 1);
                        v.statue.transform.localPosition = trap.origin - trap.direction * 0.15f;
                        v.statue.transform.localScale = Vector3.one * 1.25f;
                        int n = Mathf.Max(2, Mathf.RoundToInt(trap.length / 0.75f));
                        for (int i = 0; i < n; i++)
                        {
                            Vector2 p = trap.origin + trap.direction * (0.5f + i * trap.length / n);
                            var f = SpriteUtil.Make("Flame", parent, flame.Length > 0 ? flame[0] : soft, Color.white, -Mathf.RoundToInt(p.y * 20f) + 2);
                            f.transform.localPosition = p;
                            f.transform.localScale = Vector3.one * 0.9f;
                            f.enabled = false;
                            v.parts.Add(f);
                        }
                        break;
                    }
                    case TrapType.Blade:
                    {
                        var shadow = SpriteUtil.Make("Sombra", parent, soft, new Color(0f, 0f, 0f, 0.35f), Sorting.Shadow);
                        shadow.transform.localPosition = trap.origin + new Vector2(0f, -0.1f);
                        shadow.transform.localScale = new Vector3(1.3f, 0.6f, 1f);
                        var baseR = SpriteUtil.Make("Pedestal", parent, plinth, Color.white, -Mathf.RoundToInt(trap.origin.y * 20f));
                        baseR.transform.localPosition = trap.origin;
                        for (int i = 0; i < 2; i++)
                        {
                            var holder = new GameObject("Katana").transform;
                            holder.SetParent(parent, false);
                            holder.localPosition = trap.origin;
                            var ghost = SpriteUtil.Make("Rastro", holder, katana[0], new Color(0.85f, 0.9f, 1f, 0.22f), Sorting.Overlay - 41);
                            ghost.transform.localScale = Vector3.one * trap.length;
                            v.ghosts.Add(ghost);
                            var arm = SpriteUtil.Make("Lamina", holder, katana[0], Color.white, Sorting.Overlay - 40);
                            arm.transform.localScale = Vector3.one * trap.length;
                            v.parts.Add(arm);
                        }
                        v.hub = SpriteUtil.Make("Tsuba", parent, tsuba, Color.white, Sorting.Overlay - 39);
                        v.hub.transform.localPosition = trap.origin;
                        v.hub.transform.localScale = Vector3.one * 0.9f;
                        break;
                    }
                }
                views.Add(v);
            }
        }

        public void Render(int tick, float time)
        {
            foreach (View v in views)
            {
                TrapDef trap = v.trap;
                int phase = trap.PhaseAt(tick);
                switch (trap.type)
                {
                    case TrapType.Spikes:
                        RenderStakes(v, tick, phase, time);
                        break;
                    case TrapType.FlameJet:
                    {
                        float warm = phase == 1 ? 0.35f + 0.25f * Mathf.Sin(time * 18f) : phase == 2 ? 0.6f : 0f;
                        v.glow.color = new Color(1f, 0.5f, 0.2f, warm);
                        bool awake = phase > 0;
                        v.statue.sprite = oni[awake && (phase == 2 || Mathf.Repeat(time * 8f, 1f) < 0.6f) ? 1 : 0];
                        bool blink = phase == 1 && Mathf.Repeat(time * 10f, 1f) < 0.5f;
                        foreach (SpriteRenderer plate in v.lane) plate.color = phase == 2 ? LaneHot : blink ? LaneWarn : LaneIdle;
                        for (int i = 0; i < v.parts.Count; i++)
                        {
                            SpriteRenderer f = v.parts[i];
                            f.enabled = phase == 2;
                            if (phase == 2 && flame.Length > 0) f.sprite = flame[(Mathf.FloorToInt(time * 14f) + i) % flame.Length];
                        }
                        break;
                    }
                    case TrapType.Blade:
                    {
                        float a = trap.BladeAngle(tick);
                        Sprite steel = katana[Mathf.Clamp(trap.Hits / 2, 0, 3)];
                        // O rastro fica um pouco atrás no giro.
                        float trail = -Mathf.Sign(trap.spin) * 11f;
                        for (int i = 0; i < v.parts.Count; i++)
                        {
                            v.parts[i].sprite = steel;
                            v.ghosts[i].sprite = steel;
                            v.parts[i].transform.parent.localRotation = Quaternion.Euler(0f, 0f, a + i * 180f);
                            v.ghosts[i].transform.localRotation = Quaternion.Euler(0f, 0f, trail);
                        }
                        v.hub.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                        break;
                    }
                }
            }
        }

        /// <summary>O fosso dorme; no aviso as pontas tremem; ativas, brotam, ficam e se recolhem.</summary>
        void RenderStakes(View v, int tick, int phase, float time)
        {
            TrapDef trap = v.trap;
            int t = ((tick + trap.offset) % trap.period + trap.period) % trap.period;
            int frame = 0;
            Color color = Color.white;
            if (phase == 1)
            {
                bool peek = Mathf.Repeat(time * 9f, 1f) < 0.55f;
                frame = peek ? 1 : 0;
                color = peek ? TipsWarn : Color.white;
            }
            else if (phase == 2)
            {
                int k = t - (trap.period - trap.active);
                int left = trap.active - 1 - k;
                int up = Mathf.Min(k, left) / RiseTicksPerFrame;
                frame = Mathf.Clamp(2 + up, 2, 5);
            }
            foreach (SpriteRenderer sr in v.parts)
            {
                sr.sprite = stakes[frame];
                sr.color = color;
                // Erguidas, as estacas ficam de pé entre os lutadores; recolhidas, são chão.
                sr.sortingOrder = frame >= 2 ? -Mathf.RoundToInt(sr.transform.localPosition.y * 20f) : Sorting.Floor + 7;
            }
        }
    }
}
