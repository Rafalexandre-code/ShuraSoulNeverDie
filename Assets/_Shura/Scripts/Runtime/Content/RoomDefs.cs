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

        public RoomDef(string name, int depth, Biome biome, float width, float height, bool guardian, EnemySpawn[][] waves)
        {
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

            public RosterEntry(string id, int unlockDepth, float cost)
            {
                this.id = id;
                this.unlockDepth = unlockDepth;
                this.cost = cost;
            }
        }

        static readonly RosterEntry[] Roster =
        {
            new RosterEntry("ronin", 0, 1.0f),
            new RosterEntry("lanceiro", 1, 1.2f),
            new RosterEntry("kenkaku", 2, 1.4f),
            new RosterEntry("escudeiro", 3, 1.4f),
            new RosterEntry("arqueiro", 4, 1.3f),
            new RosterEntry("monge", 5, 2.0f),
            new RosterEntry("sombra", 6, 1.8f),
            new RosterEntry("carrasco", 7, 2.5f),
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

        public static string NewEnemyAt(int depth)
        {
            foreach (RosterEntry r in Roster)
            {
                if (r.unlockDepth == depth && depth > 0) return r.id;
            }
            return null;
        }

        public static IEnumerable<string> RosterIds()
        {
            foreach (RosterEntry r in Roster) yield return r.id;
        }

        public static RoomDef Intro()
        {
            return new RoomDef("Pátio das Cinzas", 0, Biomes.All[0], 19f, 10.5f, false, new[]
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
            string newcomer = NewEnemyAt(depth);

            var waves = new List<EnemySpawn[]>();
            for (int w = 0; w < waveCount; w++)
            {
                if (w == 0 && newcomer != null)
                {
                    // A new enemy is always met alone first.
                    waves.Add(new[] { new EnemySpawn(newcomer) });
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
            float width = rng.Range(17f, 22f);
            float height = rng.Range(9.5f, 12f);
            return new RoomDef(name, depth, biome, width, height, guardian, waves.ToArray());
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
                if (r.cost >= 1.3f) heavy.Add(r);
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
