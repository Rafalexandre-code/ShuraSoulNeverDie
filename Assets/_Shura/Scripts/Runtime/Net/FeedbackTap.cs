using System;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Sits between the world and the real feedback router: everything still plays on the
    /// host, and while hosting, each moment (hits, parries, blood, popups, toasts) is also
    /// written down for the next snapshot so the other players see and hear it too. Each
    /// moment carries who caused it, so a guest's screen only freezes and shakes for its own.
    /// </summary>
    public sealed class FeedbackTap : IFeedback
    {
        const int MaxEvents = 160;

        readonly IFeedback inner;
        readonly StringBuilder events = new StringBuilder(4096);
        int count;

        public bool Recording { get; set; }
        /// <summary>Who is acting right now (-1 the world, 0 the host, otherwise a guest's id).</summary>
        public Func<int> Actor { get; set; }

        public FeedbackTap(IFeedback inner)
        {
            this.inner = inner;
        }

        int ActorId => Actor != null ? Actor() : -1;

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

        /// <summary>Every gameplay moment as it happens (the tutorial listens for what the player just did).</summary>
        public event Action<FxType, Vector2, float> Emitted;

        public void Emit(FxType type, Vector2 position, Vector2 direction, float magnitude = 1f, Color? color = null)
        {
            inner.Emit(type, position, direction, magnitude, color);
            Emitted?.Invoke(type, position, magnitude);
            if (type == FxType.TimingCue) return;
            StringBuilder sb = Begin("fx");
            sb?.Str(type.ToString()).Append(',').Num(position.x).Append(',').Num(position.y).Append(',')
              .Num(direction.x).Append(',').Num(direction.y).Append(',').Num(magnitude).Append(',').Hex(color ?? Color.white).Append(',').Int(ActorId).Append(']');
        }

        public void Popup(string text, Vector2 worldPosition, Color color)
        {
            inner.Popup(text, worldPosition, color);
            Begin("pop")?.Str(text).Append(',').Num(worldPosition.x).Append(',').Num(worldPosition.y).Append(',').Hex(color).Append(',').Int(ActorId).Append(']');
        }

        public void Toast(string text, Color color)
        {
            inner.Toast(text, color);
            Begin("toast")?.Str(text).Append(',').Hex(color).Append(']');
        }

        public void Hint(HintId hint)
        {
            inner.Hint(hint);
            Begin("hint")?.Int((int)hint).Append(']');
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
                         .Num(direction.x).Append(',').Num(direction.y).Append(',').Hex(blood).Append(',').Num(radius).Append(',').Int(ActorId).Append(']');
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
