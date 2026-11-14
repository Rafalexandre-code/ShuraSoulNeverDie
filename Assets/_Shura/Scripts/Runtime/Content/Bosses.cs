using System.Collections.Generic;
using static Shura.EnemyDefs;

namespace Shura
{
    /// <summary>What makes each Lord of a biome its own fight (design doc §5.2).</summary>
    public enum BossKind
    {
        /// <summary>Kagutsuchi: every parry scatters embers; the arena shrinks with the fire.</summary>
        Fire,
        /// <summary>Jorōgumo: webs on the floor bind the dodge; she is only vulnerable once a Perfect Swap tears her web.</summary>
        Web,
        /// <summary>Ō-Kyūketsuki: every hit drinks your Flow; the third phase is a duel in the dark.</summary>
        Blood,
        /// <summary>Gashadokuro Colossal: break the bones of both hands and the skull opens.</summary>
        Colossus,
        /// <summary>Yuki-onna: blows freeze the ring halfway; the timing changes every phase.</summary>
        Frost,
        /// <summary>Ashura: three arms, three weapons, three rings at once; it uses your techniques.</summary>
        Ashura,
    }

    /// <summary>A Lord of a biome: three phases, each one a posture bar of its own.</summary>
    public sealed class BossDef
    {
        public string id;
        public string name;
        public string title;
        public string kanji;
        public BossKind kind;
        /// <summary>The biome it rules (index in Biomes.All); −1 for the Ashura.</summary>
        public int biome;
        public EnemyDef body;
        /// <summary>The moves of each phase (three).</summary>
        public MoveDef[][] phases;
        public string[] phaseLines;
        /// <summary>The Breaking Point: a master's answer to its signature that skips a whole phase.</summary>
        public string breakName;
        public string breakText;
        /// <summary>The exclusive item it leaves to whoever broke through.</summary>
        public string relicName;
        public float healthMult = 1f;
    }

    /// <summary>The five Lords of the biomes and the Ashura, King of the Shuradō.</summary>
    public static class BossDefs
    {
        public const int Phases = 3;
        /// <summary>The Ashura waits at the end of every 50 rooms (room 50, 100…).</summary>
        public const int AshuraEvery = 50;

        static EnemyDef Body(string id, string name, string art, string weapon, string color, float health, float posture, float scale,
                             SignatureAnswer answer, string technique, string lore, bool unarmed = false)
        {
            return new EnemyDef
            {
                id = id, displayName = name, art = art, weaponArt = weapon, artScale = scale, unarmed = unarmed,
                shape = Shape.Diamond, color = ColorUtil.Hex(color), bloodColor = ColorUtil.Hex("#8E0F1E"),
                maxHealth = health, maxPosture = posture, radius = 0.42f + 0.18f * (scale - 1f), mass = 3f,
                moveSpeed = 3.2f, preferredRange = 1.8f, blockChance = 0.25f, postureRegenPerSecond = 8f,
                idleMin = 34, idleMax = 64, superArmor = true, signatureAnswer = answer, techniqueId = technique, lore = lore,
            };
        }

        // ── Kagutsuchi, the Samurai in Flames (Cinzas) ───────────────────────────────

        static readonly EnemyDef KagutsuchiBody = Body("kagutsuchi", "Kagutsuchi", "SamuraiRed", "weapon_Katana", "#FF6A2A", 150f, 120f, 1.5f,
            SignatureAnswer.CounterThrust, RewardIds.IaiDraw, "O deus do fogo que queimou a própria mãe ao nascer. No Shuradō, queima quem o apara.");

        static MoveDef[] KagutsuchiPhase(int p)
        {
            int f = p * 3;
            var moves = new List<MoveDef>
            {
                Move("corte_flamejante", 0f, 2.3f, 4,
                     Arc("corte_flamejante_1", 22 - f, 4, 8, 16, 16f, 18f, 20f, 1.9f, 120f, 0.6f),
                     Arc("corte_flamejante_2", 16 - f, 4, 22, 10, 16f, 18f, 22f, 1.9f, 130f, 0.7f)),
                Move("pilar_de_fogo", 2.5f, 10f, 2, Blast("pilar_de_fogo", 30 - f, 30, 18f, 16f, 1.4f, 50 - f, BlastKind.Fire, 1 + p, 1.8f)),
                Signature("estocada_do_vulcao", 0f, 4.2f, 2,
                          Line("estocada_do_vulcao", 34 - f, 6, 30, 22, 26f, 0f, 0f, 3.4f, 0.8f, 2.2f, AttackKind.Thrust)),
            };
            if (p >= 1) moves.Add(Move("onda_de_brasas", 0f, 2.4f, 2, Sweep("onda_de_brasas", 30 - f, 6, 30, 20f, 2.6f)));
            if (p >= 2) moves.Add(Move("chuva_de_brasas", 2f, 12f, 2, Blast("chuva_de_brasas", 36, 30, 16f, 14f, 1.2f, 45, BlastKind.Fire, 5, 3.4f)));
            return moves.ToArray();
        }

