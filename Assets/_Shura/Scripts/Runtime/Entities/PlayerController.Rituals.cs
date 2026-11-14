using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The rituals of the blade (design doc §8.5). Chiburi and Nōtō: after a room is cleared,
    /// holding Attack, standing still, shakes the blood off the blade and sheathes it; letting
    /// go on the exact click of the scabbard is worth 5 Ecos and a signature sound.
    /// </summary>
    public sealed partial class PlayerController
    {
        public const int ChiburiStart = 16;
        public const int NotoClick = 58;
        public const int NotoWindow = 6;

        int chiburiFrames;
        bool chiburiDone;

        /// <summary>Shaking the blood off or sheathing.</summary>
        public bool InChiburi => chiburiFrames >= ChiburiStart && !chiburiDone;
        /// <summary>Holding for the ritual (the room waits for it to end).</summary>
        public bool ChiburiHolding => chiburiFrames > 0 && !chiburiDone;

        void ResetRituals()
        {
            chiburiFrames = 0;
            chiburiDone = false;
        }

        void TickRituals()
        {
            TickQuietAndSeal();
            if (Build == null || !IsAlive || input == null) return;
            bool calm = world.RoomCleared && world.ActiveEnemyCount == 0 && !world.TutorialRoom;
            if (!calm || chiburiDone)
            {
                chiburiFrames = 0;
                return;
            }
            bool still = CurrentState == State.Free && Velocity.sqrMagnitude < 0.6f;
            if (input.AttackHeld && still)
            {
                chiburiFrames++;
                if (chiburiFrames == ChiburiStart)
                {
                    // Chiburi: one sharp flick, and the blood leaves the steel.
                    world.Fx.Popup(Strings.PopupChiburi, Position + Vector2.up * 1.3f, pal.uiText);
                    world.Fx.Blood(Position + Facing * 0.6f, Facing, 14f, ColorUtil.Hex("#8E0F1E"));
                    world.Fx.Emit(FxType.PlayerSwing, Position, Facing, 1.4f, Color.white.WithAlpha(0.6f));
                    Squash(0.9f, 1.1f);
                }
                if (chiburiFrames == NotoClick - 20) world.Fx.Popup(Strings.PopupNotoSlow, Position + Vector2.up * 1.3f, pal.uiDim);
                if (chiburiFrames == NotoClick) world.Fx.Emit(FxType.Kamae, Position, Facing, 1f, pal.gold.WithAlpha(0.8f));
                if (chiburiFrames > NotoClick + NotoWindow + 24)
                {
                    chiburiDone = true;
                    world.Fx.Popup(Strings.PopupNotoLate, Position + Vector2.up * 1.3f, pal.uiDim);
                }
                return;
            }
            if (chiburiFrames >= ChiburiStart)
            {
                chiburiDone = true;
                int off = chiburiFrames - NotoClick;
                if (Mathf.Abs(off) <= NotoWindow)
                {
                    // Nōtō: the guard meets the mouth of the scabbard with a single click.
                    run.BonusEcos += 5;
                    world.Fx.Popup(Strings.PopupNoto, Position + Vector2.up * 1.5f, pal.gold);
                    world.Fx.Emit(FxType.StreakMilestone, Position, Vector2.up, 5f, pal.gold);
                    Squash(1.08f, 0.94f);
                    run.Meta?.AddStat(TeaGuests.StatNoto, 1);
                }
                else world.Fx.Popup(off < 0 ? Strings.PopupNotoEarly : Strings.PopupNotoLate, Position + Vector2.up * 1.3f, pal.uiDim);
            }
            chiburiFrames = 0;
        }

        int quietFrames;
        int sealedFrames;

        /// <summary>Standing still, blade at rest: the blind duelist loses you.</summary>
        public bool Quiet => quietFrames > 24;

        void TickQuietAndSeal()
        {
            bool still = Velocity.sqrMagnitude < 0.3f && (CurrentState == State.Free || CurrentState == State.Parry);
            quietFrames = still ? quietFrames + 1 : 0;
            if (sealedFrames > 0 && --sealedFrames == 0 && Build != null)
            {
                Build.TechniquesSealed = false;
                Build.RecomputeModifiers();
                ApplyStats(false);
                world.Fx.Popup(Strings.PopupTechniquesFree, Position + Vector2.up * 1.3f, pal.gold);
            }
        }

        /// <summary>An Exorcist's parry: the talisman burns away at the next moment.</summary>
        public void ReleaseSeal()
        {
            if (sealedFrames > 1) sealedFrames = 1;
        }

        /// <summary>The corrupted priest's talisman: no technique works for a while.</summary>
        public void SealTechniques(int frames)
        {
            if (Build == null || !IsAlive) return;
            bool was = sealedFrames > 0;
            sealedFrames = Mathf.Max(sealedFrames, frames);
            if (was) return;
            Build.TechniquesSealed = true;
            Build.RecomputeModifiers();
            ApplyStats(false);
            world.Fx.Popup(Strings.PopupTechniquesSealed, Position + Vector2.up * 1.4f, ColorUtil.Hex("#B08AE0"));
            world.HintOnce(HintId.Factions);
        }

        /// <summary>Pushed by a living wall.</summary>
        public void KnockBack(Vector2 dir, float distance)
        {
            if (!IsAlive || dir.sqrMagnitude < 1e-4f) return;
            Velocity = dir.normalized * distance * 6f;
        }

        /// <summary>A bow before the standoff (Interact).</summary>
        public void Bow()
        {
            Squash(1.15f, 0.8f);
            Velocity = Vector2.zero;
        }
    }
}
