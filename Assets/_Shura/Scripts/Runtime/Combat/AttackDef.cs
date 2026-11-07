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

        public bool IsDangerous => kind != AttackKind.Normal;
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
