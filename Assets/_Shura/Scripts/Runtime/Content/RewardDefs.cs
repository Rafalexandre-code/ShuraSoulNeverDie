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
        // Bestiary (M1.10)
        public const string KunaiFan = "tec_leque_de_kunai";
        public const string ShortFuse = "tec_pavio_curto";
        public const string IaiDraw = "tec_saque_iai";
        public const string ExplosiveSeal = "tec_selo_explosivo";
        public const string ShamanBlessing = "tec_bencao_da_xama";
        public const string LivingLantern = "tec_lanterna_viva";
        public const string EtherealBody = "tec_corpo_etereo";
        public const string KappaCurrent = "tec_correnteza_do_kappa";
        public const string OniCrush = "tec_golpe_do_oni";
        public const string SlimeHunger = "tec_fome_do_lodo";
        public const string Frenzy = "tec_furia_do_barbaro";
        public const string ShadowCopy = "tec_copia_de_sombra";
        public const string SentinelBlink = "tec_piscar_do_vigia";
        public const string BloodThirst = "tec_sede_de_sangue";
        public const string StubbornBones = "tec_ossos_teimosos";
        public const string CounterPunch = "tec_contragolpe";

        public static readonly string[] Techniques =
        {
            PatientBlade, MirrorThrust, PhantomStep, DuelistRhythm, ExecutionerBlow, ReflexArrow, Ambush, Bulwark,
            KunaiFan, ShortFuse, IaiDraw, ExplosiveSeal, ShamanBlessing, LivingLantern, EtherealBody, KappaCurrent,
            OniCrush, SlimeHunger, Frenzy, ShadowCopy, SentinelBlink, BloodThirst, StubbornBones, CounterPunch,
        };

        public const string HungryBlade = "rel_lamina_faminta";
        public const string IronGuard = "rel_guarda_de_ferro";
        public const string LightSteps = "rel_passos_leves";
        public const string BloodOath = "rel_juramento_de_sangue";
        public const string RelentlessRhythm = "rel_ritmo_implacavel";
        public const string HawkEyes = "rel_olhos_de_falcao";
        public const string BurningFlow = "rel_fluxo_ardente";

        public const string BlessingVigor = "ben_vigor";
        public const string BlessingEdge = "ben_gume";
        public const string BlessingGuard = "ben_guarda";
        public const string BlessingWind = "ben_vento";
        public const string BlessingCalm = "ben_calma";

        public static readonly string[] Blessings = { BlessingVigor, BlessingEdge, BlessingGuard, BlessingWind, BlessingCalm };

        public static readonly string[] Relics =
        {
            HungryBlade, IronGuard, LightSteps, BloodOath, RelentlessRhythm, HawkEyes, BurningFlow,
            RoadRelics.HannyaMask, RoadRelics.BlankScroll, RoadRelics.StrawSandals, RoadRelics.VowOfSilence,
            RoadRelics.ExecutionerChain, RoadRelics.BrokenHourglass, RoadRelics.SixMonCoin, RoadRelics.DragonEye,
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
                case RewardIds.KunaiFan: return new KunaiFanTechnique();
                case RewardIds.ShortFuse: return new ShortFuseTechnique();
                case RewardIds.IaiDraw: return new IaiDrawTechnique();
                case RewardIds.ExplosiveSeal: return new ExplosiveSealTechnique();
                case RewardIds.ShamanBlessing: return new ShamanBlessingTechnique();
                case RewardIds.LivingLantern: return new LivingLanternTechnique();
                case RewardIds.EtherealBody: return new EtherealBodyTechnique();
                case RewardIds.KappaCurrent: return new KappaCurrentTechnique();
                case RewardIds.OniCrush: return new OniCrushTechnique();
                case RewardIds.SlimeHunger: return new SlimeHungerTechnique();
                case RewardIds.Frenzy: return new FrenzyTechnique();
                case RewardIds.ShadowCopy: return new ShadowCopyTechnique();
                case RewardIds.SentinelBlink: return new SentinelBlinkTechnique();
                case RewardIds.BloodThirst: return new BloodThirstTechnique();
                case RewardIds.StubbornBones: return new StubbornBonesTechnique();
                case RewardIds.CounterPunch: return new CounterPunchTechnique();

                case RewardIds.BlessingVigor:
                    return new Blessing(id, "Bênção do Vigor", "+12 de vida máxima e cura 20.", 20f, m => m.maxHealthBonus += 12f);
                case RewardIds.BlessingEdge:
                    return new Blessing(id, "Bênção do Gume", "+8% de dano.", 0f, m => m.damageMult *= 1.08f);
                case RewardIds.BlessingGuard:
                    return new Blessing(id, "Bênção da Guarda", "+15 de postura máxima.", 0f, m => m.maxPostureBonus += 15f);
                case RewardIds.BlessingWind:
                    return new Blessing(id, "Bênção do Vento", "Esquivas recarregam 6% mais rápido.", 0f, m => m.dodgeRechargeMult *= 0.94f);
                case RewardIds.BlessingCalm:
                    return new Blessing(id, "Bênção da Calma", "+6 de cura ao vencer cada sala.", 0f, m => m.roomClearHealBonus += 6f);

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
                    RewardDef road = RoadRelics.Create(id);
                    if (road != null) return road;
                    Debug.LogError("[Shura] Recompensa desconhecida: " + id);
                    return null;
            }
        }
    }

    /// <summary>Ronin: a perfect parry charges your next swing with crushing posture damage.</summary>
    public sealed class PatientBladeTechnique : TechniqueDef
    {
        const int ChargeFrames = 60;
        const float ChargedPostureMult = 3f;

        public override string Id => RewardIds.PatientBlade;
        protected override string BaseName => "Lâmina Paciente";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Guarda };
        protected override string BaseGain => "Após um parry perfeito, seu próximo golpe em 1 s causa 3x postura.";
        protected override string BaseCost => "Seus golpes normais causam 25% menos postura.";
        public override string SourceEnemy => EnemyDefs.Ronin.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.normalAttackPostureMult *= 0.75f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            world.Player.EmpoweredFrames = ChargeFrames;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Player.EmpoweredFrames <= 0) return;
            posture *= ChargedPostureMult * Power;
            world.Player.EmpoweredFrames = 0;
            world.Fx.Popup("PACIENTE", target.Position, world.Config.palette.gold);
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Lanceiro: every perfect parry answers with a piercing thrust.</summary>
    public sealed class MirrorThrustTechnique : TechniqueDef
    {
        public override string Id => RewardIds.MirrorThrust;
        protected override string BaseName => "Estocada Espelhada";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Guarda };
        protected override string BaseGain => "Parry perfeito dispara uma estocada que atravessa todos à frente (12 dano, 18 postura).";
        protected override string BaseCost => "Bloquear desgasta 30% mais a sua postura.";
        public override string SourceEnemy => EnemyDefs.Lanceiro.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.blockWearMult *= 1.3f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Vector2 dir = attacker.Position - world.Player.Position;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.PlayerInstantStrike(world.Player.Position, dir.normalized, HitShape.Line(3.4f * Power, 0.8f), 12f * Power, 18f * Power);
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Monge: a perfect dodge leaves an echo that detonates.</summary>
    public sealed class PhantomStepTechnique : TechniqueDef
    {
        public override string Id => RewardIds.PhantomStep;
        protected override string BaseName => "Passo Fantasma";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Espirito };
        protected override string BaseGain => "Esquiva perfeita deixa um eco que explode após 0,35 s (8 dano, 26 postura).";
        protected override string BaseCost => "Suas esquivas recarregam 40% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Monge.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeRechargeMult *= 1.4f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            world.ScheduleBurst(world.Player.Position, 21, 1.8f * Power, 8f * Power, 26f * Power);
            Acted(world, world.Player.Position, attacker);
        }
    }
}

