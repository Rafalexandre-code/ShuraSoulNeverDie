using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The lion gauntlets fight like a boxer: one fist guards the face while the other snaps out
    /// (chambered, thrown with the hips, overshooting a hair, pulled back to the guard), the lead
    /// and the rear hand take turns, the hook swings round, and the finisher is a flurry that ends
    /// in a straight right that roars.
    /// </summary>
    public sealed partial class PlayerController
    {
        const float GuardAngle = 30f;

        /// <summary>The off hand's own pose this frame (angle from facing, extension).</summary>
        float offAngle = -GuardAngle, offExtend = -0.1f;
        bool offOwnPose;

        bool Boxing => Weapon.boxer;

        /// <summary>Which fist throws this blow: the lead (main) hand on even steps, the rear on odd ones.</summary>
        bool LeadThrows(PlayerAttackDef step) => CurrentState == State.SwapStrike || ComboIndex % 2 == 0 || step.style != SwingStyle.Jab;

        /// <summary>A punch, frame by frame: chamber, snap with the hips, overshoot, back to the guard.</summary>
        static void PunchCurve(float f, PlayerAttackDef step, int recovery, float guard, float reach, out float angle, out float extend)
        {
            float tW = Mathf.Clamp01(f / Mathf.Max(1, step.windup));
            float tA = Mathf.Clamp01((f - step.windup) / Mathf.Max(1, step.active));
            float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, recovery));
            float chamber = guard + Mathf.Sign(guard) * 14f;
            // The fist crosses a little toward the centre line as it lands.
            float land = -Mathf.Sign(guard) * 4f;
            if (f < step.windup)
            {
                angle = Mathf.Lerp(guard, chamber, CombatMath.EaseInOutSine(tW));
                extend = Mathf.Lerp(-0.1f, -0.38f, CombatMath.EaseInOutSine(tW));
            }
            else if (f < step.windup + step.active)
            {
                angle = Mathf.Lerp(chamber, land, CombatMath.EaseOutCubic(tA));
                extend = Mathf.LerpUnclamped(-0.38f, reach, CombatMath.EaseOutBack(tA));
            }
            else
            {
                float back = CombatMath.EaseInOutSine(tR);
                angle = Mathf.Lerp(land, guard, back);
                extend = Mathf.Lerp(reach, -0.1f, back);
            }
        }

        /// <summary>The boxer's attack pose: sets the main hand on <paramref name="pose"/> and the off hand aside.</summary>
        void BoxerAttackPose(ref WeaponPose pose)
        {
            PlayerAttackDef step = CurrentStep;
            float f = AnimFrame(StateFrame);
            int rec = RecoveryFrames(step);
            float bob = 3f * Mathf.Sin(Time.time * 9f);
            offOwnPose = true;
            if (step.style == SwingStyle.Sweep)
            {
                // The hook: the lead fist swings round wide and level; the rear hand stays up.
                pose.angle = StrikeAngle();
                pose.extend = Mathf.Max(0.05f, StrikeExtend());
                offAngle = -GuardAngle + bob;
                offExtend = -0.12f;
                return;
            }
            float reach = step.extend + 0.12f;
            if (LeadThrows(step))
            {
                PunchCurve(f, step, rec, GuardAngle, reach, out pose.angle, out pose.extend);
                offAngle = -GuardAngle - bob;
                offExtend = -0.14f;
            }
            else
            {
                PunchCurve(f, step, rec, -GuardAngle, reach, out offAngle, out offExtend);
                pose.angle = GuardAngle + bob;
                pose.extend = -0.14f;
            }
        }

        /// <summary>Hips and shoulders turn into the punch (the lead and the rear the opposite way).</summary>
        float BoxerLean(PlayerAttackDef step)
        {
            float f = AnimFrame(StateFrame);
            float side = LeadThrows(step) ? 1f : -1f;
            if (step.style == SwingStyle.Sweep) side = 1.4f;
            float tA = Mathf.Clamp01((f - step.windup) / Mathf.Max(1, step.active));
            float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step)));
            if (f < step.windup) return side * 9f * CombatMath.EaseInOutSine(f / Mathf.Max(1, step.windup));
            if (f < step.windup + step.active) return Mathf.Lerp(9f, -16f, CombatMath.EaseOutCubic(tA)) * side;
            return -16f * side * (1f - CombatMath.EaseInOutSine(tR));
        }

        /// <summary>The instant the fist lands: a flash of the lion's colour and a squash of the body.</summary>
        void BoxerImpactFrame(PlayerAttackDef step, bool swapStrike)
        {
            if (!Boxing || StateFrame != step.windup + 1) return;
            bool lead = LeadThrows(step);
            float side = lead ? 1f : -1f;
            Vector2 fist = Position + (Vector2)(Quaternion.Euler(0f, 0f, side * -4f) * Facing) * (Radius + step.extend + 0.3f);
            world.Fx.Emit(FxType.Burst, fist, Facing, swapStrike ? 1.4f : 0.55f, WeaponMastery.BladeColor(Weapon));
            Squash(0.82f, 1.22f);
        }

        // ── The finisher: Coração Partido ───────────────────────────────────────────────
        // 0–31: a flurry of eight body blows, the fists taking turns; 32–41: the right hand
        // is drawn back, the world slows; 42: the straight lands with the lion's roar.

        const int FlurryEnd = 32;
        const int RoarFrame = 42;
        public const int HeartBreakLength = 66;

        void TickHeartBreak(Enemy e, int f)
        {
            if (f < FlurryEnd && f % 4 == 1)
            {
                finisherAnchor += finisherDir * 0.04f;
                Impact(e, 0.35f + 0.05f * (f / 4));
                world.Fx.Emit(FxType.Afterimage, Position, Facing, Radius * 1.2f, WeaponMastery.BladeColor(Weapon));
            }
            if (f == FlurryEnd + 1) world.RequestSlowMo(0.35f);
            e.HoldAt(finisherAnchor, 1f);
            if (f != RoarFrame) return;
            Impact(e, 1.4f);
            Color lion = ColorUtil.Hex("#E8B448");
            world.Fx.Emit(FxType.SlamImpact, e.Position, finisherDir, 2.4f, lion);
            world.Fx.Emit(FxType.Burst, e.Position, finisherDir, 3f, lion);
            world.Fx.Popup(Strings.PopupLionRoar, e.Position + Vector2.up * 1.6f, lion);
            Kill(e);
        }

        void HeartBreakPose(ref WeaponPose pose)
        {
            float f = AnimFrame(StateFrame);
            offOwnPose = true;
            if (f < FlurryEnd)
            {
                // Alternating body blows, short and fast.
                float phase = f % 8f;
                bool lead = f % 8f < 4f;
                float t = Mathf.Sin(Mathf.Clamp01((phase % 4f) / 4f) * Mathf.PI);
                if (lead)
                {
                    pose.angle = Mathf.Lerp(GuardAngle, 2f, t);
                    pose.extend = Mathf.Lerp(-0.1f, 0.42f, t);
                    offAngle = -GuardAngle;
                    offExtend = -0.12f;
                }
                else
                {
                    offAngle = Mathf.Lerp(-GuardAngle, -2f, t);
                    offExtend = Mathf.Lerp(-0.1f, 0.42f, t);
                    pose.angle = GuardAngle;
                    pose.extend = -0.12f;
                }
                return;
            }
            if (f < RoarFrame)
            {
                // The rear hand draws all the way back; the lead covers.
                float t = CombatMath.EaseInOutSine((f - FlurryEnd) / (float)(RoarFrame - FlurryEnd));
                offAngle = Mathf.Lerp(-GuardAngle, -62f, t);
                offExtend = Mathf.Lerp(-0.1f, -0.42f, t);
                pose.angle = GuardAngle - 6f * t;
                pose.extend = -0.05f;
                return;
            }
            // The straight: all the way through, held for a beat, then the guard.
            float hold = Mathf.Clamp01((f - RoarFrame) / 14f);
            offAngle = Mathf.Lerp(-62f, -2f, Mathf.Clamp01((f - RoarFrame + 2) / 3f));
            offExtend = hold < 1f ? 0.95f : Mathf.Lerp(0.95f, -0.1f, Mathf.Clamp01((f - RoarFrame - 14) / 8f));
            pose.angle = GuardAngle;
            pose.extend = -0.12f;
        }

        float HeartBreakLean()
        {
            float f = AnimFrame(StateFrame);
            if (f < FlurryEnd) return 8f * Mathf.Sin(f * Mathf.PI / 4f);
            if (f < RoarFrame) return 14f * CombatMath.EaseInOutSine((f - FlurryEnd) / (float)(RoarFrame - FlurryEnd));
            return Mathf.Lerp(-22f, 0f, Mathf.Clamp01((f - RoarFrame) / 18f));
        }
    }
}
