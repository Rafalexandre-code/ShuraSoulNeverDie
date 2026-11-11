using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The eight relics of the design doc (§4.8): every one bends a rule of the game, and every
    /// one asks for something in return.
    /// </summary>
    public static class RoadRelics
    {
        public const string HannyaMask = "rel_mascara_hannya";
        public const string BlankScroll = "rel_pergaminho_em_branco";
        public const string StrawSandals = "rel_sandalias_de_palha";
        public const string VowOfSilence = "rel_voto_de_silencio";
        public const string ExecutionerChain = "rel_corrente_do_carrasco";
        public const string BrokenHourglass = "rel_ampulheta_quebrada";
        public const string SixMonCoin = "rel_moeda_de_seis_mon";
        public const string DragonEye = "rel_olho_do_dragao";

        public static readonly string[] All =
        {
            HannyaMask, BlankScroll, StrawSandals, VowOfSilence, ExecutionerChain, BrokenHourglass, SixMonCoin, DragonEye,
        };

        public static RewardDef Create(string id)
        {
            switch (id)
            {
                case HannyaMask:
                    return new Relic(id, "Máscara Hannya", "O Shura sobe 2× mais rápido.", "O Asura Interior surge com 50% a mais de vida.",
                                     m => { m.shuraGainMult *= 2f; m.innerAsuraHealthMult *= 1.5f; });
                case BlankScroll: return new BlankScrollRelic();
                case StrawSandals:
                    return new Relic(id, "Sandálias de Palha", "Esquiva infinita: as cargas nunca acabam.", "Sem bloqueio: segurar aparar não protege (o aparar perfeito continua).",
                                     m => { m.infiniteDodge = true; m.noBlock = true; });
                case VowOfSilence:
                    return new Relic(id, "Voto de Silêncio", "O Mushin chega com uma Sequência de 6 (em vez de 10).", "A interface fica sempre oculta.",
                                     m => { m.mushinStreak = 6; m.hideHud = true; });
                case ExecutionerChain:
                    return new Relic(id, "Corrente do Carrasco", "Agarrar: aperte E perto de um inimigo para puxá-lo e abalar a postura dele (35).", "Você anda 15% mais devagar.",
                                     m => { m.grab = true; m.moveSpeedMult *= 0.85f; });
                case BrokenHourglass: return new BrokenHourglassRelic();
                case SixMonCoin: return new SixMonCoinRelic();
                case DragonEye:
                    return new Relic(id, "Olho do Dragão", "Você vê o próximo golpe de cada inimigo (um sinal sobre a cabeça).", "A IA aprende o seu jeito 2× mais rápido.",
                                     m => { m.dragonEye = true; m.adaptMult *= 2f; });
                default: return null;
            }
        }
    }

    /// <summary>Copies the last technique absorbed: it is yours at once, without waiting for its card.</summary>
    public sealed class BlankScrollRelic : RewardDef
    {
        public override string Id => RoadRelics.BlankScroll;
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name => "Pergaminho em Branco";
        public override string Gain => "Copia na hora a última técnica absorvida (sem esperar a carta dela).";
        public override string Cost => "−1 opção em toda escolha de recompensa.";

        public override void Apply(PlayerModifiers m) { m.extraRewardChoices -= 1; }

        public override void OnAcquired(PlayerBuild build)
        {
            if (build.PendingTechniques.Count == 0) return;
            string last = build.PendingTechniques[build.PendingTechniques.Count - 1];
            build.Acquire(build.TechniqueCard(last));
        }
    }

    /// <summary>A perfect parry slows the world for a breath; the rings warn a little later.</summary>
    public sealed class BrokenHourglassRelic : RewardDef
    {
        public override string Id => RoadRelics.BrokenHourglass;
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name => "Ampulheta Quebrada";
        public override string Gain => "Parry perfeito desacelera o tempo por 0,3 s.";
        public override string Cost => "Os anéis fecham 10% mais rápido (menos aviso).";

        public override void Apply(PlayerModifiers m) { m.ringWarningMult *= 0.9f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            world.RequestSlowMo(0.3f);
        }
    }

    /// <summary>Paying the ferryman early: one fall a run is refused, and the run is worth half.</summary>
    public sealed class SixMonCoinRelic : RewardDef
    {
        bool spent;

        public override string Id => RoadRelics.SixMonCoin;
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name => "Moeda de Seis Mon";
        public override string Gain => "Revive 1 vez por run, com metade da vida.";
        public override string Cost => "Os Ecos desta run caem pela metade.";

        public override void Apply(PlayerModifiers m) { m.ecosMult *= 0.5f; }

        public override bool OnLethalHit(World world)
        {
            if (spent) return false;
            spent = true;
            PlayerController p = world.Player;
            if (p.Build != null) p.Build.PendingReviveHealth = p.MaxHealth * 0.5f;
            world.Fx.Popup("ROKUMONSEN", p.Position + Vector2.up * 1.3f, world.Config.palette.gold);
            world.Fx.Emit(FxType.FlowUp, p.Position, Vector2.up, 2f, world.Config.palette.gold);
            return true;
        }
    }
}
