using UnityEngine;

namespace Shura
{
    /// <summary>The Perfect Answer that masters an enemy's signature attack.</summary>
    public enum SignatureAnswer
    {
        PerfectParry,
        CounterThrust,
        PerfectDodge,
    }

    public sealed class EnemyDef
    {
        public string id;
        public string displayName;
        public Shape shape;
        public Color color;
        public Shape weapon = Shape.Blade;
        public float weaponLength = 1.2f;
        public Color weaponColor = ColorUtil.Hex("#D8DEE9");
        public float maxHealth;
        public float maxPosture;
        public float radius;
        public float mass = 1f;
        public float moveSpeed;
        public float preferredRange;
        /// <summary>Chance to block a player swing while not attacking. Blocking only builds posture.</summary>
        public float blockChance;
        public float postureRegenPerSecond;
        public int idleMin;
        public int idleMax;
        public MoveDef[] moves;
        public SignatureAnswer signatureAnswer;
        public string techniqueId;
    }

    /// <summary>
    /// Enemy content. Every enemy has fixed, learnable moves (pillar 1: randomness lives in
    /// room composition and rewards, never in how an attack behaves).
    /// </summary>
    public static class EnemyDefs
    {
        public static readonly EnemyDef Ronin = new EnemyDef
        {
            id = "ronin",
            displayName = "Ronin Errante",
            shape = Shape.Square,
            color = ColorUtil.Hex("#C9A66B"),
            maxHealth = 60f,
            maxPosture = 60f,
            radius = 0.42f,
            moveSpeed = 3.2f,
            preferredRange = 1.6f,
            blockChance = 0.45f,
            postureRegenPerSecond = 9f,
            idleMin = 40,
            idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectParry,
            techniqueId = RewardIds.PatientBlade,
            moves = new[]
            {
                new MoveDef
                {
                    id = "corte_duplo", minRange = 0f, maxRange = 2.2f, weight = 3,
                    steps = new[]
                    {
                        new AttackDef
                        {
                            id = "corte_duplo_1", windup = 16, active = 4, recovery = 6, trackUntil = 10,
                            damage = 12f, postureDamage = 14f, postureOnParried = 10f,
                            shape = HitShape.Arc(1.6f, 110f), lunge = 0.5f,
                        },
                        new AttackDef
                        {
                            id = "corte_duplo_2", windup = 11, active = 4, recovery = 24, trackUntil = 6,
                            damage = 12f, postureDamage = 14f, postureOnParried = 12f,
                            shape = HitShape.Arc(1.6f, 110f), lunge = 0.5f,
                        },
                    },
                },
                new MoveDef
                {
                    id = "corte_atrasado", isSignature = true, minRange = 0f, maxRange = 2.6f, weight = 2,
                    steps = new[]
                    {
                        // Long, held anticipation: the test is waiting for the real swing.
                        new AttackDef
                        {
                            id = "corte_atrasado", windup = 44, active = 5, recovery = 28, trackUntil = 34,
                            damage = 18f, postureDamage = 24f, postureOnParried = 22f,
                            shape = HitShape.Arc(1.8f, 120f), lunge = 0.9f,
                        },
                    },
                },
            },
        };

        public static readonly EnemyDef Lanceiro = new EnemyDef
        {
            id = "lanceiro",
            displayName = "Lanceiro",
            shape = Shape.Triangle,
            color = ColorUtil.Hex("#9DB4C0"),
            weapon = Shape.Spear,
            weaponLength = 1.9f,
            weaponColor = ColorUtil.Hex("#C9CED6"),
            maxHealth = 55f,
            maxPosture = 50f,
            radius = 0.40f,
            moveSpeed = 3.0f,
            preferredRange = 2.8f,
            blockChance = 0.3f,
            postureRegenPerSecond = 8f,
            idleMin = 45,
            idleMax = 90,
            signatureAnswer = SignatureAnswer.CounterThrust,
            techniqueId = RewardIds.MirrorThrust,
            moves = new[]
            {
                new MoveDef
                {
                    id = "cutucada", minRange = 0.8f, maxRange = 3.2f, weight = 3,
                    steps = new[]
                    {
                        new AttackDef
                        {
                            id = "cutucada", windup = 15, active = 4, recovery = 18, trackUntil = 9,
                            damage = 10f, postureDamage = 12f, postureOnParried = 12f,
                            shape = HitShape.Line(2.8f, 0.55f), lunge = 0.3f,
                        },
                    },
                },
                new MoveDef
                {
                    id = "estocada", isSignature = true, minRange = 2.0f, maxRange = 4.6f, weight = 2,
                    steps = new[]
                    {
                        new AttackDef
                        {
                            id = "estocada", kind = AttackKind.Thrust, windup = 34, active = 7, recovery = 32,
                            trackUntil = 24, damage = 26f, postureDamage = 0f, postureOnParried = 0f,
                            shape = HitShape.Line(2.4f, 0.7f), lunge = 3.2f,
                        },
                    },
                },
            },
        };

        public static readonly EnemyDef Monge = new EnemyDef
        {
            id = "monge",
            displayName = "Monge do Sino",
            shape = Shape.Hexagon,
            color = ColorUtil.Hex("#B48EAD"),
            weapon = Shape.Staff,
            weaponLength = 1.7f,
            weaponColor = ColorUtil.Hex("#B08D6A"),
            maxHealth = 110f,
            maxPosture = 100f,
            radius = 0.62f,
            mass = 2.5f,
            moveSpeed = 2.2f,
            preferredRange = 1.5f,
            blockChance = 0.6f,
            postureRegenPerSecond = 12f,
            idleMin = 50,
            idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectDodge,
            techniqueId = RewardIds.PhantomStep,
            moves = new[]
            {
                new MoveDef
                {
                    id = "palmas", minRange = 0f, maxRange = 2.0f, weight = 3,
                    steps = new[]
                    {
                        new AttackDef
                        {
                            id = "palma_1", windup = 14, active = 4, recovery = 5, trackUntil = 8,
                            damage = 9f, postureDamage = 16f, postureOnParried = 14f,
                            shape = HitShape.Arc(1.5f, 100f), lunge = 0.3f,
                        },
                        new AttackDef
                        {
                            id = "palma_2", windup = 10, active = 4, recovery = 5, trackUntil = 5,
                            damage = 9f, postureDamage = 16f, postureOnParried = 14f,
                            shape = HitShape.Arc(1.5f, 100f), lunge = 0.3f,
                        },
                        new AttackDef
                        {
                            id = "palma_3", windup = 10, active = 4, recovery = 26, trackUntil = 5,
                            damage = 9f, postureDamage = 16f, postureOnParried = 18f,
                            shape = HitShape.Arc(1.5f, 100f), lunge = 0.4f,
                        },
                    },
                },
                new MoveDef
                {
                    id = "varredura", isSignature = true, minRange = 0f, maxRange = 2.4f, weight = 2,
                    steps = new[]
                    {
                        new AttackDef
                        {
                            id = "varredura", kind = AttackKind.Sweep, windup = 38, active = 6, recovery = 34,
                            trackUntil = 0, damage = 28f, postureDamage = 0f, postureOnParried = 0f,
                            shape = HitShape.Circle(2.6f), lunge = 0f,
                        },
                    },
                },
            },
        };

        public static EnemyDef Get(string id)
        {
            switch (id)
            {
                case "ronin": return Ronin;
                case "lanceiro": return Lanceiro;
                case "monge": return Monge;
                default:
                    Debug.LogError("[Shura] Inimigo desconhecido: " + id);
                    return Ronin;
            }
        }
    }
}
