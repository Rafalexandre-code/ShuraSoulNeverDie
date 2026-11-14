using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>The ways to walk the road (design doc §7.8).</summary>
    public enum RunMode
    {
        Journey,
        Daily,
        Tower,
        Master,
        Pilgrimage,
    }

    [Serializable]
    public sealed class DailyResult
    {
        public string date;
        public int depth;
        public int ecos;
    }

    /// <summary>
    /// Jornada (the endless road), Desafio Diário (the day's seed, one try, its own ranking),
    /// Torre dos Cem Duelos (only duels, no items, a hundred floors), Modo Mestre (no timing
    /// rings) and Peregrinação (a fixed road of 50 rooms with an ending).
    /// </summary>
    public static class GameModes
    {
        public const int TowerFloors = 100;
        public const int PilgrimageRooms = 50;
        public const ulong PilgrimageSeed = 108UL;

        public static string Name(RunMode m)
        {
            switch (m)
            {
                case RunMode.Daily: return "Desafio Diário";
                case RunMode.Tower: return "Torre dos Cem Duelos";
                case RunMode.Master: return "Modo Mestre";
                case RunMode.Pilgrimage: return "Peregrinação";
                default: return "Jornada";
            }
        }

        public static string Kanji(RunMode m)
        {
            switch (m)
            {
                case RunMode.Daily: return "日";
                case RunMode.Tower: return "塔";
                case RunMode.Master: return "盲";
                case RunMode.Pilgrimage: return "巡";
                default: return "道";
            }
        }

        public static string Text(RunMode m)
        {
            switch (m)
            {
                case RunMode.Daily: return "A mesma estrada para todos hoje. Uma única tentativa. Ecos ×1,2.";
                case RunMode.Tower: return "Só duelos, sem itens, cem andares até o topo.";
                case RunMode.Master: return "Sem anéis de tempo: só a animação e o som. Ecos ×1,5.";
                case RunMode.Pilgrimage: return "Uma estrada fixa de 50 salas, com um final que depende do seu Karma.";
                default: return "A estrada sem fim do Shuradō, com mapa de rotas e Kalpas.";
            }
        }

        public static float EcosMult(RunMode m) => m == RunMode.Master ? 1.5f : m == RunMode.Daily ? 1.2f : 1f;

        /// <summary>Rooms that end the road in victory (0 = the road never ends).</summary>
        public static int Goal(RunMode m) => m == RunMode.Tower ? TowerFloors : m == RunMode.Pilgrimage ? PilgrimageRooms : 0;

        public static string DateKey(DateTime day) => day.ToString("yyyy-MM-dd");

        /// <summary>The day's seed: the same for every player on the same date.</summary>
        public static ulong DailySeed(DateTime day)
        {
            ulong h = 1469598103934665603UL;
            foreach (char c in "shura-diario-" + DateKey(day))
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        public static bool DailyTried(MetaProgress meta, DateTime day) => meta.lastDaily == DateKey(day);

        /// <summary>Your days, the deepest first (the local ranking).</summary>
        public static List<DailyResult> Ranking(MetaProgress meta)
        {
            var list = new List<DailyResult>(meta.dailyResults);
            list.Sort((a, b) => b.depth != a.depth ? b.depth.CompareTo(a.depth) : string.CompareOrdinal(b.date, a.date));
            return list;
        }

        public static void RecordDaily(MetaProgress meta, DateTime day, int depth, int ecos)
        {
            meta.lastDaily = DateKey(day);
            meta.dailyResults.RemoveAll(d => d.date == meta.lastDaily);
            meta.dailyResults.Add(new DailyResult { date = meta.lastDaily, depth = depth, ecos = ecos });
            // Keep the last 60 days.
            meta.dailyResults.Sort((a, b) => string.CompareOrdinal(a.date, b.date));
            while (meta.dailyResults.Count > 60) meta.dailyResults.RemoveAt(0);
        }

        // ── The pilgrimage's ending (§8.6, for the road that ends) ─────────────────

        public const string EndingLiberation = "libertacao";
        public const string EndingShura = "novo_ashura";
        public const string EndingWheel = "roda_eterna";

        /// <summary>The persistent Karma plus what this road added decides which door opens at the end.</summary>
        public static string EndingFor(int karma) => karma > 60 ? EndingLiberation : karma <= -60 ? EndingShura : EndingWheel;

        public static string EndingTitle(string ending)
        {
            switch (ending)
            {
                case Endings.Secret: return "AS CAMÉLIAS CAEM INTEIRAS";
                case EndingLiberation: return "LIBERTAÇÃO";
                case EndingShura: return "O NOVO ASHURA";
                default: return "RODA ETERNA";
            }
        }

        public static string EndingText(string ending)
        {
            switch (ending)
            {
                case Endings.Secret:
                    return "O Ashura cai, e atrás do trono está Tsubaki, com a lâmina baixa. \"Os seus Generais também caíram. Não sobrou ninguém para nos prender aqui.\" " +
                           "Vocês dois saem pelo portão do Shuradō como as camélias caem: inteiras, sem perder uma pétala.";
                case EndingLiberation:
                    return "No fim da estrada não há inimigo. Há um rio, e do outro lado ninguém segura uma espada. " +
                           "Você pousa a lâmina na margem, e pela primeira vez em mil vidas ela não pede sangue. A roda para de girar. Você atravessa.";
                case EndingShura:
                    return "No fim da estrada há um trono, e o trono está vazio. As almas que você carrega sussurram: sente-se. " +
                           "Você se senta. Lá embaixo, outro samurai acaba de renascer, e já sabe o seu nome.";
                default:
                    return "No fim da estrada há um portão, e atrás dele, a mesma estrada. Você sorri: conhece cada pedra. " +
                           "A roda gira de novo, e desta vez você não a teme. Ela é sua casa, e você é a lâmina dela.";
            }
        }
    }

    /// <summary>The Karma's weight on the road (design doc §8.3).</summary>
    public static class KarmaRules
    {
        /// <summary>Bushidō favours defensive relics, Shura aggressive ones.</summary>
        static readonly string[] Defensive =
        {
            RewardIds.IronGuard, RewardIds.LightSteps, RewardIds.HawkEyes, RoadRelics.StrawSandals, RoadRelics.VowOfSilence,
            RoadRelics.SixMonCoin, RoadRelics.DragonEye, RoadRelics.BrokenHourglass,
        };

        static readonly string[] Aggressive =
        {
            RewardIds.HungryBlade, RewardIds.BloodOath, RewardIds.RelentlessRhythm, RewardIds.BurningFlow,
            RoadRelics.HannyaMask, RoadRelics.ExecutionerChain,
        };

        public const int Lean = 30;

        public static bool IsDefensive(string relic) => Array.IndexOf(Defensive, relic) >= 0;
        public static bool IsAggressive(string relic) => Array.IndexOf(Aggressive, relic) >= 0;

        /// <summary>The Karma a run carries: what the soul brought plus what this road has done.</summary>
        public static int Of(RunState run) => Mathf.Clamp((run.Meta != null ? run.Meta.karma : 0) + run.Karma * Legacy.KarmaWeight, -100, 100);

        /// <summary>
        /// Picks a relic from the pool. Leaning to one side, a relic of the other side is
        /// drawn again once (60%), so the offers carry the soul's colour.
        /// </summary>
        public static int PickRelic(List<string> pool, SeededRandom rng, int karma)
        {
            int index = rng.Range(0, pool.Count);
            if (karma > -Lean && karma < Lean || pool.Count < 2) return index;
            bool wrong = karma >= Lean ? IsAggressive(pool[index]) : IsDefensive(pool[index]);
            if (wrong && rng.NextFloat() < 0.6f) index = rng.Range(0, pool.Count);
            return index;
        }

        static readonly int[] BushidoEvents = { 0, 2, 3, 5, 7 };
        static readonly int[] ShuraEvents = { 1, 4, 6 };

        /// <summary>The persistent Karma at the start of the run (the road's events lean with it).</summary>
        public static int RoadKarma;
        /// <summary>Tsubaki's mission: honour duels come more often.</summary>
        public static bool HonorDuels;

        /// <summary>
        /// The event behind a door: Bushidō meets more monks, Jizō, ronin, onsen and honour;
        /// Shura more foxes, dice and the Muramasa.
        /// </summary>
        public static int Event(ulong hash, int count)
        {
            int e = (int)(hash % (ulong)count);
            ulong h = hash / 8UL;
            if (HonorDuels && h % 5UL == 0UL) return 7 % count;
            if (RoadKarma >= Lean && Array.IndexOf(ShuraEvents, e) >= 0 && h % 2UL == 0UL) return BushidoEvents[(int)(h / 2UL % (ulong)BushidoEvents.Length)] % count;
            if (RoadKarma <= -Lean && Array.IndexOf(BushidoEvents, e) >= 0 && h % 2UL == 0UL) return ShuraEvents[(int)(h / 2UL % (ulong)ShuraEvents.Length)] % count;
            return e;
        }
    }
}
