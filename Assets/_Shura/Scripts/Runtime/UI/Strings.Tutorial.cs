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

        // ── The prologue ────────────────────────────────────────────────────────────────
        public const string PrologueTitle = "O Dojo da Montanha";
        public const string PrologueSubtitle = "Monte Kurama · a última noite antes da estrada";
        public const string PrologueWalk = "Os passos ecoam nas tábuas. O mestre já o esperava.";
        public const string PrologueHero = "Você";
        public const string PrologueMaster = "Mestre do Dojo";
        public const string PrologueKeys = "Enter / ataque / clique: continuar  ·  Esc duas vezes: pular o tutorial";
        public const string PrologueCount = "{0} / {1}";
        public const float PrologueCharsPerSecond = 42f;

        // 0 = the duelist, 1 = the master.
        static readonly int[] PrologueWho = { 1, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1 };
        static readonly string[] PrologueLines =
        {
            "Então você veio. Ouvi os seus passos desde o portão de pedra — pesados demais para quem diz estar pronto.",
            "Mestre. Parto ao amanhecer. O Shuradō não vai esperar por mim.",
            "O Shuradō espera por todos nós. É justamente por isso que não há motivo para pressa.",
            "Muitos desceram aquela estrada com lâminas afiadas e mãos impacientes. Nenhum deles voltou para me contar o que viu.",
            "Eu não sou como eles.",
            "Todos disseram isso. A lâmina não ouve palavras, filho. Ela só ouve a respiração de quem a empunha.",
            "Fique esta noite. Mostre-me a sua guarda, o seu passo, o seu silêncio. Aprenda a ler o golpe antes que ele exista.",
            "Se ao fim você ainda estiver de pé diante de mim, eu mesmo abro o portão.",
            "...Então me ensine, mestre.",
            "Primeiro, olhe. Antes de lutar, saiba o que os seus olhos precisam ler.",
            "E lembre: aqui ninguém morre. Lá fora, ninguém perdoa.",
        };

        public static int PrologueLineCount => PrologueLines.Length;
        public static string PrologueText(int i) => PrologueLines[UnityEngine.Mathf.Clamp(i, 0, PrologueLines.Length - 1)];
        public static int PrologueSpeaker(int i) => PrologueWho[UnityEngine.Mathf.Clamp(i, 0, PrologueWho.Length - 1)];

        // ── The HUD tour ─────────────────────────────────────────────────────────────────
        public const string TutorialTourStep = "CONHEÇA A TELA · {0} / {1}";
        public const string TutorialTourNext = "Continuar ▸";
        public const string TutorialTourLast = "Ao dojo ▸";
        public const string TutorialTourBack = "◂ Voltar";
        public const string TutorialTourKeys = "Enter / ataque / clique: continuar  ·  ←: voltar  ·  Esc duas vezes: pular o tutorial";
        public const string TutorialSkipArmed = "Aperte Esc de novo para pular o tutorial  ·  qualquer outra coisa continua";

        static readonly GameUI.HudPart[] TourParts =
        {
            GameUI.HudPart.None, GameUI.HudPart.Portrait, GameUI.HudPart.Stances, GameUI.HudPart.Health, GameUI.HudPart.Posture,
            GameUI.HudPart.Breath, GameUI.HudPart.Shura, GameUI.HudPart.Dodge, GameUI.HudPart.Wounds, GameUI.HudPart.Status,
            GameUI.HudPart.Flow, GameUI.HudPart.Ecos, GameUI.HudPart.Weapons, GameUI.HudPart.Gourd, GameUI.HudPart.Bandage,
            GameUI.HudPart.Tool, GameUI.HudPart.Mon, GameUI.HudPart.Log, GameUI.HudPart.None,
        };

        static readonly string[] TourTitles =
        {
            "Bem-vindo ao dojo", "Você e a sua guarda", "As quatro guardas", "Vida", "Postura", "Fôlego", "Shura", "Esquivas",
            "Ferimentos", "Linha de estado", "Fluxo", "Ecos", "Suas duas armas", "Cabaça", "Bandagem", "Ferramenta e papéis",
            "Mon", "Registro", "Agora, à prática",
        };

        static readonly string[] TourTexts =
        {
            "Antes de empunhar a lâmina, conheça a sua tela. Cada parte do HUD conta algo que você vai precisar no meio da luta. O tempo está parado: leia com calma.",
            "O medalhão mostra o seu duelista na pose da guarda atual (o kanji embaixo diz qual). O anel em volta acende em dourado quando você tem Fluxo, e fica vermelho no Asura.",
            "Jōdan, Chūdan, Gedan e Waki. Cada guarda muda o dano, a janela do aparar e o jeito dos cortes. A acesa é a que você está usando. Troque com 1–4 (no controle, o direcional).",
            "Chegou a zero, a jornada acaba. A faixa clara mostra o dano que acabou de entrar. Cure com a cabaça, ao vencer cada sala e com algumas técnicas e itens.",
            "Enche quando você bloqueia ou leva golpes na guarda. Cheia, a sua guarda QUEBRA e você fica aberto. O aparar perfeito não gasta a sua postura — e enche a do inimigo.",
            "Cada golpe, esquiva e bloqueio gasta fôlego. Sem fôlego você fica EXAUSTO: mais lento e com a janela do aparar menor. Ele volta sozinho quando você para de atacar.",
            "A fúria do caminho: sobe a cada morte e a cada golpe. Em níveis altos você bate mais forte, mas os anéis de tempo somem. No máximo, você cai no Shuradō. Medite nos santuários para acalmar.",
            "Cada círculo é uma carga de esquiva; elas recarregam com o tempo. A esquiva no instante certo atravessa o golpe sem dano (a esquiva perfeita).",
            "O boneco mostra onde você foi ferido: braço (golpes mais fracos), perna (mais lento), tronco (menos postura), olhos (sem anéis). A bandagem trata os ferimentos.",
            "Aqui aparecem a CARGA dos Elos (trocas seguidas), a maldição que você carrega e a sua ALMA — quando uma técnica chega ao nível III, você pode virar aquele inimigo por 8 s.",
            "Lutar bem enche o Fluxo: cada ensō aceso é um nível. Mais Fluxo é mais dano e um duelo mais rápido. Levar dano apaga um nível. A barra mostra quanto falta para o próximo.",
            "A moeda das almas, ganha lutando. Os Ecos ficam com você quando a jornada acaba: gaste no Altar das Almas, no menu, para ficar mais forte em todas as jornadas.",
            "A arma acesa está na mão; a outra espera. Trocar no instante em que o anel fecha é a TROCA PERFEITA, o golpe mais forte do jogo. A tecla de trocar aparece no canto.",
            "Cada cabaça cheia é um gole de cura. Beber deixa você parado por um instante — escolha a hora. Elas se enchem a cada sala.",
            "Trata os ferimentos e devolve um pouco de vida. No controle, segure o botão da cabaça.",
            "A ferramenta em mãos (shuriken, bomba, sino de bronze...) e os papéis espirituais que ela gasta. Uma tecla lança, a outra troca de ferramenta.",
            "O dinheiro da jornada: compra itens com mercadores, serviços do ferreiro e favores pelo caminho. Ganho a cada inimigo e a cada sala.",
            "Tudo o que aconteceu — itens, técnicas, avisos — fica escrito aqui. Clique ou aperte C (no controle, segure View) para abrir.",
            "Agora, ao dojo. Cada lição só termina quando você faz o que ela pede, e o mestre só ataca quando a lição manda. Esc duas vezes pula o tutorial a qualquer momento.",
        };

        public static int TutorialTourCount => TourTitles.Length;
        public static GameUI.HudPart TutorialTourPart(int i) => TourParts[UnityEngine.Mathf.Clamp(i, 0, TourParts.Length - 1)];
        public static string TutorialTourTitle(int i) => TourTitles[UnityEngine.Mathf.Clamp(i, 0, TourTitles.Length - 1)];
        public static string TutorialTourText(int i) => TourTexts[UnityEngine.Mathf.Clamp(i, 0, TourTexts.Length - 1)];

        public static string TutorialTitle(Tutorial.Step s)
        {
            switch (s)
            {
                case Tutorial.Step.Prologue: return "Prólogo";
                case Tutorial.Step.Hud: return "Conheça a tela";
                case Tutorial.Step.LockOn: return "Travar a mira";
                case Tutorial.Step.Bandage: return "A bandagem";
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
                case Tutorial.Step.Prologue:
                    return "";
                case Tutorial.Step.Hud:
                    return "Cada parte da tela, uma de cada vez.";
                case Tutorial.Step.LockOn:
                    return pad ? "No controle a mira trava sozinha no inimigo (a retícula vermelha): a lâmina sempre aponta para ele. Empurre o analógico direito para pular para outro alvo e aperte R3 para escolher de novo à sua frente. Fique travado no mestre."
                               : "Travar a mira faz a lâmina sempre apontar para um inimigo (a retícula vermelha). No teclado ela é opcional — ligue em Configurações → Trava de mira. Experimente agora: aperte Z perto do mestre e fique travado nele (a roda do mouse troca de alvo).";
                case Tutorial.Step.Bandage:
                    return "Você levou um corte no braço (veja o boneco ao lado da vida): golpes mais fracos até tratar. Use a bandagem: ela trata os ferimentos e devolve um pouco de vida."
                           + (pad ? " No controle, segure o botão da cabaça." : "");
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
                case Tutorial.Step.LockOn: return pad ? new[] { K(ControlAction.LockOn), c != null && c.PadStyle == GlyphStyle.PlayStation ? "R ↔" : "RS ↔" } : new[] { K(ControlAction.LockOn) };
                case Tutorial.Step.Bandage: return pad ? new[] { "segure " + K(ControlAction.Bandage) } : new[] { K(ControlAction.Bandage) };
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
