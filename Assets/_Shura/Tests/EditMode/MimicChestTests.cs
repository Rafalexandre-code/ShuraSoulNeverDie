using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>O baú amaldiçoado: a folha do mímico existe e a criatura se monta sobre a oferenda.</summary>
    public class MimicChestTests
    {
        [Test]
        public void TheSheet_HasSixRowsOfNineCells()
        {
            var tex = Resources.Load<Texture2D>("Art/Extra/mimic_purple");
            Assert.IsNotNull(tex, "Resources/Art/Extra/mimic_purple.png");
            Assert.AreEqual(36 * 9, tex.width);
            Assert.AreEqual(30 * 6, tex.height);
        }

        [Test]
        public void TheMimic_DressesTheOffering()
        {
            var go = new GameObject("Oferenda");
            try
            {
                World.DressOffering(go.transform, World.MimicArt, new RectInt(0, 0, 1, 1), null, false, 0);
                Assert.IsNotNull(go.GetComponent<MimicChest>());
                Assert.IsNotNull(go.transform.Find("Mimico"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnlyTheMimic_HasABody()
        {
            Assert.AreEqual(0f, new Offering().Solid, "shrines, wares and the torii can be stood on");
        }
    }
}
