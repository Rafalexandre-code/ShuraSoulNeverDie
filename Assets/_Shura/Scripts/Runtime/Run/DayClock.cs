using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The day that passes while you fight: a 24-hour clock that runs while playing (one game
    /// minute per real second — a whole day in 24 minutes), from the dawn of the departure. The
    /// hour lights the world (see <see cref="PostMood"/> and the light map), wakes the yokai at
    /// night and the fireflies at dusk, and marks its passing with small moments: the first
    /// birds, the sunrise, the noon bell, the sunset, midnight, the Hour of the Ox.
    /// </summary>
    public sealed class DayClock
    {
        /// <summary>Game minutes per real second.</summary>
        public const float MinutesPerSecond = 1f;
        public const float StartHour = 5.5f;

        public float Hour { get; private set; } = StartHour;
        public int Day { get; private set; } = 1;

        /// <summary>A moment of the day crossed: its index in <see cref="Moments"/>.</summary>
        public event Action<int> MomentReached;

        public struct Moment
        {
            public float hour;
            public string text;
        }

        public static readonly Moment[] Moments =
        {
            new Moment { hour = 5.5f, text = "Os primeiros pássaros cantam. A névoa da aurora cobre o chão." },
            new Moment { hour = 6.25f, text = "O sol nasce por trás das montanhas." },
            new Moment { hour = 9f, text = "O orvalho seca; o vento traz o cheiro de pinheiro." },
            new Moment { hour = 12f, text = "Meio-dia. Um sino de templo, muito longe, soa doze vezes." },
            new Moment { hour = 15.5f, text = "A luz da tarde fica dourada. As cigarras cantam." },
            new Moment { hour = 18f, text = "O sol se põe. As sombras ficam longas e vermelhas." },
            new Moment { hour = 19.5f, text = "Anoitece. Os yokai despertam, e os vagalumes também." },
            new Moment { hour = 0f, text = "Meia-noite. A lua está no alto." },
            new Moment { hour = 1.5f, text = "A Hora do Boi: os espíritos estão mais perto do que nunca." },
            new Moment { hour = 4f, text = "O céu clareia a leste. A noite está quase no fim." },
        };

        public DayClock(float startHour = StartHour)
        {
            Hour = Mathf.Repeat(startHour, 24f);
        }

        public void Set(float hour) => Hour = Mathf.Repeat(hour, 24f);

        public void Advance(float realSeconds)
        {
            if (realSeconds <= 0f) return;
            float before = Hour;
            float after = Hour + realSeconds * MinutesPerSecond / 60f;
            if (after >= 24f) Day++;
            for (int i = 0; i < Moments.Length; i++)
            {
                float h = Moments[i].hour;
                bool crossed = after < 24f ? before < h && after >= h : before < h || h <= after - 24f;
                if (crossed) MomentReached?.Invoke(i);
            }
            Hour = Mathf.Repeat(after, 24f);
        }

        public TimeOfDay Period => PeriodAt(Hour);

        public static TimeOfDay PeriodAt(float hour)
        {
            if (hour >= 5f && hour < 7f) return TimeOfDay.Dawn;
            if (hour >= 7f && hour < 12f) return TimeOfDay.Morning;
            if (hour >= 12f && hour < 17f) return TimeOfDay.Afternoon;
            if (hour >= 17f && hour < 19.5f) return TimeOfDay.Dusk;
            return TimeOfDay.Night;
        }

        /// <summary>"06:42".</summary>
        public static string Format(float hour)
        {
            int m = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f);
            return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
        }

        /// <summary>How dark the open air is at this hour (0 bright noon, ~0.42 deep night), smoothly.</summary>
        public static float Darkness(float hour)
        {
            // Keyframes over the day; linear between them.
            float[] h = { 0f, 4.5f, 5.5f, 6.5f, 8f, 12f, 16f, 17.5f, 18.5f, 19.5f, 21f, 24f };
            float[] d = { 0.42f, 0.4f, 0.28f, 0.16f, 0.08f, 0.04f, 0.07f, 0.14f, 0.24f, 0.34f, 0.42f, 0.42f };
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i < h.Length - 1; i++)
            {
                if (hour < h[i + 1]) return Mathf.Lerp(d[i], d[i + 1], (hour - h[i]) / (h[i + 1] - h[i]));
            }
            return d[d.Length - 1];
        }
    }
}
