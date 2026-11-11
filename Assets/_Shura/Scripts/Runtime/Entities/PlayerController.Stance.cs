using UnityEngine;

namespace Shura
{
    /// <summary>
    /// What a guard (kamae) looks like. Angles are relative to where the duelist faces
    /// (0 = blade pointing at the enemy, positive = towards the left hand, ±180 = behind).
    ///
    ///   Jōdan  — the blade waits high over the shoulder and falls in long diagonal cuts
    ///            (kesa-giri); thrusts stab downward; the duelist steps deep into every blow.
    ///   Chūdan — the tip points at the enemy's throat and draws a small circle; level cuts
    ///            from side to side; straight thrusts.
    ///   Gedan  — the blade hangs low by the knee and rises (kiri-age): every cut travels the
    ///            other way, from below; short steps, planted feet.
    ///   Waki   — the blade is hidden behind the body, half unseen; cuts are drawn from the
    ///            back in a wide whip, like an iai draw.
    ///
    /// Only what is seen (and how far the duelist steps) changes here: reach, damage and the
    /// frame data of every weapon stay as they are, so no guard is a hidden nerf.
    /// </summary>
    public sealed partial class PlayerController
    {
        const int StanceFlourishFrames = 16;

        Kamae previousStance = Kamae.Chudan;

        /// <summary>
        /// Where the blade rests in a guard, how far it is held out, and how visible it is.
        /// High and low are read on the screen, whichever way the duelist faces: in Jōdan the
        /// blade stands above the head (leaning back), in Gedan it points at the ground ahead.
        /// </summary>
        void StanceRest(Kamae k, float time, out float angle, out float extend, out float alpha)
        {
            alpha = 1f;
            float facing = FacingAngle;
            switch (k)
            {
                case Kamae.Jodan:
                    // High: raised over the head, breathing slowly.
                    angle = Mathf.DeltaAngle(facing, 90f + 32f * Facing.x) + 4f * Mathf.Sin(time * 1.3f);
                    extend = 0.1f + 0.03f * Mathf.Sin(time * 1.3f + 1f);
                    break;
                case Kamae.Gedan:
                    // Low: the tip towards the ground in front, swaying.
                    angle = Mathf.DeltaAngle(facing, -90f + 42f * Facing.x) + 5f * Mathf.Sin(time * 1.6f);
                    extend = 0.04f;
                    break;
                case Kamae.Waki:
                    // Hidden: behind the hip, barely seen.
                    angle = 166f + 2.5f * Mathf.Sin(time * 1.1f);
                    extend = -0.28f;
                    alpha = 0.5f;
                    break;
                default:
                    // Middle: the tip on the enemy, tracing a small circle.
                    angle = -6f + 3.5f * Mathf.Sin(time * 2.1f);
                    extend = 0.12f + 0.035f * Mathf.Cos(time * 2.1f);
                    break;
            }
        }

        /// <summary>The resting blade angle of the guard held now (no sway).</summary>
        float RestNow
        {
            get
            {
                StanceRest(Stance, 0f, out float angle, out _, out _);
                return angle;
            }
        }

        /// <summary>
        /// Start and end of a cut in the guard held now. A plain combo sweep is redrawn by the
        /// guard; swap strikes, Links, spins and slams keep the weapon's own shape.
        /// </summary>
        void SwingAngles(PlayerAttackDef step, out float from, out float to)
        {
            from = step.fromAngle;
            to = step.toAngle;
            if (CurrentState != State.Attack) return;
            float side = to >= from ? 1f : -1f;
            if (step.style == SwingStyle.Slam)
            {
                switch (Stance)
                {
                    case Kamae.Jodan:
                        from = 172f;
                        to = -12f;
                        break;
                    case Kamae.Gedan:
                        // The rising slam: from the ground up and over.
                        from = -150f;
                        to = 10f;
                        break;
                    case Kamae.Waki:
                        from = 185f;
                        to = -5f;
                        break;
                }
                return;
            }
            if (step.style != SwingStyle.Sweep) return;
            float span = Mathf.Abs(to - from);
            switch (Stance)
            {
                case Kamae.Jodan:
                    // From high behind the shoulder, down through the enemy and a little past.
                    from = -side * Mathf.Max(158f, span * 0.5f + 70f);
                    to = side * 42f;
                    break;
                case Kamae.Gedan:
                    // The other way: from low at the side, rising across.
                    from = side * Mathf.Max(108f, span * 0.5f + 20f);
                    to = -side * 52f;
                    break;
                case Kamae.Waki:
                    // Drawn from behind in a wide whip.
                    from = -side * 176f;
                    to = side * Mathf.Max(76f, span * 0.5f);
                    break;
            }
        }