        // ── Jorōgumo, the Spider Lady (Musgo) ────────────────────────────────────────

        static readonly EnemyDef JorogumoBody = Body("jorogumo", "Jorōgumo", "SorcererBlack", null, "#7B4BB8", 140f, 110f, 1.5f,
            SignatureAnswer.PerfectParry, RewardIds.EtherealBody, "Uma mulher bonita à beira da cachoeira. Os fios de seda já estavam nos seus tornozelos.", true);

        static MoveDef[] JorogumoPhase(int p)
        {
            int f = p * 3;
            var moves = new List<MoveDef>
            {
                Move("garras_de_seda", 0f, 2.1f, 4,
                     Arc("garras_de_seda_1", 18 - f, 3, 6, 12, 12f, 14f, 16f, 1.6f, 110f, 0.6f),
                     Arc("garras_de_seda_2", 14 - f, 3, 6, 8, 12f, 14f, 16f, 1.6f, 110f, 0.6f),
                     Arc("garras_de_seda_3", 14 - f, 3, 24, 8, 14f, 16f, 20f, 1.7f, 120f, 0.7f)),
                Move("fio_de_seda", 2.5f, 11f, 3, Fan("fio_de_seda", 26 - f, 26, 20, 10f, 12f, 16f, 11f, 3 + p, 36f, "fx_kunai")),
                Signature("abraco_da_aranha", 0f, 2.6f, 2,
                          Spirit(Arc("abraco_da_aranha", 32 - f, 5, 30, 24, 22f, 20f, 30f, 2.1f, 160f, 0.8f))),
            };
            if (p >= 1) moves.Add(Move("salto_da_aranha", 3f, 9f, 2, Teleporting(Arc("salto_da_aranha", 28 - f, 4, 26, 18, 18f, 18f, 22f, 1.8f, 120f, 0.4f))));
            return moves.ToArray();
        }

        // ── Ō-Kyūketsuki, the Great Vampire (Sangue) ─────────────────────────────────

        static readonly EnemyDef VampireBody = Body("o_kyuketsuki", "Ō-Kyūketsuki", "Vampire", "weapon_Sai", "#B3123A", 160f, 120f, 1.6f,
            SignatureAnswer.PerfectDodge, RewardIds.BloodThirst, "Bebeu o sangue de um exército inteiro e ainda tem sede. Cada golpe dele bebe um pouco do seu Fluxo.");

        static MoveDef[] VampirePhase(int p)
        {
            int f = p * 3;
            var moves = new List<MoveDef>
            {
                Move("garras_rubras", 0f, 2.2f, 4,
                     Arc("garras_rubras_1", 18 - f, 4, 6, 12, 13f, 15f, 18f, 1.7f, 120f, 0.6f),
                     Arc("garras_rubras_2", 14 - f, 4, 26, 8, 15f, 16f, 20f, 1.7f, 120f, 0.7f)),
                Move("revoada", 2.5f, 11f, 2, Fan("revoada", 26 - f, 28, 20, 9f, 10f, 14f, 10f, 5, 60f, "fx_fireball")),
                Move("passo_noturno", 2f, 8f, 2, Teleporting(Arc("passo_noturno", 26 - f, 4, 26, 16, 18f, 18f, 24f, 1.8f, 120f, 0.3f))),
                Signature("beijo_rubro", 0f, 2.6f, 2, Arc("beijo_rubro", 30 - f, 6, 32, 22, 26f, 0f, 0f, 1.6f, 70f, 1.6f, AttackKind.Grab)),
            };
            return moves.ToArray();
        }

