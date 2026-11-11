using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// This game as a guest in a friend's duel, in its own window: no browser. The host's game
    /// runs the one true simulation; here the very same arena is rebuilt from its seed, every
    /// duelist, enemy, arrow and drop is drawn from the host's snapshots, every hit and drop of
    /// blood is replayed through this game's own effects and sounds, and this player's keys
    /// (or gamepad) go back to the host.
    ///
    /// Latency, in order of what matters most:
    ///   * this duelist is drawn where it will be, not where it was: the moves sent but not yet
    ///     seen back are added on top of the host's last word (prediction), and it turns to the
    ///     mouse at once;
    ///   * everything else is drawn on the host's own timeline, as little behind as the
    ///     network's jitter allows (a clock estimate plus an adaptive buffer);
    ///   * moments (hits, parries, blood) play when what they belong to is drawn; this
    ///     duelist's own play at once;
    ///   * input leaves every frame something changes, with each press counted, so a lost
    ///     packet never loses a press.
    /// </summary>
    public sealed class RemoteSession
    {
        public sealed class Snap
        {
            public int seq;
            public double hostMs;
            public string mode = "lobby";
            public Dictionary<string, object> d;
            public List<object> P, E, R, B, I, T, O, Z, PR, SB, LB, env, ch, enc, so, rt;
            public readonly Dictionary<int, List<object>> players = new Dictionary<int, List<object>>();
            public readonly Dictionary<int, List<object>> enemies = new Dictionary<int, List<object>>();
        }

        public sealed class Card
        {
            public string name, gain, cost, source;
            public RewardKind kind;
        }

        public sealed class Door
        {
            public RoomKind kind;
            public string glyph, text, kindText;
            public Color color;
            /// <summary>0..2 across the map: a door leads to the next layer's doors within one column.</summary>
            public int column;
        }

        public sealed class Gear
        {
            public string name, rarity, gain;
            public Color color;
            public int slot;
        }

        struct Pending
        {
            public double hostMs;
            public List<object> ev;
        }

        struct MoveSample
        {
            public double at;
            public Vector2 move;
        }

        const int MaxSnaps = 16;
        const float PingEvery = 1f;
        const float InputKeepAlive = 0.05f;
        const float PredictionCap = 0.3f;
        const float JoinTimeout = 25f;
        public const float PlayerRadius = 0.38f;

        readonly Game game;
        readonly List<Snap> snaps = new List<Snap>(MaxSnaps);
        readonly List<Pending> pending = new List<Pending>(256);
        readonly List<MoveSample> moves = new List<MoveSample>(64);
        readonly StringBuilder sb = new StringBuilder(512);
        IRemoteLink link;
        bool joinSent;
        double clockOffset = double.NaN;
        double jitterMs = 10.0;
        float pingTimer;
        float inputTimer;
        string lastInput;
        int resend;
        int kamae = -1;
        float speedEstimate;
        double lastPredictAt;
        Vector2 shownOwn;
        bool shownValid;
        string lastMode;

        public RemoteSession(Game owner)
        {
            game = owner;
            View = new RemoteView(owner, this);
            speedEstimate = owner.Config.player.moveSpeed;
        }

        public RemoteView View { get; }
        public bool Active => link != null;
        public bool Joined { get; private set; }
        public int MyId { get; private set; } = -1;
        public string Status { get; private set; } = string.Empty;
        /// <summary>Why the last try ended (shown on the join screen), or null.</summary>
        public string LastError { get; set; }
        float joinTimer;
        public string Where => link != null ? link.Describe : string.Empty;
        /// <summary>Round trip to the host, seconds (measured by this game every second).</summary>
        public float Ping { get; private set; } = 0.08f;
        public Snap Latest => snaps.Count > 0 ? snaps[snaps.Count - 1] : null;
        public string Mode => Latest != null ? Latest.mode : "lobby";
        public bool Between => Mode == "lobby" || Mode == "over";
        public bool Ready { get; private set; }
        /// <summary>The screens stop this duelist (menus, cards): it stands still while the fight goes on.</summary>
        public bool InputBlocked { get; set; }

        // Personal: this guest's own cards, the doors to vote for, the equipment.
        public readonly List<Card> Cards = new List<Card>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<List<Door>> DoorsAhead = new List<List<Door>>();
        public int MyVote { get; set; } = -1;
        public readonly Gear[] Worn = new Gear[4];
        public readonly string[] SlotNames = new string[4];
        public readonly List<Gear> Bag = new List<Gear>();
        public readonly List<string> Owned = new List<string>();
        public readonly int[] Stats = new int[5];
        public bool HasInventory { get; private set; }

        /// <summary>This guest's row of the last snapshot (null before the welcome).</summary>
        public List<object> Me
        {
            get
            {
                Snap s = Latest;
                if (s == null || MyId < 0) return null;
                s.players.TryGetValue(MyId, out List<object> row);
                return row;
            }
        }

        /// <summary>Where this duelist is drawn (predicted), for the camera, the aim and the HUD.</summary>
        public Vector2 OwnPosition => shownValid ? shownOwn : Vector2.zero;
        public bool HasOwn => shownValid;
        public static double NowMs => Time.realtimeSinceStartupAsDouble * 1000.0;
        static readonly Color BloodRed = ColorUtil.Hex("#8E0F1E");

        // ── Lifecycle ───────────────────────────────────────────────────────────────────

        public void Begin(IRemoteLink newLink)
        {
            Leave(false);
            link = newLink;
            LastError = null;
            joinTimer = 0f;
            joinSent = false;
            Joined = false;
            MyId = -1;
            Ready = false;
            Status = Strings.RemoteConnecting;
            clockOffset = double.NaN;
            snaps.Clear();
            pending.Clear();
            moves.Clear();
            Cards.Clear();
            Doors.Clear();
            HasInventory = false;
            lastMode = null;
            Application.runInBackground = true;
        }

        /// <summary>Back to this game's own menus.</summary>
        public void Leave(bool toMenu = true)
        {
            bool was = Joined;
            // Leaving in the middle of a run still banks what it was worth.
            if (was && inRun) BankRun();
            link?.Close();
            link = null;
            Joined = false;
            MyId = -1;
            snaps.Clear();
            pending.Clear();
            shownValid = false;
            View.Clear();
            game.Feedback.GuestActor = null;
            game.Feedback.FocusOverride = null;
            if (was) game.World.EndRemote();
            Application.runInBackground = game.Launch.Automated;
            if (toMenu) game.OnRemoteEnded();
        }

        // ── Per frame ───────────────────────────────────────────────────────────────────

        public void Update(float dt)
        {
            if (link == null) return;
            if (!joinSent && link.Connected)
            {
                joinSent = true;
                Status = string.Format(Strings.RemoteJoining, link.Describe);
                SendJoin();
            }
            while (link != null && link.TryReceive(out string text)) Handle(text);
            if (link == null) return;
            if (!Joined) joinTimer += dt;
            if (link.Lost || (!Joined && joinTimer > JoinTimeout))
            {
                bool wasJoined = Joined;
                Status = wasJoined ? Strings.RemoteLost : string.Format(Strings.RemoteFailed, link.Describe);
                LastError = Status;
                Leave();
                if (wasJoined) game.UI.Toast(Strings.RemoteLost, game.Config.palette.danger);
                return;
            }
            if (!Joined) return;

            pingTimer += dt;
            if (pingTimer >= PingEvery)
            {
                pingTimer = 0f;
                Send("{\"t\":\"gping\",\"c\":" + NowMs.ToString("0.###", CultureInfo.InvariantCulture) + "}");
            }
            if (game.Launch.Automated) AutoPlay();
            SendInput(dt);
            FirePending(RenderHostMs());
        }

        /// <summary>Draws the host's duel as of now (called after the frame's input).</summary>
        public void Render(float dt)
        {
            if (!Joined || snaps.Count == 0) return;
            double renderMs = RenderHostMs();
            Frame(renderMs, out Snap a, out Snap b, out float t);
            UpdateOwn(dt);
            View.Render(a, b, t, renderMs, Latest, dt);
        }

        // ── Messages from the host ──────────────────────────────────────────────────────

        void Handle(string text)
        {
            if (!(MiniJson.Parse(text) is Dictionary<string, object> m)) return;
            switch (MiniJson.S(m, "t"))
            {
                case "s":
                    OnSnapshot(m);
                    break;
                case "welcome":
                    MyId = MiniJson.I(m, "id", -1);
                    Joined = true;
                    Status = string.Format(Strings.RemoteConnected, link.Describe);
                    game.Feedback.GuestActor = actor => actor >= 0 && actor != MyId;
                    game.Feedback.FocusOverride = () => OwnPosition;
                    game.UI.ClearLog();
                    game.OnRemoteJoined();
                    Send("{\"t\":\"inv\"}");
                    break;
                case "room":
                    OnRoom(m);
                    break;
                case "rewards":
                    Cards.Clear();
                    List<object> list = MiniJson.L(m, "list");
                    if (list != null)
                    {
                        foreach (object o in list)
                        {
                            if (!(o is Dictionary<string, object> c)) continue;
                            Cards.Add(new Card
                            {
                                name = MiniJson.S(c, "n"), gain = MiniJson.S(c, "g"), cost = MiniJson.S(c, "c"),
                                kind = (RewardKind)MiniJson.I(c, "k"), source = MiniJson.S(c, "src"),
                            });
                        }
                    }
                    game.UI.OnRemoteCards();
                    break;
                case "picked":
                    Cards.Clear();
                    game.UI.Toast(string.Format(Strings.Obtained, MiniJson.S(m, "n")), game.Config.palette.gold);
                    game.Audio.Play(Sfx.UiConfirm, 0.8f, 1f, 0f);
                    break;
                case "route":
                    OnRoute(m);
                    break;
                case "routed":
                    Doors.Clear();
                    DoorsAhead.Clear();
                    MyVote = -1;
                    break;
                case "inv":
                    OnInventory(m);
                    break;
                case "toast":
                    game.UI.Toast(MiniJson.S(m, "text"), MiniJson.Color(MiniJson.S(m, "c"), game.Config.palette.uiText));
                    break;
                case "full":
                    Status = Strings.RemoteFull;
                    LastError = Status;
                    game.UI.Toast(Strings.RemoteFull, game.Config.palette.danger);
                    Leave();
                    break;
                case "ping":
                    if (m.TryGetValue("c", out object c0) && c0 is double hc)
                        Send("{\"t\":\"pong\",\"c\":" + hc.ToString("0.###", CultureInfo.InvariantCulture) + "}");
                    break;
                case "gpong":
                    float rtt = (float)((NowMs - MiniJson.F(m, "c")) / 1000.0);
                    if (rtt > 0f && rtt < 3f) Ping = Ping <= 0f ? rtt : Mathf.Lerp(Ping, rtt, 0.35f);
                    break;
            }
        }

        void OnRoom(Dictionary<string, object> m)
        {
            List<object> ld = MiniJson.L(m, "ld");
            if (ld == null) return;
            ulong.TryParse(MiniJson.S(ld, 5, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed);
            game.World.ShowRemoteArena(MiniJson.F(ld, 0, 24f), MiniJson.F(ld, 1, 14f), (ArenaShape)MiniJson.I(ld, 2), MiniJson.I(ld, 3),
                                       MiniJson.I(ld, 4), seed, MiniJson.B(ld, 6));
            game.Vfx.Clear();
            game.Vfx.ClearDecals();
            game.Vfx.StainBounds = game.World.Bounds;
            View.OnRoom();
            shownValid = false;
            moves.Clear();
        }

        void OnRoute(Dictionary<string, object> m)
        {
            Doors.Clear();
            DoorsAhead.Clear();
            MyVote = -1;
            List<object> o = MiniJson.L(m, "o");
            if (o != null) foreach (object n in o) Doors.Add(DoorOf(n as List<object>));
            List<object> ahead = MiniJson.L(m, "ahead");
            if (ahead != null)
            {
                foreach (object layer in ahead)
                {
                    var row = new List<Door>();
                    if (layer is List<object> l) foreach (object n in l) row.Add(DoorOf(n as List<object>));
                    DoorsAhead.Add(row);
                }
            }
            game.UI.OnRemoteCards();
        }

        static Door DoorOf(List<object> n) => new Door
        {
            kind = (RoomKind)MiniJson.I(n, 0), glyph = MiniJson.S(n, 1), text = MiniJson.S(n, 2), kindText = MiniJson.S(n, 3),
            color = MiniJson.Color(MiniJson.S(n, 4), Color.white), column = MiniJson.I(n, 5, 1),
        };

        void OnInventory(Dictionary<string, object> m)
        {
            HasInventory = true;
            List<object> eq = MiniJson.L(m, "eq");
            for (int i = 0; i < Worn.Length; i++) Worn[i] = eq != null && i < eq.Count ? GearOf(eq[i]) : null;
            List<object> slots = MiniJson.L(m, "slots");
            for (int i = 0; i < SlotNames.Length; i++) SlotNames[i] = MiniJson.S(slots, i, "");
            Bag.Clear();
            List<object> bag = MiniJson.L(m, "bag");
            if (bag != null) foreach (object o in bag) Bag.Add(GearOf(o));
            Owned.Clear();
            List<object> own = MiniJson.L(m, "own");
            if (own != null) foreach (object o in own) Owned.Add(MiniJson.S(o as List<object>, 0));
            List<object> st = MiniJson.L(m, "st");
            for (int i = 0; i < Stats.Length; i++) Stats[i] = MiniJson.I(st, i);
        }

        static Gear GearOf(object o)
        {
            if (!(o is Dictionary<string, object> d)) return null;
            return new Gear
            {
                name = MiniJson.S(d, "n"), rarity = MiniJson.S(d, "r"), color = MiniJson.Color(MiniJson.S(d, "rc"), Color.white),
                slot = MiniJson.I(d, "s"), gain = MiniJson.S(d, "g"),
            };
        }

        void OnSnapshot(Dictionary<string, object> d)
        {
            var s = new Snap
            {
                d = d,
                seq = MiniJson.I(d, "q"),
                hostMs = d.TryGetValue("ht", out object ht) && ht is double h ? h : NowMs,
                mode = MiniJson.S(d, "m", "lobby"),
                P = MiniJson.L(d, "P"), E = MiniJson.L(d, "E"), R = MiniJson.L(d, "R"), B = MiniJson.L(d, "B"), I = MiniJson.L(d, "I"),
                T = MiniJson.L(d, "T"), O = MiniJson.L(d, "O"), Z = MiniJson.L(d, "Z"), PR = MiniJson.L(d, "PR"), SB = MiniJson.L(d, "SB"),
                LB = MiniJson.L(d, "LB"), env = MiniJson.L(d, "env"), ch = MiniJson.L(d, "ch"), enc = MiniJson.L(d, "enc"),
                so = MiniJson.L(d, "so"), rt = MiniJson.L(d, "rt"),
            };
            if (s.P != null)
            {
                foreach (object o in s.P)
                {
                    if (o is List<object> row) s.players[MiniJson.I(row, 0)] = row;
                }
            }
            if (s.E != null)
            {
                foreach (object o in s.E)
                {
                    if (o is List<object> row) s.enemies[MiniJson.I(row, 0)] = row;
                }
            }

            // The host's clock: the fastest path seen is the true offset; it relaxes slowly.
            double sample = NowMs - s.hostMs;
            if (double.IsNaN(clockOffset) || sample < clockOffset) clockOffset = sample;
            else clockOffset += (sample - clockOffset) * 0.003;
            jitterMs += ((sample - clockOffset) - jitterMs) * 0.05;

            // Moments: this duelist's own at once, the rest when their moment is drawn.
            List<object> events = MiniJson.L(d, "F");
            if (events != null)
            {
                foreach (object o in events)
                {
                    if (!(o is List<object> ev)) continue;
                    if (IsMine(ev)) Fire(ev);
                    else pending.Add(new Pending { hostMs = s.hostMs, ev = ev });
                }
            }

            Snap last = Latest;
            if (last != null && s.seq <= last.seq && s.seq != 0) return;
            if (last != null) LearnSpeed(last, s);
            snaps.Add(s);
            if (snaps.Count > MaxSnaps) snaps.RemoveAt(0);
            OnModeSeen(s.mode);
        }

        /// <summary>My blade finished an enemy: my own weapon mastery grows.</summary>
        void RecordMyFinisher()
        {
            Snap s = Latest;
            if (s == null || !s.players.TryGetValue(MyId, out List<object> row)) return;
            string weapon = MiniJson.S(row, 61);
            if (!string.IsNullOrEmpty(weapon)) game.RecordGuestFinisher(WeaponDefs.Get(weapon).BaseId);
        }

        bool IsMine(List<object> ev)
        {
            string kind = MiniJson.S(ev, 0);
            if (kind == "toast" || kind == "hint") return true;
            int actorIndex = kind == "fx" ? 8 : kind == "pop" ? 5 : kind == "gore" ? 8 : -1;
            return actorIndex >= 0 && MiniJson.I(ev, actorIndex, -1) == MyId;
        }

        bool inRun;
        int runEcos;

        /// <summary>The Ecos of the run go to this player's own Altar das Almas, once.</summary>
        void BankRun()
        {
            inRun = false;
            int ecos = runEcos;
            runEcos = 0;
            game.BankGuestRun(ecos);
        }

        void OnModeSeen(string mode)
        {
            Snap latest = Latest;
            bool runMode = mode != "lobby" && mode != "over";
            if (runMode && latest != null && latest.d != null && latest.d.ContainsKey("ec"))
            {
                inRun = true;
                runEcos = Math.Max(runEcos, MiniJson.I(latest.d, "ec"));
            }
            if (mode == lastMode) return;
            string was = lastMode;
            lastMode = mode;
            if ((mode == "lobby" || mode == "over") && inRun) BankRun();
            if (mode == "play" && (was == "lobby" || was == "over"))
            {
                // A run starts: a clean log and floor, ready again next time.
                Ready = false;
                game.UI.ClearLog();
                game.Vfx.ClearDecals();
                Send("{\"t\":\"inv\"}");
            }
            if (mode == "lobby" || mode == "over")
            {
                Cards.Clear();
                Doors.Clear();
                if (was != null && was != "lobby" && was != "over") Ready = false;
            }
            if (mode != "reward") Cards.Clear();
        }

        // ── Moments ─────────────────────────────────────────────────────────────────────

        void FirePending(double renderMs)
        {
            int n = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                // A little early: the eye forgives a spark before the blow far less than after.
                if (pending[i].hostMs > renderMs + 10.0) break;
                Fire(pending[i].ev);
                n++;
            }
            if (n > 0) pending.RemoveRange(0, n);
            // Never let moments pile up behind a stalled clock.
            if (pending.Count > 400) pending.RemoveRange(0, pending.Count - 400);
        }

        void Fire(List<object> e)
        {
            FeedbackRouter fx = game.Feedback;
            Palette pal = game.Config.palette;
            try
            {
                switch (MiniJson.S(e, 0))
                {
                    case "fx":
                        if (!Enum.TryParse(MiniJson.S(e, 1), out FxType type)) return;
                        if (type == FxType.Finisher && MyId >= 0 && MiniJson.I(e, 8, -1) == MyId) RecordMyFinisher();
                        fx.EmitAs(MiniJson.I(e, 8, -1), type, new Vector2(MiniJson.F(e, 2), MiniJson.F(e, 3)), new Vector2(MiniJson.F(e, 4), MiniJson.F(e, 5)),
                                  MiniJson.F(e, 6, 1f), MiniJson.Color(MiniJson.S(e, 7), Color.white));
                        break;
                    case "pop":
                        game.UI.Popup(MiniJson.S(e, 1), new Vector2(MiniJson.F(e, 2), MiniJson.F(e, 3)), MiniJson.Color(MiniJson.S(e, 4), Color.white));
                        break;
                    case "toast":
                        game.UI.Toast(MiniJson.S(e, 1), MiniJson.Color(MiniJson.S(e, 2), pal.uiText));
                        break;
                    case "hint":
                        fx.Hint((HintId)MiniJson.I(e, 1));
                        break;
                    case "blood":
                        fx.Blood(new Vector2(MiniJson.F(e, 1), MiniJson.F(e, 2)), new Vector2(MiniJson.F(e, 3), MiniJson.F(e, 4)), MiniJson.F(e, 5),
                                 MiniJson.Color(MiniJson.S(e, 6), BloodRed));
                        break;
                    case "gore":
                    {
                        if (!Enum.TryParse(MiniJson.S(e, 1), out FinisherStyle style)) return;
                        var pos = new Vector2(MiniJson.F(e, 2), MiniJson.F(e, 3));
                        Sprite body = View.BodyNear(pos);
                        fx.GoreAs(MiniJson.I(e, 8, -1), style, pos, new Vector2(MiniJson.F(e, 4), MiniJson.F(e, 5)), body, Color.white,
                                  MiniJson.Color(MiniJson.S(e, 6), BloodRed), MiniJson.F(e, 7, 0.45f));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Shura][Remoto] efeito ignorado: " + ex.Message);
            }
        }

        // ── Timeline ────────────────────────────────────────────────────────────────────

        /// <summary>The host time being drawn: now, minus the way here, minus just enough buffer for the jitter.</summary>
        double RenderHostMs()
        {
            if (double.IsNaN(clockOffset)) return Latest != null ? Latest.hostMs : 0.0;
            double delay = 1000.0 / 45.0 * 1.1 + Math.Max(0.0, jitterMs) * 1.5 + 4.0;
            delay = Math.Max(16.0, Math.Min(150.0, delay));
            return NowMs - clockOffset - delay;
        }

        public float InterpolationDelayMs
        {
            get
            {
                double delay = 1000.0 / 45.0 * 1.1 + Math.Max(0.0, jitterMs) * 1.5 + 4.0;
                return (float)Math.Max(16.0, Math.Min(150.0, delay));
            }
        }

        void Frame(double renderMs, out Snap a, out Snap b, out float t)
        {
            a = b = snaps[snaps.Count - 1];
            t = 1f;
            for (int i = snaps.Count - 1; i > 0; i--)
            {
                if (snaps[i - 1].hostMs <= renderMs)
                {
                    a = snaps[i - 1];
                    b = snaps[i];
                    double span = b.hostMs - a.hostMs;
                    // Past the newest: hold a little extrapolation (up to half a snapshot) and stop.
                    t = span > 0.0 ? (float)Math.Min(1.5, Math.Max(0.0, (renderMs - a.hostMs) / span)) : 1f;
                    return;
                }
            }
            if (snaps.Count > 1)
            {
                // Older than everything kept: draw the oldest.
                a = b = snaps[0];
                t = 0f;
            }
        }

        // ── This duelist: prediction ────────────────────────────────────────────────────

        /// <summary>How fast this duelist walks on the host, learned from what the host shows.</summary>
        void LearnSpeed(Snap a, Snap b)
        {
            if (MyId < 0 || !a.players.TryGetValue(MyId, out List<object> ra) || !b.players.TryGetValue(MyId, out List<object> rb)) return;
            double dt = (b.hostMs - a.hostMs) / 1000.0;
            if (dt <= 0.005 || dt > 0.2) return;
            if (MiniJson.I(rb, 59) != (int)PlayerController.State.Free || !MiniJson.B(rb, 21)) return;
            Vector2 move = LastMove;
            if (move.magnitude < 0.9f) return;
            float speed = new Vector2(MiniJson.F(rb, 3) - MiniJson.F(ra, 3), MiniJson.F(rb, 4) - MiniJson.F(ra, 4)).magnitude / (float)dt;
            if (speed < 0.5f || speed > 20f) return;
            speedEstimate = Mathf.Lerp(speedEstimate, speed, 0.2f);
        }

        Vector2 LastMove => moves.Count > 0 ? moves[moves.Count - 1].move : Vector2.zero;

        /// <summary>
        /// The host's last word on where this duelist stands, plus every step taken here that
        /// the host cannot have shown yet (the last round trip's worth), eased so corrections glide.
        /// </summary>
        void UpdateOwn(float dt)
        {
            List<object> me = Me;
            if (me == null)
            {
                shownValid = false;
                return;
            }
            var server = new Vector2(MiniJson.F(me, 3), MiniJson.F(me, 4));
            bool free = MiniJson.B(me, 21) && MiniJson.I(me, 59) == (int)PlayerController.State.Free && !Between;
            Vector2 predicted = server;
            if (free)
            {
                double horizon = Math.Min(PredictionCap, Ping + 1000.0 / 45.0 / 1000.0 + 0.008) * 1000.0;
                double now = NowMs, from = now - horizon;
                Vector2 sum = Vector2.zero;
                for (int i = moves.Count - 1; i >= 0; i--)
                {
                    double t1 = i == moves.Count - 1 ? now : moves[i + 1].at;
                    double t0 = Math.Max(from, moves[i].at);
                    if (t1 <= from) break;
                    if (t1 > t0) sum += moves[i].move * (float)((t1 - t0) / 1000.0);
                }
                predicted = server + sum * speedEstimate;
                if (game.World.Layout != null) predicted = game.World.Layout.NearestFloor(predicted, PlayerRadius);
            }
            if (!shownValid || (predicted - shownOwn).sqrMagnitude > 9f)
            {
                shownOwn = predicted;
                shownValid = true;
                return;
            }
            // Glide toward the prediction; snapshot steps and corrections never pop.
            shownOwn = Vector2.Lerp(shownOwn, predicted, 1f - Mathf.Exp(-28f * dt));
        }

        // ── Input ───────────────────────────────────────────────────────────────────────

        /// <summary>Where this duelist aims, from the drawn (predicted) position.</summary>
        public Vector2 Aim { get; private set; } = Vector2.right;

        /// <summary>The guest's own target lock, on the host's enemies as the snapshot shows them.</summary>
        public readonly TargetLock Lock = new TargetLock();
        int seenToggles = -1, seenFlicks = -1, seenNext = -1, seenPrev = -1;

        /// <summary>Where the locked enemy stands (null = no lock).</summary>
        public Vector2? LockPosition => Lock.Active ? Lock.TargetPos : (Vector2?)null;

        void TickLock(InputService c, bool blocked)
        {
            if (seenToggles < 0)
            {
                seenToggles = c.LockToggles;
                seenFlicks = c.LockFlicks;
                seenNext = c.LockCycleNext;
                seenPrev = c.LockCyclePrev;
            }
            Lock.Candidates.Clear();
            Snap s = Latest;
            if (s != null)
            {
                foreach (KeyValuePair<int, List<object>> e in s.enemies)
                {
                    List<object> row = e.Value;
                    Lock.Candidates.Add(new LockCandidate { id = e.Key, pos = new Vector2(MiniJson.F(row, 2), MiniJson.F(row, 3)) });
                }
            }
            Vector2 me = OwnPosition;
            if (!blocked)
            {
                Vector2 look = c.Move.sqrMagnitude > 0.1f ? c.Move.normalized : Aim;
                Lock.ApplyPolicy(c.UsingGamepad, c.MouseLockAllowed, c.LockToggles != seenToggles, me, look);
                if (c.LockFlicks != seenFlicks && Lock.Active) Lock.Switch(me, c.LastFlick);
                if (c.LockCycleNext != seenNext) Lock.Cycle(me, 1);
                if (c.LockCyclePrev != seenPrev) Lock.Cycle(me, -1);
            }
            seenToggles = c.LockToggles;
            seenFlicks = c.LockFlicks;
            seenNext = c.LockCycleNext;
            seenPrev = c.LockCyclePrev;
            if (Between || s == null) Lock.Release();
            else Lock.Refresh(me);
            c.LockEngaged = Lock.Active;
        }

        void SendInput(float dt)
        {
            InputService c = game.Controls;
            bool blocked = InputBlocked || Between || game.CurrentMode != Game.Mode.Remote;
            Vector2 move = blocked ? Vector2.zero : (game.Launch.Automated ? botMove : c.Move);
            double now = NowMs;
            moves.Add(new MoveSample { at = now, move = move });
            while (moves.Count > 2 && moves[1].at < now - 1000.0) moves.RemoveAt(0);

            Vector2 aim = Aim;
            if (!game.Launch.Automated) TickLock(c, blocked);
            if (!blocked && Lock.Active && Lock.TargetPos != OwnPosition)
            {
                aim = (Lock.TargetPos - OwnPosition).normalized;
                if (!game.Launch.Automated)
                {
                    int k = c.TakeKamae();
                    if (k >= 0) kamae = k;
                }
            }
            else if (!blocked)
            {
                Vector2 a = game.Launch.Automated ? botAim : c.AimDirection(OwnPosition);
                if (a.sqrMagnitude > 0.01f) aim = a.normalized;
                else if (move.sqrMagnitude > 0.04f && c.UsingGamepad) aim = move.normalized;
                if (!game.Launch.Automated)
                {
                    int k = c.TakeKamae();
                    if (k >= 0) kamae = k;
                }
            }
            Aim = aim;
            int[] p = game.Launch.Automated ? botPresses : c.PressCounts;
            bool parry = !blocked && (game.Launch.Automated ? botParry : c.ParryHeld);
            bool revive = !blocked && !game.Launch.Automated && c.InteractHeld;
            bool held = !blocked && !game.Launch.Automated && c.AttackHeld;

            sb.Clear();
            sb.Append("{\"t\":\"in\",\"mx\":").Num(move.x, 3).Append(",\"my\":").Num(move.y, 3)
              .Append(",\"ax\":").Num(aim.x, 3).Append(",\"ay\":").Num(aim.y, 3)
              .Append(",\"p\":").Bool(parry).Append(",\"r\":").Bool(revive)
              .Append(",\"atk\":").Int(p[0]).Append(",\"par\":").Int(p[1]).Append(",\"dod\":").Int(p[2]).Append(",\"swp\":").Int(p[3])
              .Append(",\"det\":").Int(p[4]).Append(",\"kia\":").Int(p[5]).Append(",\"tl\":").Int(p[6]).Append(",\"tc\":").Int(p[7])
              .Append(",\"gd\":").Int(p[8]).Append(",\"bd\":").Int(p[9]).Append(",\"sou\":").Int(p[10]).Append(",\"km\":").Int(kamae).Append(",\"ah\":").Bool(held).Append('}');
            string msg = sb.ToString();
            inputTimer += dt;
            if (msg != lastInput)
            {
                // A change goes now and twice more: a lost packet costs nothing.
                resend = 2;
                lastInput = msg;
                inputTimer = 0f;
                Send(msg);
                return;
            }
            if (resend > 0 || inputTimer >= InputKeepAlive)
            {
                if (resend > 0) resend--;
                inputTimer = 0f;
                Send(msg);
            }
        }

        // ── Lobby and screens ───────────────────────────────────────────────────────────

        /// <summary>The settings' default name is the host's; a guest who never typed one becomes "Ronin N" there.</summary>
        string GuestName => game.Settings.playerName == new GameSettings().playerName ? string.Empty : game.Settings.playerName;

        void SendJoin()
        {
            GameSettings s = game.Settings;
            sb.Clear();
            sb.Append("{\"t\":\"join\",\"name\":").Str(GuestName).Append(",\"a\":").Str(s.Loadout(s.weaponA)).Append(",\"b\":").Str(s.Loadout(s.weaponB))
              .Append(",\"art\":").Int(s.playerArt).Append(",\"mt\":").Str(game.Meta.LevelsCode()).Append('}');
            Send(sb.ToString());
        }

        /// <summary>Name, look and blades changed here: the host updates this duelist.</summary>
        public void SendLoadout()
        {
            if (!Joined) return;
            GameSettings s = game.Settings;
            sb.Clear();
            sb.Append("{\"t\":\"loadout\",\"name\":").Str(GuestName).Append(",\"a\":").Str(s.Loadout(s.weaponA)).Append(",\"b\":").Str(s.Loadout(s.weaponB))
              .Append(",\"art\":").Int(s.playerArt).Append(",\"mt\":").Str(game.Meta.LevelsCode()).Append('}');
            Send(sb.ToString());
        }

        public void ToggleReady()
        {
            Ready = !Ready;
            Send("{\"t\":\"ready\",\"rd\":" + (Ready ? 1 : 0) + "}");
        }

        public void Pick(int index)
        {
            if (index < 0 || index >= Cards.Count) return;
            Send("{\"t\":\"pick\",\"i\":" + index + "}");
        }

        public void Vote(int index)
        {
            if (index < 0 || index >= Doors.Count) return;
            MyVote = index;
            Send("{\"t\":\"vote\",\"i\":" + index + "}");
        }

        public void Equip(int bagIndex) => Send("{\"t\":\"equip\",\"i\":" + bagIndex + "}");

        public void Unequip(int slot) => Send("{\"t\":\"unequip\",\"i\":" + slot + "}");

        void Send(string text) => link?.Send(text);

        // ── Automated guest (tests): stands ready, fights the nearest enemy, takes the first card ──

        Vector2 botMove, botAim = Vector2.right;
        bool botParry;
        readonly int[] botPresses = new int[11];
        int botTick;
        int botAnswered;

        void AutoPlay()
        {
            botTick++;
            Snap s = Latest;
            if (s == null) return;
            if (Between && !Ready && botTick % 30 == 0) ToggleReady();
            if (Cards.Count > 0 && botTick % 40 == 0) Pick(0);
            if (Doors.Count > 0 && MyVote < 0 && botTick % 40 == 0) Vote(0);
            botMove = Vector2.zero;
            botParry = false;
            if (s.mode != "play" || !HasOwn) return;
            Vector2 me = OwnPosition;
            List<object> best = null;
            float bestD = float.MaxValue;
            foreach (List<object> e in s.enemies.Values)
            {
                if (MiniJson.B(e, 32) || MiniJson.B(e, 38)) continue;
                float d = (new Vector2(MiniJson.F(e, 2), MiniJson.F(e, 3)) - me).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            if (best == null)
            {
                // An encounter: walk to the torii on the right.
                botMove = new Vector2(1f, 0f);
                return;
            }
            Vector2 to = new Vector2(MiniJson.F(best, 2), MiniJson.F(best, 3)) - me;
            botAim = to.sqrMagnitude > 0.01f ? to.normalized : botAim;
            float dist = to.magnitude;
            botMove = dist > 1.6f ? botAim : dist < 0.9f ? -botAim * 0.5f : Vector2.zero;
            // Answer the closed ring: parry the cyan, dodge the red.
            int ring = MiniJson.I(best, 29);
            if (ring == 2 && botTick - botAnswered > 20)
            {
                botAnswered = botTick;
                if (MiniJson.B(best, 28)) botPresses[2]++;
                else botPresses[1]++;
                botParry = true;
            }
            else if (botTick % 18 == 0 && dist < 2.2f) botPresses[0]++;
        }
    }
}
