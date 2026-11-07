using UnityEngine;

namespace Shura
{
    public static class RewardIds
    {
        public const string PatientBlade = "tec_lamina_paciente";
        public const string MirrorThrust = "tec_estocada_espelhada";
        public const string PhantomStep = "tec_passo_fantasma";
        public const string DuelistRhythm = "tec_ritmo_do_duelista";
        public const string ExecutionerBlow = "tec_golpe_do_carrasco";
        public const string ReflexArrow = "tec_flecha_reflexa";
        public const string Ambush = "tec_emboscada";
        public const string Bulwark = "tec_muralha";

        public static readonly string[] Techniques =
        {
            PatientBlade, MirrorThrust, PhantomStep, DuelistRhythm, ExecutionerBlow, ReflexArrow, Ambush, Bulwark,
        };

        public const string HungryBlade = "rel_lamina_faminta";
        public const string IronGuard = "rel_guarda_de_ferro";
        public const string LightSteps = "rel_passos_leves";
        public const string BloodOath = "rel_juramento_de_sangue";
        public const string RelentlessRhythm = "rel_ritmo_implacavel";
        public const string HawkEyes = "rel_olhos_de_falcao";
        public const string BurningFlow = "rel_fluxo_ardente";

        public static readonly string[] Relics =
        {
            HungryBlade, IronGuard, LightSteps, BloodOath, RelentlessRhythm, HawkEyes, BurningFlow,
        };
    }

    /// <summary>Creates fresh reward instances (techniques keep per-run state).</summary>
    public static class RewardRegistry
    {
        public static RewardDef Create(string id)
        {
            switch (id)
            {
                case RewardIds.PatientBlade: return new PatientBladeTechnique();
                case RewardIds.MirrorThrust: return new MirrorThrustTechnique();
                case RewardIds.PhantomStep: return new PhantomStepTechnique();
                case RewardIds.DuelistRhythm: return new DuelistRhythmTechnique();
                case RewardIds.ExecutionerBlow: return new ExecutionerBlowTechnique();
                case RewardIds.ReflexArrow: return new ReflexArrowTechnique();
                case RewardIds.Ambush: return new AmbushTechnique();
                case RewardIds.Bulwark: return new BulwarkTechnique();

                case RewardIds.HungryBlade:
                    return new Relic(id, "Lâmina Faminta",
                        "+25% de dano na vida dos inimigos.",
                        "-15 de vida máxima.",
                        m => { m.damageMult *= 1.25f; m.maxHealthBonus -= 15f; });
                case RewardIds.IronGuard:
                    return new Relic(id, "Guarda de Ferro",
                        "+40 de postura máxima.",
                        "Janela de parry perfeito 1 frame menor.",
                        m => { m.maxPostureBonus += 40f; m.perfectParryFrameBonus -= 1; });
                case RewardIds.LightSteps:
                    return new Relic(id, "Passos Leves",
                        "+1 carga de esquiva.",
                        "Bloquear desgasta 25% mais a sua postura.",
                        m => { m.dodgeChargeBonus += 1; m.blockWearMult *= 1.25f; });
                case RewardIds.BloodOath:
                    return new Relic(id, "Juramento de Sangue",
                        "Finalizadores curam 20 de vida.",
                        "Levar dano tira 1 nível de Fluxo a mais.",
                        m => { m.finisherHeal += 20f; m.extraFlowLossOnHurt += 1; });
                case RewardIds.RelentlessRhythm:
                    return new Relic(id, "Ritmo Implacável",
                        "Recuperação dos seus ataques 30% mais curta.",
                        "Seus ataques causam 20% menos postura.",
                        m => { m.recoveryMult *= 0.7f; m.normalAttackPostureMult *= 0.8f; });
                case RewardIds.HawkEyes:
                    return new Relic(id, "Olhos de Falcão",
                        "Janela de parry perfeito 2 frames maior.",
                        "Parry perfeito causa 30% menos postura no inimigo.",
                        m => { m.perfectParryFrameBonus += 2; m.perfectParryPostureMult *= 0.7f; });
                case RewardIds.BurningFlow:
                    return new Relic(id, "Fluxo Ardente",
                        "O Fluxo sobe 50% mais rápido.",
                        "No Fluxo máximo você recebe 25% mais dano.",
                        m => { m.flowGainMult *= 1.5f; m.damageTakenAtMaxFlowMult *= 1.25f; });
                default:
                    Debug.LogError("[Shura] Recompensa desconhecida: " + id);
                    return null;
            }
        }
    }

