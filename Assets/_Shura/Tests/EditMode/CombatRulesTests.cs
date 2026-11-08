using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    public class CombatRulesTests
    {
        static AttackDef Normal() => new AttackDef { kind = AttackKind.Normal, windup = 10, active = 4, recovery = 10, shape = HitShape.Arc(1.5f, 100f) };
        static AttackDef Thrust() => new AttackDef { kind = AttackKind.Thrust, windup = 30, active = 6, recovery = 20, shape = HitShape.Line(2.4f, 0.7f) };
        static AttackDef Sweep() => new AttackDef { kind = AttackKind.Sweep, windup = 30, active = 6, recovery = 20, shape = HitShape.Circle(2.6f) };

        static DefenderState Facing(Vector2 facing, Vector2 toAttacker)
        {
            return new DefenderState { facing = facing, toAttacker = toAttacker, parryArcDegrees = 120f, counterDot = 0.5f };
        }

        // ── HitResolver ─────────────────────────────────────────────────────────────

        [Test]
        public void ParryWindow_FacingAttacker_IsPerfectParry()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.PerfectParry, HitResolver.Resolve(Normal(), d));
        }

        [Test]
        public void Guarding_AfterWindow_IsBlocked()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Blocked, HitResolver.Resolve(Normal(), d));
        }

        [Test]
        public void Parry_FromBehind_StillHits()
        {
            DefenderState d = Facing(Vector2.right, Vector2.left);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Normal(), d));
        }

        [Test]
        public void Parry_AtEdgeOfArc_Counts()
        {
            Vector2 sixtyDegrees = new Vector2(Mathf.Cos(59f * Mathf.Deg2Rad), Mathf.Sin(59f * Mathf.Deg2Rad));
            DefenderState d = Facing(Vector2.right, sixtyDegrees);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.PerfectParry, HitResolver.Resolve(Normal(), d));
        }

        [Test]
        public void DangerousAttacks_CannotBeParried()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Thrust(), d));
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Sweep(), d));
        }

        [Test]
        public void DodgeIntoThrust_IsCounter()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.dodgeIFrames = true;
            d.dodgeDirection = Vector2.right;
            Assert.AreEqual(HitOutcome.CounterThrust, HitResolver.Resolve(Thrust(), d));
        }

        [Test]
        public void SidestepThrust_IsPerfectDodgeNotCounter()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.dodgeIFrames = true;
            d.dodgeDirection = Vector2.up;
            Assert.AreEqual(HitOutcome.PerfectDodge, HitResolver.Resolve(Thrust(), d));
        }

        [Test]
        public void DodgeThroughSweep_IsPerfectDodge()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.dodgeIFrames = true;
            d.dodgeDirection = Vector2.right;
            Assert.AreEqual(HitOutcome.PerfectDodge, HitResolver.Resolve(Sweep(), d));
        }

        [Test]
        public void Grab_CannotBeParriedOrBlocked_OnlyDodged()
        {
            AttackDef grab = new AttackDef { kind = AttackKind.Grab, windup = 30, active = 6, shape = HitShape.Arc(1.7f, 70f) };
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(grab, d));

            DefenderState dodging = Facing(Vector2.right, Vector2.right);
            dodging.dodgeIFrames = true;
            dodging.dodgeDirection = Vector2.right;
            Assert.AreEqual(HitOutcome.PerfectDodge, HitResolver.Resolve(grab, dodging), "Dodging into a grab is a perfect dodge, not a counter");
        }

        [Test]
        public void Rooms_AreEndlessDeterministicAndFair()
        {
            foreach (string id in RoomGenerator.RosterIds()) Assert.IsTrue(EnemyDefs.TryGet(id, out _), "Roster has unknown enemy " + id);
            Assert.AreEqual("ronin", RoomGenerator.Generate(1, 0).waves[0][0].id, "Depth 0 is the teaching room");

            for (int depth = 1; depth < 60; depth++)
            {
                RoomDef a = RoomGenerator.Generate(77, depth);
                RoomDef b = RoomGenerator.Generate(77, depth);
                Assert.AreEqual(a.name, b.name, "Same seed and depth, same room");
                Assert.AreEqual(a.waves.Length, b.waves.Length);
                Assert.AreEqual(RoomGenerator.IsGuardianDepth(depth), a.guardian);

                List<string> newcomers = RoomGenerator.NewEnemiesAt(depth);
                for (int n = 0; n < newcomers.Count; n++)
                {
                    Assert.AreEqual(1, a.waves[n].Length, "A new enemy is met alone first");
                    Assert.AreEqual(newcomers[n], a.waves[n][0].id);
                }

                bool sawGuardian = false;
                foreach (EnemySpawn[] wave in a.waves)
                {
                    Assert.That(wave.Length, Is.InRange(1, 3), a.name + ": 1 to 3 enemies at once");
                    foreach (EnemySpawn spawn in wave)
                    {
                        Assert.LessOrEqual(RoomGenerator.UnlockDepth(spawn.id), depth, spawn.id + " appeared before it was unlocked");
                        sawGuardian |= spawn.elite;
                    }
                }
                Assert.AreEqual(a.guardian, sawGuardian, "Guardian rooms have exactly the Guardian flag set");
            }
        }

        [Test]
        public void Difficulty_EnemiesOutscaleAscensionButBothGrow()
        {
            for (int d = 1; d < 30; d++)
            {
                Assert.Greater(DifficultyRules.EnemyHealthMult(d), DifficultyRules.EnemyHealthMult(d - 1));
                Assert.GreaterOrEqual(DifficultyRules.AdaptStrength(d), DifficultyRules.AdaptStrength(d - 1));
            }
            var run = new RunState(1, new FlowTuning());
            run.Ascend();
            run.Ascend();
            Assert.AreEqual(2f * DifficultyRules.AscensionHealth, run.Modifiers.maxHealthBonus, 1e-3f);
            Assert.AreEqual(1f + 2f * DifficultyRules.AscensionDamage, run.Modifiers.damageMult, 1e-3f);
        }

        [Test]
        public void Profile_LearnsEarlyParriesAndMashing()
        {
            var profile = new PlayerProfile();
            AdaptiveTraits cold = profile.Traits(1f);
            Assert.AreEqual(0, cold.Active().Count, "No reading before the enemies have watched you fight");

            for (int i = 0; i < 600; i++)
            {
                profile.Tick(SimClock.Dt, true);
                if (i % 20 == 0) profile.OnParryPress(true);
                if (i % 20 == 10) profile.OnAttack();
                if (i % 20 == 15) profile.OnAttack();
                if (i % 30 == 0) profile.OnAttack();
            }
            AdaptiveTraits traits = profile.Traits(1f);
            Assert.Greater(traits.Delay, AdaptiveTraits.ActiveThreshold, "Always parrying early must be noticed");
            Assert.Contains(ReadingId.Delay, traits.Active());
            Assert.Greater(traits.Guard, AdaptiveTraits.ActiveThreshold, "Mashing several attacks per second must be noticed");

            AdaptiveTraits weak = profile.Traits(0.15f);
            Assert.Less(weak.Delay, traits.Delay, "Shallow depths use only part of what was learned");
        }

        [Test]
        public void Blessings_Stack()
        {
            var run = new RunState(3, new FlowTuning());
            run.Acquire(RewardRegistry.Create(RewardIds.BlessingEdge));
            run.Acquire(RewardRegistry.Create(RewardIds.BlessingEdge));
            Assert.AreEqual(1.08f * 1.08f, run.Modifiers.damageMult, 1e-3f);
            run.Acquire(RewardRegistry.Create(RewardIds.HungryBlade));
            run.Acquire(RewardRegistry.Create(RewardIds.HungryBlade));
            Assert.AreEqual(-15f, run.Modifiers.maxHealthBonus, 1e-3f, "Relics never stack");
            List<RewardDef> choices = run.RollChoices(3, 5);
            Assert.AreEqual(RewardKind.Blessing, choices[choices.Count - 1].Kind, "From depth 2 one slot is a Blessing");
        }

        [Test]
        public void HurtInvulnerability_IgnoresHits()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.hurtInvulnerable = true;
            Assert.AreEqual(HitOutcome.Ignored, HitResolver.Resolve(Normal(), d));
        }

        // ── CombatMath ──────────────────────────────────────────────────────────────

        [Test]
        public void Arc_HitsInFront_MissesBehind()
        {
            HitShape arc = HitShape.Arc(1.5f, 100f);
            Assert.IsTrue(CombatMath.Overlaps(arc, Vector2.zero, Vector2.right, new Vector2(1.2f, 0f), 0.4f));
            Assert.IsFalse(CombatMath.Overlaps(arc, Vector2.zero, Vector2.right, new Vector2(-1.2f, 0f), 0.4f));
            Assert.IsFalse(CombatMath.Overlaps(arc, Vector2.zero, Vector2.right, new Vector2(3f, 0f), 0.4f));
        }

        [Test]
        public void Line_RespectsWidthAndLength()
        {
            HitShape line = HitShape.Line(2.4f, 0.7f);
            Assert.IsTrue(CombatMath.Overlaps(line, Vector2.zero, Vector2.right, new Vector2(2f, 0.3f), 0.4f));
            Assert.IsFalse(CombatMath.Overlaps(line, Vector2.zero, Vector2.right, new Vector2(2f, 1.2f), 0.4f));
            Assert.IsFalse(CombatMath.Overlaps(line, Vector2.zero, Vector2.right, new Vector2(3.5f, 0f), 0.4f));
        }

        [Test]
        public void Circle_HitsAllAround()
        {
            HitShape circle = HitShape.Circle(2.6f);
            Assert.IsTrue(CombatMath.Overlaps(circle, Vector2.zero, Vector2.right, new Vector2(-2.5f, 0f), 0.4f));
            Assert.IsFalse(CombatMath.Overlaps(circle, Vector2.zero, Vector2.right, new Vector2(0f, 3.2f), 0.4f));
        }

        // ── Meters, buffer, clock ───────────────────────────────────────────────────

        [Test]
        public void Posture_FillsAndBreaks_ThenRegeneratesAfterDelay()
        {
            var p = new PostureMeter(50f);
            Assert.IsFalse(p.Add(30f));
            Assert.IsTrue(p.Add(30f));
            Assert.AreEqual(50f, p.Value, 1e-3f);

            p.Reset(20f);
            for (int i = 0; i < 10; i++) p.Tick(60f, 10);
            Assert.AreEqual(20f, p.Value, 1e-3f, "No regen during the delay");
            p.Tick(60f, 10);
            Assert.Less(p.Value, 20f);
        }

        [Test]
        public void Flow_LevelsUp_AndDropsToPreviousLevelStart()
        {
            var flow = new FlowMeter(new[] { 3, 7, 12 }, 14f);
            flow.Add(2f);
            Assert.AreEqual(0, flow.Level);
            flow.Add(6f);
            Assert.AreEqual(2, flow.Level);
            flow.DropLevels(1);
            Assert.AreEqual(1, flow.Level);
            Assert.AreEqual(3f, flow.Points, 1e-3f);
            flow.Add(100f);
            Assert.AreEqual(3, flow.Level);
            Assert.AreEqual(14f, flow.Points, 1e-3f);
        }

        [Test]
        public void InputBuffer_ExpiresAfterFrames()
        {
            var b = new InputBuffer();
            b.Press(BufferedAction.Attack, 2);
            b.Age();
            Assert.IsTrue(b.Peek(BufferedAction.Attack));
            b.Age();
            Assert.IsFalse(b.Consume(BufferedAction.Attack));
        }

        [Test]
        public void SimClock_HitstopFreezesTicks()
        {
            var clock = new SimClock();
            clock.AddHitstop(2);
            clock.Accumulate(SimClock.Dt * 3.5f);
            Assert.IsTrue(clock.Step(out bool f1));
            Assert.IsTrue(f1);
            Assert.IsTrue(clock.Step(out bool f2));
            Assert.IsTrue(f2);
            Assert.IsTrue(clock.Step(out bool f3));
            Assert.IsFalse(f3);
            Assert.IsFalse(clock.Step(out _));
            Assert.AreEqual(1, clock.TickCount);
        }

        [Test]
        public void SimClock_LongHitchDoesNotSpiral()
        {
            var clock = new SimClock();
            clock.Accumulate(5f);
            int steps = 0;
            while (clock.Step(out _)) steps++;
            Assert.LessOrEqual(steps, 16);
        }

        [Test]
        public void Coordinator_OneAttackerAtATime_WithGap()
        {
            var c = new AttackCoordinator(3);
            object a = new object(), b = new object();
            Assert.IsTrue(c.TryAcquire(a));
            Assert.IsFalse(c.TryAcquire(b));
            c.Release(a);
            Assert.IsFalse(c.TryAcquire(b), "Gap must pass first");
            c.Tick();
            c.Tick();
            c.Tick();
            Assert.IsTrue(c.TryAcquire(b));
        }

        // ── Run & rewards ───────────────────────────────────────────────────────────

        [Test]
        public void SeededRandom_IsDeterministic()
        {
            var r1 = new SeededRandom(42);
            var r2 = new SeededRandom(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(r1.NextULong(), r2.NextULong());
            var r3 = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = r3.Range(2, 5);
                Assert.That(v, Is.InRange(2, 4));
            }
        }

        [Test]
        public void RewardChoices_PrioritizeAbsorbedTechniques_AndNeverRepeat()
        {
            var run = new RunState(123, new FlowTuning());
            run.AddPendingTechnique(RewardIds.PatientBlade);
            List<RewardDef> choices = run.RollChoices(3);
            Assert.AreEqual(3, choices.Count);
            Assert.AreEqual(RewardIds.PatientBlade, choices[0].Id);
            var ids = new HashSet<string>();
            foreach (RewardDef r in choices) Assert.IsTrue(ids.Add(r.Id), "Duplicate reward offered");

            run.Acquire(choices[0]);
            Assert.IsFalse(run.CanAbsorb(RewardIds.PatientBlade));
            Assert.AreEqual(0, run.PendingTechniques.Count);
        }

        [Test]
        public void RewardChoices_SameSeed_SameOffer()
        {
            var a = new RunState(999, new FlowTuning()).RollChoices(3);
            var b = new RunState(999, new FlowTuning()).RollChoices(3);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Id, b[i].Id);
        }

        [Test]
        public void EveryReward_HasAGainAndACost()
        {
            var ids = new List<string>(RewardIds.Relics);
            ids.AddRange(RewardIds.Techniques);
            ids.AddRange(RewardIds.Blessings);
            foreach (string id in ids)
            {
                RewardDef r = RewardRegistry.Create(id);
                Assert.IsNotNull(r, id);
                Assert.IsFalse(string.IsNullOrEmpty(r.Gain), id + " gain");
                Assert.IsFalse(string.IsNullOrEmpty(r.Cost), id + " cost");
            }
        }

        [Test]
        public void EnemyContent_IsConsistent()
        {
            var techniques = new HashSet<string>();
            foreach (EnemyDef def in EnemyDefs.All)
            {
                // Techniques are optional for the bestiary, but never shared.
                if (def.techniqueId != null)
                {
                    Assert.IsTrue(techniques.Add(def.techniqueId), def.id + " shares a technique with another enemy");
                    Assert.IsNotNull(RewardRegistry.Create(def.techniqueId), def.id + " technique");
                }
                int signatures = 0;
                foreach (MoveDef m in def.moves)
                {
                    if (m.isSignature) signatures++;
                    Assert.Less(m.minRange, m.maxRange, def.id + "/" + m.id);
                    foreach (AttackDef a in m.steps)
                    {
                        Assert.Greater(a.windup, 0, a.id);
                        Assert.Greater(a.active, 0, a.id);
                        Assert.LessOrEqual(a.trackUntil, a.windup, a.id + " must commit before the swing");
                        if (a.IsProjectile) Assert.AreEqual(AttackKind.Normal, a.kind, a.id + " arrows must be parryable");
                    }
                }
                Assert.AreEqual(1, signatures, def.id + " needs exactly one signature move");
            }
        }
    }
}
