namespace Shura
{
    /// <summary>All player-facing text in one place, ready for translation.</summary>
    public static class Strings
    {
        public const string Title = "SHURA";
        public const string Tagline = "No reino Shura, quem vence herda a técnica do vencido.";
        public const string PressAnyButton = "Pressione qualquer botão";
        public const string BuildLabel = "Protótipo M1";

        public const string Play = "Jogar";
        public const string Settings = "Configurações";
        public const string Credits = "Créditos";
        public const string Quit = "Sair";
        public const string Resume = "Continuar";
        public const string RestartRun = "Recomeçar run";
        public const string MainMenu = "Menu principal";
        public const string Back = "Voltar";
        public const string NewRun = "Nova run";

        public const string Paused = "PAUSA";
        public const string SettingsTitle = "CONFIGURAÇÕES";
        public const string CreditsTitle = "CRÉDITOS";
        public const string ControlsTitle = "Controles atuais";

        public const string MasterVolume = "Volume geral";
        public const string MusicVolume = "Música";
        public const string SfxVolume = "Efeitos";
        public const string ScreenShake = "Tremida de tela";
        public const string ReduceFlashes = "Reduzir flashes";
        public const string Fullscreen = "Tela cheia";
        public const string ResetControls = "Restaurar controles padrão";
        public const string On = "Ligado";
        public const string Off = "Desligado";
        public const string ControlsReset = "Controles restaurados";

        public const string RewardTitle = "ESCOLHA SUA RECOMPENSA";
        public const string RewardSubtitle = "Toda escolha tem um custo.";
        public const string Technique = "TÉCNICA";
        public const string Relic = "RELÍQUIA";
        public const string Gain = "GANHO";
        public const string Cost = "CUSTO";
        public const string AbsorbedFrom = "Absorvida de: {0}";
        public const string RewardControls = "Esquerda / Direita para escolher  ·  Enter / A para confirmar";
        public const string Obtained = "Obtido: {0}";

        public const string DeathTitle = "A MORTE ENSINA";
        public const string DeathSubtitle = "Leia o golpe. Responda no tempo. Tente de novo.";
        public const string VictoryTitle = "FIM DO PROTÓTIPO";
        public const string VictorySubtitle = "Você atravessou o Portão Shura.";

        public const string StatRooms = "Salas vencidas";
        public const string StatParries = "Parries perfeitos";
        public const string StatDodges = "Esquivas perfeitas";
        public const string StatCounters = "Contra-investidas";
        public const string StatFinishers = "Finalizações";
        public const string StatTechniques = "Técnicas absorvidas";
        public const string StatTime = "Tempo";
        public const string StatSeed = "Semente";

        public const string HudHealth = "VIDA";
        public const string HudPosture = "POSTURA";
        public const string HudDodge = "ESQUIVA";
        public const string HudFlow = "FLUXO";
        public const string HudRoom = "Sala {0}/{1}  ·  {2}";
        public const string HudWave = "Onda {0}/{1}";
        public const string HudOwned = "TÉCNICAS E RELÍQUIAS";
        public const string HudPending = "Técnica a escolher: {0}";
        public const string TechniqueTag = "[T] ";
        public const string RelicTag = "[R] ";

        public const string PopupPerfect = "PERFEITO";
        public const string PopupBlocked = "bloqueou";
        public const string PopupCounter = "CONTRA-INVESTIDA";
        public const string PopupBreak = "POSTURA QUEBRADA";
        public const string PopupFinisher = "FINALIZADO";
        public const string PopupDodge = "ESQUIVA PERFEITA";
        public const string PopupMastered = "DOMINADO";
        public const string PopupGuardBroken = "GUARDA QUEBRADA";

        public const string ToastTechnique = "Técnica absorvida: {0}";
        public const string ToastFlowUp = "FLUXO {0}";
        public const string ToastRoomClear = "Sala vencida  ·  +{0} de vida";
        public const string ToastInvincible = "Invencível: {0}";

        public const string CreditsBody =
            "SHURA  ·  Protótipo M1\n\n" +
            "Direção criativa: [seu nome aqui]\n" +
            "Design, programação, efeitos e áudio gerados por código: Claude (Anthropic)\n" +
            "Feito com Unity 6\n\n" +
            "Inspirações: Sekiro, Nine Sols, Dead Cells, Hades, Hollow Knight, Katana ZERO";

        public static string HintText(HintId hint, bool gamepad)
        {
            switch (hint)
            {
                case HintId.Controls:
                    return gamepad
                        ? "Analógico esq. mover  ·  Analógico dir. mirar  ·  RB atacar  ·  LB aparar/bloquear  ·  A esquivar"
                        : "WASD mover  ·  Mouse mirar  ·  Botão esq. atacar  ·  Botão dir. aparar/bloquear  ·  Espaço esquivar";
                case HintId.Parry:
                    return "Aperte APARAR no instante do golpe: o parry perfeito enche a postura do inimigo. " +
                           "Segurar só bloqueia e desgasta a SUA postura.";
                case HintId.DangerThrust:
                    return "PERIGO (losango vermelho)  ·  Estocada: não dá para aparar. " +
                           "Esquive PARA DENTRO dela no último instante para uma CONTRA-INVESTIDA.";
                case HintId.DangerSweep:
                    return "PERIGO (losango vermelho)  ·  Varredura: não dá para aparar. " +
                           "Saia do círculo ou esquive ATRAVÉS dela no último instante.";
                case HintId.Finisher:
                    return "Postura quebrada! Ataque perto do inimigo para FINALIZAR.";
                case HintId.Mastery:
                    return "SELO DOURADO: você dominou o golpe-assinatura. Finalize este inimigo para ABSORVER a técnica dele.";
                default:
                    return string.Empty;
            }
        }
    }
}
