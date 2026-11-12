namespace Shura
{
    /// <summary>The ruined temple, the oaths, the lineage (design doc §7 and §8).</summary>
    public static partial class Strings
    {
        public const string Temple = "Templo em Ruínas";
        public const string TempleTitle = "TEMPLO EM RUÍNAS";
        public const string TempleSubtitle = "Entre uma vida e outra, a alma volta ao templo. Reconstrua-o com Ecos e Aço Celeste.";
        public const string TempleBuiltLabel = "Reconstruído";
        public const string TempleRuinLabel = "Em ruínas";
        public const string TempleSoonLabel = "Ainda em ruínas";
        public const string TempleCost = "{0} Ecos";
        public const string TempleCostSteel = "{0} Ecos · {1} Aço";
        public const string TempleCannotBuild = "Faltam Ecos ou Aço Celeste para reconstruir.";
        public const string TempleBuilt = "{0} reconstruído!";
        public const string TempleEnter = "Entrar";
        public const string TempleRebuild = "Reconstruir";
        public const string TempleControls = "Setas / analógico para escolher  ·  Enter / A para entrar ou reconstruir  ·  Esc / B para voltar";
        public const string TempleListControls = "Cima / Baixo para escolher  ·  Esc / B para voltar";
        public const string TempleNeedForge = "A Forja está em ruínas: reconstrua-a no Templo.";
        public const string ArsenalForgeRuined = "Forja em ruínas";

        public const string CurrencyEcos = "Ecos";
        public const string CurrencySteel = "Aço Celeste";
        public const string CurrencyFragments = "Fragmentos de Alma";
        public const string CurrencyPetals = "Pétalas";
        public const string KarmaShura = "Shura";
        public const string KarmaBushido = "Bushidō";
        public const string KarmaLabel = "Karma {0:+0;−0;0}";

        public const string DiscipleLabel = "O DISCÍPULO";
        public const string DiscipleInherits = "Herdou: {0}";
        public const string DiscipleJourneys = "Jornadas além da profundidade 10: {0}";
        public const string DiscipleSenseis = "Senseis no Dojo: {0} (+{1} de vida, +{2}% de dano)";
        public const string DiscipleNone = "Nenhum corpo ainda: o primeiro discípulo nasce na próxima jornada.";

        // ── The Altar's tree ──
        public const string AltarTreeSubtitle = "Cada nível custa um ponto, e o fim de um ramo custa dois. Os pontos são limitados: escolha um perfil.";
        public const string AltarPoints = "Pontos {0} / {1}";
        public const string AltarPointsHint = "+1 ponto a cada 3 runs (até {0})";
        public const string AltarNodeNeeds = "Pede {0} níveis neste ramo · {1} Ecos · {2} Fragmentos";
        public const string AltarNodeOwned = "Desperto";
        public const string AltarNodeLocked = "Ainda não: faltam níveis no ramo, Ecos, Fragmentos ou pontos.";
        public const string AltarNodeOpened = "{0} desperta no Altar!";
        public const string AltarResetLabel = "Redistribuir pontos";
        public const string AltarReset = "Os pontos voltaram: Ecos e Fragmentos devolvidos.";
        public const string AltarNoPoints = "Sem pontos livres: redistribua ou viva mais runs.";
        public const string AltarTreeControls = "Setas para escolher  ·  Enter / A para comprar  ·  Esc / B para voltar";

        // ── The Zen Garden ──
        public const string GardenTitle = "JARDIM ZEN";
        public const string GardenSubtitle = "Jure dificuldades para as próximas jornadas. Cada Juramento soma Calor, e o Calor multiplica Ecos e Pétalas.";
        public const string GardenOaths = "JURAMENTOS";
        public const string GardenHeat = "Calor {0}  ·  Ecos ×{1:0.0}";
        public const string GardenSealed = "Selado: chega com os Rivais.";
        public const string GardenSeed = "Semente da jornada";
        public const string GardenSeedRandom = "Nova a cada jornada";
        public const string GardenSeedHint = "Enter para escrever (números ou palavras); vazio = aleatória.";
        public const string GardenColors = "CORES DA LÂMINA";
        public const string GardenOrnaments = "ORNAMENTOS DO JARDIM";
        public const string GardenPrice = "{0} pétalas";
        public const string GardenWorn = "Na lâmina";
        public const string GardenOwned = "Seu";
        public const string GardenBought = "{0}: seu!";
        public const string GardenControls = "Setas para escolher  ·  Esquerda / Direita mudam o nível do Juramento  ·  Enter / A para comprar ou vestir  ·  Esc / B para voltar";

        // ── The Graveyard ──
        public const string GraveyardTitle = "CEMITÉRIO DA LINHAGEM";
        public const string GraveyardSubtitle = "Cada corpo que a alma usou descansa aqui, com sua arma e seu último poema.";
        public const string GraveyardEmpty = "Nenhum túmulo ainda. Que continue assim por muito tempo.";
        public const string GraveyardSensei = "Sensei aposentado aos {0} anos";
        public const string GraveyardFell = "Caiu aos {0} anos, na profundidade {1}";
        public const string GraveyardKilledBy = "Pela lâmina de: {0}";
        public const string GraveyardWeapon = "A arma no túmulo: {0}";

        // ── The Library ──
        public const string LibraryTitle = "BIBLIOTECA";
        public const string LibrarySubtitle = "O bestiário de quem você já enfrentou, e os pergaminhos do Shuradō.";
        public const string LibraryUnknown = "???";
        public const string LibraryUnknownText = "Ainda não cruzou o seu caminho.";
        public const string LibraryKills = "Derrotados: {0}";
        public const string LibraryStats = "Vida {0}  ·  Postura {1}  ·  Golpes {2}";
        public const string LibraryAnswer = "Resposta à assinatura: {0}";
        public const string LibraryTechnique = "Técnica que deixa: {0}";
        public const string LibraryScrolls = "PERGAMINHOS DO SHURADŌ";
        public const string LibraryScrollLocked = "Pergaminho lacrado ({0} runs)";

        // ── The Dojo ──
        public const string DojoTitle = "DOJO";
        public const string DojoSubtitle = "O mestre espera, as lâminas lembram, e os velhos discípulos ensinam.";
        public const string DojoTrain = "Treinar com o mestre";
        public const string DojoMastery = "MAESTRIA";
        public const string DojoSenseis = "SENSEIS";
        public const string DojoNoSenseis = "Nenhum discípulo chegou aos 70 anos ainda.";

        // ── The heir ──
        public const string HeirTitle = "A LINHAGEM CONTINUA";
        public const string HeirSubtitle = "{0} caiu. A alma escolhe o próximo corpo.";
        public const string HeirSubtitleRetired = "{0} agora ensina no Dojo. A alma escolhe o próximo corpo.";
        public const string HeirSubtitleAny = "A alma escolhe o próximo corpo.";
        public const string HeirChoose = "Escolher o herdeiro";
        public const string HeirChosen = "{0} empunha a lâmina da linhagem.";
        public const string HeirAge = "{0} anos · {1}";
        public const string HeirControls = "Esquerda / Direita para escolher  ·  Enter / A para seguir com este discípulo";

        // ── The end screen ──
        public const string LegacyTitle = "O QUE FICA";
        public const string LegacySteel = "+{0} Aço Celeste";
        public const string LegacyFragments = "+{0} Fragmentos de Alma";
        public const string LegacyPetals = "+{0} Pétalas";
        public const string LegacyKarma = "Karma {0:+0;−0;0}";
        public const string LegacyAged = "{0} volta ao templo, 5 anos mais velho ({1} anos).";
        public const string LegacyRetired = "{0} chega aos {1} anos e vira Sensei no Dojo.";
        public const string LegacyDied = "{0} cai aos {1} anos. O poema fica no túmulo:";
        public const string LegacyTurnedBack = "{0} volta ao templo sem ter descido além da profundidade 10.";
        public const string LegacyHeat = "Calor {0}";

        // ── The road ──
        public const string HudHaste = "Pressa · {0} s";
        public const string HudHasteOver = "O tempo acabou";
        public const string ToastHasteOver = "Pressa: o tempo da sala acabou, a vida escorre.";
        public const string ArsenalNeedOne = "Juramento: escolha uma arma";
        public const string ArsenalNeedTwoOrThree = "Escolha duas ou três armas";
        public const string ArsenalThird = "A 3ª lâmina fica embainhada e entra na troca.";

        static readonly string[] Scrolls =
        {
            "O Shuradō (修羅道) é um dos seis reinos de renascimento: o reino onde guerreiros lutam para sempre.",
            "Você é uma alma de samurai presa nele. Cada jornada é uma reencarnação; cada discípulo, um corpo emprestado.",
            "Os Ecos são memórias de vidas passadas. Por isso sobrevivem à morte e voltam ao Altar.",
            "Absorver uma técnica é literal: aqui, quem vence leva um pedaço da alma do vencido.",
            "Os inimigos estão presos no mesmo ciclo. Também morrem, renascem, e lembram de você.",
            "O Ashura, rei do reino, absorveu tantas almas que não consegue mais morrer.",
            "Dizem que há duas saídas da roda: a libertação, ou tomar o trono e tornar-se o próximo Ashura.",
        };

        static readonly int[] ScrollRuns = { 0, 1, 2, 4, 6, 9, 12 };

        public static int ScrollCount => Scrolls.Length;
        public static string Scroll(int i) => Scrolls[i];
        public static int ScrollUnlockRuns(int i) => ScrollRuns[i];
    }
}
