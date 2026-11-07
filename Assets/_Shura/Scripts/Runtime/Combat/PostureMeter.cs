using UnityEngine;

namespace Shura
{
    /// <summary>Posture fills with pressure and drains after a quiet period. Full = broken.</summary>
    public sealed class PostureMeter
    {
        int framesSinceDamage;

        public float Max { get; private set; }
        public float Value { get; private set; }
        public float Ratio => Max > 0f ? Value / Max : 0f;
        public bool IsFull => Value >= Max - 0.001f;

        public PostureMeter(float max)
        {
            Max = Mathf.Max(1f, max);
        }

        /// <summary>Returns true when this addition fills the meter.</summary>
        public bool Add(float amount)
        {
            if (amount <= 0f) return false;
            Value = Mathf.Min(Max, Value + amount);
            framesSinceDamage = 0;
            return IsFull;
        }

        public void Tick(float regenPerSecond, int regenDelayFrames)
        {
            framesSinceDamage++;
            if (framesSinceDamage > regenDelayFrames)
            {
                Value = Mathf.Max(0f, Value - regenPerSecond * SimClock.Dt);
            }
        }

        public void Reset(float value = 0f)
        {
            Value = Mathf.Clamp(value, 0f, Max);
            framesSinceDamage = 0;
        }

        public void SetMax(float max)
        {
            Max = Mathf.Max(1f, max);
            Value = Mathf.Min(Value, Max);
        }
    }
}
