using System;

namespace Shura
{
    /// <summary>What a duelist is doing in a PvP duel.</summary>
    public enum DuelAct : byte
    {
        Free,
        Windup,
        Active,
        Recovery,
        Dodge,
        /// <summary>Perfectly parried: open for a counter.</summary>
        Stagger,
        /// <summary>Posture broken: the next blow is a finisher.</summary>
        Broken,
        Hurt,
    }

    /// <summary>What just happened to a duelist (for the screen and the sound; never read by the rules).</summary>
    public enum DuelEvent : byte
    {
        None,
        Hit,
        Blocked,
        Parried,
        Dodged,
        Broken,
        Finished,
        Staggered,
        Swing,
    }

    /// <summary>One frame of a duelist's buttons, packed in 16 bits so it travels and compares cheaply.</summary>
    public static class DuelInput
    {
        public const ushort Attack = 1 << 4, Red = 1 << 5, Dodge = 1 << 6, ParryPress = 1 << 7, ParryHeld = 1 << 8;
        /// <summary>The press bits: a prediction never repeats them.</summary>
        public const ushort Presses = Attack | Red | Dodge | ParryPress;

        public static ushort Pack(int mx, int my, bool attack, bool red, bool dodge, bool parryPress, bool parryHeld)
        {
            int v = (Clamp(mx) + 1) | ((Clamp(my) + 1) << 2);
            if (attack) v |= Attack;
            if (red) v |= Red;
            if (dodge) v |= Dodge;
            if (parryPress) v |= ParryPress;
            if (parryHeld) v |= ParryHeld;
            return (ushort)v;
        }

        static int Clamp(int v) => v < -1 ? -1 : v > 1 ? 1 : v;
        public static int MoveX(ushort v) => (v & 3) - 1;
        public static int MoveY(ushort v) => ((v >> 2) & 3) - 1;
        public static bool Has(ushort v, ushort bit) => (v & bit) != 0;
        /// <summary>What the other side is guessed to do: same stick, same guard, no new presses.</summary>
        public static ushort Predict(ushort last) => (ushort)(last & ~Presses);
    }

    /// <summary>One blow of a duelist's weapon, in whole frames and millimetres.</summary>
    public struct DuelBlow
    {
        public int windup, active, recovery, damage, posture, range, cosHalfArc, lunge;
    }

    /// <summary>A duelist's fixed loadout for the whole duel: one weapon, one kamae (§9.6).</summary>
    public sealed class DuelSide
    {
        public string name = "?";
        public string weaponId = "katana";
        public int art;
        public int kamae = 1;
        public DuelBlow[] combo;
        public DuelBlow red;
        /// <summary>Walking speed in mm per frame.</summary>
        public int speed;
        public int perfectWindow;
        /// <summary>Damage in per-mille (1000 = as written).</summary>
        public int damageMille;

        public static DuelSide From(string name, WeaponDef w, int kamae, int art)
        {
            w = w ?? WeaponDefs.Katana;
            var s = new DuelSide { name = name, weaponId = w.id, art = art, kamae = kamae < 0 || kamae > 3 ? 1 : kamae };
            PlayerAttackDef[] c = w.combo != null && w.combo.Length > 0 ? w.combo : WeaponDefs.Katana.combo;
            s.combo = new DuelBlow[c.Length];
            for (int i = 0; i < c.Length; i++) s.combo[i] = Blow(c[i], false);
            s.red = Blow(w.swapStrike ?? c[c.Length - 1], true);
            s.speed = (int)Math.Round(70.0 * (w.moveMult > 0f ? w.moveMult : 1f));
            switch (s.kamae)
            {
                case 0: s.perfectWindow = 4; s.damageMille = 1250; break;   // Jōdan: harder, a narrower parry.
                case 2: s.perfectWindow = 8; s.damageMille = 900; break;    // Gedan: a wider parry, lighter blows.
                case 3: s.perfectWindow = 5; s.damageMille = 850; break;    // Waki: the blade hidden, rings late.
                default: s.perfectWindow = 6; s.damageMille = 1000; break;  // Chūdan.
            }
            return s;
        }

