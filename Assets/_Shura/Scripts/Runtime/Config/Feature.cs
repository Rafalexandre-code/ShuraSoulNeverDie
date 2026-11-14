using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Feature flags do redesenho "Ponta-Cabeça" (WP-00). Cada corte do plano entra primeiro
    /// atrás de uma flag: o padrão é sempre o design NOVO (a maioria = false, ou seja, o sistema
    /// antigo desligado); ligar a flag devolve o comportamento da v0.15.0. O código antigo só
    /// sai no WP-20, depois do playtest humano.
    ///
    /// Ordem de leitura: padrões deste arquivo → StreamingAssets/features.json (só as chaves
    /// presentes) → opção de linha de comando "-shuraFeatures legacy|novo" (testes e playtest).
    /// </summary>
    public static class Feature
    {
        /// <summary>Os valores, num objeto que o JsonUtility sabe ler e escrever.</summary>
        [Serializable]
        public sealed class Flags
        {
            // ── WP-03: núcleo do combate ──────────────────────────────────────────────
            /// <summary>WP-03 · Fôlego (Kokū): custo de ataque/esquiva, exaustão, Brilho do Respiro. Padrão: false (cortado).</summary>
            public bool Stamina = false;
            /// <summary>WP-03 · Ferimentos localizados no jogador, bandagem (H) e boneco do HUD. Padrão: false.</summary>
            public bool LocalWounds = false;
            /// <summary>WP-03 · Selos de Ki na base (tecla G); vira a técnica "Selo Interno". Padrão: false.</summary>
            public bool KiSealsBase = false;
            /// <summary>WP-03 · Troca de kamae em combate (1–4 / direcional); vira guarda fixa por arma. Padrão: false.</summary>
            public bool KamaeSwitch = false;
            /// <summary>WP-03 · Modificadores de janela de parry (kamae, idade, Altar, relíquias, tsubas, itens, exaustão). Padrão: false (janela constante).</summary>
            public bool ParryWindowModifiers = false;
            /// <summary>WP-03 · O Fluxo acelera a simulação (ritmo 0,82 → 1,03). Padrão: false (ritmo fixo 1,0).</summary>
            public bool FlowTimeScale = false;
            /// <summary>WP-03 · Contra-investida e Pisão como duas respostas à estocada (fundidas no Mikiri). Padrão: false.</summary>
            public bool SplitCounterAndStomp = false;

            // ── WP-04: anéis e Troca Perfeita ─────────────────────────────────────────
            /// <summary>WP-04 · Anel verde maldito (bloquear amaldiçoa). Padrão: false.</summary>
            public bool CursedRing = false;
            /// <summary>WP-04 · Anel "duplo" como tipo próprio (vira dois anéis em sequência). Padrão: false.</summary>
            public bool DoubleRingType = false;
            /// <summary>WP-04 · 45 Elos com comportamento próprio (vira 10, um por arma que entra). Padrão: false.</summary>
            public bool NamedLinks45 = false;

            // ── WP-05: teclas e Arte ──────────────────────────────────────────────────
            /// <summary>WP-05 · Ferramentas antigas (R/T, papéis espirituais, petardo, óleo) e teclas G/V/B/H/T/1–4. Padrão: false.</summary>
            public bool LegacyToolsAndKeys = false;

            // ── WP-07: Shura opcional ─────────────────────────────────────────────────
            /// <summary>WP-07 · Medidor de Shura sempre ativo (sem o Caminho do Asura). Padrão: false.</summary>
            public bool ShuraMeterBase = false;

            // ── WP-08 a WP-10: builds ─────────────────────────────────────────────────
            /// <summary>WP-08 · Kanabō e Wakizashi + Kunai como armas próprias (viram Aspectos). Padrão: false.</summary>
            public bool ExtraWeapons = false;
            /// <summary>WP-08 · Forja de empunhaduras (100 combinações); vira Arsenal com Aspectos. Padrão: false.</summary>
            public bool ForgeHandles = false;
            /// <summary>WP-10 · Loot de itens com atributos, mochila e tela de Equipamento. Padrão: false.</summary>
            public bool StatLoot = false;
            /// <summary>WP-10 · Clãs de itens (bônus de 2 e 4 peças). Padrão: false.</summary>
            public bool ItemClans = false;
            /// <summary>WP-10 · Cartas de bênção de atributo puro. Padrão: false.</summary>
            public bool Blessings = false;

            // ── WP-11: meta ───────────────────────────────────────────────────────────
            /// <summary>WP-11 · Idade muda velocidade, fôlego e janela de parry. Padrão: false (idade só narrativa).</summary>
            public bool AgeStats = false;
            /// <summary>WP-11 · Senseis dão +vida e +dano permanentes. Padrão: false.</summary>
            public bool SenseiStats = false;
            /// <summary>WP-11 · Templo com 8 construções (vira 6 lugares). Padrão: false.</summary>
            public bool LegacyTemple = false;
            /// <summary>WP-11 · Aço Celeste e Fragmentos de Alma como moedas próprias. Padrão: false.</summary>
            public bool LegacyCurrencies = false;
            /// <summary>WP-11 · Segunda Troca do Altar (levar 3 armas). Padrão: true (em observação no Playtest 2).</summary>
            public bool ThirdWeapon = true;

            // ── WP-12: estrutura da run ───────────────────────────────────────────────
            /// <summary>WP-12 · Run infinita como modo principal (vira Jornada de 4 atos + Roda Eterna). Padrão: false.</summary>
            public bool InfiniteRunDefault = false;
            /// <summary>WP-12 · Relógio de 24 h em tempo real (vira hora fixa por ato). Padrão: false.</summary>
            public bool RealtimeDayClock = false;
            /// <summary>WP-12 · Clima Tempestade. Padrão: false.</summary>
            public bool StormWeather = false;
            /// <summary>WP-12 · Modos Torre, Mestre e Peregrinação no menu. Padrão: false.</summary>
            public bool LegacyModes = false;

            // ── WP-13 e WP-14: inimigos e Algozes ─────────────────────────────────────
            /// <summary>WP-13 · Inimigos fora da lista da 1.0. Padrão: false.</summary>
            public bool ExtraEnemies = false;
            /// <summary>WP-13 · Leituras da IA além das 5 originais (FLANCO, FERRAMENTA, CURA, TROCA). Padrão: false.</summary>
            public bool AiReadsExtended = false;
            /// <summary>WP-14 · Hierarquia de rivais (Capitães, Generais, promoção, guarda-costas). Padrão: false (Algozes).</summary>
            public bool RivalHierarchy = false;

            // ── WP-19: online ─────────────────────────────────────────────────────────
            /// <summary>WP-19 · Cliente pelo navegador no fluxo principal. Padrão: false (congelado).</summary>
            public bool BrowserClient = false;
            /// <summary>WP-19 · Festa de 5 a 8 jogadores (experimental). Padrão: false (oficial até 4).</summary>
            public bool PartyMode8 = false;
            /// <summary>WP-19 · Pacto de Sangue no lobby. Padrão: false.</summary>
            public bool BloodPact = false;
            /// <summary>WP-19 · Presença assíncrona sem servidor (mensagens, manchas, espíritos, rivais emprestados). Padrão: false.</summary>
            public bool AsyncPresence = false;
            /// <summary>WP-19 · Ranking PvP (Elo, temporadas, títulos). Padrão: false (duelo entre amigos).</summary>
            public bool PvpRanked = false;
        }

        static Flags current = new Flags();
        static readonly Flags Defaults = new Flags();

        /// <summary>De onde vieram os valores atuais (para o log e o painel).</summary>
        public static string Source { get; private set; } = "padrões do código";

        public static bool Stamina => current.Stamina;
        public static bool LocalWounds => current.LocalWounds;
        public static bool KiSealsBase => current.KiSealsBase;
        public static bool KamaeSwitch => current.KamaeSwitch;
        public static bool ParryWindowModifiers => current.ParryWindowModifiers;
        public static bool FlowTimeScale => current.FlowTimeScale;
        public static bool SplitCounterAndStomp => current.SplitCounterAndStomp;
        public static bool CursedRing => current.CursedRing;
        public static bool DoubleRingType => current.DoubleRingType;
        public static bool NamedLinks45 => current.NamedLinks45;
        public static bool LegacyToolsAndKeys => current.LegacyToolsAndKeys;
        public static bool ShuraMeterBase => current.ShuraMeterBase;
        public static bool ExtraWeapons => current.ExtraWeapons;
        public static bool ForgeHandles => current.ForgeHandles;
        public static bool StatLoot => current.StatLoot;
        public static bool ItemClans => current.ItemClans;
        public static bool Blessings => current.Blessings;
        public static bool AgeStats => current.AgeStats;
        public static bool SenseiStats => current.SenseiStats;
        public static bool LegacyTemple => current.LegacyTemple;
        public static bool LegacyCurrencies => current.LegacyCurrencies;
        public static bool ThirdWeapon => current.ThirdWeapon;
        public static bool InfiniteRunDefault => current.InfiniteRunDefault;
        public static bool RealtimeDayClock => current.RealtimeDayClock;
        public static bool StormWeather => current.StormWeather;
        public static bool LegacyModes => current.LegacyModes;
        public static bool ExtraEnemies => current.ExtraEnemies;
        public static bool AiReadsExtended => current.AiReadsExtended;
        public static bool RivalHierarchy => current.RivalHierarchy;
        public static bool BrowserClient => current.BrowserClient;
        public static bool PartyMode8 => current.PartyMode8;
        public static bool BloodPact => current.BloodPact;
        public static bool AsyncPresence => current.AsyncPresence;
        public static bool PvpRanked => current.PvpRanked;

        static FieldInfo[] fields;
        static FieldInfo[] Fields => fields ?? (fields = typeof(Flags).GetFields(BindingFlags.Public | BindingFlags.Instance));

        /// <summary>Os nomes de todas as flags, na ordem do arquivo (para o painel do WP-02).</summary>
        public static IEnumerable<string> Names
        {
            get
            {
                foreach (FieldInfo f in Fields) yield return f.Name;
            }
        }

        public static int Count => Fields.Length;

        public static bool Get(string name)
        {
            foreach (FieldInfo f in Fields)
            {
                if (f.Name == name) return (bool)f.GetValue(current);
            }
            throw new ArgumentException("Flag desconhecida: " + name);
        }

        public static bool DefaultOf(string name)
        {
            foreach (FieldInfo f in Fields)
            {
                if (f.Name == name) return (bool)f.GetValue(Defaults);
            }
            throw new ArgumentException("Flag desconhecida: " + name);
        }

        public static void Set(string name, bool value)
        {
            foreach (FieldInfo f in Fields)
            {
                if (f.Name != name) continue;
                f.SetValue(current, value);
                return;
            }
            throw new ArgumentException("Flag desconhecida: " + name);
        }

        /// <summary>Liga (true) ou desliga (false) todas: true = o jogo como na v0.15.0.</summary>
        public static void SetAll(bool value)
        {
            foreach (FieldInfo f in Fields) f.SetValue(current, value);
            Source = value ? "todas ligadas (v0.15.0)" : "todas desligadas";
        }

        /// <summary>Volta aos padrões do código (o design novo).</summary>
        public static void Reset()
        {
            current = new Flags();
            Source = "padrões do código";
        }

        /// <summary>Lê um JSON parcial por cima dos valores atuais (só as chaves presentes mudam).</summary>
        public static void Apply(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            JsonUtility.FromJsonOverwrite(json, current);
        }

        public static string ToJson() => JsonUtility.ToJson(current, true);

        public static string FilePath => Path.Combine(Application.streamingAssetsPath, "features.json");

        /// <summary>No boot, antes do Game: padrões → features.json → linha de comando.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            Reset();
            try
            {
                if (File.Exists(FilePath))
                {
                    Apply(File.ReadAllText(FilePath));
                    Source = "features.json";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shura][Flags] features.json ilegível, ficam os padrões: " + e.Message);
            }
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "-shuraFeatures") continue;
                string mode = args[i + 1].ToLowerInvariant();
                if (mode == "legacy" || mode == "antigo" || mode == "v015") SetAll(true);
                else if (mode == "novo" || mode == "new" || mode == "padrao") Reset();
            }
            Debug.Log("[Shura][Flags] " + Count + " flags (" + Source + ")");
        }

        /// <summary>Salva os valores atuais em features.json (painel de desenvolvedor).</summary>
        public static void Save()
        {
            File.WriteAllText(FilePath, ToJson());
            Source = "features.json";
        }
    }
}
