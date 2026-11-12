using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Data-driven duelist AI. Keeps its preferred range, waits for the attack token, then
    /// commits to a fixed, telegraphed move. A perfect parry stops the swing (the blade
    /// bounces and the enemy is open); when it deflects the player's swing, the player's
    /// blade bounces instead. Never changes how a move behaves at random.
    /// </summary>
    public sealed partial class Enemy : Fighter
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
            /// <summary>Bones on the floor, rising again (Gashadokuro).</summary>
            Reassembling,
        }

        const float BarWidth = 1.1f;
        const int TeleportFadeFrames = 10;
        const int ReassembleFrames = 240;
        const int ExposedFrames = 180;
        const int HealPeriodFrames = 120;
        const int CrashStunFrames = 150;
        const int StanceCounterWindup = 12;
        const int BlinkCooldownFrames = 240;

        // Bestiary state.
        bool reassembled;
        bool enraged;
        int exposedFrames;
        int healTimer;
        int blinkCooldown;
        int closeFrames;
        int dustTimer;

        /// <summary>Whoever called this enemy (shadow clones vanish with them).</summary>
        public Enemy Summoner { get; private set; }
        public bool IsEnraged => enraged;
        /// <summary>Ethereal and not exposed: blades pass through.</summary>
        public bool IsIntangible => (Def.ethereal || world.AllEthereal) && !IsAlly && exposedFrames <= 0 && !IsBroken;
        /// <summary>Under the floor during a burrow windup.</summary>
        public bool IsBurrowed
        {
            get
            {
                AttackDef a = CurrentAttack;
                return a != null && a.burrow && StepFrame < stepWindup - 2;
            }
        }

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
        /// <summary>The moves this enemy uses (its def's, plus extras such as a Guardian's group blow).</summary>
        MoveDef[] moves;
        float sealBarY;

        /// <summary>White broken ring: a huge blow only two duelists parrying together can stop.</summary>
        static readonly MoveDef GroupBlow = new MoveDef
        {
            id = "golpe_do_grupo", minRange = 0f, maxRange = 3.4f, weight = 1,
            steps = new[]
            {
                new AttackDef
                {
                    id = "golpe_do_grupo", windup = 56, active = 6, recovery = 38, trackUntil = 40,
                    damage = 30f, postureDamage = 30f, postureOnParried = 70f, shape = HitShape.Circle(3.2f), groupStrike = true,
                },
            },
        };
        public State CurrentState { get; private set; }
        public int StateFrame { get; private set; }
        public MoveDef CurrentMove { get; private set; }
        public int StepIndex { get; private set; }
        public int StepFrame { get; private set; }
        /// <summary>Increments every time a new swing starts (lets observers tell swings apart).</summary>
        public int AttackSerial { get; private set; }

        static int silentSerial;
        /// <summary>Each enemy's own salt for the silent-ring roll.</summary>
        readonly int silentSalt = ++silentSerial;

        /// <summary>Oath "Anel Silencioso": this blow (and only this one) shows no ring.</summary>
        bool SilentFor(PlayerController viewer)
        {
            float chance = viewer.SilentRingChance;
            if (chance <= 0f) return false;
            uint h = (uint)(silentSalt * 73856093) ^ (uint)(AttackSerial * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h % 1000u) < (uint)(chance * 1000f);
        }
        public bool Mastered { get; private set; }
        public bool IsElite => scaling.elite;
        public bool IsChilled => chillTime > 0f;
        public Sprite BodySprite => body.sprite;
        public Color BodyColor => Def.color;
        /// <summary>Damage multiplier from depth and Guardian status.</summary>
        public float PowerMult => scaling.damage * KalpaPowerMult;
        /// <summary>Frames until the current swing becomes active (adaptive holds included).</summary>
        public int FramesUntilActive => CurrentAttack != null ? stepWindup - StepFrame : int.MaxValue;

        public AttackDef CurrentAttack =>
            CurrentState == State.Attacking && CurrentMove != null ? CurrentMove.steps[StepIndex] : null;

        public bool CanBeHit =>
            CurrentState != State.Spawning && CurrentState != State.Finishing &&
            CurrentState != State.Dying && CurrentState != State.Dead && !IsBurrowed;

        public bool IsThreatening
        {
            get
            {
                AttackDef a = CurrentAttack;
                return a != null && !a.IsBlast && !a.IsSummon && StepFrame < stepWindup + a.active;
            }
        }

        public bool IsBroken => CurrentState == State.Broken;

        public bool IsGone => CurrentState == State.Dying || CurrentState == State.Dead;

        public void Init(World owner, EnemyDef def, Vector2 spawn, GameConfig config, SeededRandom random, EnemyScaling enemyScaling, Enemy summoner = null)
        {
            Def = def;
            Summoner = summoner;
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

            moves = def.moves;
            if (scaling.elite && owner.PartySize >= 2)
            {
                moves = new MoveDef[def.moves.Length + 1];
                def.moves.CopyTo(moves, 0);
                moves[def.moves.Length] = GroupBlow;
            }
            float barY = VisualTop + 0.25f;
            sealBarY = barY;
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
            healTimer = rng.Range(0, HealPeriodFrames);
            Enter(State.Spawning);
            owner.Fx.Emit(FxType.Spawn, spawn, Vector2.up, Radius, scaling.elite ? pal.gold : def.color);
            if (def.ethereal) owner.HintOnce(HintId.Ethereal);
            if (def.healRadius > 0f) owner.HintOnce(HintId.Healer);
            if (scaling.elite) owner.Fx.Popup(Strings.PopupGuardian + "  ·  " + def.displayName, spawn + Vector2.up * 1.4f, pal.gold);
        }

        public void Tick()
        {
            BeginTick();
            if (CurrentState == State.Dead) return;
            StateFrame++;
            if (guardFrames > 0) guardFrames--;
            if (exposedFrames > 0) exposedFrames--;
            TickStatus();
            TickElement();
            if (CurrentState == State.Dead) return;
            TickKi();
            if (CurrentState == State.Dead) return;
            TickRoad();
            TickTraits();
            if (TickYield()) return;

            switch (CurrentState)
            {
                case State.Spawning:
                    // Held in place, fully drawn, while the standoff lasts.
                    if (HeldInSpawn) StateFrame = Mathf.Min(StateFrame, et.spawnFrames - 1);
                    else if (StateFrame >= et.spawnFrames) EnterEngage(rng.Range(20, 50));
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
                    // Safety net: no finisher lasts this long — whoever held it let go.
                    if (StateFrame > 300) ReleaseFromFinisher();
                    break;
                case State.Dying:
                    Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.2f);
                    if (StateFrame >= et.dyingFrames) Enter(State.Dead);
                    break;
                case State.Reassembling:
                    Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.25f);
                    if (StateFrame % 20 == 0) world.Fx.Emit(FxType.ShadowStep, Position, Vector2.up, Radius * 0.6f, Def.color);
                    if (StateFrame >= ReassembleFrames)
                    {
                        Health = MaxHealth * 0.5f;
                        Posture.Reset(0f);
                        world.Fx.Emit(FxType.Spawn, Position, Vector2.up, Radius, Def.color);
                        world.Fx.Popup(Strings.PopupReassembled, Position + Vector2.up * 1.2f, Def.color);
                        EnterEngage(20);
                    }
                    break;
            }
            Integrate();
        }

        /// <summary>Passive traits that run whatever the enemy is doing: healing aura, blinking away.</summary>
        void TickTraits()
        {
            if (CurrentState == State.Spawning || CurrentState == State.Dying || CurrentState == State.Reassembling) return;

            if (Def.healRadius > 0f && ++healTimer >= HealPeriodFrames)
            {
                healTimer = 0;
                bool any = false;
                foreach (Enemy e in world.Enemies)
                {
                    if (e == this || e.IsGone || e.CurrentState == State.Reassembling) continue;
                    if (Vector2.Distance(e.Position, Position) > Def.healRadius) continue;
                    if (e.Health >= e.MaxHealth && e.Posture.Ratio <= 0f) continue;
                    e.Mend(e.MaxHealth * Def.healFraction, 15f);
                    world.Fx.Emit(FxType.Heal, e.Position, Vector2.up, e.Radius, Def.color);
                    any = true;
                }
                if (any) world.Fx.Emit(FxType.HealPulse, Position, Vector2.up, Def.healRadius, Def.color);
            }

            if (Def.blinkRange > 0f && CurrentState == State.Engage)
            {
                if (blinkCooldown > 0) blinkCooldown--;
                PlayerController near = world.NearestAlivePlayer(Position);
                bool close = near != null && Vector2.Distance(near.Position, Position) < Def.blinkRange;
                closeFrames = close ? closeFrames + 1 : 0;
                if (closeFrames >= 30 && blinkCooldown == 0)
                {
                    closeFrames = 0;
                    blinkCooldown = BlinkCooldownFrames;
                    Vector2 to = world.FarFloorPoint(Position, Radius);
                    world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius, Def.color);
                    Teleport(to);
                    world.Fx.Emit(FxType.Smoke, to, Vector2.up, Radius, Def.color);
                    idleTimer = Mathf.Max(idleTimer, 25);
                }
            }
        }

        /// <summary>A sturdier (or frailer) foe than its kind: health scaled, kept full.</summary>
        public void ScaleHealth(float k)
        {
            if (k <= 0f || Mathf.Approximately(k, 1f)) return;
            MaxHealth *= k;
            Health = MaxHealth;
        }

        /// <summary>The blow this enemy will throw next (rolled ahead, so the Dragon Eye can read it).</summary>
        public MoveDef NextMove { get; private set; }

        /// <summary>Healed by an ally's aura: health and some posture back.</summary>
        public void Mend(float health, float posture)
        {
            if (IsGone) return;
            Health = Mathf.Min(MaxHealth, Health + health);
            Posture.Recover(posture);
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
            RollNextMove();
        }

        /// <summary>Picks the next blow by weight, ignoring range (range decides later whether it comes).</summary>
        void RollNextMove()
        {
            NextMove = null;
            if (moves == null || moves.Length == 0) return;
            int total = 0;
            foreach (MoveDef m in moves) total += MoveWeight(m);
            if (total <= 0) return;
            int roll = rng.Range(0, total);
            foreach (MoveDef m in moves)
            {
                int w = MoveWeight(m);
                if (roll < w)
                {
                    NextMove = m;
                    return;
                }
                roll -= w;
            }
        }

        /// <summary>The duelist this enemy is fighting (re-chosen now and then, and when they fall).</summary>
        public PlayerController Target { get; set; }
        static int nextUid;
        /// <summary>Stable id for network snapshots.</summary>
        public int NetUid { get; } = ++nextUid;
        int retargetTimer;

        void TickEngage()
        {
            if (TickConfused()) return;
            if (IsAlly && TickAllyIdle()) return;
            if (Target == null || !Target.IsAlive || --retargetTimer <= 0)
            {
                Target = world.ChooseTarget(this);
                retargetTimer = 90 + (NetUid & 63);
            }
            PlayerController player = Target;
            Vector2 to = FoePosition - Position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.001f ? to / dist : Facing;
            // The FLANK reading: they turn faster to face whoever strikes their backs.
            Facing = CombatMath.RotateTowards(Facing, dir, et.turnDegreesPerSecond * (1f + 1.5f * world.Adaptation.Flank) * SimClock.Dt);
            RegenPosture();

            // Enemies waiting for their turn hang back so the active duels stay readable.
            bool waiting = world.Coordinator.IsFull && !world.Coordinator.IsHeldBy(this);
            float range = Def.preferredRange + (waiting ? et.waitingRangeBonus : 0f);

            Vector2 desired;
            bool brawling = InfightTarget != null;
            if (!player.IsAlive && !brawling) desired = Vector2.zero;
            else if (dist > range + 0.6f) desired = world.Layout.Steer(Position, FoePosition, Radius) * Def.moveSpeed;
            else if (dist < range - 0.6f) desired = -dir * Def.moveSpeed * 0.7f;
            else desired = CombatMath.Perpendicular(dir) * strafeSign * Def.moveSpeed * 0.45f;

            if (--strafeTimer <= 0)
            {
                strafeTimer = rng.Range(60, 140);
                if (rng.NextFloat() < 0.5f) strafeSign = -strafeSign;
            }
            if (IsChilled) desired *= 0.55f;
            if (enraged) desired *= 1.35f;
            desired *= world.MoveMultWorld;
            Velocity = Vector2.Lerp(Velocity, desired, 0.15f);

            if (!player.IsAlive && !brawling) return;
            // A training partner strikes only when the lesson asks.
            if (Training) TickTraining(dist);
            else if (--idleTimer <= 0) TryChooseAttack(dist);
        }

        int Idle(int min, int max)
        {
            float mult = scaling.idle * (1f - 0.45f * world.Adaptation.Pressure - 0.4f * world.HealPressure(Target)) * (IsChilled ? 1.4f : 1f) * (enraged ? 0.5f : 1f);
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
            if (enraged && m.isSignature) bias *= 3f;
            return Mathf.Max(1, Mathf.RoundToInt(m.weight * bias * 2f));
        }

        /// <summary>Summon moves are skipped while enough minions already stand.</summary>
        bool Usable(MoveDef m, float dist)
        {
            if (dist < m.minRange || dist > m.maxRange) return false;
            if (m.steps[0].IsSummon && (string.IsNullOrEmpty(Def.summonId) || world.MinionsOf(this) >= Def.summonMax)) return false;
            if (Winded > 0 && HasDangerousStep(m)) return false;
            if (!LegsAllow(m)) return false;
            if (m.steps[0].groupStrike && world.PartySize < 2) return false;
            return true;
        }

        static bool HasDangerousStep(MoveDef m)
        {
            foreach (AttackDef a in m.steps)
            {
                if (a.IsDangerous) return true;
            }
            return false;
        }

        void TryChooseAttack(float dist)
        {
            int total = 0;
            foreach (MoveDef m in moves)
            {
                if (Usable(m, dist)) total += MoveWeight(m);
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

            // The blow read in advance comes, if it still can.
            if (NextMove != null && Usable(NextMove, dist))
            {
                MoveDef chosen = NextMove;
                NextMove = null;
                StartMove(chosen);
                return;
            }
            int roll = rng.Range(0, total);
            foreach (MoveDef m in moves)
            {
                if (!Usable(m, dist)) continue;
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
            OnMoveStartedCursed();
            CurrentMove = move;
            StepIndex = 0;
            Enter(State.Attacking);
            StartStep();
        }

        bool brawlHit;

        void StartStep()
        {
            StepFrame = 0;
            stepHit = false;
            brawlHit = false;
            AttackSerial++;
            AttackDef attack = CurrentAttack;
            AdaptStep(attack);

            if (attack.teleportBehind)
            {
                world.TeleportBehindPlayer(this);
                world.HintOnce(HintId.Shadow);
            }
            if (attack.burrow)
            {
                world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius, Def.color);
                world.HintOnce(HintId.Burrow);
            }
            if (attack.stance) world.HintOnce(HintId.Stance);
            if (attack.IsBlast) world.HintOnce(HintId.Blast);
            if (attack.wallStun) world.HintOnce(HintId.Charge);

            if (attack.IsDangerous)
            {
                world.Fx.Emit(FxType.DangerCue, Position, Facing, 1f, pal.danger);
                world.HintOnce(attack.kind == AttackKind.Thrust ? HintId.DangerThrust
                             : attack.kind == AttackKind.Sweep ? HintId.DangerSweep
                             : HintId.DangerGrab);
            }
            else if (attack.IsProjectile)
            {
                world.HintOnce(HintId.Projectile);
            }
            else if (attack.kind == AttackKind.Spirit)
            {
                world.HintOnce(HintId.SpiritBlow);
            }
            else if (attack.ShowsTimingRing)
            {
                world.HintOnce(HintId.Parry);
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
            // A frenzy speeds up every swing; the ring still closes on the real impact.
            if (enraged && !attack.stance && !attack.burrow) stepWindup = Mathf.Max(8, Mathf.RoundToInt(stepWindup * 0.72f));
            if (ArmWound) stepWindup = Mathf.RoundToInt(stepWindup * 1.2f);
            stepWindup = KalpaWindup(stepWindup);
            bool hiddenBlade = Target != null && Target.Stance == Kamae.Waki;
            // The SWAP reading breaks the rhythm the same way the early parry does.
            float hold = Mathf.Max(traits.Delay, traits.Swap * 0.8f);
            if (!hiddenBlade && !attack.NoMeleeHit && !attack.burrow && !attack.teleportBehind && rng.NextFloat() < hold * 0.6f * world.AdaptationSpeed)
            {
                stepWindup += rng.Range(8, 19);
                stepAdapted = true;
            }
            int trackBonus = Mathf.RoundToInt(6f * traits.Track);
            stepTrack = Mathf.Min(attack.trackUntil + trackBonus, Mathf.Max(attack.trackUntil, stepWindup - 6));
            // Always commit a few frames before impact, even when a frenzy shortened the windup.
            stepTrack = Mathf.Min(stepTrack, Mathf.Max(0, stepWindup - 4));

            // Online lag compensation: a browser player sees this swing late by their ping, so
            // the blow lands that much later while the ring keeps closing when they see it.
            stepLag = !attack.NoMeleeHit && Target != null && Target != world.Host ? Target.LatencyTicks : 0;
            stepWindup += stepLag;
        }

        int stepLag;

        /// <summary>The windup as the target perceives it (the timing ring closes on this).</summary>
        int CueWindup => stepWindup - stepLag;

        void TickAttack()
        {
            StepFrame++;
            AttackDef attack = CurrentAttack;
            // Arrows cue on their own flight; melee cues when the ring closes. Feints, stances
            // and ranged releases never ring: the ring only ever means a real blow.
            if (attack.ShowsTimingRing && StepFrame == TelegraphTiming.CueFrame(CueWindup, feel.timingCueLeadFrames))
            {
                world.Fx.Emit(FxType.TimingCue, Position, Facing, attack.IsDangerous ? 1f : 0f);
            }

            PlayerController victim = Target != null ? Target : world.Host;
            Vector2 to = FoePosition - Position;
            if (StepFrame <= stepTrack && to.sqrMagnitude > 1e-4f)
            {
                Facing = CombatMath.RotateTowards(Facing, to.normalized, et.turnDegreesPerSecond * 1.5f * (1f + world.Adaptation.Flank) * SimClock.Dt);
            }
            Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.25f);
            RegenPosture();

            if (attack.burrow && StepFrame < stepWindup)
            {
                // Crawling under the floor: the danger circle follows the target until it commits.
                if (StepFrame <= stepTrack && to.magnitude > 0.2f)
                {
                    Velocity = world.Layout.Steer(Position, victim.Position, Radius) * Def.moveSpeed * 1.5f;
                }
                if (++dustTimer % 6 == 0) world.Fx.Emit(FxType.Footstep, Position, Velocity.normalized);
                if (StepFrame == stepWindup - 2) world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius * 1.4f, Def.color);
            }

            if (StepFrame == stepWindup)
            {
                if (!attack.feint && !attack.stance && !attack.IsSummon)
                {
                    world.Fx.Emit(FxType.EnemySwing, Position, Facing, attack.IsDangerous ? 1.5f : attack.IsProjectile ? 2f : 1f);
                }
                Squash(attack.feint ? 1.08f : 1.25f, attack.feint ? 0.94f : 0.82f);
                if (attack.feint) world.PunishFeintParry(this, attack);
                if (attack.IsProjectile) world.SpawnProjectile(this, attack, CurrentMove.isSignature);
                if (attack.IsBlast) ReleaseBlasts(attack, victim);
                if (attack.IsSummon) world.Summon(this, Def.summonId, attack.summonCount);
            }

            bool active = StepFrame >= stepWindup && StepFrame < stepWindup + attack.active;
            if (active && !attack.NoMeleeHit)
            {
                if (attack.lunge > 0f && !Lunge(attack)) return;
                if (!stepHit && world.TryHitPlayer(this, attack)) stepHit = true;
                if (!brawlHit && InfightTarget != null)
                {
                    brawlHit = true;
                    HitBrawlTarget(attack);
                }
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

        /// <summary>
        /// Moves along the swing. A charge that meets a wall or statue ends in a crash: the
        /// brute is stunned and open. Returns false when the swing ended here.
        /// </summary>
        bool Lunge(AttackDef attack)
        {
            Vector2 step = Facing * (attack.lunge / attack.active);
            Vector2 next = Position + step;
            if (attack.wallStun && !world.Layout.Clear(Position, next + Facing * Radius * 0.5f, Radius * 0.8f))
            {
                Crash();
                return false;
            }
            Position = next;
            return true;
        }

        void Crash()
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            Velocity = -Facing * 2f;
            world.Fx.Emit(FxType.SlamImpact, Position + Facing * Radius, -Facing, 2.2f);
            world.Fx.Popup(Strings.PopupStunned, Position + Vector2.up * 1.6f, pal.gold);
            Flash(0.1f);
            if (Posture.Add(Posture.Max * 0.35f))
            {
                BreakPosture();
                return;
            }
            staggerFrames = CrashStunFrames;
            Enter(State.Stagger);
        }

        /// <summary>Bombs and talismans: circles on the floor at the target (and around it) that explode later.</summary>
        void ReleaseBlasts(AttackDef attack, PlayerController victim)
        {
            Vector2 center = victim.Position;
            for (int i = 0; i < attack.blastCount; i++)
            {
                Vector2 at = center;
                if (i > 0)
                {
                    float a = (i - 1) * (360f / Mathf.Max(1, attack.blastCount - 1)) + rng.Range(-25f, 25f);
                    at += CombatMath.FromAngle(a) * attack.blastScatter;
                }
                at = world.Layout.NearestFloor(at, 0.1f);
                world.ScheduleEnemyBlast(this, at, attack.blastRadius, attack.blastFuse + i * 6, attack.damage * PowerMult,
                                         attack.postureDamage * PowerMult, attack.blastKind, false, CurrentMove != null && CurrentMove.isSignature);
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
            EnterEngage(NextIdle(Idle(Def.idleMin, Def.idleMax)));
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
            LoseKalpaFlow();
            Expose();
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

        /// <summary>A perfect answer pins an ethereal enemy to the world for a few seconds.</summary>
        void Expose()
        {
            if (!Def.ethereal) return;
            if (exposedFrames <= 0) world.Fx.Popup(Strings.PopupExposed, Position + Vector2.up * 1.2f, pal.gold);
            exposedFrames = ExposedFrames;
        }

        public void OnCounterThrust(float posture)
        {
            Expose();
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
            Thaw();
            if (!CanBeHit) return StrikeResult.None;
            Vector2 contact = Position - hitDir * Radius;
            // A blow on someone kneeling is an execution.
            if (Kneeling)
            {
                world.OnExecution(this);
                world.Fx.Emit(FxType.HitEnemy, contact, hitDir, damage, Def.bloodColor);
                Kneeling = false;
                Health = 0f;
                FinishDying();
                return StrikeResult.Hit;
            }

            // Bones on the floor: one blow scatters them for good.
            if (CurrentState == State.Reassembling)
            {
                world.Fx.Emit(FxType.HitEnemy, contact, hitDir, damage, Def.bloodColor);
                Health = 0f;
                FinishDying();
                return StrikeResult.Hit;
            }

            // Ethereal: the blade passes through like smoke.
            if (IsIntangible)
            {
                world.Fx.Emit(FxType.Phase, contact, hitDir, Radius, Def.color);
                world.HintOnce(HintId.Ethereal);
                return StrikeResult.None;
            }

            // Iaijutsu: striking the stance is turned aside and answered with the draw at once.
            AttackDef current = CurrentAttack;
            if (current != null && current.stance && StepFrame < stepWindup && !unblockable && StepIndex < CurrentMove.steps.Length - 1)
            {
                Deflect(0f, hitDir, contact, false);
                StepIndex++;
                StartStep();
                stepWindup = Mathf.Min(stepWindup, StanceCounterWindup + stepLag);
                stepTrack = Mathf.Min(stepTrack, Mathf.Max(0, stepWindup - 4));
                world.Fx.Popup(Strings.PopupStanceCounter, Position + Vector2.up * 1.3f, pal.danger);
                return StrikeResult.Deflected;
            }

            bool facingAttacker = Vector2.Dot(Facing, -hitDir) > 0.26f;
            if (!unblockable && CurrentState == State.Engage && Def.evadeChance > 0f && rng.NextFloat() < Def.evadeChance)
            {
                // A backflip out of reach instead of a parry.
                Vector2 away = (hitDir + CombatMath.Perpendicular(hitDir) * (rng.NextFloat() < 0.5f ? 0.7f : -0.7f)).normalized;
                Velocity = away * 9f;
                Squash(1.2f, 0.85f);
                world.Fx.Emit(FxType.Smoke, Position, away, Radius, Def.color);
                return StrikeResult.None;
            }
            float blockChance = Mathf.Min(0.95f, Def.blockChance + 0.25f * world.Adaptation.Guard);
            if (world.Player.Stance == Kamae.Jodan) blockChance *= 0.5f;
            if (!unblockable && CurrentState == State.Engage && facingAttacker && rng.NextFloat() < blockChance)
            {
                Deflect(posture, hitDir, contact);
                return StrikeResult.Deflected;
            }

            blockStreak = 0;
            // From behind: +50% posture.
            if (Vector2.Dot(Facing, hitDir) > 0.5f) posture *= 1.5f;
            RegisterHitRegion(hitDir);
            if (TwinOf != null && !TwinOf.IsGone)
            {
                TwinOf.TakeTwinDamage(damage, hitDir);
                damage = 0f;
            }
            if (HasAffix(EliteAffix.Mirrored) && damage > 0f && !unblockable) world.ReflectToStriker(this, damage * 0.2f);
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
            CheckEnrage();
            if (CurrentState != State.Broken && Posture.Add(posture)) BreakPosture();
            return StrikeResult.Hit;
        }

        void CheckEnrage()
        {
            if (enraged || Def.enrageBelow <= 0f || Health > MaxHealth * Def.enrageBelow) return;
            enraged = true;
            world.Fx.Emit(FxType.Enrage, Position, Vector2.up, Radius, pal.danger);
            world.Fx.Popup(Strings.PopupEnraged, Position + Vector2.up * 1.5f, pal.danger);
            world.HintOnce(HintId.Enrage);
        }

        /// <summary>Lifesteal: the blow that landed feeds it.</summary>
        public void OnDealtDamage(float damage)
        {
            if (Def.lifesteal <= 0f || IsGone || damage <= 0f) return;
            float heal = Mathf.Min(MaxHealth - Health, damage * Def.lifesteal);
            if (heal <= 0.5f) return;
            Health += heal;
            world.Fx.Emit(FxType.Drain, Position, Vector2.up, heal, Def.bloodColor);
            world.HintOnce(HintId.Lifesteal);
        }

        /// <summary>Traps: cannot be guarded, never counted as the player's blow.</summary>
        public void ReceiveHazard(float damage, float posture, Vector2 dir)
        {
            ReceivePlayerHit(damage, posture, dir, true);
        }

        // ── Status effects (from equipment) ─────────────────────────────────────────────

        public void ApplyDot(float damagePerSecond, float seconds, Element element, bool mark = true)
        {
            if (!CanBeHit) return;
            if (mark) MarkElement(element);
            // Embers make fire last longer; rain puts it out sooner.
            if (element == Element.Fire) seconds *= world.FireDotMult;
            dotDps = Mathf.Max(dotDps, damagePerSecond);
            dotTime = Mathf.Max(dotTime, seconds);
            dotElement = element;
        }

        public void ApplyChill(float seconds, bool mark = true)
        {
            if (!CanBeHit) return;
            if (mark) MarkElement(Element.Frost);
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
        void Deflect(float posture, Vector2 hitDir, Vector2 contact, bool canRiposte = true)
        {
            if (!canRiposte)
            {
                Facing = -hitDir;
                Squash(0.9f, 1.08f);
                world.Fx.Emit(FxType.EnemyBlocked, contact, -hitDir);
                world.Player.OnDeflected();
                return;
            }
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
            // The riposte answers whoever struck the guard.
            Target = world.Player;
            MoveDef fastest = null;
            foreach (MoveDef m in moves)
            {
                AttackDef first = m.steps[0];
                if (first.IsDangerous || first.NoMeleeHit || first.burrow || first.teleportBehind || dist < m.minRange || dist > m.maxRange) continue;
                if (fastest == null || first.windup < fastest.steps[0].windup) fastest = m;
            }
            if (fastest != null && world.Coordinator.ForceAcquire(this)) StartMove(fastest);
        }

        void BreakPosture()
        {
            Expose();
            world.Coordinator.Release(this);
            CurrentMove = null;
            Velocity = Vector2.zero;
            Enter(State.Broken);
            world.Fx.Emit(FxType.PostureBreak, Position, Facing, Radius, pal.gold);
            world.HintOnce(HintId.Finisher);
        }

        /// <summary>The finisher was abandoned: back on its feet with half its posture.</summary>
        public void ReleaseFromFinisher()
        {
            if (CurrentState != State.Finishing) return;
            Posture.Reset(Posture.Max * 0.5f);
            EnterEngage(30);
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
            if (Def.reassembles && !reassembled && !gibbed && CurrentState != State.Reassembling)
            {
                // The bones fall apart... and start crawling back together.
                reassembled = true;
                world.Coordinator.Release(this);
                CurrentMove = null;
                Health = 1f;
                Velocity = Vector2.zero;
                Enter(State.Reassembling);
                world.Fx.Emit(FxType.EnemyDeath, Position, Facing, Radius, Def.color);
                world.HintOnce(HintId.Reassemble);
                return;
            }
            FinishDying();
        }

        /// <summary>Vanishing without a body or loot (shadow clones when their master falls).</summary>
        public void Dispel()
        {
            if (IsGone) return;
            world.Coordinator.Release(this);
            CurrentMove = null;
            Health = 0f;
            gibbed = true;
            Enter(State.Dying);
            world.Fx.Emit(FxType.Smoke, Position, Vector2.up, Radius, Def.color);
        }

        void FinishDying()
        {
            world.Coordinator.Release(this);
            CurrentMove = null;
            Health = 0f;
            Enter(State.Dying);
            if (scaling.elite && world.Run != null) world.Run.ElitesKilled++;
            // Minions never carry loot (or a summoner could be farmed).
            if (!Def.minion) world.OnEnemyDied(this);
            world.Fx.Emit(FxType.EnemyDeath, Position, Facing, Radius, Def.color);

            world.NotifyKill(this);
            if (Def.clones) world.DispelMinionsOf(this);
            if (AsuraOwner != null) AsuraOwner.OnInnerAsuraDefeated();
            OnDeathAffixes();
            // A finisher ends it for good: no split.
            if (!gibbed && !string.IsNullOrEmpty(Def.splitInto) && Def.splitCount > 0)
            {
                world.Split(this, Def.splitInto, Def.splitCount);
            }
            if (Def.deathBlastRadius > 0f)
            {
                world.ScheduleEnemyBlast(this, Position, Def.deathBlastRadius, Def.deathBlastFuse, Def.deathBlastDamage * PowerMult,
                                         Def.deathBlastDamage * PowerMult, BlastKind.Fire, true);
                world.HintOnce(HintId.DeathBlast);
            }
        }

        // ── View ────────────────────────────────────────────────────────────────────────

        protected override bool HurtPose => CurrentState == State.Recoil || CurrentState == State.Stagger || Flashing;

        protected override bool FallenPose => CurrentState == State.Reassembling || base.FallenPose;

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

            if (attack.stance)
            {
                // Hand on the hilt, blade sheathed low; it trembles harder as the draw nears.
                pose.angle = -125f + Mathf.Sin(Time.unscaledTime * 40f) * 4f * tW;
                pose.extend = -0.25f;
                pose.tint = Color.Lerp(weaponColor, Color.white, tW * 0.6f);
                return;
            }
            if (attack.IsSummon || attack.IsBlast)
            {
                // Raised toward the sky while the spell gathers.
                pose.angle = winding ? Mathf.Lerp(idle, 95f, CombatMath.EaseOutCubic(tW)) + tremble : Mathf.Lerp(95f, idle, tR);
                return;
            }
            if (attack.feint)
            {
                // Pulls back as for a real blow, then lets it drop: nothing comes.
                float startF = (attack.shape.arcDegrees * 0.5f + 35f) * (StepIndex % 2 == 0 ? 1f : -1f);
                pose.angle = winding ? Mathf.Lerp(idle, startF, CombatMath.EaseInOutSine(tW)) : Mathf.Lerp(startF, idle, CombatMath.EaseOutCubic(Mathf.Clamp01((f - stepWindup) / 6f)));
                return;
            }
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
                if (a == null || a.NoMeleeHit || Def.unarmed) return false;
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
            bool ring = attack != null && (attack.ShowsTimingRing || attack.feint) && CurrentState == State.Attacking &&
                        TelegraphTiming.ApproachVisible(StepFrame, CueWindup, feel.timingCueLeadFrames, ApproachFrames);
            NetRing = 0;
            NetRingKind = (int)CurrentRingKind;
            if (!ring)
            {
                timingGlow.enabled = timingTarget.enabled = timingApproach.enabled = false;
                if (timingInner != null) timingInner.enabled = false;
                return;
            }

            RingKind kind = CurrentRingKind;
            int lead = Mathf.Max(1, feel.timingCueLeadFrames);
            int cue = TelegraphTiming.CueFrame(CueWindup, lead);
            float t = TelegraphTiming.ApproachProgress(StepFrame, CueWindup, lead, ApproachFrames);
            float size = Radius * 2f * 1.55f;
            NetRingSize = size;
            NetRing = StepFrame < cue ? 1 : 2;
            NetRingProgress = StepFrame < cue ? t : Mathf.Clamp01((StepFrame - cue) / (float)lead);

            // What this computer's duelist sees: nothing in Mushin, Frenzy, or with wounded eyes.
            PlayerController viewer = world.Host;
            bool veiled = Veiled && Vector2.Distance(viewer.Position, Position) > 3f;
            if (!world.ShowTimingIndicator || viewer.RingsHidden || veiled || !world.RingVisibleTo(viewer.Position, Position) || SilentFor(viewer))
            {
                timingGlow.enabled = timingTarget.enabled = timingApproach.enabled = false;
                if (timingInner != null) timingInner.enabled = false;
                return;
            }
            size *= viewer.RingScale;

            Color c = RingColor(kind);
            Sprite pattern = ShapeLibrary.RingPattern((int)kind);
            timingTarget.sprite = pattern;
            timingApproach.sprite = pattern;
            // Two-beat blow: an inner ring marks the real moment.
            bool twoBeat = kind == RingKind.Double;
            if (twoBeat && timingInner == null) timingInner = SpriteUtil.Make("TimingInner", transform, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);
            float closeAt = twoBeat ? size * 0.6f : size;
            if (timingInner != null)
            {
                timingInner.enabled = twoBeat;
                timingInner.transform.localScale = new Vector3(size * 0.6f, size * 0.6f, 1f);
                timingInner.color = c.WithAlpha(0.6f);
            }

            timingTarget.enabled = true;
            if (StepFrame < cue)
            {
                timingApproach.enabled = true;
                timingGlow.enabled = false;
                float s = Mathf.Lerp(size * 3.4f, closeAt, t);
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
            float pulse = closeAt * (1.18f - 0.18f * k);
            timingTarget.transform.localScale = new Vector3(pulse, pulse, 1f);
            timingTarget.color = Color.Lerp(Color.white, c, 0.35f + 0.4f * k);
            float g = closeAt * 1.9f;
            timingGlow.transform.localScale = new Vector3(g, g, 1f);
            timingGlow.color = c.WithAlpha(0.55f * (1f - k * 0.5f));
        }

        SpriteRenderer timingInner;

        /// <summary>How long the ring takes to close, as this computer's duelist sees it (Ampulheta Quebrada shortens it).</summary>
        int ApproachFrames => Mathf.Max(4, Mathf.RoundToInt(feel.timingApproachFrames * (world.Host.Build != null ? world.Host.Build.Modifiers.ringWarningMult : 1f)));

        public Color RingColor(RingKind kind)
        {
            switch (kind)
            {
                case RingKind.Danger: return pal.danger;
                case RingKind.Feint: return pal.gold;
                case RingKind.Spirit: return ColorUtil.Hex("#B07CFF");
                case RingKind.Cursed: return ColorUtil.Hex("#7BC950");
                case RingKind.Group: return Color.white;
                default: return pal.playerAccent;
            }
        }

        public int NetRingKind { get; private set; }
        /// <summary>Elite affix: rings only show within 3 m.</summary>
        public bool Veiled { get; set; }

        // ── Read by the network snapshot (browser players draw the same telegraphs) ─────

        /// <summary>0 none, 1 cone, 2 circle, 3 line.</summary>
        public int NetTeleType { get; private set; }
        public float NetTeleRange { get; private set; }
        /// <summary>Cone: degrees; line: width.</summary>
        public float NetTeleWidth { get; private set; }
        public float NetTeleAlpha { get; private set; }
        public bool NetDanger { get; private set; }
        /// <summary>Timing ring: 0 none, 1 closing in, 2 closed (press now).</summary>
        public int NetRing { get; private set; }
        public float NetRingSize { get; private set; }
        public float NetRingProgress { get; private set; }
        public float HealthRatio => MaxHealth > 0f ? Mathf.Clamp01(Health / MaxHealth) : 0f;

        protected override void RenderExtra(Vector2 renderPosition, float visualDt)
        {
            AttackDef attack = CurrentAttack;
            RenderTimingIndicator(attack);
            RenderSeals(sealBarY);

            float windupT = attack != null ? Mathf.Clamp01(StepFrame / (float)Mathf.Max(1, stepWindup)) : 0f;
            bool active = attack != null && StepFrame >= stepWindup && StepFrame < stepWindup + attack.active;
            bool telegraphing = attack != null && !attack.NoMeleeHit && StepFrame < stepWindup + attack.active;

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
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
            if (attack != null && attack.stance && StepFrame < stepWindup) glow = Color.white.WithAlpha(0.12f + 0.3f * windupT);
            if (attack != null && (attack.IsSummon || attack.IsBlast) && StepFrame < stepWindup) glow = Def.color.WithAlpha(0.2f + 0.45f * windupT);
            if (IsAlly && !IsGone) glow = pal.gold.WithAlpha(0.18f);
            if (Frozen && !IsGone) c = Color.Lerp(c, Elements.Color(Element.Frost), 0.7f);
            if (InKalpaMushin && !IsGone) glow = pal.danger.WithAlpha(0.3f + 0.2f * wave);
            // Fog: enemies fade out beyond a few metres (snow footprints keep the invisible in sight).
            if (world.FogRange > 0f && !IsGone)
            {
                float d = Vector2.Distance(world.Host.Position, Position);
                c.a *= Mathf.Clamp01(1f - (d - world.FogRange) / 2f);
            }
            if ((Def.ethereal || world.AllEthereal) && !IsGone)
            {
                // Translucent and cold while blades pass through; solid with a golden edge once exposed.
                if (IsIntangible) c.a *= world.Weather == Weather.Snow ? 0.85f : 0.5f;
                else glow = pal.gold.WithAlpha(0.2f + 0.2f * wave);
            }
            if (enraged && !IsGone) glow = Color.Lerp(glow, pal.danger.WithAlpha(0.35f + 0.25f * wave), 0.8f);
            if (Def.healRadius > 0f && healTimer > HealPeriodFrames - 24 && !IsGone) glow = Def.color.WithAlpha(0.5f);
            if (Summoner != null && Summoner.Def.clones) c.a *= 0.88f;
            if (IsBurrowed) c.a = 0f;
            if (IsChilled && !IsGone) c = Color.Lerp(c, Elements.Color(Element.Frost), 0.45f);
            if (dotTime > 0f && !IsGone) c = Color.Lerp(c, dotElement == Element.Blood ? Def.bloodColor : Elements.Color(dotElement), 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 14f));
            // An adapted (held) strike glows violet: the enemy is reading you.
            if (attack != null && stepAdapted && StepFrame < stepWindup) glow = pal.adapted.WithAlpha(0.35f + 0.25f * windupT);
            if (Flashing) glow = Color.white;
            if (gibbed) c.a = 0f;
            glow.a *= c.a;
            SetBodyLook(c, glow);
            shadow.enabled = !gibbed && !IsBurrowed;
            weapon.enabled = !gibbed && !Def.unarmed && !IsBurrowed && CurrentState != State.Reassembling;

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

            NetTeleType = !telegraphing ? 0 : shapeType == HitShapeType.Line ? 3 : shapeType == HitShapeType.Circle ? 2 : 1;
            NetDanger = attack != null && attack.IsDangerous;
            NetTeleAlpha = alpha;
            if (NetTeleType != 0)
            {
                NetTeleRange = shapeType == HitShapeType.Line ? attack.shape.range + (active ? 0f : attack.lunge) : attack.shape.range;
                NetTeleWidth = shapeType == HitShapeType.Line ? attack.shape.width : attack.shape.arcDegrees;
            }

            dangerGlyph.enabled = telegraphing && attack.IsDangerous;
            if (dangerGlyph.enabled)
            {
                float s = 0.42f + 0.08f * Mathf.Sin(Time.unscaledTime * 18f);
                dangerGlyph.transform.localScale = new Vector3(s, s * 1.25f, 1f);
            }

            bool showBars = !IsGone && CurrentState != State.Spawning && CurrentState != State.Reassembling && !IsBurrowed;
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

            bool rising = CurrentState == State.Reassembling;
            brokenRing.enabled = IsBroken || rising;
            if (brokenRing.enabled)
            {
                // Bones rising: a pale ring that tightens as they come back together.
                float s = rising
                    ? Radius * 2f * Mathf.Lerp(2.4f, 1.1f, StateFrame / (float)ReassembleFrames)
                    : Radius * 2f * (1.5f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f));
                brokenRing.transform.localScale = new Vector3(s, s, 1f);
                brokenRing.color = rising ? Def.color.WithAlpha(0.7f) : pal.gold;
            }
        }
    }
}
