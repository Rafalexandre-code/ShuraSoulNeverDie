using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>One body the soul wears (design doc §7.5): the living disciple, a tomb, or a Sensei.</summary>
    [Serializable]
    public sealed class Disciple
    {
        public string name;
        public int age = 22;
        public int generation = 1;
        public List<string> traits = new List<string>();
        /// <summary>The predecessor's technique this disciple starts every run with ("" = none).</summary>
        public string technique = "";
        /// <summary>Journeys that went past depth 10 (each one ages the body 5 years).</summary>
        public int journeys;
        public int bestRoom;
        // ── On the tomb ──
        public bool retired;
        /// <summary>The weapon carried at the end (the tomb keeps it).</summary>
        public string weapon = "";
        public string cause = "";
        public string jisei = "";
        /// <summary>The run's Karma at the end (−: Shura, +: Bushidō).</summary>
        public int karma;

        public void Sanitize()
        {
            if (string.IsNullOrEmpty(name)) name = "Sem Nome";
            age = Mathf.Clamp(age, 16, 99);
            generation = Mathf.Max(1, generation);
            if (traits == null) traits = new List<string>();
            traits.RemoveAll(t => Lineage.Trait(t) == null);
            if (traits.Count > 2) traits.RemoveRange(2, traits.Count - 2);
            if (technique == null) technique = "";
            if (weapon == null) weapon = "";
            if (cause == null) cause = "";
            if (jisei == null) jisei = "";
            journeys = Mathf.Max(0, journeys);
            bestRoom = Mathf.Max(0, bestRoom);
        }
    }

    public sealed class TraitDef
    {
        public string id;
        public string name;
        public string text;
        public Action<PlayerModifiers> apply;
    }

    public enum AgeBand
    {
        Young,
        Prime,
        Elder,
        Retired,
    }

    /// <summary>
    /// The lineage: disciples, their traits, their age, the Senseis who retire and the poem
    /// written on each tomb. A journey that goes past depth 10 brings the disciple home five
    /// years older; one that ends earlier ends the disciple too, and an heir is chosen.
    /// </summary>
    public static class Lineage
    {
        public const int SurviveDepth = 10;
        public const int YearsPerJourney = 5;
        public const int RetireAge = 70;
        public const int MaxSenseiBonus = 5;

        public static readonly TraitDef[] Traits =
        {
            new TraitDef { id = "impaciente", name = "Impaciente", text = "O Fluxo sobe 30% mais rápido, mas cai inteiro com qualquer dano.",
                apply = m => { m.flowGainMult *= 1.3f; m.extraFlowLossOnHurt += 9; } },
            new TraitDef { id = "hattori", name = "Herdeiro do Clã Hattori", text = "Começa cada jornada com 2 peças do conjunto Hattori.",
                apply = m => { } },
            new TraitDef { id = "mao_pesada", name = "Mão Pesada", text = "+20 de postura máxima e +10% de dano na postura, −6% de velocidade.",
                apply = m => { m.maxPostureBonus += 20f; m.postureMult *= 1.1f; m.moveSpeedMult *= 0.94f; } },
            new TraitDef { id = "olhar_noturno", name = "Olhar Noturno", text = "Enxerga no escuro: a noite, o Abismo e as lanternas quebradas pesam pela metade.",
                apply = m => m.nightSight = true },
            new TraitDef { id = "pes_leves", name = "Pés Leves", text = "Esquivas 15% mais longas, −10 de vida máxima.",
                apply = m => { m.dodgeDistanceMult *= 1.15f; m.maxHealthBonus -= 10f; } },
            new TraitDef { id = "sangue_frio", name = "Sangue Frio", text = "O Shura sobe 25% mais devagar.",
                apply = m => m.shuraGainMult *= 0.75f },
            new TraitDef { id = "coracao_ardente", name = "Coração Ardente", text = "+10% de dano, +10% de dano recebido.",
                apply = m => { m.damageMult *= 1.1f; m.damageTakenMult *= 1.1f; } },
            new TraitDef { id = "paciente", name = "Paciente", text = "Bloquear gasta 15% menos postura.",
                apply = m => m.blockWearMult *= 0.85f },
        };

        static readonly string[] Names =
        {
            "Haru", "Ren", "Sora", "Kaito", "Aoi", "Hana", "Yuki", "Daichi", "Kenji", "Riku", "Mei", "Akane",
            "Tsubasa", "Hikaru", "Nao", "Shin", "Rin", "Kaede", "Jun", "Takumi", "Emi", "Hayato", "Masa", "Chiyo",
        };

        public static TraitDef Trait(string id)
        {
            if (id == null) return null;
            foreach (TraitDef t in Traits)
            {
                if (t.id == id) return t;
            }
            return null;
        }

        public static AgeBand Band(int age) => age >= RetireAge ? AgeBand.Retired : age >= 50 ? AgeBand.Elder : age >= 30 ? AgeBand.Prime : AgeBand.Young;

        public static string BandName(int age)
        {
            switch (Band(age))
            {
                case AgeBand.Young: return "Jovem";
                case AgeBand.Prime: return "Maduro";
                case AgeBand.Elder: return "Veterano";
                default: return "Sensei";
            }
        }

        public static string BandText(int age)
        {
            switch (Band(age))
            {
                case AgeBand.Young: return "+5% de velocidade, Fôlego 8% mais rápido; janela do parry −1 frame.";
                case AgeBand.Prime: return "Corpo e mente em equilíbrio.";
                case AgeBand.Elder: return "−6% de velocidade, Fôlego mais lento; janela do parry +2 frames e anéis mais cedo.";
                default: return "Aposentado: ensina no Dojo.";
            }
        }

        /// <summary>The body's age, the traits, and every Sensei who retired before.</summary>
        public static void Apply(MetaProgress meta, PlayerModifiers m)
        {
            Disciple d = meta.disciple;
            if (d != null)
            {
                switch (Band(d.age))
                {
                    case AgeBand.Young:
                        m.moveSpeedMult *= 1.05f;
                        m.dodgeRechargeMult *= 0.92f;
                        m.perfectParryFrameBonus -= 1;
                        break;
                    case AgeBand.Elder:
                    case AgeBand.Retired:
                        m.moveSpeedMult *= 0.94f;
                        m.dodgeRechargeMult *= 1.1f;
                        m.perfectParryFrameBonus += 2;
                        m.ringWarningMult *= 1.25f;
                        break;
                }
                if (d.traits != null)
                {
                    foreach (string t in d.traits) Trait(t)?.apply(m);
                }
            }
            int senseis = Senseis(meta);
            m.maxHealthBonus += 6f * senseis;
            m.damageMult *= 1f + 0.02f * senseis;
        }

        /// <summary>Retired disciples teaching in the Dojo (their bonus is capped).</summary>
        public static int Senseis(MetaProgress meta)
        {
            int n = 0;
            if (meta.cemetery != null)
            {
                foreach (Disciple d in meta.cemetery)
                {
                    if (d.retired) n++;
                }
            }
            return Mathf.Min(MaxSenseiBonus, n);
        }

        public static bool HasTrait(MetaProgress meta, string trait) => meta?.disciple?.traits != null && meta.disciple.traits.Contains(trait);

        /// <summary>The very first body: a disciple in balance (30), with no traits.</summary>
        public static Disciple First() => new Disciple { name = "Haru", age = 30, generation = 1 };

        /// <summary>Three candidates for the next generation (seeded, so a reload offers the same three).</summary>
        public static List<Disciple> Candidates(ulong seed, int generation, string technique)
        {
            var rng = new SeededRandom(seed ^ ((ulong)generation * 0x9E3779B97F4A7C15UL));
            var names = new List<string>(Names);
            var list = new List<Disciple>();
            for (int i = 0; i < 3; i++)
            {
                int k = rng.Range(0, names.Count);
                var d = new Disciple
                {
                    name = names[k],
                    age = 18 + rng.Range(0, 23),
                    generation = generation,
                    technique = technique ?? "",
                };
                names.RemoveAt(k);
                var pool = new List<TraitDef>(Traits);
                int traits = rng.NextFloat() < 0.45f ? 2 : 1;
                for (int t = 0; t < traits; t++)
                {
                    int p = rng.Range(0, pool.Count);
                    d.traits.Add(pool[p].id);
                    pool.RemoveAt(p);
                }
                list.Add(d);
            }
            return list;
        }

        // ── The death poem (jisei, design doc §8.4) ──────────────────────────────────

        public struct Facts
        {
            public string biome;
            public TimeOfDay time;
            public Weather weather;
            /// <summary>Who dealt the last blow (null = the road itself).</summary>
            public string killer;
            public string weapon;
            public int depth;
            public int karma;
            public ulong seed;
        }

        static readonly string[][] Openings =
        {
            new[] { "Na luz da manhã", "Sol claro no orvalho" },
            new[] { "Ao sol da tarde", "Cigarras no calor" },
            new[] { "No crepúsculo rubro", "O sol se deita" },
            new[] { "Sob a lua fria", "Noite sem estrelas" },
            new[] { "Na névoa da aurora", "O céu que clareia" },
        };

        static string WeatherOpening(Weather w, SeededRandom rng)
        {
            switch (w)
            {
                case Weather.Rain: return rng.NextFloat() < 0.5f ? "Sob a chuva fina" : "A chuva não para";
                case Weather.Wind: return "O vento carrega";
                case Weather.Fog: return "Na névoa espessa";
                case Weather.Storm: return "Trovão sobre a serra";
                case Weather.Snow: return "Na neve calada";
                default: return null;
            }
        }

        /// <summary>A three-line haiku from what happened (always the same for the same facts).</summary>
        public static string Jisei(Facts f)
        {
            var rng = new SeededRandom(f.seed ^ ((ulong)(f.depth + 1) * 0xD1B54A32D192ED03UL));
            string[] times = Openings[Mathf.Clamp((int)f.time, 0, Openings.Length - 1)];
            string first = WeatherOpening(f.weather, rng);
            if (first == null || rng.NextFloat() < 0.35f) first = times[rng.Range(0, times.Length)];

            string killer = string.IsNullOrEmpty(f.killer) ? null : f.killer;
            string weapon = string.IsNullOrEmpty(f.weapon) ? "lâmina" : f.weapon.ToLowerInvariant();
            string[] middles = killer != null
                ? new[] { "o golpe do " + killer, "a lâmina do " + killer, killer + " me encontra", "cai minha " + weapon }
                : new[] { "a estrada me cobra", "cai minha " + weapon, "o caminho se fecha" };
            string second = middles[rng.Range(0, middles.Length)];

            var lasts = new List<string> { "calou meu silêncio", "pétalas no chão", "volto para a roda" };
            if (!string.IsNullOrEmpty(f.biome)) lasts.Add(f.biome.ToLowerInvariant() + " me guarda");
            if (f.karma > 0) lasts.Add("parto sem rancor");
            if (f.karma < 0) lasts.Add("o Shura me espera");
            if (f.depth >= 25) lasts.Add("tão perto do trono");
            string third = lasts[rng.Range(0, lasts.Count)];
            return Capital(first) + "\n" + second + "\n" + third;
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>"Ren · 3ª geração · 34 anos".</summary>
        public static string Title(Disciple d)
        {
            var sb = new StringBuilder();
            sb.Append(d.name).Append(" · ").Append(d.generation).Append("ª geração · ").Append(d.age).Append(" anos");
            return sb.ToString();
        }

        public static string TraitNames(Disciple d)
        {
            if (d.traits == null || d.traits.Count == 0) return "Sem traços";
            var names = new List<string>();
            foreach (string t in d.traits)
            {
                TraitDef def = Trait(t);
                if (def != null) names.Add(def.name);
            }
            return string.Join(" · ", names);
        }
    }
}
