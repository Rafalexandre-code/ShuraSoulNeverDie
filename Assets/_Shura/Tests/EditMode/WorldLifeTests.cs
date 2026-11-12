using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>The living world: the 24-hour day, the lakes of the Wild Wood, the dojo's order.</summary>
    public class WorldLifeTests
    {
        [Test]
        public void Day_RunsAMinutePerSecond_AndNamesItsPeriods()
        {
            var c = new DayClock(5.5f);
            Assert.AreEqual(TimeOfDay.Dawn, c.Period, "the journey leaves at dawn");
            c.Advance(60f * 2f);
            Assert.AreEqual(7.5f, c.Hour, 1e-3f, "two real minutes = two game hours");
            Assert.AreEqual(TimeOfDay.Morning, c.Period);
            Assert.AreEqual(TimeOfDay.Afternoon, DayClock.PeriodAt(14f));
            Assert.AreEqual(TimeOfDay.Dusk, DayClock.PeriodAt(18f));
            Assert.AreEqual(TimeOfDay.Night, DayClock.PeriodAt(23f));
            Assert.AreEqual(TimeOfDay.Night, DayClock.PeriodAt(2f));
            Assert.AreEqual("06:42", DayClock.Format(6.7f));
        }

        [Test]
        public void Day_MarksEveryMoment_EvenAcrossMidnight()
        {
            var c = new DayClock(23.9f);
            var seen = new List<int>();
            c.MomentReached += seen.Add;
            c.Advance(60f * 2f);
            Assert.Contains(7, seen, "midnight");
            Assert.Contains(8, seen, "the Hour of the Ox");
            Assert.AreEqual(2, c.Day);

            var d = new DayClock(11.9f);
            var noon = new List<int>();
            d.MomentReached += noon.Add;
            d.Advance(30f);
            CollectionAssert.AreEqual(new[] { 3 }, noon, "the noon bell, once");
        }

        [Test]
        public void Day_LightChangesSmoothly()
        {
            float prev = DayClock.Darkness(0f);
            for (float h = 0.1f; h < 24f; h += 0.1f)
            {
                float d = DayClock.Darkness(h);
                Assert.Less(Mathf.Abs(d - prev), 0.03f, "no jump at " + h);
                prev = d;
            }
            Assert.Less(DayClock.Darkness(12f), DayClock.Darkness(6f));
            Assert.Less(DayClock.Darkness(6f), DayClock.Darkness(23f));
        }

        [Test]
        public void Lakes_AreDeepInTheMiddle_AndShallowAtTheShore()
        {
            var layout = new ArenaLayout(24, 14, ArenaShape.Hall);
            Vector2 c = layout.FloorBounds.center;
            layout.AddLake(c, new Vector2(3f, 2f));
            Assert.AreEqual(1f, layout.LakeDepth(c), 1e-3f);
            Assert.Greater(layout.LakeDepth(c + new Vector2(2.5f, 0f)), 0f);
            Assert.AreEqual(0f, layout.LakeDepth(c + new Vector2(3.5f, 0f)));
            Assert.Greater(layout.LakeDepth(c + new Vector2(3.2f, 0f), 0.45f), 0f, "the shore ring");
            Assert.IsTrue(layout.Fits(c, 0.4f), "shallow water: you can wade in");
        }

        [Test]
        public void Dojo_OpensWithThePrologue_ThenTheScreen_ThenTheLessons()
        {
            Assert.AreEqual(0, (int)Tutorial.Step.Prologue);
            Assert.AreEqual(1, (int)Tutorial.Step.Hud);
            Assert.AreEqual(2, (int)Tutorial.Step.Move);
            Assert.Greater(Strings.PrologueLineCount, 6);
            Assert.Greater(Strings.TutorialTourCount, 15);
            for (int i = 0; i < Strings.PrologueLineCount; i++) Assert.That(Strings.PrologueSpeaker(i), Is.InRange(0, 1));
        }
    }
}
