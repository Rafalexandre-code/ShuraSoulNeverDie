using System.Collections.Generic;
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
        /// <summary>0x72 character id (idle/run frames) and weapon sprite.</summary>
        public string art;
        public string weaponArt;
        public Color color;
        public Shape weapon = Shape.Blade;
        public float weaponLength = 1.2f;
        public Color weaponColor = ColorUtil.Hex("#D8DEE9");
        public Color bloodColor = ColorUtil.Hex("#8E0F1E");
        public float maxHealth;
        public float maxPosture;
        public float radius;
        public float mass = 1f;
        public float moveSpeed;
        public float preferredRange;
        /// <summary>Chance to deflect a player swing while not attacking. A deflect bounces the player's blade.</summary>
        public float blockChance;
        public float postureRegenPerSecond;
        public int idleMin;
        public int idleMax;
        /// <summary>Heavy enemies are not pushed back by hits.</summary>
        public bool superArmor;
        public MoveDef[] moves;
        public SignatureAnswer signatureAnswer;
        public string techniqueId;
    }

    /// <summary>
    /// Enemy content. Every enemy has fixed, learnable moves (pillar 1: randomness lives in
    /// room composition and rewards, never in how an attack behaves). Each one tests a
    /// different skill: timing, rhythm, direction, distance or patience.
    /// </summary>
    public static class EnemyDefs
    {
        // ── Builders (keep the tables below readable) ───────────────────────────────

        static AttackDef Arc(string id, int windup, int active, int recovery, int track, float damage, float posture,
                             float parried, float range, float degrees, float lunge, AttackKind kind = AttackKind.Normal)
        {
            return new AttackDef
            {
                id = id, kind = kind, windup = windup, active = active, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = parried,
                shape = HitShape.Arc(range, degrees), lunge = lunge,
            };
        }

        static AttackDef Line(string id, int windup, int active, int recovery, int track, float damage, float posture,
                              float parried, float length, float width, float lunge, AttackKind kind = AttackKind.Normal)
        {
            return new AttackDef
            {
                id = id, kind = kind, windup = windup, active = active, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = parried,
                shape = HitShape.Line(length, width), lunge = lunge,
            };
        }

        static AttackDef Shot(string id, int windup, int recovery, int track, float damage, float posture, float reflectedPosture, float speed)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 2, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = reflectedPosture,
                shape = HitShape.Circle(0.22f), projectileSpeed = speed,
            };
        }

        static MoveDef Move(string id, float minRange, float maxRange, int weight, params AttackDef[] steps)
        {
            return new MoveDef { id = id, minRange = minRange, maxRange = maxRange, weight = weight, steps = steps };
        }

        static MoveDef Signature(string id, float minRange, float maxRange, int weight, params AttackDef[] steps)
        {
            MoveDef m = Move(id, minRange, maxRange, weight, steps);
            m.isSignature = true;
            return m;
        }

        // ── Roster ──────────────────────────────────────────────────────────────────

        /// <summary>The teacher: honest swings and one held strike that tests patience.</summary>
        public static readonly EnemyDef Ronin = new EnemyDef
        {
            id = "ronin", art = "orc_warrior", weaponArt = "weapon_regular_sword", displayName = "Ronin Errante", shape = Shape.Square, color = ColorUtil.Hex("#C9A66B"),
            maxHealth = 60f, maxPosture = 60f, radius = 0.42f, moveSpeed = 3.2f, preferredRange = 1.6f,
            blockChance = 0.45f, postureRegenPerSecond = 9f, idleMin = 40, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.PatientBlade,
            moves = new[]
            {
                Move("corte_duplo", 0f, 2.2f, 3,
                     Arc("corte_duplo_1", 16, 4, 6, 10, 12f, 14f, 10f, 1.6f, 110f, 0.5f),
                     Arc("corte_duplo_2", 11, 4, 24, 6, 12f, 14f, 12f, 1.6f, 110f, 0.5f)),
                Signature("corte_atrasado", 0f, 2.6f, 2,
                          Arc("corte_atrasado", 44, 5, 28, 34, 18f, 24f, 22f, 1.8f, 120f, 0.9f)),
            },
        };

        /// <summary>Distance: pokes from range, and a dangerous thrust you must dodge INTO.</summary>
        public static readonly EnemyDef Lanceiro = new EnemyDef
        {
            id = "lanceiro", art = "masked_orc", weaponArt = "weapon_spear", displayName = "Lanceiro", shape = Shape.Triangle, color = ColorUtil.Hex("#9DB4C0"),
            weapon = Shape.Spear, weaponLength = 1.9f, weaponColor = ColorUtil.Hex("#C9CED6"),
            maxHealth = 55f, maxPosture = 50f, radius = 0.40f, moveSpeed = 3.0f, preferredRange = 2.8f,
            blockChance = 0.3f, postureRegenPerSecond = 8f, idleMin = 45, idleMax = 90,
            signatureAnswer = SignatureAnswer.CounterThrust, techniqueId = RewardIds.MirrorThrust,
            moves = new[]
            {
                Move("cutucada", 0.8f, 3.2f, 3, Line("cutucada", 15, 4, 18, 9, 10f, 12f, 12f, 2.8f, 0.55f, 0.3f)),
                Signature("estocada", 2.0f, 4.6f, 2,
                          Line("estocada", 34, 7, 32, 24, 26f, 0f, 0f, 2.4f, 0.7f, 3.2f, AttackKind.Thrust)),
            },
        };

        /// <summary>Heavy posture pressure and a dangerous sweep you must dodge through.</summary>
        public static readonly EnemyDef Monge = new EnemyDef
        {
            id = "monge", art = "big_zombie", weaponArt = "weapon_red_magic_staff", displayName = "Monge do Sino", shape = Shape.Hexagon, color = ColorUtil.Hex("#B48EAD"),
            weapon = Shape.Staff, weaponLength = 1.7f, weaponColor = ColorUtil.Hex("#B08D6A"),
            maxHealth = 110f, maxPosture = 100f, radius = 0.62f, mass = 2.5f, moveSpeed = 2.2f, preferredRange = 1.5f,
            blockChance = 0.6f, postureRegenPerSecond = 12f, idleMin = 50, idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.PhantomStep,
            moves = new[]
            {
                Move("palmas", 0f, 2.0f, 3,
                     Arc("palma_1", 14, 4, 5, 8, 9f, 16f, 14f, 1.5f, 100f, 0.3f),
                     Arc("palma_2", 10, 4, 5, 5, 9f, 16f, 14f, 1.5f, 100f, 0.3f),
                     Arc("palma_3", 10, 4, 26, 5, 9f, 16f, 18f, 1.5f, 100f, 0.4f)),
                Signature("varredura", 0f, 2.4f, 2,
                          new AttackDef
                          {
                              id = "varredura", kind = AttackKind.Sweep, windup = 38, active = 6, recovery = 34,
                              damage = 28f, shape = HitShape.Circle(2.6f),
                          }),
            },
        };

        /// <summary>Rhythm: fast flurries; its signature is four strikes to parry in a row.</summary>
        public static readonly EnemyDef Kenkaku = new EnemyDef
        {
            id = "kenkaku", art = "chort", weaponArt = "weapon_duel_sword", displayName = "Kenkaku Veloz", shape = Shape.Pentagon, color = ColorUtil.Hex("#D1495B"),
            weapon = Shape.Blade, weaponLength = 1.05f, weaponColor = ColorUtil.Hex("#F0E6E8"),
            maxHealth = 50f, maxPosture = 55f, radius = 0.40f, moveSpeed = 3.9f, preferredRange = 1.5f,
            blockChance = 0.5f, postureRegenPerSecond = 11f, idleMin = 30, idleMax = 60,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.DuelistRhythm,
            moves = new[]
            {
                Move("corte_rapido", 0f, 2.0f, 3,
                     Arc("rapido_1", 13, 3, 5, 8, 8f, 10f, 9f, 1.5f, 100f, 0.5f),
                     Arc("rapido_2", 9, 3, 22, 5, 8f, 10f, 10f, 1.5f, 100f, 0.5f)),
                Signature("tempestade", 0f, 2.2f, 2,
                          Arc("tempestade_1", 18, 3, 4, 12, 8f, 10f, 12f, 1.5f, 100f, 0.4f),
                          Arc("tempestade_2", 9, 3, 4, 5, 8f, 10f, 12f, 1.5f, 100f, 0.4f),
                          Arc("tempestade_3", 9, 3, 4, 5, 8f, 10f, 12f, 1.5f, 100f, 0.4f),
                          Arc("tempestade_4", 11, 3, 28, 5, 10f, 12f, 16f, 1.6f, 110f, 0.6f)),
            },
        };

        /// <summary>Patience: slow and enormous, ignores knockback, and grabs (dodge only).</summary>
        public static readonly EnemyDef Carrasco = new EnemyDef
        {
            id = "carrasco", art = "ogre", weaponArt = "weapon_waraxe", displayName = "Carrasco", shape = Shape.Octagon, color = ColorUtil.Hex("#7A6C5D"),
            weapon = Shape.Axe, weaponLength = 1.9f, weaponColor = ColorUtil.Hex("#A39A90"), bloodColor = ColorUtil.Hex("#6A0A14"),
            maxHealth = 150f, maxPosture = 130f, radius = 0.72f, mass = 3.5f, moveSpeed = 1.9f, preferredRange = 1.6f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 55, idleMax = 95, superArmor = true,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.ExecutionerBlow,
            moves = new[]
            {
                Move("machado_descendente", 0f, 2.3f, 3,
                     Arc("machado", 30, 5, 26, 22, 24f, 34f, 20f, 1.9f, 90f, 0.6f)),
                Move("golpe_duplo", 0f, 2.3f, 2,
                     Arc("duplo_1", 24, 5, 8, 16, 18f, 26f, 16f, 1.9f, 110f, 0.4f),
                     Arc("duplo_2", 18, 5, 30, 10, 20f, 28f, 20f, 1.9f, 110f, 0.5f)),
                Signature("agarrao", 0f, 2.4f, 2,
                          Arc("agarrao", 32, 6, 34, 24, 32f, 0f, 0f, 1.7f, 70f, 1.4f, AttackKind.Grab)),
            },
        };

        /// <summary>Distance and direction: keeps away and shoots arrows that a perfect parry reflects.</summary>
        public static readonly EnemyDef Arqueiro = new EnemyDef
        {
            id = "arqueiro", art = "skelet", weaponArt = "weapon_bow", displayName = "Arqueiro das Cinzas", shape = Shape.Capsule, color = ColorUtil.Hex("#8AA170"),
            weapon = Shape.Bow, weaponLength = 0.95f, weaponColor = ColorUtil.Hex("#B08D6A"),
            maxHealth = 45f, maxPosture = 45f, radius = 0.38f, moveSpeed = 3.0f, preferredRange = 6.0f,
            blockChance = 0.2f, postureRegenPerSecond = 8f, idleMin = 40, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.ReflexArrow,
            moves = new[]
            {
                Move("chute", 0f, 1.7f, 4, Arc("chute", 12, 3, 18, 8, 8f, 10f, 10f, 1.3f, 100f, 0.4f)),
                Move("flecha", 2.5f, 14f, 3, Shot("flecha", 26, 26, 22, 10f, 12f, 18f, 11f)),
                Signature("flecha_carregada", 3f, 14f, 2, Shot("flecha_carregada", 46, 30, 38, 18f, 20f, 30f, 15f)),
            },
        };

        /// <summary>Direction: vanishes and strikes from behind — turn and parry.</summary>
        public static readonly EnemyDef Sombra = new EnemyDef
        {
            id = "sombra", art = "necromancer", weaponArt = "weapon_knife", displayName = "Sombra", shape = Shape.Diamond, color = ColorUtil.Hex("#6A5FA0"),
            weapon = Shape.Blade, weaponLength = 0.8f, weaponColor = ColorUtil.Hex("#C9BFF0"), bloodColor = ColorUtil.Hex("#3D1F5C"),
            maxHealth = 50f, maxPosture = 55f, radius = 0.38f, moveSpeed = 3.6f, preferredRange = 2.4f,
            blockChance = 0.35f, postureRegenPerSecond = 10f, idleMin = 45, idleMax = 85,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.Ambush,
            moves = new[]
            {
                Move("corte_sombrio", 0f, 2.0f, 3, Arc("corte_sombrio", 14, 4, 20, 9, 11f, 12f, 12f, 1.4f, 100f, 0.6f)),
                Signature("golpe_das_sombras", 0f, 8f, 3, Teleporting(Arc("golpe_das_sombras", 38, 4, 28, 32, 16f, 18f, 26f, 1.6f, 110f, 0.5f))),
            },
        };

        /// <summary>Patience: deflects almost everything from the front. Break it by parrying.</summary>
        public static readonly EnemyDef Escudeiro = new EnemyDef
        {
            id = "escudeiro", art = "dwarf_m", weaponArt = "weapon_knight_sword", displayName = "Escudeiro", shape = Shape.Shield, color = ColorUtil.Hex("#4F7CAC"),
            weapon = Shape.Blade, weaponLength = 1.0f, weaponColor = ColorUtil.Hex("#D8DEE9"),
            maxHealth = 70f, maxPosture = 80f, radius = 0.5f, mass = 2f, moveSpeed = 2.6f, preferredRange = 1.7f,
            blockChance = 0.85f, postureRegenPerSecond = 9f, idleMin = 50, idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.Bulwark,
            moves = new[]
            {
                Move("golpe_de_escudo", 0f, 2.0f, 3, Arc("golpe_de_escudo", 16, 4, 20, 10, 10f, 16f, 14f, 1.4f, 100f, 0.4f)),
                Signature("investida_de_escudo", 1.2f, 3.6f, 2,
                          Line("investida_de_escudo", 30, 6, 30, 22, 14f, 22f, 30f, 1.5f, 0.9f, 1.8f)),
            },
        };

        public static readonly EnemyDef[] All = { Ronin, Lanceiro, Monge, Kenkaku, Carrasco, Arqueiro, Sombra, Escudeiro };

        static readonly Dictionary<string, EnemyDef> ById = BuildIndex();

        static AttackDef Teleporting(AttackDef a)
        {
            a.teleportBehind = true;
            return a;
        }

        static Dictionary<string, EnemyDef> BuildIndex()
        {
            var index = new Dictionary<string, EnemyDef>();
            foreach (EnemyDef def in All) index[def.id] = def;
            return index;
        }

        public static bool TryGet(string id, out EnemyDef def) => ById.TryGetValue(id, out def);

        public static EnemyDef Get(string id)
        {
            if (TryGet(id, out EnemyDef def)) return def;
            Debug.LogError("[Shura] Inimigo desconhecido: " + id);
            return Ronin;
        }
    }
}
