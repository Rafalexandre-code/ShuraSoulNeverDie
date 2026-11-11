using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The duelist. A frame-based state machine: every action has windup/active/recovery
    /// frames, defined cancel points and an input buffer, so timing is exact and learnable.
    /// Carries two weapons: swapping inside an enemy's swing is a Troca Perfeita — a parry
    /// with no block fallback that unleashes the incoming weapon's Swap Strike.
    /// </summary>
    public sealed partial class PlayerController : Fighter
    {
        public enum State
        {
            Free,
            Attack,
            Parry,
            /// <summary>Changing weapons; the first frames are the Troca Perfeita window.</summary>
            Swap,
            /// <summary>The special attack after a Troca Perfeita (invulnerable).</summary>
            SwapStrike,
            Dodge,
            /// <summary>Your swing was deflected by an enemy guard: blade bounced, combo cut.</summary>
            Deflected,
            Hurt,
            GuardBroken,
            Finisher,
            Dead,
            /// <summary>Drinking from the gourd or binding a wound.</summary>
            Drink,
            /// <summary>Time slows over a broken enemy: aim the finishing cut.</summary>
            FinisherAim,
            /// <summary>The Iai standoff at the start of a room.</summary>
            Standoff,
        }

        static readonly PlayerModifiers DefaultMods = new PlayerModifiers();
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
        int swapWindow;
        int framesSinceSwap = 9999;
        bool lastSwapSucceeded = true;
        int hurtInvuln;
        int footstepTimer;
        int trailTimer;
        int clashFrames;
        float clashStrength;
        float deflectFromAngle;
        float deflectSide;

        Enemy finisherTarget;
        FinisherStyle finisherStyle;
        Vector2 finisherDir;
        Vector2 finisherStart;
        Vector2 finisherAnchor;
        bool finisherKilled;

        SpriteRenderer parryArc, aura, empowerGlow, offhand, chain;
        bool flickDone;

        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public int ComboIndex { get; private set; }
        public float DodgeCharges { get; private set; }
        public int MaxDodgeCharges { get; private set; }
        /// <summary>Frames left on an empowered strike (set by techniques).</summary>
        public int EmpoweredFrames { get; set; }
        public int HurtInvulnFrames => hurtInvuln;
        public int ParryWindowFrames => parryWindow;

        PlayerModifiers Mods => Build != null ? Build.Modifiers : DefaultMods;
        int FlowLevel => Build != null ? Build.Flow.Level : 0;
        /// <summary>This duelist's own weapons, items, rewards and Flow.</summary>
        public PlayerBuild Build { get; private set; }
        /// <summary>This duelist's two weapons (their build's pair once a run starts).</summary>
        WeaponDef[] loadout;
        int activeWeapon;
        public WeaponDef Weapon => loadout != null && loadout[activeWeapon] != null ? loadout[activeWeapon] : WeaponDefs.Katana;
        public WeaponDef OtherWeapon => loadout != null && loadout[1 - activeWeapon] != null ? loadout[1 - activeWeapon] : WeaponDefs.Katana;
        public string DisplayName { get; set; } = "Anfitrião";
        /// <summary>Network id (0 = host).</summary>
        public int NetId { get; set; }
        /// <summary>How late (in ticks) this player sees and answers the world; enemies wait that long before landing on them.</summary>
        public int LatencyTicks { get; set; }
        public IPlayerInputSource Input { get; set; }
        public int ArtIndex { get; private set; }
        bool ownLoadout;

        /// <summary>The samurai variants players can be (host is 0).</summary>
        public static readonly string[] PartyArt = { "Samurai", "SamuraiRed", "NinjaBlue", "Monk", "Master", "NinjaGreen", "Hunter", "FighterWhite" };

        public void SetLoadout(WeaponDef a, WeaponDef b)
        {
            loadout = new[] { a ?? WeaponDefs.Katana, b ?? WeaponDefs.Montante };
            activeWeapon = 0;
            ownLoadout = true;
            ApplyWeaponVisual();
        }

        public void SetArt(int index)
        {
            ArtIndex = Mathf.Clamp(index, 0, PartyArt.Length - 1);
            SetCharacterArt(PixelArt.Character(PartyArt[ArtIndex]) ?? PixelArt.Character(PlayerArt));
        }

        /// <summary>Back on their feet: by a teammate's side, or at the start of a room.</summary>
        public void Revive(float healthFraction)
        {
            Health = Mathf.Max(1f, MaxHealth * healthFraction);
            Posture.Reset();
            hurtInvuln = 90;
            Velocity = Vector2.zero;
            Enter(State.Free);
        }

        /// <summary>Traps: no parry, no block; a dodge's i-frames still slip through.</summary>
        /// <summary>Traps and floor blasts. Returns true when a dodge carried the player through it.</summary>
        public bool ReceiveHazard(float damage, float posture, Vector2 dir)
        {
            if (!IsAlive) return false;
            if (IsDodgeInvulnerable) return true;
            if (hurtInvuln > 0 || CurrentState == State.Finisher || CurrentState == State.FinisherAim) return false;
            hazardAttack.damage = damage;
            hazardAttack.postureDamage = posture;
            TakeHit(hazardAttack, -dir, 1f);
            return false;
        }

        /// <summary>A perfect dodge through something that was not a swing (a bomb or talisman going off).</summary>
        public void CreditPerfectDodge(Enemy source, bool signature)
        {
            PerfectDodgeAgainst(source, true, signature);
        }

        void PerfectDodgeAgainst(Enemy attacker, bool dangerous, bool signature)
        {
            Build.PerfectDodges++;
            if (dangerous) world.RegisterPerfect(Position);
            world.TryMaster(attacker, SignatureAnswer.PerfectDodge, signature);
            world.Fx.Emit(FxType.PerfectDodge, Position, dodgeDir);
            if (dangerous) world.AddFlow(flowTuning.perfectDodgePoints);
            for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnPerfectDodge(world, attacker);
        }

        /// <summary>Nothing lands for a moment (techniques).</summary>
        public void GrantInvulnerable(int frames)
        {
            hurtInvuln = Mathf.Max(hurtInvuln, frames);
        }

        /// <summary>Gives back one dodge charge (techniques).</summary>
        public void RefundDodge()
        {
            DodgeCharges = Mathf.Min(MaxDodgeCharges, DodgeCharges + 1f);
        }

        readonly AttackDef hazardAttack = new AttackDef { id = "trap" };
        PlayerAttackDef CurrentStep => CurrentState == State.SwapStrike ? (eloStep ?? Weapon.swapStrike) : Weapon.combo[Mathf.Clamp(ComboIndex, 0, Weapon.combo.Length - 1)];

        public bool IsDodgeInvulnerable =>
            CurrentState == State.Dodge && StateFrame >= t.dodgeIFrameStart && StateFrame <= t.dodgeIFrameEnd;

        public int PerfectParryFrames => Mathf.Max(2, (t.perfectParryFrames + Mods.perfectParryFrameBonus + KiParryBonus + (Build?.AgeParryBonus ?? 0) + world.ParryAssistFrames) / (Exhausted ? 2 : 1));

        public float PostureDealtMult =>
            Mods.postureMult * (FlowLevel >= 1 ? 1f + flowTuning.level1PostureBonus : 1f);

        /// <summary>Flow makes every blow land harder.</summary>
        public float DamageDealtMult => Mods.damageMult * (1f + flowTuning.damagePerLevel * FlowLevel) * KiDamageMult;

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

            BuildView(owner, PixelArt.Character(PlayerArt), t.radius, 1f);
            BuildWeapon(WeaponSprite(WeaponDefs.Katana), Color.white, WeaponDefs.Katana.length);

            parryArc = SpriteUtil.Make("ParryArc", transform, ShapeLibrary.Wedge(t.parryArcDegrees), pal.playerAccent, Sorting.Ground + 10);
            aura = SpriteUtil.Make("FlowAura", transform, ShapeLibrary.Get(Shape.SoftCircle), pal.gold, Sorting.Shadow + 1);
            empowerGlow = SpriteUtil.Make("Empowered", transform, ShapeLibrary.Get(Shape.Ring), pal.gold, Sorting.Overlay - 1);
            offhand = SpriteUtil.Make("Offhand", transform, WeaponSprite(WeaponDefs.Katana), Color.white, 0);
            Sprite link = PixelArt.ChainLink();
            chain = SpriteUtil.Make("Chain", transform, link != null ? link : ShapeLibrary.Get(Shape.PixelLeft), Color.white, 0);
            if (link != null) chain.drawMode = SpriteDrawMode.Tiled;
            parryArc.enabled = aura.enabled = empowerGlow.enabled = offhand.enabled = chain.enabled = false;
        }

        public void BeginRun(RunState runState, IPlayerInputSource source, PlayerBuild build = null)
        {
            run = runState;
            Build = build ?? runState.Main;
            input = source;
            Input = source;
            if (ownLoadout)
            {
                // A teammate brings the pair they picked in the lobby into their build.
                Build.Weapons[0] = loadout[0];
                Build.Weapons[1] = loadout[1];
                Build.ActiveWeapon = 0;
            }
            loadout = Build.Weapons;
            activeWeapon = Build.ActiveWeapon;
            ApplyStats(true);
            DodgeCharges = MaxDodgeCharges;
            EmpoweredFrames = 0;
            Facing = Vector2.right;
            ApplyWeaponVisual();
            Enter(State.Free);
        }

        public void ResetForRoom(Vector2 spawn)
        {
            ResetKiForRoom();
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
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount * HealMult);
        }

        /// <summary>The straw-hat samurai from Ninja Adventure.</summary>
        public const string PlayerArt = "Samurai";

        /// <summary>Pixel-art weapon, or the old procedural shape if the art is missing.</summary>
        public static Sprite WeaponSprite(WeaponDef w)
        {
            Sprite s = PixelArt.Weapon(w.art);
            return s != null ? s : ShapeLibrary.Get(w.sprite);
        }

        void ApplyWeaponVisual()
        {
            WeaponDef w = Weapon;
            SetWeapon(WeaponSprite(w), Color.white, w.length);
            offhand.sprite = WeaponSprite(w);
            offhand.enabled = w.dual;
        }

        protected override bool HurtPose => CurrentState == State.Hurt || CurrentState == State.GuardBroken;

        protected override bool StrikePose =>
            CurrentState == State.Attack || CurrentState == State.SwapStrike || CurrentState == State.Finisher;

        protected override bool FallenPose => CurrentState == State.Dead;

        protected override void OnFootfall()
        {
            world.Fx.Footstep(Position);
        }

        public void Tick()
        {
            BeginTick();
            if (CurrentState == State.Dead || input == null) return;

            StateFrame++;
            if (hurtInvuln > 0) hurtInvuln--;
            if (framesSinceParryPress < 9999) framesSinceParryPress++;
            if (framesSinceSwap < 9999) framesSinceSwap++;
            if (EmpoweredFrames > 0) EmpoweredFrames--;
            if (clashFrames > 0) clashFrames--;
            RechargeDodge();
            RegenPosture();
            TickKi();
            if (world.Standoff.Active && CurrentState != State.Standoff && IsAlive) BeginStandoff();

            switch (CurrentState)
            {
                case State.Free: TickFree(); break;
                case State.Parry: TickParry(); break;
                case State.Swap: TickSwap(); break;
                case State.Attack: TickStrike(false); break;
                case State.SwapStrike: TickStrike(true); break;
                case State.Dodge: TickDodge(); break;
                case State.Deflected: TickDeflected(); break;
                case State.Hurt: TickHurt(); break;
                case State.GuardBroken: TickGuardBroken(); break;
                case State.Finisher: TickFinisher(); break;
                case State.Drink: TickDrink(); break;
                case State.FinisherAim: TickFinisherAim(); break;
                case State.Standoff: TickStandoff(); break;
            }
            Integrate();
            EmitFlowTrail();
        }

        void Enter(State state)
        {
            // Leaving a finisher before the kill (knocked out of it, room reset…): let the victim go.
            bool wasFinishing = CurrentState == State.FinisherAim || CurrentState == State.Finisher;
            if (wasFinishing && state != State.Finisher && finisherTarget != null && !finisherKilled) finisherTarget.ReleaseFromFinisher();
            CurrentState = state;
            StateFrame = 0;
        }

        float MoveSpeed => t.moveSpeed * Mods.moveSpeedMult * Weapon.moveMult * KiMoveMult;

        // ── States ──────────────────────────────────────────────────────────────────────

        void TickFree()
        {
            UpdateFacing(false);
            if (TryKiActions()) return;
            if (TryStartDodge()) return;
            if (TryStartSwap()) return;
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
            if (TryKiActions()) return;

            bool windowOver = StateFrame >= parryWindow;
            if (windowOver && TryStartSwap()) return;
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

        void TickSwap()
        {
            UpdateFacing(true);
            Steer(input.Move * MoveSpeed * 0.6f);
            if (StateFrame < swapWindow) return;
            if (TryStartDodge()) return;
            if (StateFrame >= t.swapFrames) Enter(State.Free);
        }

        /// <summary>Combo steps and Swap Strikes share the same frame logic.</summary>
        void TickStrike(bool swapStrike)
        {
            PlayerAttackDef step = CurrentStep;
            int activeEnd = step.windup + step.active;
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.35f);

            if (StateFrame == step.windup)
            {
                world.Fx.Emit(swapStrike ? FxType.SwapStrike : FxType.PlayerSwing, Position, Facing, Weapon.swingPitch, TrailColor());
                Squash(1.28f, 0.8f);
                if (!swapStrike) for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnSwing(world, ComboIndex, Facing);
                if (step.style == SwingStyle.Thrust || step.style == SwingStyle.Jab)
                {
                    world.Fx.Emit(FxType.ThrustTrail, Position, Facing, step.shape.range, TrailColor());
                }
            }

            if (StateFrame >= step.windup && StateFrame < activeEnd)
            {
                int f = StateFrame - step.windup;
                if (step.hits > 1 && f > 0 && f % Mathf.Max(1, step.active / step.hits) == 0) hitThisSwing.Clear();
                float move = (step.lunge * (swapStrike ? 1f : StanceLungeMult) + step.dash) / step.active;
                Position += Facing * move;
                float damage = step.damage * DamageDealtMult;
                float posture = step.posture * PostureDealtMult * Mods.normalAttackPostureMult * (descendFrames > 0 ? 2f : 1f);
                world.PlayerStrike(step.shape, damage, posture, hitThisSwing, step.pull, swapStrike || InAsura);
                if (step.style == SwingStyle.Slam && f == step.active - 1) world.Fx.Emit(FxType.SlamImpact, Position + Facing * step.shape.range * 0.6f, Facing, step.shape.range);
                // A deflect inside PlayerStrike ends the swing immediately.
                if (CurrentState != (swapStrike ? State.SwapStrike : State.Attack)) return;
            }

            if (StateFrame < activeEnd) return;
            if (StateFrame == activeEnd)
            {
                if (descendFrames > 0 && hitThisSwing.Count > 0) descendFrames = 0;
                if (!swapStrike && ComboIndex == Weapon.combo.Length - 1) OfferBreath();
            }

            int sinceActive = StateFrame - activeEnd;
            if (!swapStrike && sinceActive >= t.attackCancelDelay)
            {
                if (TryStartDodge()) return;
                if (TryKiActions()) return;
                if (TryStartSwap()) return;
                if (input.Buffer.Consume(BufferedAction.Parry))
                {
                    StartParry();
                    return;
                }
            }

            if (!swapStrike && sinceActive >= t.comboChainDelay && ComboIndex + 1 < Weapon.combo.Length &&
                input.Buffer.Consume(BufferedAction.Attack))
            {
                if (!TryStartFinisher()) StartAttack(ComboIndex + 1);
                return;
            }

            if (StateFrame >= activeEnd + RecoveryFrames(step)) Enter(State.Free);
        }

        void TickDodge()
        {
            int frames = DodgeFrames;
            float prev = CombatMath.EaseOutQuad((StateFrame - 1) / (float)frames);
            float now = CombatMath.EaseOutQuad(StateFrame / (float)frames);
            Position += dodgeDir * t.dodgeDistance * Mods.dodgeDistanceMult * KiDodgeDistance * world.DodgeDistanceMult * (now - prev);

            if (StateFrame % 2 == 0) world.Fx.Emit(FxType.Afterimage, Position, Facing, Radius, pal.playerAccent);

            if (StateFrame >= frames)
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
                if (TryKiActions()) return;
                if (TryStartSwap()) return;
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

        // ── Finishers: one scripted kill per weapon ─────────────────────────────────────

        int FinisherLength(FinisherStyle style)
        {
            switch (style)
            {
                case FinisherStyle.CleanCut: return 46;
                case FinisherStyle.Execution: return 48;
                case FinisherStyle.ThousandCuts: return 52;
                case FinisherStyle.Impale: return 50;
                case FinisherStyle.Reap: return 52;
                default: return 46;
            }
        }

        void TickFinisher()
        {
            int f = StateFrame;
            Enemy e = finisherTarget;
            Velocity = Vector2.zero;

            if (e != null && !finisherKilled)
            {
                switch (finisherStyle)
                {
                    case FinisherStyle.CleanCut:
                        // Dash through, stand with your back to it, and let it fall apart.
                        if (f <= 6)
                        {
                            Vector2 beyond = finisherAnchor + finisherDir * (e.Radius + Radius + 0.9f);
                            Position = Vector2.Lerp(finisherStart, beyond, CombatMath.EaseOutCubic(f / 6f));
                        }
                        if (f == 4) Impact(e, 0.8f);
                        e.HoldAt(finisherAnchor, 1f);
                        if (f == 30) Kill(e);
                        break;

                    case FinisherStyle.Execution:
                        e.HoldAt(finisherAnchor, f < 18 ? Mathf.Lerp(1f, 0.85f, f / 18f) : 0.85f);
                        if (f == 18) Kill(e);
                        break;

                    case FinisherStyle.ThousandCuts:
                        if (f >= 4 && f <= 36 && f % 4 == 0)
                        {
                            // Blink around the target, a stab from each side.
                            float side = (f / 4) % 2 == 0 ? 1f : -1f;
                            Vector2 dir = CombatMath.RotateTowards(finisherDir, CombatMath.Perpendicular(finisherDir) * side, 50f);
                            Position = finisherAnchor - dir * (e.Radius + Radius + 0.15f);
                            Facing = dir;
                            Impact(e, 0.45f);
                        }
                        e.HoldAt(finisherAnchor, 1f);
                        if (f == 42) Kill(e);
                        break;

                    case FinisherStyle.Impale:
                        if (f == 6) Impact(e, 1f);
                        if (f >= 6)
                        {
                            float lift = Mathf.Clamp01((f - 6) / 20f);
                            Vector2 tip = Position + Facing * (Radius * 0.55f + Weapon.length * 0.85f);
                            e.HoldAt(Vector2.Lerp(finisherAnchor, tip, lift), 1f + 0.25f * lift);
                            if (f % 6 == 0) world.Fx.Blood(e.Position, Vector2.down, 4f, e.Def.bloodColor);
                        }
                        else e.HoldAt(finisherAnchor, 1f);
                        if (f == 28) Kill(e);
                        break;

                    case FinisherStyle.Reap:
                        if (f == 8) Impact(e, 0.8f);
                        if (f >= 8)
                        {
                            float a = CombatMath.AngleDegrees(finisherDir) + 540f * CombatMath.EaseOutQuad(Mathf.Clamp01((f - 8) / 26f));
                            var around = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                            e.HoldAt(Position + around * 2f, 1f);
                            Facing = around;
                            if (f % 3 == 0) world.Fx.Blood(e.Position, CombatMath.Perpendicular(around), 3f, e.Def.bloodColor);
                        }
                        else e.HoldAt(finisherAnchor, 1f);
                        if (f == 34)
                        {
                            finisherDir = CombatMath.Perpendicular(Facing);
                            Kill(e);
                        }
                        break;

                    case FinisherStyle.HeartBreak:
                        if (f == 5 || f == 11 || f == 17)
                        {
                            finisherAnchor += finisherDir * 0.15f;
                            Impact(e, 0.6f);
                        }
                        e.HoldAt(finisherAnchor, 1f);
                        if (f == 27) Kill(e);
                        break;
                }
            }

            if (f >= FinisherLength(finisherStyle)) Enter(State.Free);
        }

        void Impact(Enemy e, float intensity)
        {
            world.FinisherImpact(e, finisherDir, intensity);
            Squash(1.3f, 0.78f);
        }

        void Kill(Enemy e)
        {
            finisherKilled = true;
            world.ExecuteFinisher(e, finisherStyle, finisherDir);
            finisherTarget = null;
            Squash(1.35f, 0.75f);
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

        /// <summary>
        /// Swapping is always allowed; inside an enemy swing it is a Troca Perfeita. A whiffed
        /// swap shortens the next window, so the swap cannot be spammed as a free parry.
        /// </summary>
        bool TryStartSwap()
        {
            if (!input.Buffer.Consume(BufferedAction.Swap)) return false;
            // Swapping right after the last blow of a combo throws a Link (Elo) instead.
            bool elo = CurrentState == State.Attack && ComboIndex == Weapon.combo.Length - 1 &&
                       StateFrame >= CurrentStep.windup + CurrentStep.active;
            WeaponDef outgoing = Weapon;
            int outgoingCharge = charge[activeWeapon];
            charge[activeWeapon] = 0;
            world.ProfileParryPress(PerfectParryFrames);
            int window = PerfectParryFrames;
            if (framesSinceSwap < t.swapSpamWindow && !lastSwapSucceeded) window = Mathf.Max(1, window / 2);
            framesSinceSwap = 0;
            lastSwapSucceeded = false;
            // A wounded sword arm cannot turn the swap into a parry.
            swapWindow = HasWound(WoundRegion.Arm) ? 0 : window;
            activeWeapon = 1 - activeWeapon;
            if (Build != null) Build.ActiveWeapon = activeWeapon;
            ApplyWeaponVisual();
            UpdateFacing(true);
            ReleaseCharge(outgoing, outgoingCharge);
            if (elo)
            {
                StartElo(outgoing, Weapon);
                return true;
            }
            Enter(State.Swap);
            Squash(0.85f, 1.15f);
            world.Fx.Emit(FxType.WeaponSwap, Position, Facing, 1f, Weapon.color);
            return true;
        }

        void StartSwapStrike()
        {
            eloStep = null;
            hitThisSwing.Clear();
            Velocity = Vector2.zero;
            Enter(State.SwapStrike);
            world.Fx.Popup(Weapon.swapStrike.name.ToUpperInvariant(), Position + Vector2.up * 1.1f, pal.gold);
        }

        void StartAttack(int index)
        {
            run.Profile.OnAttack();
            if (index == 0) comboSpent = 0f;
            SpendStamina(AttackStamina);
            comboSpent += AttackStamina;
            chargedThisSwing = false;
            if (CycleFramesLeft > 0 && index > 0)
            {
                // Ciclo das Duas Lâminas: every blow comes from the other weapon.
                activeWeapon = 1 - activeWeapon;
                if (Build != null) Build.ActiveWeapon = activeWeapon;
                ApplyWeaponVisual();
                index = Mathf.Min(index, Weapon.combo.Length - 1);
            }
            ComboIndex = index;
            hitThisSwing.Clear();
            UpdateFacing(false);
            PlayerAttackDef step = Weapon.combo[index];
            Enemy target = world.FindAssistTarget(Position, Facing, t.aimAssistDegrees, step.shape.range + 1.2f, false);
            if (target != null) Facing = (target.Position - Position).normalized;
            Velocity *= 0.3f;
            Enter(State.Attack);
            Squash(0.85f, 1.12f);
            flickDone = false;
        }

        bool TryStartDodge()
        {
            if (TryBreathe()) return true;
            if (!input.Buffer.Peek(BufferedAction.Dodge) || DodgeCharges < 1f) return false;
            input.Buffer.Consume(BufferedAction.Dodge);
            DodgeCharges -= 1f;
            SpendStamina(DodgeStamina);
            run.Profile.OnDodge();
            Vector2 move = input.Move;
            dodgeDir = move.sqrMagnitude > 0.04f ? move.normalized : Facing;
            Velocity = Vector2.zero;
            Enter(State.Dodge);
            world.OnDodgeForChallenge();
            Squash(1.35f, 0.72f);
            world.Fx.Emit(FxType.Dodge, Position, dodgeDir);
            for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnDodge(world, Position);
            return true;
        }

        bool TryStartFinisher()
        {
            Enemy target = world.FindFinisherTarget(Position, t.finisherRange);
            if (target == null) return false;
            finisherTarget = target;
            finisherStyle = Weapon.finisher;
            target.BeginFinisher();
            Vector2 to = target.Position - Position;
            finisherDir = to.sqrMagnitude > 1e-4f ? to.normalized : Facing;
            Facing = finisherDir;
            finisherStart = Position;
            finisherAnchor = target.Position;
            finisherKilled = false;
            Velocity = Vector2.zero;
            // The golden line runs from the duelist to a random point of the enemy's body.
            float r = target.Radius;
            AimPoint = target.Position + new Vector2(world.Run.Rng.Range(-r * 0.8f, r * 0.8f), world.Run.Rng.Range(0.1f, r * 2.2f));
            AimLineAngle = CombatMath.AngleDegrees(AimPoint - Position);
            CutPrecision = 0f;
            Enter(State.FinisherAim);
            world.HintOnce(HintId.FinisherCut);
            Squash(0.8f, 1.2f);
            world.Fx.Emit(FxType.FinisherStart, target.Position, finisherDir, (int)finisherStyle);
            return true;
        }

        int RecoveryFrames(PlayerAttackDef step)
        {
            return Mathf.Max(2, Mathf.RoundToInt(step.recovery * Mods.recoveryMult));
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

        Color TrailColor()
        {
            if (EmpoweredFrames > 0 || FlowLevel >= 3) return pal.gold;
            if (Mods.trailElement != Element.None) return Elements.Color(Mods.trailElement);
            return Weapon.trail;
        }

        // ── Blade contact ───────────────────────────────────────────────────────────────

        /// <summary>An enemy guard deflected this swing: the blade bounces back and the combo ends.</summary>
        public void OnDeflected()
        {
            if (CurrentState != State.Attack) return;
            deflectFromAngle = StrikeAngle();
            SwingAngles(CurrentStep, out float deflectedFrom, out _);
            deflectSide = Mathf.Sign(deflectedFrom == 0f ? 1f : deflectedFrom);
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
                hurtInvulnerable = hurtInvuln > 0 || CurrentState == State.Finisher || CurrentState == State.FinisherAim || CurrentState == State.SwapStrike,
                parryWindow = CurrentState == State.Parry && StateFrame < parryWindow,
                swapWindow = CurrentState == State.Swap && StateFrame < swapWindow,
                // A talisman worn lets an ordinary parry stop a spirit blow.
                spiritWard = Build != null && Build.Equipped[(int)ItemSlot.Talisman] != null,
                guarding = CurrentState == State.Parry,
                facing = Facing,
                toAttacker = toAttacker,
                parryArcDegrees = t.parryArcDegrees,
                dodgeDirection = CurrentState == State.Dodge ? dodgeDir : Vector2.zero,
                counterDot = t.counterThrustDotThreshold,
                groupAnswered = incoming.groupAnswered,
            };
            lastAttacker = attacker;
            // Mikiri timing: the dodge began in the last few frames before the blow.
            bool lateDodge = CurrentState == State.Dodge && StateFrame <= 5;

            HitOutcome outcome = HitResolver.Resolve(attack, defender);
            // Where the two blades meet: just in front of the guard.
            Vector2 clashPoint = Position + toDir * (Radius + 0.45f);

            switch (outcome)
            {
                case HitOutcome.PerfectParry:
                case HitOutcome.SwapParry:
                {
                    bool swap = outcome == HitOutcome.SwapParry;
                    Build.PerfectParries++;
                    if (swap) Build.SwapParries++;
                    run.Profile.OnPerfect();
                    if (swap) lastSwapSucceeded = true;
                    else lastParrySucceeded = true;
                    clashFrames = ClashFrames;
                    clashStrength = 1f;
                    world.RegisterPerfect(clashPoint);
                    world.TryMaster(attacker, SignatureAnswer.PerfectParry, incoming.signature);
                    world.Fx.Emit(swap ? FxType.SwapParry : FxType.PerfectParry, clashPoint, toDir, FlowLevel, Weapon.color);
                    Squash(0.8f, 1.2f);
                    if (attacker != null)
                    {
                        // Each perfect parry plants a Ki seal and steals the attacker's breath.
                        attacker.AddSeal(1 + (Build?.ExtraSealsPerParry ?? 0));
                        attacker.DrainStamina(10f);
                        world.RegisterResonance(attacker, this);
                    }
                    if (!incoming.projectile)
                    {
                        float posture = attack.postureOnParried * PostureDealtMult * Mods.perfectParryPostureMult * (swap ? t.swapParryPostureMult : 1f);
                        posture *= world.ResonanceMult(attacker);
                        for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].ModifyParryPosture(world, attacker, ref posture);
                        attacker.OnParried(posture);
                    }
                    world.AddFlow(flowTuning.perfectParryPoints * (swap ? t.swapParryFlowMult : 1f));
                    for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnPerfectParry(world, attacker);
                    if (swap)
                    {
                        Facing = toDir;
                        StartSwapStrike();
                    }
                    break;
                }

                case HitOutcome.Blocked:
                    clashFrames = ClashFrames;
                    clashStrength = 0.5f;
                    world.Fx.Emit(FxType.PlayerBlock, clashPoint, toDir);
                    Velocity = -toDir * 1.5f;
                    Squash(0.88f, 1.1f);
                    if (!incoming.projectile)
                    {
                        for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnBlock(world, attacker);
                    }
                    SpendStamina(attack.postureDamage * power);
                    if (attack.cursed) Curse();
                    if (Posture.Add(attack.postureDamage * power * t.blockPostureMultiplier * Mods.blockWearMult) && !PostureUnbreakable) GuardBreak(toDir);
                    break;

                case HitOutcome.PerfectDodge:
                    PerfectDodgeAgainst(attacker, attack.IsDangerous, incoming.signature);
                    if (attacker != null && lateDodge) MasterDodge(attack, attacker, toDir);
                    break;

                case HitOutcome.CounterThrust:
                    Build.Counters++;
                    run.Profile.OnPerfect();
                    world.RegisterPerfect(clashPoint);
                    world.TryMaster(attacker, SignatureAnswer.CounterThrust, incoming.signature);
                    world.Fx.Emit(FxType.CounterThrust, clashPoint, toDir, FlowLevel);
                    attacker.OnCounterThrust(t.counterThrustPosture * PostureDealtMult * (Stance == Kamae.Chudan ? 1.3f : 1f));
                    attacker.Shove(toDir * -1f, 2.5f, this);
                    if (lateDodge)
                    {
                        // Pisar na Lâmina: the thrust is pinned to the floor.
                        attacker.Disarm(60);
                        attacker.ApplyPosture(40f);
                        world.Fx.Popup(Strings.PopupStomp, attacker.Position + Vector2.up * 1.3f, pal.gold);
                        world.HintOnce(HintId.MasterAnswers);
                    }
                    world.AddFlow(flowTuning.counterPoints);
                    for (int i = 0; i < Build.Effects.Count; i++) Build.Effects[i].OnPerfectDodge(world, attacker);
                    break;

                case HitOutcome.Hit:
                    // Parried a spirit blow with bare steel: say why it went through.
                    if (attack.kind == AttackKind.Spirit && CurrentState == State.Parry)
                    {
                        world.Fx.Popup(Strings.PopupSpiritThrough, Position + Vector2.up * 1.5f, ColorUtil.Hex("#B07CFF"));
                        world.HintOnce(HintId.SpiritBlow);
                    }
                    if (attack.cursed) Curse();
                    TakeHit(attack, toDir, power);
                    break;
            }
            return outcome;
        }

        Enemy lastAttacker;

        /// <summary>
        /// The master answers (Mikiri, widened): a dodge started in the last frames before a
        /// sweep becomes a descending blow; toward a grab, a throw.
        /// </summary>
        void MasterDodge(AttackDef attack, Enemy attacker, Vector2 toDir)
        {
            if (attack.kind == AttackKind.Sweep)
            {
                descendFrames = DescendFrames;
                Squash(0.7f, 1.3f);
                world.Fx.Popup(Strings.PopupDescend, Position + Vector2.up * 1.3f, pal.gold);
                world.HintOnce(HintId.MasterAnswers);
            }
            else if (attack.kind == AttackKind.Grab && Vector2.Dot(dodgeDir, toDir) > 0.4f)
            {
                // Reversão: the grabber is thrown over the shoulder.
                attacker.Throw(toDir * -1f, this);
                world.Fx.Popup(Strings.PopupReversal, attacker.Position + Vector2.up * 1.3f, pal.gold);
                world.HintOnce(HintId.MasterAnswers);
            }
        }

        /// <summary>What hurt this duelist last (for automated run reports).</summary>
        public string LastHurtBy { get; private set; } = "";

        void TakeHit(AttackDef attack, Vector2 toAttacker, float power)
        {
            LastHurtBy = (lastAttacker != null ? lastAttacker.Def.id + ":" : "") + (attack.id ?? "perigo") + " sala=" + (world.CurrentRoom != null ? world.CurrentRoom.kind.ToString() : "?");
            bool maxFlow = Build.Flow.Level >= Build.Flow.MaxLevel;
            float damage = attack.damage * power * Mods.damageTakenMult * (maxFlow ? Mods.damageTakenAtMaxFlowMult : 1f);
            if (!world.PlayerInvincible) Health = Mathf.Max(0f, Health - damage);
            if (Health <= 0f)
            {
                // A technique may refuse the fall once (Ossos Teimosos).
                for (int i = 0; i < Build.Effects.Count; i++)
                {
                    if (!Build.Effects[i].OnLethalHit(world)) continue;
                    Health = 1f;
                    hurtInvuln = Mathf.Max(hurtInvuln, 60);
                    break;
                }
            }

            world.Fx.Emit(FxType.PlayerHurt, Position, -toAttacker, damage);
            world.Fx.Blood(Position, -toAttacker, 6f + damage * 0.3f, ColorUtil.Hex("#8E0F1E"));
            world.DropFlow(1 + Mods.extraFlowLossOnHurt);
            Build.BreakStreak();
            OnHurtKi(toAttacker, lastAttacker);
            lastAttacker = null;
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

            if (Posture.Add(attack.postureDamage * power * t.hitPostureFraction) && !PostureUnbreakable)
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

        const float Anticipation = 16f;
        const float Overshoot = 18f;

        /// <summary>+1 when the swing travels counter-clockwise, -1 clockwise.</summary>
        float SwingSide(PlayerAttackDef step)
        {
            SwingAngles(step, out float from, out float to);
            return to >= from ? 1f : -1f;
        }

        bool IsFinalComboStep => CurrentState == State.Attack && ComboIndex == Weapon.combo.Length - 1;

        /// <summary>
        /// Weapon angle at the current (sub-frame) moment of a strike: pull back past the start
        /// (anticipation), whip through and past the end (overshoot), then settle — with the
        /// katana's chiburi flick after the last hit of its combo.
        /// </summary>
        float StrikeAngle()
        {
            PlayerAttackDef step = CurrentStep;
            float f = AnimFrame(StateFrame);
            float tW = Mathf.Clamp01(f / Mathf.Max(1, step.windup));
            float tA = Mathf.Clamp01((f - step.windup) / step.active);
            float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step)));
            bool winding = f < step.windup;
            bool active = !winding && f < step.windup + step.active;

            switch (step.style)
            {
                case SwingStyle.Sweep:
                case SwingStyle.Spin:
                case SwingStyle.Slam:
                {
                    SwingAngles(step, out float cutFrom, out float cutTo);
                    float side = cutTo >= cutFrom ? 1f : -1f;
                    float from = cutFrom - side * Anticipation;
                    float to = cutTo + side * (step.style == SwingStyle.Spin ? 0f : Overshoot);
                    float restNow = RestNow;
                    // From the guard to the start of the cut the short way round (never through the enemy).
                    if (winding) return restNow + Mathf.DeltaAngle(restNow, from) * CombatMath.EaseInOutSine(tW);
                    if (active) return Mathf.Lerp(from, to, CombatMath.EaseOutCubic(tA));
                    float settle = step.style == SwingStyle.Spin
                        ? Mathf.Lerp(to, to + Mathf.DeltaAngle(to, restNow), CombatMath.EaseInOutSine(tR))
                        : to + Mathf.DeltaAngle(to, restNow) * CombatMath.EaseInOutSine(tR);
                    if (IsFinalComboStep && Weapon.flourish > 0f)
                    {
                        // Chiburi: a sharp flick of the blade before it settles.
                        settle -= side * Weapon.flourish * Mathf.Sin(Mathf.Clamp01((tR - 0.2f) / 0.45f) * Mathf.PI);
                    }
                    return settle;
                }
                case SwingStyle.Jab:
                {
                    float hand = (ComboIndex % 2 == 0 ? 12f : -12f) * (winding ? CombatMath.EaseInOutSine(tW) : 1f - CombatMath.EaseInOutSine(tR));
                    return hand + ThrustGuardAngle(step, winding, active, tW, tA, tR);
                }
                default:
                    if (IsFinalComboStep && Weapon.flourish > 0f && !winding && !active)
                    {
                        // Polearm twirl after the last thrust.
                        return Weapon.flourish * Mathf.Sin(tR * Mathf.PI * 2f) * (1f - tR);
                    }
                    return ThrustGuardAngle(step, winding, active, tW, tA, tR);
            }
        }

        /// <summary>
        /// A thrust starts where the guard holds the blade (high, low or behind), straightens
        /// onto the target as it lands, and goes back to the guard.
        /// </summary>
        float ThrustGuardAngle(PlayerAttackDef step, bool winding, bool active, float tW, float tA, float tR)
        {
            float start = StanceThrustStart(step);
            if (Mathf.Abs(start) < 0.01f) return 0f;
            float restNow = RestNow;
            if (winding) return restNow + Mathf.DeltaAngle(restNow, start) * CombatMath.EaseInOutSine(tW);
            if (active) return Mathf.Lerp(start, 0f, CombatMath.EaseOutCubic(Mathf.Clamp01(tA * 1.6f)));
            return Mathf.DeltaAngle(0f, restNow) * CombatMath.EaseInOutSine(tR);
        }

        /// <summary>Thrusts and jabs: draw back, spring past full reach, then return.</summary>
        float StrikeExtend()
        {
            PlayerAttackDef step = CurrentStep;
            if (step.style == SwingStyle.Sweep || step.style == SwingStyle.Spin) return SweepExtend(step);
            float f = AnimFrame(StateFrame);
            float tW = Mathf.Clamp01(f / Mathf.Max(1, step.windup));
            float tA = Mathf.Clamp01((f - step.windup) / step.active);
            float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step)));
            if (f < step.windup) return Mathf.Lerp(0f, -0.35f, CombatMath.EaseInOutSine(tW));
            if (f < step.windup + step.active) return Mathf.LerpUnclamped(-0.35f, step.extend, CombatMath.EaseOutBack(tA));
            return Mathf.Lerp(step.extend, 0f, CombatMath.EaseInOutSine(tR));
        }

        /// <summary>Sweeps reach out at mid-swing (and chain weapons fly out to their extend).</summary>
        float SweepExtend(PlayerAttackDef step)
        {
            float f = AnimFrame(StateFrame);
            float tA = Mathf.Clamp01((f - step.windup) / step.active);
            float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step)));
            float reach = Mathf.Max(0.12f, step.extend);
            if (f < step.windup) return Mathf.Lerp(0f, -0.1f, f / Mathf.Max(1, step.windup));
            if (f < step.windup + step.active) return reach * Mathf.Sin(tA * Mathf.PI * 0.5f + 0.4f);
            return Mathf.Lerp(reach * 0.6f, 0f, CombatMath.EaseInOutSine(tR));
        }

        protected override float BodyLean
        {
            get
            {
                if (CurrentState == State.Attack || CurrentState == State.SwapStrike)
                {
                    PlayerAttackDef step = CurrentStep;
                    if (step.style == SwingStyle.Thrust || step.style == SwingStyle.Jab) return 0f;
                    float side = SwingSide(step);
                    float f = AnimFrame(StateFrame);
                    if (f < step.windup) return -side * 12f * CombatMath.EaseInOutSine(f / Mathf.Max(1, step.windup));
                    float tA = Mathf.Clamp01((f - step.windup) / step.active);
                    float tR = Mathf.Clamp01((f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step)));
                    return f < step.windup + step.active ? Mathf.Lerp(-12f, 20f, tA) * side : 20f * side * (1f - CombatMath.EaseInOutSine(tR));
                }
                if (CurrentState == State.Deflected) return -deflectSide * 18f * Bounce(AnimFrame(StateFrame), t.deflectedFrames, 3);
                if (CurrentState == State.Finisher && finisherStyle == FinisherStyle.Execution && StateFrame < 18) return -10f;
                return CurrentState == State.Free ? StanceLean : 0f;
            }
        }

        protected override bool TrailEmitting
        {
            get
            {
                if (CurrentState == State.Attack || CurrentState == State.SwapStrike)
                {
                    PlayerAttackDef step = CurrentStep;
                    float f = AnimFrame(StateFrame);
                    bool flourishing = IsFinalComboStep && Weapon.flourish > 0f && f >= step.windup + step.active;
                    return (f >= step.windup - 0.5f && f < step.windup + step.active + 1.5f) || (flourishing && f < step.windup + step.active + RecoveryFrames(step) * 0.6f);
                }
                if (CurrentState == State.Finisher) return StateFrame <= 8 || (StateFrame >= 16 && StateFrame <= 30);
                // Changing guard: the blade's turn leaves a brief streak.
                if (CurrentState == State.Free && kamaeCooldown > 0 && KamaeCooldown - kamaeCooldown < StanceFlourishFrames - 4) return true;
                return CurrentState == State.Swap && StateFrame < 6;
            }
        }

        protected override Color TrailTint => TrailColor();

        /// <summary>The chiburi flick throws the blood off the blade.</summary>
        void EmitChiburiBlood()
        {
            if (flickDone || !IsFinalComboStep || Weapon.flourish <= 0f || hitThisSwing.Count == 0) return;
            PlayerAttackDef step = CurrentStep;
            float f = AnimFrame(StateFrame);
            float tR = (f - step.windup - step.active) / Mathf.Max(1, RecoveryFrames(step));
            if (tR < 0.4f) return;
            flickDone = true;
            float rad = WeaponWorldAngle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 tip = RenderPosition + dir * (WeaponReach + WeaponLength * 0.9f);
            world.Fx.Blood(tip, CombatMath.Perpendicular(dir) * -SwingSide(step), 7f, ColorUtil.Hex("#8E0F1E"));
        }

        protected override WeaponPose GetWeaponPose()
        {
            var pose = new WeaponPose { tint = weaponColor };
            // Idle, the blade sits where the guard holds it (and flourishes when the guard changes).
            StanceIdlePose(ref pose);
            if (CurrentState != State.Free && CurrentState != State.Standoff)
            {
                pose.angle = RestNow;
                pose.extend = 0f;
                pose.tint = weaponColor;
                pose.snap = false;
            }

            switch (CurrentState)
            {
                case State.Parry:
                    bool perfect = StateFrame < parryWindow;
                    StanceParry(perfect, out pose.angle, out pose.extend);
                    pose.snap = StateFrame < 4;
                    if (perfect) pose.tint = Color.Lerp(weaponColor, pal.playerAccent, 0.75f);
                    break;
                case State.Swap:
                    // The new weapon spins into the hand.
                    pose.snap = true;
                    pose.angle = RestNow + 360f * (1f - CombatMath.EaseOutCubic(Mathf.Clamp01(AnimFrame(StateFrame) / t.swapFrames)));
                    if (StateFrame < swapWindow) pose.tint = Color.Lerp(weaponColor, Color.white, 0.6f);
                    break;
                case State.Attack:
                case State.SwapStrike:
                    pose.snap = true;
                    pose.angle = StrikeAngle();
                    pose.extend = StrikeExtend();
                    if (CurrentState == State.SwapStrike) pose.tint = Color.Lerp(weaponColor, pal.gold, 0.5f);
                    break;
                case State.Deflected:
                {
                    pose.snap = true;
                    float f = AnimFrame(StateFrame);
                    float knocked = deflectFromAngle + deflectSide * 65f;
                    pose.angle = f < 4f
                        ? Mathf.Lerp(deflectFromAngle, knocked, CombatMath.EaseOutBack(f / 4f))
                        : knocked + Mathf.DeltaAngle(knocked, RestNow) * CombatMath.EaseInOutSine((f - 4f) / Mathf.Max(1, t.deflectedFrames - 4));
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
                    FinisherPose(ref pose);
                    break;
                case State.Dead:
                    pose.angle = -150f;
                    pose.tint = weaponColor * 0.5f;
                    break;
            }

            // The parry clash: the blade is slammed back by the enemy's sword, then settles.
            if (clashFrames > 0 && (CurrentState == State.Parry || CurrentState == State.Free || CurrentState == State.Swap))
            {
                float f = ClashFrames - clashFrames - 1f + AnimFrame(1);
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

        void FinisherPose(ref WeaponPose pose)
        {
            float f = AnimFrame(StateFrame);
            pose.tint = Color.Lerp(weaponColor, finisherKilled ? ColorUtil.Hex("#8E0F1E") : pal.gold, 0.45f);
            switch (finisherStyle)
            {
                case FinisherStyle.CleanCut:
                    // Blade held behind after the pass, then slowly sheathed.
                    pose.angle = f < 6 ? Mathf.Lerp(80f, -150f, f / 6f) : Mathf.Lerp(-150f, -95f, Mathf.Clamp01((f - 6) / 24f));
                    break;
                case FinisherStyle.Execution:
                    pose.angle = f < 18 ? RestNow + Mathf.DeltaAngle(RestNow, 170f) * CombatMath.EaseOutCubic(f / 18f) : Mathf.Lerp(170f, 0f, CombatMath.EaseOutCubic(Mathf.Clamp01((f - 18) / 3f)));
                    pose.extend = f < 18 ? -0.2f : 0.25f;
                    break;
                case FinisherStyle.ThousandCuts:
                    pose.angle = 25f * Mathf.Sin(f * 0.8f);
                    pose.extend = 0.35f * Mathf.Abs(Mathf.Sin(f * 0.8f));
                    break;
                case FinisherStyle.Impale:
                    pose.angle = 0f;
                    pose.extend = f < 6 ? Mathf.Lerp(-0.3f, 0.9f, f / 6f) : 0.9f;
                    break;
                case FinisherStyle.Reap:
                    pose.angle = 0f;
                    pose.extend = f < 8 ? Mathf.Lerp(0f, 1.6f, f / 8f) : 1.6f;
                    break;
                case FinisherStyle.HeartBreak:
                    pose.angle = 10f * Mathf.Sin(f * 0.5f);
                    pose.extend = f >= 24 ? 0.7f : 0.35f * Mathf.Abs(Mathf.Sin(f * 0.5f));
                    break;
            }
        }

        protected override void RenderExtra(Vector2 renderPosition, float visualDt)
        {
            Color c = Color.white;
            Color glow = Color.clear;
            if (CurrentState == State.GuardBroken) c = Color.Lerp(Color.white, pal.postureHigh, 0.5f);
            if (CurrentState == State.Dead) c = new Color(0.45f, 0.45f, 0.5f, 1f);
            if (hurtInvuln > 0 && CurrentState != State.Hurt && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f) c.a = 0.35f;
            if (IsDodgeInvulnerable)
            {
                c.a = 0.6f;
                glow = pal.playerAccent.WithAlpha(0.35f);
            }
            if (Flashing) glow = Color.white.WithAlpha(c.a);
            SetBodyLook(c, glow);

            float facingAngle = FacingAngle;
            RenderOffhandAndChain(c.a);

            bool showParry = CurrentState == State.Parry || (CurrentState == State.Swap && StateFrame < swapWindow);
            parryArc.enabled = showParry;
            if (showParry)
            {
                bool perfect = CurrentState == State.Swap || StateFrame < parryWindow;
                parryArc.color = (CurrentState == State.Swap ? pal.gold : pal.playerAccent).WithAlpha(perfect ? 0.6f : 0.22f);
                parryArc.transform.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                float s = perfect ? 2.3f : 2.0f;
                parryArc.transform.localScale = new Vector3(s, s, 1f);
            }

            EmitChiburiBlood();

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

        /// <summary>Second dagger/fist mirrors the main hand; chain weapons draw their chain.</summary>
        void RenderOffhandAndChain(float alpha)
        {
            WeaponDef w = Weapon;
            int order = body.sortingOrder;
            if (offhand.enabled)
            {
                float mirror = -(WeaponWorldAngle - FacingAngle) + FacingAngle;
                bool jab = (CurrentState == State.Attack || CurrentState == State.SwapStrike) && CurrentStep.style == SwingStyle.Jab;
                float reach = jab && ComboIndex % 2 == 1 ? WeaponReach : Radius * 0.55f;
                float rad = mirror * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                offhand.transform.localPosition = (Vector3)(dir * reach) + new Vector3(0f, 0f, 0f);
                offhand.transform.localRotation = Quaternion.Euler(0f, 0f, mirror);
                offhand.transform.localScale = new Vector3(w.length, w.length, 1f);
                offhand.color = weapon.color.WithAlpha(alpha);
                offhand.sortingOrder = Mathf.Sin(rad) > 0.2f ? order - 1 : order + 2;
                offhand.flipY = Mathf.Cos(rad) < 0f;
            }

            bool showChain = w.chain && WeaponReach > Radius * 0.55f + 0.4f;
            chain.enabled = showChain;
            if (showChain)
            {
                float rad = WeaponWorldAngle * Mathf.Deg2Rad;
                chain.transform.localPosition = Vector3.zero;
                chain.transform.localRotation = Quaternion.Euler(0f, 0f, WeaponWorldAngle);
                if (chain.drawMode == SpriteDrawMode.Tiled)
                {
                    chain.transform.localScale = Vector3.one;
                    chain.size = new Vector2(WeaponReach, chain.sprite.bounds.size.y);
                    chain.color = Color.white.WithAlpha(alpha);
                }
                else
                {
                    chain.transform.localScale = new Vector3(WeaponReach, 0.05f, 1f);
                    chain.color = ColorUtil.Hex("#8C8A86").WithAlpha(alpha);
                }
                chain.sortingOrder = Mathf.Sin(rad) > 0.2f ? order - 2 : order + 1;
            }
        }
    }
}
