using System.Collections.Generic;

namespace Shura
{
    /// <summary>As Artes da 1.0 (WP-05, E.2.10). A ordem é a da tabela do plano.</summary>
    public enum ArtId
    {
        Kiai = 0,
        Shuriken = 1,
        Smoke = 2,
        Bell = 3,
        Chain = 4,
        Rupture = 5,
        Soul = 6,
    }

    /// <summary>
    /// A Arte (技): um encaixe, uma tecla (R / RT), duas cargas, e cada finalização devolve uma.
    /// Funde as ferramentas, os papéis espirituais, o Kiai e a Transformação em alma numa coisa
    /// só (Hades: um botão especial, o jogador escolhe QUAL, não acumula todos). Com a flag
    /// LegacyToolsAndKeys volta o sistema antigo (R/T, papéis, V, B, G).
    /// </summary>
    public static class Arts
    {
        public const int MaxCharges = 2;

        static readonly string[] Ids = { "kiai", "shuriken", "fumaca", "sino", "corrente", "ruptura", "alma" };
        static readonly string[] Names = { "Kiai", "Shuriken", "Fumaça", "Sino", "Corrente", "Ruptura", "Alma" };
        static readonly string[] Kanji = { "気", "手", "煙", "鐘", "鎖", "破", "魂" };
        static readonly string[] Rules =
        {
            "Um grito que interrompe o golpe vermelho em preparo do inimigo mais perto — o parry do imparável.",
            "Um leque de 3 estrelas: interrompem saltos e investidas.",
            "Some na fumaça: as leituras da IA zeram e você reaparece 3 m atrás.",
            "O sino de bronze: os etéreos ficam sólidos por 5 s ao seu redor.",
            "A corrente com gancho puxa um inimigo até você.",
            "Detona os selos do Selo Interno quando você quiser.",
            "Vira o inimigo dono da técnica por 8 s: o corpo, a força e o jeito de lutar dele.",
        };
        static readonly string[] Origins =
        {
            "Inicial.", "Inicial.", "Dojo do Templo.", "Biblioteca do Templo.", "Dojo do Templo.",
            "Carta: vem com a técnica Selo Interno.", "Carta: quando uma técnica chega ao nível III.",
        };

        public static readonly ArtId[] All = { ArtId.Kiai, ArtId.Shuriken, ArtId.Smoke, ArtId.Bell, ArtId.Chain, ArtId.Rupture, ArtId.Soul };

        public static string Id(ArtId a) => Ids[(int)a];
        public static string Name(ArtId a) => Names[(int)a];
        public static string KanjiOf(ArtId a) => Kanji[(int)a];
        public static string Rule(ArtId a) => Rules[(int)a];
        public static string Origin(ArtId a) => Origins[(int)a];

        public static ArtId Parse(string id, ArtId fallback = ArtId.Kiai)
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                if (Ids[i] == id) return (ArtId)i;
            }
            return fallback;
        }

        /// <summary>Ruptura e Alma só vêm por carta, no meio da run: não se escolhem no Arsenal.</summary>
        public static bool FromCardOnly(ArtId a) => a == ArtId.Rupture || a == ArtId.Soul;

        /// <summary>Pode ser escolhida no Arsenal? Kiai e Shuriken sempre; o resto, pelo Templo.</summary>
        public static bool Unlocked(ArtId a, MetaProgress meta)
        {
            switch (a)
            {
                case ArtId.Kiai:
                case ArtId.Shuriken:
                    return true;
                case ArtId.Smoke:
                case ArtId.Chain:
                    return meta == null || Temple.IsBuilt(meta, Temple.Dojo);
                case ArtId.Bell:
                    return meta == null || Temple.IsBuilt(meta, Temple.Library);
                default:
                    return false;
            }
        }

        /// <summary>As Artes que o Arsenal oferece agora, na ordem da tabela.</summary>
        public static List<ArtId> Choosable(MetaProgress meta)
        {
            var list = new List<ArtId>();
            foreach (ArtId a in All)
            {
                if (!FromCardOnly(a) && Unlocked(a, meta)) list.Add(a);
            }
            return list;
        }
    }

    /// <summary>
    /// A carta que troca a Arte da run: Ruptura (com o Selo Interno), Alma (técnica no nível III,
    /// "tornar a alma deste inimigo a sua Arte"), ou outra Arte achada no caminho. Não vai para a
    /// lista de recompensas: só troca o encaixe e enche as cargas.
    /// </summary>
    public sealed class ArtCard : RewardDef
    {
        public const string Prefix = "arte_";

        public readonly ArtId Art;
        /// <summary>Para a Alma: o inimigo cuja alma vira a Arte.</summary>
        public readonly string SoulEnemy;

        public ArtCard(ArtId art, string soulEnemy = null)
        {
            Art = art;
            SoulEnemy = soulEnemy;
        }

        public override string Id => Prefix + Arts.Id(Art) + (SoulEnemy != null ? ":" + SoulEnemy : "");
        public override RewardKind Kind => RewardKind.Relic;
        public override string Name
        {
            get
            {
                if (Art == ArtId.Soul && SoulEnemy != null && EnemyDefs.TryGet(SoulEnemy, out EnemyDef d)) return "Arte: Alma do " + d.displayName;
                return "Arte: " + Arts.Name(Art);
            }
        }
        public override string Gain => Arts.Rule(Art) + " (R · 2 cargas; finalizar recarrega)";
        public override string Cost => "Substitui a sua Arte atual.";

        /// <summary>"arte_alma:onryo" → a carta; null se o id não é de Arte.</summary>
        public static ArtCard FromId(string id)
        {
            if (id == null || !id.StartsWith(Prefix)) return null;
            string body = id.Substring(Prefix.Length);
            int colon = body.IndexOf(':');
            string art = colon >= 0 ? body.Substring(0, colon) : body;
            string soul = colon >= 0 ? body.Substring(colon + 1) : null;
            return new ArtCard(Arts.Parse(art), soul);
        }
    }
}
