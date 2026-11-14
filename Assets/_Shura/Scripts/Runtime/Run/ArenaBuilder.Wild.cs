using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Wild Wood (Bosque Selvagem), ao luar: uma clareira numa mata fechada à noite — capim
    /// verde-azulado, trilhas de terra escura, flores que brilham, e um ou dois lagos rasos que
    /// espelham quem entra (com caminho de lua, o reflexo das copas, vitórias-régias e lótus).
    /// A borda é um emaranhado de pinheiros, árvores redondas e algumas cerejeiras acesas, mais
    /// escuro quanto mais fundo. A natureza é a do Ninja Adventure recolorida para a noite
    /// (tiles_nature_night.png, Tools/art_traps/gen_night_nature.py).
    /// Everything comes from the room's own seed, so every player sees the same clearing.
    /// </summary>
    public static partial class ArenaBuilder
    {
        const string NightSheet = "tiles_nature_night";

        // Na folha noturna: pinheiro, maciço de pinheiros, árvores redondas, cerejeira acesa, pinheiro de luar.
        static readonly RectInt Pine = new RectInt(2, 0, 2, 2);
        static readonly RectInt PineCluster = new RectInt(0, 2, 4, 3);
        static readonly RectInt[] RoundTrees = { new RectInt(0, 0, 2, 2), new RectInt(16, 0, 2, 2), new RectInt(18, 0, 2, 2) };
        static readonly RectInt Blossom = new RectInt(14, 0, 2, 2);
        static readonly RectInt MoonPine = new RectInt(10, 0, 2, 2);
        /// <summary>
        /// Uma árvore da folha noturna: onde fica o tronco no desenho (fração da largura) e os
        /// troncos que viram corpo sólido (deslocamento em unidades a partir do pivô, meia largura).
        /// Medidos nos pixels da base de cada árvore, para a colisão casar com o que se vê.
        /// </summary>
        sealed class TreeArt
        {
            public RectInt tile;
            public float pivotX;
            public Vector2[] trunks;
            public float halfWidth;
        }

        static readonly TreeArt PineArt = new TreeArt { tile = Pine, pivotX = 11f / 32f, trunks = new[] { Vector2.zero }, halfWidth = 0.32f };
        static readonly TreeArt ClusterArt = new TreeArt
        {
            tile = PineCluster, pivotX = 27f / 64f, halfWidth = 0.32f,
            trunks = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0.28f, 0.35f) },
        };
        static readonly TreeArt[] RoundArts =
        {
            new TreeArt { tile = RoundTrees[0], pivotX = 15.5f / 32f, trunks = new[] { Vector2.zero }, halfWidth = 0.4f },
            new TreeArt { tile = RoundTrees[1], pivotX = 15.5f / 32f, trunks = new[] { Vector2.zero }, halfWidth = 0.4f },
            new TreeArt { tile = RoundTrees[2], pivotX = 15.5f / 32f, trunks = new[] { Vector2.zero }, halfWidth = 0.4f },
        };
        static readonly TreeArt BlossomArt = new TreeArt { tile = Blossom, pivotX = 15.5f / 32f, trunks = new[] { Vector2.zero }, halfWidth = 0.4f };

        static Sprite TreeSprite(TreeArt t) => PixelArt.Region(NightSheet, t.tile.x, t.tile.y, t.tile.width, t.tile.height, new Vector2(t.pivotX, 0.05f));

        /// <summary>Planta uma árvore com corpo: só a base dos troncos bloqueia (lâminas e corpos).</summary>
        static void PlantTree(Transform parent, ArenaLayout layout, TreeArt t, Vector2 at, float scale, string name)
        {
            if (Prop(parent, name, TreeSprite(t), at, scale) == null) return;
            float hw = t.halfWidth * scale;
            foreach (Vector2 trunk in t.trunks)
            {
                Vector2 c = at + trunk * scale;
                layout.AddSolid(new Rect(c.x - hw, c.y - 0.12f, hw * 2f, 0.42f));
            }
        }

        static readonly RectInt[] Undergrowth =
        {
            new RectInt(0, 10, 1, 1), new RectInt(1, 10, 1, 1), new RectInt(4, 10, 1, 1), new RectInt(6, 10, 1, 1),
            new RectInt(7, 10, 1, 1), new RectInt(8, 10, 1, 1), new RectInt(9, 10, 1, 1),
        };

        public static ArenaDecor BuildWild(Transform parent, ArenaLayout layout, Biome biome, ulong seed, bool encodePng)
        {
            var decor = new ArenaDecor { FlameColor = biome.flame, Moss = true, Moonlit = true };
            var rng = new System.Random((int)(seed % 2000000000UL) + 31);
            // Na mata não há estátuas: o bloco vira chão, e só os troncos da árvore bloqueiam.
            foreach (RectInt b in layout.Statues)
            {
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    for (int x = b.xMin; x < b.xMax; x++) layout.SetBlocked(x, y, false);
                }
            }
            PlaceLakes(layout, rng);
            Sprite arena = BuildWildSprite(layout, decor, (int)(seed % 9973UL), encodePng);
            var floor = SpriteUtil.Make("Clearing", parent, arena, Color.white, Sorting.Floor);
            floor.transform.localPosition = Vector3.zero;
            decor.FloorTexture = arena.texture;
            AddNightJungle(parent, layout, (int)(seed % 997UL) + 3, (int)(seed % 9973UL));
            AddLakeSurfaces(parent, layout);
            AddLakeLife(parent, layout, rng);
            AddClearingTrees(parent, layout, rng);
            // A great tree over every thicket.
            foreach (RectInt b in layout.Statues)
            {
                Vector2 at = layout.Origin + new Vector2(b.center.x, b.yMin + 0.35f);
                TreeArt t = b.width >= 3 ? ClusterArt : rng.Next(3) == 0 ? RoundArts[rng.Next(RoundArts.Length)] : PineArt;
                PlantTree(parent, layout, t, at, b.width >= 3 ? 1.1f : 1.25f, "ThicketTree");
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

        /// <summary>Distância de um ponto à armadilha mais próxima (fosso, faixa de fogo ou giro da lâmina) é menor que a margem?</summary>
        static bool NearTrap(ArenaLayout layout, Vector2 p, float margin)
        {
            foreach (TrapDef t in layout.Traps)
            {
                switch (t.type)
                {
                    case TrapType.Spikes:
                        foreach (Vector2Int c in t.cells)
                        {
                            if (Vector2.Distance(layout.CellCenter(c.x, c.y), p) < 0.75f + margin) return true;
                        }
                        break;
                    case TrapType.FlameJet:
                    {
                        Vector2 d = p - t.origin;
                        float along = Mathf.Clamp(Vector2.Dot(d, t.direction), 0f, t.length);
                        if (Vector2.Distance(t.origin + t.direction * along, p) < t.width * 0.5f + margin) return true;
                        break;
                    }
                    case TrapType.Blade:
                        if (Vector2.Distance(t.origin, p) < t.length + margin) return true;
                        break;
                }
            }
            return false;
        }

        /// <summary>Um lago (centro, raios) cobriria alguma armadilha? Testa a borda e o miolo do lago.</summary>
        static bool LakeCoversTrap(ArenaLayout layout, Vector2 c, Vector2 r)
        {
            if (NearTrap(layout, c, Mathf.Max(r.x, r.y) * 0.5f)) return true;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                for (float k = 0.5f; k <= 1.05f; k += 0.5f)
                {
                    var p = c + new Vector2(Mathf.Cos(a) * r.x * k, Mathf.Sin(a) * r.y * k);
                    if (NearTrap(layout, p, 0.6f)) return true;
                }
            }
            return false;
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
                    if (LakeCoversTrap(layout, c, new Vector2(rx, ry))) continue;
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
            for (int py = 0; py < ch; py++)
            {
                int cy = py / Tile;
                for (int px = 0; px < cw; px++)
                {
                    int cx = px / Tile;
                    Vector2 world = layout.Origin + new Vector2((px + 0.5f) / Tile, (py + 0.5f) / Tile);
                    if (layout.IsFloor(cx, cy))
                    {
                        float depth = layout.LakeDepth(world);
                        float shore = layout.LakeDepth(world, 0.45f);
                        canvas[py * cw + px] = depth > 0f ? WaterPixel(px, py, depth) : shore > 0f ? ShorePixel(px, py, shore) : ForestFloor(world, seed, 0f);
                    }
                    // Fora do chão a imagem é transparente: o chão da mata (o mesmo capim) continua por baixo, sem costura.
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

        /// <summary>
        /// O chão da mata ao luar, igual dentro e fora da arena (calculado no pixel do mundo, sem
        /// costura): capim verde-azulado com trilhas escuras dentro; fora, folhas caídas que
        /// escurecem aos poucos com a distância (<paramref name="outside"/>, em unidades).
        /// </summary>
        static Color32 ForestFloor(Vector2 world, int seed, float outside)
        {
            int wx = Mathf.FloorToInt(world.x * Tile), wy = Mathf.FloorToInt(world.y * Tile);
            float n = Hash(wx + seed * 7, wy);
            float clump = Hash((wx >> 1) + seed, wy >> 1);
            float patch = Mathf.PerlinNoise(world.x * 0.18f + seed, world.y * 0.18f);
            float moon = Mathf.PerlinNoise(world.x * 0.07f + seed * 0.3f, world.y * 0.07f + 11f);
            float fade = Mathf.Lerp(1f, 0.3f, Mathf.SmoothStep(0f, 1f, outside / 7f));
            // Trilhas de terra escura onde a clareira é mais pisada (só dentro e logo na beira).
            if (patch < 0.3f && outside < 0.6f)
            {
                float k = (0.85f + 0.2f * clump) * fade;
                return Rgb(0.15f * k, 0.15f * k, 0.22f * k);
            }
            float g = (0.8f + 0.35f * patch + 0.08f * (clump - 0.5f) + 0.25f * moon) * fade;
            if (n > 0.992f) return Rgb(0.2f * fade, 0.44f * fade, 0.48f * fade);                // folha pegando a lua
            if (n < 0.0012f && moon > 0.55f && outside < 0.5f) return Rgb(1f, 0.62f, 0.8f);     // florzinha rosa acesa (rara)
            // Fora da arena, folhas caídas: manchas um pouco mais escuras e frias.
            if (outside > 0f && clump < 0.18f) g *= 0.82f;
            return Rgb(0.07f * g, 0.2f * g, 0.24f * g);
        }

        /// <summary>A água: anil fundo no meio, azul-petróleo na beira, com linhas de onda.</summary>
        static Color32 WaterPixel(int x, int y, float depth)
        {
            float d = Mathf.SmoothStep(0f, 1f, depth);
            float wave = Hash(x >> 2, (y + (x >> 3)) >> 1) * 0.04f;
            bool line = ((y + (x >> 2)) % 9) == 0 && Hash(x >> 3, y) > 0.55f;
            float l = line ? 0.06f : 0f;
            return Rgb(Mathf.Lerp(0.08f, 0.03f, d) + wave + l, Mathf.Lerp(0.24f, 0.08f, d) + wave + l, Mathf.Lerp(0.36f, 0.2f, d) + wave + l * 1.5f);
        }

        /// <summary>A margem: pedra molhada escura e juncos.</summary>
        static Color32 ShorePixel(int x, int y, float shore)
        {
            float n = Hash(x, y);
            if (n > 0.9f) return Rgb(0.16f, 0.4f, 0.4f);
            float k = 0.85f + 0.2f * n;
            return Rgb(0.14f * k, 0.15f * k, 0.2f * k);
        }

        /// <summary>
        /// O chão de mata em volta e o emaranhado: uma faixa densa de pinheiros (com árvores
        /// redondas, cerejeiras acesas e pinheiros de luar) colada às bordas, vegetação baixa na
        /// beirada, e mata mais rala e escura ao fundo.
        /// </summary>
        static void AddNightJungle(Transform parent, ArenaLayout layout, int seed, int groundSeed)
        {
            int gw = (layout.W + 2 * WildMargin) * Tile, gh = (layout.H + 2 * WildMargin) * Tile;
            var px = new Color32[gw * gh];
            Vector2 origin = layout.Origin - new Vector2(WildMargin, WildMargin);
            Rect fb = layout.FloorBounds;
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    Vector2 world = origin + new Vector2((x + 0.5f) / Tile, (y + 0.5f) / Tile);
                    float dx = Mathf.Max(0f, Mathf.Max(fb.xMin - world.x, world.x - fb.xMax));
                    float dy = Mathf.Max(0f, Mathf.Max(fb.yMin - world.y, world.y - fb.yMax));
                    px[y * gw + x] = ForestFloor(world, groundSeed, Mathf.Sqrt(dx * dx + dy * dy));
                }
            }
            var tex = new Texture2D(gw, gh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "NightWilds" };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var ground = SpriteUtil.Make("Wilds", parent, Sprite.Create(tex, new Rect(0, 0, gw, gh), Vector2.zero, Tile), Color.white, Sorting.Floor - 10);
            ground.transform.localPosition = origin;

            // O limite: uma cerca viva de moitas e samambaias em cada célula que encosta no chão.
            AddLivingHedge(parent, layout, seed);

            // Embaixo a mata começa mais longe: as copas sobem e cobririam a luta.
            Rect clear = new Rect(fb.xMin - 1.2f, fb.yMin - 1.6f, fb.width + 2.4f, fb.height + 2.4f);
            Sprite pine = TreeSprite(PineArt);
            Sprite cluster = TreeSprite(ClusterArt);
            Sprite blossom = TreeSprite(BlossomArt);
            Sprite moonPine = PixelArt.Region(NightSheet, MoonPine.x, MoonPine.y, MoonPine.width, MoonPine.height, new Vector2(0.5f, 0.05f));
            var round = new Sprite[RoundTrees.Length];
            for (int i = 0; i < round.Length; i++) round[i] = TreeSprite(RoundArts[i]);
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);

            float w = layout.W + 2 * WildMargin, h = layout.H + 2 * WildMargin;
            const float step = 0.8f;
            for (float y = origin.y + 0.5f; y < origin.y + h - 0.5f; y += step)
            {
                for (float x = origin.x + 0.5f; x < origin.x + w - 0.5f; x += step)
                {
                    int ix = Mathf.RoundToInt(x * 10f), iy = Mathf.RoundToInt(y * 10f);
                    float r0 = Hash(ix + seed * 17, iy), r1 = Hash(ix, iy + 5), r2 = Hash(ix + 9, iy), r3 = Hash(ix + 3, iy + 11);
                    var p = new Vector2(x + (r1 - 0.5f) * step, y + (r2 - 0.5f) * step);
                    if (clear.Contains(p)) continue;
                    float d = Mathf.Max(Mathf.Max(clear.xMin - p.x, p.x - clear.xMax), Mathf.Max(clear.yMin - p.y, p.y - clear.yMax));
                    // Mais fundo, mais ralo (e mais escuro).
                    float keep = d < 6f ? 0.92f : d < 10f ? 0.55f : 0.3f;
                    if (r0 > keep) continue;
                    Sprite s;
                    float scale = 0.95f + 0.6f * r3;
                    double pick = r1 * 0.6f + r2 * 0.4f;
                    if (d > 1.8f && r3 > 0.86f) s = cluster;
                    else if (pick < 0.08f) s = blossom;
                    else if (pick < 0.12f) s = moonPine;
                    else if (pick < 0.3f) s = round[(int)(r2 * round.Length) % round.Length];
                    else s = pine;
                    SpriteRenderer t = Prop(parent, s == blossom ? "Cerejeira" : "Pinheiro", s, p, scale);
                    if (t == null) continue;
                    t.flipX = r0 > keep * 0.5f;
                    float dark = Mathf.Clamp01((d - 1f) / 9f);
                    t.color = Color.Lerp(Color.white, new Color(0.42f, 0.5f, 0.66f), dark);
                    // As cerejeiras acendem um halo rosado.
                    if (s == blossom)
                    {
                        var glow = SpriteUtil.Make("Halo", parent, soft, new Color(1f, 0.45f, 0.7f, 0.18f * (1f - dark)), t.sortingOrder - 1);
                        glow.transform.localPosition = p + new Vector2(0f, 0.9f * scale);
                        glow.transform.localScale = Vector3.one * 2.6f * scale;
                    }
                }
            }

            var flies = parent.gameObject.AddComponent<Fireflies>();
            flies.Init(new Rect(origin.x + 4f, origin.y + 4f, w - 8f, h - 8f), seed);
        }

        /// <summary>
        /// A cerca viva do limite: em cada célula fora do chão que encosta nele, duas ou três
        /// moitas e samambaias de tamanhos variados, tortas e sobrepostas, com uma sombra macia
        /// por baixo. Lê-se como mata fechada (não dá para passar), sem nenhuma linha reta.
        /// </summary>
        static void AddLivingHedge(Transform parent, ArenaLayout layout, int seed)
        {
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);
            var bushes = new Sprite[Undergrowth.Length];
            for (int i = 0; i < bushes.Length; i++) bushes[i] = PixelArt.Region(NightSheet, Undergrowth[i].x, Undergrowth[i].y, 1, 1, new Vector2(0.5f, 0.1f));
            for (int cy = -1; cy <= layout.H; cy++)
            {
                for (int cx = -1; cx <= layout.W; cx++)
                {
                    if (layout.IsFloor(cx, cy) || !NearFloor(layout, cx, cy)) continue;
                    Vector2 cell = layout.Origin + new Vector2(cx + 0.5f, cy + 0.5f);
                    var shade = SpriteUtil.Make("SombraDaMata", parent, soft, new Color(0.01f, 0.03f, 0.06f, 0.55f), Sorting.Floor + 1);
                    shade.transform.localPosition = cell;
                    shade.transform.localScale = new Vector3(1.9f, 1.6f, 1f);
                    int n = 2 + (Hash(cx * 13 + seed, cy * 7) > 0.5f ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        float a = Hash(cx * 31 + i * 7 + seed, cy * 17 + i), b = Hash(cx * 11 + i, cy * 29 + i * 5 + seed);
                        var p = cell + new Vector2((a - 0.5f) * 0.9f, (b - 0.5f) * 0.8f);
                        Sprite s = bushes[(int)(Hash(cx + i * 3, cy + seed) * bushes.Length) % bushes.Length];
                        SpriteRenderer r = Prop(parent, "CercaViva", s, p, 1.05f + 0.55f * a);
                        if (r == null) continue;
                        r.flipX = b > 0.5f;
                        r.color = Color.Lerp(Color.white, new Color(0.7f, 0.78f, 0.9f), b * 0.6f);
                    }
                }
            }
        }

        /// <summary>The moving light on each lake: a mask of its shape, drawn with the water shader.</summary>
        static void AddLakeSurfaces(Transform parent, ArenaLayout layout)
        {
            Shader water = Resources.Load<Shader>("Shaders/ShuraWater");
            if (water == null || !water.isSupported) return;
            foreach (Vector4 l in layout.Lakes)
            {
                // Um material por lago: o caminho da lua passa pelo centro de cada um.
                var mat = new Material(water);
                mat.SetColor("_Tint", new Color(0.78f, 0.92f, 1f, 1f));
                mat.SetFloat("_Strength", 0.7f);
                mat.SetVector("_Center", new Vector4(l.x, l.y, 0f, 0f));
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

        // ── Vitórias-régias, lótus e flores acesas na margem (desenhados para o jogo) ─────

        static readonly Dictionary<char, Color> LakePalette = new Dictionary<char, Color>
        {
            { 'a', ColorUtil.Hex("#123E44") },
            { 'b', ColorUtil.Hex("#1F6462") },
            { 'c', ColorUtil.Hex("#3A8C83") },
            { 'w', ColorUtil.Hex("#EEF3FF") },
            { 'v', ColorUtil.Hex("#C9D6F2") },
            { 'p', ColorUtil.Hex("#FFADC8") },
            { 'y', ColorUtil.Hex("#FFE38A") },
        };

        static readonly string[] LilyPad =
        {
            "..aaaa..",
            ".abbbbaa",
            "abbcbbba",
            "abbbb.ba",
            ".abbb.a.",
            "..aaa...",
        };

        static readonly string[] Lotus =
        {
            "...w....",
            "..vwv...",
            ".wvpvw..",
            "wvpypvw.",
            ".wvvvw..",
            "..abba..",
            ".abbbba.",
            "..aaaa..",
        };

        static readonly string[] GlowFlower =
        {
            ".p.p.",
            "ppypp",
            ".ppp.",
            "..b..",
            ".bcb.",
        };

        /// <summary>Vitórias-régias e lótus brancos sobre os lagos; flores rosadas acesas na margem.</summary>
        static void AddLakeLife(Transform parent, ArenaLayout layout, System.Random rng)
        {
            Sprite pad = PixelArt.FromGrid("lily_pad", LilyPad, LakePalette, new Vector2(0.5f, 0.5f));
            Sprite lotus = PixelArt.FromGrid("lotus_white", Lotus, LakePalette, new Vector2(0.5f, 0.3f));
            Sprite flower = PixelArt.FromGrid("glow_flower", GlowFlower, LakePalette, new Vector2(0.5f, 0.1f));
            Sprite soft = ShapeLibrary.Get(Shape.SoftCircle);
            foreach (Vector4 l in layout.Lakes)
            {
                var c = new Vector2(l.x, l.y);
                int pads = 4 + rng.Next(4);
                for (int i = 0; i < pads; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.35f + 0.5f * (float)rng.NextDouble();
                    var p = c + new Vector2(Mathf.Cos(a) * l.z * r, Mathf.Sin(a) * l.w * r);
                    if (layout.LakeDepth(p) < 0.25f) continue;
                    bool bloom = rng.Next(3) == 0;
                    var s = SpriteUtil.Make(bloom ? "Lotus" : "VitoriaRegia", parent, bloom ? lotus : pad, Color.white, Sorting.Floor + 3);
                    s.transform.localPosition = p;
                    s.transform.localRotation = Quaternion.Euler(0f, 0f, bloom ? 0f : rng.Next(4) * 90f);
                    if (!bloom) continue;
                    var glow = SpriteUtil.Make("LuzDoLotus", parent, soft, new Color(0.85f, 0.92f, 1f, 0.22f), Sorting.Floor + 2);
                    glow.transform.localPosition = p + new Vector2(0f, 0.15f);
                    glow.transform.localScale = Vector3.one * 1.4f;
                }
                // Flores rosadas acesas pela margem.
                int flowers = 3 + rng.Next(4);
                for (int i = 0; i < flowers; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var p = c + new Vector2(Mathf.Cos(a) * (l.z + 0.55f), Mathf.Sin(a) * (l.w + 0.5f));
                    if (layout.LakeDepth(p) > 0f || NearTrap(layout, p, 0.3f) || !layout.IsFloor(Mathf.FloorToInt(p.x - layout.Origin.x), Mathf.FloorToInt(p.y - layout.Origin.y))) continue;
                    Prop(parent, "FlorAcesa", flower, p);
                    var glow = SpriteUtil.Make("LuzDaFlor", parent, soft, new Color(1f, 0.5f, 0.75f, 0.25f), Sorting.Floor + 4);
                    glow.transform.localPosition = p + new Vector2(0f, 0.15f);
                    glow.transform.localScale = Vector3.one * 1.1f;
                }
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
                if (Vector2.Distance(p, fb.center) < 3.5f || layout.LakeDepth(p, 1.2f) > 0f || layout.InSolid(p) || NearTrap(layout, p, 1.4f)) continue;
                TreeArt t = rng.Next(4) == 0 ? BlossomArt : rng.Next(2) == 0 ? PineArt : RoundArts[rng.Next(RoundArts.Length)];
                // The trunk only (the crown is above), where the drawing puts it.
                PlantTree(parent, layout, t, p, 1f, "Tree");
                placed++;
            }
        }
    }
}
