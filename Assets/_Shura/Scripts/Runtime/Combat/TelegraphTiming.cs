using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Timing of the visual "press now" indicator. A ring shrinks onto the attacker and closes
    /// a few frames BEFORE the swing becomes active, so pressing exactly when it closes lands
    /// in the middle of the perfect-parry window (and inside the dodge i-frames), leaving
    /// margin for human error on both sides.
    /// </summary>
    public static class TelegraphTiming
    {
        /// <summary>Step frame at which the ring closes ("press now").</summary>
        public static int CueFrame(int windup, int leadFrames)
        {
            return Mathf.Max(0, windup - leadFrames);
        }

        /// <summary>0 when the approach ring appears, 1 when it closes.</summary>
        public static float ApproachProgress(int stepFrame, int windup, int leadFrames, int approachFrames)
        {
            int cue = CueFrame(windup, leadFrames);
            int start = cue - Mathf.Max(1, approachFrames);
            return Mathf.Clamp01((stepFrame - start) / (float)(cue - start));
        }

        public static bool ApproachVisible(int stepFrame, int windup, int leadFrames, int approachFrames)
        {
            int cue = CueFrame(windup, leadFrames);
            return stepFrame >= cue - approachFrames && stepFrame <= windup;
        }

        /// <summary>A parry pressed at the cue is perfect if the swing lands inside the window.</summary>
        public static bool ParryAtCueIsPerfect(int leadFrames, int perfectParryFrames)
        {
            return leadFrames >= 0 && leadFrames < perfectParryFrames;
        }

        /// <summary>A dodge pressed at the cue is invulnerable when the swing becomes active.</summary>
        public static bool DodgeAtCueIsInvulnerable(int leadFrames, int iFrameStart, int iFrameEnd)
        {
            return leadFrames >= iFrameStart && leadFrames <= iFrameEnd;
        }
    }
}
