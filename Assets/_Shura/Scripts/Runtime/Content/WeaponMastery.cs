using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Weapon mastery (design doc §4.3): five levels per weapon, earned by finishing enemies
    /// with it, kept across runs. Each level unlocks something you can feel: a heavier blow on
    /// the posture, a finisher that returns a spirit paper, a stronger swap strike, the blade's
    /// own colour, and at the top the Ougi — the style's secret strike, once per room.
    /// </summary>
    public static class WeaponMastery
    {
        public const int MaxLevel = 5;
        static readonly int[] Thresholds = { 5, 15, 30, 50, 80 };
        static readonly string[] Numerals = { "—", "I", "II", "III", "IV", "V" };

        public static int LevelFor(int finishers)
        {
            int level = 0;
            while (level < MaxLevel && finishers >= Thresholds[level]) level++;
            return level;
        }

        /// <summary>Finishers needed for the next level (or -1 at the top).</summary>
        public static int NextAt(int level) => level >= MaxLevel ? -1 : Thresholds[level];

        /// <summary>Where the progress bar starts for this level.</summary>
        public static int StartAt(int level) => level <= 0 ? 0 : Thresholds[level - 1];

        public static string Numeral(int level) => Numerals[Mathf.Clamp(level, 0, MaxLevel)];

        public static string Unlock(int level)
        {
            switch (level)
            {
                case 1: return "Golpe de postura: +15% de postura com esta arma.";
                case 2: return "Segunda finalização: finalizar com ela devolve 1 papel espiritual.";
                case 3: return "Elo extra: o golpe especial da troca causa +20% de dano.";
                case 4: return "Cor da lâmina: o rastro ganha a cor do estilo.";
                case 5: return "Ougi: o primeiro golpe especial de cada sala vira o golpe secreto (2,5× dano e postura).";
                default: return string.Empty;
            }
        }

        public static string OugiName(WeaponDef w)
        {
            switch (w.id)
            {
                case "katana": return "Ougi · Lua Partida";
                case "montante": return "Ougi · Montanha que Cai";
                case "gemeas": return "Ougi · Tempestade de Pétalas";
                case "naginata": return "Ougi · Dragão Ascendente";
                case "kusarigama": return "Ougi · Foice do Ceifador";
                case "manoplas": return "Ougi · Punho dos Mil Céus";
                case "tessen": return "Ougi · Vento dos Mil Leques";
                case "kanabo": return "Ougi · Fúria do Oni Rei";
                case "shakujo": return "Ougi · Sino dos Mundos";
                case "wakizashi": return "Ougi · Sombra sem Dono";
                default: return "Ougi";
            }
        }

        /// <summary>The colour of a mastered blade's trail.</summary>
        public static Color BladeColor(WeaponDef w)
        {
            switch (w.id)
            {
                case "katana": return ColorUtil.Hex("#BFE3FF");
                case "montante": return ColorUtil.Hex("#FFB347");
                case "gemeas": return ColorUtil.Hex("#FF8FB8");
                case "naginata": return ColorUtil.Hex("#8CF0D2");
                case "kusarigama": return ColorUtil.Hex("#C7A6FF");
                case "manoplas": return ColorUtil.Hex("#FF6B3D");
                case "tessen": return ColorUtil.Hex("#A8FFB0");
                case "kanabo": return ColorUtil.Hex("#FF4040");
                case "shakujo": return ColorUtil.Hex("#FFE38A");
                case "wakizashi": return ColorUtil.Hex("#7D7DFF");
                default: return Color.white;
            }
        }
    }
}
