using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The duelist. A frame-based state machine: every action has windup/active/recovery
    /// frames, defined cancel points and an input buffer, so timing is exact and learnable.
    /// Blades physically answer each other: a perfect parry kicks your sword up with the
    /// impact, and an enemy guard bounces your swing back and cuts your combo.
    /// </summary>
    public sealed class PlayerController : Fighter
    {
        public enum State
        {
            Free,
            Attack,
            Parry,
            Dodge,
            /// <summary>Your swing was deflected by an enemy guard: blade bounced, combo cut.</summary>
            Deflected,
            Hurt,
            GuardBroken,
            Finisher,
            Dead,
        }

        static readonly PlayerModifiers DefaultMods = new PlayerModifiers();
        const float RestAngle = -55f;
        const float SwingReach = 72f;
        const float FinalSwingReach = 100f;
        const int FootstepFrames = 15;
        const int ClashFrames = 14;
        const int FlowTrailInterval = 4;

        PlayerTuning t;
        FlowTuning flowTuning;
        Palette pal;
        IPlayerInputSource input;
        RunState run;

        readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();
        Vector2 dodgeDir;
        int parryWindow;
        int framesSinceParryPress = 9999;
        bool lastParrySucceeded;
        int hurtInvuln;
        Enemy finisherTarget;
        int footstepTimer;
        int trailTimer;
        int clashFrames;
        float clashStrength;
        float deflectFromAngle;
        float deflectSide;

        SpriteRenderer parryArc, swingArc, aura, empowerGlow;

        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public int ComboIndex { get; private set; }
        public float DodgeCharges { get; private set; }
        public int MaxDodgeCharges { get; private set; }
        /// <summary>Frames left on an empowered strike (set by techniques).</summary>
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

        /// <summary>Flow makes every blow land harder.</summary>
        public float DamageDealtMult => Mods.damageMult * (1f + flowTuning.damagePerLevel * FlowLevel);

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
            BuildWeapon(Shape.Blade, ColorUtil.Hex("#E9EEF5"), 1.15f);

            parryArc = SpriteUtil.Make("ParryArc", transform, ShapeLibrary.Wedge(t.parryArcDegrees), pal.playerAccent, Sorting.Ground + 10);
            swingArc = SpriteUtil.Make("SlashTrail", transform, ShapeLibrary.ArcBand(SwingReach * 2f), Color.white, Sorting.Overlay - 5);
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
            clashFrames = 0;
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
            if (clashFrames > 0) clashFrames--;
            RechargeDodge();
            RegenPosture();

            switch (CurrentState)
            {
                case State.Free: TickFree(); break;
                case State.Parry: TickParry(); break;
                case State.Attack: TickAttack(); break;
                case State.Dodge: TickDodge(); break;
                case State.Deflected: TickDeflected(); break;
                case State.Hurt: TickHurt(); break;
                case State.GuardBroken: TickGuardBroken(); break;
                case State.Finisher: TickFinisher(); break;
            }
            Integrate();
            EmitFlowTrail();
        }

        void Enter(State state)
        {
            CurrentState = state;
            StateFrame = 0;
        }

        float MoveSpeed => t.moveSpeed * Mods.moveSpeedMult;

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
            Steer(input.Move * MoveSpeed);

            if (Velocity.sqrMagnitude > 6f && ++footstepTimer >= FootstepFrames)
            {
                footstepTimer = 0;
                world.Fx.Emit(FxType.Footstep, Position - Velocity.normalized * Radius * 0.6f, -Velocity.normalized);
            }
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
            Steer(input.Move * MoveSpeed * t.guardMoveMultiplier);
        }

        void TickAttack()
        {
            int windup = t.attackWindup;
            int activeEnd = windup + t.attackActive;
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.35f);

            if (StateFrame == windup)
            {
                world.Fx.Emit(FxType.PlayerSwing, Position, Facing, FlowLevel);
                Squash(1.28f, 0.8f);
            }

            if (StateFrame >= windup && StateFrame < activeEnd)
            {
                Position += Facing * (t.attackLunge / t.attackActive);
                float damage = t.attackDamage * DamageDealtMult;
                float posture = t.attackPosture * PostureDealtMult * Mods.normalAttackPostureMult;
                world.PlayerStrike(HitShape.Arc(t.attackRange, t.attackArcDegrees), damage, posture, hitThisSwing);
                // A deflect inside PlayerStrike ends the swing immediately.
                if (CurrentState != State.Attack) return;
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
            Position += dodgeDir * t.dodgeDistance * Mods.dodgeDistanceMult * (now - prev);

            if (StateFrame % 2 == 0) world.Fx.Emit(FxType.Afterimage, Position, Facing, Radius, pal.playerAccent);

            if (StateFrame >= t.dodgeFrames)
            {
                Enter(State.Free);
                Velocity = dodgeDir * MoveSpeed * 0.6f;
            }
        }

        void TickDeflected()
        {
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, CombatMath.Damp(10f, SimClock.Dt));
            if (StateFrame >= t.deflectCancelFrames)
            {
                // Recover in time to parry the riposte: deflection is a warning, not a stun-lock.
                if (TryStartDodge()) return;
                if (input.Buffer.Consume(BufferedAction.Parry))
                {
                    StartParry();
                    return;
                }
            }
            if (StateFrame >= t.deflectedFrames) Enter(State.Free);
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
            world.ProfileParryPress(PerfectParryFrames);
            int window = PerfectParryFrames;
            // Only a whiffed parry is punished, so a flurry can be answered blow by blow.
            if (framesSinceParryPress < t.parrySpamWindow && !lastParrySucceeded) window = Mathf.Max(1, window / 2);
            framesSinceParryPress = 0;
            lastParrySucceeded = false;
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
            run.Profile.OnAttack();
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
            run.Profile.OnDodge();
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

        /// <summary>From Flow 2 the duelist leaves golden afterimages while moving.</summary>
        void EmitFlowTrail()
        {
            if (FlowLevel < 2 || Velocity.sqrMagnitude < 4f) return;
            if (++trailTimer % FlowTrailInterval != 0) return;
            world.Fx.Emit(FxType.Afterimage, Position, Facing, Radius * 0.9f, pal.gold.WithAlpha(0.6f));
        }

        // ── Blade contact ───────────────────────────────────────────────────────────────

        /// <summary>An enemy guard deflected this swing: the blade bounces back and the combo ends.</summary>
        public void OnDeflected()
        {
            if (CurrentState != State.Attack) return;
            deflectFromAngle = SwingAngle();
            deflectSide = ComboIndex % 2 == 0 ? 1f : -1f;
            Enter(State.Deflected);
            Velocity = -Facing * 2.5f;
            Squash(0.82f, 1.15f);
        }

        public HitOutcome ReceiveAttack(in IncomingAttack incoming)
        {
            if (CurrentState == State.Dead) return HitOutcome.Ignored;

            Enemy attacker = incoming.attacker;
            AttackDef attack = incoming.attack;
            float power = incoming.powerMult > 0f ? incoming.powerMult : 1f;
            Vector2 toAttacker = incoming.source - Position;
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
            // Where the two blades meet: just in front of the guard.
            Vector2 clashPoint = Position + toDir * (Radius + 0.45f);

            switch (outcome)
            {
                case HitOutcome.PerfectParry:
                    run.PerfectParries++;
                    run.Profile.OnPerfect();
                    lastParrySucceeded = true;
                    clashFrames = ClashFrames;
                    clashStrength = 1f;
                    world.RegisterPerfect(clashPoint);
                    world.TryMaster(attacker, SignatureAnswer.PerfectParry, incoming.signature);
                    world.Fx.Emit(FxType.PerfectParry, clashPoint, toDir, FlowLevel);
                    Squash(0.8f, 1.2f);
                    if (!incoming.projectile)
                    {
                        float posture = attack.postureOnParried * PostureDealtMult * Mods.perfectParryPostureMult;
                        for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].ModifyParryPosture(world, attacker, ref posture);
                        attacker.OnParried(posture);
                    }
                    world.AddFlow(flowTuning.perfectParryPoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectParry(world, attacker);
                    break;

                case HitOutcome.Blocked:
                    clashFrames = ClashFrames;
                    clashStrength = 0.5f;
                    world.Fx.Emit(FxType.PlayerBlock, clashPoint, toDir);
                    Velocity = -toDir * 1.5f;
                    Squash(0.88f, 1.1f);
                    if (!incoming.projectile)
                    {
                        for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnBlock(world, attacker);
                    }
                    if (Posture.Add(attack.postureDamage * power * t.blockPostureMultiplier * Mods.blockWearMult)) GuardBreak(toDir);
                    break;

                case HitOutcome.PerfectDodge:
                    run.PerfectDodges++;
                    if (attack.IsDangerous) world.RegisterPerfect(Position);
                    world.TryMaster(attacker, SignatureAnswer.PerfectDodge, incoming.signature);
                    world.Fx.Emit(FxType.PerfectDodge, Position, dodgeDir);
                    if (attack.IsDangerous) world.AddFlow(flowTuning.perfectDodgePoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectDodge(world, attacker);
                    break;

                case HitOutcome.CounterThrust:
                    run.Counters++;
                    run.Profile.OnPerfect();
                    world.RegisterPerfect(clashPoint);
                    world.TryMaster(attacker, SignatureAnswer.CounterThrust, incoming.signature);
                    world.Fx.Emit(FxType.CounterThrust, clashPoint, toDir, FlowLevel);
                    attacker.OnCounterThrust(t.counterThrustPosture * PostureDealtMult);
                    world.AddFlow(flowTuning.counterPoints);
                    for (int i = 0; i < run.Owned.Count; i++) run.Owned[i].OnPerfectDodge(world, attacker);
                    break;

                case HitOutcome.Hit:
                    TakeHit(attack, toDir, power);
                    break;
            }
            return outcome;
        }

        void TakeHit(AttackDef attack, Vector2 toAttacker, float power)
        {
            bool maxFlow = run.Flow.Level >= run.Flow.MaxLevel;
            float damage = attack.damage * power * Mods.damageTakenMult * (maxFlow ? Mods.damageTakenAtMaxFlowMult : 1f);
            if (!world.PlayerInvincible) Health = Mathf.Max(0f, Health - damage);

            world.Fx.Emit(FxType.PlayerHurt, Position, -toAttacker, damage);
            world.DropFlow(1 + Mods.extraFlowLossOnHurt);
            run.BreakStreak();
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

            if (Posture.Add(attack.postureDamage * power * t.hitPostureFraction))
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

        protected override WeaponPose GetWeaponPose()
        {
            var pose = new WeaponPose
            {
                angle = RestAngle + 4f * Mathf.Sin(Time.unscaledTime * 1.8f),
                tint = weaponColor,
            };

            switch (CurrentState)
            {
                case State.Parry:
                    bool perfect = StateFrame < parryWindow;
                    pose.angle = perfect ? 78f : 64f;
                    pose.extend = -0.1f;
                    if (perfect) pose.tint = Color.Lerp(weaponColor, pal.playerAccent, 0.75f);
                    break;
                case State.Attack:
                    pose.snap = true;
                    pose.angle = SwingAngle();
                    break;
                case State.Deflected:
                {
                    pose.snap = true;
                    int f = StateFrame;
                    float knocked = deflectFromAngle + deflectSide * 65f;
                    pose.angle = f < 4
                        ? Mathf.Lerp(deflectFromAngle, knocked, CombatMath.EaseOutCubic(f / 4f))
                        : Mathf.Lerp(knocked, RestAngle, CombatMath.EaseOutQuad((f - 4) / (float)Mathf.Max(1, t.deflectedFrames - 4)));
                    if (f < 4) pose.tint = Color.white;
                    break;
                }
                case State.Dodge:
                    pose.angle = 165f;
                    pose.extend = -0.1f;
                    break;
                case State.Hurt:
                case State.GuardBroken:
                    pose.angle = -125f + 10f * Mathf.Sin(Time.unscaledTime * 30f);
                    break;
                case State.Finisher:
                    pose.snap = true;
                    pose.angle = 0f;
                    pose.extend = StateFrame < t.finisherImpactFrame
                        ? Mathf.Lerp(0f, -0.45f, StateFrame / (float)t.finisherImpactFrame)
                        : Mathf.Lerp(0.65f, 0.1f, (StateFrame - t.finisherImpactFrame) / (float)Mathf.Max(1, t.finisherFrames - t.finisherImpactFrame));
                    pose.tint = Color.Lerp(weaponColor, pal.gold, 0.5f);
                    break;
                case State.Dead:
                    pose.angle = -150f;
                    pose.tint = weaponColor * 0.5f;
                    break;
            }

            // The parry clash: the blade is slammed back by the enemy's sword, then settles.
            if (clashFrames > 0 && (CurrentState == State.Parry || CurrentState == State.Free))
            {
                int f = ClashFrames - clashFrames;
                float bump = Bounce(f, ClashFrames, 3);
                pose.snap = true;
                pose.angle = 78f + 38f * clashStrength * bump;
                pose.extend = -0.15f * bump;
                pose.tint = Color.Lerp(pose.tint, Color.white, bump * clashStrength);
            }

            if (EmpoweredFrames > 0) pose.tint = Color.Lerp(pose.tint, pal.gold, 0.65f);
            else if (FlowLevel >= 3) pose.tint = Color.Lerp(pose.tint, pal.gold, 0.35f);
            return pose;
        }

        /// <summary>Combo swings alternate sides; the last one is wider.</summary>
        float SwingAngle()
        {
            float side = ComboIndex % 2 == 0 ? 1f : -1f;
            float reach = ComboIndex == t.comboLength - 1 ? FinalSwingReach : SwingReach;
            float start = reach * side;
            float end = -reach * side;
            int windup = t.attackWindup;
            int active = t.attackActive;
            if (StateFrame < windup) return Mathf.Lerp(RestAngle, start, CombatMath.EaseOutQuad(StateFrame / (float)windup));
            if (StateFrame < windup + active) return Mathf.Lerp(start, end, CombatMath.EaseOutQuad((StateFrame - windup + 1) / (float)active));
            float r = (StateFrame - windup - active) / (float)Mathf.Max(1, RecoveryFrames());
            return Mathf.Lerp(end, RestAngle, CombatMath.EaseOutQuad(Mathf.Clamp01(r)));
        }

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

            float facingAngle = FacingAngle;

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
            int trailFrames = t.attackActive + 4;
            bool swinging = CurrentState == State.Attack && StateFrame >= windup && StateFrame < windup + trailFrames;
            swingArc.enabled = swinging;
            if (swinging)
            {
                float k = (StateFrame - windup) / (float)trailFrames;
                Color trail = EmpoweredFrames > 0 || FlowLevel >= 3 ? pal.gold : Color.white;
                swingArc.color = trail.WithAlpha(0.75f * (1f - k * k));
                swingArc.transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                float s = t.attackRange * 2f * (1f + 0.08f * k + 0.06f * FlowLevel);
                swingArc.transform.localScale = new Vector3(s, ComboIndex % 2 == 0 ? s : -s, 1f);
            }

            int flow = FlowLevel;
            aura.enabled = flow > 0 && IsAlive;
            if (aura.enabled)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * (6f + flow * 2f));
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
