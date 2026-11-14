namespace Shura
{
    public enum AttackKind
    {
        /// <summary>Can be parried or blocked.</summary>
        Normal,
        /// <summary>Dangerous circle around the attacker: leave it or dodge through it.</summary>
        Sweep,
        /// <summary>Dangerous straight line: dodge INTO it for a counter, or sidestep.</summary>
        Thrust,
        /// <summary>Dangerous grab: cannot be parried or blocked, only dodged.</summary>
        Grab,
        /// <summary>A yokai's spirit blow (purple ring): it passes through the blade; only a Troca Perfeita (or a dodge) answers it.</summary>
        Spirit,
    }

    /// <summary>How a floor blast looks (and sounds).</summary>
    public enum BlastKind
    {
        Bomb,
        Talisman,
        Fire,
        Spirit,
        /// <summary>Storm lightning (weather), drawn to steel.</summary>
        Lightning,
    }

    public enum HitShapeType
    {
        Arc,
        Circle,
        Line,
    }

    public struct HitShape
    {
        public HitShapeType type;
        public float range;
        public float arcDegrees;
        public float width;

        public static HitShape Arc(float range, float degrees)
        {
            return new HitShape { type = HitShapeType.Arc, range = range, arcDegrees = degrees };
        }

        public static HitShape Circle(float radius)
        {
            return new HitShape { type = HitShapeType.Circle, range = radius, arcDegrees = 360f };
        }

        public static HitShape Line(float length, float width)
        {
            return new HitShape { type = HitShapeType.Line, range = length, width = width };
        }
    }

    /// <summary>One swing. Frame counts are at 60 Hz.</summary>
    public sealed class AttackDef
    {
        public string id;
        public AttackKind kind = AttackKind.Normal;
        public int windup;
        public int active;
        public int recovery;
        /// <summary>The attacker keeps turning toward the target until this windup frame, then commits.</summary>
        public int trackUntil;
        public float damage;
        /// <summary>Posture dealt to the player when they block it.</summary>
        public float postureDamage;
        /// <summary>Posture dealt to the attacker when the player perfectly parries it.</summary>
        public float postureOnParried;
        public HitShape shape;
        /// <summary>Distance travelled forward across the active frames.</summary>
        public float lunge;
        /// <summary>When above zero, the swing releases a projectile (units/second) instead of hitting in melee.</summary>
        public float projectileSpeed;
        public float projectileRadius = 0.22f;
        /// <summary>The attacker vanishes and reappears behind the player when the windup starts.</summary>
        public bool teleportBehind;

        // ── Bestiary mechanics ───────────────────────────────────────────────────────
        /// <summary>Projectiles released at once, spread evenly across <see cref="spreadDegrees"/> (a fan of kunai).</summary>
        public int projectileCount = 1;
        public float spreadDegrees;
        /// <summary>Sprite strip of the projectile in Resources/Art/NA (empty = arrow).</summary>
        public string projectileArt;

        /// <summary>
        /// Instead of hitting, the release marks a circle on the floor under the target that
        /// explodes after <see cref="blastFuse"/> frames (bombs, talismans). Leave it or dodge.
        /// </summary>
        public float blastRadius;
        public int blastFuse = 50;
        public int blastCount = 1;
        /// <summary>Extra blasts land this far around the first one.</summary>
        public float blastScatter;
        public BlastKind blastKind;

        /// <summary>Hidden underground through the windup, crawling toward the target; erupts on the active frames.</summary>
        public bool burrow;
        /// <summary>A charge that ends in a stun if the attacker slams into a wall or statue.</summary>
        public bool wallStun;
        /// <summary>A fake: winds up and pulls back without striking (no timing ring).</summary>
        public bool feint;
        /// <summary>
        /// A drawn stance: striking the attacker during its windup is deflected and answered at
        /// once by the next step (an iaijutsu draw). Waiting it out lets the stance end into that step.
        /// </summary>
        public bool stance;
        /// <summary>The release calls minions (see EnemyDef.summonId) instead of striking.</summary>
        public int summonCount;
        /// <summary>Cursed blow (green ring): parrying is safe, but blocking or taking it curses.</summary>
        public bool cursed;
        /// <summary>A huge group blow (white broken ring): only two duelists parrying together stop it.</summary>
        public bool groupStrike;
        /// <summary>Ittan-momen: it smothers — a hit drains breath.</summary>
        public bool drainsBreath;
        /// <summary>Nurikabe: hit or blocked, the duelist is pushed this far.</summary>
        public float pushBack;
        /// <summary>The spider's blast leaves a web on the floor.</summary>
        public bool leavesWeb;
        /// <summary>The corrupted priest's talisman seals the duelist's techniques.</summary>
        public bool sealsTechniques;

        public bool IsProjectile => projectileSpeed > 0f;
        public bool IsBlast => blastRadius > 0f;
        public bool IsSummon => summonCount > 0;
        /// <summary>The swing itself never touches anyone (stances, feints, ranged releases).</summary>
        public bool NoMeleeHit => IsProjectile || IsBlast || IsSummon || feint || stance;
        /// <summary>Shows the melee "answer now" ring.</summary>
        public bool ShowsTimingRing => !NoMeleeHit;

        public bool IsDangerous => kind != AttackKind.Normal && kind != AttackKind.Spirit;
        public bool Parryable => kind == AttackKind.Normal;
        public int TotalFrames => windup + active + recovery;
    }

    /// <summary>A sequence of swings the AI commits to (a combo).</summary>
    public sealed class MoveDef
    {
        public string id;
        public AttackDef[] steps;
        public float minRange;
        public float maxRange;
        public int weight = 1;
        /// <summary>Mastering the signature (its Perfect Answer) lets the player absorb the enemy's technique.</summary>
        public bool isSignature;
    }
}
