namespace Shura
{
    public enum BufferedAction
    {
        Attack = 0,
        Parry = 1,
        Dodge = 2,
    }

    /// <summary>
    /// Remembers a press for a few simulation frames so a command pressed slightly early
    /// (during recovery or hit-stop) still comes out. Ages only on real ticks, never during
    /// hit-stop, so pressing during a freeze is never lost.
    /// </summary>
    public sealed class InputBuffer
    {
        readonly int[] remaining = new int[3];

        public void Press(BufferedAction action, int frames)
        {
            remaining[(int)action] = frames;
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

        public void Age()
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] > 0) remaining[i]--;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < remaining.Length; i++) remaining[i] = 0;
        }
    }
}
