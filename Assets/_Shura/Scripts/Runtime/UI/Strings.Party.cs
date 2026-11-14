namespace Shura
{
    /// <summary>Phase 5, part 4: playing together (§9) — roles, rescue, the Blood Pact, presence.</summary>
    public static partial class Strings
    {
        // Threat and roles.
        public static string RoleName(PartyRole r)
        {
            switch (r)
            {
                case PartyRole.Vanguard: return "VANGUARDA";
                case PartyRole.Guardian: return "GUARDIÃO";
                case PartyRole.Hunter: return "CAÇADOR";
                case PartyRole.Exorcist: return "EXORCISTA";
                default: return "";
            }
        }

        // Falling and being brought back.
        public const string PopupRescueParry = "+0,4 s de resgate";
        public const string PopupNoOffering = "nenhuma técnica para oferecer";
        public const string ToastOffering = "{0} ofereceu {1}: {2} se levanta!";
        public const string PopupSpiritKiai = "KIAI ESPIRITUAL";
        public const string PopupSpiritHint = "espírito: Aparar + Esquivar = Kiai";

        // The Blood Pact.
        public const string PopupFrenzy = "FRENESI";
        public const string ToastFrenzy = "{0} entra em frenesi pela queda de {1}!";
        public const string LobbyPact = "Pacto de Sangue";
        public const string PactNobody = "ninguém";
        public const string PactOffered = "oferecido a {0}";
        public const string PactSealed = "SELADO com {0}";
        public const string PactNone = "nenhum pacto";

        // The group's Oath.
        public const string LobbyOath = "Juramento do grupo";
        public const string GroupOathNone = "nenhum";
        public const string GroupOathVotes = "{0} ({1} votos)";
        public const string ToastGroupOath = "O grupo jurou: {0}";
        public const string LobbyPartyLine = "Pactos: {0}   ·   Juramento do grupo: {1}";

        // Presence.
        public const string InkItem = "Mensagem de tinta";
        public const string PopupInkApproved = "mensagem aprovada";
        public const string ToastInkApproved = "{1} aprovou {0} das suas mensagens de tinta.";
        public const string PopupGhost = "{0} caiu aqui — “{1}”";
        public const string SpiritName = "Espírito de {0}";
        public const string ToastFriendSpirit = "O espírito de {0} veio lutar ao seu lado ({1}).";
        public const string SpiritRelentless = "implacável";
        public const string SpiritPatient = "paciente";
        public const string SpiritBalanced = "equilibrado";
        public const string NewAshuraName = "{0}, o Novo Ashura";
        public const string ToastNewAshura = "{0} virou o Ashura nesta semana e espera no trono.";
        public const string PopupMirrorFriend = "reflete {0}";
        public const string RivalLent = "Rival de {0}";
    }
}
