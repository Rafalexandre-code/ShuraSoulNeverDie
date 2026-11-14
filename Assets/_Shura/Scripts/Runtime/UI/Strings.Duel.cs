namespace Shura
{
    /// <summary>Phase 5, part 5: the Temple courtyard as the lobby (§9.7) and the PvP duel (§9.6).</summary>
    public static partial class Strings
    {
        // The courtyard.
        public const string CourtTitle = "Pátio do Templo";
        public const string CourtCircle = "CÍRCULO DE DUELO";
        public const string CourtTorii = "TORII";
        public const string CourtTea = "CASA DE CHÁ";
        public const string CourtStone = "PEDRA DO JURAMENTO";
        public const string CourtDueling = "duelando";
        public const string CourtSitting = "tomando chá";
        public const string CourtTouch = "toque!";
        public const string CourtParried = "aparou!";
        public const string CourtChallenge = "desafia!";
        public const string CourtWithdraw = "desistiu";
        public const string CourtHostStarts = "o anfitrião começa a jornada pelo menu (Esc)";
        public const string CourtAtCircle = "E: pedir um duelo — quando dois pedirem no círculo, o duelo começa";
        public const string CourtAtCircleWaiting = "esperando um adversário no círculo (E: desistir)";
        public const string CourtAtTea = "E: sentar para o chá";
        public const string CourtAtStone = "E: votar o Juramento do grupo";
        public const string CourtAtToriiGuest = "E: pronto para partir";
        public const string CourtDuelBegins = "Duelo no círculo: {0} contra {1}!";
        public const string CourtSeason = "Temporada de {0}  ·  {1} ({2})  ·  {3}V {4}D";
        public const string CourtHint = "Andar: WASD / analógico   ·   {0}: golpe de treino   ·   {1}: aparar   ·   {2}: usar   ·   1–4: kamae do duelo   ·   Esc: menu da sala";

        public static readonly string[] CourtTeaLines =
        {
            "“O chá esfria enquanto você espera. Beba.”",
            "“Ninguém vence sentado. Ninguém perde também.”",
            "“A lâmina descansa na bainha; o duelista, na xícara.”",
            "“Quem serve o chá vê as mãos de todos.”",
            "“Amanhã a roda gira. Hoje, só o vapor.”",
        };

        // The duel.
        public const string DuelHint = "{0}: golpe   ·   {1}: golpe vermelho   ·   {2}: aparar (segure: guarda)   ·   {3}: esquiva   ·   anel ciano: apare   ·   anel vermelho: esquive";
        public const string DuelNetLine = "rollback · atraso {0} frames · voltas {1} · vantagem {2}";
        public const string DuelRound = "Round {0}";
        public const string DuelBegin = "COMECE";
        public const string DuelRoundDraw = "Round empatado";
        public const string DuelRoundTo = "{0} leva o round";
        public const string DuelYou = "  (você)";
        public const string DuelWon = "VITÓRIA";
        public const string DuelLost = "DERROTA";
        public const string DuelDraw = "EMPATE";
        public const string DuelDesync = "DUELO ANULADO";
        public const string DuelGone = "O ADVERSÁRIO SE FOI";
        public const string DuelLadder = "Ranking {0} ({1})  ·  {2}  ·  temporada de {3}: {4}V {5}D {6}E";
        public const string DuelDesyncNote = "Os dois jogos discordaram do que aconteceu: este duelo não conta.";
        public const string DuelGoneNote = "A conexão caiu: este duelo não conta.";
        public const string DuelNetSummary = "{0} frames  ·  {1} voltas no tempo ({2} frames refeitos)";
        public const string DuelBack = "Enter: voltar ao pátio";
        public const string DuelLineWon = "{0} venceu {1} no círculo ({2} a {3}).";
        public const string DuelLineDraw = "{0} e {1} empataram no círculo.";

        public static string KamaeName(int k)
        {
            switch (k)
            {
                case 0: return "Jōdan";
                case 2: return "Gedan";
                case 3: return "Waki";
                default: return "Chūdan";
            }
        }

        public static string DuelEventName(DuelEvent e)
        {
            switch (e)
            {
                case DuelEvent.Hit: return "corte";
                case DuelEvent.Blocked: return "bloqueou";
                case DuelEvent.Parried: return "PERFEITO";
                case DuelEvent.Dodged: return "esquivou";
                case DuelEvent.Broken: return "POSTURA QUEBRADA";
                case DuelEvent.Finished: return "FINALIZADO";
                case DuelEvent.Staggered: return "aberto!";
                default: return "";
            }
        }
    }
}