    /// <summary>Ronin: a perfect parry charges your next swing with crushing posture damage.</summary>
    public sealed class PatientBladeTechnique : RewardDef
    {
        const int ChargeFrames = 60;
        const float ChargedPostureMult = 3f;

        public override string Id => RewardIds.PatientBlade;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Lâmina Paciente";
        public override string Gain => "Após um parry perfeito, seu próximo golpe em 1 s causa 3x postura.";
        public override string Cost => "Seus golpes normais causam 25% menos postura.";
        public override string SourceEnemy => EnemyDefs.Ronin.displayName;

        public override void Apply(PlayerModifiers m) { m.normalAttackPostureMult *= 0.75f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            world.Player.EmpoweredFrames = ChargeFrames;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Player.EmpoweredFrames <= 0) return;
            posture *= ChargedPostureMult;
            world.Player.EmpoweredFrames = 0;
            world.Fx.Popup("PACIENTE", target.Position, world.Config.palette.gold);
        }
    }

    /// <summary>Lanceiro: every perfect parry answers with a piercing thrust.</summary>
    public sealed class MirrorThrustTechnique : RewardDef
    {
        public override string Id => RewardIds.MirrorThrust;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Estocada Espelhada";
        public override string Gain => "Parry perfeito dispara uma estocada que atravessa todos à frente (12 dano, 18 postura).";
        public override string Cost => "Bloquear desgasta 30% mais a sua postura.";
        public override string SourceEnemy => EnemyDefs.Lanceiro.displayName;

        public override void Apply(PlayerModifiers m) { m.blockWearMult *= 1.3f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Vector2 dir = attacker.Position - world.Player.Position;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.PlayerInstantStrike(world.Player.Position, dir.normalized, HitShape.Line(3.4f, 0.8f), 12f, 18f);
        }
    }

    /// <summary>Monge: a perfect dodge leaves an echo that detonates.</summary>
    public sealed class PhantomStepTechnique : RewardDef
    {
        public override string Id => RewardIds.PhantomStep;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Passo Fantasma";
        public override string Gain => "Esquiva perfeita deixa um eco que explode após 0,35 s (8 dano, 26 postura).";
        public override string Cost => "Suas esquivas recarregam 40% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Monge.displayName;

        public override void Apply(PlayerModifiers m) { m.dodgeRechargeMult *= 1.4f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            world.ScheduleBurst(world.Player.Position, 21, 1.8f, 8f, 26f);
        }
    }
}

namespace Shura
{
    /// <summary>Kenkaku: chained perfect parries hit the attacker's posture harder and harder.</summary>
    public sealed class DuelistRhythmTechnique : RewardDef
    {
        const float ChainSeconds = 2f;
        const int MaxStacks = 4;
        const float BonusPerStack = 0.25f;

        int stacks;
        float lastParryTime = -99f;

        public override string Id => RewardIds.DuelistRhythm;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Ritmo do Duelista";
        public override string Gain => "Cada parry perfeito em até 2 s do anterior causa +25% de postura (acumula até +100%).";
        public override string Cost => "Bloquear desgasta 20% mais e zera o ritmo.";
        public override string SourceEnemy => EnemyDefs.Kenkaku.displayName;

        public override void Apply(PlayerModifiers m) { m.blockWearMult *= 1.2f; }

