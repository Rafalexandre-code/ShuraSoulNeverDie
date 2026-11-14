using UnityEngine;

namespace Shura
{
    // The twelve techniques of the new enemies (design doc §5.8). Like the others: the enemy's
    // own mechanic turned into a tool, and a price paid at level I.

    /// <summary>Kamaitachi: after a perfect parry, the next blow cuts three times.</summary>
    public sealed class TriSickleTechnique : TechniqueDef
    {
        bool armed;
        public override string Id => RewardIds.TriSickle;
        protected override string BaseName => "Foice Tríplice";
        public override Mark[] MarkSet => new[] { Mark.Vento, Mark.Corte };
        protected override string BaseGain => "Depois de um parry perfeito, o próximo golpe corta três vezes (+80% de dano).";
        protected override string BaseCost => "−5% de dano nos outros golpes.";
        public override string SourceEnemy => EnemyDefs.Kamaitachi.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.damageMult *= 0.95f; }
        public override void OnPerfectParry(World world, Enemy attacker) { armed = true; }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (!armed) return;
            armed = false;
            damage *= 1f + 0.8f * Power;
            world.Fx.Emit(FxType.SwapStrike, target.Position, Vector2.up, 1f, ColorUtil.Hex("#B8F0C0"));
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Rokurokubi: you see the blows coming from further away.</summary>
    public sealed class LookOverTechnique : TechniqueDef
    {
        public override string Id => RewardIds.LookOver;
        protected override string BaseName => "Olhar por Cima";
        public override Mark[] MarkSet => new[] { Mark.Espirito };
        protected override string BaseGain => "Os anéis de tempo começam a fechar 20% mais cedo.";
        protected override string BaseCost => "−10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Rokurokubi.displayName;

