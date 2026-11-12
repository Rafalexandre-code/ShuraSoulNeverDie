using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    public struct EnemySpawn
    {
        public string id;
        /// <summary>A Guardian: bigger, tougher, worth extra Ecos.</summary>
        public bool elite;
        /// <summary>A rival of the Shuradō in this slot (§5.1).</summary>
        public string rivalId;

        public EnemySpawn(string id, bool elite = false)
        {
            this.id = id;
            this.elite = elite;
            rivalId = null;
        }
    }

    /// <summary>The combat rule each biome changes (design doc §6.3).</summary>
    public enum BiomeRule
    {
        /// <summary>Cinzas: embers make Fire last twice as long; marked eruptions.</summary>
        Embers,
        /// <summary>Musgo: wet floor, dodges slide 30% farther; spores hide the rings.</summary>
        WetMoss,
        /// <summary>Sangue: red moon, healing −50%, lifesteal +100%; pools that feed the Shura.</summary>
        BloodMoon,
        /// <summary>Abismo: darkness, only the lantern's radius is seen; rings within 5 m; edges.</summary>
        Darkness,
        /// <summary>Gelo: cold drains breath; thin ice breaks under heavy blades; frozen enemies.</summary>
        Cold,
        /// <summary>Bambuzal: cut bamboo falls like a trap; ninjas drop from the canopy.</summary>
        Bamboo,
        /// <summary>Castelo: shōji screens tear; narrow halls and ambushes.</summary>
        Castle,
        /// <summary>Rio Sanzu: knee-deep water, Lightning spreads, everyone wades slower.</summary>
        River,
        /// <summary>Yomi (depth 40+): everything is ethereal until a perfect parry.</summary>
        Yomi,
        /// <summary>Bosque Selvagem: a clearing with shallow lakes and trees; fireflies at night.</summary>
        Wild,
    }

    public enum Weather
    {
        Clear,
        /// <summary>Fire −50%, Lightning +50%, blood washes away.</summary>
        Rain,
        /// <summary>Projectiles curve; leaves show the way.</summary>
        Wind,
        /// <summary>Enemies only seen within 6 m.</summary>
        Fog,
        /// <summary>Lightning strikes the ground (with warning), drawn to steel.</summary>
        Storm,
        /// <summary>Footprints reveal the invisible.</summary>
        Snow,
        /// <summary>Shura rises 50% faster for everyone, enemies included.</summary>
        FullMoon,
    }

    /// <summary>Every three rooms the hour moves on (§6.4).</summary>
    public enum TimeOfDay
    {
        Morning,
        Afternoon,
        Dusk,
        /// <summary>More yokai, fewer humans.</summary>
        Night,
        /// <summary>The dead are weak.</summary>
        Dawn,
    }

    /// <summary>Visual identity of a stretch of depth.</summary>
    public sealed class Biome
    {
        public string suffix;
        public string name;
        public BiomeRule rule;
        /// <summary>The biome only appears from this depth on (Yomi: 40).</summary>
        public int minDepth;
        public Weather[] weathers;
        public Color floorA;
        public Color floorB;
        public Color mortar;
        public Color wall;
        public Color wallEdge;
        public Color flame;
    }

    public static class Biomes
    {
        public const int RoomsPerBiome = 5;

        public static readonly Biome[] All =
        {
            new Biome
            {
                suffix = "das Cinzas", name = "Cinzas", rule = BiomeRule.Embers, weathers = new[] { Weather.Wind, Weather.Storm, Weather.Rain }, floorA = ColorUtil.Hex("#23212C"), floorB = ColorUtil.Hex("#272432"),
                mortar = ColorUtil.Hex("#1A1921"), wall = ColorUtil.Hex("#3D3847"), wallEdge = ColorUtil.Hex("#5A5266"),
                flame = ColorUtil.Hex("#F2A541"),
            },
            new Biome
            {
                suffix = "do Bosque Selvagem", name = "Bosque Selvagem", rule = BiomeRule.Wild, weathers = new[] { Weather.Rain, Weather.Fog, Weather.Wind },
                floorA = ColorUtil.Hex("#22301F"), floorB = ColorUtil.Hex("#273623"),
                mortar = ColorUtil.Hex("#141D13"), wall = ColorUtil.Hex("#2E4A2A"), wallEdge = ColorUtil.Hex("#4C7244"),
                flame = ColorUtil.Hex("#FFD27A"),
            },
            new Biome
            {
                suffix = "do Musgo", name = "Musgo", rule = BiomeRule.WetMoss, weathers = new[] { Weather.Rain, Weather.Fog, Weather.Wind }, floorA = ColorUtil.Hex("#1F2622"), floorB = ColorUtil.Hex("#232B25"),
                mortar = ColorUtil.Hex("#161C18"), wall = ColorUtil.Hex("#34433A"), wallEdge = ColorUtil.Hex("#4F6455"),
                flame = ColorUtil.Hex("#B8D46A"),
            },
            new Biome
            {
                suffix = "do Sangue", name = "Sangue", rule = BiomeRule.BloodMoon, weathers = new[] { Weather.FullMoon, Weather.Fog, Weather.Storm }, floorA = ColorUtil.Hex("#2A1C1E"), floorB = ColorUtil.Hex("#2F2023"),
                mortar = ColorUtil.Hex("#1E1416"), wall = ColorUtil.Hex("#4A2B2E"), wallEdge = ColorUtil.Hex("#6E3A3F"),
                flame = ColorUtil.Hex("#FF6B4A"),
            },
            new Biome
            {
                suffix = "do Abismo", name = "Abismo", rule = BiomeRule.Darkness, weathers = new[] { Weather.Fog, Weather.Wind, Weather.Storm }, floorA = ColorUtil.Hex("#1E1C2E"), floorB = ColorUtil.Hex("#232038"),
                mortar = ColorUtil.Hex("#151424"), wall = ColorUtil.Hex("#362F55"), wallEdge = ColorUtil.Hex("#54497D"),
                flame = ColorUtil.Hex("#A58BFF"),
            },
            new Biome
            {
                suffix = "do Gelo", name = "Gelo", rule = BiomeRule.Cold, weathers = new[] { Weather.Snow, Weather.Wind, Weather.Fog },
                floorA = ColorUtil.Hex("#1E2630"), floorB = ColorUtil.Hex("#222B37"),
                mortar = ColorUtil.Hex("#161D25"), wall = ColorUtil.Hex("#36475A"), wallEdge = ColorUtil.Hex("#557089"),
                flame = ColorUtil.Hex("#8FD3FF"),
            },
            new Biome
            {
                suffix = "do Bambuzal", name = "Bambuzal", rule = BiomeRule.Bamboo, weathers = new[] { Weather.Wind, Weather.Rain, Weather.Fog },
                floorA = ColorUtil.Hex("#1F2A1E"), floorB = ColorUtil.Hex("#243022"),
                mortar = ColorUtil.Hex("#151D14"), wall = ColorUtil.Hex("#38502F"), wallEdge = ColorUtil.Hex("#557A45"),
                flame = ColorUtil.Hex("#D8F28A"),
            },
            new Biome
            {
                suffix = "do Castelo", name = "Castelo", rule = BiomeRule.Castle, weathers = new[] { Weather.Rain, Weather.Storm, Weather.FullMoon },
                floorA = ColorUtil.Hex("#2B2219"), floorB = ColorUtil.Hex("#30261C"),
                mortar = ColorUtil.Hex("#1D1711"), wall = ColorUtil.Hex("#4B3A28"), wallEdge = ColorUtil.Hex("#6E5539"),
                flame = ColorUtil.Hex("#FFC46B"),
            },
            new Biome
            {
                suffix = "do Rio Sanzu", name = "Rio Sanzu", rule = BiomeRule.River, weathers = new[] { Weather.Fog, Weather.Rain, Weather.Storm },
                floorA = ColorUtil.Hex("#18242A"), floorB = ColorUtil.Hex("#1C2A31"),
                mortar = ColorUtil.Hex("#10191D"), wall = ColorUtil.Hex("#2D4650"), wallEdge = ColorUtil.Hex("#43656F"),
                flame = ColorUtil.Hex("#7FE0D8"),
            },
            new Biome
            {
                suffix = "de Yomi", name = "Yomi", rule = BiomeRule.Yomi, minDepth = 40, weathers = new[] { Weather.Fog, Weather.FullMoon, Weather.Snow },
                floorA = ColorUtil.Hex("#201E24"), floorB = ColorUtil.Hex("#25222A"),
                mortar = ColorUtil.Hex("#141217"), wall = ColorUtil.Hex("#3B3644"), wallEdge = ColorUtil.Hex("#5C5468"),
                flame = ColorUtil.Hex("#C9B6FF"),
            },
        };

        /// <summary>Five rooms per biome; ten biomes in a cycle (Yomi waits for depth 40).</summary>
        public static Biome ForDepth(int depth)
        {
            int i = (depth / RoomsPerBiome) % All.Length;
            Biome b = All[i];
            return depth >= b.minDepth ? b : All[(i + 1) % All.Length];
        }

        static readonly string[] RuleTexts =
        {
            "Brasas no chão: o Fogo dura o dobro. Erupções marcadas.",
            "Chão molhado: a esquiva desliza 30% mais longe. Esporos escondem os anéis.",
            "Lua vermelha: cura −50%, roubo de vida +100%. Poças alimentam o Shura.",
            "Escuridão: só o raio da sua luz; anéis só a 5 m. Cuidado com as bordas.",
            "Frio: o fôlego volta devagar. Gelo fino quebra sob lâminas pesadas; inimigos congelados despertam.",
            "Bambus cortados caem como armadilhas. Ninjas descem das copas.",
            "Portas de shōji rasgam com um golpe. Corredores estreitos, emboscadas.",
            "Água até o joelho: todos andam mais devagar e o Raio se espalha.",
            "Mundo dos mortos: tudo é etéreo até um parry perfeito.",
            "Mata fechada: lagos rasos espelham quem entra neles, árvores param lâminas e corpos. À noite, vagalumes.",
        };

        public static string RuleText(BiomeRule r) => RuleTexts[(int)r];

        static readonly string[] WeatherNames = { "Céu limpo", "Chuva", "Vento forte", "Neblina", "Tempestade", "Neve", "Lua cheia" };
        static readonly string[] WeatherTexts =
        {
            "",
            "Fogo −50%, Raio +50%, o sangue é lavado.",
            "Projéteis fazem curva; as folhas mostram a direção.",
            "Inimigos só aparecem a 6 m.",
            "Raios caem com aviso e procuram o aço.",
            "Pegadas revelam os invisíveis.",
            "O Shura sobe 50% mais rápido para todos, inimigos inclusive.",
        };
        static readonly string[] TimeNames = { "Manhã", "Tarde", "Crepúsculo", "Noite", "Aurora" };

        public static string WeatherName(Weather w) => WeatherNames[(int)w];
        public static string WeatherText(Weather w) => WeatherTexts[(int)w];
        public static string TimeName(TimeOfDay t) => TimeNames[(int)t];

        /// <summary>The hour moves on every three rooms.</summary>
        public static TimeOfDay TimeAt(int depth) => (TimeOfDay)((Mathf.Max(0, depth) / 3) % 5);
    }

    public sealed class RoomDef
    {
        public readonly string name;
        public readonly int depth;
        public readonly Biome biome;
        public readonly float width;
        public readonly float height;
        public readonly bool guardian;
        /// <summary>Each wave spawns when the previous one is dead.</summary>
        public readonly EnemySpawn[][] waves;
        public readonly ArenaShape shape;
        /// <summary>Seeds the arena's statues and traps.</summary>
        public readonly ulong layoutSeed;
        /// <summary>The room opens with an Iai standoff against the first wave's leader.</summary>
        public bool standoff;
        /// <summary>The door it came through on the route map.</summary>
        public RoomKind kind = RoomKind.Combat;
        public RewardHint hint;
        public ChallengeKind challenge;
        public int eventId = -1;
        public Weather weather;
        public TimeOfDay time;
        /// <summary>The tutorial's hall (built as a wooden dojo in a night forest).</summary>
        public bool dojo;
        /// <summary>A Corrupted Zone: purple mist around a core stone (§6.3).</summary>
        public bool corrupted;
        /// <summary>A rival's lair: who waits in it.</summary>
        public string rivalId;
        /// <summary>Everything the door decided (kind, weather, hour, standoff…), for scaled copies of the room.</summary>
        public void CopyRoadFrom(RoomDef o)
        {
            standoff = o.standoff;
            kind = o.kind;
            hint = o.hint;
            challenge = o.challenge;
            eventId = o.eventId;
            weather = o.weather;
            time = o.time;
            corrupted = o.corrupted;
            rivalId = o.rivalId;
        }

        /// <summary>Encounter rooms (shrine, merchant, event…) have no waves: their doors decide.</summary>
        public bool IsEncounter => RouteMap.IsEncounter(kind);

        public RoomDef(string name, int depth, Biome biome, float width, float height, bool guardian, EnemySpawn[][] waves,
                       ArenaShape shape = ArenaShape.Hall, ulong layoutSeed = 0)
        {
            this.shape = shape;
            this.layoutSeed = layoutSeed;
            this.name = name;
            this.depth = depth;
            this.biome = biome;
            this.width = width;
            this.height = height;
            this.guardian = guardian;
            this.waves = waves;
        }
    }

    /// <summary>
    /// Endless, seeded room generation. Depth 0 teaches; afterwards each depth up to 7
    /// introduces one new enemy alone before mixing it in. Waves are filled from a budget
    /// that grows with depth; every 5th room ends with a Guardian. The same seed and depth
    /// always produce the same room.
    /// </summary>
    public static class RoomGenerator
    {
        struct RosterEntry
        {
            public string id;
            public int unlockDepth;
            public float cost;
            /// <summary>Support enemies (healers, sentinels) never lead a Guardian room.</summary>
            public bool guardian;

            public RosterEntry(string id, int unlockDepth, float cost, bool guardian = true)
            {
                this.id = id;
                this.unlockDepth = unlockDepth;
                this.cost = cost;
                this.guardian = guardian;
            }
        }

        /// <summary>
        /// Two newcomers per depth from depth 1 to 11, one at 12: the whole bestiary is met
        /// by depth 12, each one alone first.
        /// </summary>
        static readonly RosterEntry[] Roster =
        {
            new RosterEntry("ronin", 0, 1.0f),
            new RosterEntry("lanceiro", 1, 1.2f),
            new RosterEntry("lodo", 1, 1.1f),
            new RosterEntry("kenkaku", 2, 1.4f),
            new RosterEntry("kunoichi", 2, 1.3f),
            new RosterEntry("escudeiro", 3, 1.4f),
            new RosterEntry("lanterna", 3, 1.2f, false),
            new RosterEntry("arqueiro", 4, 1.3f),
            new RosterEntry("kappa", 4, 1.4f),
            new RosterEntry("monge", 5, 2.0f),
            new RosterEntry("bombardeiro", 5, 1.4f),
            new RosterEntry("sombra", 6, 1.8f),
            new RosterEntry("onryo", 6, 1.6f),
            new RosterEntry("carrasco", 7, 2.5f),
            new RosterEntry("curandeira", 7, 1.5f, false),
            new RosterEntry("tengu", 8, 1.9f),
            new RosterEntry("boxeador", 8, 1.6f),
            new RosterEntry("ciclope", 9, 2.6f),
            new RosterEntry("vampiro", 9, 1.8f),
            new RosterEntry("onmyoji", 10, 1.9f),
            new RosterEntry("olho", 10, 1.5f, false),
            new RosterEntry("raijin", 11, 1.8f),
            new RosterEntry("esqueleto", 11, 2.0f),
            new RosterEntry("berserker", 12, 2.2f),
        };

        static readonly string[] Places =
        {
            "Pátio", "Ponte", "Templo", "Salão", "Jardim", "Cripta", "Torre", "Claustro", "Arena", "Santuário", "Passagem", "Átrio",
        };

        public const int GuardianEvery = 5;
        const float MaxBudget = 6.5f;

        public static bool IsGuardianDepth(int depth) => depth % GuardianEvery == GuardianEvery - 1;

        public static int UnlockDepth(string id)
        {
            foreach (RosterEntry r in Roster)
            {
                if (r.id == id) return r.unlockDepth;
            }
            return int.MaxValue;
        }

        /// <summary>The first enemy introduced at this depth (null if none).</summary>
        public static string NewEnemyAt(int depth)
        {
            List<string> all = NewEnemiesAt(depth);
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>Every enemy introduced at this depth, in the order they are met.</summary>
        public static List<string> NewEnemiesAt(int depth)
        {
            var list = new List<string>();
            if (depth <= 0) return list;
            foreach (RosterEntry r in Roster)
            {
                if (r.unlockDepth == depth) list.Add(r.id);
            }
            return list;
        }

        public static IEnumerable<string> RosterIds()
        {
            foreach (RosterEntry r in Roster) yield return r.id;
        }

        public static RoomDef Intro()
        {
            return new RoomDef("Pátio das Cinzas", 0, Biomes.All[0], 24f, 13f, false, new[]
            {
                new[] { new EnemySpawn("ronin") },
                new[] { new EnemySpawn("ronin"), new EnemySpawn("ronin") },
            });
        }

        public static RoomDef Generate(ulong runSeed, int depth) => Generate(runSeed, depth, RouteMap.Default(runSeed, depth));

        /// <summary>The room behind one door of the route map.</summary>
        public static RoomDef Generate(ulong runSeed, int depth, RouteNode node)
        {
            if (node.kind == RoomKind.RivalLair && depth > 0) return Lair(runSeed, depth, node);
            RoomDef room = depth <= 0 ? Intro() : GenerateCore(runSeed, depth, node);
            room.kind = depth <= 0 ? RoomKind.Combat : node.kind;
            room.hint = node.hint;
            room.challenge = node.challenge;
            room.eventId = node.eventId;
            room.time = Biomes.TimeAt(depth);
            var env = new SeededRandom(Mix(runSeed, depth + 3000 + node.index * 17));
            room.weather = depth < 2 || env.NextFloat() < 0.45f ? Weather.Clear : room.biome.weathers[env.Range(0, room.biome.weathers.Length)];
            // The full moon only rises at night.
            if (room.weather == Weather.FullMoon && room.time != TimeOfDay.Night) room.weather = Weather.Clear;
            bool fight = !room.IsEncounter;
            room.corrupted = fight && !room.guardian && depth >= 6 && env.NextFloat() < 0.15f;
            if (room.kind == RoomKind.Duel) room.standoff = true;
            if (!fight) room.standoff = false;
            return room;
        }

        /// <summary>A rival's lair: a duel arena, the rival alone (a General with its bodyguards).</summary>
        static RoomDef Lair(ulong runSeed, int depth, RouteNode node)
        {
            var duel = new RouteNode { depth = depth, index = node.index, column = node.column, kind = RoomKind.Duel, hint = RewardHint.Technique };
            RoomDef basis = Generate(runSeed, depth, duel);
            Rival r = null;
            foreach (Rival x in Rivals.Roster)
            {
                if (x.id == node.rivalId) r = x;
            }
            var wave = new List<EnemySpawn>();
            if (r != null)
            {
                wave.Add(new EnemySpawn(r.enemyId, true) { rivalId = r.id });
                for (int i = 0; i < Rivals.Guards(r); i++) wave.Add(new EnemySpawn(basis.waves.Length > 0 ? basis.waves[0][0].id : "ronin"));
            }
            else wave.AddRange(basis.waves.Length > 0 ? basis.waves[0] : new[] { new EnemySpawn("ronin") });
            string name = r != null ? "Covil de " + r.surname : basis.name;
            var room = new RoomDef(name, depth, basis.biome, 22f, 13f, false, new[] { wave.ToArray() }, ArenaShape.Octagon, basis.layoutSeed);
            room.CopyRoadFrom(basis);
            room.kind = RoomKind.RivalLair;
            room.rivalId = node.rivalId;
            room.hint = RewardHint.Technique;
            room.standoff = true;
            return room;
        }

        static RoomDef Encounter(ulong runSeed, int depth, RouteNode node)
        {
            Biome biome = Biomes.ForDepth(depth);
            string name = RouteMap.KindName(node.kind) + " " + biome.suffix;
            var room = new RoomDef(name, depth, biome, 20f, 12f, false, new EnemySpawn[0][], ArenaShape.Hall, Mix(runSeed, depth + 1000));
            return room;
        }

        static RoomDef GenerateCore(ulong runSeed, int depth, RouteNode node)
        {
            if (RouteMap.IsEncounter(node.kind)) return Encounter(runSeed, depth, node);
            var rng = new SeededRandom(Mix(runSeed, depth + node.index * 101));
            Biome biome = Biomes.ForDepth(depth);
            bool guardian = IsGuardianDepth(depth);
            int waveCount = Mathf.Clamp(2 + depth / 6, 2, 4);
            int maxPerWave = depth < 3 ? 2 : 3;
            float budget = Mathf.Min(MaxBudget, 1.4f + depth * 0.25f);
            List<string> newcomers = NewEnemiesAt(depth);
            // Room for every newcomer's solo wave before the Guardian.
            waveCount = Mathf.Max(waveCount, newcomers.Count + (guardian ? 1 : 0));

            var waves = new List<EnemySpawn[]>();
            if (node.kind == RoomKind.Duel)
            {
                // One against one: the newcomer if there is one, otherwise a heavy fighter.
                string duelist = newcomers.Count > 0 ? newcomers[0] : GuardianWave(rng, depth)[0].id;
                waves.Add(new[] { new EnemySpawn(duelist) });
                waveCount = 0;
            }
            if (node.kind == RoomKind.Dispute) waveCount = Mathf.Max(newcomers.Count + 1, waveCount - 1);
            for (int w = 0; w < waveCount; w++)
            {
                if (w < newcomers.Count && !(guardian && w == waveCount - 1))
                {
                    // A new enemy is always met alone first.
                    waves.Add(new[] { new EnemySpawn(newcomers[w]) });
                    continue;
                }
                if (guardian && w == waveCount - 1)
                {
                    waves.Add(GuardianWave(rng, depth));
                    continue;
                }
                if (node.kind == RoomKind.Dispute)
                {
                    // Two factions: one wave of yokai and one of humans arrive together.
                    var both = new List<EnemySpawn>(FillWave(rng, depth, budget * 0.75f, 2, Faction.Yokai));
                    both.AddRange(FillWave(rng, depth, budget * 0.75f, 2, Faction.Human));
                    waves.Add(both.ToArray());
                    continue;
                }
                waves.Add(FillWave(rng, depth, budget * (0.8f + 0.2f * w), maxPerWave, Faction.Any, Biomes.TimeAt(depth), biome.rule));
            }

            // Stepping by 7 through 12 places (coprime) never repeats a name within 12 rooms.
            int place = (int)((runSeed % (ulong)Places.Length + (ulong)depth * 7UL) % (ulong)Places.Length);
            string name = guardian ? "Covil do Guardião " + biome.suffix
                        : node.kind == RoomKind.Duel ? "Duelo " + biome.suffix
                        : node.kind == RoomKind.Dispute ? "Disputa " + biome.suffix
                        : Places[place] + " " + biome.suffix;
            // Bigger arenas than the first prototype, each with its own outline and traps.
            float width = rng.Range(22, 30);
            float height = rng.Range(12, 17);
            ArenaShape shape = guardian ? (rng.NextFloat() < 0.5f ? ArenaShape.Courtyard : ArenaShape.Octagon)
                                        : (ArenaShape)rng.Range(0, 5);
            // The castle: narrow halls.
            if (biome.rule == BiomeRule.Castle && !guardian) height = Mathf.Min(height, 12f);
            if (node.kind == RoomKind.Duel)
            {
                width = 20f;
                height = 12f;
                shape = ArenaShape.Octagon;
            }
            var room = new RoomDef(name, depth, biome, width, height, guardian, waves.ToArray(), shape, Mix(runSeed, depth + 1000));
            // One room in five (from depth 2) opens with a standoff.
            room.standoff = !guardian && node.kind != RoomKind.Dispute && depth >= 2 && new SeededRandom(Mix(runSeed, depth + 2000)).NextFloat() < 0.2f;
            return room;
        }

        public enum Faction
        {
            Any,
            Human,
            Yokai,
        }

        /// <summary>Spirits, beasts and demons (the night brings more of them; corruption feeds them).</summary>
        public static bool IsYokai(string id)
        {
            switch (id)
            {
                case "lanterna": case "onryo": case "kappa": case "tengu": case "ciclope": case "lodo": case "olho":
                case "vampiro": case "esqueleto": case "boxeador": case "raijin": case "hitodama": case "lodo_pequeno":
                case "raijin_sombra": case "berserker":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The dead: weak at dawn.</summary>
        public static bool IsUndead(string id) => id == "onryo" || id == "esqueleto" || id == "vampiro" || id == "hitodama";

        static bool IsNinja(string id) => id == "kunoichi" || id == "sombra" || id == "bombardeiro";

        static EnemySpawn[] FillWave(SeededRandom rng, int depth, float budget, int maxCount, Faction faction = Faction.Any,
                                     TimeOfDay time = TimeOfDay.Morning, BiomeRule rule = BiomeRule.Embers)
        {
            var wave = new List<EnemySpawn>();
            float remaining = budget;
            while (wave.Count < maxCount)
            {
                List<RosterEntry> options = Unlocked(depth, wave.Count == 0 ? float.MaxValue : remaining);
                if (faction != Faction.Any) options.RemoveAll(r => IsYokai(r.id) != (faction == Faction.Yokai));
                if (options.Count == 0) break;
                RosterEntry pick = WeightedPick(rng, options, time, rule);
                wave.Add(new EnemySpawn(pick.id));
                remaining -= pick.cost;
                if (remaining <= 0f) break;
            }
            return wave.ToArray();
        }

        static EnemySpawn[] GuardianWave(SeededRandom rng, int depth)
        {
            var heavy = new List<RosterEntry>();
            foreach (RosterEntry r in Unlocked(depth, float.MaxValue))
            {
                if (r.cost >= 1.3f && r.guardian) heavy.Add(r);
            }
            if (heavy.Count == 0) heavy = Unlocked(depth, float.MaxValue);
            RosterEntry boss = WeightedPick(rng, heavy);

            var wave = new List<EnemySpawn> { new EnemySpawn(boss.id, true) };
            if (depth >= 9) wave.Add(new EnemySpawn(Roster[rng.Range(0, 2)].id));
            return wave.ToArray();
        }

        static List<RosterEntry> Unlocked(int depth, float maxCost)
        {
            var list = new List<RosterEntry>();
            foreach (RosterEntry r in Roster)
            {
                if (r.unlockDepth <= depth && r.cost <= maxCost) list.Add(r);
            }
            return list;
        }

        /// <summary>
        /// Recently unlocked enemies show up more often, so the roster keeps rotating. Night
        /// favours yokai, the bamboo grove its ninjas, the Sanzu its kappas.
        /// </summary>
        static RosterEntry WeightedPick(SeededRandom rng, List<RosterEntry> options, TimeOfDay time = TimeOfDay.Morning, BiomeRule rule = BiomeRule.Embers)
        {
            float total = 0f;
            foreach (RosterEntry r in options) total += Weight(r, time, rule);
            float roll = rng.Range(0f, total);
            foreach (RosterEntry r in options)
            {
                roll -= Weight(r, time, rule);
                if (roll <= 0f) return r;
            }
            return options[options.Count - 1];
        }

        static float Weight(RosterEntry r, TimeOfDay time, BiomeRule rule)
        {
            float w = 1f + 0.3f * r.unlockDepth;
            if (time == TimeOfDay.Night) w *= IsYokai(r.id) ? 2f : 0.5f;
            if (rule == BiomeRule.Bamboo && IsNinja(r.id)) w *= 2.5f;
            if (rule == BiomeRule.River && r.id == "kappa") w *= 3f;
            return w;
        }

        public static ulong Mix(ulong seed, int depth)
        {
            unchecked
            {
                ulong z = seed ^ ((ulong)depth * 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                return z ^ (z >> 27);
            }
        }
    }
}
