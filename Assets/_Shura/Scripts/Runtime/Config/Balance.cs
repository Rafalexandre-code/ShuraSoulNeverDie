using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shura
{
    /// <summary>Escala dos inimigos por profundidade e de elite (WP-02: antes eram constantes em DifficultyRules).</summary>
    [Serializable]
    public sealed class ScalingTuning
    {
        /// <summary>Vida dos inimigos: +fração por profundidade efetiva (0,08 = +8% por sala).</summary>
        public float healthPerDepth = 0.08f;
        /// <summary>Postura dos inimigos: +fração por profundidade efetiva.</summary>
        public float posturePerDepth = 0.06f;
        /// <summary>Dano dos inimigos: +fração por profundidade efetiva.</summary>
        public float damagePerDepth = 0.05f;
        /// <summary>Depois do 1º Kalpa (prof. 25), cada sala conta só esta fração (0,6 = números sobem 40% mais devagar).</summary>
        public float depthAfterKalpa = 0.6f;
        /// <summary>Espera entre golpes: −fração por sala (0,03 = 3% menos espera por sala)...</summary>
        public float idleDropPerDepth = 0.03f;
        /// <summary>...até no mínimo esta fração da espera original.</summary>
        public float idleMinMult = 0.55f;
        /// <summary>Intervalo mínimo entre ataques de inimigos diferentes, em quadros (piso).</summary>
        public int attackGapMinFrames = 8;
        /// <summary>Elite: multiplicador de vida.</summary>
        public float eliteHealthMult = 2.2f;
        /// <summary>Elite: multiplicador de postura.</summary>
        public float elitePostureMult = 1.6f;
        /// <summary>Elite: multiplicador de dano.</summary>
        public float eliteDamageMult = 1.15f;
        /// <summary>Elite: multiplicador do tamanho do corpo.</summary>
        public float eliteSizeMult = 1.2f;
        /// <summary>Ascensão do jogador por sala vencida: +vida (pontos).</summary>
        public float ascensionHealth = 4f;
        /// <summary>Ascensão do jogador por sala vencida: +dano (fração).</summary>
        public float ascensionDamage = 0.03f;
        /// <summary>Ascensão do jogador por sala vencida: +postura (pontos).</summary>
        public float ascensionPosture = 2f;
    }

    /// <summary>Ajuste fino de um inimigo (multiplicadores sobre a definição em EnemyDefs; 1 = sem mudança).</summary>
    [Serializable]
    public sealed class EnemyOverride
    {
        public string id = "";
        public float health = 1f;
        public float posture = 1f;
        public float damage = 1f;
    }

    /// <summary>
    /// Os números de balanceamento e de sensação em dados (WP-02): StreamingAssets/Balance.json,
    /// lido por cima dos padrões do código ao iniciar o jogo e editável no painel de afinação
    /// (F1, só em build de desenvolvimento) sem recompilar. As seções são as do GameConfig
    /// (jogador, inimigos, Fluxo, sensação) mais a escala por profundidade e os ajustes por inimigo.
    /// Unidades: quadros = 1/60 s; frações (0,12 = 12%); segundos e metros onde o nome diz.
    /// </summary>
    public static class Balance
    {
        [Serializable]
        sealed class File
        {
            public string leia = "Shura · Balance.json. Quadros = 1/60 s; frações (0.12 = 12%). Comentários de cada campo em Config/GameConfig.cs e Config/Balance.cs.";
            public PlayerTuning player;
            public EnemyTuning enemies;
            public FlowTuning flow;
            public FeelTuning feel;
            public ScalingTuning scaling;
            public List<EnemyOverride> enemyOverrides = new List<EnemyOverride>();
        }

        public static ScalingTuning Scaling { get; private set; } = new ScalingTuning();
        public static readonly List<EnemyOverride> EnemyOverrides = new List<EnemyOverride>();
        public static string Source { get; private set; } = "padrões do código";

        public static string FilePath => Path.Combine(Application.streamingAssetsPath, "Balance.json");

        /// <summary>Lê o Balance.json por cima do config (só as chaves presentes mudam).</summary>
        public static void Load(GameConfig config, string path = null)
        {
            path = path ?? FilePath;
            try
            {
                if (!System.IO.File.Exists(path)) return;
                Apply(config, System.IO.File.ReadAllText(path));
                Source = Path.GetFileName(path);
                Debug.Log("[Shura][Balance] lido de " + path + " (janela de parry " + config.player.perfectParryFrames + " f, anel " + config.feel.timingApproachFrames + " f)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shura][Balance] Balance.json ilegível, ficam os padrões: " + e.Message);
            }
        }

        public static void Apply(GameConfig config, string json)
        {
            var f = new File { player = config.player, enemies = config.enemies, flow = config.flow, feel = config.feel, scaling = Scaling };
            JsonUtility.FromJsonOverwrite(json, f);
            // Os objetos do config são os mesmos (sobrescritos no lugar); só a escala e a lista mudam de dono.
            Scaling = f.scaling ?? new ScalingTuning();
            EnemyOverrides.Clear();
            if (f.enemyOverrides != null) EnemyOverrides.AddRange(f.enemyOverrides);
        }

        public static string ToJson(GameConfig config) =>
            JsonUtility.ToJson(new File { player = config.player, enemies = config.enemies, flow = config.flow, feel = config.feel, scaling = Scaling, enemyOverrides = new List<EnemyOverride>(EnemyOverrides) }, true);

        public static void Save(GameConfig config, string path = null)
        {
            path = path ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, ToJson(config));
            Source = Path.GetFileName(path);
        }

        /// <summary>Volta a escala e os ajustes por inimigo aos padrões (o config se recria à parte).</summary>
        public static void ResetStatics()
        {
            Scaling = new ScalingTuning();
            EnemyOverrides.Clear();
            Source = "padrões do código";
        }

        /// <summary>Aplica o ajuste de um inimigo, se houver, sobre a escala dele.</summary>
        public static void ApplyOverride(string enemyId, ref EnemyScaling s)
        {
            foreach (EnemyOverride o in EnemyOverrides)
            {
                if (o.id != enemyId) continue;
                s.health *= o.health;
                s.posture *= o.posture;
                s.damage *= o.damage;
                return;
            }
        }
    }
}
