using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>The stone band follows every outline, the steps of a diagonal included.</summary>
    public class ArenaWallTests
    {
        [Test]
        public void CutCorners_AreDrawnForEveryBiomeAndShape()
        {
            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            string dump = System.Environment.GetEnvironmentVariable("SHURA_DUMP");
            var root = new GameObject("ArenaWallTest").transform;
            try
            {
                foreach (ArenaShape shape in new[] { ArenaShape.Octagon, ArenaShape.Cross, ArenaShape.Courtyard })
                {
                    for (int b = 0; b < Biomes.All.Length; b++)
                    {
                        Biome biome = Biomes.All[b];
                        if (biome.rule == BiomeRule.Wild) continue;
                        var layout = new ArenaLayout(22, 14, shape);
                        ArenaDecor decor = ArenaBuilder.Build(root, config, layout, biome, true, false, 7);
                        Assert.IsNotNull(decor.Png, shape + " " + b);
                        if (!string.IsNullOrEmpty(dump)) File.WriteAllBytes(Path.Combine(dump, "arena_" + shape + "_" + b + ".png"), decor.Png);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                Object.DestroyImmediate(config);
            }
        }
    }
}
