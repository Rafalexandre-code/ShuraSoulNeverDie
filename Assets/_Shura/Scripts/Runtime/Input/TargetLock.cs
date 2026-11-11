using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Someone the lock can hold: an id that survives between frames and where it stands.</summary>
    public struct LockCandidate
    {
        public int id;
        public Vector2 pos;
    }

    /// <summary>
    /// Souls-like target lock. One press holds the enemy in front of you; a flick of the right
    /// stick (or the mouse wheel) jumps to the next one in that direction; when the target falls,
    /// the lock moves to the nearest enemy; too far away, it lets go. The same logic serves the
    /// host's duelist (live enemies) and an online guest (the host's snapshot).
    /// </summary>
    public sealed class TargetLock
    {
        public const float AcquireRange = 11f;
        public const float BreakRange = 15f;
        public const float AutoNextRange = 9f;
        const float FlickMaxAngle = 70f;

        public readonly List<LockCandidate> Candidates = new List<LockCandidate>();
        public int TargetId { get; private set; } = -1;
        public Vector2 TargetPos { get; private set; }
        public bool Active => TargetId >= 0;

        /// <summary>Lock or let go. Returns true when a target was taken.</summary>
        public bool Toggle(Vector2 from, Vector2 facing)
        {
            if (Active)
            {
                Release();
                return false;
            }
            return Acquire(from, facing);
        }

        /// <summary>The enemy most in front of you, weighed by distance.</summary>
        public bool Acquire(Vector2 from, Vector2 facing)
        {
            if (facing.sqrMagnitude < 1e-4f) facing = Vector2.right;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                Vector2 to = Candidates[i].pos - from;
                float d = to.magnitude;
                if (d > AcquireRange) continue;
                float angle = d > 1e-3f ? Vector2.Angle(facing, to) : 0f;
                float score = d * (1f + angle / 60f);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            if (best < 0) return false;
            TargetId = Candidates[best].id;
            TargetPos = Candidates[best].pos;
            return true;
        }

        /// <summary>A flick toward a direction (screen = world here): the nearest enemy that way from the current one.</summary>
        public bool Switch(Vector2 from, Vector2 direction)
        {
            if (!Active || direction.sqrMagnitude < 1e-4f) return false;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                LockCandidate c = Candidates[i];
                if (c.id == TargetId || Vector2.Distance(c.pos, from) > BreakRange) continue;
                Vector2 v = c.pos - TargetPos;
                if (v.sqrMagnitude < 1e-4f) continue;
                float angle = Vector2.Angle(direction, v);
                if (angle > FlickMaxAngle) continue;
                float score = v.magnitude * (1f + angle / 35f);
                if (score >= bestScore) continue;
                bestScore = score;
                best = i;
            }
            if (best < 0) return false;
            TargetId = Candidates[best].id;
            TargetPos = Candidates[best].pos;
            return true;
        }

        /// <summary>Next enemy around you, clockwise (+1) or counter-clockwise (-1): the mouse wheel.</summary>
        public bool Cycle(Vector2 from, int step)
        {
            if (!Active) return false;
            float current = CombatMath.AngleDegrees(TargetPos - from);
            int best = -1;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                LockCandidate c = Candidates[i];
                if (c.id == TargetId || Vector2.Distance(c.pos, from) > BreakRange) continue;
                float a = CombatMath.AngleDegrees(c.pos - from);
                // Clockwise = decreasing angle on screen.
                float delta = Mathf.Repeat(step > 0 ? current - a : a - current, 360f);
                if (delta < 0.5f) delta += 360f;
                if (delta >= bestDelta) continue;
                bestDelta = delta;
                best = i;
            }
            if (best < 0) return false;
            TargetId = Candidates[best].id;
            TargetPos = Candidates[best].pos;
            return true;
        }

        /// <summary>
        /// Keeps the lock honest each frame. Returns 0 if nothing changed, 1 if it moved to another
        /// enemy (the old one fell), -1 if it let go.
        /// </summary>
        public int Refresh(Vector2 from)
        {
            if (!Active) return 0;
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (Candidates[i].id != TargetId) continue;
                TargetPos = Candidates[i].pos;
                if (Vector2.Distance(TargetPos, from) <= BreakRange) return 0;
                Release();
                return -1;
            }
            // The target is gone: the nearest enemy close to you takes its place.
            Vector2 last = TargetPos;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                float d = Vector2.Distance(Candidates[i].pos, from);
                if (d > AutoNextRange) continue;
                d += 0.35f * Vector2.Distance(Candidates[i].pos, last);
                if (d >= bestD) continue;
                bestD = d;
                best = i;
            }
            if (best < 0)
            {
                Release();
                return -1;
            }
            TargetId = Candidates[best].id;
            TargetPos = Candidates[best].pos;
            return 1;
        }

        public void Release() => TargetId = -1;

        /// <summary>
        /// The house rule. On a gamepad the lock is always on: it takes the best enemy by itself,
        /// and the lock button picks again in front of you. On keyboard and mouse it is optional:
        /// off, the mouse aims as it always did; on, the lock key toggles it.
        /// </summary>
        public void ApplyPolicy(bool usingPad, bool mouseLockAllowed, bool togglePressed, Vector2 from, Vector2 facing)
        {
            if (usingPad)
            {
                if (togglePressed)
                {
                    Release();
                    Acquire(from, facing);
                }
                else if (!Active) Acquire(from, facing);
                return;
            }
            if (!mouseLockAllowed)
            {
                Release();
                return;
            }
            if (togglePressed) Toggle(from, facing);
        }
    }
}
