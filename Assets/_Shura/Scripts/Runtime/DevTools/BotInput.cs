using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A scripted duelist used only for automated verification. It reads enemy frame data
    /// directly and gives each attack its Perfect Answer, which exercises the whole loop:
    /// parry → posture break → finisher → absorption → reward → next room, including arrows,
    /// grabs and teleports. It is deliberately not "fun" or human-like.
    /// </summary>
    public sealed class BotInput : IPlayerInputSource
    {
        const int AnswerLeadFrames = 3;
        const float CloseRange = 1.3f;

        Vector2 aim = Vector2.right;
        object lastAnsweredKey;
        int lastAnsweredSerial = -1;
        int attackCooldown;
        int answers;

        public Vector2 Move { get; private set; }
        public bool ParryHeld { get; private set; }
        /// <summary>The bot always lends a hand to a fallen teammate it stands over.</summary>
        public bool InteractHeld => true;
        public InputBuffer Buffer { get; } = new InputBuffer();
        /// <summary>Held through a standoff, released the moment the enemy advances.</summary>
        public bool AttackHeld { get; private set; }

        int kamae = -1;
        /// <summary>Automated inspection: hold this guard instead of Chūdan.</summary>
        public int ForcedKamae = -1;
        int lingered;
        /// <summary>Frames to stand still on entering an encounter (screenshot tours).</summary>
        public int EncounterLinger { get; set; }
        readonly System.Collections.Generic.HashSet<Offering> used = new System.Collections.Generic.HashSet<Offering>();
        int sealCooldown;
        int toolCooldown;

        public Vector2 AimDirection(Vector2 playerWorldPosition) => aim;

        /// <summary>The bot settles into Chūdan once (exercising the guard swap) and stays there.</summary>
        public int TakeKamae()
        {
            int k = kamae;
            kamae = -1;
            return k;
        }

        bool settled;

        public void Think(World world)
        {
            PlayerController p = world.Player;
            layout = world.Layout;
            Move = Vector2.zero;
            ParryHeld = false;
            if (!p.IsAlive) return;
            if (attackCooldown > 0) attackCooldown--;
            if (sealCooldown > 0) sealCooldown--;
            if (toolCooldown > 0) toolCooldown--;
            if (!settled)
            {
                settled = true;
                kamae = ForcedKamae >= 0 ? ForcedKamae : (int)Kamae.Chudan;
            }

            // The Iai standoff: hold the draw, release on the real advance (never on a feint).
            if (world.Standoff.Active)
            {
                AttackHeld = !world.Standoff.Advancing || world.Standoff.AdvanceProgress < 0.25f;
                return;
            }
            AttackHeld = false;
            // Aiming a finishing cut: along the golden line.
            if (p.AimingCut)
            {
                float a = p.AimLineAngle * Mathf.Deg2Rad;
                aim = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                return;
            }

            if (AnswerProjectile(world, p)) return;
            if (EscapeBlasts(world, p)) return;

            Enemy threat = null;
            int soonest = int.MaxValue;
            foreach (Enemy e in world.Enemies)
            {
                AttackDef a = e.CurrentAttack;
                if (a == null || a.IsProjectile || !e.IsThreatening) continue;
                int until = a.windup - e.StepFrame;
                if (until < soonest)
                {
                    soonest = until;
                    threat = e;
                }
            }

            // A kept soul is spent as soon as there is a fight close by.
            if (p.SoulReady && threat != null) Buffer.Press(BufferedAction.Soul, 3);
            if (threat != null && AnswerThreat(p, threat, soonest)) return;

            // Drink when hurt and nothing is about to land.
            if (p.Health < p.MaxHealth * 0.4f && p.Sips > 0 && (threat == null || soonest > 40))
            {
                Buffer.Press(BufferedAction.Gourd, 3);
                return;
            }
            // Detonate a stack of seals.
            if (sealCooldown == 0)
            {
                foreach (Enemy e in world.Enemies)
                {
                    if (e.Seals >= 3 && Vector2.Distance(e.Position, p.Position) < 6f)
                    {
                        Buffer.Press(BufferedAction.Detonate, 3);
                        sealCooldown = 60;
                        break;
                    }
                }
            }
            // Throw a shuriken now and then at range.
            if (toolCooldown == 0 && p.Build.SpiritPapers > 2 && threat == null)
            {
                Buffer.Press(BufferedAction.Tool, 3);
                toolCooldown = 300;
            }

            // Encounter rooms: use one thing, then everyone to the torii.
            if (world.ActiveEnemyCount == 0 && world.Offerings.Count > 0)
            {
                // Screenshot tours look around first.
                if (lingered < EncounterLinger)
                {
                    lingered++;
                    return;
                }
                Offering goal = null;
                foreach (Offering o in world.Offerings)
                {
                    if (o.Exit) continue;
                    // Anything already used (even in passing) or refused counts as done.
                    if (o.UsedBy.Contains(p) || o.Refused.Contains(p)) used.Add(o);
                    if (!o.AvailableTo(p) || (o.Price > 0 && p.Build.Mon < o.Price) || used.Contains(o)) continue;
                    if (goal == null) goal = o;
                }
                if (goal == null || used.Count >= 1)
                {
                    foreach (Offering o in world.Offerings)
                    {
                        if (o.Exit) goal = o;
                    }
                }
                if (goal != null)
                {
                    if (Vector2.Distance(p.Position, goal.Position) > 0.5f) Move = layout != null ? layout.Steer(p.Position, goal.Position, p.Radius) : (goal.Position - p.Position).normalized;
                    else if (!goal.Exit && goal.UsedBy.Contains(p)) used.Add(goal);
                    if (!goal.Exit && (goal.Spent || goal.UsedBy.Contains(p))) used.Add(goal);
                }
                return;
            }
            if (world.Offerings.Count == 0)
            {
                used.Clear();
                lingered = 0;
            }

            Enemy broken = world.FindFinisherTarget(p.Position, 99f);
            if (broken != null)
            {
                Approach(p, broken.Position, 1.4f, true);
                return;
            }

            Enemy target = null;
            float best = float.MaxValue;
            foreach (Enemy e in world.Enemies)
            {
                if (!e.CanBeHit || e.IsAlly) continue;
                // Blades pass through an unexposed spirit: go for anyone solid first.
                float d = Vector2.Distance(e.Position, p.Position) + (e.IsIntangible ? 6f : 0f);
                if (d < best)
                {
                    best = d;
                    target = e;
                }
            }
            if (target == null) return;
            bool threatSoon = threat != null && soonest <= 14;
            Approach(p, target.Position, CloseRange, !threatSoon);
        }

        /// <summary>Walks out of a marked circle; dodges through the blast if it is about to go off.</summary>
        bool EscapeBlasts(World world, PlayerController p)
        {
            foreach (EnemyBlast b in world.Blasts)
            {
                Vector2 from = p.Position - b.Position;
                float d = from.magnitude;
                if (d > b.Radius + p.Radius + 0.3f) continue;
                Vector2 away = d > 1e-3f ? from / d : Vector2.right;
                Move = away;
                aim = away;
                if (b.Fuse - b.Age <= AnswerLeadFrames) Buffer.Press(BufferedAction.Dodge, 3);
                return true;
            }
            return false;
        }

        /// <summary>Parries the arrow about to land; returns true while one is incoming.</summary>
        bool AnswerProjectile(World world, PlayerController p)
        {
            Projectile incoming = null;
            float soonest = float.PositiveInfinity;
            foreach (Projectile pr in world.Projectiles)
            {
                if (pr.Reflected || pr.PassedPlayer) continue;
                float frames = pr.FramesToImpact(p);
                if (frames < soonest)
                {
                    soonest = frames;
                    incoming = pr;
                }
            }
            if (incoming == null || soonest > 12f) return false;
            aim = -incoming.Direction;
            if (soonest <= AnswerLeadFrames && !ReferenceEquals(lastAnsweredKey, incoming))
            {
                Buffer.Press(BufferedAction.Parry, 3);
                lastAnsweredKey = incoming;
                lastAnsweredSerial = -1;
            }
            return true;
        }

        /// <summary>Returns true when the threat is in reach and was handled this tick.</summary>
        bool AnswerThreat(PlayerController p, Enemy threat, int framesUntilActive)
        {
            AttackDef a = threat.CurrentAttack;
            Vector2 to = threat.Position - p.Position;
            float dist = to.magnitude;
            float reach = a.shape.range + a.lunge + p.Radius + 1f;
            if (dist > reach) return false;

            Vector2 dir = dist > 1e-3f ? to / dist : p.Facing;
            aim = dir;
            bool answered = ReferenceEquals(threat, lastAnsweredKey) && threat.AttackSerial == lastAnsweredSerial;
            if (answered || framesUntilActive > AnswerLeadFrames) return true;

            if (a.feint)
            {
                // A feint: do nothing and wait for the real blow.
            }
            else if (a.kind == AttackKind.Spirit)
            {
                // Steel does not stop a spirit: only the Troca Perfeita.
                Buffer.Press(BufferedAction.Swap, 3);
            }
            else if (a.Parryable)
            {
                // Every other parryable swing is answered with a Troca Perfeita to exercise it.
                Buffer.Press(++answers % 2 == 0 ? BufferedAction.Swap : BufferedAction.Parry, 3);
            }
            else
            {
                // Thrust: dodge into it (counter). Sweep and grab: dodge through the attacker.
                Move = dir;
                Buffer.Press(BufferedAction.Dodge, 3);
            }
            lastAnsweredKey = threat;
            lastAnsweredSerial = threat.AttackSerial;
            return true;
        }

        ArenaLayout layout;

        void Approach(PlayerController p, Vector2 target, float range, bool allowAttack)
        {
            Vector2 to = target - p.Position;
            float dist = to.magnitude;
            if (dist > 1e-3f) aim = to / dist;
            if (dist > range)
            {
                // Walk around statues and pillars instead of pressing into them.
                Move = layout != null ? layout.Steer(p.Position, target, p.Radius) : aim;
                return;
            }
            if (allowAttack && attackCooldown == 0)
            {
                Buffer.Press(BufferedAction.Attack, 4);
                attackCooldown = 12;
            }
        }
    }
}