        /// <summary>The PvE numbers made readable for a human opponent: longer wind-ups, whole units.</summary>
        static DuelBlow Blow(PlayerAttackDef a, bool red)
        {
            double arc = a.shape.type == HitShapeType.Circle ? 360.0 : a.shape.type == HitShapeType.Line ? 30.0 : Math.Max(20.0, a.shape.arcDegrees);
            return new DuelBlow
            {
                windup = red ? 26 + a.windup * 2 : 12 + (int)Math.Round(a.windup * 1.5),
                active = Math.Max(3, a.active),
                recovery = a.recovery + (red ? 10 : 4),
                damage = (int)Math.Round(a.damage * 10.0 * (red ? 1.2 : 1.0)),
                posture = (int)Math.Round(a.posture * 10.0),
                range = (int)Math.Round(Math.Max(0.9, a.shape.range) * 1000.0),
                cosHalfArc = arc >= 359.0 ? -1000 : (int)Math.Round(Math.Cos(arc * 0.5 * Math.PI / 180.0) * 1000.0),
                lunge = (int)Math.Round((a.lunge + (red ? a.dash * 0.5f : 0f)) * 1000.0),
            };
        }
    }

    public struct DuelFighter
    {
        public int x, y;
        /// <summary>Facing ×1000 (always toward the opponent: a duel is a lock-on).</summary>
        public int fx, fy;
        /// <summary>The blow's direction ×1000, tracked during the wind-up, then locked.</summary>
        public int ax, ay;
        public DuelAct act;
        public int frame;
        public int health, posture;
        public int step;
        public bool red, hitDone, queued, guarding;
        public int parryAgo;
        public int dodgeCooldown, dodgeX, dodgeY;
        public int calm;
        public int rounds;
        public DuelEvent ev;
        public int evFrame;
    }

    /// <summary>The whole duel in one value: copying it is saving it (rollback).</summary>
    public struct DuelState
    {
        public int frame;
        public DuelFighter a, b;
        public int round;
        public int pause;
        public int roundTimer;
        /// <summary>-1 while the duel lasts, else 0 or 1; a draw is 2.</summary>
        public int winner;
        public int winFrame;
        /// <summary>Who took the last round (-1 none, 2 a draw), for the banner.</summary>
        public int lastRound;
    }

    /// <summary>
    /// A PvP duel (design doc §9.6) as a pure, deterministic step: integers only (millimetres,
    /// frames, per-mille), the same inputs always giving the same state on every machine — the
    /// ground rollback netcode stands on. Two rounds win; a broken posture invites the finisher.
    /// </summary>
    public static class DuelSim
    {
        public const int Health = 1000, PostureMax = 1000;
        public const int ArenaRadius = 4500, BodyRadius = 600;
        public const int DodgeFrames = 18, DodgeInvulnFrom = 2, DodgeInvulnTo = 12, DodgeSpeed = 120, DodgeCooldown = 16;
        public const int StaggerFrames = 36, HurtFrames = 14, BrokenFrames = 80;
        public const int RoundPause = 120, RoundFrames = 60 * 75, RoundsToWin = 2;
        public const int PerfectParryPosture = 250;

        public static DuelState Start()
        {
            var s = new DuelState { winner = -1, lastRound = -1, round = 1, pause = 60 };
            ResetRound(ref s);
            return s;
        }

        static void ResetRound(ref DuelState s)
        {
            s.roundTimer = RoundFrames;
            Place(ref s.a, -2200);
            Place(ref s.b, 2200);
        }

        static void Place(ref DuelFighter f, int x)
        {
            int rounds = f.rounds;
            f = new DuelFighter { x = x, y = 0, fx = x < 0 ? 1000 : -1000, health = Health, parryAgo = 999, rounds = rounds, ax = x < 0 ? 1000 : -1000 };
        }

