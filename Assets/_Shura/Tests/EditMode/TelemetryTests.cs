using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>WP-01: a telemetria classifica cada golpe pelo anel que o jogador vê.</summary>
    public class TelemetryTests
    {
        [Test]
        public void EveryAttack_HasTheRingThePlayerSees()
        {
            Assert.AreEqual("ciano", TelemetryService.RingOf(new AttackDef { kind = AttackKind.Normal }));
            Assert.AreEqual("roxo", TelemetryService.RingOf(new AttackDef { kind = AttackKind.Spirit }));
            Assert.AreEqual("finta", TelemetryService.RingOf(new AttackDef { kind = AttackKind.Normal, feint = true }));
            Assert.AreEqual("maldito", TelemetryService.RingOf(new AttackDef { kind = AttackKind.Normal, cursed = true }));
            Assert.AreEqual("grupo", TelemetryService.RingOf(new AttackDef { kind = AttackKind.Normal, groupStrike = true }));
            Assert.AreEqual("?", TelemetryService.RingOf(null));
            foreach (EnemyDef d in EnemyDefs.All)
            {
                foreach (MoveDef m in d.moves)
                {
                    foreach (AttackDef a in m.steps) Assert.AreNotEqual("?", TelemetryService.RingOf(a), d.id);
                }
            }
        }
    }
}
