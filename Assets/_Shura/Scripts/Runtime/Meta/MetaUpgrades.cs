using System;
using UnityEngine;

namespace Shura
{
    public sealed class MetaUpgradeDef
    {
        public readonly string id;
        public readonly string name;
        public readonly string description;
        public readonly int maxLevel;
        public readonly int baseCost;
        readonly Action<PlayerModifiers, int> apply;
        readonly Func<int, string> effect;

        public MetaUpgradeDef(string id, string name, string description, int maxLevel, int baseCost,
                              Action<PlayerModifiers, int> apply, Func<int, string> effect)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.maxLevel = maxLevel;
            this.baseCost = baseCost;
            this.apply = apply;
            this.effect = effect;
        }

        /// <summary>Each level costs 60% more than the first, rounded to 5.</summary>
        public int CostFor(int currentLevel)
        {
            return Mathf.RoundToInt(baseCost * (1f + 0.6f * currentLevel) / 5f) * 5;
        }

        public void Apply(PlayerModifiers m, int level)
        {
            if (level <= 0) return;
            apply(m, Mathf.Min(level, maxLevel));
        }

        public string EffectAt(int level) => level <= 0 ? "—" : effect(Mathf.Min(level, maxLevel));
    }

    /// <summary>The Altar das Almas catalogue. Caps keep skill as the main progression.</summary>
    public static class MetaUpgrades
    {
        public static readonly MetaUpgradeDef[] All =
        {
            new MetaUpgradeDef("vigor", "Vigor", "Mais vida máxima.", 5, 25,
                (m, l) => m.maxHealthBonus += 10f * l, l => "+" + 10 * l + " de vida"),
            new MetaUpgradeDef("postura", "Postura de Aço", "Mais postura máxima: aguenta mais bloqueios.", 4, 25,
                (m, l) => m.maxPostureBonus += 12f * l, l => "+" + 12 * l + " de postura"),
            new MetaUpgradeDef("lamina", "Lâmina Temperada", "Mais dano na vida dos inimigos.", 4, 35,
                (m, l) => m.damageMult *= 1f + 0.08f * l, l => "+" + 8 * l + "% de dano"),
            new MetaUpgradeDef("folego", "Fôlego", "Esquivas recarregam mais rápido.", 3, 30,
                (m, l) => m.dodgeRechargeMult *= 1f - 0.08f * l, l => "-" + 8 * l + "% de recarga"),
            new MetaUpgradeDef("meditacao", "Meditação", "Cura extra ao vencer cada sala.", 3, 30,
                (m, l) => m.roomClearHealBonus += 8f * l, l => "+" + 8 * l + " de cura por sala"),
            new MetaUpgradeDef("olhar", "Olhar Afiado", "Janela do parry perfeito maior.", 2, 60,
                (m, l) => m.perfectParryFrameBonus += l, l => "+" + l + (l == 1 ? " frame" : " frames")),
            new MetaUpgradeDef("despertar", "Despertar", "Começa cada run com Fluxo acumulado.", 2, 45,
                (m, l) => m.startFlowPoints += 3f * l, l => "Fluxo inicial " + l),
            new MetaUpgradeDef("visao", "Visão do Destino", "Uma opção a mais em toda escolha de recompensa.", 1, 120,
                (m, l) => m.extraRewardChoices += l, l => "+" + l + " opção"),
        };

        public static MetaUpgradeDef Get(string id)
        {
            foreach (MetaUpgradeDef def in All)
            {
                if (def.id == id) return def;
            }
            return null;
        }
    }

    /// <summary>How many Ecos a run is worth. Derived from run statistics, so it is always consistent.</summary>
    public static class MetaRules
    {
        public const int EcosPerFinisher = 3;
        public const int EcosPerPerfectParry = 1;
        public const int EcosPerCounter = 2;
        public const int EcosPerTechnique = 5;
        public const int EcosPerGuardian = 15;

        /// <summary>Deeper rooms are worth more, up to a cap so the Altar still takes many runs.</summary>
        public static int RoomEcos(int roomIndex) => 15 + 3 * Mathf.Min(roomIndex, 25);

        public static int EcosForRun(RunState run)
        {
            if (run == null) return 0;
            int ecos = 0;
            for (int i = 0; i < run.RoomsCleared; i++) ecos += RoomEcos(i);
            ecos += run.Finishers * EcosPerFinisher;
            ecos += run.PerfectParries * EcosPerPerfectParry;
            ecos += run.Counters * EcosPerCounter;
            ecos += run.TechniquesAbsorbed * EcosPerTechnique;
            ecos += run.ElitesKilled * EcosPerGuardian;
            return ecos;
        }
    }
}
