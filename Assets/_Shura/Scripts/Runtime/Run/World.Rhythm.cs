using UnityEngine;

namespace Shura
{
    /// <summary>Resultado de uma batida da trilha de ritmo (para o lampejo no centro).</summary>
    public enum RhythmBeat
    {
        None,
        Perfect,
        Block,
        Dodge,
        Swap,
        Hit,
        RiposteOnBeat,
        RiposteMissed,
    }

    /// <summary>
    /// Experimento do diretor (14 nov 2026): a trilha de ritmo, no estilo de BPM. Os golpes que
    /// vêm na sua direção chegam ao centro da trilha no instante ideal da resposta; depois de um
    /// parry perfeito, uma batida de contra-ataque vem do outro lado — atacar no tempo dela dá
    /// um golpe mais forte. Só o anfitrião (e o jogo solo) desenha e usa a trilha.
    /// </summary>
    public sealed partial class World
    {
        /// <summary>Quadros entre o parry perfeito e a batida do contra-ataque.</summary>
        public const int RiposteDelayFrames = 16;
        /// <summary>Tolerância da batida do contra-ataque, em quadros para cada lado.</summary>
        public const int RiposteWindowFrames = 5;
        /// <summary>Contra-ataque no tempo: multiplicadores de postura e de dano do golpe.</summary>
        public const float RipostePostureMult = 1.5f, RiposteDamageMult = 1.2f;

        /// <summary>Ligada pelas opções (o Game repassa a cada quadro): sem ela, nada de batida de contra-ataque.</summary>
        public bool RhythmEnabled { get; set; }
        /// <summary>Quadros de simulação desde o início (o relógio da trilha).</summary>
        public int SimTick { get; private set; }
        /// <summary>Quando a próxima batida de contra-ataque chega ao centro (−1 = nenhuma).</summary>
        public int RiposteDueTick { get; private set; } = -1;
        public RhythmBeat LastBeat { get; private set; }
        public int LastBeatTick { get; private set; } = -1000;
        PlayerController riposteReady;

        void TickRhythm()
        {
            SimTick++;
            if (RiposteDueTick >= 0 && SimTick > RiposteDueTick + RiposteWindowFrames) RiposteDueTick = -1;
        }

        void ClearRhythm()
        {
            RiposteDueTick = -1;
            riposteReady = null;
        }

        void Beat(RhythmBeat b)
        {
            LastBeat = b;
            LastBeatTick = SimTick;
        }

        /// <summary>A resposta do anfitrião a um golpe vira uma batida; o parry perfeito chama o contra-ataque.</summary>
        void OnRhythmAttackResolved(PlayerController p, HitOutcome o)
        {
            if (!RhythmEnabled || p != Host) return;
            switch (o)
            {
                case HitOutcome.PerfectParry:
                    Beat(RhythmBeat.Perfect);
                    RiposteDueTick = SimTick + RiposteDelayFrames;
                    break;
                case HitOutcome.SwapParry: Beat(RhythmBeat.Swap); break;
                case HitOutcome.Blocked: Beat(RhythmBeat.Block); break;
                case HitOutcome.PerfectDodge:
                case HitOutcome.CounterThrust: Beat(RhythmBeat.Dodge); break;
                case HitOutcome.Hit: Beat(RhythmBeat.Hit); break;
            }
        }

        /// <summary>O anfitrião começou um golpe: estava no tempo da batida do contra-ataque?</summary>
        public void NotifyAttackStarted(PlayerController p)
        {
            if (!RhythmEnabled || p != Host || RiposteDueTick < 0) return;
            int d = SimTick - RiposteDueTick;
            if (Mathf.Abs(d) <= RiposteWindowFrames)
            {
                riposteReady = p;
                Beat(RhythmBeat.RiposteOnBeat);
                Fx.Popup(Strings.PopupOnBeat, p.Position + Vector2.up * 1.4f, Config.palette.gold);
            }
            else Beat(RhythmBeat.RiposteMissed);
            RiposteDueTick = -1;
        }

        /// <summary>O primeiro golpe que acerta depois de um contra-ataque no tempo leva o bônus.</summary>
        bool TakeRiposteBonus(PlayerController p)
        {
            if (riposteReady == null || riposteReady != p) return false;
            riposteReady = null;
            return true;
        }
    }
}
