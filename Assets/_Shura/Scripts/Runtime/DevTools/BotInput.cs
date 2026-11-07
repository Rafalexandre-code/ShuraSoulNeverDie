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
        public InputBuffer Buffer { get; } = new InputBuffer();

        public Vector2 AimDirection(Vector2 playerWorldPosition) => aim;

        public void Think(World world)
        {
            PlayerController p = world.Player;
            Move = Vector2.zero;
            ParryHeld = false;
            if (!p.IsAlive) return;
            if (attackCooldown > 0) attackCooldown--;

            if (AnswerProjectile(world, p)) return;

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

            if (threat != null && AnswerThreat(p, threat, soonest)) return;

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
                if (!e.CanBeHit) continue;
                float d = Vector2.Distance(e.Position, p.Position);
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

            if (a.Parryable)
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

        void Approach(PlayerController p, Vector2 target, float range, bool allowAttack)
        {
            Vector2 to = target - p.Position;
            float dist = to.magnitude;
            if (dist > 1e-3f) aim = to / dist;
            if (dist > range)
            {
                Move = aim;
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
