using UnityEngine;

namespace Shura
{
    /// <summary>The souls-like target lock of the duelist played on this machine (see <see cref="TargetLock"/>).</summary>
    public sealed partial class PlayerController
    {
        public readonly TargetLock Lock = new TargetLock();
        /// <summary>The enemy held by the lock (null = free aim).</summary>
        public Enemy LockTarget { get; private set; }

        int seenLockToggles = -1, seenFlicks = -1, seenNext = -1, seenPrev = -1;

        /// <summary>Automated screenshots: the bot holds the lock on whoever is near.</summary>
        public bool DevForceLock { get; set; }

        void TickLock()
        {
            if (!(input is InputService local))
            {
                if (DevForceLock) TickDevLock();
                else LockTarget = null;
                return;
            }
            if (seenLockToggles < 0)
            {
                seenLockToggles = local.LockToggles;
                seenFlicks = local.LockFlicks;
                seenNext = local.LockCycleNext;
                seenPrev = local.LockCyclePrev;
            }

            Lock.Candidates.Clear();
            foreach (Enemy e in world.Enemies)
            {
                if (!e.CanBeHit || e.IsAlly || e.IsGone) continue;
                Lock.Candidates.Add(new LockCandidate { id = e.NetUid, pos = e.Position });
            }

            bool pressed = local.LockToggles != seenLockToggles;
            seenLockToggles = local.LockToggles;
            int before = Lock.TargetId;
            Vector2 look = local.Move.sqrMagnitude > 0.1f ? local.Move.normalized : Facing;
            Lock.ApplyPolicy(local.UsingGamepad, local.MouseLockAllowed, pressed, Position, look);
            if (Lock.TargetId != before) world.Fx.Emit(FxType.LockOn, Position, Facing, Lock.Active ? 1f : 0f, pal.danger);
            else if (pressed && !Lock.Active && Lock.Candidates.Count == 0 && (local.UsingGamepad || local.MouseLockAllowed))
                world.Fx.Popup(Strings.PopupNoTarget, Position + Vector2.up, pal.uiDim);
            if (local.LockFlicks != seenFlicks)
            {
                seenFlicks = local.LockFlicks;
                if (Lock.Active && Lock.Switch(Position, local.LastFlick)) world.Fx.Emit(FxType.LockOn, Position, Facing, 1f, pal.danger);
            }
            if (local.LockCycleNext != seenNext)
            {
                seenNext = local.LockCycleNext;
                if (Lock.Cycle(Position, 1)) world.Fx.Emit(FxType.LockOn, Position, Facing, 1f, pal.danger);
            }
            if (local.LockCyclePrev != seenPrev)
            {
                seenPrev = local.LockCyclePrev;
                if (Lock.Cycle(Position, -1)) world.Fx.Emit(FxType.LockOn, Position, Facing, 1f, pal.danger);
            }

            Lock.Refresh(Position);
            LockTarget = null;
            if (Lock.Active)
            {
                foreach (Enemy e in world.Enemies)
                {
                    if (e.NetUid != Lock.TargetId) continue;
                    LockTarget = e;
                    break;
                }
            }
            local.LockEngaged = LockTarget != null;
        }

        void TickDevLock()
        {
            Lock.Candidates.Clear();
            foreach (Enemy e in world.Enemies)
            {
                if (e.CanBeHit && !e.IsAlly && !e.IsGone) Lock.Candidates.Add(new LockCandidate { id = e.NetUid, pos = e.Position });
            }
            if (!Lock.Active) Lock.Acquire(Position, Facing);
            Lock.Refresh(Position);
            LockTarget = null;
            foreach (Enemy e in world.Enemies)
            {
                if (Lock.Active && e.NetUid == Lock.TargetId) LockTarget = e;
            }
        }

        /// <summary>Locked on, the blade always faces the target.</summary>
        bool LockedAim(out Vector2 aim)
        {
            aim = Vector2.zero;
            if (LockTarget == null || LockTarget.IsGone) return false;
            Vector2 d = LockTarget.Position - Position;
            if (d.sqrMagnitude < 1e-4f) return false;
            aim = d.normalized;
            return true;
        }

        void ResetLockForRoom()
        {
            Lock.Release();
            LockTarget = null;
            if (input is InputService local) local.LockEngaged = false;
        }
    }
}
