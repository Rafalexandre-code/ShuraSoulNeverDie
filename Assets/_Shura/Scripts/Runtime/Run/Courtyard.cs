using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>What a duelist in the courtyard does this frame.</summary>
    public struct CourtInput
    {
        public Vector2 move;
        public bool attack, parry, interact;
        /// <summary>A kamae chosen this frame (0..3), or -1.</summary>
        public int kamae;
    }

    /// <summary>
    /// The lobby as a place (design doc §9.7): the courtyard of the ruined Temple, where the
    /// party walks before leaving. Spar without damage (a touch, or a parry if they were ready),
    /// sit at the Tea House, vote the group's Oath at the stone, step into the duel circle, stand
    /// under the torii when ready. The host runs it; guests see it at 15 frames a second.
    /// </summary>
    public sealed class Courtyard
    {
        public const float HalfW = 9f, HalfH = 5f;
        public const float Speed = 3.6f;
        public const float SparReach = 1.5f;
        public const int ParryWindow = 12;

        public static readonly Vector2 Torii = new Vector2(-7.4f, 0f);
        public static readonly Vector2 TeaHouse = new Vector2(5.6f, 3.1f);
        public static readonly Vector2 OathStone = new Vector2(-4.2f, 3.3f);
        public static readonly Vector2 DuelCircle = new Vector2(3.6f, -1.6f);
        public const float SpotRadius = 1.3f, CircleRadius = 2.1f;

        public sealed class Walker
        {
            public int id;
            public string name = "";
            public int art;
            public Vector2 pos;
            public Vector2 facing = Vector2.down;
            public bool moving;
            /// <summary>Frames left of a practice swing / a raised guard.</summary>
            public int swing, guard;
            public int sinceParry = 999;
            public bool sitting;
            public int kamae = 1;
            /// <summary>Standing in the duel circle, asking for a duel.</summary>
            public bool challenging;
            public bool dueling;
            public int touches;
            public float stepTime;
        }

        public struct Popup
        {
            public string text;
            public Vector2 at;
            public int color;
            public float age;
        }

        public readonly List<Walker> Walkers = new List<Walker>();
        public readonly List<Popup> Popups = new List<Popup>();
        /// <summary>A duel to start: the two in the circle who both asked (set once, taken by the host).</summary>
        public (int a, int b)? PendingDuel;

        public Walker Get(int id) => Walkers.Find(w => w.id == id);

        public static bool In(Vector2 p, Vector2 spot, float r) => (p - spot).sqrMagnitude <= r * r;

        public Walker Join(int id, string name, int art)
        {
            Walker w = Get(id);
            if (w == null)
            {
                w = new Walker { id = id, pos = new Vector2(-2f + Walkers.Count * 1.1f, -0.5f - (Walkers.Count % 2) * 1.2f) };
                Walkers.Add(w);
            }
            w.name = name;
            w.art = art;
            return w;
        }

        public void Leave(int id) => Walkers.RemoveAll(w => w.id == id);

        public void Say(string text, Vector2 at, int color = 0) => Popups.Add(new Popup { text = text, at = at, color = color });

        /// <summary>One frame (1/60 s). <paramref name="act"/> handles what the spots do (the Oath, the tea).</summary>
        public void Tick(System.Func<int, CourtInput> inputOf, System.Action<Walker, string> act)
        {
            const float dt = SimClock.Dt;
            foreach (Walker w in Walkers)
            {
                if (w.dueling)
                {
                    w.moving = false;
                    continue;
                }
                CourtInput i = inputOf(w.id);
                if (i.kamae >= 0 && i.kamae <= 3) w.kamae = i.kamae;
                if (w.swing > 0) w.swing--;
                if (w.guard > 0) w.guard--;
                if (w.sinceParry < 999) w.sinceParry++;
                if (i.parry)
                {
                    w.guard = 18;
                    w.sinceParry = 0;
                }
                Vector2 m = i.move.sqrMagnitude > 1f ? i.move.normalized : i.move;
                w.moving = m.sqrMagnitude > 0.04f && !w.sitting;
                if (w.moving)
                {
                    w.pos += m * Speed * dt;
                    w.facing = m.normalized;
                    w.stepTime += dt;
                    w.challenging = w.challenging && In(w.pos, DuelCircle, CircleRadius);
                }
                else if (i.move.sqrMagnitude > 0.04f && w.sitting)
                {
                    w.sitting = false;
                }
                w.pos.x = Mathf.Clamp(w.pos.x, -HalfW + 0.5f, HalfW - 0.5f);
                w.pos.y = Mathf.Clamp(w.pos.y, -HalfH + 0.5f, HalfH - 0.8f);
                if (i.attack && w.swing == 0 && !w.sitting) Spar(w);
                if (i.interact) Interact(w, act);
            }
            Separate();
            MatchDuelists();
            for (int k = Popups.Count - 1; k >= 0; k--)
            {
                Popup p = Popups[k];
                p.age += dt;
                if (p.age > 1.6f) Popups.RemoveAt(k);
                else Popups[k] = p;
            }
        }

        /// <summary>A practice blow: no damage — a touch, or the partner's parry if their guard rose in time.</summary>
        void Spar(Walker w)
        {
            w.swing = 16;
            Walker target = null;
            float best = SparReach;
            foreach (Walker o in Walkers)
            {
                if (o == w || o.dueling) continue;
                float d = Vector2.Distance(o.pos, w.pos);
                if (d < best)
                {
                    best = d;
                    target = o;
                }
            }
            if (target == null) return;
            w.facing = (target.pos - w.pos).normalized;
            if (target.sinceParry <= ParryWindow)
            {
                Say(Strings.CourtParried, target.pos + Vector2.up * 1.2f, 1);
                w.swing = 30;
                return;
            }
            w.touches++;
            Say(Strings.CourtTouch, target.pos + Vector2.up * 1.2f, 2);
        }

        void Interact(Walker w, System.Action<Walker, string> act)
        {
            if (In(w.pos, TeaHouse, SpotRadius))
            {
                w.sitting = !w.sitting;
                if (w.sitting) act?.Invoke(w, "cha");
                return;
            }
            if (In(w.pos, OathStone, SpotRadius))
            {
                act?.Invoke(w, "juramento");
                return;
            }
            if (In(w.pos, DuelCircle, CircleRadius))
            {
                w.challenging = !w.challenging;
                Say(w.challenging ? Strings.CourtChallenge : Strings.CourtWithdraw, w.pos + Vector2.up * 1.3f, w.challenging ? 3 : 0);
                return;
            }
            if (In(w.pos, Torii, SpotRadius)) act?.Invoke(w, "torii");
        }

        /// <summary>Two in the circle who both asked: their duel begins (the first pair found).</summary>
        void MatchDuelists()
        {
            if (PendingDuel != null) return;
            Walker first = null;
            foreach (Walker w in Walkers)
            {
                if (!w.challenging || w.dueling || !In(w.pos, DuelCircle, CircleRadius)) continue;
                if (first == null)
                {
                    first = w;
                    continue;
                }
                first.challenging = w.challenging = false;
                PendingDuel = (first.id, w.id);
                return;
            }
        }

        void Separate()
        {
            for (int i = 0; i < Walkers.Count; i++)
            {
                for (int j = i + 1; j < Walkers.Count; j++)
                {
                    Walker a = Walkers[i], b = Walkers[j];
                    Vector2 d = b.pos - a.pos;
                    float dist = d.magnitude;
                    if (dist >= 0.7f) continue;
                    Vector2 n = dist > 1e-3f ? d / dist : Vector2.right;
                    float push = (0.7f - dist) * 0.5f;
                    if (!a.dueling && !a.sitting) a.pos -= n * push;
                    if (!b.dueling && !b.sitting) b.pos += n * push;
                }
            }
        }

        /// <summary>Who stands under the torii (ready to leave).</summary>
        public bool UnderTorii(Walker w) => In(w.pos, Torii, SpotRadius + 0.4f);
    }
}
