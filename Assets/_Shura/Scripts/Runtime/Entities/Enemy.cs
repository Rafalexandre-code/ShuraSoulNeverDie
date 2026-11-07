using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Data-driven duelist AI. Keeps its preferred range, waits for the attack token, then
    /// commits to a fixed, telegraphed move. A perfect parry stops the swing (the blade
    /// bounces and the enemy is open); when it deflects the player's swing, the player's
    /// blade bounces instead. Never changes how a move behaves at random.
    /// </summary>
    public sealed class Enemy : Fighter
    {
        public enum State
        {
            Spawning,
            Engage,
            Attacking,
            /// <summary>Blade bounced off a perfect parry: open to punishment.</summary>
            Recoil,
            Stagger,
            Broken,
            Finishing,
            Dying,
            Dead,
        }

        const float BarWidth = 1.1f;
        const int TeleportFadeFrames = 10;

        EnemyTuning et;
        FeelTuning feel;
        Palette pal;
        SeededRandom rng;

        bool stepHit;
        int idleTimer;
        int staggerFrames;
        int strafeTimer;
        int blockStreak;
        float strafeSign = 1f;

        int recoilDuration;
        float recoilFromAngle;
        float recoilSide;
        bool recoilIsThrust;
        bool resumeAfterRecoil;
        int guardFrames;

        EnemyScaling scaling;
        bool gibbed;
        float dotDps;
        float dotTime;
        Element dotElement;
        float dotAccumulator;
        float chillTime;
        int stepWindup;
        int stepTrack;
        bool stepAdapted;
        SpriteRenderer eliteAura;

        SpriteRenderer barBack, healthFill, postureFill, dangerGlyph, areaPreview, ringPreview, linePreview, seal, brokenRing;
        SpriteRenderer timingTarget, timingApproach, timingGlow;

        public EnemyDef Def { get; private set; }
        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public MoveDef CurrentMove { get; private set; }
        public int StepIndex { get; private set; }
        public int StepFrame { get; private set; }
        /// <summary>Increments every time a new swing starts (lets observers tell swings apart).</summary>
        public int AttackSerial { get; private set; }
        public bool Mastered { get; private set; }
        public bool IsElite => scaling.elite;
        public bool IsChilled => chillTime > 0f;
        public Sprite BodySprite => body.sprite;
        public Color BodyColor => Def.color;
        /// <summary>Damage multiplier from depth and Guardian status.</summary>
        public float PowerMult => scaling.damage;
        /// <summary>Frames until the current swing becomes active (adaptive holds included).</summary>
        public int FramesUntilActive => CurrentAttack != null ? stepWindup - StepFrame : int.MaxValue;

        public AttackDef CurrentAttack =>
            CurrentState == State.Attacking && CurrentMove != null ? CurrentMove.steps[StepIndex] : null;

        public bool CanBeHit =>
            CurrentState != State.Spawning && CurrentState != State.Finishing &&
            CurrentState != State.Dying && CurrentState != State.Dead;

        public bool IsThreatening
        {
            get
            {
                AttackDef a = CurrentAttack;
                return a != null && StepFrame < stepWindup + a.active;
            }
        }

        public bool IsBroken => CurrentState == State.Broken;

        public bool IsGone => CurrentState == State.Dying || CurrentState == State.Dead;

        public void Init(World owner, EnemyDef def, Vector2 spawn, GameConfig config, SeededRandom random, EnemyScaling enemyScaling)
        {
            Def = def;
            scaling = enemyScaling;
            et = config.enemies;
            feel = config.feel;
            pal = config.palette;
            rng = random;
            Mass = def.mass;
            MaxHealth = def.maxHealth * scaling.health;
            Health = MaxHealth;
            Posture = new PostureMeter(def.maxPosture * scaling.posture);
            float size = scaling.elite ? DifficultyRules.EliteSizeMult : 1f;
            if (scaling.elite) Mass *= 2f;

            BuildView(owner, PixelArt.Character(def.art), def.radius * size, size * def.artScale);
            Sprite weaponArt = def.weapon == Shape.Bow ? PixelArt.WeaponCentered(def.weaponArt) : PixelArt.Weapon(def.weaponArt);
            BuildWeapon(weaponArt != null ? weaponArt : ShapeLibrary.Get(def.weapon), weaponArt != null ? Color.white : def.weaponColor, def.weaponLength * size);
            eliteAura = SpriteUtil.Make("GuardianAura", transform, ShapeLibrary.Get(Shape.SoftCircle), pal.gold, Sorting.Shadow + 3);
            eliteAura.enabled = scaling.elite;
            Teleport(spawn);
            Vector2 toPlayer = owner.Player.Position - spawn;
            if (toPlayer.sqrMagnitude > 1e-4f) Facing = toPlayer.normalized;

            float barY = VisualTop + 0.25f;
            barBack = SpriteUtil.Make("BarBack", transform, ShapeLibrary.Get(Shape.Pixel), new Color(0f, 0f, 0f, 0.65f), Sorting.Overlay);
            barBack.transform.localPosition = new Vector3(0f, barY, 0f);
            barBack.transform.localScale = new Vector3(BarWidth + 0.08f, 0.22f, 1f);
            healthFill = SpriteUtil.Make("Health", transform, ShapeLibrary.Get(Shape.PixelLeft), pal.health, Sorting.Overlay + 1);
            healthFill.transform.localPosition = new Vector3(-BarWidth * 0.5f, barY + 0.045f, 0f);
            postureFill = SpriteUtil.Make("Posture", transform, ShapeLibrary.Get(Shape.Pixel), pal.postureLow, Sorting.Overlay + 1);
            postureFill.transform.localPosition = new Vector3(0f, barY - 0.05f, 0f);

            dangerGlyph = SpriteUtil.Make("Danger", transform, ShapeLibrary.Get(Shape.Diamond), pal.danger, Sorting.Overlay + 2);
            dangerGlyph.transform.localPosition = new Vector3(0f, barY + 0.45f, 0f);
            areaPreview = SpriteUtil.Make("AreaPreview", transform, ShapeLibrary.Wedge(110f), Color.white, Sorting.Ground + 1);
            ringPreview = SpriteUtil.Make("RingPreview", transform, ShapeLibrary.Get(Shape.Ring), pal.danger, Sorting.Ground + 2);
            linePreview = SpriteUtil.Make("LinePreview", transform, ShapeLibrary.Get(Shape.PixelLeft), Color.white, Sorting.Ground + 1);
            seal = SpriteUtil.Make("Seal", transform, ShapeLibrary.Get(Shape.DiamondOutline), pal.gold, Sorting.Overlay + 2);
            seal.transform.localPosition = new Vector3(BarWidth * 0.5f + 0.25f, barY, 0f);
            seal.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            brokenRing = SpriteUtil.Make("BrokenRing", transform, ShapeLibrary.Get(Shape.Ring), pal.gold, Sorting.Overlay - 2);

            timingGlow = SpriteUtil.Make("TimingGlow", transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay - 4);
            timingTarget = SpriteUtil.Make("TimingTarget", transform, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);
            timingApproach = SpriteUtil.Make("TimingApproach", transform, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);

            dangerGlyph.enabled = areaPreview.enabled = ringPreview.enabled = linePreview.enabled = false;
            seal.enabled = brokenRing.enabled = false;
            timingGlow.enabled = timingTarget.enabled = timingApproach.enabled = false;

            strafeTimer = rng.Range(60, 140);
            Enter(State.Spawning);
            owner.Fx.Emit(FxType.Spawn, spawn, Vector2.up, Radius, scaling.elite ? pal.gold : def.color);
            if (scaling.elite) owner.Fx.Popup(Strings.PopupGuardian + "  ·  " + def.displayName, spawn + Vector2.up * 1.4f, pal.gold);
        }

        public void Tick()
        {
            BeginTick();
            if (CurrentState == State.Dead) return;
            StateFrame++;
            if (guardFrames > 0) guardFrames--;
            TickStatus();
            if (CurrentState == State.Dead) return;

            switch (CurrentState)
            {
                case State.Spawning:
                    if (StateFrame >= et.spawnFrames) EnterEngage(rng.Range(20, 50));
                    break;
                case State.Engage:
                    TickEngage();
                    break;
                case State.Attacking:
                    TickAttack();
                    break;
                case State.Recoil:
                    TickRecoil();
                    break;
                case State.Stagger:
                    Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.2f);
                    RegenPosture();
                    if (StateFrame >= staggerFrames) EnterEngage(rng.Range(15, 35));
                    break;
                case State.Broken:
                    Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.25f);
                    if (StateFrame >= et.brokenFrames)
                    {
                        // Missed the finisher window: recovers, but keeps half its posture.
                        Posture.Reset(Posture.Max * et.brokenRecoverPostureRatio);
                        EnterEngage(30);
                    }
                    break;
                case State.Finishing:
                    Velocity = Vector2.zero;
                    break;
                case State.Dying:
                    Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.2f);
                    if (StateFrame >= et.dyingFrames) Enter(State.Dead);
                    break;
            }
            Integrate();
        }

        void Enter(State state)
        {
            CurrentState = state;
            StateFrame = 0;
        }

        void EnterEngage(int idleFrames)
        {
            Enter(State.Engage);
            idleTimer = idleFrames;
        }

        void TickEngage()
        {
            PlayerController player = world.Player;
            Vector2 to = player.Position - Position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.001f ? to / dist : Facing;
            Facing = CombatMath.RotateTowards(Facing, dir, et.turnDegreesPerSecond * SimClock.Dt);
            RegenPosture();

            // Enemies waiting for their turn hang back so the active duel stays readable.
            bool waiting = world.Coordinator.Holder != null && !world.Coordinator.IsHeldBy(this);
            float range = Def.preferredRange + (waiting ? et.waitingRangeBonus : 0f);

            Vector2 desired;
            if (!player.IsAlive) desired = Vector2.zero;
            else if (dist > range + 0.6f) desired = dir * Def.moveSpeed;
            else if (dist < range - 0.6f) desired = -dir * Def.moveSpeed * 0.7f;
            else desired = CombatMath.Perpendicular(dir) * strafeSign * Def.moveSpeed * 0.45f;

            if (--strafeTimer <= 0)
            {
                strafeTimer = rng.Range(60, 140);
                if (rng.NextFloat() < 0.5f) strafeSign = -strafeSign;
            }
            if (IsChilled) desired *= 0.55f;
            Velocity = Vector2.Lerp(Velocity, desired, 0.15f);

            if (!player.IsAlive) return;
            if (--idleTimer <= 0) TryChooseAttack(dist);
        }

        int Idle(int min, int max)
        {
            float mult = scaling.idle * (1f - 0.45f * world.Adaptation.Pressure) * (IsChilled ? 1.4f : 1f);
            return Mathf.Max(6, Mathf.RoundToInt(rng.Range(min, max) * mult));
        }

        /// <summary>Precise parriers see more of the moves that cannot be parried.</summary>
        int MoveWeight(MoveDef m)
        {
            bool dangerous = false;
            foreach (AttackDef a in m.steps)
            {
                if (a.IsDangerous) dangerous = true;
            }
            float bias = dangerous ? 1f + 1.5f * world.Adaptation.Danger : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(m.weight * bias * 2f));
        }

        void TryChooseAttack(float dist)
        {
            int total = 0;
            foreach (MoveDef m in Def.moves)
            {
                if (dist >= m.minRange && dist <= m.maxRange) total += MoveWeight(m);
            }
            if (total == 0)
            {
                idleTimer = 8;
                return;
            }
            if (!world.Coordinator.TryAcquire(this))
            {
                idleTimer = rng.Range(10, 25);
                return;
            }

            int roll = rng.Range(0, total);
            foreach (MoveDef m in Def.moves)
            {
                if (dist < m.minRange || dist > m.maxRange) continue;
                int w = MoveWeight(m);
                if (roll < w)
                {
                    StartMove(m);
                    return;
                }
                roll -= w;
            }
        }

        void StartMove(MoveDef move)
        {
            CurrentMove = move;
            StepIndex = 0;
            Enter(State.Attacking);
            StartStep();
        }

        void StartStep()
        {
            StepFrame = 0;
            stepHit = false;
            AttackSerial++;
            AttackDef attack = CurrentAttack;
            AdaptStep(attack);

            if (attack.teleportBehind)
            {
                world.TeleportBehindPlayer(this);
                world.HintOnce(HintId.Shadow);
            }

            if (attack.IsDangerous)
            {
                world.Fx.Emit(FxType.DangerCue, Position, Facing, 1f, pal.danger);
                world.HintOnce(attack.kind == AttackKind.Thrust ? HintId.DangerThrust
                             : attack.kind == AttackKind.Sweep ? HintId.DangerSweep
                             : HintId.DangerGrab);
            }
            else
            {
                world.HintOnce(attack.IsProjectile ? HintId.Projectile : HintId.Parry);
            }
            Squash(0.88f, 1.1f);
            areaPreview.sprite = ShapeLibrary.Wedge(attack.shape.type == HitShapeType.Circle ? 360f : attack.shape.arcDegrees);
        }

        /// <summary>
        /// Applies what the enemies learned to this swing. A held strike lengthens the windup
        /// (the ring and the trembling blade show it); longer tracking always commits at least
        /// 6 frames before impact so a well-timed dodge still works.
        /// </summary>
        void AdaptStep(AttackDef attack)
        {
            AdaptiveTraits traits = world.Adaptation;
            stepWindup = attack.windup;
            stepAdapted = false;
            if (!attack.IsProjectile && !attack.teleportBehind && rng.NextFloat() < traits.Delay * 0.6f)
            {
                stepWindup += rng.Range(8, 19);
                stepAdapted = true;
            }
            int trackBonus = Mathf.RoundToInt(6f * traits.Track);
            stepTrack = Mathf.Min(attack.trackUntil + trackBonus, Mathf.Max(attack.trackUntil, stepWindup - 6));
        }

        void TickAttack()
        {
            StepFrame++;
            AttackDef attack = CurrentAttack;
            // Arrows cue on their own flight; melee cues when the ring closes.
            if (!attack.IsProjectile && StepFrame == TelegraphTiming.CueFrame(stepWindup, feel.timingCueLeadFrames))
            {
                world.Fx.Emit(FxType.TimingCue, Position, Facing, attack.IsDangerous ? 1f : 0f);
            }

            Vector2 to = world.Player.Position - Position;
            if (StepFrame <= stepTrack && to.sqrMagnitude > 1e-4f)
            {
                Facing = CombatMath.RotateTowards(Facing, to.normalized, et.turnDegreesPerSecond * 1.5f * SimClock.Dt);
            }
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.25f);
            RegenPosture();

            if (StepFrame == stepWindup)
            {
                world.Fx.Emit(FxType.EnemySwing, Position, Facing, attack.IsDangerous ? 1.5f : attack.IsProjectile ? 2f : 1f);
                Squash(1.25f, 0.82f);
                if (attack.IsProjectile) world.SpawnProjectile(this, attack, CurrentMove.isSignature);
            }

            bool active = StepFrame >= stepWindup && StepFrame < stepWindup + attack.active;
            if (active && !attack.IsProjectile)
            {
                if (attack.lunge > 0f) Position += Facing * (attack.lunge / attack.active);
                if (!stepHit && world.TryHitPlayer(this, attack)) stepHit = true;
                // A perfect answer can break posture, recoil or stagger us mid-swing.
                if (CurrentState != State.Attacking) return;
            }

            if (StepFrame < stepWindup + attack.active + attack.recovery) return;
            if (StepIndex < CurrentMove.steps.Length - 1)
            {
                StepIndex++;
                StartStep();
            }
            else
            {
                EndMove();
            }
        }

        void TickRecoil()
        {
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.2f);
            RegenPosture();
            if (StateFrame < recoilDuration) return;
            if (resumeAfterRecoil && CurrentMove != null)
            {
                // Combos continue after the bounce: every strike of a flurry is its own clash.
                StepIndex++;
                Enter(State.Attacking);
                StartStep();
            }
            else
            {
                EndMove();
            }
        }

        void EndMove()
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            EnterEngage(Idle(Def.idleMin, Def.idleMax));
        }

        void RegenPosture()
        {
            // Wounded enemies recover posture slower, rewarding sustained pressure.
            float hpRatio = MaxHealth > 0f ? Health / MaxHealth : 0f;
            Posture.Tick(Def.postureRegenPerSecond * (0.25f + 0.75f * hpRatio), et.postureRegenDelay);
        }

        // ── Reactions ───────────────────────────────────────────────────────────────────

        /// <summary>Perfectly parried: the blade stops and bounces back; the swing ends.</summary>
        public void OnParried(float posture)
        {
            Squash(0.82f, 1.15f);
            Velocity = -Facing * 2.2f;
            if (Posture.Add(posture))
            {
                BreakPosture();
                return;
            }
            AttackDef attack = CurrentAttack;
            if (attack == null) return;

            recoilFromAngle = CurrentWeaponAngle;
            recoilSide = StepIndex % 2 == 0 ? 1f : -1f;
            recoilIsThrust = attack.shape.type == HitShapeType.Line;
            resumeAfterRecoil = StepIndex < CurrentMove.steps.Length - 1;
            recoilDuration = resumeAfterRecoil ? et.recoilFrames : et.recoilFinalFrames;
            Flash(0.05f);
            Enter(State.Recoil);
        }

        public void OnCounterThrust(float posture)
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            Velocity = -Facing * 3f;
            Flash(0.08f);
            if (Posture.Add(posture))
            {
                BreakPosture();
                return;
            }
            staggerFrames = et.counterStaggerFrames;
            Enter(State.Stagger);
        }

        /// <summary>Posture damage from techniques (shockwaves, Muralha…).</summary>
        public void ApplyPosture(float amount)
        {
            if (!CanBeHit || IsBroken) return;
            if (Posture.Add(amount)) BreakPosture();
        }

        /// <summary>Posture damage and wounds from a player strike. Reports whether it bit or was deflected.</summary>
        public StrikeResult ReceivePlayerHit(float damage, float posture, Vector2 hitDir, bool unblockable)
        {
            if (!CanBeHit) return StrikeResult.None;
            Vector2 contact = Position - hitDir * Radius;

            bool facingAttacker = Vector2.Dot(Facing, -hitDir) > 0.26f;
            float blockChance = Mathf.Min(0.95f, Def.blockChance + 0.25f * world.Adaptation.Guard);
            if (!unblockable && CurrentState == State.Engage && facingAttacker && rng.NextFloat() < blockChance)
            {
                Deflect(posture, hitDir, contact);
                return StrikeResult.Deflected;
            }

            blockStreak = 0;
            Health -= damage;
            Flash(0.07f);
            Squash(Def.superArmor ? 0.92f : 0.8f, Def.superArmor ? 1.08f : 1.2f);
            if (!Def.superArmor && CurrentState != State.Attacking && CurrentState != State.Broken) Velocity = hitDir * et.hitKnockback;
            world.Fx.Emit(FxType.HitEnemy, contact, hitDir, damage, Def.bloodColor);

            if (Health <= 0f)
            {
                Die();
                return StrikeResult.Hit;
            }
            if (CurrentState != State.Broken && Posture.Add(posture)) BreakPosture();
            return StrikeResult.Hit;
        }

        // ── Status effects (from equipment) ─────────────────────────────────────────────

        public void ApplyDot(float damagePerSecond, float seconds, Element element)
        {
            if (!CanBeHit) return;
            dotDps = Mathf.Max(dotDps, damagePerSecond);
            dotTime = Mathf.Max(dotTime, seconds);
            dotElement = element;
        }

        public void ApplyChill(float seconds)
        {
            if (!CanBeHit) return;
            chillTime = Mathf.Max(chillTime, seconds);
        }

        void TickStatus()
        {
            if (chillTime > 0f) chillTime -= SimClock.Dt;
            if (dotTime <= 0f || !CanBeHit) return;
            dotTime -= SimClock.Dt;
            dotAccumulator += dotDps * SimClock.Dt;
            if (dotAccumulator < 2f) return;
            float dealt = dotAccumulator;
            dotAccumulator = 0f;
            Health -= dealt;
            world.Fx.Emit(FxType.DotTick, Position, Vector2.up, dealt, dotElement == Element.Blood ? Def.bloodColor : Elements.Color(dotElement));
            if (Health <= 0f) Die();
        }

        /// <summary>Chain weapons drag enemies in; never through the player, never Guardians or heavy armour much.</summary>
        public void Pull(Vector2 toward, float strength)
        {
            if (!CanBeHit || strength <= 0f) return;
            Vector2 to = toward - Position;
            float dist = to.magnitude;
            float keep = Radius + 0.9f;
            if (dist <= keep) return;
            float resist = Def.superArmor || IsElite ? 0.3f : 1f;
            Position += to / dist * Mathf.Min(dist - keep, 0.45f * strength * resist);
        }

        // ── Finisher control ────────────────────────────────────────────────────────────

        /// <summary>During a finisher the player's script places the victim.</summary>
        public void HoldAt(Vector2 position, float scale)
        {
            if (CurrentState != State.Finishing) return;
            Position = position;
            Velocity = Vector2.zero;
            Squash(scale, scale);
        }

        /// <summary>Killed by a finisher: the body is replaced by gore effects.</summary>
        public void DieByFinisher()
        {
            gibbed = true;
            Die();
        }

        /// <summary>The enemy guards: blades clash, the player's sword bounces off.</summary>
        void Deflect(float posture, Vector2 hitDir, Vector2 contact)
        {
            blockStreak++;
            guardFrames = et.guardVisualFrames;
            Facing = -hitDir;
            Squash(0.9f, 1.08f);
            Velocity = hitDir * et.hitKnockback * 0.6f;
            world.Fx.Emit(FxType.EnemyBlocked, contact, -hitDir);
            world.Player.OnDeflected();
            if (Posture.Add(posture * et.blockPostureMultiplier))
            {
                BreakPosture();
                return;
            }
            // Turtling forever is not allowed: after a few blocks it punishes mashing.
            int riposteAfter = world.Adaptation.Guard > 0.5f ? et.riposteAfterBlocks - 1 : et.riposteAfterBlocks;
            if (blockStreak >= riposteAfter)
            {
                blockStreak = 0;
                Riposte();
            }
        }

        void Riposte()
        {
            float dist = Vector2.Distance(world.Player.Position, Position);
            MoveDef fastest = null;
            foreach (MoveDef m in Def.moves)
            {
                AttackDef first = m.steps[0];
                if (first.IsDangerous || first.IsProjectile || first.teleportBehind || dist < m.minRange || dist > m.maxRange) continue;
                if (fastest == null || first.windup < fastest.steps[0].windup) fastest = m;
            }
            if (fastest != null && world.Coordinator.ForceAcquire(this)) StartMove(fastest);
        }

        void BreakPosture()
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            Velocity = Vector2.zero;
            Enter(State.Broken);
            world.Fx.Emit(FxType.PostureBreak, Position, Facing, Radius, pal.gold);
            world.HintOnce(HintId.Finisher);
        }

        public void BeginFinisher()
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            Velocity = Vector2.zero;
            Enter(State.Finishing);
        }

        public void MarkMastered()
        {
            Mastered = true;
        }

        public void Die()
        {
            if (IsGone) return;
            world.Coordinator.Release(this);
            CurrentMove = null;
            Health = 0f;
            Enter(State.Dying);
            if (scaling.elite && world.Run != null) world.Run.ElitesKilled++;
            world.OnEnemyDied(this);
            world.Fx.Emit(FxType.EnemyDeath, Position, Facing, Radius, Def.color);
        }

        // ── View ────────────────────────────────────────────────────────────────────────

        protected override bool HurtPose => CurrentState == State.Recoil || CurrentState == State.Stagger || Flashing;

        /// <summary>The strike pose snaps in just before the blow lands and holds through recovery.</summary>
        protected override bool StrikePose => CurrentState == State.Attacking && CurrentAttack != null && StepFrame >= stepWindup - 3;

        float IdleAngle =>
            Def.weapon == Shape.Spear || Def.weapon == Shape.Bow ? 0f :
            Def.weapon == Shape.Staff || Def.weapon == Shape.Axe ? 35f : -50f;

        protected override WeaponPose GetWeaponPose()
        {
            float idle = IdleAngle;
            var pose = new WeaponPose
            {
                angle = idle + 5f * Mathf.Sin(Time.unscaledTime * 1.5f + Def.radius * 10f),
                tint = weaponColor,
            };

            AttackDef attack = CurrentAttack;
            switch (CurrentState)
            {
                case State.Engage:
                    if (guardFrames > 0)
                    {
                        // Guard raised across the body, kicked by the impact of the player's blade.
                        int f = et.guardVisualFrames - guardFrames;
                        pose.snap = true;
                        pose.angle = 75f + 30f * Bounce(f, et.guardVisualFrames, 3);
                        pose.tint = Color.Lerp(weaponColor, Color.white, 0.5f * Bounce(f, et.guardVisualFrames, 2));
                    }
                    break;
                case State.Attacking:
                    if (attack != null)
                    {
                        pose.snap = true;
                        AttackPose(attack, idle, ref pose);
                    }
                    break;
                case State.Recoil:
                    pose.snap = true;
                    RecoilPose(idle, ref pose);
                    break;
                case State.Broken:
                case State.Finishing:
                    pose.angle = -150f;
                    pose.tint = weaponColor * 0.6f;
                    break;
                case State.Stagger:
                    pose.angle = idle - 70f + 12f * Mathf.Sin(Time.unscaledTime * 25f);
                    break;
                case State.Spawning:
                    pose.tint = weaponColor.WithAlpha(Mathf.Clamp01(StateFrame / (float)et.spawnFrames));
                    break;
                case State.Dying:
                    pose.angle = -160f;
                    pose.tint = weaponColor.WithAlpha(1f - Mathf.Clamp01(StateFrame / (float)et.dyingFrames));
                    break;
            }
            return pose;
        }

        /// <summary>The blade is knocked back the way it came, freezes, then drifts back to guard.</summary>
        void RecoilPose(float idle, ref WeaponPose pose)
        {
            float f = AnimFrame(StateFrame);
            float bump = Bounce(f, recoilDuration, 4);
            if (recoilIsThrust)
            {
                pose.angle = 25f * bump;
                pose.extend = Mathf.Lerp(0.2f, -0.6f, CombatMath.EaseOutCubic(Mathf.Clamp01(f / 4f))) * (0.4f + 0.6f * bump);
            }
            else
            {
                float knocked = recoilFromAngle + recoilSide * 60f;
                pose.angle = f < 4
                    ? Mathf.Lerp(recoilFromAngle, knocked, CombatMath.EaseOutBack(f / 4f))
                    : Mathf.Lerp(knocked, idle, CombatMath.EaseInOutSine((f - 4f) / Mathf.Max(1, recoilDuration - 4)));
            }
            pose.tint = Color.Lerp(weaponColor, Color.white, f < 4 ? 0.8f : 0f);
        }

        /// <summary>
        /// The weapon itself is the main telegraph: it pulls back through the whole windup
        /// (anticipation), trembles while a long strike is held, then releases.
        /// </summary>
        void AttackPose(AttackDef attack, float idle, ref WeaponPose pose)
        {
            float f = AnimFrame(StepFrame);
            float tW = Mathf.Clamp01(f / Mathf.Max(1, stepWindup));
            bool winding = f < stepWindup;
            bool active = !winding && f < stepWindup + attack.active;
            float tA = Mathf.Clamp01((f - stepWindup) / attack.active);
            float tR = Mathf.Clamp01((f - stepWindup - attack.active) / Mathf.Max(1, attack.recovery));
            float tremble = stepWindup >= 30 && tW > 0.66f && winding ? Mathf.Sin(Time.unscaledTime * 70f) * 3f : 0f;

            if (attack.IsProjectile)
            {
                // Draw the bow: pull back through the windup, snap forward on release.
                pose.angle = tremble;
                pose.extend = winding ? Mathf.Lerp(0f, -0.25f, CombatMath.EaseOutCubic(tW)) : Mathf.Lerp(0.15f, 0f, tR);
                if (winding && tW > 0.6f) pose.tint = Color.Lerp(weaponColor, Color.white, (tW - 0.6f) * 2f);
                return;
            }

            switch (attack.shape.type)
            {
                case HitShapeType.Arc:
                {
                    float side = StepIndex % 2 == 0 ? 1f : -1f;
                    float start = (attack.shape.arcDegrees * 0.5f + 35f) * side;
                    float end = -start - side * 15f;
                    if (winding) pose.angle = Mathf.Lerp(idle, start, CombatMath.EaseInOutSine(tW)) + tremble;
                    else if (active) pose.angle = Mathf.Lerp(start, end, CombatMath.EaseOutCubic(tA));
                    else pose.angle = Mathf.Lerp(end, idle, CombatMath.EaseInOutSine(tR));
                    break;
                }
                case HitShapeType.Line:
                {
                    pose.angle = tremble;
                    if (winding) pose.extend = Mathf.Lerp(0f, -0.5f, CombatMath.EaseInOutSine(tW));
                    else if (active) pose.extend = Mathf.LerpUnclamped(-0.5f, 0.8f, CombatMath.EaseOutBack(tA));
                    else pose.extend = Mathf.Lerp(0.8f, 0f, CombatMath.EaseInOutSine(tR));
                    break;
                }
                case HitShapeType.Circle:
                {
                    if (winding) pose.angle = idle - 80f * CombatMath.EaseOutCubic(tW) + tremble;
                    else if (active) pose.angle = idle - 80f + 400f * CombatMath.EaseOutQuad(tA);
                    else pose.angle = idle + 320f + 40f * tR;
                    break;
                }
            }
            if (attack.IsDangerous && (winding || active)) pose.tint = Color.Lerp(weaponColor, pal.danger, 0.55f);
        }

        protected override bool TrailEmitting
        {
            get
            {
                AttackDef a = CurrentAttack;
                if (a == null || a.IsProjectile) return false;
                float f = AnimFrame(StepFrame);
                return f >= stepWindup - 0.5f && f < stepWindup + a.active + 1.5f;
            }
        }

        protected override Color TrailTint
        {
            get
            {
                AttackDef a = CurrentAttack;
                return a != null && a.IsDangerous ? pal.danger : Color.Lerp(weaponColor, Color.white, 0.3f).WithAlpha(0.75f);
            }
        }

        protected override float BodyLean
        {
            get
            {
                AttackDef a = CurrentAttack;
                if (a == null || a.shape.type != HitShapeType.Arc) return 0f;
                float side = StepIndex % 2 == 0 ? 1f : -1f;
                float f = AnimFrame(StepFrame);
                if (f < stepWindup) return side * 10f * CombatMath.EaseInOutSine(f / Mathf.Max(1, stepWindup));
                float tA = Mathf.Clamp01((f - stepWindup) / a.active);
                float tR = Mathf.Clamp01((f - stepWindup - a.active) / Mathf.Max(1, a.recovery));
                return f < stepWindup + a.active ? Mathf.Lerp(10f, -16f, tA) * side : -16f * side * (1f - CombatMath.EaseInOutSine(tR));
            }
        }

        /// <summary>
        /// "Press now" indicator: a ring shrinks onto the attacker at a constant speed and
        /// closes a few frames before impact (see TelegraphTiming). Cyan = parry, red = dodge.
        /// Arrows carry their own cue in flight.
        /// </summary>
        void RenderTimingIndicator(AttackDef attack)
        {
            bool show = world.ShowTimingIndicator && attack != null && !attack.IsProjectile && CurrentState == State.Attacking &&
                        TelegraphTiming.ApproachVisible(StepFrame, stepWindup, feel.timingCueLeadFrames, feel.timingApproachFrames);
            if (!show)
            {
                timingGlow.enabled = timingTarget.enabled = timingApproach.enabled = false;
                return;
            }

            int lead = Mathf.Max(1, feel.timingCueLeadFrames);
            int cue = TelegraphTiming.CueFrame(stepWindup, lead);
            float t = TelegraphTiming.ApproachProgress(StepFrame, stepWindup, lead, feel.timingApproachFrames);
            Color c = attack.IsDangerous ? pal.danger : pal.playerAccent;
            float size = Radius * 2f * 1.55f;

            timingTarget.enabled = true;
            if (StepFrame < cue)
            {
                timingApproach.enabled = true;
                timingGlow.enabled = false;
                float s = size * Mathf.Lerp(3.4f, 1f, t);
                timingApproach.transform.localScale = new Vector3(s, s, 1f);
                timingApproach.color = c.WithAlpha(Mathf.Lerp(0.15f, 0.95f, t));
                timingTarget.transform.localScale = new Vector3(size, size, 1f);
                timingTarget.color = c.WithAlpha(0.35f);
                return;
            }

            // Closed: the moment to answer. Bright flash that decays until the impact.
            float k = Mathf.Clamp01((StepFrame - cue) / (float)lead);
            timingApproach.enabled = false;
            timingGlow.enabled = true;
            float pulse = size * (1.18f - 0.18f * k);
            timingTarget.transform.localScale = new Vector3(pulse, pulse, 1f);
            timingTarget.color = Color.Lerp(Color.white, c, 0.35f + 0.4f * k);
            float g = size * 1.9f;
            timingGlow.transform.localScale = new Vector3(g, g, 1f);
            timingGlow.color = c.WithAlpha(0.55f * (1f - k * 0.5f));
        }

        protected override void RenderExtra(Vector2 renderPosition, float visualDt)
        {
            AttackDef attack = CurrentAttack;
            RenderTimingIndicator(attack);

            float windupT = attack != null ? Mathf.Clamp01(StepFrame / (float)Mathf.Max(1, stepWindup)) : 0f;
            bool active = attack != null && StepFrame >= stepWindup && StepFrame < stepWindup + attack.active;
            bool telegraphing = attack != null && !attack.IsProjectile && StepFrame < stepWindup + attack.active;

            // The art keeps its own colours: states tint it, and a white silhouette on top
            // carries the hit flash, the wind-up heat and the "reading you" glow.
            Color c = Color.white;
            Color glow = Color.clear;
            switch (CurrentState)
            {
                case State.Spawning:
                    c.a = Mathf.Clamp01(StateFrame / (float)et.spawnFrames);
                    glow = Def.color.WithAlpha(0.6f * (1f - c.a));
                    break;
                case State.Attacking:
                    // Anticipation: the body heats toward white as the swing approaches.
                    if (!active && attack != null && StepFrame < stepWindup) glow = Color.white.WithAlpha(windupT * windupT * 0.6f);
                    if (attack != null && attack.IsDangerous && !active && StepFrame < stepWindup) glow = pal.danger.WithAlpha(windupT * windupT * 0.55f);
                    if (attack != null && attack.teleportBehind && StepFrame < TeleportFadeFrames) c.a = 0.2f + 0.8f * StepFrame / TeleportFadeFrames;
                    break;
                case State.Recoil:
                    c = Color.Lerp(Color.white, pal.uiDim, 0.35f);
                    break;
                case State.Stagger:
                    c = Color.Lerp(Color.white, pal.uiDim, 0.5f);
                    break;
                case State.Broken:
                case State.Finishing:
                    c = Color.Lerp(Color.white, pal.gold, 0.25f);
                    glow = pal.gold.WithAlpha(0.18f + 0.14f * Mathf.Sin(Time.unscaledTime * 12f));
                    break;
                case State.Dying:
                    c.a = 1f - Mathf.Clamp01(StateFrame / (float)et.dyingFrames);
                    break;
            }
            if (IsChilled && !IsGone) c = Color.Lerp(c, Elements.Color(Element.Frost), 0.45f);
            if (dotTime > 0f && !IsGone) c = Color.Lerp(c, dotElement == Element.Blood ? Def.bloodColor : Elements.Color(dotElement), 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 14f));
            // An adapted (held) strike glows violet: the enemy is reading you.
            if (attack != null && stepAdapted && StepFrame < stepWindup) glow = pal.adapted.WithAlpha(0.35f + 0.25f * windupT);
            if (Flashing) glow = Color.white;
            if (gibbed) c.a = 0f;
            glow.a *= c.a;
            SetBodyLook(c, glow);
            shadow.enabled = !gibbed;
            weapon.enabled = !gibbed;

            if (eliteAura.enabled)
            {
                float pulse = Radius * 2f * (2.2f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f));
                eliteAura.transform.localScale = new Vector3(pulse, pulse, 1f);
                eliteAura.color = pal.gold.WithAlpha(IsGone ? 0f : 0.22f);
            }

            float rotation = FacingAngle;
            Color tint = attack != null && attack.IsDangerous ? pal.danger : Color.white;
            float alpha = active ? 0.6f : Mathf.Lerp(0.06f, 0.32f, windupT);
            HitShapeType shapeType = attack != null ? attack.shape.type : HitShapeType.Arc;

            areaPreview.enabled = telegraphing && (shapeType == HitShapeType.Arc || shapeType == HitShapeType.Circle);
            ringPreview.enabled = telegraphing && shapeType == HitShapeType.Circle;
            linePreview.enabled = telegraphing && shapeType == HitShapeType.Line;

            if (areaPreview.enabled)
            {
                areaPreview.color = tint.WithAlpha(shapeType == HitShapeType.Circle ? alpha * 0.6f : alpha);
                areaPreview.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                float s = attack.shape.range * 2f;
                areaPreview.transform.localScale = new Vector3(s, s, 1f);
            }
            if (ringPreview.enabled)
            {
                ringPreview.color = tint.WithAlpha(Mathf.Min(1f, alpha * 2f));
                float s = attack.shape.range * 2f;
                ringPreview.transform.localScale = new Vector3(s, s, 1f);
            }
            if (linePreview.enabled)
            {
                float length = attack.shape.range + (active ? 0f : attack.lunge);
                linePreview.color = tint.WithAlpha(alpha);
                linePreview.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                linePreview.transform.localScale = new Vector3(length, attack.shape.width, 1f);
            }

            dangerGlyph.enabled = telegraphing && attack.IsDangerous;
            if (dangerGlyph.enabled)
            {
                float s = 0.42f + 0.08f * Mathf.Sin(Time.unscaledTime * 18f);
                dangerGlyph.transform.localScale = new Vector3(s, s * 1.25f, 1f);
            }

            bool showBars = !IsGone && CurrentState != State.Spawning;
            barBack.enabled = healthFill.enabled = postureFill.enabled = showBars;
            if (showBars)
            {
                float hr = Mathf.Clamp01(Health / MaxHealth);
                healthFill.transform.localScale = new Vector3(BarWidth * hr, 0.07f, 1f);
                float pr = Posture.Ratio;
                postureFill.transform.localScale = new Vector3(BarWidth * pr, 0.06f, 1f);
                postureFill.color = IsBroken ? pal.gold : Color.Lerp(pal.postureLow, pal.postureHigh, pr);
            }

            seal.enabled = Mastered && !IsGone;
            if (seal.enabled) seal.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);

            brokenRing.enabled = IsBroken;
            if (brokenRing.enabled)
            {
                float s = Radius * 2f * (1.5f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f));
                brokenRing.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