        // ── Gashadokuro Colossal (Abismo) ────────────────────────────────────────────

        static readonly EnemyDef ColossusBody = Body("gashadokuro_colossal", "Gashadokuro Colossal", "SkeletonDemon", null, "#D8D2C2", 170f, 130f, 3f,
            SignatureAnswer.PerfectDodge, RewardIds.StubbornBones, "Feito dos ossos de todos que morreram de fome. Enquanto as mãos dele existirem, o crânio fica fechado.", true);

        /// <summary>The Colossus's hands: break both and the skull opens.</summary>
        public static readonly EnemyDef ColossusHand = new EnemyDef
        {
            id = "mao_colossal", displayName = "Mão do Colosso", art = "Skeleton", artScale = 1.5f, unarmed = true, minion = true,
            shape = Shape.Diamond, color = ColorUtil.Hex("#D8D2C2"), bloodColor = ColorUtil.Hex("#9A9284"),
            maxHealth = 55f, maxPosture = 50f, radius = 0.55f, mass = 2.5f, moveSpeed = 2.6f, preferredRange = 1.6f,
            blockChance = 0f, postureRegenPerSecond = 6f, idleMin = 40, idleMax = 80, signatureAnswer = SignatureAnswer.PerfectParry,
            lore = "Uma mão de ossos do tamanho de um homem.",
            moves = new[]
            {
                Move("tapa_de_ossos", 0f, 2.2f, 3, Arc("tapa_de_ossos", 24, 5, 30, 16, 14f, 18f, 24f, 1.9f, 110f, 0.5f)),
                Move("esmagar", 0f, 2.0f, 2, Sweep("esmagar", 32, 6, 34, 18f, 1.8f)),
            },
        };

        static MoveDef[] ColossusPhase(int p)
        {
            int f = p * 3;
            var moves = new List<MoveDef>
            {
                Move("punho_de_ossos", 0f, 3.2f, 3, Arc("punho_de_ossos", 30 - f, 6, 34, 20, 22f, 26f, 30f, 3.0f, 100f, 0.3f)),
                Move("ossos_do_chao", 2f, 12f, 3, Blast("ossos_do_chao", 34 - f, 32, 18f, 16f, 1.4f, 50, BlastKind.Spirit, 2 + p, 2.6f)),
                Move("varredura_colossal", 0f, 3.4f, 2, Sweep("varredura_colossal", 38 - f, 6, 36, 22f, 3.2f)),
                Signature("abraco_da_fome", 0f, 3.2f, 1, Arc("abraco_da_fome", 34 - f, 6, 36, 24, 30f, 0f, 0f, 2.8f, 80f, 1.0f, AttackKind.Grab)),
            };
            return moves.ToArray();
        }

        // ── Yuki-onna, the Snow Woman (Gelo) ─────────────────────────────────────────

        static readonly EnemyDef YukiBody = Body("yuki_onna", "Yuki-onna", "Spirit", "weapon_Lance2", "#BFE3FF", 140f, 110f, 1.6f,
            SignatureAnswer.PerfectParry, RewardIds.PhantomStep, "Aparece na nevasca e pede abrigo. O frio dela para o tempo: os anéis congelam no meio do caminho.");

        static MoveDef[] YukiPhase(int p)
        {
            var moves = new List<MoveDef>
            {
                Move("lamina_de_gelo", 0f, 2.4f, 4,
                     Arc("lamina_de_gelo_1", 22, 4, 8, 16, 14f, 16f, 20f, 2.0f, 120f, 0.6f),
                     Arc("lamina_de_gelo_2", 18, 4, 24, 12, 15f, 16f, 22f, 2.0f, 120f, 0.6f)),
                Move("estilhacos", 2.5f, 11f, 3, Fan("estilhacos", 26, 26, 20, 9f, 10f, 14f, 11f, 4 + p, 50f, "fx_kunai")),
                Signature("abraco_da_nevasca", 0f, 2.8f, 2,
                          Arc("abraco_da_nevasca_1", 30, 5, 6, 22, 16f, 18f, 26f, 2.2f, 150f, 0.6f),
                          Arc("abraco_da_nevasca_2", 22, 5, 30, 14, 18f, 20f, 30f, 2.2f, 150f, 0.6f)),
            };
            if (p >= 1) moves.Add(Move("nevasca", 0f, 2.6f, 2, Sweep("nevasca", 30, 6, 30, 18f, 2.4f)));
            return moves.ToArray();
        }

