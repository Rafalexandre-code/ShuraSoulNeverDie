namespace Shura
{
    public enum BufferedAction
    {
        Attack = 0,
        Parry = 1,
        Dodge = 2,
        /// <summary>Weapon swap — doubles as the Troca Perfeita (swap-parry).</summary>
        Swap = 3,
        /// <summary>Detonate the Ki seals (Attack + Parry together, or its own key).</summary>
        Detonate = 4,
        /// <summary>A war cry that interrupts a red blow for a level of Flow (Parry + Dodge together, or its own key).</summary>
        Kiai = 5,
        /// <summary>Use the equipped tool (spends spirit papers).</summary>
        Tool = 6,
        /// <summary>Drink from the sake gourd.</summary>
        Gourd = 7,
        /// <summary>Bind wounds with a bandage.</summary>
        Bandage = 8,
        /// <summary>Switch to the next tool.</summary>
        ToolCycle = 9,
    }

    /// <summary>
    /// Remembers a press for a few simulation frames so a command pressed slightly early
    /// (during recovery or hit-stop) still comes out. Ages only on real ticks, never during
    /// hit-stop, so pressing during a freeze is never lost.
    ///
    /// Two-button commands are recognised here, for every input source at once (keyboard,
    /// gamepad, browser, bot): Attack and Parry within <see cref="ChordFrames"/> of each other
    /// also press Detonate; Parry and Dodge press Kiai.
    /// </summary>
    public sealed class InputBuffer
    {
        public const int Count = 10;
        /// <summary>Ticks two presses may be apart and still count as one chord.</summary>
        public const int ChordFrames = 5;

        readonly int[] remaining = new int[Count];
        /// <summary>Ticks since each action was last pressed (not cleared by consuming it).</summary>
        readonly int[] since = { 999, 999, 999, 999, 999, 999, 999, 999, 999, 999 };

        public void Press(BufferedAction action, int frames)
        {
            int a = (int)action;
            remaining[a] = frames;
            since[a] = 0;
            switch (action)
            {
                case BufferedAction.Attack when since[(int)BufferedAction.Parry] <= ChordFrames:
                case BufferedAction.Parry when since[(int)BufferedAction.Attack] <= ChordFrames:
                    Chord(BufferedAction.Detonate, frames);
                    break;
            }
            switch (action)
            {
                case BufferedAction.Dodge when since[(int)BufferedAction.Parry] <= ChordFrames:
                case BufferedAction.Parry when since[(int)BufferedAction.Dodge] <= ChordFrames:
                    Chord(BufferedAction.Kiai, frames);
                    break;
            }
        }

        void Chord(BufferedAction chord, int frames)
        {
            remaining[(int)chord] = frames;
            since[(int)chord] = 0;
        }

        public bool Peek(BufferedAction action)
        {
            return remaining[(int)action] > 0;
        }

        public bool Consume(BufferedAction action)
        {
            if (remaining[(int)action] <= 0) return false;
            remaining[(int)action] = 0;
            return true;
        }

        /// <summary>A chord fired: the single presses that formed it must not also act.</summary>
        public void Drop(BufferedAction action)
        {
            remaining[(int)action] = 0;
        }

        public void Age()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] > 0) remaining[i]--;
                if (since[i] < 999) since[i]++;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                remaining[i] = 0;
                since[i] = 999;
            }
        }
    }
}
