using UnityEngine;

namespace Shura
{
    /// <summary>O que um Elo faz além do golpe de troca, quando o golpe sai (PlayerController.StartElo).</summary>
    public enum EloBurst
    {
        None,
        /// <summary>Atordoa quem está no alcance do Elo.</summary>
        Stun,
        /// <summary>Empurra para longe quem está ao redor.</summary>
        Gust,
        /// <summary>Uma onda de choque no chão, que quebra postura.</summary>
        Quake,
        /// <summary>O sino acalma: devolve postura e esfria o Shura.</summary>
        Bell,
        /// <summary>Quem está no alcance começa a sangrar.</summary>
        Bleed,
    }

    /// <summary>Um dos 10 Elos: o comportamento da arma que ENTRA.</summary>
    public sealed class EloRule
    {
        public string weapon;
        public string name;
        /// <summary>A regra numa frase (cartões, Biblioteca, dica).</summary>
        public string rule;
        public float damageMult = 1f;
        public float postureMult = 1f;
        public float rangeMult = 1f;
        public int extraHits;
        /// <summary>Arco mínimo (graus) do corte do Elo; 0 = o do golpe de troca.</summary>
        public float minArc;
        public float extraPull;
        /// <summary>Preparação fixa (quadros); -1 = a do golpe de troca menos 2.</summary>
        public int windup = -1;
        public EloBurst burst;
    }

    /// <summary>
    /// Os Elos da Troca Perfeita (WP-04, E.2.1): trocar logo depois do último golpe do combo
    /// solta o golpe de troca da arma que entra, com um comportamento próprio dessa arma —
    /// 10 comportamentos, um por arma, aprendíveis. Os 45 nomes de par (EloNames) ficam só
    /// como texto de sabor. Com NamedLinks45 ligada volta o Elo antigo (só o nome do par).
    /// </summary>
    public static class EloRules
    {
        public static readonly EloRule[] All =
        {
            new EloRule { weapon = "katana", name = "Fio Duplo", rule = "O Elo da katana corta duas vezes.", extraHits = 1 },
            new EloRule { weapon = "montante", name = "Peso da Montanha", rule = "O Elo do montante quebra o dobro de postura.", postureMult = 2f },
            new EloRule { weapon = "gemeas", name = "Enxame", rule = "O Elo das lâminas gêmeas acerta três vezes e faz sangrar.", extraHits = 2, damageMult = 0.7f, burst = EloBurst.Bleed },
            new EloRule { weapon = "naginata", name = "Varrida do Horizonte", rule = "O Elo da naginata varre ao redor, mais longe.", rangeMult = 1.35f, minArc = 300f },
            new EloRule { weapon = "kusarigama", name = "Laço da Corrente", rule = "O Elo da kusarigama alcança longe e puxa quem prende.", rangeMult = 1.5f, extraPull = 2.5f },
            new EloRule { weapon = "manoplas", name = "Punho Atordoante", rule = "O Elo das manoplas atordoa quem está no alcance.", postureMult = 1.3f, burst = EloBurst.Stun },
            new EloRule { weapon = "tessen", name = "Vento do Leque", rule = "O Elo do tessen corta em círculo e afasta todos.", minArc = 360f, burst = EloBurst.Gust },
            new EloRule { weapon = "kanabo", name = "Terremoto do Oni", rule = "O Elo do kanabō abre uma onda de choque no chão.", burst = EloBurst.Quake },
            new EloRule { weapon = "shakujo", name = "Sino do Templo", rule = "O Elo do shakujō devolve sua postura e acalma o Shura.", burst = EloBurst.Bell },
            new EloRule { weapon = "wakizashi", name = "Golpe Furtivo", rule = "O Elo da wakizashi sai na hora e fere mais.", windup = 1, damageMult = 1.3f },
        };

        /// <summary>O Elo da arma que entra (uma arma forjada usa o da lâmina).</summary>
        public static EloRule For(string incomingWeapon)
        {
            string id = WeaponDefs.Get(incomingWeapon).BaseId;
            foreach (EloRule r in All)
            {
                if (r.weapon == id) return r;
            }
            return All[0];
        }

        /// <summary>
        /// O golpe do Elo: o golpe de troca da arma que entra (mais rápido e um pouco mais leve,
        /// como sempre foi) com a regra da arma por cima. Puro: não mexe no golpe de origem.
        /// </summary>
        public static PlayerAttackDef Build(PlayerAttackDef swapStrike, EloRule rule, string name)
        {
            var shape = swapStrike.shape;
            shape.range *= rule != null ? rule.rangeMult : 1f;
            if (rule != null && rule.minArc > 0f)
            {
                if (rule.minArc >= 360f) shape = HitShape.Circle(shape.range);
                else if (shape.type == HitShapeType.Arc) shape.arcDegrees = Mathf.Max(shape.arcDegrees, rule.minArc);
                else shape = HitShape.Arc(shape.range, rule.minArc);
            }
            int windup = Mathf.Max(3, swapStrike.windup - 2);
            if (rule != null && rule.windup >= 0) windup = rule.windup;
            return new PlayerAttackDef
            {
                name = name, style = swapStrike.style, windup = windup,
                active = swapStrike.active, recovery = swapStrike.recovery,
                damage = swapStrike.damage * 0.85f * (rule != null ? rule.damageMult : 1f),
                posture = swapStrike.posture * (rule != null ? rule.postureMult : 1f),
                shape = shape, lunge = swapStrike.lunge, fromAngle = swapStrike.fromAngle, toAngle = swapStrike.toAngle,
                extend = swapStrike.extend, hits = swapStrike.hits + (rule != null ? rule.extraHits : 0),
                pull = swapStrike.pull + (rule != null ? rule.extraPull : 0f), dash = swapStrike.dash,
            };
        }
    }
}
