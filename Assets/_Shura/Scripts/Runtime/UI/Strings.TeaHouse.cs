namespace Shura
{
    /// <summary>Phase 4, part 2: the Tea House, the rituals, the scrolls, the modes, the layers.</summary>
    public static partial class Strings
    {
        // ── Rituals ──
        public const string PopupBow = "REVERÊNCIA";
        public const string PopupFoeBows = "O adversário se curva";
        public const string PopupCoward = "GOLPE COVARDE";
        public const string PopupPraying = "orando…";
        public const string PopupChiburi = "CHIBURI";
        public const string PopupNotoSlow = "nōtō…";
        public const string PopupNoto = "NŌTŌ  +5 ECOS";
        public const string PopupNotoEarly = "cedo demais";
        public const string PopupNotoLate = "a lâmina escorregou";
        public const string ToastScrollDone = "Pergaminho completo: {0}. Pintura sumi-e na Biblioteca, +{1} Fragmentos.";
        public const string KitsuneLies = "Uma relíquia… e Kohaku pisca: o chá é maldito.";
        public const string KitsuneTruth = "Uma relíquia… e Kohaku não pisca: o chá é bom.";
        public const string ToastTsubakiHonor = "Tsubaki espalhou a notícia: +1 Karma e +5 pétalas.";

        static readonly string[] PrayerLines =
        {
            "O incenso sobe. Alguém deixou aqui um nome escrito em papel de arroz, e a chuva já o apagou.",
            "A pedra está morna. Dizem que as lanternas guardam o último calor de quem morreu perto delas.",
            "Uma moeda de seis mon aos pés da estátua: o preço da travessia do rio Sanzu.",
            "Há marcas de lâmina na base. Alguém treinou aqui por muitas vidas antes de você.",
            "O musgo cobre um poema antigo. Só se lê a última linha: \"…e voltei à roda.\"",
            "As oferendas são frescas. Quem as deixou também está preso no Shuradō.",
            "Uma lanterna que nunca se apaga. Os monges dizem que é a alma de um guarda que não quis partir.",
            "O vento traz o som de um sino distante. Ninguém aqui sabe onde fica o templo que o toca.",
            "Há sangue seco no altar, de muitas cores. Yokai e homens rezam para os mesmos deuses.",
            "A estátua não tem rosto. Foi o Ferreiro sem Rosto que a esculpiu, e ele esquece de propósito.",
            "Uma raposa de pedra, com a cauda quebrada. Kohaku diria que é um retrato.",
            "Alguém riscou na pedra o número de vezes que morreu. A conta para no trezentos e doze.",
            "O incenso desenha no ar a forma de uma lâmina e se desfaz. Bom presságio, ou aviso.",
            "Aos pés do Jizō, sandálias de criança. A Zashiki-warashi as procura há séculos.",
            "As velas tremem quando você se ajoelha. O Shuradō reconhece os que ainda rezam.",
            "Uma tábua votiva pede: \"Que meu filho nunca empunhe uma espada.\" Está assinada por um ronin.",
        };

        public static string PrayerLine(int i) => PrayerLines[((i % PrayerLines.Length) + PrayerLines.Length) % PrayerLines.Length];

        // ── The Tea House ──
        public const string TeaTitle = "CASA DE CHÁ";
        public const string TeaSubtitle = "Os hóspedes lembram da sua última vida. Cada um guarda uma missão pessoal.";
        public const string TeaMission = "MISSÃO";
        public const string TeaReward = "Recompensa: {0}";
        public const string TeaClaim = "Receber";
        public const string TeaDone = "Missão cumprida";
        public const string TeaClaimed = "{0}: missão cumprida!";
        public const string TeaControls = "Setas para escolher  ·  Enter / A para receber a recompensa  ·  Esc / B para voltar";

        // ── The scrolls ──
        public const string ScrollTitle = "PERGAMINHO";
        public const string ScrollLocked = "A Zashiki-warashi guarda os pergaminhos (Casa de Chá).";
        public const string ScrollLegend = "LENDA";
        public const string ScrollDoneText = "Pergaminho completo: a técnica dele já vem no nível II.";

        // ── The modes ──
        public const string ModesMenu = "Modos de jogo";
        public const string ModesControls = "Setas para escolher  ·  Enter / A para começar neste modo  ·  Esc / B para voltar";
        public const string ModesTitle = "MODOS DE JOGO";
        public const string ModesSubtitle = "Escolha como caminhar a estrada. O arsenal vem em seguida.";
        public const string ModeLocked = "O Modo Mestre é o presente de Mestre Zatō (Casa de Chá).";
        public const string ModeLockedShort = "Fale com Mestre Zatō";
        public const string DailyDone = "O Desafio de hoje já foi tentado. Volte amanhã.";
        public const string DailyToday = "Hoje: {0}";
        public const string DailyTodayDone = "Tentado hoje";
        public const string DailyRanking = "SEUS DIAS";
        public const string DailyRankingEmpty = "Nenhum desafio ainda.";
        public const string DailyRow = "{0}   ·   profundidade {1}   ·   {2} Ecos";
        public const string ModeBest = "Recorde: {0}";
        public const string ModeEndings = "Finais vistos: {0}";
        public const string ModeChosen = "Modo: {0}";

        // ── The end of the road ──
        public const string VictoryTowerTitle = "O TOPO DA TORRE";
        public const string VictoryTowerText = "Cem duelos, cem adversários que se curvaram ou caíram. Do alto da torre, o Shuradō parece pequeno, e o próximo andar é o céu.";
        public const string VictorySubtitle = "A estrada chegou ao fim.";
        public const string NewJourney = "Nova jornada";

        // ── The layers (§7.7) ──
        public const string TempleLocked = "O Templo se abre depois de {0} runs completas.";
        public const string LayerLocked = "Abre após {0} runs";
        public const string OathsLocked = "Os Juramentos se abrem depois de {0} runs completas.";
        public const string LineageLocked = "A linhagem começa depois de {0} runs: até lá, o primeiro corpo não envelhece.";
        public const string LegacyUnlocked = "NOVO: {0}";
        public const string UnlockAll = "Liberar todos os sistemas";
        public const string HelpUnlockAll = "Novas almas conhecem o jogo aos poucos: selos, cabaça e mapa na 2ª run; guardas, ferramentas e Templo na 4ª; reações e Shura na 7ª; fusões, Juramentos e linhagem na 11ª. Ligue para abrir tudo de uma vez.";
    }
}
