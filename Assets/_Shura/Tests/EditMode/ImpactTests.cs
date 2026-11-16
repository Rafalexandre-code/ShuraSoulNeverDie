using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>WP-06: a hierarquia de impacto, o Iai mais raro e justo.</summary>
    public class ImpactTests
    {
        [Test]
        public void TheImpactTable_IsThePlansTable_AndClimbsInOrder()
        {
            var t = new ImpactTable();
            Assert.AreEqual(2, t.hitHitstop);
            Assert.AreEqual(3, t.blockHitstop);
            Assert.AreEqual(6, t.perfectParryHitstop);
            Assert.AreEqual(8, t.swapParryHitstop);
            Assert.AreEqual(8, t.masterHitstop);
            Assert.AreEqual(10, t.breakHitstop);
            Assert.AreEqual(6, t.quickFinisherHitstop);
            Assert.AreEqual(12, t.directedFinisherHitstop);
            Assert.AreEqual(14, t.lastKillHitstop);
            Assert.AreEqual(16, t.bossBreakHitstop);
            Assert.AreEqual(4, t.hurtHitstop);
            // A escada: comum < bloqueio < parry < troca/mestre < quebra < dirigida < último < chefe.
            int[] ladder = { t.hitHitstop, t.blockHitstop, t.perfectParryHitstop, t.swapParryHitstop, t.breakHitstop, t.directedFinisherHitstop, t.lastKillHitstop, t.bossBreakHitstop };
            for (int i = 1; i < ladder.Length; i++) Assert.Greater(ladder[i], ladder[i - 1], "degrau " + i);
            Assert.LessOrEqual(t.hitShake, t.perfectParryShake);
            Assert.LessOrEqual(t.perfectParryShake, t.breakShake);
            Assert.LessOrEqual(t.breakShake, t.bossBreakShake);
        }

        [Test]
        public void NoCommonEvent_UsesSlowMotion()
        {
            var t = new ImpactTable();
            Assert.AreEqual(0f, t.breakSlowMoSeconds, "quebra de postura comum: sem câmera lenta (era 0,25 s)");
            Assert.Greater(t.lastKillSlowMoSeconds, 0f);
            Assert.Greater(t.bossBreakSlowMoSeconds, 0f);
        }

        [Test]
        public void TheStandoff_ComesAtMostOnceEveryFourDepths_NeverTwiceInARow()
        {
            RoomGenerator.StandoffEvery = 4;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                int last = -10, total = 0;
                for (int depth = 0; depth < 60; depth++)
                {
                    if (!RoomGenerator.StandoffAt(seed, depth)) continue;
                    Assert.GreaterOrEqual(depth, 2);
                    Assert.Greater(depth - last, 1, "dois impasses seguidos, semente " + seed);
                    last = depth;
                    total++;
                }
                Assert.LessOrEqual(total, 15);
                Assert.GreaterOrEqual(total, 14, "um por bloco de 4 profundidades (2 a 59)");
            }
        }

        [Test]
        public void MissingTheDraw_NoLongerCostsHealth_ByDefault()
        {
            Assert.AreEqual(0f, new PlayerTuning().standoffMissDamage);
            Assert.AreEqual(15f, new PlayerTuning().perfectCutHeal);
            Assert.Greater(new PlayerTuning().quickFinisherHoldFrames, 0);
        }
    }
}
