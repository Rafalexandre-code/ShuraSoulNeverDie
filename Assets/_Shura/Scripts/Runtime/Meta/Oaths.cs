using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>A voluntary difficulty sworn in the Zen Garden (design doc §7.4).</summary>
    public sealed class OathDef
    {
        public string id;
        public string name;
        public string kanji;
        public int maxLevel;
        /// <summary>Calor per level.</summary>
        public int heat;
        /// <summary>What each level does (index 0 = level 1).</summary>
        public string[] levels;
        /// <summary>Needs the Rivals (Phase 5): shown, but cannot be sworn yet.</summary>
        public bool sealedUntilRivals;
    }

    /// <summary>
    /// The oaths and their Calor. Calor multiplies the Ecos and the petals a run brings home.
    /// The run copies the oaths when it starts (<see cref="OathSet"/>), so the garden can be
    /// changed between runs without touching one already on the road.
    /// </summary>
    public static class Oaths
    {
        public const string SingleBlade = "lamina_unica";
        public const string SilentRing = "anel_silencioso";
        public const string FrailFlesh = "carne_fragil";
        public const string Hunger = "fome";
        public const string Haste = "pressa";
        public const string EternalRival = "rival_eterno";
        public const string DeepReading = "leitura_profunda";
        public const string BloodPath = "caminho_de_sangue";
        public const string DemonBell = "sino_do_demonio";
        public const string EarlyKalpa = "kalpa_precoce";

        public static readonly OathDef[] All =
        {
            new OathDef { id = SingleBlade, name = "Lâmina Única", kanji = "一", maxLevel = 2, heat = 2,
                levels = new[] { "Leva só uma arma.", "Uma arma, e sem Troca Perfeita." } },
            new OathDef { id = SilentRing, name = "Anel Silencioso", kanji = "無", maxLevel = 3, heat = 1,
                levels = new[] { "20% dos golpes não mostram anel.", "40% dos golpes não mostram anel.", "60% dos golpes não mostram anel." } },
            new OathDef { id = FrailFlesh, name = "Carne Frágil", kanji = "脆", maxLevel = 3, heat = 1,
                levels = new[] { "−20% de vida máxima.", "−40% de vida máxima.", "−60% de vida máxima." } },
            new OathDef { id = Hunger, name = "Fome", kanji = "飢", maxLevel = 1, heat = 2,
                levels = new[] { "Nenhuma cura ao vencer uma sala." } },
            new OathDef { id = Haste, name = "Pressa", kanji = "急", maxLevel = 2, heat = 2,
                levels = new[] { "Cada sala tem 150 s; depois, a vida escorre.", "Cada sala tem 90 s; depois, a vida escorre." } },
            new OathDef { id = EternalRival, name = "Rival Eterno", kanji = "敵", maxLevel = 1, heat = 2,
                levels = new[] { "Rivais com +1 patente e os dois traços roubados de você." } },
            new OathDef { id = DeepReading, name = "Leitura Profunda", kanji = "読", maxLevel = 2, heat = 1,
                levels = new[] { "Os inimigos aprendem você 50% mais rápido.", "Os inimigos aprendem você 100% mais rápido." } },
            new OathDef { id = BloodPath, name = "Caminho de Sangue", kanji = "血", maxLevel = 2, heat = 2,
                levels = new[] { "+1 onda em cada sala de luta.", "+2 ondas em cada sala de luta." } },
            new OathDef { id = DemonBell, name = "Sino do Demônio", kanji = "鐘", maxLevel = 1, heat = 2,
                levels = new[] { "Elites ganham um afixo a mais, em qualquer profundidade." } },
            new OathDef { id = EarlyKalpa, name = "Kalpa Precoce", kanji = "劫", maxLevel = 1, heat = 3,
                levels = new[] { "Uma regra do Kalpa II já na profundidade 1." } },
        };

        public static OathDef Get(string id)
        {
            if (id == null) return null;
            foreach (OathDef o in All)
            {
                if (o.id == id) return o;
            }
            return null;
        }

        /// <summary>Total Calor of what the garden holds now.</summary>
        public static int Heat(MetaProgress meta)
        {
            int h = 0;
            foreach (OathDef o in All) h += o.heat * meta.OathLevel(o.id);
            return h;
        }

        /// <summary>Calor multiplies Ecos: +10% per point.</summary>
        public static float EcosMult(int heat) => 1f + 0.1f * heat;

        /// <summary>Seconds a room lasts under Pressa (0 = no limit).</summary>
        public static float HasteSeconds(int level) => level >= 2 ? 90f : level == 1 ? 150f : 0f;

        /// <summary>
        /// Petals a run brings home: one per challenge kept and one per five rooms, multiplied by
        /// Calor (+25% per point), plus the Calor itself for having sworn.
        /// </summary>
        public static int Petals(int heat, int roomsCleared, int challengesWon)
        {
            float basePetals = challengesWon + roomsCleared / 5;
            return Mathf.RoundToInt(basePetals * (1f + 0.25f * heat)) + (roomsCleared > 0 ? heat : 0);
        }
    }

    /// <summary>What the oaths do to the rooms themselves.</summary>
    public static class OathRules
    {
        /// <summary>
        /// "Caminho de Sangue": every fight gets more waves, copies of its own ordinary waves
        /// (the Guardian, if any, still comes last). Duels, encounters and the dojo are left alone.
        /// </summary>
        public static RoomDef WithExtraWaves(RoomDef room, int extra)
        {
            if (extra <= 0 || room == null || room.dojo || room.IsEncounter || room.kind == RoomKind.Duel || room.kind == RoomKind.RivalLair || room.waves == null || room.waves.Length == 0)
                return room;
            var plain = new List<EnemySpawn[]>();
            foreach (EnemySpawn[] w in room.waves)
            {
                var copy = new List<EnemySpawn>();
                foreach (EnemySpawn s in w)
                {
                    if (!s.elite) copy.Add(new EnemySpawn(s.id));
                }
                if (copy.Count > 0) plain.Add(copy.ToArray());
            }
            if (plain.Count == 0) plain.Add(new[] { new EnemySpawn("ronin"), new EnemySpawn("lanceiro") });
            var waves = new List<EnemySpawn[]>(room.waves);
            bool guardianLast = room.guardian && waves.Count > 1;
            for (int i = 0; i < extra; i++)
            {
                EnemySpawn[] add = plain[(plain.Count - 1 - i % plain.Count + plain.Count) % plain.Count];
                if (guardianLast) waves.Insert(waves.Count - 1, add);
                else waves.Add(add);
            }
            var bloody = new RoomDef(room.name, room.depth, room.biome, room.width, room.height, room.guardian, waves.ToArray(), room.shape, room.layoutSeed);
            bloody.CopyRoadFrom(room);
            return bloody;
        }
    }

    /// <summary>The oaths one run carries (copied from the garden when it starts).</summary>
    public sealed class OathSet
    {
        readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public int Heat { get; private set; }

        public static OathSet From(MetaProgress meta)
        {
            var set = new OathSet();
            if (meta == null) return set;
            foreach (OathDef o in Oaths.All)
            {
                int l = o.sealedUntilRivals ? 0 : Mathf.Clamp(meta.OathLevel(o.id), 0, o.maxLevel);
                if (l > 0) set.Set(o.id, l);
            }
            return set;
        }

        public void Set(string id, int level)
        {
            OathDef o = Oaths.Get(id);
            if (o == null) return;
            if (levels.TryGetValue(id, out int old)) Heat -= old * o.heat;
            levels[id] = level;
            Heat += level * o.heat;
        }

        public int Level(string id) => levels.TryGetValue(id, out int l) ? l : 0;

        public bool Any => Heat > 0;

        /// <summary>The oaths that shape each duelist's own numbers.</summary>
        public void Apply(PlayerModifiers m)
        {
            int frail = Level(Oaths.FrailFlesh);
            if (frail > 0) m.maxHealthMult *= 1f - 0.2f * frail;
            int reading = Level(Oaths.DeepReading);
            if (reading > 0) m.adaptMult *= 1f + 0.5f * reading;
            if (Level(Oaths.Hunger) > 0) m.noRoomHeal = true;
            int silent = Level(Oaths.SilentRing);
            if (silent > 0) m.silentRingChance = Mathf.Max(m.silentRingChance, 0.2f * silent);
            if (Level(Oaths.SingleBlade) >= 2) m.noSwapParry = true;
        }

        /// <summary>Short list for the HUD and the end screen ("Fome · Pressa II").</summary>
        public string Summary()
        {
            var parts = new List<string>();
            foreach (OathDef o in Oaths.All)
            {
                int l = Level(o.id);
                if (l <= 0) continue;
                parts.Add(o.maxLevel > 1 ? o.name + " " + TechniqueDef.Numeral(l) : o.name);
            }
            return string.Join(" · ", parts);
        }
    }
}
