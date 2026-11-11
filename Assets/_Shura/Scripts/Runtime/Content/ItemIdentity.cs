using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>From Legendary up, an item carries one rule-changing affix (design doc §4.6).</summary>
    public enum UniqueAffix
    {
        None,
        IronRose,
        HannyaEye,
        TenguFeather,
        CrackedBell,
        KitsuneTear,
        KappaAnchor,
    }

    public enum Clan
    {
        None,
        Takeda,
        Hattori,
        Minamoto,
        Shimazu,
    }

    /// <summary>A corrupted item's flaw: great power with a price that a feat of skill removes.</summary>
    public enum Corruption
    {
        None,
        HiddenRings,
        Fragile,
        Heavy,
        ShortBreath,
    }

    public static class ItemIdentity
    {
        public const int UniqueCount = 6;
        public const int PurifyFinishers = 5;

        public static string UniqueName(UniqueAffix u)
        {
            switch (u)
            {
                case UniqueAffix.IronRose: return "Rosa de Ferro";
                case UniqueAffix.HannyaEye: return "Olho de Hannya";
                case UniqueAffix.TenguFeather: return "Pena do Tengu";
                case UniqueAffix.CrackedBell: return "Sino Rachado";
                case UniqueAffix.KitsuneTear: return "Lágrima da Kitsune";
                case UniqueAffix.KappaAnchor: return "Âncora do Kappa";
                default: return string.Empty;
            }
        }

        public static string UniqueText(UniqueAffix u)
        {
            switch (u)
            {
                case UniqueAffix.IronRose: return "ÚNICO · Rosa de Ferro: bloquear vale meio parry (planta um selo e quebra metade da postura de um parry perfeito).";
                case UniqueAffix.HannyaEye: return "ÚNICO · Olho de Hannya: os anéis de tempo começam a fechar 30% antes.";
                case UniqueAffix.TenguFeather: return "ÚNICO · Pena do Tengu: a esquiva vira um teleporte curto à frente.";
                case UniqueAffix.CrackedBell: return "ÚNICO · Sino Rachado: o Mushin não acaba com o primeiro dano.";
                case UniqueAffix.KitsuneTear: return "ÚNICO · Lágrima da Kitsune: o primeiro golpe em cada inimigo causa +60% de dano.";
                case UniqueAffix.KappaAnchor: return "ÚNICO · Âncora do Kappa: finalizar devolve uma carga de esquiva e dá 1 s intocável.";
                default: return string.Empty;
            }
        }

        public static string ClanName(Clan c)
        {
            switch (c)
            {
                case Clan.Takeda: return "Takeda";
                case Clan.Hattori: return "Hattori";
                case Clan.Minamoto: return "Minamoto";
                case Clan.Shimazu: return "Shimazu";
                default: return string.Empty;
            }
        }

        public static Color ClanColor(Clan c)
        {
            switch (c)
            {
                case Clan.Takeda: return ColorUtil.Hex("#E4572E");
                case Clan.Hattori: return ColorUtil.Hex("#8B6BD6");
                case Clan.Minamoto: return ColorUtil.Hex("#4FA3FF");
                case Clan.Shimazu: return ColorUtil.Hex("#D3203A");
                default: return Color.white;
            }
        }

        public static string ClanSetText(Clan c)
        {
            switch (c)
            {
                case Clan.Takeda: return "Clã Takeda — 2 peças: +10% de dano · 4 peças: finalizar solta uma explosão de fogo (raio 2,4: 14 dano, 20 postura).";
                case Clan.Hattori: return "Clã Hattori — 2 peças: +1 carga de esquiva · 4 peças: esquivar deixa uma cópia de sombra que explode (10 dano, 14 postura).";
                case Clan.Minamoto: return "Clã Minamoto — 2 peças: +20 de postura máxima · 4 peças: parry perfeito dispara uma flecha no inimigo mais próximo (12 dano, 16 postura).";
                case Clan.Shimazu: return "Clã Shimazu — 2 peças: +15 de vida máxima · 4 peças: abaixo de 50% de vida, +25% de dano e 5% do dano vira vida.";
                default: return string.Empty;
            }
        }

        public static void ApplyTwoPieces(Clan c, PlayerModifiers m)
        {
            switch (c)
            {
                case Clan.Takeda: m.damageMult *= 1.1f; break;
                case Clan.Hattori: m.dodgeChargeBonus += 1; break;
                case Clan.Minamoto: m.maxPostureBonus += 20f; break;
                case Clan.Shimazu: m.maxHealthBonus += 15f; break;
            }
        }

        public static string FlawText(Corruption f)
        {
            switch (f)
            {
                case Corruption.HiddenRings: return "os anéis de tempo ficam invisíveis";
                case Corruption.Fragile: return "você recebe 25% mais dano";
                case Corruption.Heavy: return "você anda 12% mais devagar";
                case Corruption.ShortBreath: return "−1 carga de esquiva";
                default: return string.Empty;
            }
        }

        public static void ApplyFlaw(Corruption f, PlayerModifiers m)
        {
            switch (f)
            {
                case Corruption.HiddenRings: m.hiddenRings = true; break;
                case Corruption.Fragile: m.damageTakenMult *= 1.25f; break;
                case Corruption.Heavy: m.moveSpeedMult *= 0.88f; break;
                case Corruption.ShortBreath: m.dodgeChargeBonus -= 1; break;
            }
        }

        /// <summary>The steel a broken item gives back (Aço Celeste, the smith's currency).</summary>
        public static int SteelFrom(Item item) => 1 + (int)item.rarity / 2;

        /// <summary>How many worn pieces of each clan.</summary>
        public static int CountClan(Item[] equipped, Clan c)
        {
            int n = 0;
            foreach (Item it in equipped)
            {
                if (it != null && it.clan == c) n++;
            }
            return n;
        }

        /// <summary>The smith's fusion: three items of the same rarity, the weakest such trio first.</summary>
        public static bool FindTrio(List<Item> bag, out Rarity rarity)
        {
            for (int r = 0; r < Rarities.Count - 1; r++)
            {
                int n = 0;
                foreach (Item it in bag)
                {
                    if ((int)it.rarity == r) n++;
                }
                if (n < 3) continue;
                rarity = (Rarity)r;
                return true;
            }
            rarity = Rarity.Common;
            return false;
        }
    }

    /// <summary>The four-piece bonus of a clan set, worn as one more effect.</summary>
    public sealed class ClanSetEffect : RewardDef
    {
        public readonly Clan clan;
        float lastShadow = -99f;

        public ClanSetEffect(Clan clan) { this.clan = clan; }

        public override string Id => "clan_" + clan;
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name => "Clã " + ItemIdentity.ClanName(clan);
        public override string Gain => ItemIdentity.ClanSetText(clan);
        public override string Cost => "Nenhum.";

        public override void OnFinisher(World world, Enemy target)
        {
            if (clan != Clan.Takeda) return;
            world.ScheduleBurst(target.Position, 6, 2.4f, 14f, 20f, World.BlastColor(BlastKind.Fire), true);
        }

        public override void OnDodge(World world, Vector2 from)
        {
            if (clan != Clan.Hattori || world.Run.Elapsed - lastShadow < 0.6f) return;
            lastShadow = world.Run.Elapsed;
            world.Fx.Emit(FxType.Phase, from, Vector2.up, world.Player.Radius, Elements.Color(Element.Shadow));
            world.ScheduleBurst(from, 20, 1.4f, 10f, 14f, Elements.Color(Element.Shadow));
        }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (clan != Clan.Minamoto) return;
            PlayerController p = world.Player;
            Enemy target = world.FindNearestEnemy(p.Position, null) ?? attacker;
            if (target == null) return;
            Vector2 dir = target.Position - p.Position;
            world.FirePlayerArrow(p.Position, dir.sqrMagnitude > 1e-4f ? dir.normalized : p.Facing, 12f, 16f);
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (clan != Clan.Shimazu) return;
            PlayerController p = world.Player;
            if (p.Health >= p.MaxHealth * 0.5f) return;
            damage *= 1.25f;
        }

        public override void OnDealtDamage(World world, Enemy target, float damage)
        {
            if (clan != Clan.Shimazu) return;
            PlayerController p = world.Player;
            if (p.Health < p.MaxHealth * 0.5f) p.Heal(damage * 0.05f);
        }
    }
}