        // ── Ashura, King of the Shuradō ──────────────────────────────────────────────

        static readonly EnemyDef AshuraBody = Body("ashura", "Ashura", "DemonRed", "weapon_BigSword", "#D7263D", 200f, 150f, 2.2f,
            SignatureAnswer.PerfectParry, RewardIds.ExecutionerBlow, "O guerreiro que absorveu tantas almas que não consegue mais morrer. Três braços, três armas, e todas as suas técnicas.");

        /// <summary>The Ashura's other two arms: floating blades that strike with him.</summary>
        public static readonly EnemyDef AshuraArm = new EnemyDef
        {
            id = "braco_ashura", displayName = "Braço do Ashura", art = "Flam", weaponArt = "weapon_Lance", artScale = 1.2f, minion = true,
            shape = Shape.Diamond, color = ColorUtil.Hex("#FF5A3C"), bloodColor = ColorUtil.Hex("#5A0A12"),
            maxHealth = 60f, maxPosture = 50f, radius = 0.4f, mass = 1f, moveSpeed = 3.4f, preferredRange = 2.2f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 50, idleMax = 90, signatureAnswer = SignatureAnswer.PerfectParry,
            lore = "Um dos três braços do rei.",
            moves = new[]
            {
                Move("golpe_do_braco", 0f, 2.6f, 3, Arc("golpe_do_braco", 26, 4, 30, 18, 13f, 15f, 20f, 2.2f, 100f, 0.6f)),
                Move("estocada_do_braco", 0f, 3.2f, 2, Line("estocada_do_braco", 28, 5, 30, 20, 14f, 16f, 20f, 3.0f, 0.6f, 1.2f)),
            },
        };

        static MoveDef[] AshuraPhase(int p)
        {
            int f = p * 3;
            var moves = new List<MoveDef>
            {
                Move("tres_laminas", 0f, 2.6f, 4,
                     Arc("tres_laminas_1", 22 - f, 4, 6, 16, 18f, 20f, 22f, 2.2f, 130f, 0.6f),
                     Arc("tres_laminas_2", 16 - f, 4, 6, 10, 18f, 20f, 22f, 2.2f, 130f, 0.6f),
                     Arc("tres_laminas_3", 16 - f, 4, 26, 10, 20f, 22f, 26f, 2.3f, 140f, 0.8f)),
                Move("pilar_do_rei", 2f, 12f, 2, Blast("pilar_do_rei", 32 - f, 30, 20f, 18f, 1.5f, 48, BlastKind.Fire, 3 + p, 2.6f)),
                Move("varredura_do_rei", 0f, 2.8f, 2, Sweep("varredura_do_rei", 32 - f, 6, 30, 22f, 2.8f)),
                Signature("julgamento", 0f, 3.2f, 2,
                          Arc("julgamento_1", 30 - f, 5, 4, 22, 20f, 22f, 30f, 2.4f, 150f, 0.6f),
                          Spirit(Arc("julgamento_2", 20 - f, 5, 4, 14, 20f, 22f, 30f, 2.4f, 150f, 0.6f)),
                          Line("julgamento_3", 24 - f, 6, 34, 16, 26f, 0f, 0f, 3.6f, 0.8f, 2.0f, AttackKind.Thrust)),
            };
            return moves.ToArray();
        }