        public override void ModifyParryPosture(World world, Enemy attacker, ref float posture)
        {
            if (world.Run.Elapsed - lastParryTime > ChainSeconds) stacks = 0;
            posture *= 1f + BonusPerStack * stacks;
        }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            bool chained = world.Run.Elapsed - lastParryTime <= ChainSeconds;
            stacks = chained ? Mathf.Min(MaxStacks, stacks + 1) : 1;
            lastParryTime = world.Run.Elapsed;
            if (stacks > 1) world.Fx.Popup("RITMO x" + stacks, world.Player.Position + Vector2.up * 0.9f, world.Config.palette.gold);
        }

        public override void OnBlock(World world, Enemy attacker)
        {
            stacks = 0;
        }
    }

    /// <summary>Carrasco: finishers send out a shockwave that crushes nearby posture.</summary>
    public sealed class ExecutionerBlowTechnique : RewardDef
    {
        public override string Id => RewardIds.ExecutionerBlow;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Golpe do Carrasco";
        public override string Gain => "Finalizadores soltam uma onda de choque (10 dano, 35 postura) nos inimigos próximos.";
        public override string Cost => "Você anda 10% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Carrasco.displayName;

        public override void Apply(PlayerModifiers m) { m.moveSpeedMult *= 0.9f; }

        public override void OnFinisher(World world, Enemy target)
        {
            world.ScheduleBurst(target.Position, 1, 2.8f, 10f, 35f);
        }
    }

    /// <summary>Arqueiro: perfect parries loose an arrow at another enemy.</summary>
    public sealed class ReflexArrowTechnique : RewardDef
    {
        public override string Id => RewardIds.ReflexArrow;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Flecha Reflexa";
        public override string Gain => "Todo parry perfeito dispara uma flecha no inimigo mais próximo (12 dano, 16 postura).";
        public override string Cost => "-10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Arqueiro.displayName;

        public override void Apply(PlayerModifiers m) { m.maxHealthBonus -= 10f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Enemy target = world.FindNearestEnemy(world.Player.Position, attacker) ?? attacker;
            Vector2 dir = target.Position - world.Player.Position;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.FirePlayerArrow(world.Player.Position, dir.normalized, 12f, 16f);
        }
    }

    /// <summary>Sombra: a perfect dodge empowers your next two strikes.</summary>
    public sealed class AmbushTechnique : RewardDef
    {
        const float WindowSeconds = 1.5f;
        const float DamageMult = 1.6f;

        float activeUntil = -1f;
        int charges;

        public override string Id => RewardIds.Ambush;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Emboscada";
        public override string Gain => "Após uma esquiva perfeita, seus 2 próximos golpes em 1,5 s causam +60% de dano.";
        public override string Cost => "Suas esquivas são 20% mais curtas.";
        public override string SourceEnemy => EnemyDefs.Sombra.displayName;

        public override void Apply(PlayerModifiers m) { m.dodgeDistanceMult *= 0.8f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            activeUntil = world.Run.Elapsed + WindowSeconds;
            charges = 2;
            world.Player.EmpoweredFrames = Mathf.Max(world.Player.EmpoweredFrames, Mathf.RoundToInt(WindowSeconds * SimClock.TickRate));
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (charges <= 0 || world.Run.Elapsed > activeUntil) return;
            damage *= DamageMult;
            charges--;
            world.Fx.Popup("EMBOSCADA", target.Position + Vector2.up * 0.9f, world.Config.palette.gold);
        }
    }

    /// <summary>Escudeiro: an ordinary block jars the attacker's posture.</summary>
    public sealed class BulwarkTechnique : RewardDef
    {
        const float PostureOnBlock = 8f;

        public override string Id => RewardIds.Bulwark;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Muralha";
        public override string Gain => "Bloquear (sem ser perfeito) causa 8 de postura em quem atacou.";
        public override string Cost => "Janela de parry perfeito 1 frame menor.";
        public override string SourceEnemy => EnemyDefs.Escudeiro.displayName;

        public override void Apply(PlayerModifiers m) { m.perfectParryFrameBonus -= 1; }

        public override void OnBlock(World world, Enemy attacker)
        {
            attacker.ApplyPosture(PostureOnBlock);
        }
    }
}
