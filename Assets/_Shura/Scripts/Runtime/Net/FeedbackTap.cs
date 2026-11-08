using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Sits between the world and the real feedback router: everything still plays on the
    /// host, and while hosting, each moment (hits, parries, blood, popups, toasts) is also
    /// written down for the next snapshot so browser players see and hear it too.
    /// </summary>
    public sealed class FeedbackTap : IFeedback
    {
        const int MaxEvents = 160;

        readonly IFeedback inner;
        readonly StringBuilder events = new StringBuilder(4096);
        int count;

        public bool Recording { get; set; }

        public FeedbackTap(IFeedback inner)
        {
            this.inner = inner;
        }

        /// <summary>The recorded events as the inside of a JSON array, then cleared.</summary>
        public string Take()
        {
            string s = events.ToString();
            events.Clear();
            count = 0;
            return s;
        }

        StringBuilder Begin(string kind)
        {
            if (!Recording || count >= MaxEvents) return null;
            if (count > 0) events.Append(',');
            count++;
            return events.Append('[').Str(kind).Append(',');
        }

        public void Emit(FxType type, Vector2 position, Vector2 direction, float magnitude = 1f, Color? color = null)
        {
            inner.Emit(type, position, direction, magnitude, color);
            if (type == FxType.TimingCue) return;
            StringBuilder sb = Begin("fx");
            sb?.Str(type.ToString()).Append(',').Num(position.x).Append(',').Num(position.y).Append(',')
              .Num(direction.x).Append(',').Num(direction.y).Append(',').Num(magnitude).Append(',').Hex(color ?? Color.white).Append(']');
        }

        public void Popup(string text, Vector2 worldPosition, Color color)
        {
            inner.Popup(text, worldPosition, color);
            Begin("pop")?.Str(text).Append(',').Num(worldPosition.x).Append(',').Num(worldPosition.y).Append(',').Hex(color).Append(']');
        }

        public void Toast(string text, Color color)
        {
            inner.Toast(text, color);
            Begin("toast")?.Str(text).Append(',').Hex(color).Append(']');
        }

        public void Hint(HintId hint)
        {
            inner.Hint(hint);
        }

        public void Blood(Vector2 position, Vector2 direction, float amount, Color color)
        {
            inner.Blood(position, direction, amount, color);
            Begin("blood")?.Num(position.x).Append(',').Num(position.y).Append(',').Num(direction.x).Append(',').Num(direction.y).Append(',')
                          .Num(amount, 0).Append(',').Hex(color).Append(']');
        }

        public void Gore(FinisherStyle style, Vector2 position, Vector2 direction, Sprite body, Color bodyColor, Color blood, float radius)
        {
            inner.Gore(style, position, direction, body, bodyColor, blood, radius);
            Begin("gore")?.Str(style.ToString()).Append(',').Num(position.x).Append(',').Num(position.y).Append(',')
                         .Num(direction.x).Append(',').Num(direction.y).Append(',').Hex(blood).Append(']');
        }

        public void ItemCollected(Item item, bool equipped, Vector2 position)
        {
            inner.ItemCollected(item, equipped, position);
            Begin("toast")?.Str(item.Name).Append(',').Hex(Rarities.Color(item.rarity)).Append(']');
        }

        public void Footstep(Vector2 position)
        {
            inner.Footstep(position);
        }
    }
}