namespace Shura
{
    /// <summary>Kenkaku: chained perfect parries hit the attacker's posture harder and harder.</summary>
    public sealed class DuelistRhythmTechnique : TechniqueDef
    {
        const float ChainSeconds = 2f;
        const int MaxStacks = 4;
        const float BonusPerStack = 0.25f;

        int stacks;
        float lastParryTime = -99f;

        public override string Id => RewardIds.DuelistRhythm;
        protected override string BaseName => "Ritmo do Duelista";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Trovao };
        protected override string BaseGain => "Cada parry perfeito em até 2 s do anterior causa +25% de postura (acumula até +100%).";
        protected override string BaseCost => "Bloquear desgasta 20% mais e zera o ritmo.";
        public override string SourceEnemy => EnemyDefs.Kenkaku.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.blockWearMult *= 1.2f; }

        public override void ModifyParryPosture(World world, Enemy attacker, ref float posture)
        {
            if (world.Run.Elapsed - lastParryTime > ChainSeconds) stacks = 0;
            posture *= 1f + BonusPerStack * Power * stacks;
        }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            bool chained = world.Run.Elapsed - lastParryTime <= ChainSeconds;
            stacks = chained ? Mathf.Min(MaxStacks, stacks + 1) : 1;
            lastParryTime = world.Run.Elapsed;
            if (stacks <= 1) return;
            world.Fx.Popup("RITMO x" + stacks, world.Player.Position + Vector2.up * 0.9f, world.Config.palette.gold);
            Acted(world, attacker != null ? attacker.Position : world.Player.Position, attacker);
        }

        public override void OnBlock(World world, Enemy attacker)
        {
            stacks = 0;
        }
    }

    /// <summary>Carrasco: finishers send out a shockwave that crushes nearby posture.</summary>
    public sealed class ExecutionerBlowTechnique : TechniqueDef
    {
        public override string Id => RewardIds.ExecutionerBlow;
        protected override string BaseName => "Golpe do Carrasco";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Sangue };
        protected override string BaseGain => "Finalizadores soltam uma onda de choque (10 dano, 35 postura) nos inimigos próximos.";
        protected override string BaseCost => "Você anda 10% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Carrasco.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.moveSpeedMult *= 0.9f; }

        public override void OnFinisher(World world, Enemy target)
        {
            world.ScheduleBurst(target.Position, 1, 2.8f, 10f * Power, 35f * Power);
            Acted(world, target.Position, null);
        }
    }

    /// <summary>Arqueiro: perfect parries loose an arrow at another enemy.</summary>
    public sealed class ReflexArrowTechnique : TechniqueDef
    {
        public override string Id => RewardIds.ReflexArrow;
        protected override string BaseName => "Flecha Reflexa";
        public override Mark[] MarkSet => new[] { Mark.Vento };
        protected override string BaseGain => "Todo parry perfeito dispara uma flecha no inimigo mais próximo (12 dano, 16 postura).";
        protected override string BaseCost => "-10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Arqueiro.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 10f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Enemy target = world.FindNearestEnemy(world.Player.Position, attacker) ?? attacker;
            Vector2 dir = target.Position - world.Player.Position;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.FirePlayerArrow(world.Player.Position, dir.normalized, 12f * Power, 16f * Power);
            if (Power > 1f) world.FirePlayerArrow(world.Player.Position, CombatMath.FromAngle(CombatMath.AngleDegrees(dir) + 14f), 8f, 10f);
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Sombra: a perfect dodge empowers your next two strikes.</summary>
    public sealed class AmbushTechnique : TechniqueDef
    {
        const float WindowSeconds = 1.5f;
        const float DamageMult = 1.6f;

        float activeUntil = -1f;
        int charges;

        public override string Id => RewardIds.Ambush;
        protected override string BaseName => "Emboscada";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Corte };
        protected override string BaseGain => "Após uma esquiva perfeita, seus 2 próximos golpes em 1,5 s causam +60% de dano.";
        protected override string BaseCost => "Suas esquivas são 20% mais curtas.";
        public override string SourceEnemy => EnemyDefs.Sombra.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeDistanceMult *= 0.8f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            activeUntil = world.Run.Elapsed + WindowSeconds;
            charges = 2;
            world.Player.EmpoweredFrames = Mathf.Max(world.Player.EmpoweredFrames, Mathf.RoundToInt(WindowSeconds * SimClock.TickRate));
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (charges <= 0 || world.Run.Elapsed > activeUntil) return;
            damage *= 1f + (DamageMult - 1f) * Power;
            charges--;
            world.Fx.Popup("EMBOSCADA", target.Position + Vector2.up * 0.9f, world.Config.palette.gold);
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Escudeiro: an ordinary block jars the attacker's posture.</summary>
    public sealed class BulwarkTechnique : TechniqueDef
    {
        const float PostureOnBlock = 8f;

        public override string Id => RewardIds.Bulwark;
        protected override string BaseName => "Muralha";
        public override Mark[] MarkSet => new[] { Mark.Guarda };
        protected override string BaseGain => "Bloquear (sem ser perfeito) causa 8 de postura em quem atacou.";
        protected override string BaseCost => "Janela de parry perfeito 1 frame menor.";
        public override string SourceEnemy => EnemyDefs.Escudeiro.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.perfectParryFrameBonus -= 1; }

        public override void OnBlock(World world, Enemy attacker)
        {
            attacker.ApplyPosture(PostureOnBlock * Power);
            Acted(world, attacker.Position, attacker);
        }
    }
}
