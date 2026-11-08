using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Balance for co-op parties of up to 8 duelists. The game stays a series of duels:
    /// every player gets enemies of their own (more enemies per wave, one attacker per
    /// player at a time) in an arena that grows with the party, while each enemy only gets a
    /// little tougher so a lone duel feels the same as in solo.
    /// </summary>
    public static class PartyScaling
    {
        public const int MaxPlayers = 8;

        /// <summary>Arena area grows ~16% per extra player (8 players ≈ 2.1× the floor).</summary>
        public static float AreaMult(int players) => 1f + 0.16f * (Mathf.Clamp(players, 1, MaxPlayers) - 1);

        /// <summary>Enemies per wave grow ~50% per extra player, capped so the arena never floods.</summary>
        public static int WaveSize(int baseCount, int players)
        {
            int n = Mathf.Clamp(players, 1, MaxPlayers);
            int target = Mathf.CeilToInt(baseCount * (1f + 0.5f * (n - 1)));
            return Mathf.Min(target, 3 + 2 * n);
        }

        public static float EnemyHealth(int players) => 1f + 0.08f * (Mathf.Clamp(players, 1, MaxPlayers) - 1);

        /// <summary>Guardians are the shared boss of the room: they scale much more.</summary>
        public static float GuardianHealth(int players) => 1f + 0.35f * (Mathf.Clamp(players, 1, MaxPlayers) - 1);

        /// <summary>One active attacker per player (capped), so duels run in parallel.</summary>
        public static int SimultaneousAttackers(int players) => Mathf.Clamp(players, 1, 6);

        public static int AttackGap(int baseGap, int players) =>
            Mathf.Max(4, Mathf.RoundToInt(baseGap / Mathf.Sqrt(Mathf.Clamp(players, 1, MaxPlayers))));

        /// <summary>The room as the party will meet it: bigger floor, fuller waves (deterministic).</summary>
        public static RoomDef Scale(RoomDef room, int players)
        {
            if (players <= 1) return room;
            float side = Mathf.Sqrt(AreaMult(players));
            var rng = new SeededRandom(room.layoutSeed ^ ((ulong)players * 0x9E37UL) ^ (ulong)room.depth);
            var waves = new EnemySpawn[room.waves.Length][];
            for (int w = 0; w < room.waves.Length; w++)
            {
                EnemySpawn[] src = room.waves[w];
                var list = new List<EnemySpawn>(src);
                var pool = new List<EnemySpawn>();
                foreach (EnemySpawn s in src)
                {
                    if (!s.elite) pool.Add(s);
                }
                int target = WaveSize(src.Length, players);
                while (list.Count < target)
                {
                    // Extra enemies mirror the wave's own line-up; a lone Guardian brings escorts.
                    EnemySpawn copy = pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : new EnemySpawn(rng.NextFloat() < 0.5f ? "ronin" : "lanceiro");
                    list.Add(new EnemySpawn(copy.id));
                }
                waves[w] = list.ToArray();
            }
            return new RoomDef(room.name, room.depth, room.biome, room.width * side, room.height * side, room.guardian, waves,
                               room.shape, room.layoutSeed);
        }
    }
}
