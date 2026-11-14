using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A janela do parry perfeito (WP-03, regra R5 do plano): constante em todo o jogo. Com
    /// Feature.ParryWindowModifiers = false, nada da build muda a janela (kamae, idade, Altar,
    /// relíquias, tsubas, itens, técnicas, exaustão, Mushin); só a assistência de acessibilidade
    /// (WP-17) pode somar quadros. Com a flag ligada, volta a fórmula da v0.15.0.
    /// </summary>
    public static class ParryWindow
    {
        public static int Frames(PlayerTuning t, PlayerModifiers mods, int kiBonus, int ageBonus, int assist, bool exhausted)
        {
            if (!Feature.ParryWindowModifiers) return Mathf.Max(2, t.perfectParryFrames + assist);
            return Mathf.Max(2, (t.perfectParryFrames + (mods != null ? mods.perfectParryFrameBonus : 0) + kiBonus + ageBonus + assist) / (exhausted ? 2 : 1));
        }

        /// <summary>
        /// Perdão no fim da janela (D-11, "designers mentem a favor do jogador"): +N quadros
        /// quando o anel daquele golpe ficou visível por tempo suficiente para ser lido.
        /// </summary>
        public static int Forgiveness(PlayerTuning t, AttackDef attack, int ringApproachFrames)
        {
            if (attack == null) return 0;
            int visible = Mathf.Min(attack.windup, ringApproachFrames);
            return visible > t.forgivenessMinRingFrames ? t.parryLateForgivenessFrames : 0;
        }
    }
}