        public static void Step(ref DuelState s, ushort ia, ushort ib, DuelSide sa, DuelSide sb)
        {
            if (s.winner >= 0) return;
            s.frame++;
            if (s.pause > 0)
            {
                if (--s.pause == 0 && s.lastRound >= 0 && s.lastRound != 3)
                {
                    s.round++;
                    ResetRound(ref s);
                    s.lastRound = 3;
                }
                return;
            }
            Think(ref s.a, s.b, ia, sa, s.frame);
            Think(ref s.b, s.a, ib, sb, s.frame);
            Strike(ref s.a, ref s.b, sa, sb, s.frame);
            Strike(ref s.b, ref s.a, sb, sa, s.frame);
            Separate(ref s.a, ref s.b);
            Bound(ref s.a);
            Bound(ref s.b);
            Face(ref s.a, s.b);
            Face(ref s.b, s.a);
            if (--s.roundTimer <= 0) EndRound(ref s, s.a.health > s.b.health ? 0 : s.b.health > s.a.health ? 1 : 2);
            else if (s.a.health <= 0 || s.b.health <= 0) EndRound(ref s, s.a.health <= 0 && s.b.health <= 0 ? 2 : s.a.health <= 0 ? 1 : 0);
        }

        static void EndRound(ref DuelState s, int who)
        {
            if (who == 0) s.a.rounds++;
            if (who == 1) s.b.rounds++;
            s.lastRound = who;
            s.pause = RoundPause;
            if (s.a.rounds >= RoundsToWin || s.b.rounds >= RoundsToWin)
            {
                s.winner = s.a.rounds >= RoundsToWin && s.b.rounds >= RoundsToWin ? 2 : s.a.rounds >= RoundsToWin ? 0 : 1;
                s.winFrame = s.frame;
            }
        }

        // ── One duelist's frame ──────────────────────────────────────────────────────

        static void Think(ref DuelFighter f, DuelFighter o, ushort input, DuelSide side, int now)
        {
            f.frame++;
            f.parryAgo = DuelInput.Has(input, DuelInput.ParryPress) ? 0 : Math.Min(999, f.parryAgo + 1);
            if (f.dodgeCooldown > 0) f.dodgeCooldown--;
            f.calm++;
            if (f.calm > 60 && f.posture > 0 && f.act != DuelAct.Broken) f.posture = Math.Max(0, f.posture - 3);
            int mx = DuelInput.MoveX(input), my = DuelInput.MoveY(input);
            bool attack = DuelInput.Has(input, DuelInput.Attack), red = DuelInput.Has(input, DuelInput.Red);
            bool dodge = DuelInput.Has(input, DuelInput.Dodge);
            f.guarding = false;

            switch (f.act)
            {
                case DuelAct.Free:
                    if (dodge && f.dodgeCooldown == 0)
                    {
                        StartDodge(ref f, o, mx, my);
                        break;
                    }
                    if (attack || red)
                    {
                        StartBlow(ref f, o, red ? -1 : 0);
                        break;
                    }
                    f.guarding = DuelInput.Has(input, DuelInput.ParryHeld);
                    Walk(ref f, mx, my, f.guarding ? side.speed / 2 : side.speed);
                    break;
                case DuelAct.Windup:
                    Blow(f, side, out DuelBlow wb);
                    // The blade follows the opponent for two thirds of the wind-up, then commits.
                    if (f.frame < wb.windup * 2 / 3) Toward(f.x, f.y, o.x, o.y, out f.ax, out f.ay);
                    if (f.frame >= wb.windup) Enter(ref f, DuelAct.Active);
                    break;
                case DuelAct.Active:
                    Blow(f, side, out DuelBlow ab);
                    int push = ab.lunge / Math.Max(1, ab.active);
                    f.x += f.ax * push / 1000;
                    f.y += f.ay * push / 1000;
                    if (attack && !f.red) f.queued = true;
                    if (f.frame >= ab.active) Enter(ref f, DuelAct.Recovery);
                    break;
                case DuelAct.Recovery:
                    Blow(f, side, out DuelBlow rb);
                    if (attack && !f.red) f.queued = true;
                    // Half-way through the recovery a dodge may cut it short.
                    if (dodge && f.dodgeCooldown == 0 && f.frame * 2 >= rb.recovery)
                    {
                        StartDodge(ref f, o, mx, my);
                        break;
                    }
                    if (f.frame >= rb.recovery)
                    {
                        if (f.queued && !f.red && f.step + 1 < side.combo.Length) StartBlow(ref f, o, f.step + 1);
                        else
                        {
                            Enter(ref f, DuelAct.Free);
                            f.step = 0;
                        }
                    }
                    break;
                case DuelAct.Dodge:
                    if (f.frame <= 12)
                    {
                        f.x += f.dodgeX * DodgeSpeed / 1000;
                        f.y += f.dodgeY * DodgeSpeed / 1000;
                    }
                    if (f.frame >= DodgeFrames)
                    {
                        Enter(ref f, DuelAct.Free);
                        f.dodgeCooldown = DodgeCooldown;
                    }
                    break;
                case DuelAct.Stagger:
                    if (f.frame >= StaggerFrames) Enter(ref f, DuelAct.Free);
                    break;
                case DuelAct.Hurt:
                    if (f.frame >= HurtFrames) Enter(ref f, DuelAct.Free);
                    break;
                case DuelAct.Broken:
                    if (f.frame >= BrokenFrames)
                    {
                        Enter(ref f, DuelAct.Free);
                        f.posture = 0;
                    }
                    break;
            }
        }

