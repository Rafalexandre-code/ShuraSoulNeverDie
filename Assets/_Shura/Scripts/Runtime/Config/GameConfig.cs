using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Every tunable number of the game lives here. Without an asset the code defaults are
    /// used; to tweak values in the Inspector, create one via Create > Shura > Game Config and
    /// drag it onto the Game component. Times are in 60 Hz simulation frames unless named
    /// "Seconds".
    /// </summary>
    [CreateAssetMenu(menuName = "Shura/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public PlayerTuning player = new PlayerTuning();
        public EnemyTuning enemies = new EnemyTuning();
        public FlowTuning flow = new FlowTuning();
        public FeelTuning feel = new FeelTuning();
        public ArenaTuning arena = new ArenaTuning();
        public Palette palette = new Palette();
    }

    [Serializable]
    public sealed class PlayerTuning
    {
        [Header("Vida e postura")]
        public float maxHealth = 100f;
        public float maxPosture = 100f;
        public float postureRegenPerSecond = 22f;
        public int postureRegenDelay = 45;
        public int guardBreakFrames = 60;
        [Tooltip("Fração do dano de postura do golpe aplicada quando o jogador é acertado em cheio.")]
        public float hitPostureFraction = 0.35f;

        [Header("Movimento")]
        [Tooltip("Velocidade antes do ritmo global (ver FeelTuning.baseTempo).")]
        public float moveSpeed = 6.4f;
        public float accelTime = 0.10f;
        public float decelTime = 0.07f;
        public float guardMoveMultiplier = 0.45f;
        public float radius = 0.38f;

        [Header("Ataque (combo)")]
        public int comboLength = 3;
        public int attackWindup = 5;
        public int attackActive = 4;
        public int attackRecovery = 13;
        public int finalHitExtraRecovery = 8;
        public int attackCancelDelay = 4;
        public int comboChainDelay = 3;
        public float attackDamage = 10f;
        public float attackPosture = 7f;
        public float attackRange = 1.45f;
        public float attackArcDegrees = 110f;
        public float attackLunge = 0.35f;

        [Header("Parry e bloqueio")]
        public int perfectParryFrames = 9;
        [Tooltip("Apertar de novo antes disso corta a janela perfeita pela metade (anti-spam).")]
        public int parrySpamWindow = 20;
        public float parryArcDegrees = 120f;
        public float blockPostureMultiplier = 1f;
        public float aimAssistDegrees = 40f;
        public float aimAssistRange = 4f;

        [Header("Esquiva")]
        public int dodgeFrames = 11;
        public int dodgeIFrameStart = 2;
        public int dodgeIFrameEnd = 9;
        public float dodgeDistance = 3.0f;
        public int dodgeCharges = 2;
        public float dodgeRechargeSeconds = 0.9f;
        public float counterThrustPosture = 45f;
        [Tooltip("Quão alinhada com o atacante a esquiva precisa estar para virar contra-investida (cosseno).")]
        public float counterThrustDotThreshold = 0.5f;

        [Header("Finalizador")]
        public int finisherFrames = 30;
        public int finisherImpactFrame = 9;
        public float finisherRange = 1.9f;

        [Header("Golpe defletido pelo inimigo")]
        [Tooltip("Quando o inimigo bloqueia seu golpe, sua espada quica e o combo é interrompido.")]
        public int deflectedFrames = 18;
        public int deflectCancelFrames = 7;

        [Header("Dano recebido")]
        public int hurtStunFrames = 16;
        public int hurtInvulnFrames = 40;
        public float hurtKnockback = 4f;

        [Header("Buffer de entrada")]
        public int actionBufferFrames = 8;
        public int parryBufferFrames = 3;

        [Header("Entre salas")]
        public float healOnRoomClear = 20f;
    }

    [Serializable]
    public sealed class EnemyTuning
    {
        public int attackGapFrames = 20;
        public int postureRegenDelay = 90;
        public int brokenFrames = 150;
        public float brokenRecoverPostureRatio = 0.5f;
        public int spawnFrames = 40;
        [Tooltip("Dano de postura que o inimigo recebe quando bloqueia um golpe seu.")]
        public float blockPostureMultiplier = 0.5f;
        public int riposteAfterBlocks = 3;
        [Tooltip("Parry perfeito interrompe o golpe: a arma quica e o inimigo fica aberto.")]
        public int recoilFrames = 14;
        [Tooltip("Recuo maior quando o golpe aparado era o último do combo (janela de contra-ataque).")]
        public int recoilFinalFrames = 28;
        public int guardVisualFrames = 14;
        public float teleportBehindDistance = 1.7f;
        public int counterStaggerFrames = 40;
        public float hitKnockback = 1.6f;
        public float turnDegreesPerSecond = 540f;
        public float waitingRangeBonus = 1.2f;
        public int firstWaveDelay = 40;
        public int waveDelayFrames = 60;
        public float minSpawnDistance = 4.5f;
        public int dyingFrames = 24;
    }

    [Serializable]
    public sealed class FlowTuning
    {
        public int[] levelThresholds = { 3, 7, 12 };
        public float maxPoints = 14f;
        public float perfectParryPoints = 1f;
        public float perfectDodgePoints = 1f;
        public float counterPoints = 2f;
        public float finisherPoints = 2f;
        [Header("Efeitos por nível")]
        [Tooltip("Dano extra por nível de Fluxo.")]
        public float damagePerLevel = 0.12f;
        [Tooltip("Impacto extra (tremida, faíscas) por nível de Fluxo.")]
        public float impactPerLevel = 0.3f;
        public float level1PostureBonus = 0.15f;
        public float level2DodgeRechargeSpeed = 1.3f;
        public float level3FinisherHeal = 10f;
    }

    [Serializable]
    public sealed class FeelTuning
    {
        [Header("Hit-stop (frames)")]
        public int hitstopHit = 4;
        public int hitstopBlock = 3;
        public int hitstopPerfectParry = 7;
        public int hitstopCounter = 10;
        public int hitstopPostureBreak = 9;
        public int hitstopFinisher = 12;
        public int hitstopHurt = 5;

        [Header("Tremida (trauma 0-1)")]
        public float shakeHit = 0.18f;
        public float shakeBlock = 0.12f;
        public float shakePerfectParry = 0.3f;
        public float shakeCounter = 0.42f;
        public float shakePostureBreak = 0.45f;
        public float shakeFinisher = 0.55f;
        public float shakeHurt = 0.45f;
        public float shakeMaxOffset = 0.35f;
        public float shakeMaxAngle = 2.5f;
        public float shakeDecayPerSecond = 1.8f;
        public float shakeFrequency = 22f;

        [Header("Câmera")]
        public float cameraOrthoSize = 6.2f;
        public float cameraFollowSharpness = 6f;
        public float cameraFollowWeight = 0.4f;
        public float cameraLookAhead = 1.3f;
        public float zoomPunchPerfectParry = 0.25f;
        public float zoomPunchFinisher = 0.45f;

        [Header("Indicador de tempo")]
        [Tooltip("Quantos frames o anel leva para fechar. Constante para todo golpe, para o ritmo ser aprendível.")]
        public int timingApproachFrames = 36;
        [Tooltip("O anel fecha esta quantidade de frames ANTES do golpe acertar (meio da janela perfeita).")]
        public int timingCueLeadFrames = 4;

        [Header("Ritmo global")]
        [Tooltip("Velocidade do jogo no Fluxo 0. Abaixo de 1 = duelo mais pausado e legível.")]
        public float baseTempo = 0.82f;
        [Tooltip("Cada nível de Fluxo acelera o jogo por este valor.")]
        public float tempoPerFlowLevel = 0.07f;
        public float flowZoomPerLevel = 0.12f;

        [Header("Choque de lâminas e câmera lenta")]
        public int hitstopDeflected = 5;
        public float shakeDeflected = 0.2f;
        public float slowMoLastKillSeconds = 0.9f;
        public float slowMoLastKillScale = 0.25f;
        public float slowMoBreakSeconds = 0.25f;
        public float slowMoBreakScale = 0.5f;

        [Header("Ambiente")]
        public float embersPerSecond = 6f;
    }

    [Serializable]
    public sealed class ArenaTuning
    {
        public float width = 19f;
        public float height = 10.5f;
        public int pixelsPerUnit = 16;
        public float wallThickness = 0.45f;
    }

    [Serializable]
    public sealed class Palette
    {
        public Color background = ColorUtil.Hex("#15141B");
        public Color floorA = ColorUtil.Hex("#23212C");
        public Color floorB = ColorUtil.Hex("#272432");
        public Color mortar = ColorUtil.Hex("#1A1921");
        public Color wall = ColorUtil.Hex("#3D3847");
        public Color wallEdge = ColorUtil.Hex("#5A5266");
        public Color player = ColorUtil.Hex("#E8E2D0");
        public Color playerAccent = ColorUtil.Hex("#6FC3DF");
        public Color danger = ColorUtil.Hex("#FF5A3C");
        public Color spark = ColorUtil.Hex("#FFE08A");
        public Color gold = ColorUtil.Hex("#F2C14E");
        public Color health = ColorUtil.Hex("#C8553D");
        public Color postureLow = ColorUtil.Hex("#E9C46A");
        public Color postureHigh = ColorUtil.Hex("#E76F51");
        public Color uiText = ColorUtil.Hex("#EDE6D6");
        public Color uiDim = ColorUtil.Hex("#8C8597");
        public Color uiPanel = ColorUtil.Hex("#0E0D13", 0.88f);
    }

    public static class ColorUtil
    {
        /// <summary>Pure C# hex parser: safe to call from ScriptableObject field initializers.</summary>
        public static Color Hex(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);
            uint v = Convert.ToUInt32(hex, 16);
            return new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f, alpha);
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
