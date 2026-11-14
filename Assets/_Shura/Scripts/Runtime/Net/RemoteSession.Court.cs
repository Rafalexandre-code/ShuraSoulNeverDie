using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>The guest's courtyard (drawn from the host's "court" messages) and its duels.</summary>
    public sealed partial class RemoteSession
    {
        /// <summary>The courtyard as last heard: one list per walker, and the popups.</summary>
        public List<object> CourtWalkers { get; private set; }
        public List<object> CourtPrev { get; private set; }
        public List<object> CourtPopups { get; private set; }
        public float CourtAge { get; private set; }
        float courtInterval = 1f / 15f;
        /// <summary>Set by the screen: walking in the courtyard (no menu open), so the input goes to the host.</summary>
        public bool CourtWalk { get; set; }
        bool botInteract;
        float botCourtTimer;

        public void SendRaw(string text) => Send(text);

        void OnCourt(Dictionary<string, object> m)
        {
            if (!(m.TryGetValue("w", out object w) && w is List<object> walkers)) return;
            CourtPrev = CourtWalkers;
            CourtWalkers = walkers;
            CourtPopups = m.TryGetValue("p", out object p) ? p as List<object> : null;
            if (CourtAge > 0.02f) courtInterval = Mathf.Lerp(courtInterval, Mathf.Clamp(CourtAge, 0.03f, 0.3f), 0.2f);
            CourtAge = 0f;
        }

        /// <summary>0..1 between the previous and the latest courtyard message.</summary>
        public float CourtBlend => Mathf.Clamp01(CourtAge / Mathf.Max(0.02f, courtInterval));

        public void AgeCourt(float dt) => CourtAge += dt;

        public List<object> CourtMe()
        {
            if (CourtWalkers == null) return null;
            foreach (object o in CourtWalkers)
            {
                if (o is List<object> w && MiniJson.I(w, 0) == MyId) return w;
            }
            return null;
        }

        void OnDuelMessage(Dictionary<string, object> m, string type)
        {
            switch (type)
            {
                case "duel":
                    game.BeginDuel(DuelStart.Parse(m, MyId));
                    break;
                default:
                    long h = m.TryGetValue("h", out object ho) && ho is double hd ? (long)hd : 0L;
                    game.Duel?.ReceiveFromHost(type, MiniJson.I(m, "d"), MiniJson.I(m, "f"), MiniJson.I(m, "v"), h, MiniJson.I(m, "rt", -1), MiniJson.I(m, "g"));
                    break;
            }
        }

        /// <summary>Automated duel test (guest): walk into the circle and ask, once.</summary>
        void BotCourt()
        {
            botInteract = false;
            List<object> me = CourtMe();
            if (me == null || !game.Launch.DuelTest || game.DuelsFinished > 0) return;
            var pos = new Vector2(MiniJson.F(me, 3), MiniJson.F(me, 4));
            Vector2 to = Courtyard.DuelCircle + Vector2.right * 0.8f - pos;
            botMove = to.magnitude > 0.5f ? to.normalized : Vector2.zero;
            botCourtTimer += 1f / 60f;
            bool challenging = MiniJson.B(me, 12);
            if (Courtyard.In(pos, Courtyard.DuelCircle, Courtyard.CircleRadius) && !challenging && botCourtTimer > 1.5f)
            {
                botCourtTimer = 0f;
                botInteract = true;
            }
        }
    }
}
