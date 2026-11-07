using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Flow rewards perfect play with levels (0..3) that drive bonuses and music layers.
    /// Taking damage drops you back to the start of the previous level.
    /// </summary>
    public sealed class FlowMeter
    {
        readonly int[] thresholds;
        readonly float maxPoints;

        public float Points { get; private set; }
        public int MaxLevel => thresholds.Length;

        public FlowMeter(int[] levelThresholds, float maxPoints)
        {
            thresholds = levelThresholds;
            this.maxPoints = maxPoints;
        }

        public int Level
        {
            get
            {
                int level = 0;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (Points >= thresholds[i]) level = i + 1;
                }
                return level;
            }
        }

        /// <summary>0..1 progress toward the next level (1 at max level).</summary>
        public float ProgressInLevel
        {
            get
            {
                int level = Level;
                if (level >= MaxLevel) return 1f;
                float low = level == 0 ? 0f : thresholds[level - 1];
                float high = thresholds[level];
                return Mathf.Clamp01((Points - low) / (high - low));
            }
        }

        public int Add(float points)
        {
            Points = Mathf.Min(maxPoints, Points + Mathf.Max(0f, points));
            return Level;
        }

        public int DropLevels(int levels)
        {
            int target = Mathf.Max(0, Level - Mathf.Max(1, levels));
            Points = target == 0 ? 0f : thresholds[target - 1];
            return Level;
        }

        public void Reset()
        {
            Points = 0f;
        }
    }
}
