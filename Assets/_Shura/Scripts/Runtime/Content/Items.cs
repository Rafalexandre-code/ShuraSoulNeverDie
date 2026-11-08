using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shura
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic,
        Ancestral,
        Divine,
        Celestial,
        Shura,
    }

    public enum ItemSlot
    {
        Amulet,
        Ring,
        Bracer,
        Talisman,
    }

    /// <summary>The element gives an item its look (colour, trail) and its attack variation.</summary>
    public enum Element
    {
        None,
        Fire,
        Frost,
        Lightning,
        Blood,
        Wind,
        Shadow,
    }

    public enum AffixStat
    {
        Damage,
        Posture,
        MaxHealth,
        MaxPosture,
        DodgeRecharge,
        FlowGain,
        RoomHeal,
        FinisherHeal,
        MoveSpeed,
        Crit,
        Lifesteal,
        ParryWindow,
    }

    public struct Affix
    {
        public AffixStat stat;
        public float value;
    }

    public static class Rarities
    {
        public const int Count = 10;

        static readonly string[] Names =
        {
            "Comum", "Incomum", "Raro", "Épico", "Lendário", "Mítico", "Ancestral", "Divino", "Celestial", "Shura",
        };

        static readonly Color[] Colors =
        {
            ColorUtil.Hex("#B8B8B8"), ColorUtil.Hex("#6FCF6F"), ColorUtil.Hex("#4FA3FF"), ColorUtil.Hex("#B06CFF"),
            ColorUtil.Hex("#F2A541"), ColorUtil.Hex("#FF5A5A"), ColorUtil.Hex("#3FD6C6"), ColorUtil.Hex("#FFE680"),
            ColorUtil.Hex("#9FE7FF"), ColorUtil.Hex("#FF2E63"),
        };

        static readonly float[] Power = { 1f, 1.25f, 1.5f, 1.8f, 2.1f, 2.5f, 2.9f, 3.4f, 4f, 5f };
        static readonly int[] AffixCounts = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };

        public static string Name(Rarity r) => Names[(int)r];
        public static Color Color(Rarity r) => Colors[(int)r];
        public static float PowerOf(Rarity r) => Power[(int)r];
        public static int AffixCount(Rarity r) => AffixCounts[(int)r];
        /// <summary>From Rare up, items carry their element's attack variation.</summary>
        public static bool HasProc(Rarity r) => r >= Rarity.Rare;
    }

    public static class Elements
    {
        public static string Suffix(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "da Brasa";
                case Element.Frost: return "do Inverno";
                case Element.Lightning: return "da Tempestade";
                case Element.Blood: return "Sanguinário";
                case Element.Wind: return "do Vendaval";
                case Element.Shadow: return "da Penumbra";
                default: return "do Duelista";
            }
        }

        public static string Name(Element e)
        {
            switch (e)
            {
                case Element.Fire: return "Fogo";
                case Element.Frost: return "Gelo";
                case Element.Lightning: return "Raio";
                case Element.Blood: return "Sangue";
                case Element.Wind: return "Vento";
                case Element.Shadow: return "Sombra";
                default: return "Aço";
            }
        }

        public static Color Color(Element e)
        {
            switch (e)
            {
                case Element.Fire: return ColorUtil.Hex("#FF7A2E");
                case Element.Frost: return ColorUtil.Hex("#8FD3FF");
                case Element.Lightning: return ColorUtil.Hex("#FFE14D");
                case Element.Blood: return ColorUtil.Hex("#D3203A");
                case Element.Wind: return ColorUtil.Hex("#9BE8B5");
                case Element.Shadow: return ColorUtil.Hex("#8B6BD6");
                default: return ColorUtil.Hex("#D8DEE9");
            }
        }

        /// <summary>What the element's attack variation does, scaled by item power.</summary>
        public static string ProcText(Element e, float power)
        {
            switch (e)
            {
                case Element.Fire: return "Golpes incendeiam: " + Mathf.RoundToInt(4f * power) + " de dano/s por 3 s.";
                case Element.Frost: return "Golpes congelam: o inimigo anda e ataca mais devagar por 2 s.";
                case Element.Lightning: return Mathf.RoundToInt(Mathf.Min(0.6f, 0.15f * power) * 100f) + "% de chance de raio em cadeia em 2 inimigos (" + Mathf.RoundToInt(6f * power) + " dano).";
                case Element.Blood: return "Golpes fazem sangrar (" + Mathf.RoundToInt(3f * power) + " dano/s por 4 s) e curam 1 de vida.";
                case Element.Wind: return "O 3º golpe do combo solta uma onda de corte (" + Mathf.RoundToInt(8f * power) + " dano).";
                case Element.Shadow: return "Parry perfeito invoca uma lâmina sombria no atacante (" + Mathf.RoundToInt(10f * power) + " dano, " + Mathf.RoundToInt(12f * power) + " postura).";
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// A dropped piece of equipment. Temporary: it lasts for the run. Implements the same
    /// hooks as techniques, so its effects plug into combat without special cases.
    /// </summary>
    public sealed class Item : RewardDef
    {
        static int nextId;

        readonly string id;
        readonly string name;
        public readonly ItemSlot slot;
        public readonly Rarity rarity;
        public readonly Element element;
        public readonly List<Affix> affixes = new List<Affix>();
        /// <summary>Strength of the element's attack variation.</summary>
        public readonly float power;

        public Item(ItemSlot slot, Rarity rarity, Element element, string baseName)
        {
            id = "item_" + (++nextId);
            this.slot = slot;
            this.rarity = rarity;
            this.element = element;
            power = Rarities.PowerOf(rarity);
            name = baseName + " " + Elements.Suffix(element);
        }

        public override string Id => id;
        public override RewardKind Kind => RewardKind.Item;
        public override string Name => name;
        public override bool Stackable => true;
        public override string Cost => "Nenhum. Dura até o fim da run.";
        public bool HasProc => Rarities.HasProc(rarity) && element != Element.None;

        public override string Gain
        {
            get
            {
                var sb = new StringBuilder();
                foreach (Affix a in affixes) sb.Append(ItemGenerator.Describe(a)).Append('\n');
                if (HasProc) sb.Append(Elements.ProcText(element, power));
                return sb.ToString().TrimEnd();
            }
        }

        public override void Apply(PlayerModifiers m)
        {
            foreach (Affix a in affixes)
            {
                switch (a.stat)
                {
                    case AffixStat.Damage: m.damageMult *= 1f + a.value; break;
                    case AffixStat.Posture: m.postureMult *= 1f + a.value; break;
                    case AffixStat.MaxHealth: m.maxHealthBonus += a.value; break;
                    case AffixStat.MaxPosture: m.maxPostureBonus += a.value; break;
                    case AffixStat.DodgeRecharge: m.dodgeRechargeMult *= 1f - a.value; break;
                    case AffixStat.FlowGain: m.flowGainMult *= 1f + a.value; break;
                    case AffixStat.RoomHeal: m.roomClearHealBonus += a.value; break;
                    case AffixStat.FinisherHeal: m.finisherHeal += a.value; break;
                    case AffixStat.MoveSpeed: m.moveSpeedMult *= 1f + a.value; break;
                    case AffixStat.Crit: m.critChance += a.value; break;
                    case AffixStat.Lifesteal: m.lifesteal += a.value; break;
                    case AffixStat.ParryWindow: m.perfectParryFrameBonus += Mathf.RoundToInt(a.value); break;
                }
            }
            if (HasProc && m.trailElement == Element.None) m.trailElement = element;
        }

        public override void OnDealtDamage(World world, Enemy target, float damage)
        {
            if (!HasProc) return;
            switch (element)
            {
                case Element.Fire:
                    target.ApplyDot(4f * power, 3f, Element.Fire);
                    break;
                case Element.Frost:
                    target.ApplyChill(2f);
                    break;
                case Element.Lightning:
                    if (world.Run.Rng.NextFloat() < Mathf.Min(0.6f, 0.15f * power)) world.ChainLightning(target, 2, 6f * power, 6f * power);
                    break;
                case Element.Blood:
                    target.ApplyDot(3f * power, 4f, Element.Blood);
                    world.Player.Heal(1f);
                    break;
            }
        }

        public override void OnSwing(World world, int comboIndex, Vector2 direction)
        {
            if (!HasProc || element != Element.Wind || comboIndex != 2) return;
            world.FireSlashWave(world.Player.Position, direction, 8f * power, 6f * power);
        }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (!HasProc || element != Element.Shadow) return;
            world.ShadowStrike(attacker, 10f * power, 12f * power);
        }
    }

    /// <summary>
    /// Rolls items. Rarity follows an exponential curve that depth and Guardians push toward
    /// the top: a Shura item is a legend at depth 1 and merely rare deep in the abyss.
    /// </summary>
    public static class ItemGenerator
    {
        static readonly string[][] BaseNames =
        {
            new[] { "Amuleto", "Pingente", "Colar" },
            new[] { "Anel", "Aro", "Selo" },
            new[] { "Bracelete", "Braçal", "Pulseira" },
            new[] { "Talismã", "Sinete", "Relicário" },
        };

        static readonly AffixStat[] CommonStats =
        {
            AffixStat.Damage, AffixStat.Posture, AffixStat.MaxHealth, AffixStat.MaxPosture, AffixStat.DodgeRecharge,
            AffixStat.FlowGain, AffixStat.RoomHeal, AffixStat.FinisherHeal, AffixStat.MoveSpeed, AffixStat.Crit, AffixStat.Lifesteal,
        };

        public static float DropChance(int depth, bool elite) => elite ? 1f : Mathf.Min(0.4f, 0.18f + 0.01f * depth);

        public static Rarity RollRarity(SeededRandom rng, int depth, bool elite)
        {
            float luck = depth * 0.03f + (elite ? 0.6f : 0f);
            float r = Mathf.Min(rng.NextFloat(), 0.99999f);
            int tier = Mathf.FloorToInt(-Mathf.Log(1f - r) / 0.9f * (1f + luck));
            return (Rarity)Mathf.Clamp(tier, 0, Rarities.Count - 1);
        }

        public static Item Roll(SeededRandom rng, int depth, bool elite) => RollAt(rng, RollRarity(rng, depth, elite), null);

        /// <summary>An item of a given rarity (forges, chests, Jizō); a fixed slot when re-engraving.</summary>
        public static Item RollAt(SeededRandom rng, Rarity rarity, ItemSlot? fixedSlot)
        {
            rarity = (Rarity)Mathf.Clamp((int)rarity, 0, Rarities.Count - 1);
            var slot = fixedSlot ?? (ItemSlot)rng.Range(0, 4);
            var element = (Element)rng.Range(0, 7);
            string[] names = BaseNames[(int)slot];
            var item = new Item(slot, rarity, element, names[rng.Range(0, names.Length)]);

            var pool = new List<AffixStat>(CommonStats);
            if (rarity >= Rarity.Legendary) pool.Add(AffixStat.ParryWindow);
            int count = Rarities.AffixCount(rarity);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = rng.Range(0, pool.Count);
                AffixStat stat = pool[index];
                pool.RemoveAt(index);
                item.affixes.Add(new Affix { stat = stat, value = BaseValue(stat) * item.power * rng.Range(0.8f, 1.2f) });
            }
            return item;
        }

        static float BaseValue(AffixStat stat)
        {
            switch (stat)
            {
                case AffixStat.Damage: return 0.05f;
                case AffixStat.Posture: return 0.06f;
                case AffixStat.MaxHealth: return 6f;
                case AffixStat.MaxPosture: return 6f;
                case AffixStat.DodgeRecharge: return 0.04f;
                case AffixStat.FlowGain: return 0.08f;
                case AffixStat.RoomHeal: return 3f;
                case AffixStat.FinisherHeal: return 3f;
                case AffixStat.MoveSpeed: return 0.025f;
                case AffixStat.Crit: return 0.03f;
                case AffixStat.Lifesteal: return 0.015f;
                case AffixStat.ParryWindow: return 0.4f;
                default: return 0f;
            }
        }

        public static string Describe(Affix a)
        {
            switch (a.stat)
            {
                case AffixStat.Damage: return "+" + Pct(a.value) + " de dano";
                case AffixStat.Posture: return "+" + Pct(a.value) + " de dano de postura";
                case AffixStat.MaxHealth: return "+" + Mathf.RoundToInt(a.value) + " de vida máxima";
                case AffixStat.MaxPosture: return "+" + Mathf.RoundToInt(a.value) + " de postura máxima";
                case AffixStat.DodgeRecharge: return "Esquiva recarrega " + Pct(a.value) + " mais rápido";
                case AffixStat.FlowGain: return "+" + Pct(a.value) + " de ganho de Fluxo";
                case AffixStat.RoomHeal: return "+" + Mathf.RoundToInt(a.value) + " de cura por sala";
                case AffixStat.FinisherHeal: return "Finalizar cura " + Mathf.RoundToInt(a.value);
                case AffixStat.MoveSpeed: return "+" + Pct(a.value) + " de velocidade";
                case AffixStat.Crit: return "+" + Pct(a.value) + " de chance de crítico";
                case AffixStat.Lifesteal: return Pct(a.value) + " do dano vira vida";
                case AffixStat.ParryWindow: return "Janela de parry +" + Mathf.Max(1, Mathf.RoundToInt(a.value)) + " frame";
                default: return string.Empty;
            }
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

        public static Shape IconShape(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.Amulet: return Shape.Diamond;
                case ItemSlot.Ring: return Shape.Ring;
                case ItemSlot.Bracer: return Shape.Capsule;
                default: return Shape.Hexagon;
            }
        }

        public static string SlotName(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.Amulet: return "Amuleto";
                case ItemSlot.Ring: return "Anel";
                case ItemSlot.Bracer: return "Bracelete";
                default: return "Talismã";
            }
        }
    }
}
