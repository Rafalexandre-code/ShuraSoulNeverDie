using System.Collections.Generic;

namespace Shura
{
    /// <summary>
    /// A permanent rule added at the Gate of the Shuradō, every 25 rooms (design doc §6.6).
    /// From Kalpa V on, each gate adds two.
    /// </summary>
    public enum KalpaRule
    {
        /// <summary>Enemies chain a second move without pausing (they "swap" mid-combo).</summary>
        EnemySwap,
        /// <summary>Enemies build Flow when they hit you and enter Mushin at three.</summary>
        EnemyFlow,
        /// <summary>When an enemy dies, the nearest one takes its strength.</summary>
        EnemyAbsorb,
        /// <summary>Guardians carry affixes from any depth, and one more.</summary>
        EarlyAffixes,
        /// <summary>Windups 12% shorter.</summary>
        SwiftBlades,
        /// <summary>Guardian rooms hold two Guardians.</summary>
        TwinGuardians,
    }

    public static class KalpaRules
    {
        static readonly string[] Names =
        {
            "Lâminas Trocadas", "Fluxo Inimigo", "Herança dos Mortos", "Marcas Antigas", "Golpes Ligeiros", "Guardiões Gêmeos",
        };

        static readonly string[] Texts =
        {
            "Inimigos emendam um segundo golpe sem pausa, como se trocassem de arma no meio do combo.",
            "Inimigos ganham Fluxo ao te acertar e entram em Mushin no terceiro acerto.",
            "Quando um inimigo morre, o mais próximo herda a força dele.",
            "Guardiões carregam afixos em qualquer profundidade, e um a mais.",
            "Todos os preparos de golpe ficam 12% mais curtos.",
            "Salas de Guardião trazem dois Guardiões.",
        };

        static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        public static string Name(KalpaRule r) => Names[(int)r];
        public static string Text(KalpaRule r) => Texts[(int)r];
        public static string Numeral(int kalpa) => kalpa >= 1 && kalpa <= Numerals.Length ? Numerals[kalpa - 1] : kalpa.ToString();

        /// <summary>The rules a gate offers: up to three the run does not have yet (seeded).</summary>
        public static List<KalpaRule> Offer(RunState run, SeededRandom rng)
        {
            var pool = new List<KalpaRule>();
            foreach (KalpaRule r in System.Enum.GetValues(typeof(KalpaRule)))
            {
                if (!run.HasRule(r)) pool.Add(r);
            }
            var offer = new List<KalpaRule>();
            while (offer.Count < 3 && pool.Count > 0)
            {
                int k = rng.Range(0, pool.Count);
                offer.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return offer;
        }

        /// <summary>Descends: the chosen rule (and, from Kalpa V, a second one drawn at random).</summary>
        public static void Descend(RunState run, KalpaRule chosen, SeededRandom rng)
        {
            run.Kalpa++;
            if (!run.HasRule(chosen)) run.KalpaRules.Add(chosen);
            if (run.Kalpa < 5) return;
            List<KalpaRule> extra = Offer(run, rng);
            if (extra.Count > 0) run.KalpaRules.Add(extra[0]);
        }
    }

    /// <summary>Curses from cursed treasures, poisoned tea and the Muramasa (they last a few rooms).</summary>
    public enum RunCurse
    {
        None,
        /// <summary>No healing at all.</summary>
        NoHeal,
        /// <summary>+30% damage taken.</summary>
        Fragile,
        /// <summary>One dodge charge fewer.</summary>
        Clumsy,
        /// <summary>The Shura rises on its own.</summary>
        Restless,
    }

    public static class RunCurses
    {
        static readonly string[] Names = { "", "Sem cura", "Frágil", "Desajeitado", "Inquieto" };
        static readonly string[] Texts =
        {
            "",
            "Nenhuma cura funciona.",
            "Você recebe 30% mais dano.",
            "Uma carga de esquiva a menos.",
            "O Shura sobe sozinho.",
        };

        public static string Name(RunCurse c) => Names[(int)c];
        public static string Text(RunCurse c) => Texts[(int)c];

        /// <summary>A maldição pelo nome (o HUD do convidado online só recebe o nome).</summary>
        public static RunCurse FromName(string name)
        {
            for (int i = 1; i < Names.Length; i++)
            {
                if (Names[i] == name) return (RunCurse)i;
            }
            return RunCurse.None;
        }
        public static RunCurse Random(SeededRandom rng) => (RunCurse)rng.Range(1, 5);
    }
}
