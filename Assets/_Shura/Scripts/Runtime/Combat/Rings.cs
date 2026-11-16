namespace Shura
{
    /// <summary>O som de um tipo de anel: provisório até o WP-17, mas já distinto por tipo.</summary>
    public struct RingSound
    {
        /// <summary>O id que o WP-17 vai trocar por um som gravado ("anel_ciano", ...).</summary>
        public string id;
        public Sfx sfx;
        public float volume;
        public float pitch;
    }

    /// <summary>
    /// A linguagem de ameaça (WP-04, E.2.4): uma tríade, cada resposta do núcleo com o seu
    /// anel, mais a finta. Ciano liso = aparar; vermelho com losango = esquivar; roxo
    /// pontilhado = Troca Perfeita; tracejado = esperar o golpe real. O branco partido só
    /// existe com 2+ jogadores. Verde (maldito) e duplo são as flags CursedRing/DoubleRingType.
    /// Fonte única: o anel sobre o inimigo, o brilho da arma, a trilha de ritmo, o som e a
    /// telemetria leem daqui.
    /// </summary>
    public static class Rings
    {
        /// <summary>Os quatro anéis do jogo solo, na ordem da tabela de E.2.4.</summary>
        public static readonly RingKind[] Core = { RingKind.Parry, RingKind.Danger, RingKind.Spirit, RingKind.Feint };

        /// <summary>Que anel um golpe mostra. adapted = a leitura adaptativa segurou o golpe.</summary>
        public static RingKind Of(AttackDef a, bool adapted)
        {
            if (a == null) return RingKind.Parry;
            if (a.feint) return RingKind.Feint;
            if (a.groupStrike) return RingKind.Group;
            if (a.kind == AttackKind.Spirit) return RingKind.Spirit;
            if (a.IsDangerous) return RingKind.Danger;
            if (a.cursed && Feature.CursedRing) return RingKind.Cursed;
            // Sem o tipo duplo, o golpe segurado é só um anel ciano que fecha mais tarde
            // (a trilha de ritmo já mostra a batida no lugar certo).
            if (adapted && Feature.DoubleRingType) return RingKind.Double;
            return RingKind.Parry;
        }

        /// <summary>Bloquear este golpe amaldiçoa? Só com o anel verde de volta (flag CursedRing).</summary>
        public static bool Curses(AttackDef a) => a != null && a.cursed && Feature.CursedRing;

        /// <summary>A resposta certa, como texto curto (cartões, telemetria, testes).</summary>
        public static string Answer(RingKind k)
        {
            switch (k)
            {
                case RingKind.Danger: return "esquivar";
                case RingKind.Spirit: return "troca";
                case RingKind.Feint: return "esperar";
                case RingKind.Group: return "aparar juntos";
                default: return "aparar";
            }
        }

        /// <summary>O nome do anel na telemetria (ring_resolved).</summary>
        public static string TelemetryName(RingKind k)
        {
            switch (k)
            {
                case RingKind.Danger: return "vermelho";
                case RingKind.Spirit: return "roxo";
                case RingKind.Feint: return "finta";
                case RingKind.Group: return "grupo";
                case RingKind.Cursed: return "maldito";
                case RingKind.Double: return "duplo";
                default: return "ciano";
            }
        }

        /// <summary>
        /// O som que toca quando o anel fecha. Provisório (WP-17 grava os de verdade): "tim"
        /// metálico, batida grave, sino de templo, batida abafada.
        /// </summary>
        public static RingSound Sound(RingKind k)
        {
            switch (k)
            {
                case RingKind.Danger: return new RingSound { id = "anel_vermelho", sfx = Sfx.Danger, volume = 0.6f, pitch = 0.55f };
                case RingKind.Spirit: return new RingSound { id = "anel_roxo", sfx = Sfx.Chime, volume = 0.6f, pitch = 0.62f };
                case RingKind.Feint: return new RingSound { id = "anel_finta", sfx = Sfx.SheathClick, volume = 0.35f, pitch = 0.7f };
                case RingKind.Group: return new RingSound { id = "anel_grupo", sfx = Sfx.Clash, volume = 0.5f, pitch = 0.8f };
                case RingKind.Cursed: return new RingSound { id = "anel_maldito", sfx = Sfx.Glint, volume = 0.45f, pitch = 0.9f };
                case RingKind.Double: return new RingSound { id = "anel_duplo", sfx = Sfx.Glint, volume = 0.45f, pitch = 1.1f };
                default: return new RingSound { id = "anel_ciano", sfx = Sfx.Glint, volume = 0.45f, pitch = 1f };
            }
        }
    }
}
