using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Shared body for the player and enemies: simulated position (advanced only in Tick),
    /// interpolated rendering, procedural animation (breathing, walk bob, lean, squash &amp;
    /// stretch, hit flash), a weapon rig whose pose subclasses drive from their frame data,
    /// and a blade trail drawn from the blade's real path. Gameplay never reads the Transform.
    /// </summary>
    public abstract class Fighter : MonoBehaviour
    {
        /// <summary>Weapon angle (degrees, relative to facing) and reach offset for this frame.</summary>
        protected struct WeaponPose
        {
            public float angle;
            public float extend;
            /// <summary>Strikes follow the pose tightly; idle poses ease in softly.</summary>
            public bool snap;
            public Color tint;
        }

        const float SnapSharpness = 60f;
        const float SoftSharpness = 16f;
        const float LeanSharpness = 18f;

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
        /// <summary>The figure's own frame on screen (lakes mirror it).</summary>
        public SpriteRenderer BodyRenderer => body;
        /// <summary>White silhouette over the body: hit flash, wind-up heat, "reading you" glow.</summary>
        protected SpriteRenderer bodyGlow;
        protected SpriteRenderer shadow;
        protected SpriteRenderer weapon;
        protected Color weaponColor = Color.white;
        protected CharacterArt art;

        const float LeanToTilt = 0.35f;
        const float MaxTilt = 14f;

        float visualScale = 1f;
        float feetY;
        float animTime;
        ArtDir dir = ArtDir.Down;
        bool wasMoving;
        int currentFrame;

        Transform weaponPivot;
        float weaponLength;
        float weaponAngle;
        float weaponExtend;
        float lean;
        float renderAlpha = 1f;
        SpriteRenderer hurtbox;
        float flashTimer;
        Vector2 squash = Vector2.one;
        float bobPhase;
        BladeTrail trail;

        protected bool Flashing => flashTimer > 0f;
        /// <summary>Weapon angle as last rendered (relative to facing): where the blade was at a clash.</summary>
        protected float CurrentWeaponAngle => weaponAngle;
        protected float FacingAngle => CombatMath.AngleDegrees(Facing);

        /// <summary>World-space angle of the weapon as last rendered.</summary>
        protected float WeaponWorldAngle => FacingAngle + weaponAngle;
        /// <summary>Distance from the body centre to the weapon's grip as last rendered.</summary>
        protected float WeaponReach => Radius * 0.55f + weaponExtend;
        protected float WeaponLength => weaponLength;

        /// <summary>
        /// Continuous animation time for a state frame counter: the simulation runs in whole
        /// ticks, but poses are evaluated between them so blades glide at any refresh rate.
        /// </summary>
        protected float AnimFrame(int stateFrame) => Mathf.Max(0f, stateFrame - 1f + renderAlpha);

        /// <summary>0 → 1 quickly, then back to 0: the shape of a blade bouncing off another.</summary>
        protected static float Bounce(float frame, int duration, int peakFrame = 5)
        {
            if (frame <= 0f || duration <= 0) return 0f;
            if (frame < peakFrame) return CombatMath.EaseOutCubic(frame / peakFrame);
            return 1f - CombatMath.EaseOutQuad((frame - peakFrame) / Mathf.Max(1f, duration - peakFrame));
        }

        /// <summary>
        /// Pixel-art body: the sprite stands on the collision circle's lower edge (top-down
        /// three-quarter view), faces left or right, and is drawn at the shared 16 px/unit
        /// density times <paramref name="scale"/> (Guardians are bigger).
        /// </summary>
        protected void BuildView(World owner, CharacterArt characterArt, float radius, float scale)
        {
            world = owner;
            art = characterArt;
            Radius = radius;
            visualScale = scale;
            feetY = -radius * 0.6f;
            bobPhase = Random.Range(0f, 10f);
            animTime = Random.Range(0f, 4f);

            shadow = SpriteUtil.Make("Shadow", transform, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.45f), Sorting.Shadow);
            shadow.transform.localPosition = new Vector3(0f, feetY + 0.04f, 0f);
            shadow.transform.localScale = new Vector3(radius * 2.6f, radius * 0.95f, 1f);

            bodyPivot = new GameObject("Body").transform;
            bodyPivot.SetParent(transform, false);
            bodyPivot.localPosition = new Vector3(0f, feetY, 0f);
            Sprite first = art != null ? art.idle[0] : ShapeLibrary.Get(Shape.Circle);
            body = SpriteUtil.Make("Sprite", bodyPivot, first, Color.white, 0);
            bodyGlow = SpriteUtil.Make("Glow", bodyPivot, art != null ? art.idleWhite[0] : first, Color.clear, 1);

            hurtbox = SpriteUtil.Make("DebugHurtbox", transform, ShapeLibrary.Get(Shape.Ring), new Color(0.3f, 1f, 0.4f, 0.8f), Sorting.Overlay + 50);
            hurtbox.transform.localScale = Vector3.one * radius * 2f;
            hurtbox.enabled = false;
        }

        /// <summary>Swaps the character drawn (party members each get their own samurai).</summary>
        protected void SetCharacterArt(CharacterArt characterArt)
        {
            if (characterArt == null) return;
            art = characterArt;
            body.sprite = art.idle[0];
            bodyGlow.sprite = art.idleWhite[0];
        }

        // ── State shared with browser players (read by the network snapshot) ───────────

        public ArtDir NetDir => dir;
        /// <summary>0 idle, 1 walk, 2 strike, 3 fallen.</summary>
        public int NetPose { get; private set; }
        public int NetFrame => currentFrame;
        public float NetWeaponAngle => FacingAngle + weaponAngle;
        public float NetWeaponReach => Radius * 0.55f + weaponExtend;
        public float NetWeaponLength => weaponLength;
        public bool NetWeaponVisible => weapon != null && weapon.enabled;
        public Color NetGlow => bodyGlow != null ? bodyGlow.color : Color.clear;
        public Color NetTint => body != null ? body.color : Color.white;
        public float NetScale => visualScale;
        public float NetFeetY => feetY;

        /// <summary>Top of the drawn character, relative to the entity's position (bars sit above it).</summary>
        protected float VisualTop => feetY + (art != null ? art.visibleHeight * visualScale : Radius * 2f);

        /// <summary>Body colour (multiplies the art) and the white overlay (flash / heat / glow).</summary>
        protected void SetBodyLook(Color tint, Color glow)
        {
            body.color = tint;
            bodyGlow.color = glow;
        }

        /// <summary>True while the character is reeling from a hit (held, flashing frame).</summary>
        protected virtual bool HurtPose => false;

        /// <summary>True while the character should show its strike pose.</summary>
        protected virtual bool StrikePose => false;

        /// <summary>True once the character has fallen (dead frame).</summary>
        protected virtual bool FallenPose => !IsAlive;

        /// <summary>Swaps the drawn weapon (the pose system keeps running).</summary>
        protected void SetWeapon(Sprite sprite, Color color, float length)
        {
            weapon.sprite = sprite;
            weaponColor = color;
            weaponLength = length;
            trail?.Clear();
        }

        protected void BuildWeapon(Sprite sprite, Color color, float length)
        {
            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
            weapon = SpriteUtil.Make("Weapon", weaponPivot, sprite, color, 2);
            weaponColor = color;
            weaponLength = length;
            trail = new BladeTrail(transform, weapon.sharedMaterial);
        }

        public void Teleport(Vector2 p)
        {
            Position = p;
            PrevPosition = p;
            Velocity = Vector2.zero;
            RenderPosition = p;
            transform.position = new Vector3(p.x, p.y, 0f);
            trail?.Clear();
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
            renderAlpha = Mathf.Clamp01(alpha);
            Vector2 p = Vector2.LerpUnclamped(PrevPosition, Position, alpha);
            RenderPosition = p;
            transform.position = new Vector3(p.x, p.y, 0f);

            // Top-down depth: lower on screen draws in front.
            int order = -Mathf.RoundToInt(p.y * 20f);
            body.sortingOrder = order;
            bodyGlow.sortingOrder = order + 1;

            // Breathing when idle, a light hop when moving: bodies never look frozen.
            float speed = IsAlive ? Velocity.magnitude : 0f;
            bobPhase += visualDt * (2.2f + speed * 2.2f);
            float breath = IsAlive ? 0.025f * Mathf.Sin(bobPhase) : 0f;
            float hop = Mathf.Abs(Mathf.Sin(bobPhase * 1.6f)) * 0.05f * Mathf.Clamp01(speed / 4f);
            bodyPivot.localPosition = new Vector3(0f, feetY + hop, 0f);

            AnimateSprite(speed, visualDt);

            // The torso tilts into the swing (follow-through), easing back after.
            lean = Mathf.LerpAngle(lean, BodyLean, CombatMath.Damp(LeanSharpness, visualDt));
            squash = Vector2.Lerp(squash, Vector2.one, CombatMath.Damp(12f, visualDt));
            float tilt = Mathf.Clamp(lean * LeanToTilt, -MaxTilt, MaxTilt);
            bodyPivot.localRotation = Quaternion.Euler(0f, 0f, tilt);
            float size = art != null ? visualScale : Radius * 2f;
            bodyPivot.localScale = new Vector3(size * squash.x * (1f + breath), size * squash.y * (1f - breath * 0.5f), 1f);
            if (flashTimer > 0f) flashTimer -= visualDt;

            if (weapon != null) RenderWeapon(order, hop, visualDt);

            hurtbox.enabled = showHitboxes && IsAlive;
            RenderExtra(p, visualDt);
        }

        /// <summary>
        /// Four-direction animation: walk loop (faster with speed), idle, strike pose or fallen
        /// frame, facing where the fighter looks (with a little hysteresis on the diagonals).
        /// </summary>
        void AnimateSprite(float speed, float visualDt)
        {
            if (art == null) return;
            ArtDir want = CharacterArt.DirOf(Facing);
            if (want != dir)
            {
                // Keep the current side until the turn is clear, so diagonals do not flicker.
                Vector2 f = Facing;
                bool clear = Mathf.Abs(Mathf.Abs(f.x) - Mathf.Abs(f.y)) > 0.15f;
                if (clear) dir = want;
            }
            int d = (int)dir;

            if (FallenPose && art.dead != null)
            {
                NetPose = 3;
                body.sprite = art.dead;
                bodyGlow.sprite = art.deadWhite;
                return;
            }
            if (StrikePose)
            {
                NetPose = 2;
                body.sprite = art.attack[d];
                bodyGlow.sprite = art.attackWhite[d];
                return;
            }

            bool moving = speed > 0.6f && !HurtPose;
            if (moving != wasMoving)
            {
                wasMoving = moving;
                animTime = moving ? 1f : 0f;
            }
            if (!moving)
            {
                NetPose = 0;
                body.sprite = art.idle[d];
                bodyGlow.sprite = art.idleWhite[d];
                return;
            }
            animTime += visualDt * (6f + speed * 1.5f);
            Sprite[] frames = art.walk[d];
            int index = Mathf.FloorToInt(animTime) % frames.Length;
            if (index != currentFrame && (index == 0 || index == 2)) OnFootfall();
            currentFrame = index;
            NetPose = 1;
            body.sprite = frames[index];
            bodyGlow.sprite = art.walkWhite[d][index];
        }

        /// <summary>A foot touches the ground (run frames 0 and 2).</summary>
        protected virtual void OnFootfall()
        {
        }

        void RenderWeapon(int order, float hop, float visualDt)
        {
            WeaponPose pose = GetWeaponPose();
            float target = pose.angle;
            if (!pose.snap)
            {
                // Idle blades lag behind movement a little, like something with weight.
                Vector2 side = CombatMath.Perpendicular(Facing);
                target -= Mathf.Clamp(Vector2.Dot(side, Velocity) * 4f, -25f, 25f);
            }

            if (visualDt > 0f)
            {
                float k = CombatMath.Damp(pose.snap ? SnapSharpness : SoftSharpness, visualDt);
                // Follow the shortest way to the target, but keep the angle unwrapped so spins stay spins.
                weaponAngle += Mathf.DeltaAngle(weaponAngle, target) * k;
                weaponExtend = Mathf.Lerp(weaponExtend, pose.extend, k);
            }

            float worldAngle = FacingAngle + weaponAngle;
            weaponPivot.localPosition = new Vector3(0f, hop, 0f);
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, worldAngle);
            weapon.transform.localPosition = new Vector3(Radius * 0.55f + weaponExtend, 0f, 0f);
            weapon.transform.localScale = new Vector3(weaponLength, weaponLength, 1f);
            weapon.color = pose.tint;
            // Pointing "up" the screen it passes behind the body, otherwise in front. Blades on
            // the left half flip so their edge and guard never render upside down.
            weapon.sortingOrder = Mathf.Sin(worldAngle * Mathf.Deg2Rad) > 0.2f ? order - 1 : order + 2;
            weapon.flipY = Mathf.Cos(worldAngle * Mathf.Deg2Rad) < 0f;

            float reach = Radius * 0.55f + weaponExtend;
            var centre = new Vector2(transform.position.x, transform.position.y + hop);
            trail.Update(TrailEmitting && weapon.enabled, centre, worldAngle, reach + weaponLength * 0.45f, reach + weaponLength * 1.02f, TrailTint, visualDt);
        }

        protected virtual WeaponPose GetWeaponPose()
        {
            return new WeaponPose { angle = -50f, tint = weaponColor };
        }

        /// <summary>Extra torso rotation (degrees) for the current frame: leaning into a swing.</summary>
        protected virtual float BodyLean => 0f;

        /// <summary>True while the blade should paint its trail (the active part of a swing).</summary>
        protected virtual bool TrailEmitting => false;

        protected virtual Color TrailTint => Color.white;

        protected abstract void RenderExtra(Vector2 renderPosition, float visualDt);

        /// <summary>
        /// A ribbon traced by the blade between grip and tip, built from the last ~0.12 s of
        /// rendered angles. Between samples the angle is interpolated around the body, so even
        /// a 4-frame swing draws a smooth crescent instead of a polygon. Ages only with visual
        /// time: the trail freezes with the hit-stop.
        /// </summary>
        sealed class BladeTrail
        {
            struct Sample
            {
                public Vector2 centre;
                public float angle;
                public float rBase;
                public float rTip;
                public float age;
            }

            const int MaxSamples = 40;
            const int Subdivisions = 5;
            const float Lifetime = 0.13f;

            readonly List<Sample> samples = new List<Sample>(MaxSamples);
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> tris = new List<int>();
            readonly Mesh mesh;
            readonly Transform transform;

            public BladeTrail(Transform parent, Material material)
            {
                var go = new GameObject("BladeTrail");
                transform = go.transform;
                transform.SetParent(parent, false);
                mesh = new Mesh { name = "BladeTrail" };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = Sorting.Overlay - 6;
            }

            public void Clear()
            {
                samples.Clear();
                mesh.Clear();
            }

            public void Update(bool emitting, Vector2 centre, float angle, float rBase, float rTip, Color color, float dt)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    Sample s = samples[i];
                    s.age += dt;
                    samples[i] = s;
                }
                while (samples.Count > 0 && samples[0].age > Lifetime) samples.RemoveAt(0);

                if (emitting)
                {
                    if (samples.Count >= MaxSamples) samples.RemoveAt(0);
                    samples.Add(new Sample { centre = centre, angle = angle, rBase = rBase, rTip = rTip });
                }

                // Vertices are in world space; the holder sits at the origin.
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                Build(color);
            }

            void Build(Color color)
            {
                verts.Clear();
                colors.Clear();
                tris.Clear();
                if (samples.Count < 2)
                {
                    mesh.Clear();
                    return;
                }

                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Sample a = samples[i];
                    Sample b = samples[i + 1];
                    float delta = Mathf.DeltaAngle(a.angle, b.angle);
                    int steps = i == samples.Count - 2 ? Subdivisions + 1 : Subdivisions;
                    for (int s = 0; s < steps; s++)
                    {
                        float t = s / (float)Subdivisions;
                        AddPair(Vector2.Lerp(a.centre, b.centre, t), a.angle + delta * t,
                                Mathf.Lerp(a.rBase, b.rBase, t), Mathf.Lerp(a.rTip, b.rTip, t), Mathf.Lerp(a.age, b.age, t), color);
                    }
                }

                for (int v = 0; v + 3 < verts.Count; v += 2)
                {
                    tris.Add(v);
                    tris.Add(v + 1);
                    tris.Add(v + 2);
                    tris.Add(v + 1);
                    tris.Add(v + 3);
                    tris.Add(v + 2);
                }

                mesh.Clear();
                mesh.SetVertices(verts);
                mesh.SetColors(colors);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
            }

            void AddPair(Vector2 centre, float angleDeg, float rBase, float rTip, float age, Color color)
            {
                float rad = angleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float life = 1f - Mathf.Clamp01(age / Lifetime);
                float fade = life * life;
                // Hot white at the newest edge, the weapon's colour as it fades.
                Color c = Color.Lerp(color, Color.white, life * 0.6f);
                // Crescent, not a pie slice: the inner edge closes toward the tip as the streak ages,
                // so the trail tapers into a thin comet tail behind the blade.
                float inner = Mathf.Lerp(rTip, rBase, life);
                verts.Add(centre + dir * inner);
                colors.Add(c.WithAlpha(0f));
                verts.Add(centre + dir * rTip);
                colors.Add(c.WithAlpha(color.a * fade * 0.9f));
            }
        }
    }
}
