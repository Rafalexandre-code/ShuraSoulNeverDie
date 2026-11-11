namespace Shura
{
    /// <summary>The words of the dojo: one title, one instruction and the buttons of every lesson.</summary>
    public static partial class Strings
    {
        public const string TutorialMenu = "Tutorial (o dojo)";
        public const string TutorialSkip = "Pular o tutorial";
        public const string TutorialSkipLesson = "Pular esta lição";
        public const string TutorialLesson = "LIÇÃO {0} / {1}";
        public const string TutorialWellDone = "Muito bem!";
        public const string TutorialNow = "AGORA!";
        public const string TutorialFreezeParry = "O anel fechou: aperte para APARAR";
        public const string TutorialFreezeDodge = "Golpe vermelho: aperte para ESQUIVAR";
        public const string TutorialFreezeSwap = "O anel fechou: TROQUE de arma";
        public const string TutorialFrozenNote = "O tempo parou para você ver o momento certo.";
        public const string TutorialHere = "aqui";
        public const string TutorialDoneTitle = "O dojo está concluído";
        public const string TutorialDoneText = "Você conhece o caminho da lâmina. Escolha as suas armas — a estrada espera.";
        public const string TutorialPausedHint = "Você pode voltar ao dojo quando quiser, no menu inicial.";
        public const string TutorialStanceJodan = "1 Jōdan · alta: +25% de dano, aparar mais difícil";
        public const string TutorialStanceChudan = "2 Chūdan · média: equilíbrio, contra-ataque forte";
        public const string TutorialStanceGedan = "3 Gedan · baixa: aparar mais fácil, esquiva curta";
        public const string TutorialStanceWaki = "4 Waki · oculta: a lâmina some atrás do corpo";

        public static string TutorialTitle(Tutorial.Step s)
        {
            switch (s)
            {
                case Tutorial.Step.Move: return "Andar";
                case Tutorial.Step.Attack: return "Atacar";
                case Tutorial.Step.Combo: return "O combo";
                case Tutorial.Step.Block: return "Bloquear";
                case Tutorial.Step.Parry: return "O aparar perfeito";
                case Tutorial.Step.ParryFree: return "Aparar sozinho";
                case Tutorial.Step.Dodge: return "O golpe vermelho";
                case Tutorial.Step.DodgeFree: return "Esquivar sozinho";
                case Tutorial.Step.Posture: return "Quebrar a postura";
                case Tutorial.Step.Finisher: return "Finalizar";
                case Tutorial.Step.Swap: return "Duas armas";
                case Tutorial.Step.SwapParry: return "A Troca Perfeita";
                case Tutorial.Step.Stances: return "As quatro guardas";
                case Tutorial.Step.Gourd: return "A cabaça";
                case Tutorial.Step.Tool: return "Ferramentas";
                case Tutorial.Step.Seals: return "Selos";
                case Tutorial.Step.Kiai: return "Kiai";
                case Tutorial.Step.Duel: return "A prova";
                default: return TutorialDoneTitle;
            }
        }

        public static string TutorialText(Tutorial.Step s, bool pad, bool sealReady)
        {
            switch (s)
            {
                case Tutorial.Step.Move:
                    return pad ? "Ande com o analógico esquerdo até os três círculos dourados. O samurai olha para onde o analógico direito aponta."
                               : "Ande com W A S D até os três círculos dourados. O samurai olha sempre para o mouse.";
                case Tutorial.Step.Attack:
                    return "Este é o mestre do dojo. Mire nele e acerte três golpes. Cada golpe gasta um pouco de FÔLEGO (a barra verde).";
                case Tutorial.Step.Combo:
                    return "Golpes em sequência formam um COMBO: aperte de novo logo depois de cada corte até o último golpe, o mais forte. Faça o combo inteiro duas vezes.";
                case Tutorial.Step.Block:
                    return "O mestre vai atacar. SEGURE o botão de aparar para bloquear. Bloquear salva a sua vida, mas enche a SUA postura (a barra laranja) — cheia, a sua guarda quebra.";
                case Tutorial.Step.Parry:
                    return "Um anel AZUL se fecha sobre quem ataca. Aperte aparar no instante em que ele FECHA: é o parry perfeito, que não gasta a sua postura e enche a do inimigo. O tempo vai parar para mostrar o momento.";
                case Tutorial.Step.ParryFree:
                    return "Agora sem ajuda: olhe o anel azul e aperte quando ele fechar (dá para ouvir o \"tim\"). Três parries perfeitos.";
                case Tutorial.Step.Dodge:
                    return "Golpes com anel VERMELHO não podem ser aparados. Esquive no instante em que o anel fecha: você atravessa o golpe sem dano. O tempo vai parar no momento certo.";
                case Tutorial.Step.DodgeFree:
                    return "Sozinho agora: quando o anel vermelho fechar, esquive (para o lado ou através do golpe). Duas vezes.";
                case Tutorial.Step.Posture:
                    return "Vida não é tudo: cada golpe e cada parry perfeito enchem a POSTURA do inimigo (a barra laranja dele). Cheia, ela quebra. Quebre a postura do mestre!";
                case Tutorial.Step.Finisher:
                    return "Postura quebrada: ele está aberto. Ataque para FINALIZAR. Uma linha dourada aparece — aponte a lâmina sobre ela antes de atacar: quanto mais exato, maior a recompensa.";
                case Tutorial.Step.Swap:
                    return "Você carrega duas armas. Troque para a outra: cada uma tem alcance, ritmo e finalização próprios.";
                case Tutorial.Step.SwapParry:
                    return "A técnica mais forte do jogo: TROQUE de arma no instante em que o anel fecha. É um parry perfeito que solta o golpe especial da nova arma. O tempo vai parar para você.";
                case Tutorial.Step.Stances:
                    return "A guarda muda como você luta e como a lâmina se move. Experimente as quatro (cada uma tem a sua pose e os seus cortes).";
                case Tutorial.Step.Gourd:
                    return "Você está ferido. Beba da cabaça para se curar — mas beber deixa você parado por um momento. Ela se reenche a cada sala.";
                case Tutorial.Step.Tool:
                    return pad ? "Ferramentas gastam papéis espirituais. RT lança a ferramenta em mãos; LT troca de ferramenta. Lance uma no mestre."
                               : "Ferramentas gastam papéis espirituais. R lança a ferramenta em mãos; T troca de ferramenta. Lance uma no mestre.";
                case Tutorial.Step.Seals:
                    return sealReady ? "Há um SELO vermelho sobre o mestre. Detone agora: cada selo explode com dano e postura."
                                     : "Cada parry perfeito marca um SELO no inimigo. Apare perfeitamente o próximo golpe do mestre.";
                case Tutorial.Step.Kiai:
                    return "O KIAI é um grito de guerra: gasta um nível de Fluxo e interrompe golpes vermelhos de perto. Grite!";
                case Tutorial.Step.Duel:
                    return "Tudo junto agora. Um ronin de verdade entrou no dojo: apare, quebre a postura e finalize. Aqui ninguém morre — lute sem medo.";
                default:
                    return TutorialDoneText;
            }
        }

        public static string[] TutorialKeys(Tutorial.Step s, bool pad, bool sealReady)
        {
            // The player's own buttons (remapped or not), named for the device in the hands.
            InputService c = InputService.Active;
            string K(ControlAction a) => c != null ? c.Glyph(a) : "?";
            switch (s)
            {
                case Tutorial.Step.Move: return pad ? new[] { c != null && c.PadStyle == GlyphStyle.PlayStation ? "L" : "LS" } : new[] { "W", "A", "S", "D" };
                case Tutorial.Step.Attack:
                case Tutorial.Step.Combo:
                case Tutorial.Step.Posture:
                case Tutorial.Step.Finisher:
                    return new[] { K(ControlAction.Attack) };
                case Tutorial.Step.Block:
                case Tutorial.Step.Parry:
                case Tutorial.Step.ParryFree:
                    return new[] { K(ControlAction.Parry) };
                case Tutorial.Step.Dodge:
                case Tutorial.Step.DodgeFree:
                    return new[] { K(ControlAction.Dodge) };
                case Tutorial.Step.Swap:
                case Tutorial.Step.SwapParry:
                    return new[] { K(ControlAction.Swap) };
                case Tutorial.Step.Stances:
                    return new[] { K(ControlAction.Kamae1), K(ControlAction.Kamae2), K(ControlAction.Kamae3), K(ControlAction.Kamae4) };
                case Tutorial.Step.Gourd: return new[] { K(ControlAction.Gourd) };
                case Tutorial.Step.Tool: return new[] { K(ControlAction.Tool), K(ControlAction.ToolCycle) };
                case Tutorial.Step.Seals:
                    if (!sealReady) return new[] { K(ControlAction.Parry) };
                    return pad ? new[] { K(ControlAction.Attack) + " + " + K(ControlAction.Parry) } : new[] { K(ControlAction.Detonate) };
                case Tutorial.Step.Kiai:
                    return pad ? new[] { K(ControlAction.Parry) + " + " + K(ControlAction.Dodge) } : new[] { K(ControlAction.Kiai) };
                default: return new string[0];
            }
        }
    }
}
