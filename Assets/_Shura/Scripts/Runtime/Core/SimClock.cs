using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Fixed 60 Hz simulation clock. Combat is authored in frames, so the simulation advances
    /// in whole ticks regardless of display refresh rate. Hit-stop freezes ticks instead of
    /// touching Time.timeScale, so UI, audio and particles keep running during the freeze.
    /// </summary>
    public sealed class SimClock
    {
        public const int TickRate = 60;
        public const float Dt = 1f / TickRate;
        const float DefaultMaxFrameTime = 0.25f;

        float accumulator;

        public int HitstopFrames { get; private set; }
        public long TickCount { get; private set; }
        public float Alpha => Mathf.Clamp01(accumulator / Dt);

        public void Reset()
        {
            accumulator = 0f;
            HitstopFrames = 0;
        }

        /// <summary>Clamped so a long hitch never triggers a burst of catch-up ticks.</summary>
        public void Accumulate(float deltaTime, float maxFrameTime = DefaultMaxFrameTime)
        {
            accumulator += Mathf.Min(Mathf.Max(0f, deltaTime), maxFrameTime);
        }

        /// <summary>Stronger hits extend the freeze; weaker ones never shorten it.</summary>
        public void AddHitstop(int frames)
        {
            if (frames > HitstopFrames) HitstopFrames = frames;
        }

        /// <summary>Consumes one tick of accumulated time. Frozen ticks must not run gameplay.</summary>
        public bool Step(out bool frozen)
        {
            frozen = false;
            if (accumulator < Dt) return false;
            accumulator -= Dt;
            if (HitstopFrames > 0)
            {
                HitstopFrames--;
                frozen = true;
                return true;
            }
            TickCount++;
            return true;
        }
    }
}
