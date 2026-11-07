using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The duelist. A frame-based state machine: every action has windup/active/recovery
    /// frames, defined cancel points and an input buffer, so timing is exact and learnable.
    /// </summary>
    public sealed class PlayerController : Fighter
    {
        public enum State
        {
            Free,
            Attack,
            Parry,
            Dodge,
            Hurt,
            GuardBroken,
            Finisher,
            Dead,
        }

        static readonly PlayerModifiers DefaultMods = new PlayerModifiers();

        PlayerTuning t;
        FlowTuning flowTuning;
        Palette pal;
        IPlayerInputSource input;
        RunState run;

        readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();
        Vector2 dodgeDir;
        int parryWindow;
        int framesSinceParryPress = 9999;
        int hurtInvuln;
        Enemy finisherTarget;

        SpriteRenderer parryArc, swingArc, aura, empowerGlow;

        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public int ComboIndex { get; private set; }
        public float DodgeCharges { get; private set; }
        public int MaxDodgeCharges { get; private set; }
        /// <summary>Frames left on a charged strike (set by the Lâmina Paciente technique).</summary>
        public int EmpoweredFrames { get; set; }
        public int HurtInvulnFrames => hurtInvuln;
        public int ParryWindowFrames => parryWindow;

        PlayerModifiers Mods => run != null ? run.Modifiers : DefaultMods;
        int FlowLevel => run != null ? run.Flow.Level : 0;

        public bool IsDodgeInvulnerable =>
            CurrentState == State.Dodge && StateFrame >= t.dodgeIFrameStart && StateFrame <= t.dodgeIFrameEnd;

        public int PerfectParryFrames => Mathf.Max(2, t.perfectParryFrames + Mods.perfectParryFrameBonus);

        public float PostureDealtMult =>
            Mods.postureMult * (FlowLevel >= 1 ? 1f + flowTuning.level1PostureBonus : 1f);

        public void Init(World owner, GameConfig config)
        {
            t = config.player;
            flowTuning = config.flow;
            pal = config.palette;
            Posture = new PostureMeter(t.maxPosture);
            MaxHealth = t.maxHealth;
            Health = MaxHealth;
            MaxDodgeCharges = t.dodgeCharges;
            DodgeCharges = MaxDodgeCharges;

            BuildView(owner, Shape.Circle, pal.player, t.radius);
            pointer.color = pal.playerAccent;

            parryArc = SpriteUtil.Make("ParryArc", transform, ShapeLibrary.Wedge(t.parryArcDegrees), pal.playerAccent, Sorting.Ground + 10);
            swingArc = SpriteUtil.Make("SwingArc", transform, ShapeLibrary.Wedge(t.attackArcDegrees), Color.white, Sorting.Ground + 11);
            aura = SpriteUtil.Make("FlowAura", transform, ShapeLibrary.Get(Shape.SoftCircle), pal.gold, Sorting.Shadow + 1);
            empowerGlow = SpriteUtil.Make("Empowered", transform, ShapeLibrary.Get(Shape.Ring), pal.gold, Sorting.Overlay - 1);
            parryArc.enabled = swingArc.enabled = aura.enabled = empowerGlow.enabled = false;
        }

        public void BeginRun(RunState runState, IPlayerInputSource source)
        {
            run = runState;
            input = source;
            ApplyStats(true);
            DodgeCharges = MaxDodgeCharges;
            EmpoweredFrames = 0;
            Facing = Vector2.right;
            Enter(State.Free);
        }

        public void ResetForRoom(Vector2 spawn)
        {
            Teleport(spawn);
            Facing = Vector2.right;
            Posture.Reset();
            hitThisSwing.Clear();
            finisherTarget = null;
            hurtInvuln = 0;
            input?.Buffer.Clear();
            if (CurrentState != State.Dead) Enter(State.Free);
        }

        /// <summary>Re-applies max values after rewards change; never heals unless asked.</summary>
        public void ApplyStats(bool fullHeal)
        {
            MaxHealth = Mathf.Max(1f, t.maxHealth + Mods.maxHealthBonus);
            Health = fullHeal ? MaxHealth : Mathf.Min(Health, MaxHealth);
            Posture.SetMax(t.maxPosture + Mods.maxPostureBonus);
            MaxDodgeCharges = Mathf.Max(1, t.dodgeCharges + Mods.dodgeChargeBonus);
            DodgeCharges = Mathf.Min(DodgeCharges, MaxDodgeCharges);
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void Tick()
        {
            BeginTick();
            if (CurrentState == State.Dead || input == null) return;

            StateFrame++;
            if (hurtInvuln > 0) hurtInvuln--;
            if (framesSinceParryPress < 9999) framesSinceParryPress++;
            if (EmpoweredFrames > 0) EmpoweredFrames--;
            RechargeDodge();
            RegenPosture();

            switch (CurrentState)
            {
                case State.Free: TickFree(); break;
                case State.Parry: TickParry(); break;
                case State.Attack: TickAttack(); break;
                case State.Dodge: TickDodge(); break;
                case State.Hurt: TickHurt(); break;
                case State.GuardBroken: TickGuardBroken(); break;
                case State.Finisher: TickFinisher(); break;
            }
            Integrate();
        }

        void Enter(State state)
        {
            CurrentState = state;
            StateFrame = 0;
        }

        // ── States ──────────────────────────────────────────────────────────────────────

        void TickFree()
        {
            UpdateFacing(false);
            if (TryStartDodge()) return;
            if (input.Buffer.Consume(BufferedAction.Parry))
            {
                StartParry();
                return;
            }
            if (input.Buffer.Consume(BufferedAction.Attack))
            {
                if (!TryStartFinisher()) StartAttack(0);
                return;
            }
            if (input.ParryHeld)
            {
                StartGuard();
                return;
            }
            Steer(input.Move * t.moveSpeed);
        }

        void TickParry()
        {
            UpdateFacing(true);
            if (TryStartDodge()) return;

            bool windowOver = StateFrame >= parryWindow;
            if (windowOver && input.Buffer.Consume(BufferedAction.Parry))
            {
                StartParry();
                return;
            }
            if (windowOver && input.Buffer.Consume(BufferedAction.Attack))
            {
                if (!TryStartFinisher()) StartAttack(0);
                return;
            }
            // A tap keeps its full perfect window even if released immediately.
            if (windowOver && !input.ParryHeld)
            {
                Enter(State.Free);
                return;
            }
            Steer(input.Move * t.moveSpeed * t.guardMoveMultiplier);
        }

        void TickAttack()
        {
            int windup = t.attackWindup;
            int activeEnd = windup + t.attackActive;
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.35f);

            if (StateFrame == windup)
            {
                world.Fx.Emit(FxType.PlayerSwing, Position, Facing);
                Squash(1.28f, 0.8f);
            }

            if (StateFrame >= windup && StateFrame < activeEnd)
            {
                Position += Facing * (t.attackLunge / t.attackActive);
                float damage = t.attackDamage * Mods.damageMult;
                float posture = t.attackPosture * PostureDealtMult * Mods.normalAttackPostureMult;
                world.PlayerStrike(HitShape.Arc(t.attackRange, t.attackArcDegrees), damage, posture, hitThisSwing);
            }

            if (StateFrame < activeEnd) return;

            int sinceActive = StateFrame - activeEnd;
            if (sinceActive >= t.attackCancelDelay)
            {
                if (TryStartDodge()) return;
                if (input.Buffer.Consume(BufferedAction.Parry))
                {
                    StartParry();
                    return;
                }
            }

            if (sinceActive >= t.comboChainDelay && ComboIndex + 1 < t.comboLength &&
                input.Buffer.Consume(BufferedAction.Attack))
            {
                if (!TryStartFinisher()) StartAttack(ComboIndex + 1);
                return;
            }

            if (StateFrame >= activeEnd + RecoveryFrames()) Enter(State.Free);
        }

        void TickDodge()
        {
            float prev = CombatMath.EaseOutQuad((StateFrame - 1) / (float)t.dodgeFrames);
            float now = CombatMath.EaseOutQuad(StateFrame / (float)t.dodgeFrames);
            Position += dodgeDir * t.dodgeDistance * (now - prev);

            if (StateFrame % 2 == 0) world.Fx.Emit(FxType.Afterimage, Position, Facing, Radius, pal.playerAccent);

            if (StateFrame >= t.dodgeFrames)
            {
                Enter(State.Free);
                Velocity = dodgeDir * t.moveSpeed * 0.6f;
            }
        }

        void TickHurt()
        {
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, CombatMath.Damp(14f, SimClock.Dt));
            if (StateFrame >= t.hurtStunFrames) Enter(State.Free);
        }

        void TickGuardBroken()
        {
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, CombatMath.Damp(10f, SimClock.Dt));
            if (StateFrame < t.guardBreakFrames) return;
            Posture.Reset();
            Enter(State.Free);
        }

        void TickFinisher()
        {
            if (finisherTarget != null && StateFrame < t.finisherImpactFrame)
            {
                Vector2 to = finisherTarget.Position - Position;
                float dist = to.magnitude;
                float contact = finisherTarget.Radius + Radius + 0.15f;
                if (dist > contact) Position += to / dist * Mathf.Min(dist - contact, 0.25f);
            }

            if (StateFrame == t.finisherImpactFrame && finisherTarget != null)
            {
                world.ExecuteFinisher(finisherTarget);
                finisherTarget = null;
                Squash(1.35f, 0.75f);
            }

            if (StateFrame >= t.finisherFrames) Enter(State.Free);
        }

        // ── Actions ─────────────────────────────────────────────────────────────────────

        void StartParry()
        {
            int window = PerfectParryFrames;
            if (framesSinceParryPress < t.parrySpamWindow) window = Mathf.Max(1, window / 2);
            framesSinceParryPress = 0;
            parryWindow = window;
            Enter(State.Parry);
            UpdateFacing(true);
            Squash(0.9f, 1.1f);
        }

        void StartGuard()
        {
            parryWindow = 0;
            Enter(State.Parry);
        }

        void StartAttack(int index)
        {
            ComboIndex = index;
            hitThisSwing.Clear();
            UpdateFacing(false);
            Enemy target = world.FindAssistTarget(Position, Facing, t.aimAssistDegrees, t.attackRange + 1.2f, false);
            if (target != null) Facing = (target.Position - Position).normalized;
            Velocity *= 0.3f;
            Enter(State.Attack);
            Squash(0.85f, 1.12f);
        }

        bool TryStartDodge()
        {
            if (!input.Buffer.Peek(BufferedAction.Dodge) || DodgeCharges < 1f) return false;
            input.Buffer.Consume(BufferedAction.Dodge);
            DodgeCharges -= 1f;
            Vector2 move = input.Move;
            dodgeDir = move.sqrMagnitude > 0.04f ? move.normalized : Facing;
            Velocity = Vector2.zero;
            Enter(State.Dodge);
            Squash(1.35f, 0.72f);
            world.Fx.Emit(FxType.Dodge, Position, dodgeDir);
            return true;
        }

        bool TryStartFinisher()
        {
            Enemy target = world.FindFinisherTarget(Position, t.finisherRange);
            if (target == null) return false;
            finisherTarget = target;
            target.BeginFinisher();
            Vector2 to = target.Position - Position;
            if (to.sqrMagnitude > 1e-4f) Facing = to.normalized;
            Velocity = Vector2.zero;
            Enter(State.Finisher);
            Squash(0.8f, 1.2f);
            return true;
        }

        int RecoveryFrames()
        {
            int recovery = t.attackRecovery + (ComboIndex == t.comboLength - 1 ? t.finalHitExtraRecovery : 0);
            return Mathf.Max(2, Mathf.RoundToInt(recovery * Mods.recoveryMult));
        }

        void UpdateFacing(bool parryAssist)
        {
            Vector2 aim = input.AimDirection(Position);
            if (aim.sqrMagnitude < 0.01f)
            {
                Vector2 move = input.Move;
                if (move.sqrMagnitude > 0.04f) aim = move.normalized;
            }
            if (aim.sqrMagnitude > 0.01f) Facing = aim.normalized;

            if (!parryAssist) return;
            // Magnetism toward whoever is swinging, so a slightly-off aim still guards correctly.
            Enemy threat = world.FindAssistTarget(Position, Facing, t.aimAssistDegrees, t.aimAssistRange, true);
            if (threat != null && threat.IsThreatening) Facing = (threat.Position - Position).normalized;
        }

        void Steer(Vector2 desired)
        {
            float tau = desired.sqrMagnitude >= Velocity.sqrMagnitude ? t.accelTime : t.decelTime;
            Velocity = Vector2.Lerp(Velocity, desired, CombatMath.Damp(3f / Mathf.Max(0.01f, tau), SimClock.Dt));
        }

        void RechargeDodge()
        {
            if (DodgeCharges >= MaxDodgeCharges) return;
            float seconds = t.dodgeRechargeSeconds * Mods.dodgeRechargeMult;
            if (FlowLevel >= 2) seconds /= flowTuning.level2DodgeRechargeSpeed;
            DodgeCharges = Mathf.Min(MaxDodgeCharges, DodgeCharges + SimClock.Dt / Mathf.Max(0.05f, seconds));
        }

        void RegenPosture()
        {
            if (CurrentState == State.GuardBroken) return;
            float rate = t.postureRegenPerSecond * (CurrentState == State.Parry ? 0.5f : 1f);
            Posture.Tick(rate, t.postureRegenDelay);
        }

        // ── Being attacked ──────────────────────────────────────────────────────────────

        public HitOutcome ReceiveAttack(Enemy attacker, AttackDef attack)
        {
            if (CurrentState == State.Dead) return HitOutcome.Ignored;

            Vector2 toAttacker = attacker.Position - Position;
            Vector2 toDir = toAttacker.sqrMagnitude > 1e-6f ? toAttacker.normalized : Facing;
            var defender = new DefenderState
            {
                dodgeIFrames = IsDodgeInvulnerable,
                hurtInvulnerable = hurtInvuln > 0 || CurrentState == State.Finisher,
                parryWindow = CurrentState == State.Parry && StateFrame < parryWindow,
                guarding = CurrentState == State.Parry,
                facing = Facing,
                toAttacker = toAttacker,
                parryArcDegrees = t.parryArcDegrees,
                dodgeDirection = CurrentState == State.Dodge ? dodgeDir : Vector2.zero,
                counterDot = t.counterThrustDotThreshold,
            };

            HitOutcome outcome = HitResolver.Resolve(attack, defender);
            Vector2 contact = Position + toDir * Radius;

            switch (outcome)
            {
                case HitOutcome.PerfectParry:
                    run.PerfectParries++;
                    world.TryMaster(attacker, SignatureAnswer.PerfectParry);
                    world.Fx.Emit(FxType.PerfectParry, contact, toDir);
                    Squash(0.8f, 1.2f);
                    attacker.OnParried(attack.postureOnParried * PostureDealtMult * Mods.perfectParryPostureMult);
                    world.AddFlow(flowTuning.perfectParryPoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectParry(world, attacker);
                    break;

                case HitOutcome.Blocked:
                    world.Fx.Emit(FxType.PlayerBlock, contact, toDir);
                    Velocity = -toDir * 1.5f;
                    Squash(0.88f, 1.1f);
                    if (Posture.Add(attack.postureDamage * t.blockPostureMultiplier * Mods.blockWearMult)) GuardBreak(toDir);
                    break;

                case HitOutcome.PerfectDodge:
                    run.PerfectDodges++;
                    world.TryMaster(attacker, SignatureAnswer.PerfectDodge);
                    world.Fx.Emit(FxType.PerfectDodge, Position, dodgeDir);
                    if (attack.IsDangerous) world.AddFlow(flowTuning.perfectDodgePoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectDodge(world, attacker);
                    break;

                case HitOutcome.CounterThrust:
                    run.Counters++;
                    world.TryMaster(attacker, SignatureAnswer.CounterThrust);
                    world.Fx.Emit(FxType.CounterThrust, contact, toDir);
                    attacker.OnCounterThrust(t.counterThrustPosture * PostureDealtMult);
                    world.AddFlow(flowTuning.counterPoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectDodge(world, attacker);
                    break;

                case HitOutcome.Hit:
                    TakeHit(attack, toDir);
                    break;
            }
            return outcome;
        }

        void TakeHit(AttackDef attack, Vector2 toAttacker)
        {
            bool maxFlow = run.Flow.Level >= run.Flow.MaxLevel;
            float damage = attack.damage * Mods.damageTakenMult * (maxFlow ? Mods.damageTakenAtMaxFlowMult : 1f);
            if (!world.PlayerInvincible) Health = Mathf.Max(0f, Health - damage);

            world.Fx.Emit(FxType.PlayerHurt, Position, -toAttacker, damage);
            world.DropFlow(1 + Mods.extraFlowLossOnHurt);
            hurtInvuln = t.hurtInvulnFrames;
            Velocity = -toAttacker * t.hurtKnockback;
            Flash(0.08f);
            Squash(1.25f, 0.8f);

            if (Health <= 0f)
            {
                Enter(State.Dead);
                Velocity = Vector2.zero;
                world.Fx.Emit(FxType.PlayerDeath, Position, -toAttacker);
                return;
            }

            if (Posture.Add(attack.postureDamage * t.hitPostureFraction))
            {
                GuardBreak(toAttacker);
                return;
            }
            Enter(State.Hurt);
        }

        void GuardBreak(Vector2 toAttacker)
        {
            Enter(State.GuardBroken);
            Velocity = -toAttacker * 3f;
            world.Fx.Emit(FxType.GuardBreak, Position, -toAttacker);
        }

        // ── View ────────────────────────────────────────────────────────────────────────

        protected override void RenderExtra(Vector2 renderPosition, float visualDt)
        {
            Color c = pal.player;
            if (CurrentState == State.GuardBroken) c = Color.Lerp(pal.player, pal.postureHigh, 0.6f);
            if (CurrentState == State.Dead) c = new Color(0.4f, 0.4f, 0.45f, 1f);
            if (Flashing) c = Color.white;
            if (hurtInvuln > 0 && CurrentState != State.Hurt && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f) c.a = 0.35f;
            if (IsDodgeInvulnerable) c.a = 0.6f;
            body.color = c;
            pointer.color = pal.playerAccent.WithAlpha(c.a);

            float facingAngle = CombatMath.AngleDegrees(Facing);

            bool showParry = CurrentState == State.Parry;
            parryArc.enabled = showParry;
            if (showParry)
            {
                bool perfect = StateFrame < parryWindow;
                parryArc.color = pal.playerAccent.WithAlpha(perfect ? 0.6f : 0.22f);
                parryArc.transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                float s = perfect ? 2.3f : 2.0f;
                parryArc.transform.localScale = new Vector3(s, s, 1f);
            }

            int windup = t.attackWindup;
            int flashFrames = t.attackActive + 2;
            bool swinging = CurrentState == State.Attack && StateFrame >= windup && StateFrame < windup + flashFrames;
            swingArc.enabled = swinging;
            if (swinging)
            {
                float k = (StateFrame - windup) / (float)flashFrames;
                swingArc.color = new Color(1f, 1f, 1f, 0.55f * (1f - k));
                swingArc.transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                float s = t.attackRange * 2f;
                swingArc.transform.localScale = new Vector3(s, s, 1f);
            }

            int flow = FlowLevel;
            aura.enabled = flow > 0 && IsAlive;
            if (aura.enabled)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
                aura.transform.localScale = Vector3.one * Radius * 2f * (1.8f + 0.5f * flow) * pulse;
                aura.color = pal.gold.WithAlpha(0.10f + 0.07f * flow);
            }

            empowerGlow.enabled = EmpoweredFrames > 0;
            if (empowerGlow.enabled)
            {
                float s = Radius * 2f * (1.5f + 0.1f * Mathf.Sin(Time.unscaledTime * 20f));
                empowerGlow.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
