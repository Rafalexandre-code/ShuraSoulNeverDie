using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Shared body for the player and enemies: simulated position (advanced only in Tick),
    /// interpolated rendering, procedural animation (breathing, walk bob, squash & stretch,
    /// hit flash) and a weapon rig whose pose subclasses drive from their frame data.
    /// Gameplay never reads the Transform; it is purely the view.
    /// </summary>
    public abstract class Fighter : MonoBehaviour
    {
        /// <summary>Weapon angle (degrees, relative to facing) and reach offset for this frame.</summary>
        protected struct WeaponPose
        {
            public float angle;
            public float extend;
            /// <summary>When true the pose is applied exactly (swings); otherwise it is smoothed.</summary>
            public bool snap;
            public Color tint;
        }

        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Facing = Vector2.right;
        public Vector2 Velocity;

        public float Radius { get; protected set; } = 0.4f;
        public float Mass { get; protected set; } = 1f;
        public float Health { get; protected set; }
        public float MaxHealth { get; protected set; }
        public PostureMeter Posture { get; protected set; }
        public bool IsAlive => Health > 0f;
        public Vector2 RenderPosition { get; private set; }

        protected World world;
        protected Transform bodyPivot;
        protected SpriteRenderer body;
        protected SpriteRenderer pointer;
        protected SpriteRenderer shadow;
        protected SpriteRenderer weapon;
        protected Color weaponColor = Color.white;

        Transform weaponPivot;
        float weaponLength;
        float weaponAngle;
        float weaponExtend;
        SpriteRenderer hurtbox;
        float flashTimer;
        Vector2 squash = Vector2.one;
        float bobPhase;

        protected bool Flashing => flashTimer > 0f;
        protected float FacingAngle => CombatMath.AngleDegrees(Facing);

        protected void BuildView(World owner, Shape shape, Color color, float radius)
        {
            world = owner;
            Radius = radius;
            bobPhase = Random.Range(0f, 10f);

            shadow = SpriteUtil.Make("Shadow", transform, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.5f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, -radius * 0.55f, 0f);
            shadow.transform.localScale = new Vector3(radius * 2.8f, radius * 1.3f, 1f);

            bodyPivot = new GameObject("Body").transform;
            bodyPivot.SetParent(transform, false);
            body = SpriteUtil.Make("Shape", bodyPivot, ShapeLibrary.Get(shape), color, 0);
            pointer = SpriteUtil.Make("Pointer", bodyPivot, ShapeLibrary.Get(Shape.Triangle), color, 1);
            pointer.transform.localPosition = new Vector3(0.7f, 0f, 0f);
            pointer.transform.localScale = new Vector3(0.34f, 0.34f, 1f);

            hurtbox = SpriteUtil.Make("DebugHurtbox", transform, ShapeLibrary.Get(Shape.Ring), new Color(0.3f, 1f, 0.4f, 0.8f), Sorting.Overlay + 50);
            hurtbox.transform.localScale = Vector3.one * radius * 2f;
            hurtbox.enabled = false;
        }

        protected void BuildWeapon(Shape shape, Color color, float length)
        {
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
            weapon = SpriteUtil.Make("Weapon", weaponPivot, ShapeLibrary.Get(shape), color, 2);
            weaponColor = color;
            weaponLength = length;
        }

        public void Teleport(Vector2 p)
        {
            Position = p;
            PrevPosition = p;
            Velocity = Vector2.zero;
            RenderPosition = p;
            transform.position = new Vector3(p.x, p.y, 0f);
        }

        protected void BeginTick()
        {
            PrevPosition = Position;
        }

        protected void Integrate()
        {
            Position += Velocity * SimClock.Dt;
        }

        public void Squash(float along, float across)
        {
            squash = new Vector2(along, across);
        }

        public void Flash(float seconds)
        {
            if (seconds > flashTimer) flashTimer = seconds;
        }

        /// <summary>visualDt is 0 during hit-stop and pause so impact poses hold.</summary>
        public void Render(float alpha, float visualDt, bool showHitboxes)
        {
            Vector2 p = Vector2.LerpUnclamped(PrevPosition, Position, alpha);
            RenderPosition = p;
            transform.position = new Vector3(p.x, p.y, 0f);

            // Top-down depth: lower on screen draws in front.
            int order = -Mathf.RoundToInt(p.y * 20f);
            body.sortingOrder = order;
            pointer.sortingOrder = order + 1;

            // Breathing when idle, a light hop when moving: bodies never look frozen.
            float speed = IsAlive ? Velocity.magnitude : 0f;
            bobPhase += visualDt * (2.2f + speed * 2.2f);
            float breath = IsAlive ? 0.025f * Mathf.Sin(bobPhase) : 0f;
            float hop = Mathf.Abs(Mathf.Sin(bobPhase * 1.6f)) * 0.07f * Mathf.Clamp01(speed / 4f);
            bodyPivot.localPosition = new Vector3(0f, hop, 0f);

            squash = Vector2.Lerp(squash, Vector2.one, CombatMath.Damp(12f, visualDt));
            bodyPivot.localRotation = Quaternion.Euler(0f, 0f, FacingAngle);
            bodyPivot.localScale = new Vector3(Radius * 2f * squash.x * (1f + breath), Radius * 2f * squash.y * (1f - breath * 0.5f), 1f);
            if (flashTimer > 0f) flashTimer -= visualDt;

            if (weapon != null) RenderWeapon(order, hop, visualDt);

            hurtbox.enabled = showHitboxes && IsAlive;
            RenderExtra(p, visualDt);
        }

        void RenderWeapon(int order, float hop, float visualDt)
        {
            WeaponPose pose = GetWeaponPose();
            if (pose.snap || visualDt <= 0f)
            {
                weaponAngle = pose.angle;
                weaponExtend = pose.extend;
            }
            else
            {
                float k = CombatMath.Damp(16f, visualDt);
                weaponAngle = Mathf.LerpAngle(weaponAngle, pose.angle, k);
                weaponExtend = Mathf.Lerp(weaponExtend, pose.extend, k);
            }

            float worldAngle = FacingAngle + weaponAngle;
            weaponPivot.localPosition = new Vector3(0f, hop, 0f);
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, worldAngle);
            weapon.transform.localPosition = new Vector3(Radius * 0.55f + weaponExtend, 0f, 0f);
            weapon.transform.localScale = new Vector3(weaponLength, weaponLength, 1f);
            weapon.color = pose.tint;
            // A weapon pointing "up" the screen passes behind the body, otherwise in front.
            weapon.sortingOrder = Mathf.Sin(worldAngle * Mathf.Deg2Rad) > 0.2f ? order - 1 : order + 2;
        }

        protected virtual WeaponPose GetWeaponPose()
        {
            return new WeaponPose { angle = -50f, tint = weaponColor };
        }

        protected abstract void RenderExtra(Vector2 renderPosition, float visualDt);
    }
}