        static void Enter(ref DuelFighter f, DuelAct act)
        {
            f.act = act;
            f.frame = 0;
        }

        static void StartBlow(ref DuelFighter f, DuelFighter o, int step)
        {
            f.red = step < 0;
            f.step = step < 0 ? 0 : step;
            f.queued = false;
            f.hitDone = false;
            Toward(f.x, f.y, o.x, o.y, out f.ax, out f.ay);
            Enter(ref f, DuelAct.Windup);
        }

        static void StartDodge(ref DuelFighter f, DuelFighter o, int mx, int my)
        {
            if (mx == 0 && my == 0)
            {
                // No direction: a step back, away from the opponent.
                Toward(o.x, o.y, f.x, f.y, out f.dodgeX, out f.dodgeY);
            }
            else
            {
                Unit(mx, my, out f.dodgeX, out f.dodgeY);
            }
            Enter(ref f, DuelAct.Dodge);
        }

        static void Walk(ref DuelFighter f, int mx, int my, int speed)
        {
            if (mx == 0 && my == 0) return;
            Unit(mx, my, out int ux, out int uy);
            f.x += ux * speed / 1000;
            f.y += uy * speed / 1000;
        }

        static void Unit(int mx, int my, out int ux, out int uy)
        {
            bool diag = mx != 0 && my != 0;
            ux = mx * (diag ? 707 : 1000);
            uy = my * (diag ? 707 : 1000);
        }

        public static void Blow(DuelFighter f, DuelSide side, out DuelBlow b)
        {
            b = f.red ? side.red : side.combo[Math.Min(f.step, side.combo.Length - 1)];
        }

        // ── Blows landing ────────────────────────────────────────────────────────────

        static void Strike(ref DuelFighter at, ref DuelFighter d, DuelSide sa, DuelSide sd, int now)
        {
            if (at.act != DuelAct.Active || at.hitDone) return;
            Blow(at, sa, out DuelBlow b);
            long dx = d.x - at.x, dy = d.y - at.y;
            long dist = ISqrt(dx * dx + dy * dy);
            if (dist > b.range + BodyRadius) return;
            if (b.cosHalfArc > -1000 && dist > 0)
            {
                long dot = (at.ax * dx + at.ay * dy) / dist;
                if (dot < b.cosHalfArc) return;
            }
            at.hitDone = true;
            int damage = b.damage * sa.damageMille / 1000;

            if (d.act == DuelAct.Dodge && d.frame >= DodgeInvulnFrom && d.frame <= DodgeInvulnTo)
            {
                Mark(ref d, DuelEvent.Dodged, now);
                return;
            }
            bool canParry = d.act == DuelAct.Free || d.act == DuelAct.Recovery;
            if (!at.red && canParry && d.parryAgo <= sd.perfectWindow)
            {
                // Perfect: the attacker's blade bounces, their posture cracks and their guard opens.
                Mark(ref d, DuelEvent.Parried, now);
                at.posture += PerfectParryPosture;
                at.calm = 0;
                Enter(ref at, DuelAct.Stagger);
                Mark(ref at, DuelEvent.Staggered, now);
                if (at.posture >= PostureMax) Break(ref at, now);
                return;
            }
            if (d.guarding && d.act == DuelAct.Free)
            {
                // Blocked: the posture pays; a red blow pays far more.
                d.posture += at.red ? b.posture * 2 + 300 : b.posture * 12 / 10;
                d.health -= at.red ? damage / 2 : damage / 10;
                d.calm = 0;
                Mark(ref d, DuelEvent.Blocked, now);
                if (d.posture >= PostureMax) Break(ref d, now);
                return;
            }
            if (d.act == DuelAct.Broken)
            {
                d.health = 0;
                Mark(ref d, DuelEvent.Finished, now);
                return;
            }
            d.health -= damage;
            d.posture += b.posture / 2;
            d.calm = 0;
            Mark(ref d, DuelEvent.Hit, now);
            if (d.posture >= PostureMax) Break(ref d, now);
            else if (d.act != DuelAct.Windup || !d.red) Enter(ref d, DuelAct.Hurt);
        }

