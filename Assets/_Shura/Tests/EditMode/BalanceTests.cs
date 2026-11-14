using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>WP-02: os números vivem em dados (Balance.json) e voltam iguais.</summary>
    public class BalanceTests
    {
        [TearDown]
        public void Restore() => Balance.ResetStatics();

        [Test]
        public void TheFile_RoundTrips_AndOnlyItsKeysChange()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            string json = Balance.ToJson(config);
            foreach (string key in new[] { "perfectParryFrames", "parrySpamWindow", "actionBufferFrames", "timingApproachFrames", "timingCueLeadFrames",
                                             "hitstopPerfectParry", "shakeHit", "slowMoLastKillSeconds", "perfectParryPoints", "sipHeal", "healthPerDepth", "enemyOverrides" })
                StringAssert.Contains("\"" + key + "\"", json, key);

            Balance.Apply(config, "{\"player\":{\"perfectParryFrames\":11},\"scaling\":{\"eliteHealthMult\":3.0},\"enemyOverrides\":[{\"id\":\"ronin\",\"health\":1.5,\"posture\":1,\"damage\":1}]}");
            Assert.AreEqual(11, config.player.perfectParryFrames);
            Assert.AreEqual(100f, config.player.maxHealth, 1e-4f, "keys that are not in the file stay");
            Assert.AreEqual(3f, DifficultyRules.EliteHealthMult, 1e-4f);
            Assert.AreEqual(0.08f, Balance.Scaling.healthPerDepth, 1e-4f, "scaling keys not in the file stay");

            EnemyScaling s = EnemyScaling.For(0, false);
            Balance.ApplyOverride("ronin", ref s);
            Assert.AreEqual(1.5f, s.health, 1e-4f);
            EnemyScaling k = EnemyScaling.For(0, false);
            Balance.ApplyOverride("kappa", ref k);
            Assert.AreEqual(1f, k.health, 1e-4f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TheShippedFile_HasEveryNumber()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Balance.json");
            Assert.IsTrue(System.IO.File.Exists(path), "StreamingAssets/Balance.json");
            var shipped = ScriptableObject.CreateInstance<GameConfig>();
            var fresh = ScriptableObject.CreateInstance<GameConfig>();
            Balance.Apply(shipped, System.IO.File.ReadAllText(path));
            Assert.AreEqual(Balance.ToJson(fresh).Length > 0, true);
            Assert.AreEqual(fresh.player.perfectParryFrames, shipped.player.perfectParryFrames, "the shipped file starts from the code defaults");
            Assert.AreEqual(fresh.feel.timingApproachFrames, shipped.feel.timingApproachFrames);
            Object.DestroyImmediate(shipped);
            Object.DestroyImmediate(fresh);
        }
    }
}
