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
        /// <summary>Character and weapon sprites in Resources/Art/NA; artScale draws big foes bigger.</summary>
        public string art;
        public string weaponArt;
        public float artScale = 1f;
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
        /// <summary>Technique absorbed by finishing it after mastering its signature (none for some).</summary>
        public string techniqueId;

        /// <summary>
        /// A técnica que este inimigo ensina de fato. WP-03: sem os Selos de Ki na base
        /// (Feature.KiSealsBase = false), o Onmyōji ensina o Selo Interno no lugar do Selo Explosivo.
        /// </summary>
        public string Teaches => id == "onmyoji" && !Feature.KiSealsBase ? RewardIds.InnerSeal : techniqueId;

        // ── Bestiary traits ──────────────────────────────────────────────────────────
        /// <summary>Fights with claws, fists or magic: no weapon drawn.</summary>
        public bool unarmed;
        /// <summary>Floats: traps on the floor do not touch it.</summary>
        public bool flying;
        /// <summary>Blades pass through it until a perfect answer (parry, counter) makes it solid for a moment.</summary>
        public bool ethereal;
        /// <summary>Heals this fraction of the damage it deals.</summary>
        public float lifesteal;
        /// <summary>Below this share of health it enters a frenzy: faster swings, shorter pauses.</summary>
        public float enrageBelow;
        /// <summary>Chance to flip away from a blow instead of guarding.</summary>
        public float evadeChance;
        /// <summary>Gets up once more after dying, unless finished or struck while it lies in pieces.</summary>
        public bool reassembles;
        /// <summary>Dying splits it into these (a finisher prevents it).</summary>
        public string splitInto;
        public int splitCount;
        /// <summary>Explodes this long after dying, hurting players and enemies alike.</summary>
        public float deathBlastRadius;
        public float deathBlastDamage;
        public int deathBlastFuse = 45;
        /// <summary>Every few seconds it mends the allies around it.</summary>
        public float healRadius;
        public float healFraction;
        /// <summary>What its summon moves call, and how many may stand at once.</summary>
        public string summonId;
        public int summonMax = 3;
        /// <summary>Its summons are copies of itself that vanish when it dies.</summary>
        public bool clones;
        /// <summary>Teleports away when a player stays this close.</summary>
        public float blinkRange;
        /// <summary>Only ever appears called by another enemy (never placed in rooms).</summary>
        public bool minion;
        /// <summary>One line for the bestiary and the "new enemy" toast.</summary>
        public string lore;

        // ── Phase 5, part 3 ──
        /// <summary>Multiplies the art (the new enemies share sprites with old ones).</summary>
        public Color tint = Color.white;
        /// <summary>Ōmukade: segments, each with its own posture bar.</summary>
        public int segments;
        /// <summary>Nue: movesets it cycles through with every perfect parry it suffers.</summary>
        public MoveDef[][] natures;
        public string[] natureNames;
        /// <summary>Tsukumogami: health never takes it down — only a finisher.</summary>
        public bool onlyFinisher;
        /// <summary>The blind duelist: attacks only what it hears.</summary>
        public bool blind;
        /// <summary>Nopperabō: copies the moves of whoever fell last.</summary>
        public bool mimic;
        /// <summary>Espelho do Shura: a copy of a duelist's own weapon.</summary>
        public bool mirror;

        public EnemyDef Clone() => (EnemyDef)MemberwiseClone();
    }

    /// <summary>
    /// Enemy content. Every enemy has fixed, learnable moves (pillar 1: randomness lives in
    /// room composition and rewards, never in how an attack behaves). Each one tests a
    /// different skill: timing, rhythm, direction, distance or patience.
    /// </summary>
    public static class EnemyDefs
    {
        // ── Builders (keep the tables below readable) ───────────────────────────────

        internal static AttackDef Arc(string id, int windup, int active, int recovery, int track, float damage, float posture,
                             float parried, float range, float degrees, float lunge, AttackKind kind = AttackKind.Normal)
        {
            return new AttackDef
            {
                id = id, kind = kind, windup = windup, active = active, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = parried,
                shape = HitShape.Arc(range, degrees), lunge = lunge,
            };
        }

        internal static AttackDef Line(string id, int windup, int active, int recovery, int track, float damage, float posture,
                              float parried, float length, float width, float lunge, AttackKind kind = AttackKind.Normal)
        {
            return new AttackDef
            {
                id = id, kind = kind, windup = windup, active = active, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = parried,
                shape = HitShape.Line(length, width), lunge = lunge,
            };
        }

        internal static AttackDef Shot(string id, int windup, int recovery, int track, float damage, float posture, float reflectedPosture, float speed)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 2, recovery = recovery, trackUntil = track,
                damage = damage, postureDamage = posture, postureOnParried = reflectedPosture,
                shape = HitShape.Circle(0.22f), projectileSpeed = speed,
            };
        }

        internal static MoveDef Move(string id, float minRange, float maxRange, int weight, params AttackDef[] steps)
        {
            return new MoveDef { id = id, minRange = minRange, maxRange = maxRange, weight = weight, steps = steps };
        }

        internal static MoveDef Signature(string id, float minRange, float maxRange, int weight, params AttackDef[] steps)
        {
            MoveDef m = Move(id, minRange, maxRange, weight, steps);
            m.isSignature = true;
            return m;
        }

        internal static AttackDef Sweep(string id, int windup, int active, int recovery, float damage, float radius)
        {
            return new AttackDef
            {
                id = id, kind = AttackKind.Sweep, windup = windup, active = active, recovery = recovery,
                damage = damage, shape = HitShape.Circle(radius),
            };
        }

        /// <summary>Several projectiles thrown at once in a fan; each one parries back on its own.</summary>
        internal static AttackDef Fan(string id, int windup, int recovery, int track, float damage, float posture, float reflectedPosture,
                             float speed, int count, float spread, string art)
        {
            AttackDef a = Shot(id, windup, recovery, track, damage, posture, reflectedPosture, speed);
            a.projectileCount = count;
            a.spreadDegrees = spread;
            a.projectileArt = art;
            return a;
        }

        /// <summary>A circle marked on the floor under the target that explodes after the fuse.</summary>
        internal static AttackDef Blast(string id, int windup, int recovery, float damage, float posture, float radius, int fuse,
                               BlastKind kind, int count = 1, float scatter = 0f)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 2, recovery = recovery, trackUntil = windup,
                damage = damage, postureDamage = posture, shape = HitShape.Circle(radius),
                blastRadius = radius, blastFuse = fuse, blastKind = kind, blastCount = count, blastScatter = scatter,
            };
        }

        internal static AttackDef Summon(string id, int windup, int recovery, int count)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 2, recovery = recovery, trackUntil = 0,
                shape = HitShape.Circle(0.5f), summonCount = count,
            };
        }

        internal static AttackDef Stance(string id, int windup)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 1, recovery = 0, trackUntil = windup, stance = true,
                shape = HitShape.Arc(1.0f, 90f),
            };
        }

        internal static AttackDef Feint(string id, int windup)
        {
            return new AttackDef
            {
                id = id, windup = windup, active = 1, recovery = 6, trackUntil = windup, feint = true,
                shape = HitShape.Arc(1.0f, 90f),
            };
        }

        internal static AttackDef With(AttackDef a, System.Action<AttackDef> setup)
        {
            setup(a);
            return a;
        }

        // ── Roster ──────────────────────────────────────────────────────────────────

        /// <summary>The teacher: honest swings and one held strike that tests patience.</summary>
        public static readonly EnemyDef Ronin = new EnemyDef
        {
            id = "ronin", lore = "Golpes honestos e um corte segurado que testa a sua paciência.", art = "SamuraiBlue", weaponArt = "weapon_Sword", artScale = 1.0f, displayName = "Ronin Errante", shape = Shape.Square, color = ColorUtil.Hex("#C9A66B"),
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
            id = "lanceiro", lore = "Cutuca de longe; a estocada vermelha se responde esquivando PARA DENTRO.", art = "GladiatorBlue", weaponArt = "weapon_Lance", artScale = 1.0f, displayName = "Lanceiro", shape = Shape.Triangle, color = ColorUtil.Hex("#9DB4C0"),
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
            id = "monge", lore = "Pressiona a sua postura; a varredura se atravessa com uma esquiva.", art = "Monk2", weaponArt = "weapon_Stick", artScale = 1.45f, displayName = "Monge do Sino", shape = Shape.Hexagon, color = ColorUtil.Hex("#B48EAD"),
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
            id = "kenkaku", lore = "Rajadas rápidas: a tempestade são quatro golpes para aparar em sequência.", art = "NinjaRed", weaponArt = "weapon_Ninjaku", artScale = 1.0f, displayName = "Kenkaku Veloz", shape = Shape.Pentagon, color = ColorUtil.Hex("#D1495B"),
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
            id = "carrasco", lore = "Lento e enorme, não recua e agarra. Contra o agarrão, só esquiva.", art = "DemonRed", weaponArt = "weapon_Club", artScale = 1.65f, displayName = "Carrasco", shape = Shape.Octagon, color = ColorUtil.Hex("#7A6C5D"),
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
            id = "arqueiro", lore = "Mantém distância; um parry perfeito devolve a flecha.", art = "Skeleton", weaponArt = "weapon_Bow", artScale = 1.0f, displayName = "Arqueiro das Cinzas", shape = Shape.Capsule, color = ColorUtil.Hex("#8AA170"),
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
            id = "sombra", lore = "Some e reaparece às suas costas.", art = "NinjaDark", weaponArt = "weapon_Sai", artScale = 1.0f, displayName = "Sombra", shape = Shape.Diamond, color = ColorUtil.Hex("#6A5FA0"),
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
            id = "escudeiro", lore = "Defende quase tudo de frente. Quebre a postura dele aparando.", art = "Knight", weaponArt = "weapon_Sword2", artScale = 1.15f, displayName = "Escudeiro", shape = Shape.Shield, color = ColorUtil.Hex("#4F7CAC"),
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

        // ── Bestiary: each one adds a mechanic the duel must answer ────────────────────

        /// <summary>Projectile volleys: a fan of kunai to parry one by one, and flips away from blows.</summary>
        public static readonly EnemyDef Kunoichi = new EnemyDef
        {
            id = "kunoichi", art = "NinjaMasked", weaponArt = "weapon_Kunai", displayName = "Kunoichi das Lâminas", color = ColorUtil.Hex("#E8E4F0"),
            weaponLength = 0.75f, maxHealth = 48f, maxPosture = 50f, radius = 0.38f, moveSpeed = 3.8f, preferredRange = 4.5f,
            blockChance = 0.1f, evadeChance = 0.4f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 75,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.KunaiFan,
            lore = "Arremessa leques de kunai e salta para longe dos seus golpes.",
            moves = new[]
            {
                Move("leque_de_kunai", 2.5f, 12f, 3, Fan("leque_de_kunai", 24, 24, 20, 8f, 10f, 14f, 12f, 3, 28f, "fx_kunai")),
                Move("corte_rapido_k", 0f, 1.8f, 3, Arc("corte_rapido_k", 13, 3, 18, 8, 9f, 10f, 10f, 1.4f, 100f, 0.4f)),
                Signature("chuva_de_aco", 3f, 12f, 2, Fan("chuva_de_aco", 40, 30, 34, 10f, 12f, 22f, 13f, 5, 56f, "fx_shuriken")),
            },
        };

        /// <summary>Area denial: bombs land where you stand. Keep moving.</summary>
        public static readonly EnemyDef Bombardeiro = new EnemyDef
        {
            id = "bombardeiro", art = "NinjaBomb", displayName = "Bombardeiro", color = ColorUtil.Hex("#5C7A4A"), unarmed = true,
            maxHealth = 55f, maxPosture = 55f, radius = 0.42f, moveSpeed = 3.0f, preferredRange = 5.5f,
            blockChance = 0.15f, postureRegenPerSecond = 9f, idleMin = 50, idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.ShortFuse,
            lore = "Lança bombas no chão onde você pisa: saia do círculo antes do pavio acabar.",
            moves = new[]
            {
                Move("bomba", 2.5f, 11f, 3, Blast("bomba", 26, 30, 18f, 20f, 1.5f, 55, BlastKind.Bomb)),
                Move("chute_b", 0f, 1.8f, 3, Arc("chute_b", 14, 3, 20, 9, 8f, 10f, 10f, 1.3f, 100f, 0.4f)),
                Signature("tapete_de_bombas", 2f, 11f, 2, Blast("tapete_de_bombas", 40, 40, 18f, 20f, 1.4f, 60, BlastKind.Bomb, 4, 2.0f)),
            },
        };

        /// <summary>Patience: the iaijutsu stance punishes whoever strikes it. Wait, then parry the draw.</summary>
        public static readonly EnemyDef Tengu = new EnemyDef
        {
            id = "tengu", art = "Tengu", weaponArt = "weapon_Katana", displayName = "Tengu Espadachim", color = ColorUtil.Hex("#C0392B"),
            weaponLength = 1.15f, maxHealth = 70f, maxPosture = 70f, radius = 0.42f, moveSpeed = 3.4f, preferredRange = 1.9f,
            blockChance = 0.5f, postureRegenPerSecond = 11f, idleMin = 45, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.IaiDraw,
            lore = "Em postura de saque, quem o golpeia leva o contra-ataque. Espere e apare o saque.",
            moves = new[]
            {
                Move("corte_do_vento", 0f, 2.3f, 3,
                     Arc("corte_do_vento_1", 15, 4, 6, 10, 11f, 14f, 12f, 1.7f, 120f, 0.6f),
                     Arc("corte_do_vento_2", 11, 4, 24, 6, 11f, 14f, 14f, 1.7f, 120f, 0.6f)),
                Move("rajada", 2.5f, 5.5f, 2, Line("rajada", 26, 5, 26, 20, 14f, 16f, 18f, 1.6f, 0.8f, 3.6f)),
                Signature("iai", 0f, 3.2f, 2, Stance("postura_iai", 80), Arc("iai", 13, 4, 30, 8, 22f, 28f, 34f, 2.2f, 140f, 1.2f)),
            },
        };

        /// <summary>Priority target: seals the floor under you and calls will-o'-wisps.</summary>
        public static readonly EnemyDef Onmyoji = new EnemyDef
        {
            id = "onmyoji", art = "SorcererBlack", displayName = "Onmyōji", color = ColorUtil.Hex("#B23A48"), unarmed = true,
            maxHealth = 60f, maxPosture = 55f, radius = 0.4f, moveSpeed = 2.8f, preferredRange = 6f,
            blockChance = 0.3f, postureRegenPerSecond = 9f, idleMin = 55, idleMax = 95, summonId = "hitodama", summonMax = 3,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.ExplosiveSeal,
            lore = "Marca o chão com talismãs e invoca fogos-fátuos. Derrube-o primeiro.",
            moves = new[]
            {
                Move("selo", 2f, 12f, 3, Blast("selo", 30, 26, 16f, 18f, 1.3f, 45, BlastKind.Talisman)),
                Move("invocar", 0f, 14f, 2, Summon("invocar", 45, 30, 2)),
                Move("palma_o", 0f, 1.7f, 2, Cursed(Arc("palma_o", 14, 3, 20, 9, 8f, 12f, 12f, 1.3f, 100f, 0.3f))),
                Signature("selo_triplo", 2f, 12f, 2, Blast("selo_triplo", 44, 34, 16f, 18f, 1.2f, 50, BlastKind.Talisman, 3, 2.2f)),
            },
        };

        /// <summary>Support: keeps away and mends every ally near her. Reach her through the wave.</summary>
        public static readonly EnemyDef Curandeira = new EnemyDef
        {
            id = "curandeira", art = "Shaman", weaponArt = "weapon_Stick", displayName = "Xamã Curandeira", color = ColorUtil.Hex("#7FB069"),
            weapon = Shape.Staff, weaponLength = 1.3f, maxHealth = 65f, maxPosture = 60f, radius = 0.42f, moveSpeed = 3.0f, preferredRange = 6.5f,
            blockChance = 0.4f, postureRegenPerSecond = 9f, idleMin = 60, idleMax = 100, healRadius = 6f, healFraction = 0.08f,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.ShamanBlessing,
            lore = "Cura e restaura a postura dos aliados ao redor. Enquanto ela viver, ninguém cai.",
            moves = new[]
            {
                Move("cajado", 0f, 2.2f, 3, Line("cajado", 16, 4, 20, 10, 9f, 12f, 12f, 1.9f, 0.5f, 0.3f)),
                Signature("golpe_do_ritual", 0f, 2.4f, 1, Arc("golpe_do_ritual", 30, 5, 28, 22, 14f, 20f, 22f, 1.6f, 130f, 0.6f)),
            },
        };

        /// <summary>A haunted lantern: when it dies it explodes, hurting enemies too. Use it.</summary>
        public static readonly EnemyDef Lanterna = new EnemyDef
        {
            id = "lanterna", art = "LanternRed", displayName = "Chōchin-obake", color = ColorUtil.Hex("#E63946"), unarmed = true, flying = true,
            maxHealth = 40f, maxPosture = 45f, radius = 0.4f, moveSpeed = 2.6f, preferredRange = 2.2f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 45, idleMax = 80,
            deathBlastRadius = 2.0f, deathBlastDamage = 22f, deathBlastFuse = 50,
            signatureAnswer = SignatureAnswer.CounterThrust, techniqueId = RewardIds.LivingLantern,
            lore = "Lanterna assombrada: explode ao morrer e fere também os inimigos ao redor.",
            moves = new[]
            {
                Move("lingua", 0f, 2.6f, 3, Line("lingua", 16, 4, 22, 10, 9f, 10f, 12f, 2.2f, 0.45f, 0.2f)),
                Signature("baforada", 0f, 3.8f, 2, Line("baforada", 34, 8, 30, 26, 18f, 0f, 0f, 3.4f, 1.0f, 0.4f, AttackKind.Thrust)),
            },
        };

        /// <summary>Ethereal: blades pass through it. Perfectly parry its claws to make it solid.</summary>
        public static readonly EnemyDef Onryo = new EnemyDef
        {
            id = "onryo", art = "Spirit", displayName = "Onryō", color = ColorUtil.Hex("#9AD1D4"), unarmed = true, flying = true, ethereal = true,
            bloodColor = ColorUtil.Hex("#5FA8B0"),
            maxHealth = 45f, maxPosture = 40f, radius = 0.38f, moveSpeed = 3.3f, preferredRange = 1.6f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 40, idleMax = 75,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.EtherealBody,
            lore = "Espírito vingativo: sua lâmina o atravessa. Apare as garras para torná-lo sólido.",
            moves = new[]
            {
                Move("garra", 0f, 2.0f, 3, Arc("garra", 18, 4, 20, 12, 10f, 12f, 24f, 1.5f, 110f, 0.6f)),
                Move("grito", 0f, 2.6f, 1, Spirit(Arc("grito", 30, 5, 28, 20, 14f, 18f, 30f, 2.2f, 140f, 0.4f))),
                Signature("lamento", 0f, 2.2f, 2,
                          Arc("lamento_1", 20, 4, 5, 14, 9f, 10f, 18f, 1.5f, 110f, 0.5f),
                          Arc("lamento_2", 10, 4, 5, 6, 9f, 10f, 18f, 1.5f, 110f, 0.5f),
                          Arc("lamento_3", 12, 4, 26, 6, 9f, 10f, 24f, 1.6f, 120f, 0.6f)),
            },
        };

        /// <summary>Burrows under the floor and erupts beneath you: watch the circle that follows you.</summary>
        public static readonly EnemyDef Kappa = new EnemyDef
        {
            id = "kappa", art = "KappaGreen", displayName = "Kappa do Lago", color = ColorUtil.Hex("#6A994E"), unarmed = true,
            bloodColor = ColorUtil.Hex("#386641"),
            maxHealth = 60f, maxPosture = 55f, radius = 0.42f, moveSpeed = 3.6f, preferredRange = 1.5f,
            blockChance = 0.3f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.KappaCurrent,
            lore = "Mergulha no chão e irrompe debaixo de você. O círculo que te segue é ele.",
            moves = new[]
            {
                Move("mordida", 0f, 1.8f, 3,
                     Arc("mordida_1", 13, 3, 6, 8, 8f, 10f, 10f, 1.3f, 90f, 0.6f),
                     Arc("mordida_2", 10, 3, 22, 5, 8f, 10f, 12f, 1.3f, 90f, 0.6f)),
                Signature("mergulho", 2f, 10f, 2, With(Sweep("mergulho", 70, 6, 30, 20f, 1.7f), a => { a.burrow = true; a.trackUntil = 58; })),
            },
        };

        /// <summary>A charging brute: lure it into a wall or statue and it stuns itself.</summary>
        public static readonly EnemyDef Ciclope = new EnemyDef
        {
            id = "ciclope", art = "Cyclope", weaponArt = "weapon_Hammer", artScale = 2.4f, displayName = "Oni Ciclope", color = ColorUtil.Hex("#C1121F"),
            weapon = Shape.Axe, weaponLength = 1.6f, bloodColor = ColorUtil.Hex("#6A0A14"),
            maxHealth = 160f, maxPosture = 140f, radius = 0.75f, mass = 3.5f, moveSpeed = 2.0f, preferredRange = 2.0f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 55, idleMax = 95, superArmor = true,
            signatureAnswer = SignatureAnswer.CounterThrust, techniqueId = RewardIds.OniCrush,
            lore = "Investe pela arena. Faça-o bater numa parede ou estátua e ele fica atordoado.",
            moves = new[]
            {
                Move("martelada", 0f, 2.5f, 3, Arc("martelada", 28, 5, 26, 20, 24f, 32f, 22f, 2.0f, 100f, 0.5f)),
                Move("pisao", 0f, 2.4f, 2, Sweep("pisao", 36, 6, 32, 22f, 2.4f)),
                Signature("investida", 3f, 12f, 3,
                          With(Line("investida", 44, 18, 30, 34, 28f, 0f, 0f, 1.6f, 1.2f, 8f, AttackKind.Thrust), a => a.wallStun = true)),
            },
        };

        /// <summary>Splits into two smaller slimes when it dies, unless finished.</summary>
        public static readonly EnemyDef Lodo = new EnemyDef
        {
            id = "lodo", art = "Slime", displayName = "Lodo Faminto", color = ColorUtil.Hex("#8AC926"), unarmed = true,
            bloodColor = ColorUtil.Hex("#4F772D"),
            maxHealth = 50f, maxPosture = 40f, radius = 0.45f, moveSpeed = 2.4f, preferredRange = 1.6f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 40, idleMax = 80, splitInto = "lodo_pequeno", splitCount = 2,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.SlimeHunger,
            lore = "Divide-se em dois ao morrer. Uma finalização impede a divisão.",
            moves = new[]
            {
                Move("salto", 0f, 3.2f, 3, Line("salto", 20, 6, 24, 14, 10f, 12f, 12f, 1.0f, 0.9f, 2.2f)),
                Signature("esmagar", 0f, 2.0f, 2, Sweep("esmagar", 34, 5, 30, 16f, 1.8f)),
            },
        };

        /// <summary>Frenzy: below half health it swings faster and stops pausing.</summary>
        public static readonly EnemyDef Berserker = new EnemyDef
        {
            id = "berserker", art = "Caveman", weaponArt = "weapon_AxeTool", displayName = "Bárbaro Furioso", color = ColorUtil.Hex("#F4A261"),
            weapon = Shape.Axe, weaponLength = 1.3f,
            maxHealth = 95f, maxPosture = 80f, radius = 0.48f, mass = 1.6f, moveSpeed = 3.0f, preferredRange = 1.6f,
            blockChance = 0.2f, postureRegenPerSecond = 10f, idleMin = 45, idleMax = 85, enrageBelow = 0.5f,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.Frenzy,
            lore = "Abaixo de meia vida entra em FÚRIA: golpes mais rápidos e sem pausa.",
            moves = new[]
            {
                Move("golpe_duplo_b", 0f, 2.2f, 3,
                     Arc("golpe_duplo_b1", 22, 5, 6, 16, 14f, 18f, 16f, 1.7f, 110f, 0.5f),
                     Arc("golpe_duplo_b2", 16, 5, 26, 10, 14f, 18f, 18f, 1.7f, 110f, 0.5f)),
                Signature("furia", 0f, 2.2f, 1,
                          Arc("furia_1", 18, 4, 4, 12, 9f, 10f, 12f, 1.6f, 110f, 0.4f),
                          Arc("furia_2", 10, 4, 4, 6, 9f, 10f, 12f, 1.6f, 110f, 0.4f),
                          Arc("furia_3", 10, 4, 4, 6, 9f, 10f, 12f, 1.6f, 110f, 0.4f),
                          Arc("furia_4", 10, 4, 4, 6, 9f, 10f, 12f, 1.6f, 110f, 0.4f),
                          Arc("furia_5", 14, 4, 34, 8, 12f, 14f, 18f, 1.7f, 120f, 0.6f)),
            },
        };

        /// <summary>Shadow clones: copies that strike too but fall to one blow. Find the real one.</summary>
        public static readonly EnemyDef Raijin = new EnemyDef
        {
            id = "raijin", art = "NinjaThunder", weaponArt = "weapon_Katana", displayName = "Ninja do Trovão", color = ColorUtil.Hex("#FFD166"),
            weaponLength = 1.1f, maxHealth = 60f, maxPosture = 55f, radius = 0.4f, moveSpeed = 3.9f, preferredRange = 2.2f,
            blockChance = 0.3f, postureRegenPerSecond = 10f, idleMin = 45, idleMax = 80,
            summonId = "raijin_sombra", summonMax = 2, clones = true,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.ShadowCopy,
            lore = "Cria cópias de si que também atacam, mas caem com um golpe. Ache o verdadeiro.",
            moves = new[]
            {
                Move("relampago", 2f, 6f, 3, Line("relampago", 22, 4, 24, 16, 12f, 14f, 16f, 1.4f, 0.7f, 4.0f)),
                Move("corte_r", 0f, 2.0f, 3, Arc("corte_r", 14, 4, 20, 9, 10f, 12f, 12f, 1.5f, 110f, 0.5f)),
                Move("bunshin", 0f, 14f, 2, Summon("bunshin", 36, 20, 2)),
                Signature("trovao", 0f, 2.6f, 2, Arc("trovao", 36, 5, 28, 30, 18f, 22f, 26f, 1.8f, 140f, 0.8f)),
            },
        };

        /// <summary>A sentinel eye: sweeps the arena with long beams and blinks away when cornered.</summary>
        public static readonly EnemyDef Olho = new EnemyDef
        {
            id = "olho", art = "Eye", displayName = "Olho Vigia", color = ColorUtil.Hex("#F1FAEE"), unarmed = true, flying = true,
            bloodColor = ColorUtil.Hex("#A8DADC"),
            maxHealth = 50f, maxPosture = 50f, radius = 0.42f, moveSpeed = 1.2f, preferredRange = 7.5f,
            blockChance = 0f, postureRegenPerSecond = 8f, idleMin = 50, idleMax = 90, blinkRange = 2.6f,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.SentinelBlink,
            lore = "Dispara raios que cruzam a arena e se teleporta quando você chega perto.",
            moves = new[]
            {
                Move("raio", 2f, 14f, 3, Line("raio", 48, 10, 30, 36, 18f, 0f, 0f, 11f, 0.55f, 0f, AttackKind.Sweep)),
                Signature("raio_cruzado", 2f, 14f, 2,
                          Line("raio_cruzado_1", 44, 8, 6, 34, 16f, 0f, 0f, 11f, 0.5f, 0f, AttackKind.Sweep),
                          Line("raio_cruzado_2", 24, 8, 30, 14, 16f, 0f, 0f, 11f, 0.5f, 0f, AttackKind.Sweep)),
            },
        };

        /// <summary>Lifesteal: every blow that lands heals it. Don't trade hits.</summary>
        public static readonly EnemyDef Vampiro = new EnemyDef
        {
            id = "vampiro", art = "Vampire", weaponArt = "weapon_Sword", displayName = "Kyūketsuki", color = ColorUtil.Hex("#7B2CBF"),
            weaponLength = 1.1f, bloodColor = ColorUtil.Hex("#5A0F2E"),
            maxHealth = 70f, maxPosture = 60f, radius = 0.4f, moveSpeed = 3.4f, preferredRange = 1.7f,
            blockChance = 0.45f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 75, lifesteal = 1.0f,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.BloodThirst,
            lore = "Cada golpe que acerta em você o cura. Não troque golpes com ele.",
            moves = new[]
            {
                Move("corte_v", 0f, 2.1f, 3,
                     Cursed(Arc("corte_v1", 15, 4, 6, 10, 10f, 12f, 12f, 1.6f, 110f, 0.5f)),
                     Cursed(Arc("corte_v2", 12, 4, 24, 7, 10f, 12f, 14f, 1.6f, 110f, 0.5f))),
                Signature("mordida_v", 0f, 2.6f, 2, Arc("mordida_v", 30, 6, 32, 22, 22f, 0f, 0f, 1.5f, 70f, 1.6f, AttackKind.Grab)),
            },
        };

        /// <summary>Gets up once more after dying: finish it, or shatter the bones before they rise.</summary>
        public static readonly EnemyDef Esqueleto = new EnemyDef
        {
            id = "esqueleto", art = "SkeletonDemon", weaponArt = "weapon_BigSword", artScale = 1.1f, displayName = "Gashadokuro", color = ColorUtil.Hex("#E9E3D5"),
            weapon = Shape.Blade, weaponLength = 1.6f, bloodColor = ColorUtil.Hex("#BFB8A8"),
            maxHealth = 75f, maxPosture = 70f, radius = 0.44f, mass = 1.5f, moveSpeed = 2.8f, preferredRange = 1.8f,
            blockChance = 0.35f, postureRegenPerSecond = 9f, idleMin = 45, idleMax = 85, reassembles = true,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.StubbornBones,
            lore = "Remonta os ossos uma vez. Finalize-o, ou golpeie os ossos antes que se levantem.",
            moves = new[]
            {
                Move("corte_pesado", 0f, 2.4f, 3, Cursed(Arc("corte_pesado", 24, 5, 26, 18, 16f, 20f, 18f, 1.9f, 120f, 0.5f))),
                Move("estocada_e", 1.2f, 3.2f, 2, Line("estocada_e", 26, 5, 26, 20, 14f, 16f, 16f, 2.4f, 0.6f, 0.8f)),
                Signature("corte_da_cova", 0f, 2.6f, 2, Arc("corte_da_cova", 46, 5, 30, 36, 22f, 28f, 30f, 2.0f, 150f, 0.8f)),
            },
        };

        /// <summary>Feints: some punches stop short. Parry only what really comes.</summary>
        public static readonly EnemyDef Boxeador = new EnemyDef
        {
            id = "boxeador", art = "MonkeyBoxerRed", displayName = "Macaco Boxeador", color = ColorUtil.Hex("#D62828"), unarmed = true,
            maxHealth = 60f, maxPosture = 60f, radius = 0.4f, moveSpeed = 3.7f, preferredRange = 1.4f,
            blockChance = 0.45f, postureRegenPerSecond = 12f, idleMin = 30, idleMax = 60,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.CounterPunch,
            lore = "Finge socos que não vêm. Só apare quando o anel aparecer.",
            moves = new[]
            {
                Move("jab", 0f, 1.8f, 3,
                     Arc("jab_1", 12, 3, 5, 8, 7f, 10f, 9f, 1.3f, 80f, 0.5f),
                     Arc("jab_2", 9, 3, 20, 5, 7f, 10f, 10f, 1.3f, 80f, 0.5f)),
                Signature("sequencia", 0f, 1.9f, 2,
                          Arc("sequencia_1", 14, 3, 4, 9, 7f, 10f, 10f, 1.3f, 80f, 0.5f),
                          Feint("finta_1", 16),
                          Arc("sequencia_2", 10, 3, 4, 6, 7f, 10f, 10f, 1.3f, 80f, 0.5f),
                          Feint("finta_2", 12),
                          Arc("gancho", 16, 4, 28, 10, 12f, 16f, 18f, 1.5f, 120f, 0.7f)),
            },
        };

        // ── Minions (called by others, never placed in rooms) ───────────────────────

        public static readonly EnemyDef Hitodama = new EnemyDef
        {
            id = "hitodama", art = "Flam", artScale = 0.8f, displayName = "Hitodama", color = ColorUtil.Hex("#FF9F1C"), unarmed = true, flying = true, minion = true,
            bloodColor = ColorUtil.Hex("#FF6B35"),
            maxHealth = 14f, maxPosture = 14f, radius = 0.3f, moveSpeed = 4.0f, preferredRange = 1.0f,
            blockChance = 0f, postureRegenPerSecond = 6f, idleMin = 30, idleMax = 60,
            deathBlastRadius = 1.1f, deathBlastDamage = 10f, deathBlastFuse = 35,
            moves = new[] { Move("chama", 0f, 1.6f, 1, Spirit(Arc("chama", 18, 4, 24, 12, 7f, 8f, 10f, 1.1f, 120f, 0.8f))) },
        };

        public static readonly EnemyDef LodoPequeno = new EnemyDef
        {
            id = "lodo_pequeno", art = "Slime", artScale = 0.65f, displayName = "Lodo", color = ColorUtil.Hex("#8AC926"), unarmed = true, minion = true,
            bloodColor = ColorUtil.Hex("#4F772D"),
            maxHealth = 16f, maxPosture = 16f, radius = 0.3f, moveSpeed = 3.4f, preferredRange = 1.2f,
            blockChance = 0f, postureRegenPerSecond = 6f, idleMin = 35, idleMax = 70,
            moves = new[] { Move("salto_p", 0f, 2.6f, 1, Line("salto_p", 16, 5, 22, 12, 6f, 8f, 8f, 0.8f, 0.7f, 1.6f)) },
        };

        public static readonly EnemyDef RaijinSombra = new EnemyDef
        {
            id = "raijin_sombra", art = "NinjaThunder", weaponArt = "weapon_Katana", displayName = "Ninja do Trovão", color = ColorUtil.Hex("#FFD166"), minion = true,
            weaponLength = 1.1f, bloodColor = ColorUtil.Hex("#FFD166"),
            maxHealth = 1f, maxPosture = 1f, radius = 0.4f, moveSpeed = 3.9f, preferredRange = 2.0f,
            blockChance = 0f, postureRegenPerSecond = 6f, idleMin = 50, idleMax = 90,
            moves = new[]
            {
                Move("relampago_s", 2f, 6f, 2, Line("relampago_s", 22, 4, 24, 16, 6f, 8f, 16f, 1.4f, 0.7f, 4.0f)),
                Move("corte_s", 0f, 2.0f, 3, Arc("corte_s", 14, 4, 20, 9, 5f, 6f, 12f, 1.5f, 110f, 0.5f)),
            },
        };

        /// <summary>Every enemy that rooms can place.</summary>
        /// <summary>
        /// The tutorial's master: never blocks, never tires, and has only two blows, both slow
        /// and gentle — an honest cut to parry and a red thrust to dodge. Not in the roster.
        /// </summary>
        public static readonly EnemyDef Sensei = new EnemyDef
        {
            id = "sensei", lore = "Ensina o caminho da lâmina.", art = "Master", weaponArt = "weapon_Katana", artScale = 1.0f, displayName = "Mestre do Dojo", shape = Shape.Square, color = ColorUtil.Hex("#E8E2D0"),
            maxHealth = 400f, maxPosture = 70f, radius = 0.42f, moveSpeed = 2.6f, preferredRange = 1.9f,
            blockChance = 0f, postureRegenPerSecond = 3f, idleMin = 60, idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectParry,
            moves = new[]
            {
                Move("treino_corte", 0f, 2.8f, 1, Arc("treino_corte", 46, 5, 30, 30, 6f, 10f, 26f, 1.9f, 120f, 0.6f)),
                Move("treino_estocada", 1.0f, 3.6f, 1, Line("treino_estocada", 50, 6, 34, 30, 8f, 0f, 0f, 2.6f, 0.7f, 2.4f, AttackKind.Thrust)),
            },
        };

        // ── Twelve new enemies (design doc §5.8) ─────────────────────────────────────

        /// <summary>Three wind weasels: a cut, then two more from where you are not looking.</summary>
        public static readonly EnemyDef Kamaitachi = new EnemyDef
        {
            id = "kamaitachi", displayName = "Kamaitachi", art = "Tengu", weaponArt = "weapon_Sai", artScale = 0.85f, tint = ColorUtil.Hex("#B8F0C0"),
            shape = Shape.Diamond, color = ColorUtil.Hex("#7FD39B"),
            maxHealth = 55f, maxPosture = 50f, radius = 0.36f, moveSpeed = 4.6f, preferredRange = 2.2f,
            blockChance = 0.1f, postureRegenPerSecond = 12f, idleMin = 40, idleMax = 75,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.TriSickle,
            lore = "Três doninhas do vento: a primeira corta, as outras duas aparecem cada uma de um lado.",
            moves = new[]
            {
                Move("lufada", 0f, 2.2f, 3, Arc("lufada", 14, 3, 20, 10, 9f, 10f, 12f, 1.5f, 100f, 0.8f)),
                Signature("tres_doninhas", 0f, 3.0f, 2,
                          Arc("doninha_1", 18, 3, 4, 12, 8f, 9f, 14f, 1.5f, 110f, 0.6f),
                          Teleporting(Arc("doninha_2", 14, 3, 4, 8, 8f, 9f, 14f, 1.5f, 110f, 0.4f)),
                          Teleporting(Arc("doninha_3", 14, 3, 24, 8, 9f, 10f, 16f, 1.5f, 110f, 0.4f))),
            },
        };

        /// <summary>The long neck: it bites over the statues you hide behind.</summary>
        public static readonly EnemyDef Rokurokubi = new EnemyDef
        {
            id = "rokurokubi", displayName = "Rokurokubi", art = "Monk2", artScale = 1.0f, unarmed = true, tint = ColorUtil.Hex("#EAD9FF"),
            shape = Shape.Capsule, color = ColorUtil.Hex("#C9A0FF"),
            maxHealth = 70f, maxPosture = 60f, radius = 0.4f, moveSpeed = 2.6f, preferredRange = 3.6f,
            blockChance = 0.2f, postureRegenPerSecond = 9f, idleMin = 45, idleMax = 85,
            signatureAnswer = SignatureAnswer.CounterThrust, techniqueId = RewardIds.LookOver,
            lore = "O pescoço estica por cima das estátuas: atrás de uma pedra ninguém está seguro.",
            moves = new[]
            {
                Move("pescoco", 2.0f, 5.2f, 3, Line("pescoco", 24, 4, 26, 18, 13f, 14f, 16f, 5.0f, 0.7f, 0f)),
                Move("mordida_alta", 0f, 2.0f, 2, Arc("mordida_alta", 18, 4, 24, 12, 11f, 12f, 14f, 1.5f, 110f, 0.5f)),
                Signature("bote_do_pescoco", 2.5f, 6.2f, 2, Line("bote_do_pescoco", 30, 5, 30, 24, 18f, 0f, 0f, 6.0f, 0.9f, 0.4f, AttackKind.Thrust)),
            },
        };

        /// <summary>A small spider: webs on the floor that catch the dodge.</summary>
        public static readonly EnemyDef AranhaMenor = new EnemyDef
        {
            id = "aranha_menor", displayName = "Jorōgumo Menor", art = "SorcererBlack", artScale = 0.85f, unarmed = true, tint = ColorUtil.Hex("#C9A0FF"),
            shape = Shape.Diamond, color = ColorUtil.Hex("#9B6BCF"),
            maxHealth = 60f, maxPosture = 55f, radius = 0.36f, moveSpeed = 3.4f, preferredRange = 3.0f,
            blockChance = 0.15f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.SilkThread,
            lore = "Uma filha da Senhora Aranha. As teias dela no chão prendem a esquiva.",
            moves = new[]
            {
                Move("presas", 0f, 1.9f, 3, Arc("presas", 16, 3, 22, 10, 10f, 12f, 14f, 1.4f, 100f, 0.6f)),
                Move("teia_no_chao", 2f, 9f, 3, With(Blast("teia_no_chao", 26, 28, 8f, 10f, 1.2f, 40, BlastKind.Spirit), a => a.leavesWeb = true)),
                Signature("salto_da_aranha_menor", 2.5f, 7f, 2, Arc("salto_da_aranha_menor", 24, 4, 26, 16, 14f, 14f, 18f, 1.6f, 110f, 2.2f)),
            },
        };

        /// <summary>No face: it fights with the moves of whoever fell last.</summary>
        public static readonly EnemyDef Nopperabo = new EnemyDef
        {
            id = "nopperabo", displayName = "Nopperabō", art = "FighterWhite", weaponArt = "weapon_Sword", tint = ColorUtil.Hex("#F4F1EA"), mimic = true,
            shape = Shape.Circle, color = ColorUtil.Hex("#E9E1C9"),
            maxHealth = 70f, maxPosture = 60f, radius = 0.4f, moveSpeed = 3.2f, preferredRange = 1.6f,
            blockChance = 0.25f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 75,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.EmptyMask,
            lore = "Sem rosto. Imita os golpes (e os anéis) do último inimigo que caiu.",
            moves = new[]
            {
                Move("tapa_sem_rosto", 0f, 2.0f, 3, Arc("tapa_sem_rosto", 18, 4, 22, 12, 11f, 13f, 16f, 1.5f, 110f, 0.5f)),
                Signature("rosto_vazio", 0f, 2.2f, 2, Arc("rosto_vazio", 26, 5, 28, 20, 16f, 18f, 22f, 1.7f, 130f, 0.6f)),
            },
        };

        /// <summary>A flying length of cloth: it wraps and smothers.</summary>
        public static readonly EnemyDef IttanMomen = new EnemyDef
        {
            id = "ittan_momen", displayName = "Ittan-momen", art = "Spirit", artScale = 1.15f, unarmed = true, flying = true, tint = Color.white,
            shape = Shape.Capsule, color = ColorUtil.Hex("#F1E8D6"),
            maxHealth = 55f, maxPosture = 50f, radius = 0.4f, moveSpeed = 3.8f, preferredRange = 3.0f,
            blockChance = 0f, evadeChance = 0.2f, postureRegenPerSecond = 10f, idleMin = 40, idleMax = 80,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.Shroud,
            lore = "Um tecido que voa. Se te enrolar, sufoca: o fôlego vai embora.",
            moves = new[]
            {
                Move("chicote_de_pano", 0f, 2.4f, 3, Arc("chicote_de_pano", 16, 3, 20, 10, 9f, 11f, 14f, 2.0f, 90f, 0.6f)),
                Signature("mortalha", 0f, 3.4f, 2, With(Arc("mortalha", 28, 6, 32, 22, 16f, 0f, 0f, 1.6f, 70f, 2.4f, AttackKind.Grab), a => a.drainsBreath = true)),
            },
        };

        /// <summary>A living wall: it closes the way and pushes.</summary>
        public static readonly EnemyDef Nurikabe = new EnemyDef
        {
            id = "nurikabe", displayName = "Nurikabe", art = "Cyclope", artScale = 1.6f, unarmed = true, superArmor = true, tint = ColorUtil.Hex("#A8A8A0"),
            shape = Shape.Square, color = ColorUtil.Hex("#8C8C84"),
            maxHealth = 130f, maxPosture = 140f, radius = 0.7f, mass = 3f, moveSpeed = 1.6f, preferredRange = 1.6f,
            blockChance = 0.35f, postureRegenPerSecond = 8f, idleMin = 55, idleMax = 95,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.WalkingWall,
            lore = "Uma parede viva. Fecha o caminho e empurra quem estiver na frente.",
            moves = new[]
            {
                Move("empurrao", 0f, 2.2f, 3, With(Arc("empurrao", 26, 6, 30, 18, 12f, 26f, 22f, 1.9f, 140f, 0.8f), a => a.pushBack = 3f)),
                Move("muro_que_anda", 0f, 3.0f, 2, With(Sweep("muro_que_anda", 34, 8, 34, 16f, 2.4f), a => a.pushBack = 4f)),
                Signature("parede_que_cai", 0f, 2.4f, 2, Arc("parede_que_cai", 36, 6, 36, 24, 24f, 30f, 30f, 2.2f, 150f, 0.4f)),
            },
        };

        /// <summary>The great centipede: three segments, each with its own posture.</summary>
        public static readonly EnemyDef Omukade = new EnemyDef
        {
            id = "omukade", displayName = "Ōmukade", art = "Slime", artScale = 1.25f, unarmed = true, segments = 3, tint = ColorUtil.Hex("#E06050"),
            shape = Shape.Capsule, color = ColorUtil.Hex("#C0392B"),
            maxHealth = 90f, maxPosture = 50f, radius = 0.5f, mass = 1.6f, moveSpeed = 3.0f, preferredRange = 1.8f,
            blockChance = 0.1f, postureRegenPerSecond = 9f, idleMin = 40, idleMax = 75,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.SegmentedShell,
            lore = "A centopeia gigante. Cada segmento tem a sua postura: quebre os três para finalizá-la.",
            moves = new[]
            {
                Move("mandibulas", 0f, 2.0f, 3,
                     Arc("mandibulas_1", 16, 3, 6, 10, 10f, 12f, 14f, 1.5f, 100f, 0.6f),
                     Arc("mandibulas_2", 12, 3, 24, 8, 10f, 12f, 14f, 1.5f, 100f, 0.6f)),
                Move("investida_de_patas", 2f, 6f, 2, Line("investida_de_patas", 26, 8, 30, 20, 14f, 16f, 18f, 4.0f, 0.9f, 3.0f)),
                Signature("veneno_das_cem_patas", 0f, 2.2f, 2, Cursed(Arc("veneno_das_cem_patas", 24, 4, 28, 16, 14f, 16f, 22f, 1.6f, 120f, 0.6f))),
            },
        };

        static readonly MoveDef NueRoar = Signature("rugido_quimera", 0f, 2.4f, 1, Arc("rugido_quimera", 30, 5, 30, 20, 18f, 20f, 26f, 2.0f, 140f, 0.6f));

        static readonly MoveDef[] NueMonkey =
        {
            Move("macaco", 0f, 2.0f, 3,
                 Arc("macaco_1", 14, 3, 4, 10, 9f, 10f, 12f, 1.5f, 110f, 0.6f),
                 Arc("macaco_2", 12, 3, 4, 8, 9f, 10f, 12f, 1.5f, 110f, 0.6f),
                 Arc("macaco_3", 12, 3, 24, 8, 10f, 11f, 14f, 1.5f, 110f, 0.6f)),
            NueRoar,
        };

        static readonly MoveDef[] NueTiger =
        {
            Move("tigre", 1.5f, 6f, 3, Line("tigre", 24, 8, 30, 18, 16f, 18f, 20f, 4.2f, 1.0f, 3.2f)),
            Move("garra_de_tigre", 0f, 2.2f, 2, Arc("garra_de_tigre", 20, 4, 26, 14, 15f, 18f, 20f, 1.8f, 130f, 0.8f)),
            NueRoar,
        };

        static readonly MoveDef[] NueSerpent =
        {
            Move("serpente", 0f, 3.6f, 3, Line("serpente", 22, 5, 26, 16, 13f, 0f, 0f, 3.4f, 0.6f, 1.2f, AttackKind.Thrust)),
            Move("presa_venenosa", 0f, 2.0f, 2, Cursed(Arc("presa_venenosa", 20, 4, 24, 14, 12f, 14f, 18f, 1.6f, 100f, 0.6f))),
            NueRoar,
        };

        /// <summary>The chimera: monkey, tiger and serpent — it changes nature with every parry.</summary>
        public static readonly EnemyDef Nue = new EnemyDef
        {
            id = "nue", displayName = "Nue", art = "MonkeyBoxerRed", artScale = 1.25f, unarmed = true, tint = ColorUtil.Hex("#D9A26B"),
            shape = Shape.Diamond, color = ColorUtil.Hex("#C58B4F"),
            maxHealth = 100f, maxPosture = 80f, radius = 0.45f, mass = 1.6f, moveSpeed = 3.6f, preferredRange = 1.8f,
            blockChance = 0.15f, postureRegenPerSecond = 10f, idleMin = 35, idleMax = 70,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.ThreeNatures,
            lore = "Cara de macaco, corpo de tigre, cauda de serpente. A cada parry perfeito, muda de natureza.",
            natureNames = new[] { "Macaco", "Tigre", "Serpente" },
            natures = new[] { NueMonkey, NueTiger, NueSerpent },
            moves = NueMonkey,
        };

        /// <summary>A possessed blade fighting alone: only a finisher puts it to rest.</summary>
        public static readonly EnemyDef Tsukumogami = new EnemyDef
        {
            id = "tsukumogami", displayName = "Tsukumogami", art = "Flam", weaponArt = "weapon_Katana", artScale = 0.9f, flying = true, onlyFinisher = true,
            tint = ColorUtil.Hex("#D8DEE9"), shape = Shape.Diamond, color = ColorUtil.Hex("#AFC2D8"),
            maxHealth = 60f, maxPosture = 55f, radius = 0.36f, moveSpeed = 4.0f, preferredRange = 1.8f,
            blockChance = 0.2f, postureRegenPerSecond = 9f, idleMin = 35, idleMax = 70,
            signatureAnswer = SignatureAnswer.CounterThrust, techniqueId = RewardIds.LivingBlade,
            lore = "Uma espada que fez cem anos e ganhou alma. A vida não a derruba: só uma finalização.",
            moves = new[]
            {
                Move("corte_possuido", 0f, 2.2f, 3,
                     Arc("corte_possuido_1", 16, 3, 6, 10, 10f, 12f, 14f, 1.6f, 110f, 0.7f),
                     Arc("corte_possuido_2", 12, 3, 24, 8, 11f, 12f, 16f, 1.6f, 110f, 0.7f)),
                Move("rodopio", 0f, 2.0f, 2, Sweep("rodopio", 26, 6, 26, 12f, 1.8f)),
                Signature("lamina_viva", 0f, 3.4f, 2, Line("lamina_viva", 26, 5, 28, 18, 16f, 0f, 0f, 3.6f, 0.6f, 2.0f, AttackKind.Thrust)),
            },
        };

        /// <summary>A corrupted priest: talismans that seal your techniques for five seconds.</summary>
        public static readonly EnemyDef Sacerdote = new EnemyDef
        {
            id = "sacerdote", displayName = "Sacerdote Corrompido", art = "Monk", weaponArt = "weapon_Stick", tint = ColorUtil.Hex("#B08AE0"),
            shape = Shape.Capsule, color = ColorUtil.Hex("#8E5BD0"),
            maxHealth = 75f, maxPosture = 65f, radius = 0.4f, moveSpeed = 2.6f, preferredRange = 4f,
            blockChance = 0.3f, postureRegenPerSecond = 9f, idleMin = 50, idleMax = 90,
            signatureAnswer = SignatureAnswer.PerfectDodge, techniqueId = RewardIds.ForbiddenSeal,
            lore = "Os talismãs dele selam as suas técnicas por cinco segundos. Esquive através da explosão.",
            moves = new[]
            {
                Move("cajado", 0f, 2.0f, 3, Arc("cajado", 20, 4, 24, 14, 11f, 14f, 16f, 1.7f, 110f, 0.5f)),
                Move("talisma_proibido", 2.5f, 11f, 3, With(Blast("talisma_proibido", 30, 28, 10f, 12f, 1.3f, 46, BlastKind.Talisman), a => a.sealsTechniques = true)),
                Signature("selo_triplo_proibido", 2.5f, 11f, 2,
                          With(Blast("selo_triplo_proibido", 40, 34, 12f, 14f, 1.2f, 50, BlastKind.Talisman, 3, 2.4f), a => a.sealsTechniques = true)),
            },
        };

        /// <summary>The blind duelist: he does not see you — he hears your steps and your blade.</summary>
        public static readonly EnemyDef ZatoCego = new EnemyDef
        {
            id = "zato_cego", displayName = "Duelista Cego", art = "Master", weaponArt = "weapon_Katana", blind = true, tint = ColorUtil.Hex("#E8E2D0"),
            shape = Shape.Pentagon, color = ColorUtil.Hex("#D8D2C2"),
            maxHealth = 80f, maxPosture = 70f, radius = 0.42f, moveSpeed = 3.0f, preferredRange = 1.8f,
            blockChance = 0.4f, postureRegenPerSecond = 10f, idleMin = 30, idleMax = 60,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.InnerEar,
            lore = "Não vê você. Ouve os seus passos e a sua lâmina: fique imóvel e ele perde o rastro.",
            moves = new[]
            {
                Move("corte_cego", 0f, 2.2f, 3,
                     Arc("corte_cego_1", 18, 4, 6, 12, 12f, 14f, 18f, 1.8f, 120f, 0.6f),
                     Arc("corte_cego_2", 14, 4, 26, 8, 13f, 14f, 18f, 1.8f, 120f, 0.6f)),
                Signature("iai_do_cego", 0f, 2.6f, 2, Arc("iai_do_cego", 22, 4, 30, 6, 22f, 24f, 30f, 2.2f, 140f, 1.2f)),
            },
        };

        /// <summary>The Shura's mirror: a copy of a duelist, with that duelist's own weapon.</summary>
        public static readonly EnemyDef Espelho = new EnemyDef
        {
            id = "espelho", displayName = "Espelho do Shura", art = "NinjaDark", weaponArt = "weapon_Katana", mirror = true, tint = ColorUtil.Hex("#C8D6E5"),
            shape = Shape.Diamond, color = ColorUtil.Hex("#9FB3C8"),
            maxHealth = 90f, maxPosture = 70f, radius = 0.42f, moveSpeed = 3.6f, preferredRange = 1.6f,
            blockChance = 0.35f, postureRegenPerSecond = 10f, idleMin = 30, idleMax = 60,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.Reflection,
            lore = "Um reflexo de outro samurai preso no Shuradō: a arma dele, os golpes dele, os vícios dele.",
            moves = new[]
            {
                Move("reflexo_1", 0f, 2.0f, 3, Arc("reflexo_1", 18, 4, 22, 12, 12f, 14f, 16f, 1.6f, 120f, 0.6f)),
                Signature("reflexo", 0f, 2.4f, 2, Arc("reflexo", 26, 5, 28, 18, 16f, 18f, 24f, 1.8f, 130f, 0.8f)),
            },
        };

        public static readonly EnemyDef[] All =
        {
            Ronin, Lanceiro, Monge, Kenkaku, Carrasco, Arqueiro, Sombra, Escudeiro,
            Kunoichi, Bombardeiro, Tengu, Onmyoji, Curandeira, Lanterna, Onryo, Kappa,
            Ciclope, Lodo, Berserker, Raijin, Olho, Vampiro, Esqueleto, Boxeador,
            Kamaitachi, Rokurokubi, AranhaMenor, Nopperabo, IttanMomen, Nurikabe,
            Omukade, Nue, Tsukumogami, Sacerdote, ZatoCego, Espelho,
        };

        public static readonly EnemyDef[] Minions = { Hitodama, LodoPequeno, RaijinSombra };

        /// <summary>The kitsune of the road event: fox fire, claws and a blink — met only by unmasking her.</summary>
        public static readonly EnemyDef Kitsune = new EnemyDef
        {
            id = "kitsune", art = "Shaman", displayName = "Kitsune", color = ColorUtil.Hex("#F4A261"), unarmed = true,
            bloodColor = ColorUtil.Hex("#C8553D"),
            maxHealth = 70f, maxPosture = 60f, radius = 0.4f, moveSpeed = 3.8f, preferredRange = 3.2f,
            blockChance = 0.2f, evadeChance = 0.35f, blinkRange = 1.6f, postureRegenPerSecond = 10f, idleMin = 35, idleMax = 70,
            signatureAnswer = SignatureAnswer.PerfectParry, techniqueId = RewardIds.KunaiFan,
            lore = "Nove caudas debaixo do quimono. O chá era só o começo.",
            moves = new[]
            {
                Move("garras_raposa", 0f, 2.0f, 3,
                     Arc("garras_r1", 15, 3, 5, 9, 9f, 11f, 12f, 1.5f, 110f, 0.6f),
                     Arc("garras_r2", 10, 3, 22, 6, 9f, 11f, 14f, 1.5f, 110f, 0.6f)),
                Move("fogo_de_raposa", 2.5f, 10f, 3, Fan("fogo_de_raposa", 26, 26, 20, 9f, 10f, 14f, 10f, 3, 30f, "fx_fireball")),
                Signature("baile_de_chamas", 0f, 2.6f, 2,
                          Spirit(Arc("baile_de_chamas", 34, 5, 28, 24, 16f, 18f, 26f, 2.2f, 150f, 0.6f))),
            },
        };

        /// <summary>Met only through the road (events), never placed by the room generator.</summary>
        public static readonly EnemyDef[] Specials = { Kitsune };

        // Built on first use: the Lords (BossDefs) reference these builders, so the index must not
        // be built while either class is still initialising.
        static Dictionary<string, EnemyDef> byId;
        static Dictionary<string, EnemyDef> ById => byId ?? (byId = BuildIndex());

        internal static AttackDef Teleporting(AttackDef a)
        {
            a.teleportBehind = true;
            return a;
        }

        /// <summary>A spirit blow: steel does not stop it — only a Troca Perfeita (or a dodge).</summary>
        internal static AttackDef Spirit(AttackDef a)
        {
            a.kind = AttackKind.Spirit;
            return a;
        }

        /// <summary>A cursed blow: blocking it curses you (heals stop, Shura rises).</summary>
        internal static AttackDef Cursed(AttackDef a)
        {
            a.cursed = true;
            return a;
        }

        // ── The Inner Asura ──────────────────────────────────────────────────────────

        /// <summary>
        /// The dark copy that rises after the fall into the Shuradō: the duelist's own look,
        /// their combo as its chain, their Troca strike as its red signature. Built fresh every
        /// time so it always matches the build that summoned it.
        /// </summary>
        public static EnemyDef InnerAsura(PlayerController owner) => InnerAsura(owner.Weapon, owner.ArtIndex, owner.DisplayName);

        /// <summary>A duelist's combo turned into an enemy's moves (the inner Asura, a friend's spirit, the new Ashura).</summary>
        public static EnemyDef InnerAsura(WeaponDef w, int artIndex, string name)
        {
            w = w ?? WeaponDefs.Katana;
            var steps = new List<AttackDef>();
            PlayerAttackDef[] combo = w.combo ?? new PlayerAttackDef[0];
            for (int i = 0; i < combo.Length; i++)
            {
                PlayerAttackDef c = combo[i];
                bool last = i == combo.Length - 1;
                steps.Add(new AttackDef
                {
                    id = "asura_" + i,
                    windup = (i == 0 ? 16 : 10) + c.windup,
                    active = Mathf.Max(3, c.active),
                    recovery = last ? 26 : 5,
                    trackUntil = i == 0 ? 10 : 5,
                    damage = c.damage * 1.1f,
                    postureDamage = c.posture,
                    postureOnParried = 14f,
                    shape = c.shape,
                    lunge = Mathf.Max(0.4f, c.lunge),
                });
            }
            if (steps.Count == 0) steps.Add(Arc("asura_0", 16, 4, 24, 10, 12f, 14f, 14f, 1.6f, 110f, 0.5f));
            var moves = new List<MoveDef> { Move("asura_combo", 0f, 2.6f, 3, steps.ToArray()) };
            PlayerAttackDef sw = w.swapStrike;
            if (sw != null)
            {
                moves.Add(Signature("asura_troca", 0f, 3.4f, 2, new AttackDef
                {
                    id = "asura_troca", kind = AttackKind.Sweep, windup = 32, active = Mathf.Max(4, sw.active), recovery = 30, trackUntil = 22,
                    damage = sw.damage * 1.2f, postureDamage = 0f, postureOnParried = 0f,
                    shape = sw.shape, lunge = Mathf.Max(0.8f, sw.lunge + sw.dash),
                }));
            }
            int art = Mathf.Clamp(artIndex, 0, PlayerController.PartyArt.Length - 1);
            return new EnemyDef
            {
                id = "asura_interior", displayName = "Asura de " + name,
                art = PlayerController.PartyArt[art], weaponArt = w.art, artScale = 1.1f,
                shape = Shape.Diamond, color = ColorUtil.Hex("#3A0A12"),
                weaponLength = w.length, weaponColor = ColorUtil.Hex("#D7263D"), bloodColor = ColorUtil.Hex("#2A0008"),
                maxHealth = 110f, maxPosture = 80f, radius = 0.44f,
                moveSpeed = 3.6f, preferredRange = 1.6f, blockChance = 0.5f, postureRegenPerSecond = 10f,
                idleMin = 30, idleMax = 60, signatureAnswer = SignatureAnswer.PerfectDodge,
                lifesteal = 0.1f, enrageBelow = 0.3f,
                lore = "Tudo o que você matou sem misericórdia, de pé à sua frente.",
                moves = moves.ToArray(),
            };
        }

        static Dictionary<string, EnemyDef> BuildIndex()
        {
            var index = new Dictionary<string, EnemyDef>();
            foreach (EnemyDef def in All) index[def.id] = def;
            foreach (EnemyDef def in Minions) index[def.id] = def;
            foreach (EnemyDef def in Specials) index[def.id] = def;
            foreach (EnemyDef def in BossDefs.Defs()) index[def.id] = def;
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