        static void Break(ref DuelFighter f, int now)
        {
            f.posture = PostureMax;
            Enter(ref f, DuelAct.Broken);
            Mark(ref f, DuelEvent.Broken, now);
        }

        static void Mark(ref DuelFighter f, DuelEvent e, int now)
        {
            f.ev = e;
            f.evFrame = now;
        }

        // ── Space ────────────────────────────────────────────────────────────────────

        static void Separate(ref DuelFighter a, ref DuelFighter b)
        {
            long dx = b.x - a.x, dy = b.y - a.y;
            long d = ISqrt(dx * dx + dy * dy);
            const int min = BodyRadius * 2;
            if (d >= min) return;
            if (d == 0)
            {
                dx = 1;
                d = 1;
            }
            int push = (int)((min - d) / 2 + 1);
            int ux = (int)(dx * 1000 / d), uy = (int)(dy * 1000 / d);
            a.x -= ux * push / 1000;
            a.y -= uy * push / 1000;
            b.x += ux * push / 1000;
            b.y += uy * push / 1000;
        }

        static void Bound(ref DuelFighter f)
        {
            long d = ISqrt((long)f.x * f.x + (long)f.y * f.y);
            const int max = ArenaRadius - BodyRadius;
            if (d <= max || d == 0) return;
            f.x = (int)(f.x * max / d);
            f.y = (int)(f.y * max / d);
        }

        static void Face(ref DuelFighter f, DuelFighter o) => Toward(f.x, f.y, o.x, o.y, out f.fx, out f.fy);

        static void Toward(int x, int y, int tx, int ty, out int ux, out int uy)
        {
            long dx = tx - x, dy = ty - y;
            long d = ISqrt(dx * dx + dy * dy);
            if (d == 0)
            {
                ux = 1000;
                uy = 0;
                return;
            }
            ux = (int)(dx * 1000 / d);
            uy = (int)(dy * 1000 / d);
        }

        /// <summary>Integer square root (no floating point, so every machine agrees).</summary>
        public static long ISqrt(long n)
        {
            if (n <= 0) return 0;
            long x = (long)Math.Sqrt(n);
            while (x * x > n) x--;
            while ((x + 1) * (x + 1) <= n) x++;
            return x;
        }

        // ── Agreement ────────────────────────────────────────────────────────────────

        /// <summary>A fingerprint of the state, compared by both sides to catch a desync.</summary>
        public static uint Hash(in DuelState s)
        {
            uint h = 2166136261;
            void Mix(int v)
            {
                unchecked
                {
                    h ^= (uint)v;
                    h *= 16777619;
                }
            }
            Mix(s.frame); Mix(s.round); Mix(s.pause); Mix(s.roundTimer); Mix(s.winner);
            foreach (DuelFighter f in new[] { s.a, s.b })
            {
                Mix(f.x); Mix(f.y); Mix(f.ax); Mix(f.ay); Mix((int)f.act); Mix(f.frame); Mix(f.health); Mix(f.posture);
                Mix(f.step); Mix(f.red ? 1 : 0); Mix(f.hitDone ? 1 : 0); Mix(f.queued ? 1 : 0); Mix(f.parryAgo);
                Mix(f.dodgeCooldown); Mix(f.calm); Mix(f.rounds);
            }
            return h;
        }
    }
}
