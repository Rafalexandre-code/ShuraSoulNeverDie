using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The dojo: a guided first lesson in everything the game asks of a duelist. One thing at
    /// a time, and a lesson only ends when the player has actually done it — walked, cut,
    /// blocked, parried on the beat, dodged the red blow, broken a posture, finished, swapped,
    /// changed guard, drunk, thrown, detonated, cried out — and at the end a real duel.
    ///
    /// For the things that are about timing (the perfect parry, the dodge, the Troca Perfeita)
    /// the first tries stop the world at the exact moment and wait for the button: the player
    /// sees when "now" is before being asked to find it alone.
    /// </summary>
    public sealed class Tutorial
    {
        public enum Step
        {
            Prologue, Hud, Move, Attack, Combo, Block, Parry, ParryFree, Dodge, DodgeFree, Posture, Finisher,
            Swap, SwapParry, Stances, LockOn, Gourd, Bandage, Tool, Seals, Kiai, Duel, Done,
        }

        public const int LessonCount = (int)Step.Done;
        const float FlashSeconds = 1.25f;
        const float AutoSeconds = 9f;
        const int AttackGap = 105;

        readonly Game game;
        Enemy partner;
        int attackTimer;
        bool frozeThisBlow;
        int pressBase;
        float frozenFor;
        int finishersBase, papersBase, sipsBase, bandagesBase;
        float lockedFor;
        bool mouseLockBefore;

        // ── The HUD tour: the world waits while each part of the screen is shown ──────────
        public int TourIndex { get; private set; }
        public float TourTime { get; private set; }
        public bool Touring => Active && Current == Step.Hud && Flash <= 0f;
        int tourPressBase;

        // ── Esc twice skips the dojo ─────────────────────────────────────────────────────
        float skipArmedFor;
        public bool SkipArmed => skipArmedFor > 0f;

        /// <summary>First Esc: the offer to leave; a second one within a few seconds takes it.</summary>
        public void ArmSkip()
        {
            skipArmedFor = 3f;
            game.Audio.Play(Sfx.UiMove, 0.6f, 0.8f, 0f);
        }

        public void NextTourPage()
        {
            if (!Touring) return;
            game.Audio.Play(Sfx.UiMove, 0.6f, 1.1f, 0f);
            if (TourIndex + 1 >= Strings.TutorialTourCount) Complete();
            else
            {
                TourIndex++;
                TourTime = 0f;
            }
        }

        public void PreviousTourPage()
        {
            if (!Touring || TourIndex == 0) return;
            game.Audio.Play(Sfx.UiMove, 0.6f, 0.9f, 0f);
            TourIndex--;
            TourTime = 0f;
        }
        bool comboCounted, dodgedThisBlow, hurtThisBlow, blowWasDangerous, wasAttacking;
        bool listening;

        public Tutorial(Game owner)
        {
            game = owner;
        }

        /// <summary>Only the test robot may be helped along; a person must always do the lesson.</summary>
        bool RobotPlays => game.Launch.Automated && game.ActiveInput is BotInput;
        /// <summary>How far the duelist has walked in this lesson (the marks only count after a real walk).</summary>
        float walked;
        Vector2 lastPos;

        // ── The prologue: the duelist arrives at the dojo and the master speaks ─────────────
        sealed class CinematicInput : IPlayerInputSource
        {
            public Vector2 Walk;
            public Vector2 Move => Walk;
            public bool ParryHeld => false;
            public InputBuffer Buffer { get; } = new InputBuffer();
            public bool InteractHeld => false;
            public bool AttackHeld => false;
            public int TakeKamae() => -1;
            public Vector2 AimDirection(Vector2 playerWorldPosition) => Vector2.zero;
        }

        readonly CinematicInput cinema = new CinematicInput();
        IPlayerInputSource savedInput;
        /// <summary>0 the title card, 1 the walk, 2 the words.</summary>
        public int ProloguePhase { get; private set; }
        public float PhaseTime { get; private set; }
        public int LineIndex { get; private set; }
        public float LineTime { get; private set; }
        int linePressBase;
        Vector2 walkTarget;

        /// <summary>The world waits: the title card and the conversation.</summary>
        public bool HoldWorld => Active && Current == Step.Prologue && ProloguePhase != 1;

        public void AdvanceLine()
        {
            if (Current != Step.Prologue || ProloguePhase != 2) return;
            int len = Strings.PrologueText(LineIndex).Length;
            if (LineTime * Strings.PrologueCharsPerSecond < len)
            {
                LineTime = len / Strings.PrologueCharsPerSecond + 0.01f;
                return;
            }
            if (LineIndex + 1 >= Strings.PrologueLineCount)
            {
                EndPrologue();
                return;
            }
            LineIndex++;
            LineTime = 0f;
            game.Audio.Play(Sfx.UiMove, 0.45f, Strings.PrologueSpeaker(LineIndex) == 1 ? 0.8f : 1.05f, 0f);
        }

        void EndPrologue()
        {
            PlayerController p = Player;
            if (savedInput != null) p.SetInput(savedInput);
            savedInput = null;
            cinema.Walk = Vector2.zero;
            Enter(Step.Hud);
        }

        public bool Active { get; private set; }
        public Step Current { get; private set; }
        public int Progress { get; private set; }
        public int Goal { get; private set; } = 1;
        /// <summary>The world is held still, waiting for one button.</summary>
        public bool Frozen { get; private set; }
        public BufferedAction FreezeAction { get; private set; }
        /// <summary>Seconds left of the "well done" stamp between lessons.</summary>
        public float Flash { get; private set; }
        public float StepTime { get; private set; }
        /// <summary>Guards already taken in the guards lesson (bit per Kamae).</summary>
        public int StanceMask { get; private set; }
        public readonly List<Vector2> Marks = new List<Vector2>();
        public int MarkIndex { get; private set; }
        /// <summary>The seals lesson: a seal is on the partner, G will burst it.</summary>
        public bool SealReady => partner != null && !partner.IsGone && partner.Seals > 0;
        public Enemy Partner => partner != null && !partner.IsGone ? partner : null;

        World World => game.World;
        PlayerController Player => game.World.Host;

        // ── Lifecycle ───────────────────────────────────────────────────────────────────

        /// <summary>Before the dojo is built (hints and announcements stay quiet from here on).</summary>
        public void Prepare()
        {
            Active = true;
            Frozen = false;
            Flash = 0f;
            partner = null;
            if (!listening)
            {
                game.Tap.Emitted += OnMoment;
                listening = true;
            }
        }

        public void Begin()
        {
            Enter(game.Launch.Tutorial && game.Launch.TutorialStep >= 0 ? (Step)Mathf.Clamp(game.Launch.TutorialStep, 0, LessonCount - 1) : Step.Prologue);
        }

        public void Stop()
        {
            Active = false;
            Frozen = false;
            Flash = 0f;
            partner = null;
            Marks.Clear();
            RestoreMouseLock();
            if (game.World != null) game.World.PlayerInvincible = false;
        }

        void RestoreMouseLock()
        {
            if (Current == Step.LockOn) game.Controls.MouseLockAllowed = mouseLockBefore;
        }

        void Enter(Step step)
        {
            if (Current == Step.LockOn && step != Step.LockOn) RestoreMouseLock();
            Current = step;
            Progress = 0;
            Goal = 1;
            StepTime = 0f;
            attackTimer = 70;
            frozeThisBlow = false;
            Frozen = false;
            comboCounted = false;
            Marks.Clear();
            MarkIndex = 0;
            PlayerController p = Player;
            walked = 0f;
            lastPos = p.Position;
            PlayerBuild b = p.Build;
            switch (step)
            {
                case Step.Prologue:
                {
                    ProloguePhase = 0;
                    PhaseTime = 0f;
                    LineIndex = 0;
                    LineTime = 0f;
                    Rect f = World.Layout.FloorBounds;
                    if (partner == null || partner.IsGone) partner = World.SpawnPartner(EnemyDefs.Sensei, World.Layout.NearestFloor(new Vector2(f.center.x, f.center.y + 1.6f), 0.6f), true);
                    p.Teleport(World.Layout.NearestFloor(new Vector2(f.center.x - 5.5f, f.yMin + 1.2f), 0.6f));
                    walkTarget = World.Layout.NearestFloor(partner.Position + Vector2.down * 1.8f, 0.6f);
                    savedInput = p.Input;
                    p.SetInput(cinema);
                    break;
                }
                case Step.Hud:
                    TourIndex = 0;
                    TourTime = 0f;
                    tourPressBase = game.Controls.PressCounts[0];
                    break;
                case Step.LockOn:
                    Goal = 1;
                    lockedFor = 0f;
                    // The keyboard's lock is optional: the dojo lends it for this lesson.
                    mouseLockBefore = game.Controls.MouseLockAllowed;
                    game.Controls.MouseLockAllowed = true;
                    break;
                case Step.Bandage:
                    if (b != null)
                    {
                        b.Bandages = Mathf.Max(b.Bandages, 1);
                        bandagesBase = b.Bandages;
                    }
                    p.TrainingInjure(WoundRegion.Arm);
                    p.TrainingWound(0.7f);
                    break;
                case Step.Move:
                {
                    Goal = 3;
                    Rect f = World.Layout.FloorBounds;
                    Marks.Add(World.Layout.NearestFloor(new Vector2(f.center.x - 2f, f.center.y + 2.5f), 0.6f));
                    Marks.Add(World.Layout.NearestFloor(new Vector2(f.center.x + 3.5f, f.center.y + 1f), 0.6f));
                    Marks.Add(World.Layout.NearestFloor(new Vector2(f.center.x + 0.5f, f.center.y - 2.5f), 0.6f));
                    break;
                }
                case Step.Attack: Goal = 3; break;
                case Step.Combo: Goal = 2; break;
                case Step.Block: Goal = 2; break;
                case Step.Parry: Goal = 2; break;
                case Step.ParryFree: Goal = 3; break;
                case Step.Dodge: Goal = 1; break;
                case Step.DodgeFree: Goal = 2; break;
                case Step.Finisher: finishersBase = b != null ? b.Finishers : 0; break;
                case Step.SwapParry:
                    // A clean arm: the swap can always become a parry here.
                    p.ClearWounds();
                    break;
                case Step.Stances:
                    Goal = 4;
                    StanceMask = 1 << (int)p.Stance;
                    Progress = 1;
                    break;
                case Step.Gourd:
                    p.TrainingWound(0.45f);
                    p.TrainingRefill();
                    sipsBase = p.Sips;
                    break;
                case Step.Tool:
                    if (b != null)
                    {
                        b.SpiritPapers = Mathf.Max(b.SpiritPapers, 4);
                        papersBase = b.SpiritPapers;
                    }
                    break;
                case Step.Kiai:
                    // The cry spends a level of Flow: the dojo lends one.
                    for (int i = 0; b != null && i < 40 && b.Flow.Level < 1; i++) b.Flow.Add(4f);
                    break;
                case Step.Duel:
                    if (partner != null && !partner.IsGone) partner.Dispel();
                    partner = World.SpawnPartner(EnemyDefs.Ronin, FrontOf(p, 5f), false);
                    break;
                case Step.Done:
                    if (partner != null && !partner.IsGone && partner.Training) partner.Dispel();
                    game.OnTutorialFinished();
                    break;
            }
            Debug.Log("[Shura][Tutorial] lição " + ((int)step + 1) + "/" + LessonCount + ": " + step + (game.Launch.Automated ? " (teste)" : ""));
        }

        /// <summary>The middle of the dojo, or a few steps from the duelist if they stand there.</summary>
        Vector2 PartnerSpot(PlayerController p)
        {
            Vector2 c = World.Layout.FloorBounds.center;
            if (Vector2.Distance(c, p.Position) >= 3f) return c;
            return c + (p.Position.x > c.x ? Vector2.left : Vector2.right) * 4f;
        }

        static Vector2 FrontOf(PlayerController p, float distance)
        {
            Vector2 dir = p.Facing.sqrMagnitude > 0.01f ? p.Facing : Vector2.right;
            return p.Position + dir * distance;
        }

        bool NeedsPartner => Current != Step.Prologue && Current != Step.Hud && Current != Step.Move && Current != Step.Done && Current != Step.Duel;
        bool PartnerStrikes => Current == Step.Block || Current == Step.Parry || Current == Step.ParryFree || Current == Step.Dodge ||
                               Current == Step.DodgeFree || Current == Step.SwapParry || (Current == Step.Seals && !SealReady) ||
                               Current == Step.Kiai;
        bool DangerousBlow => Current == Step.Dodge || Current == Step.DodgeFree || Current == Step.Kiai;
        bool FreezeStep => Current == Step.Parry || Current == Step.Dodge || Current == Step.SwapParry;

        // ── Per simulation tick ─────────────────────────────────────────────────────────

        public void Tick()
        {
            if (!Active || Current == Step.Done) return;
            PlayerController p = Player;
            // Nobody dies in the dojo.
            World.PlayerInvincible = p.Health <= p.MaxHealth * 0.3f;
            if (Flash > 0f) return;

            if (NeedsPartner)
            {
                if (partner == null || partner.IsGone)
                {
                    partner = World.SpawnPartner(EnemyDefs.Sensei, PartnerSpot(p), true);
                    attackTimer = 90;
                }
                // The master never falls outside the lessons about falling.
                if (partner.Health < partner.MaxHealth * 0.7f) partner.Mend(partner.MaxHealth, 0f);
                if (Current != Step.Posture && Current != Step.Finisher && Current != Step.Seals) partner.Mend(0f, 4f);
            }

            if (partner != null && !partner.IsGone && partner.Training)
            {
                if (PartnerStrikes)
                {
                    if (!partner.TrainingBusy && --attackTimer <= 0)
                    {
                        partner.RequestTraining(DangerousBlow);
                        attackTimer = AttackGap;
                        frozeThisBlow = false;
                    }
                }
                else partner.CancelTraining();
                TrackBlow(p);
                if (FreezeStep && !frozeThisBlow && partner.CurrentAttack != null) TryFreeze(p);
            }

            switch (Current)
            {
                case Step.Prologue:
                    if (ProloguePhase == 1)
                    {
                        // A slow, steady walk up to the master.
                        Vector2 to = walkTarget - p.Position;
                        cinema.Walk = to.magnitude > 0.25f ? to.normalized * 0.55f : Vector2.zero;
                        if (to.magnitude <= 0.25f)
                        {
                            p.Facing = Vector2.up;
                            ProloguePhase = 2;
                            LineTime = 0f;
                            linePressBase = game.Controls.PressCounts[0];
                        }
                    }
                    break;
                case Step.Move:
                    walked += Vector2.Distance(p.Position, lastPos);
                    lastPos = p.Position;
                    if (walked > 1f && MarkIndex < Marks.Count && Vector2.Distance(p.Position, Marks[MarkIndex]) < 0.9f)
                    {
                        MarkIndex++;
                        game.Audio.Play(Sfx.Chime, 0.5f, 1f + 0.12f * MarkIndex, 0f);
                        Advance(1);
                    }
                    break;
                case Step.Combo:
                {
                    bool last = p.CurrentState == PlayerController.State.Attack && p.ComboIndex == p.Weapon.combo.Length - 1;
                    if (last && !comboCounted)
                    {
                        comboCounted = true;
                        Advance(1);
                    }
                    if (!last) comboCounted = false;
                    break;
                }
                case Step.Posture:
                    if (partner != null && partner.IsBroken) Complete();
                    break;
                case Step.Finisher:
                    if (p.Build != null && p.Build.Finishers > finishersBase) Complete();
                    else if (partner != null && !partner.IsGone && !partner.IsBroken && partner.CurrentState != Enemy.State.Finishing)
                    {
                        // The opening closed before the blow: the dojo opens it again.
                        partner.ApplyPosture(partner.Posture.Max);
                    }
                    break;
                case Step.Gourd:
                    if (p.Sips < sipsBase) Complete();
                    break;
                case Step.LockOn:
                    lockedFor = p.LockTarget != null ? lockedFor + SimClock.Dt : 0f;
                    Progress = lockedFor >= 1.2f ? 1 : 0;
                    if (lockedFor >= 1.2f) Complete();
                    break;
                case Step.Bandage:
                    if (p.Build != null && p.Build.Bandages < bandagesBase) Complete();
                    break;
                case Step.Tool:
                    if (p.Build != null && p.Build.SpiritPapers < papersBase) Complete();
                    break;
                case Step.Duel:
                    if (partner == null || partner.IsGone) Complete();
                    break;
            }
        }

        /// <summary>The red blow came and went without touching the duelist who moved out of it: that is a dodge too.</summary>
        void TrackBlow(PlayerController p)
        {
            bool attacking = partner.CurrentAttack != null;
            if (attacking && !wasAttacking)
            {
                dodgedThisBlow = false;
                hurtThisBlow = false;
                blowWasDangerous = partner.CurrentAttack.IsDangerous;
            }
            if (attacking && p.CurrentState == PlayerController.State.Dodge) dodgedThisBlow = true;
            if (attacking && p.CurrentState == PlayerController.State.Hurt) hurtThisBlow = true;
            if (!attacking && wasAttacking && blowWasDangerous && dodgedThisBlow && !hurtThisBlow &&
                (Current == Step.Dodge || Current == Step.DodgeFree))
            {
                Advance(1);
            }
            wasAttacking = attacking;
        }

        /// <summary>Stops the world at the instant the answer must be given.</summary>
        void TryFreeze(PlayerController p)
        {
            int until = partner.FramesUntilActive;
            // Parry and the Troca Perfeita share the perfect window: stop well inside it.
            int at = Current == Step.Dodge ? 4 : Mathf.Max(2, Mathf.Min(game.Config.feel.timingCueLeadFrames, p.PerfectParryFrames - 2));
            if (until > at || until < 1) return;
            // Too far to be hit: no lesson in this blow.
            if (Vector2.Distance(p.Position, partner.Position) > 4.5f) return;
            frozeThisBlow = true;
            // Whatever the duelist was doing, the answer must come out at once.
            p.TrainingInterrupt();
            Frozen = true;
            frozenFor = 0f;
            FreezeAction = Current == Step.Parry ? BufferedAction.Parry : Current == Step.Dodge ? BufferedAction.Dodge : BufferedAction.Swap;
            pressBase = game.Controls.PressCounts[PressIndex(FreezeAction)];
            game.Audio.Play(Sfx.Glint, 0.8f, 1f, 0f);
        }

        static int PressIndex(BufferedAction a) => a == BufferedAction.Parry ? 1 : a == BufferedAction.Dodge ? 2 : 3;

        // ── Per rendered frame ──────────────────────────────────────────────────────────

        public void Update(float dt)
        {
            if (!Active) return;
            StepTime += dt;
            if (skipArmedFor > 0f) skipArmedFor -= dt;
            if (Active && Current == Step.Prologue)
            {
                PhaseTime += dt;
                if (ProloguePhase == 0 && PhaseTime > 3.4f)
                {
                    ProloguePhase = 1;
                    PhaseTime = 0f;
                }
                else if (ProloguePhase == 1 && PhaseTime > 10f)
                {
                    // Never stuck on the way in.
                    Player.Teleport(walkTarget);
                    Player.Facing = Vector2.up;
                    cinema.Walk = Vector2.zero;
                    ProloguePhase = 2;
                    LineTime = 0f;
                    linePressBase = game.Controls.PressCounts[0];
                }
                else if (ProloguePhase == 2)
                {
                    LineTime += dt;
                    InputService c = game.Controls;
                    bool next = c.Submit || c.PressCounts[0] != linePressBase;
                    linePressBase = c.PressCounts[0];
                    if (RobotPlays && LineTime > 1.4f) next = true;
                    if (next && LineTime > 0.12f) AdvanceLine();
                }
                return;
            }
            if (Touring)
            {
                TourTime += dt;
                InputService c = game.Controls;
                bool next = c.Submit || c.PressCounts[0] != tourPressBase || c.NavX > 0;
                tourPressBase = c.PressCounts[0];
                if (RobotPlays && TourTime > 0.9f) next = true;
                if (next && TourTime > 0.15f) NextTourPage();
                else if (c.NavX < 0) PreviousTourPage();
                return;
            }
            if (Flash > 0f)
            {
                Flash -= dt;
                if (Flash <= 0f) Enter(Current + 1);
                return;
            }
            if (Frozen)
            {
                frozenFor += dt;
                bool pressed = game.Controls.PressCounts[PressIndex(FreezeAction)] != pressBase;
                if (RobotPlays && frozenFor > 0.7f)
                {
                    // The test duelist answers after a look at the frozen moment.
                    game.ActiveInput.Buffer.Press(FreezeAction, 8);
                    pressed = true;
                }
                if (pressed) Frozen = false;
                return;
            }
            // Test runs: a lesson the robot cannot do by itself moves on (and says so).
            if (RobotPlays && Current != Step.Done && StepTime > AutoSeconds)
            {
                Debug.Log("[Shura][Tutorial] lição " + Current + " avançada automaticamente (" + Progress + "/" + Goal + ")");
                Complete();
            }
        }

        // ── What the player just did ────────────────────────────────────────────────────

        void OnMoment(FxType type, Vector2 position, float magnitude)
        {
            if (!Active || Flash > 0f) return;
            switch (Current)
            {
                case Step.Attack:
                    if (type == FxType.HitEnemy) Advance(1);
                    break;
                case Step.Block:
                    if (type == FxType.PlayerBlock || type == FxType.PerfectParry) Advance(1);
                    break;
                case Step.Parry:
                case Step.ParryFree:
                    if (type == FxType.PerfectParry || type == FxType.SwapParry) Advance(1);
                    break;
                case Step.Dodge:
                case Step.DodgeFree:
                    if (type == FxType.PerfectDodge || type == FxType.CounterThrust)
                    {
                        // Counted once per blow (TrackBlow would count the same dodge again).
                        dodgedThisBlow = false;
                        hurtThisBlow = true;
                        Advance(1);
                    }
                    break;
                case Step.Swap:
                    if (type == FxType.WeaponSwap || type == FxType.SwapParry) Advance(1);
                    break;
                case Step.SwapParry:
                    if (type == FxType.SwapParry) Advance(1);
                    break;
                case Step.Stances:
                    if (type == FxType.Kamae)
                    {
                        StanceMask |= 1 << Mathf.Clamp(Mathf.RoundToInt(magnitude), 0, 3);
                        int n = 0;
                        for (int i = 0; i < 4; i++) if ((StanceMask & (1 << i)) != 0) n++;
                        Progress = n;
                        if (Progress >= Goal) Complete();
                    }
                    break;
                case Step.Seals:
                    if (type == FxType.SealBurst) Complete();
                    break;
                case Step.Kiai:
                    // The duelist's own cry (not the party's).
                    if (type == FxType.Kiai && Vector2.Distance(position, Player.Position) < 0.6f) Complete();
                    break;
            }
        }

        void Advance(int amount)
        {
            if (Flash > 0f || Current == Step.Done) return;
            Progress = Mathf.Min(Goal, Progress + amount);
            if (Progress >= Goal) Complete();
        }

        void Complete()
        {
            if (Flash > 0f || Current == Step.Done) return;
            Progress = Goal;
            Frozen = false;
            Flash = FlashSeconds;
            partner?.CancelTraining();
            game.Audio.Play(Sfx.Chime, 0.9f, 1f, 0f);
            game.Audio.Play(Sfx.FlowUp, 0.5f, 1.1f, 0f);
        }

        /// <summary>Pause menu: skip the lesson being taught.</summary>
        public void SkipLesson()
        {
            if (Active && Current != Step.Done) Complete();
        }

        // ── Words ───────────────────────────────────────────────────────────────────────

        public string Title => Strings.TutorialTitle(Current);

        public string Text => Strings.TutorialText(Current, game.Controls.UsingGamepad, SealReady);

        /// <summary>The button(s) this lesson is about, as key caps.</summary>
        public string[] Keys => Strings.TutorialKeys(Current, game.Controls.UsingGamepad, SealReady);

        public string FreezeKey => Strings.TutorialKeys(FreezeAction == BufferedAction.Parry ? Step.Block : FreezeAction == BufferedAction.Dodge ? Step.Dodge : Step.Swap,
                                                        game.Controls.UsingGamepad, false)[0];

        public string FreezeVerb => FreezeAction == BufferedAction.Parry ? Strings.TutorialFreezeParry
                                  : FreezeAction == BufferedAction.Dodge ? Strings.TutorialFreezeDodge : Strings.TutorialFreezeSwap;
    }
}
