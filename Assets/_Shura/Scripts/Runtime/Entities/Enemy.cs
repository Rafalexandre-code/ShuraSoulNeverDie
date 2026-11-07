using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Data-driven duelist AI. Keeps its preferred range, waits for the attack token, then
    /// commits to a fixed, telegraphed move. Never changes how a move behaves at random.
    /// </summary>
    public sealed class Enemy : Fighter
    {
        public enum State
        {
            Spawning,
            Engage,
            Attacking,
            Stagger,
            Broken,
            Finishing,
            Dying,
            Dead,
        }

        const float BarWidth = 1.1f;

        EnemyTuning et;
        Palette pal;
        SeededRandom rng;

        bool stepHit;
        bool parriedThisMove;
        int idleTimer;
        int staggerFrames;
        int strafeTimer;
        int blockStreak;
        float strafeSign = 1f;

        SpriteRenderer barBack, healthFill, postureFill, dangerGlyph, areaPreview, ringPreview, linePreview, seal, brokenRing;

        public EnemyDef Def { get; private set; }
        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public MoveDef CurrentMove { get; private set; }
        public int StepIndex { get; private set; }
        public int StepFrame { get; private set; }
        /// <summary>Increments every time a new swing starts (lets observers tell swings apart).</summary>
        public int AttackSerial { get; private set; }
        public bool Mastered { get; private set; }

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
                return a != null && StepFrame < a.windup + a.active;
            }
        }

        public bool IsBroken => CurrentState == State.Broken;

        public bool IsGone => CurrentState == State.Dying || CurrentState == State.Dead;

        public void Init(World owner, EnemyDef def, Vector2 spawn, GameConfig config, SeededRandom random)
        {
            Def = def;
            et = config.enemies;
            pal = config.palette;
            rng = random;
            Mass = def.mass;
            MaxHealth = def.maxHealth;
            Health = MaxHealth;
            Posture = new PostureMeter(def.maxPosture);

            BuildView(owner, def.shape, def.color, def.radius);
            Teleport(spawn);
            Vector2 toPlayer = owner.Player.Position - spawn;
            if (toPlayer.sqrMagnitude > 1e-4f) Facing = toPlayer.normalized;

            float barY = def.radius + 0.55f;
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

            dangerGlyph.enabled = areaPreview.enabled = ringPreview.enabled = linePreview.enabled = false;
            seal.enabled = brokenRing.enabled = false;

            strafeTimer = rng.Range(60, 140);
            Enter(State.Spawning);
            owner.Fx.Emit(FxType.Spawn, spawn, Vector2.up, def.radius, def.color);
        }

        public void Tick()
        {
            BeginTick();
            if (CurrentState == State.Dead) return;
            StateFrame++;

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
            Velocity = Vector2.Lerp(Velocity, desired, 0.15f);

            if (!player.IsAlive) return;
            if (--idleTimer <= 0) TryChooseAttack(dist);
        }

        void TryChooseAttack(float dist)
        {
            int total = 0;
            foreach (MoveDef m in Def.moves)
            {
                if (dist >= m.minRange && dist <= m.maxRange) total += m.weight;
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
                if (roll < m.weight)
                {
                    StartMove(m);
                    return;
                }
                roll -= m.weight;
            }
        }

        void StartMove(MoveDef move)
        {
            CurrentMove = move;
            StepIndex = 0;
            parriedThisMove = false;
            Enter(State.Attacking);
            StartStep();
        }

        void StartStep()
        {
            StepFrame = 0;
            stepHit = false;
            AttackSerial++;
            AttackDef attack = CurrentAttack;
            if (attack.IsDangerous)
            {
                world.Fx.Emit(FxType.DangerCue, Position, Facing, 1f, pal.danger);
                world.HintOnce(attack.kind == AttackKind.Thrust ? HintId.DangerThrust : HintId.DangerSweep);
            }
            else
            {
                world.HintOnce(HintId.Parry);
            }
            Squash(0.88f, 1.1f);
            areaPreview.sprite = ShapeLibrary.Wedge(attack.shape.type == HitShapeType.Circle ? 360f : attack.shape.arcDegrees);
        }

        void TickAttack()
        {
            StepFrame++;
            AttackDef attack = CurrentAttack;
            Vector2 to = world.Player.Position - Position;
            if (StepFrame <= attack.trackUntil && to.sqrMagnitude > 1e-4f)
            {
                Facing = CombatMath.RotateTowards(Facing, to.normalized, et.turnDegreesPerSecond * 1.5f * SimClock.Dt);
            }
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.25f);
            RegenPosture();

            if (StepFrame == attack.windup)
            {
                world.Fx.Emit(FxType.EnemySwing, Position, Facing, attack.IsDangerous ? 1.5f : 1f);
                Squash(1.25f, 0.82f);
            }

            bool active = StepFrame >= attack.windup && StepFrame < attack.windup + attack.active;
            if (active)
            {
                if (attack.lunge > 0f) Position += Facing * (attack.lunge / attack.active);
                if (!stepHit && world.TryHitPlayer(this, attack)) stepHit = true;
                // A perfect answer can break posture or stagger us mid-swing.
                if (CurrentState != State.Attacking) return;
            }

            bool lastStep = StepIndex == CurrentMove.steps.Length - 1;
            int recovery = attack.recovery + (lastStep && parriedThisMove ? et.parriedFinalStepExtraRecovery : 0);
            if (StepFrame < attack.windup + attack.active + recovery) return;

            if (!lastStep)
            {
                StepIndex++;
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
            EnterEngage(rng.Range(Def.idleMin, Def.idleMax));
        }

        void RegenPosture()
        {
            // Wounded enemies recover posture slower, rewarding sustained pressure.
            float hpRatio = MaxHealth > 0f ? Health / MaxHealth : 0f;
            Posture.Tick(Def.postureRegenPerSecond * (0.25f + 0.75f * hpRatio), et.postureRegenDelay);
        }

        // ── Reactions ───────────────────────────────────────────────────────────────────

        public void OnParried(float posture)
        {
            Squash(0.82f, 1.15f);
            Velocity = -Facing * 2.2f;
            if (Posture.Add(posture))
            {
                BreakPosture();
                return;
            }
            parriedThisMove = true;
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

        /// <summary>Returns true if the swing connected (hit or blocked).</summary>
        public bool ReceivePlayerHit(float damage, float posture, Vector2 hitDir, bool unblockable)
        {
            if (!CanBeHit) return false;
            Vector2 contact = Position - hitDir * Radius;

            bool facingAttacker = Vector2.Dot(Facing, -hitDir) > 0.26f;
            if (!unblockable && CurrentState == State.Engage && facingAttacker && rng.NextFloat() < Def.blockChance)
            {
                blockStreak++;
                world.Fx.Emit(FxType.EnemyBlocked, contact, -hitDir);
                Squash(0.9f, 1.08f);
                Velocity = hitDir * et.hitKnockback * 0.6f;
                if (Posture.Add(posture * et.blockPostureMultiplier))
                {
                    BreakPosture();
                    return true;
                }
                // Turtling forever is not allowed: after a few blocks it punishes mashing.
                if (blockStreak >= et.riposteAfterBlocks)
                {
                    blockStreak = 0;
                    Riposte();
                }
                return true;
            }

            blockStreak = 0;
            Health -= damage;
            Flash(0.07f);
            Squash(0.8f, 1.2f);
            if (CurrentState != State.Attacking && CurrentState != State.Broken) Velocity = hitDir * et.hitKnockback;
            world.Fx.Emit(FxType.HitEnemy, contact, hitDir, 1f, Def.color);

            if (Health <= 0f)
            {
                Die();
                return true;
            }
            if (CurrentState != State.Broken && Posture.Add(posture)) BreakPosture();
            return true;
        }

        void Riposte()
        {
            float dist = Vector2.Distance(world.Player.Position, Position);
            MoveDef fastest = null;
            foreach (MoveDef m in Def.moves)
            {
                if (m.steps[0].IsDangerous || dist < m.minRange || dist > m.maxRange) continue;
                if (fastest == null || m.steps[0].windup < fastest.steps[0].windup) fastest = m;
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
            world.Fx.Emit(FxType.EnemyDeath, Position, Facing, Radius, Def.color);
        }

        // ── View ────────────────────────────────────────────────────────────────────────

        protected override void RenderExtra(Vector2 renderPosition, float visualDt)
        {
            AttackDef attack = CurrentAttack;
            float windupT = attack != null ? Mathf.Clamp01(StepFrame / (float)Mathf.Max(1, attack.windup)) : 0f;
            bool active = attack != null && StepFrame >= attack.windup && StepFrame < attack.windup + attack.active;
            bool telegraphing = attack != null && StepFrame < attack.windup + attack.active;

            Color c = Def.color;
            switch (CurrentState)
            {
                case State.Spawning:
                    c.a = Mathf.Clamp01(StateFrame / (float)et.spawnFrames);
                    break;
                case State.Attacking:
                    // Anticipation: the body heats toward white as the swing approaches.
                    if (!active && attack != null && StepFrame < attack.windup) c = Color.Lerp(Def.color, Color.white, windupT * windupT * 0.55f);
                    break;
                case State.Stagger:
                    c = Color.Lerp(Def.color, pal.uiDim, 0.5f);
                    break;
                case State.Broken:
                case State.Finishing:
                    c = Color.Lerp(Def.color * 0.6f, pal.gold, 0.3f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f));
                    c.a = 1f;
                    break;
                case State.Dying:
                    c.a = 1f - Mathf.Clamp01(StateFrame / (float)et.dyingFrames);
                    break;
            }
            if (Flashing) c = Color.white.WithAlpha(c.a);
            body.color = c;
            bool glint = attack != null && !active && windupT > 0.7f;
            pointer.color = glint ? Color.white.WithAlpha(c.a) : (Def.color * 0.85f).WithAlpha(c.a);

            float rotation = CombatMath.AngleDegrees(Facing);
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