        protected override void ApplyGain(PlayerModifiers m) { m.ringWarningMult *= 1.2f * Power; }
        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 10f; }
    }

    /// <summary>Jorōgumo Menor: a dodge leaves a thread that slows whoever is near.</summary>
    public sealed class SilkThreadTechnique : TechniqueDef
    {
        public override string Id => RewardIds.SilkThread;
        protected override string BaseName => "Fio de Seda";
        public override Mark[] MarkSet => new[] { Mark.Sombra };
        protected override string BaseGain => "Esquivar deixa um fio de seda: inimigos a até 2 m ficam lentos por 2 s.";
        protected override string BaseCost => "Esquivas recarregam 8% mais devagar.";
        public override string SourceEnemy => EnemyDefs.AranhaMenor.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeRechargeMult *= 1.08f; }

        public override void OnDodge(World world, Vector2 from)
        {
            bool any = false;
            foreach (Enemy e in world.Enemies)
            {
                if (e.IsGone || e.IsAlly || Vector2.Distance(e.Position, from) > 2f * Power) continue;
                e.ApplyChill(2f * Power, false);
                any = true;
            }
            if (any) Acted(world, from, null);
        }
    }

    /// <summary>Nopperabō: a finisher lends you the strength of whoever fell.</summary>
    public sealed class EmptyMaskTechnique : TechniqueDef
    {
        int charges;
        public override string Id => RewardIds.EmptyMask;
        protected override string BaseName => "Máscara Vazia";
        public override Mark[] MarkSet => new[] { Mark.Sangue };
        protected override string BaseGain => "Ao finalizar, você veste a força dele: +30% de dano nos próximos 3 golpes.";
        protected override string BaseCost => "−5% de dano.";
        public override string SourceEnemy => EnemyDefs.Nopperabo.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.damageMult *= 0.95f; }
        public override void OnFinisher(World world, Enemy target) { charges = 3; }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (charges <= 0) return;
            charges--;
            damage *= 1f + 0.3f * Power;
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Ittan-momen: a perfect dodge smothers the attacker.</summary>
    public sealed class ShroudTechnique : TechniqueDef
    {
        public override string Id => RewardIds.Shroud;
        protected override string BaseName => "Mortalha";
        public override Mark[] MarkSet => new[] { Mark.Vento };
        protected override string BaseGain => "Esquiva perfeita sufoca quem atacou: −40 de fôlego e 20 de postura.";
        protected override string BaseCost => "−6% de velocidade.";
        public override string SourceEnemy => EnemyDefs.IttanMomen.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.moveSpeedMult *= 0.94f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone) return;
            attacker.DrainStamina(40f * Power);
            attacker.ApplyPosture(20f * Power);
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Nurikabe: a block costs less and pushes the attacker back.</summary>
    public sealed class WalkingWallTechnique : TechniqueDef
    {
        public override string Id => RewardIds.WalkingWall;
        protected override string BaseName => "Muro Andante";
        public override Mark[] MarkSet => new[] { Mark.Guarda };
        protected override string BaseGain => "Bloquear gasta 25% menos postura e empurra quem bateu 1,5 m.";
        protected override string BaseCost => "−6% de dano.";
        public override string SourceEnemy => EnemyDefs.Nurikabe.displayName;

        protected override void ApplyGain(PlayerModifiers m) { m.blockWearMult *= 0.75f; }
        protected override void ApplyCost(PlayerModifiers m) { m.damageMult *= 0.94f; }

        public override void OnBlock(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone) return;
            Vector2 dir = attacker.Position - world.Player.Position;
            attacker.Shove(dir.sqrMagnitude > 1e-4f ? dir.normalized : world.Player.Facing, 1.5f * Power, world.Player);
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Ōmukade: a shell in segments.</summary>
    public sealed class SegmentedShellTechnique : TechniqueDef
    {
        public override string Id => RewardIds.SegmentedShell;
        protected override string BaseName => "Carapaça Segmentada";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Sangue };
        protected override string BaseGain => "+25 de postura máxima e bloquear gasta 15% menos postura.";
        protected override string BaseCost => "−5% de velocidade.";
        public override string SourceEnemy => EnemyDefs.Omukade.displayName;

        protected override void ApplyGain(PlayerModifiers m)
        {
            m.maxPostureBonus += 25f * Power;
            m.blockWearMult *= 0.85f;
        }

        protected override void ApplyCost(PlayerModifiers m) { m.moveSpeedMult *= 0.95f; }
    }

    /// <summary>Nue: every perfect parry changes your nature for a while.</summary>
    public sealed class ThreeNaturesTechnique : TechniqueDef
    {
        int nature = -1;
        float until;
        public override string Id => RewardIds.ThreeNatures;
        protected override string BaseName => "Três Naturezas";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Trovao };
        protected override string BaseGain => "Cada parry perfeito troca a sua natureza por 4 s: macaco (+25% de dano), tigre (+30% de postura), serpente (cura 4 por golpe).";
        protected override string BaseCost => "Seu parry perfeito causa 10% menos postura.";
        public override string SourceEnemy => EnemyDefs.Nue.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.perfectParryPostureMult *= 0.9f; }

        bool Active(World world) => nature >= 0 && world.Run != null && world.Run.Elapsed < until;

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            nature = (nature + 1) % 3;
            until = world.Run.Elapsed + 4f * Power;
            string[] names = { "MACACO", "TIGRE", "SERPENTE" };
            world.Fx.Popup(names[nature], world.Player.Position + Vector2.up * 1.4f, ColorUtil.Hex("#D9A26B"));
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (Active(world) && nature == 0) damage *= 1.25f;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (Active(world) && nature == 1) posture *= 1.3f;
        }

        public override void OnDealtDamage(World world, Enemy target, float damage)
        {
            if (!Active(world) || nature != 2) return;
            world.Player.Heal(4f);
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Tsukumogami: after a perfect parry, your other blade cuts on its own.</summary>
    public sealed class LivingBladeTechnique : TechniqueDef
    {
        public override string Id => RewardIds.LivingBlade;
        protected override string BaseName => "Arma Viva";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Espirito };
        protected override string BaseGain => "Depois de um parry perfeito, a sua outra arma corta sozinha à sua volta (10 de dano, 12 de postura).";
        protected override string BaseCost => "−8 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Tsukumogami.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 8f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Vector2 dir = attacker != null ? attacker.Position - world.Player.Position : world.Player.Facing;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.PlayerInstantStrike(world.Player.Position, dir.normalized, HitShape.Arc(2.2f, 160f), 10f * Power, 12f * Power);
            world.Fx.Emit(FxType.PlayerSwing, world.Player.Position, dir.normalized, 1.2f, ColorUtil.Hex("#D8DEE9"));
            Acted(world, world.Player.Position, attacker);
        }
    }

    /// <summary>Sacerdote Corrompido: a perfect parry seals the attacker's red blows.</summary>
    public sealed class ForbiddenSealTechnique : TechniqueDef
    {
        public override string Id => RewardIds.ForbiddenSeal;
        protected override string BaseName => "Selo Proibido";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Fogo };
        protected override string BaseGain => "Parry perfeito sela os golpes vermelhos (perigosos) de quem atacou por 4 s.";
        protected override string BaseCost => "Seu parry perfeito causa 10% menos postura.";
        public override string SourceEnemy => EnemyDefs.Sacerdote.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.perfectParryPostureMult *= 0.9f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone) return;
            attacker.SealDanger(Mathf.RoundToInt(4f * Power * SimClock.TickRate));
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Duelista Cego: you hear the blade before you see it.</summary>
    public sealed class InnerEarTechnique : TechniqueDef
    {
        public override string Id => RewardIds.InnerEar;
        protected override string BaseName => "Ouvido Interior";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Guarda };
        protected override string BaseGain => "+1 frame na janela do parry perfeito.";
        protected override string BaseCost => "−10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.ZatoCego.displayName;

        protected override void ApplyGain(PlayerModifiers m) { m.perfectParryFrameBonus += Power > 1f ? 2 : 1; }
        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 10f; }
    }

    /// <summary>Espelho do Shura: a perfect parry sends part of the blow back.</summary>
    public sealed class ReflectionTechnique : TechniqueDef
    {
        public override string Id => RewardIds.Reflection;
        protected override string BaseName => "Reflexo";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Corte };
        protected override string BaseGain => "Parry perfeito devolve o golpe: 10 de dano em quem atacou.";
        protected override string BaseCost => "Bloquear gasta 10% mais postura.";
        public override string SourceEnemy => EnemyDefs.Espelho.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.blockWearMult *= 1.1f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone) return;
            Vector2 dir = attacker.Position - world.Player.Position;
            attacker.ReceiveHazard(10f * Power, 6f * Power, dir.sqrMagnitude > 1e-4f ? dir.normalized : world.Player.Facing);
            Acted(world, attacker.Position, attacker);
        }
    }
}
