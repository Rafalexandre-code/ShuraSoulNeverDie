using System.Collections.Generic;

namespace Shura
{
    public sealed class RoomDef
    {
        public readonly string name;
        /// <summary>Each wave is a list of enemy ids; the next wave arrives when the current one is dead.</summary>
        public readonly string[][] waves;

        public RoomDef(string name, string[][] waves)
        {
            this.name = name;
            this.waves = waves;
        }
    }

    /// <summary>
    /// A run is 6 rooms: a fixed teaching room, then rooms drawn (seeded, no repeats) from
    /// three difficulty tiers. Tier 1 introduces one new enemy alone before pairing it;
    /// tier 2 brings the hardest mechanics; tier 3 mixes everything.
    /// </summary>
    public static class RoomDefs
    {
        public const int RunLength = 6;

        public static readonly RoomDef Intro = new RoomDef("Pátio das Cinzas", new[]
        {
            new[] { "ronin" },
            new[] { "ronin", "ronin" },
        });

        public static readonly RoomDef[] Tier1 =
        {
            new RoomDef("Ponte das Lanças", new[] { new[] { "lanceiro" }, new[] { "ronin", "lanceiro" } }),
            new RoomDef("Jardim das Lâminas", new[] { new[] { "kenkaku" }, new[] { "kenkaku", "ronin" } }),
            new RoomDef("Muralha Partida", new[] { new[] { "escudeiro" }, new[] { "escudeiro", "lanceiro" } }),
            new RoomDef("Torre de Vigia", new[] { new[] { "arqueiro" }, new[] { "ronin", "arqueiro" } }),
        };

        public static readonly RoomDef[] Tier2 =
        {
            new RoomDef("Templo do Sino", new[] { new[] { "monge" }, new[] { "monge", "ronin" } }),
            new RoomDef("Bosque Sombrio", new[] { new[] { "sombra" }, new[] { "sombra", "kenkaku" } }),
            new RoomDef("Cadafalso", new[] { new[] { "carrasco" }, new[] { "carrasco", "arqueiro" } }),
            new RoomDef("Pátio dos Ecos", new[] { new[] { "escudeiro", "arqueiro" }, new[] { "sombra", "lanceiro" } }),
        };

        public static readonly RoomDef[] Tier3 =
        {
            new RoomDef("Portão Shura", new[] { new[] { "lanceiro", "ronin" }, new[] { "monge", "lanceiro", "ronin" } }),
            new RoomDef("Salão dos Mil Duelos", new[] { new[] { "kenkaku", "sombra" }, new[] { "carrasco", "kenkaku", "arqueiro" } }),
            new RoomDef("Coração do Reino", new[] { new[] { "escudeiro", "monge" }, new[] { "sombra", "carrasco", "lanceiro" } }),
        };

        public static IEnumerable<RoomDef> AllRooms()
        {
            yield return Intro;
            foreach (RoomDef r in Tier1) yield return r;
            foreach (RoomDef r in Tier2) yield return r;
            foreach (RoomDef r in Tier3) yield return r;
        }

        public static List<RoomDef> BuildRun(SeededRandom rng)
        {
            var rooms = new List<RoomDef> { Intro };
            rooms.AddRange(Pick(Tier1, 2, rng));
            rooms.AddRange(Pick(Tier2, 2, rng));
            rooms.AddRange(Pick(Tier3, 1, rng));
            return rooms;
        }

        static List<RoomDef> Pick(RoomDef[] pool, int count, SeededRandom rng)
        {
            var bag = new List<RoomDef>(pool);
            var picked = new List<RoomDef>();
            while (picked.Count < count && bag.Count > 0)
            {
                int i = rng.Range(0, bag.Count);
                picked.Add(bag[i]);
                bag.RemoveAt(i);
            }
            return picked;
        }
    }
}
