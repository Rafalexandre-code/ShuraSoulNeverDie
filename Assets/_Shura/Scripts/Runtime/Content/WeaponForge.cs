using System;
using System.Collections.Generic;

namespace Shura
{
    /// <summary>A tsuba (hand guard): one passive the weapon carries.</summary>
    public sealed class TsubaDef
    {
        public string id;
        public string name;
        public string text;
        public Action<PlayerModifiers> apply;
    }

    /// <summary>
    /// The modular forge (design doc §4.1): every weapon is a blade (damage, reach, combo), a grip
    /// (the swap strike and the finisher) and a tsuba (a passive). "katana+naginata+lua" is a
    /// katana blade on a naginata grip with the Moon tsuba — a Nagamaki.
    /// </summary>
    public static class WeaponForge
    {
        public const char Separator = '+';
        public const string NoTsuba = "nenhuma";

        public static readonly TsubaDef[] Tsubas =
        {
            new TsubaDef { id = NoTsuba, name = "Sem tsuba", text = "Nenhum efeito.", apply = m => { } },
            new TsubaDef { id = "lua", name = "Tsuba da Lua", text = "Parry perfeito planta um selo extra.", apply = m => m.extraSealsPerParry += 1 },
            new TsubaDef { id = "sol", name = "Tsuba do Sol", text = "+8% de dano.", apply = m => m.damageMult *= 1.08f },
            new TsubaDef { id = "ferro", name = "Tsuba de Ferro", text = "+20 de postura máxima.", apply = m => m.maxPostureBonus += 20f },
            new TsubaDef { id = "garca", name = "Tsuba da Garça", text = "Janela de parry perfeito +1 frame.", apply = m => m.perfectParryFrameBonus += 1 },
            new TsubaDef { id = "vento", name = "Tsuba do Vento", text = "+6% de velocidade.", apply = m => m.moveSpeedMult *= 1.06f },
            new TsubaDef { id = "sangue", name = "Tsuba de Sangue", text = "3% do dano vira vida.", apply = m => m.lifesteal += 0.03f },
            new TsubaDef { id = "trovao", name = "Tsuba do Trovão", text = "Esquivas recarregam 12% mais rápido.", apply = m => m.dodgeRechargeMult *= 0.88f },
            new TsubaDef { id = "raposa", name = "Tsuba da Raposa", text = "+6% de chance de crítico.", apply = m => m.critChance += 0.06f },
            new TsubaDef { id = "dragao", name = "Tsuba do Dragão", text = "O Fluxo sobe 15% mais rápido.", apply = m => m.flowGainMult *= 1.15f },
            new TsubaDef { id = "bronze", name = "Tsuba de Bronze", text = "+15 de vida máxima.", apply = m => m.maxHealthBonus += 15f },
            new TsubaDef { id = "carpa", name = "Tsuba da Carpa", text = "+5 de cura ao vencer cada sala.", apply = m => m.roomClearHealBonus += 5f },
            new TsubaDef { id = "lotus", name = "Tsuba do Lótus", text = "Finalizar cura 5.", apply = m => m.finisherHeal += 5f },
        };

        static readonly Dictionary<string, WeaponDef> Forged = new Dictionary<string, WeaponDef>();

        public static TsubaDef Tsuba(string id)
        {
            foreach (TsubaDef t in Tsubas)
            {
                if (t.id == id) return t;
            }
            return null;
        }

        public static int TsubaIndex(string id)
        {
            for (int i = 0; i < Tsubas.Length; i++)
            {
                if (Tsubas[i].id == id) return i;
            }
            return 0;
        }

        /// <summary>The id of a forged weapon; the plain blade's id when nothing was changed.</summary>
        public static string Compose(string blade, string grip, string tsuba)
        {
            if (!WeaponDefs.IsBlade(blade)) blade = WeaponDefs.Katana.id;
            if (!WeaponDefs.IsBlade(grip)) grip = blade;
            if (Tsuba(tsuba) == null) tsuba = NoTsuba;
            return grip == blade && tsuba == NoTsuba ? blade : blade + Separator + grip + Separator + tsuba;
        }

        public static void Split(string id, out string blade, out string grip, out string tsuba)
        {
            blade = grip = id;
            tsuba = NoTsuba;
            if (string.IsNullOrEmpty(id)) return;
            string[] parts = id.Split(Separator);
            blade = parts[0];
            grip = parts.Length > 1 ? parts[1] : blade;
            tsuba = parts.Length > 2 ? parts[2] : NoTsuba;
        }

        /// <summary>Builds (once) the weapon a forged id describes.</summary>
        public static bool TryCompose(string id, out WeaponDef weapon)
        {
            weapon = null;
            if (string.IsNullOrEmpty(id) || id.IndexOf(Separator) < 0) return false;
            if (Forged.TryGetValue(id, out weapon)) return true;
            Split(id, out string bladeId, out string gripId, out string tsubaId);
            if (!WeaponDefs.IsBlade(bladeId) || !WeaponDefs.IsBlade(gripId) || Tsuba(tsubaId) == null) return false;

            WeaponDef blade = WeaponDefs.Get(bladeId), grip = WeaponDefs.Get(gripId);
            weapon = blade.Clone();
            weapon.id = id;
            weapon.baseId = blade.id;
            weapon.gripId = grip.id;
            weapon.tsubaId = tsubaId == NoTsuba ? null : tsubaId;
            weapon.swapStrike = grip.swapStrike;
            weapon.swapPower = grip.swapPower;
            weapon.finisher = grip.finisher;
            weapon.finisherName = grip.finisherName;
            weapon.finisherPower = grip.finisherPower;
            weapon.name = ForgedName(blade, grip);
            Forged[id] = weapon;
            return true;
        }

        /// <summary>Some pairings have names of their own; the rest are "blade on a grip".</summary>
        public static string ForgedName(WeaponDef blade, WeaponDef grip)
        {
            if (blade == grip) return blade.name;
            string pair = blade.id + "/" + grip.id;
            switch (pair)
            {
                case "katana/naginata": return "Nagamaki";
                case "montante/naginata": return "Ōdachi de Haste";
                case "katana/wakizashi": return "Daishō";
                case "naginata/kusarigama": return "Lança-Corrente";
                case "kanabo/shakujo": return "Clava dos Sinos";
                case "gemeas/tessen": return "Leques de Lâmina";
                case "manoplas/kanabo": return "Punhos de Oni";
                case "wakizashi/katana": return "Kodachi";
                default: return blade.name + " · cabo de " + grip.name;
            }
        }
    }
}