        public static readonly BossDef[] All =
        {
            new BossDef
            {
                id = "kagutsuchi", name = "Kagutsuchi", title = "o Samurai em Chamas", kanji = "火", kind = BossKind.Fire, biome = 0,
                body = KagutsuchiBody, phases = new[] { KagutsuchiPhase(0), KagutsuchiPhase(1), KagutsuchiPhase(2) },
                phaseLines = new[] { "O fogo não perdoa quem o apara.", "As chamas fecham o círculo.", "Arda comigo!" },
                breakName = "Pisar na Lâmina", breakText = "Esquive para dentro da Estocada do Vulcão no último instante e pise na lâmina dele.",
                relicName = "Coração de Kagutsuchi", healthMult = 0.5f,
            },
            new BossDef
            {
                id = "jorogumo", name = "Jorōgumo", title = "a Senhora Aranha", kanji = "蜘", kind = BossKind.Web, biome = 2,
                body = JorogumoBody, phases = new[] { JorogumoPhase(0), JorogumoPhase(1), JorogumoPhase(2) },
                phaseLines = new[] { "Fique. Os fios são macios.", "Você rasgou o meu vestido.", "Então vamos cair juntos na teia." },
                breakName = "Rasgar o Abraço", breakText = "Responda ao Abraço da Aranha com uma Troca Perfeita: a teia inteira se rompe.",
                relicName = "Fio de Jorōgumo", healthMult = 0.5f,
            },
            new BossDef
            {
                id = "o_kyuketsuki", name = "Ō-Kyūketsuki", title = "o Grande Vampiro", kanji = "血", kind = BossKind.Blood, biome = 3,
                body = VampireBody, phases = new[] { VampirePhase(0), VampirePhase(1), VampirePhase(2) },
                phaseLines = new[] { "Seu Fluxo tem gosto de primavera.", "Mais. Eu quero mais.", "Agora, no escuro, só o som." },
                breakName = "Fugir do Beijo", breakText = "Esquive perfeitamente do Beijo Rubro no último instante: ele perde o fio da presa.",
                relicName = "Cálice do Grande Vampiro", healthMult = 0.55f,
            },
            new BossDef
            {
                id = "gashadokuro_colossal", name = "Gashadokuro Colossal", title = "a Fome que Anda", kanji = "骨", kind = BossKind.Colossus, biome = 4,
                body = ColossusBody, phases = new[] { ColossusPhase(0), ColossusPhase(1), ColossusPhase(2) },
                phaseLines = new[] { "…", "Os ossos se juntam outra vez.", "A fome nunca termina." },
                breakName = "Escapar do Abraço", breakText = "Esquive perfeitamente do Abraço da Fome: o colosso cai sobre as próprias mãos.",
                relicName = "Osso do Colosso", healthMult = 0.5f,
            },
            new BossDef
            {
                id = "yuki_onna", name = "Yuki-onna", title = "a Mulher da Neve", kanji = "雪", kind = BossKind.Frost, biome = 5,
                body = YukiBody, phases = new[] { YukiPhase(0), YukiPhase(1), YukiPhase(2) },
                phaseLines = new[] { "Posso entrar? Está tão frio.", "O tempo também congela.", "Durma. Ninguém acorda na neve." },
                breakName = "Quebrar o Gelo", breakText = "Apare o Abraço da Nevasca no tempo do anel congelado: o gelo racha.",
                relicName = "Lágrima de Neve", healthMult = 0.5f,
            },
            new BossDef
            {
                id = "ashura", name = "Ashura", title = "Rei do Shuradō", kanji = "修", kind = BossKind.Ashura, biome = -1,
                body = AshuraBody, phases = new[] { AshuraPhase(0), AshuraPhase(1), AshuraPhase(2) },
                phaseLines = new[] { "Mais uma alma veio me alimentar.", "Três braços. Três armas. Três anéis.", "Lute contra o que você mesmo se tornou." },
                breakName = "O Julgamento Aparado", breakText = "Apare os três golpes do Julgamento: o rei se ajoelha por um instante.",
                relicName = "Coroa do Shuradō", healthMult = 0.6f,
            },
        };

        /// <summary>Every body and minion, for the enemy index.</summary>
        public static IEnumerable<EnemyDef> Defs()
        {
            foreach (BossDef b in All) yield return b.body;
            yield return ColossusHand;
            yield return AshuraArm;
        }

        public static BossDef Get(string enemyId)
        {
            if (enemyId == null) return null;
            foreach (BossDef b in All)
            {
                if (b.body.id == enemyId) return b;
            }
            return null;
        }

        public static BossDef Ashura => All[All.Length - 1];

        /// <summary>The Lord who rules the room at this depth (the last room of its biome), or the Ashura every 50 rooms.</summary>
        public static BossDef ForDepth(int depth)
        {
            if (!RoomGenerator.IsGuardianDepth(depth)) return null;
            if ((depth + 1) % AshuraEvery == 0) return Ashura;
            int biome = System.Array.IndexOf(Biomes.All, Biomes.ForDepth(depth));
            foreach (BossDef b in All)
            {
                if (b.biome == biome && b.biome >= 0) return b;
            }
            return null;
        }
    }
}