        /// <summary>Where a thrust or jab comes from in the guard held now (it always ends straight ahead).</summary>
        float StanceThrustStart(PlayerAttackDef step)
        {
            if (CurrentState != State.Attack) return 0f;
            float k = step.style == SwingStyle.Jab ? 0.5f : 1f;
            switch (Stance)
            {
                case Kamae.Jodan: return 46f * k;
                case Kamae.Gedan: return -46f * k;
                case Kamae.Waki: return 150f * k;
                default: return 0f;
            }
        }

        /// <summary>How deep the duelist steps into a blow.</summary>
        float StanceLungeMult
        {
            get
            {
                switch (Stance)
                {
                    case Kamae.Jodan: return 1.3f;
                    case Kamae.Gedan: return 0.75f;
                    case Kamae.Waki: return 1.15f;
                    default: return 1f;
                }
            }
        }

        /// <summary>The body's set in each guard: upright and back, square, crouched forward, side-on.</summary>
        float StanceLean
        {
            get
            {
                switch (Stance)
                {
                    case Kamae.Jodan: return -6f;
                    case Kamae.Gedan: return 8f;
                    case Kamae.Waki: return 12f;
                    default: return 0f;
                }
            }
        }

        /// <summary>Where the blade meets a blow in each guard (first the perfect frames, then the hold).</summary>
        void StanceParry(bool perfect, out float angle, out float extend)
        {
            switch (Stance)
            {
                case Kamae.Jodan:
                    angle = perfect ? 104f : 88f;
                    extend = 0.02f;
                    break;
                case Kamae.Gedan:
                    angle = perfect ? 42f : 30f;
                    extend = -0.04f;
                    break;
                case Kamae.Waki:
                    // The hidden blade whips round to the front.
                    angle = perfect ? 60f : 52f;
                    extend = 0.06f;
                    break;
                default:
                    angle = perfect ? 78f : 64f;
                    extend = -0.1f;
                    break;
            }
        }

        /// <summary>
        /// The idle blade: the guard's pose, and for a third of a second after changing guard
        /// a flourish — the blade turns once on its way from the old pose to the new one and
        /// flashes as it arrives.
        /// </summary>
        void StanceIdlePose(ref WeaponPose pose)
        {
            StanceRest(Stance, Time.unscaledTime, out float angle, out float extend, out float alpha);
            int since = KamaeCooldown - kamaeCooldown;
            if (kamaeCooldown > 0 && since < StanceFlourishFrames)
            {
                float k = CombatMath.EaseOutCubic(Mathf.Clamp01(AnimFrame(since + 1) / StanceFlourishFrames));
                StanceRest(previousStance, 0f, out float fromAngle, out float fromExtend, out float fromAlpha);
                // One full turn, in the direction that passes in front of the body.
                float travel = Mathf.DeltaAngle(fromAngle, angle);
                travel += travel >= 0f ? 360f : -360f;
                angle = fromAngle + travel * k;
                extend = Mathf.Lerp(fromExtend, extend, k) + 0.18f * Mathf.Sin(k * Mathf.PI);
                alpha = Mathf.Lerp(fromAlpha, alpha, k);
                pose.snap = true;
                pose.tint = Color.Lerp(weaponColor, Color.white, 0.7f * Mathf.Sin(k * Mathf.PI));
            }
            pose.angle = angle;
            pose.extend = extend;
            pose.tint = pose.tint.WithAlpha(pose.tint.a * alpha);
        }
    }
}
