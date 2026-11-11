namespace Shura
{
    /// <summary>
    /// The Shura meter (修羅, design doc §3.11): the game's theme as a mechanic. It rises with
    /// killing, with pain at low health and with forbidden things, and never cools by itself.
    /// </summary>
    public static class ShuraRules
    {
        public const float Max = 100f;

        /// <summary>0 Serene, 1 Wrath, 2 Thirst, 3 Frenzy, 4 the fall into the Shuradō.</summary>
        public static int Level(float shura)
        {
            if (shura >= Max) return 4;
            if (shura >= 90f) return 3;
            if (shura >= 60f) return 2;
            if (shura >= 30f) return 1;
            return 0;
        }

        static readonly string[] Names = { "SERENO", "IRA", "SEDE", "FRENESI", "QUEDA NO SHURADŌ" };
        static readonly string[] Effects =
        {
            "Nenhum efeito.",
            "+15% de dano, cura −20%.",
            "Roubo de vida 5%; os anéis parecem 15% menores.",
            "Sua postura não quebra; o anel de tempo some para você.",
            "10 s como Asura; depois o Asura Interior surge.",
        };

        public static string LevelName(int level) => Names[level < 0 ? 0 : level > 4 ? 4 : level];
        public static string LevelEffect(int level) => Effects[level < 0 ? 0 : level > 4 ? 4 : level];
    }

    /// <summary>The Links of the Troca Perfeita 2.0: one named transition blow for each pair of weapons.</summary>
    public static class EloNames
    {
        static readonly string[][] Pairs =
        {
            new[] { "katana", "montante", "Lua Partida" },
            new[] { "katana", "gemeas", "Vento de Três Lâminas" },
            new[] { "katana", "naginata", "Ponte de Aço" },
            new[] { "katana", "kusarigama", "Serpente Desembainhada" },
            new[] { "katana", "manoplas", "Punho e Gume" },
            new[] { "montante", "gemeas", "Avalanche de Agulhas" },
            new[] { "montante", "naginata", "Queda da Montanha" },
            new[] { "montante", "kusarigama", "Âncora Ceifadora" },
            new[] { "montante", "manoplas", "Martelo do Céu" },
            new[] { "gemeas", "naginata", "Garça e Vespa" },
            new[] { "gemeas", "kusarigama", "Teia de Lâminas" },
            new[] { "gemeas", "manoplas", "Chuva de Golpes" },
            new[] { "naginata", "kusarigama", "Foice do Horizonte" },
            new[] { "naginata", "manoplas", "Lança Trovejante" },
            new[] { "kusarigama", "manoplas", "Corrente de Ferro" },
            new[] { "tessen", "katana", "Pétala Cortante" },
            new[] { "tessen", "kanabo", "Leque do Oni" },
            new[] { "tessen", "shakujo", "Vento do Templo" },
            new[] { "tessen", "wakizashi", "Sombra Dobrada" },
            new[] { "kanabo", "shakujo", "Badalada de Ferro" },
            new[] { "kanabo", "wakizashi", "Oni e Raposa" },
            new[] { "shakujo", "wakizashi", "Sutra Oculto" },
            new[] { "tessen", "montante", "Vendaval de Aço" },
            new[] { "tessen", "gemeas", "Leque de Espinhos" },
            new[] { "tessen", "naginata", "Garça ao Vento" },
            new[] { "tessen", "kusarigama", "Corrente de Brisa" },
            new[] { "tessen", "manoplas", "Palma do Tufão" },
            new[] { "kanabo", "katana", "Trovão e Fio" },
            new[] { "kanabo", "montante", "Dois Rochedos" },
            new[] { "kanabo", "gemeas", "Pedra e Vespas" },
            new[] { "kanabo", "naginata", "Marreta do Horizonte" },
            new[] { "kanabo", "kusarigama", "Mangual do Oni" },
            new[] { "kanabo", "manoplas", "Fúria dos Ogros" },
            new[] { "shakujo", "katana", "Prece Cortante" },
            new[] { "shakujo", "montante", "Sino da Montanha" },
            new[] { "shakujo", "gemeas", "Rosário de Lâminas" },
            new[] { "shakujo", "naginata", "Peregrino Armado" },
            new[] { "shakujo", "kusarigama", "Corrente de Contas" },
            new[] { "shakujo", "manoplas", "Punho do Monge" },
            new[] { "wakizashi", "katana", "Daishō Desperto" },
            new[] { "wakizashi", "montante", "Sombra do Gigante" },
            new[] { "wakizashi", "gemeas", "Três Presas" },
            new[] { "wakizashi", "naginata", "Lança e Punhal" },
            new[] { "wakizashi", "kusarigama", "Laço da Noite" },
            new[] { "wakizashi", "manoplas", "Punhal no Punho" },
        };

        public static string For(string a, string b)
        {
            // A forged weapon links by its blade.
            a = WeaponDefs.Get(a).BaseId;
            b = WeaponDefs.Get(b).BaseId;
            foreach (string[] p in Pairs)
            {
                if ((p[0] == a && p[1] == b) || (p[0] == b && p[1] == a)) return p[2];
            }
            return "Elo " + WeaponDefs.Get(a).name + " · " + WeaponDefs.Get(b).name;
        }

        /// <summary>The 45 Links between the ten weapons all have their own name.</summary>
        public static int NamedPairs => Pairs.Length;
    }
}
