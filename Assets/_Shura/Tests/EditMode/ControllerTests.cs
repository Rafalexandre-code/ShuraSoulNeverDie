using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>The gamepad port: the souls-like target lock and the remappable controls.</summary>
    public class ControllerTests
    {
        static TargetLock With(params Vector2[] positions)
        {
            var l = new TargetLock();
            for (int i = 0; i < positions.Length; i++) l.Candidates.Add(new LockCandidate { id = i + 1, pos = positions[i] });
            return l;
        }

        [Test]
        public void Lock_TakesTheEnemyInFront_AndLetsGo()
        {
            TargetLock l = With(new Vector2(-3f, 0f), new Vector2(4f, 0.5f), new Vector2(30f, 0f));
            Assert.IsTrue(l.Toggle(Vector2.zero, Vector2.right));
            Assert.AreEqual(2, l.TargetId, "the one in front, not the closer one behind");
            Assert.IsFalse(l.Toggle(Vector2.zero, Vector2.right));
            Assert.IsFalse(l.Active);

            TargetLock far = With(new Vector2(30f, 0f));
            Assert.IsFalse(far.Toggle(Vector2.zero, Vector2.right), "nothing within reach");
        }

        [Test]
        public void Lock_FlickJumpsToTheEnemyThatWay()
        {
            TargetLock l = With(new Vector2(4f, 0f), new Vector2(4f, 4f), new Vector2(4f, -4f));
            l.Acquire(Vector2.zero, Vector2.right);
            Assert.AreEqual(1, l.TargetId);
            Assert.IsTrue(l.Switch(Vector2.zero, Vector2.up));
            Assert.AreEqual(2, l.TargetId);
            Assert.IsTrue(l.Switch(Vector2.zero, Vector2.down));
            Assert.AreEqual(1, l.TargetId, "down from the top one is the middle one");
            Assert.IsTrue(l.Cycle(Vector2.zero, 1));
            Assert.AreEqual(3, l.TargetId, "clockwise from the right is below");
        }

        [Test]
        public void Lock_MovesOnWhenTheTargetFalls_AndBreaksFarAway()
        {
            TargetLock l = With(new Vector2(3f, 0f), new Vector2(-3f, 1f));
            l.Acquire(Vector2.zero, Vector2.right);
            Assert.AreEqual(1, l.TargetId);
            l.Candidates.RemoveAt(0);
            Assert.AreEqual(1, l.Refresh(Vector2.zero), "the lock jumps to the next enemy");
            Assert.AreEqual(2, l.TargetId);
            Assert.AreEqual(-1, l.Refresh(new Vector2(40f, 0f)), "too far: it lets go");
            Assert.IsFalse(l.Active);
        }

        [Test]
        public void Lock_IsAlwaysOnWithAPad_AndOptionalOnTheMouse()
        {
            TargetLock pad = With(new Vector2(3f, 0f));
            pad.ApplyPolicy(true, false, false, Vector2.zero, Vector2.right);
            Assert.IsTrue(pad.Active, "a gamepad locks by itself");
            pad.ApplyPolicy(true, false, true, Vector2.zero, Vector2.right);
            Assert.IsTrue(pad.Active, "R3 picks again; it never lets go");

            TargetLock mouse = With(new Vector2(3f, 0f));
            mouse.ApplyPolicy(false, false, true, Vector2.zero, Vector2.right);
            Assert.IsFalse(mouse.Active, "mouse aim, as before, unless the option is on");
            mouse.ApplyPolicy(false, true, true, Vector2.zero, Vector2.right);
            Assert.IsTrue(mouse.Active);
            mouse.ApplyPolicy(false, false, false, Vector2.zero, Vector2.right);
            Assert.IsFalse(mouse.Active, "turning the option off frees the mouse");
        }

        [Test]
        public void Glyphs_NameTheButtonsOfEachPad()
        {
            Assert.AreEqual("A", InputService.PathName("<Gamepad>/buttonSouth", GlyphStyle.Xbox));
            Assert.AreEqual("✕", InputService.PathName("<Gamepad>/buttonSouth", GlyphStyle.PlayStation));
            Assert.AreEqual("R1", InputService.PathName("<Gamepad>/rightShoulder", GlyphStyle.PlayStation));
            Assert.AreEqual("RB", InputService.PathName("<Gamepad>/rightShoulder", GlyphStyle.Xbox));
            Assert.AreEqual("R3", InputService.PathName("<Gamepad>/rightStickPress", GlyphStyle.Xbox));
            Assert.AreEqual("Q", InputService.PathName("<Keyboard>/q", GlyphStyle.Keyboard));
            Assert.AreEqual("Espaço", InputService.PathName("<Keyboard>/space", GlyphStyle.Keyboard));
            Assert.AreEqual("Mouse esq.", InputService.PathName("<Mouse>/leftButton", GlyphStyle.Keyboard));
        }

        [Test]
        public void Remap_SavesPerSlot_AndComesBack()
        {
            var a = new InputService();
            try
            {
                Assert.AreEqual("Q", a.Glyph(ControlAction.Swap, GlyphStyle.Keyboard));
                Assert.AreEqual("R3", a.Glyph(ControlAction.LockOn, GlyphStyle.Xbox), "souls-like: R3 locks on");
                Assert.AreEqual("L3", a.Glyph(ControlAction.Soul, GlyphStyle.Xbox));
                a.ClearSlot(ControlAction.Swap, 0);
                Assert.AreEqual("Mouse meio", a.Glyph(ControlAction.Swap, GlyphStyle.Keyboard), "the alternative takes over");
                string saved = a.SaveOverrides();
                StringAssert.Contains("Trocar arma#0=", saved);
                a.Disable();

                var b = new InputService();
                b.LoadOverrides(saved);
                Assert.AreEqual("—", b.SlotName(ControlAction.Swap, 0));
                b.ResetDevice(false);
                Assert.AreEqual("Q", b.SlotName(ControlAction.Swap, 0));
                b.LoadOverrides("{\"bindings\":[]}");
                b.LoadOverrides("lixo sem sentido\nAtacar#9=<Keyboard>/u");
                Assert.AreEqual("Mouse esq.", b.SlotName(ControlAction.Attack, 0), "junk never breaks the controls");
                b.Disable();
            }
            finally
            {
                a.Disable();
            }
        }
    }
}
