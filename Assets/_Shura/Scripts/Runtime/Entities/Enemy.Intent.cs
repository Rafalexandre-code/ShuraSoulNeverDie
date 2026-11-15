using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Uma batida que vem: em quantos quadros o golpe acerta e que resposta pede.</summary>
    public struct UpcomingBeat
    {
        public int framesToImpact;
        public RingKind kind;
        /// <summary>Posição no combo (0 = o primeiro golpe da sequência).</summary>
        public int index;
    }

    /// <summary>
    /// A intenção de ataque (pedido do diretor: o jogador tem de PREVER o golpe, não adivinhar).
    /// Quando decide atacar, o inimigo planeja o combo inteiro — a preparação de cada golpe,
    /// inclusive a pausa que a leitura adaptativa acrescenta — e só começa depois de uma
    /// antecedência mínima (GameConfig.feel.attackIntentMinLeadFrames): o primeiro golpe nunca
    /// acerta antes desse tempo depois de anunciado. A trilha de ritmo e o brilho da arma leem
    /// esse plano e mostram todas as batidas da sequência vindo de longe.
    /// </summary>
    public sealed partial class Enemy
    {
        MoveDef intentMove;
        int intentLeft;
        int[] plannedWindup = new int[8];
        bool[] plannedAdapted = new bool[8];
        MoveDef plannedFor;

        /// <summary>Está anunciando um golpe (ainda não começou a preparação).</summary>
        public bool HasIntent => intentMove != null;

        /// <summary>Planeja a preparação de cada golpe do combo agora (o sorteio da leitura adaptativa incluído).</summary>
        void PlanMove(MoveDef move)
        {
            int n = move.steps.Length;
            if (plannedWindup.Length < n)
            {
                plannedWindup = new int[n];
                plannedAdapted = new bool[n];
            }
            for (int i = 0; i < n; i++) plannedWindup[i] = PlanWindup(move.steps[i], out plannedAdapted[i]);
            plannedFor = move;
        }

        /// <summary>Decidiu o golpe: anuncia e espera a antecedência mínima, ou começa já se a preparação basta.</summary>
        void CommitMove(MoveDef move)
        {
            PlanMove(move);
            int minLead = feel.attackIntentMinLeadFrames;
            int wait = Mathf.Max(0, minLead - plannedWindup[0]);
            if (wait <= 0)
            {
                StartPlannedMove(move);
                return;
            }
            intentMove = move;
            intentLeft = wait;
        }

        /// <summary>Um quadro da intenção; true quando ela virou golpe (ou continua anunciada).</summary>
        bool TickIntent()
        {
            if (intentMove == null) return false;
            if (--intentLeft > 0) return true;
            MoveDef m = intentMove;
            intentMove = null;
            StartPlannedMove(m);
            return true;
        }

        void CancelIntent()
        {
            if (intentMove == null) return;
            intentMove = null;
            world.Coordinator.Release(this);
        }

        /// <summary>
        /// As batidas que vêm, na ordem: a do golpe em curso (se ainda não acertou) e as dos
        /// próximos golpes do combo, ou as do combo anunciado. Melee que pede resposta, só.
        /// </summary>
        public void UpcomingBeats(List<UpcomingBeat> into)
        {
            into.Clear();
            MoveDef move;
            int t, first;
            if (intentMove != null)
            {
                move = intentMove;
                t = intentLeft;
                first = 0;
            }
            else if (CurrentState == State.Attacking && CurrentMove != null)
            {
                move = CurrentMove;
                AttackDef cur = CurrentAttack;
                if (cur != null && StepFrame < stepWindup && ShowsBeat(cur))
                    into.Add(new UpcomingBeat { framesToImpact = stepWindup - StepFrame, kind = CurrentRingKind, index = StepIndex });
                t = cur != null ? stepWindup + cur.active + cur.recovery - StepFrame : 0;
                first = StepIndex + 1;
            }
            else return;
            bool planned = plannedFor == move;
            for (int i = first; i < move.steps.Length; i++)
            {
                AttackDef a = move.steps[i];
                int w = planned && i < plannedWindup.Length ? plannedWindup[i] : a.windup;
                if (ShowsBeat(a)) into.Add(new UpcomingBeat { framesToImpact = t + w, kind = KindOf(a, planned && plannedAdapted[i]), index = i });
                t += w + a.active + a.recovery;
            }
        }

        static bool ShowsBeat(AttackDef a) => a != null && (a.ShowsTimingRing || a.feint);

        static RingKind KindOf(AttackDef a, bool adapted)
        {
            if (a.feint) return RingKind.Feint;
            if (a.groupStrike) return RingKind.Group;
            if (a.kind == AttackKind.Spirit) return RingKind.Spirit;
            if (a.IsDangerous) return RingKind.Danger;
            if (a.cursed) return RingKind.Cursed;
            if (adapted) return RingKind.Double;
            return RingKind.Parry;
        }
    }
}
