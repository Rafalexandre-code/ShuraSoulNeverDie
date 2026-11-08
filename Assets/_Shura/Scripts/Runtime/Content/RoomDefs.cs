using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    public struct EnemySpawn
    {
        public string id;
        /// <summary>A Guardian: bigger, tougher, worth extra Ecos.</summary>
        public bool elite;

        public EnemySpawn(string id, bool elite = false)
        {
            this.id = id;
            this.elite = elite;
        }
    }

    /// <summary>Visual identity of a stretch of depth.</summary>
    public sealed class Biome
    {
        public string suffix;
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
                suffix = "das Cinzas", floorA = ColorUtil.Hex("#23212C"), floorB = ColorUtil.Hex("#272432"),
                mortar = ColorUtil.Hex("#1A1921"), wall = ColorUtil.Hex("#3D3847"), wallEdge = ColorUtil.Hex("#5A5266"),
                flame = ColorUtil.Hex("#F2A541"),
            },
            new Biome
            {
                suffix = "do Musgo", floorA = ColorUtil.Hex("#1F2622"), floorB = ColorUtil.Hex("#232B25"),
                mortar = ColorUtil.Hex("#161C18"), wall = ColorUtil.Hex("#34433A"), wallEdge = ColorUtil.Hex("#4F6455"),
                flame = ColorUtil.Hex("#B8D46A"),
            },
            new Biome
            {
                suffix = "do Sangue", floorA = ColorUtil.Hex("#2A1C1E"), floorB = ColorUtil.Hex("#2F2023"),
                mortar = ColorUtil.Hex("#1E1416"), wall = ColorUtil.Hex("#4A2B2E"), wallEdge = ColorUtil.Hex("#6E3A3F"),
                flame = ColorUtil.Hex("#FF6B4A"),
            },
            new Biome
            {
                suffix = "do Abismo", floorA = ColorUtil.Hex("#1E1C2E"), floorB = ColorUtil.Hex("#232038"),
                mortar = ColorUtil.Hex("#151424"), wall = ColorUtil.Hex("#362F55"), wallEdge = ColorUtil.Hex("#54497D"),
                flame = ColorUtil.Hex("#A58BFF"),
            },
            new Biome
            {
                suffix = "do Gelo", floorA = ColorUtil.Hex("#1E2630"), floorB = ColorUtil.Hex("#222B37"),
                mortar = ColorUtil.Hex("#161D25"), wall = ColorUtil.Hex("#36475A"), wallEdge = ColorUtil.Hex("#557089"),
                flame = ColorUtil.Hex("#8FD3FF"),
            },
        };

        public static Biome ForDepth(int depth) => All[(depth / RoomsPerBiome) % All.Length];
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

        public static RoomDef Generate(ulong runSeed, int depth)
        {
            if (depth <= 0) return Intro();

            var rng = new SeededRandom(Mix(runSeed, depth));
            Biome biome = Biomes.ForDepth(depth);
            bool guardian = IsGuardianDepth(depth);
            int waveCount = Mathf.Clamp(2 + depth / 6, 2, 4);
            int maxPerWave = depth < 3 ? 2 : 3;
            float budget = Mathf.Min(MaxBudget, 1.4f + depth * 0.25f);
            List<string> newcomers = NewEnemiesAt(depth);
            // Room for every newcomer's solo wave before the Guardian.
            waveCount = Mathf.Max(waveCount, newcomers.Count + (guardian ? 1 : 0));

            var waves = new List<EnemySpawn[]>();
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
                waves.Add(FillWave(rng, depth, budget * (0.8f + 0.2f * w), maxPerWave));
            }

            // Stepping by 7 through 12 places (coprime) never repeats a name within 12 rooms.
            int place = (int)((runSeed % (ulong)Places.Length + (ulong)depth * 7UL) % (ulong)Places.Length);
            string name = guardian ? "Covil do Guardião " + biome.suffix : Places[place] + " " + biome.suffix;
            // Bigger arenas than the first prototype, each with its own outline and traps.
            float width = rng.Range(22, 30);
            float height = rng.Range(12, 17);
            ArenaShape shape = guardian ? (rng.NextFloat() < 0.5f ? ArenaShape.Courtyard : ArenaShape.Octagon)
                                        : (ArenaShape)rng.Range(0, 5);
            return new RoomDef(name, depth, biome, width, height, guardian, waves.ToArray(), shape, Mix(runSeed, depth + 1000));
        }

        static EnemySpawn[] FillWave(SeededRandom rng, int depth, float budget, int maxCount)
        {
            var wave = new List<EnemySpawn>();
            float remaining = budget;
            while (wave.Count < maxCount)
            {
                List<RosterEntry> options = Unlocked(depth, wave.Count == 0 ? float.MaxValue : remaining);
                if (options.Count == 0) break;
                RosterEntry pick = WeightedPick(rng, options);
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

        /// <summary>Recently unlocked enemies show up more often, so the roster keeps rotating.</summary>
        static RosterEntry WeightedPick(SeededRandom rng, List<RosterEntry> options)
        {
            float total = 0f;
            foreach (RosterEntry r in options) total += 1f + 0.3f * r.unlockDepth;
            float roll = rng.Range(0f, total);
            foreach (RosterEntry r in options)
            {
                roll -= 1f + 0.3f * r.unlockDepth;
                if (roll <= 0f) return r;
            }
            return options[options.Count - 1];
        }

        static ulong Mix(ulong seed, int depth)
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
