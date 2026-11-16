using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>WP-07: o Shura é um caminho escolhido (Máscara Hannya, Muramasa ou o juramento).</summary>
    public class AsuraPathTests
    {
        [TearDown]
        public void Restore() => Feature.Reset();

        [Test]
        public void ARunWithoutThePath_HasNoShuraMeter()
        {
            var run = new RunState(5, new FlowTuning());
            Assert.IsFalse(run.Main.AsuraPath);
        }

        [Test]
        public void TheHannyaMask_TheMuramasa_OrTheOath_OpenThePath()
        {
            var a = new RunState(6, new FlowTuning());
            a.Main.Acquire(RewardRegistry.Create(RoadRelics.HannyaMask));
            Assert.IsTrue(a.Main.AsuraPath, "Máscara Hannya");

            var b = new RunState(7, new FlowTuning());
            b.Main.Muramasa = true;
            Assert.IsTrue(b.Main.AsuraPath, "Lâmina de Muramasa");

            var c = new RunState(8, new FlowTuning());
            c.Oaths.Set(Oaths.AsuraPath, 1);
            Assert.IsTrue(c.Main.AsuraPath, "juramento Caminho do Asura");
        }

        [Test]
        public void TheOldFlag_PutsTheMeterBackForEveryone()
        {
            Feature.Set("ShuraMeterBase", true);
            Assert.IsTrue(new RunState(9, new FlowTuning()).Main.AsuraPath);
        }

        [Test]
        public void TheAsuraPathOath_Exists_WithTheShuraKanji()
        {
            OathDef o = Oaths.Get(Oaths.AsuraPath);
            Assert.IsNotNull(o);
            Assert.AreEqual("修", o.kanji);
            Assert.AreEqual(1, o.maxLevel);
        }
    }
}
