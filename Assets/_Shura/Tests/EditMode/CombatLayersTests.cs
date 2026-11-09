using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>The combat layers of the design-doc expansion (§3): chords, spirit and group blows, Shura, Links, rings, standoffs.</summary>
    public class CombatLayersTests
    {
        static AttackDef Spirit() => new AttackDef { kind = AttackKind.Spirit, windup = 20, active = 4, recovery = 20, shape = HitShape.Arc(1.6f, 110f) };
        static AttackDef Group() => new AttackDef { kind = AttackKind.Normal, groupStrike = true, windup = 30, active = 6, recovery = 20, shape = HitShape.Circle(2.4f) };

        static DefenderState Facing(Vector2 facing, Vector2 toAttacker)
        {
            return new DefenderState { facing = facing, toAttacker = toAttacker, parryArcDegrees = 120f, counterDot = 0.5f };
        }

        // ── Chords ───────────────────────────────────────────────────────────────────

        [Test]
        public void AttackPlusParry_Together_PressesDetonate()
        {
            var b = new InputBuffer();
            b.Press(BufferedAction.Parry, 6);
            b.Age();
            b.Age();
            b.Press(BufferedAction.Attack, 6);
            Assert.IsTrue(b.Peek(BufferedAction.Detonate));
            Assert.IsFalse(b.Peek(BufferedAction.Kiai));
        }

        [Test]
        public void ParryPlusDodge_Together_PressesKiai()
        {
            var b = new InputBuffer();
            b.Press(BufferedAction.Dodge, 6);
            b.Press(BufferedAction.Parry, 6);
            Assert.IsTrue(b.Peek(BufferedAction.Kiai));
        }

        [Test]
        public void PressesFarApart_AreNotAChord()
        {
            var b = new InputBuffer();
            b.Press(BufferedAction.Parry, 6);
            for (int i = 0; i <= InputBuffer.ChordFrames; i++) b.Age();
            b.Press(BufferedAction.Attack, 6);
            Assert.IsFalse(b.Peek(BufferedAction.Detonate));
        }

        // ── Spirit and group blows ───────────────────────────────────────────────────

        [Test]
        public void SpiritBlow_GoesThroughParryAndBlock()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Spirit(), d));
        }

        [Test]
        public void SpiritBlow_IsAnsweredBySwapInFront_NotBehind()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.swapWindow = true;
            Assert.AreEqual(HitOutcome.SwapParry, HitResolver.Resolve(Spirit(), d));
            DefenderState behind = Facing(Vector2.right, Vector2.left);
            behind.swapWindow = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Spirit(), behind));
        }

        [Test]
        public void SpiritBlow_ATalismanLetsAPerfectParryHold()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.parryWindow = true;
            d.guarding = true;
            d.spiritWard = true;
            Assert.AreEqual(HitOutcome.PerfectParry, HitResolver.Resolve(Spirit(), d));
            d.parryWindow = false;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Spirit(), d), "blocking still does not hold it");
        }

        [Test]
        public void SpiritBlow_IsNotDangerous_NorParryable()
        {
            Assert.IsFalse(Spirit().IsDangerous);
            Assert.IsFalse(Spirit().Parryable);
        }

        [Test]
        public void GroupBlow_AloneCannotBeHeld_TogetherCan()
        {
            DefenderState alone = Facing(Vector2.right, Vector2.right);
            alone.parryWindow = true;
            alone.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Group(), alone));
            DefenderState together = alone;
            together.groupAnswered = true;
            Assert.AreEqual(HitOutcome.PerfectParry, HitResolver.Resolve(Group(), together));
        }

        [Test]
        public void Dodge_StillAnswersSpiritAndGroupBlows()
        {
            DefenderState d = Facing(Vector2.right, Vector2.right);
            d.dodgeIFrames = true;
            Assert.AreEqual(HitOutcome.PerfectDodge, HitResolver.Resolve(Spirit(), d));
            Assert.AreEqual(HitOutcome.PerfectDodge, HitResolver.Resolve(Group(), d));
        }

        // ── Shura ────────────────────────────────────────────────────────────────────

        [Test]
        public void Shura_LevelsAtTheDocThresholds()
        {
            Assert.AreEqual(0, ShuraRules.Level(29.9f));
            Assert.AreEqual(1, ShuraRules.Level(30f));
            Assert.AreEqual(2, ShuraRules.Level(60f));
            Assert.AreEqual(3, ShuraRules.Level(90f));
            Assert.AreEqual(4, ShuraRules.Level(ShuraRules.Max));
            for (int i = 0; i <= 4; i++) Assert.IsNotEmpty(ShuraRules.LevelName(i));
        }

        // ── Links (Troca Perfeita 2.0) ───────────────────────────────────────────────

        [Test]
        public void EveryPairOfOriginalWeapons_HasItsOwnLinkName()
        {
            var names = new HashSet<string>();
            WeaponDef[] all = WeaponDefs.All;
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i + 1; j < all.Length; j++)
                {
                    string n = EloNames.For(all[i].id, all[j].id);
                    Assert.AreEqual(n, EloNames.For(all[j].id, all[i].id), "Elo deve ser simétrico");
                    Assert.IsFalse(n.StartsWith("Elo "), "Par sem nome: " + all[i].id + "/" + all[j].id);
                    Assert.IsTrue(names.Add(n), "Nome repetido: " + n);
                }
            }
            Assert.AreEqual(15, names.Count);
        }

        // ── Rings ────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryRingKind_HasItsOwnPattern()
        {
            var seen = new HashSet<Sprite>();
            foreach (RingKind k in System.Enum.GetValues(typeof(RingKind)))
            {
                Sprite s = ShapeLibrary.RingPattern((int)k);
                Assert.IsNotNull(s);
                Assert.AreSame(s, ShapeLibrary.RingPattern((int)k), "padrão deve ficar em cache");
                seen.Add(s);
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(RingKind)).Length, seen.Count);
        }

        // ── Rooms ────────────────────────────────────────────────────────────────────

        [Test]
        public void Standoffs_AppearInSomeRooms_NeverInGuardianRooms_NorAtDepthOne()
        {
            int standoffs = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                for (int depth = 1; depth <= 12; depth++)
                {
                    RoomDef r = RoomGenerator.Generate(seed, depth);
                    if (!r.standoff) continue;
                    standoffs++;
                    Assert.IsFalse(r.guardian);
                    Assert.GreaterOrEqual(depth, 2);
                }
            }
            Assert.Greater(standoffs, 20);
            Assert.Less(standoffs, 160);
        }

        [Test]
        public void SpiritAndCursedBlows_ExistInTheBestiary()
        {
            bool spirit = false, cursed = false;
            foreach (EnemyDef def in EnemyDefs.All)
            {
                foreach (MoveDef m in def.moves)
                {
                    foreach (AttackDef a in m.steps)
                    {
                        spirit |= a.kind == AttackKind.Spirit;
                        cursed |= a.cursed;
                    }
                }
            }
            Assert.IsTrue(spirit);
            Assert.IsTrue(cursed);
        }

        [Test]
        public void EliteAffixes_AllHaveDistinctNames()
        {
            var names = new HashSet<string>();
            foreach (EliteAffix a in System.Enum.GetValues(typeof(EliteAffix))) Assert.IsTrue(names.Add(EliteAffixes.Name(a)));
            Assert.AreEqual(6, names.Count);
        }
    }
}
