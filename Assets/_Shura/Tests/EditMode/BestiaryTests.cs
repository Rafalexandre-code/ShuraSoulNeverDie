using System.Collections.Generic;
using NUnit.Framework;

namespace Shura.Tests
{
    public class BestiaryTests
    {
        static IEnumerable<EnemyDef> Everyone()
        {
            foreach (EnemyDef d in EnemyDefs.All) yield return d;
            foreach (EnemyDef d in EnemyDefs.Minions) yield return d;
        }

        [Test]
        public void Bestiary_IsLargeAndEveryoneIsDifferent()
        {
            Assert.GreaterOrEqual(EnemyDefs.All.Length, 24, "the bestiary grew to two dozen duelists");
            var ids = new HashSet<string>();
            foreach (EnemyDef d in Everyone()) Assert.IsTrue(ids.Add(d.id), "duplicate id " + d.id);
            foreach (string id in RoomGenerator.RosterIds()) Assert.IsFalse(EnemyDefs.Get(id).minion, id + " is a minion in the room roster");
            foreach (EnemyDef d in EnemyDefs.All)
            {
                Assert.LessOrEqual(RoomGenerator.UnlockDepth(d.id), 24, d.id + " is never met (or met too late)");
                Assert.IsFalse(string.IsNullOrEmpty(d.lore), d.id + " needs a line for the new-enemy toast");
            }
        }

        [Test]
        public void EveryoneHasArt()
        {
            foreach (EnemyDef d in Everyone())
            {
                CharacterArt art = PixelArt.Character(d.art);
                Assert.IsNotNull(art, d.id + ": missing character sheets for " + d.art);
                Assert.IsNotNull(art.dead, d.id + ": missing fallen frame");
                if (!d.unarmed) Assert.IsNotNull(PixelArt.Weapon(d.weaponArt), d.id + ": missing weapon " + d.weaponArt);
            }
            Assert.AreEqual(9, FxStrips.Explosion.Length);
            Assert.AreEqual(6, FxStrips.Smoke.Length);
            Assert.IsNotNull(PixelArt.Image("item_bomb"));
            Assert.IsNotNull(PixelArt.ProjectileSprite("fx_kunai"));
            Assert.IsNotNull(PixelArt.ProjectileSprite("fx_shuriken"));
        }

        [Test]
        public void Mechanics_ReferToRealThings()
        {
            foreach (EnemyDef d in Everyone())
            {
                bool summons = false;
                foreach (MoveDef m in d.moves)
                {
                    foreach (AttackDef a in m.steps)
                    {
                        summons |= a.IsSummon;
                        if (a.IsBlast) Assert.Greater(a.blastFuse, 20, a.id + ": a blast needs time to leave the circle");
                        if (a.stance) Assert.Less(System.Array.IndexOf(m.steps, a), m.steps.Length - 1, a.id + ": a stance must lead into a strike");
                        if (a.burrow) Assert.AreEqual(AttackKind.Sweep, a.kind, a.id + ": an eruption cannot be parried");
                        if (a.feint || a.stance) Assert.IsFalse(a.ShowsTimingRing, a.id + ": the ring only ever means a real blow");
                        if (a.projectileCount > 1) Assert.Greater(a.spreadDegrees, 0f, a.id);
                    }
                }
                if (summons) Assert.IsTrue(EnemyDefs.TryGet(d.summonId, out EnemyDef minion) && minion.minion, d.id + " summons an unknown minion");
                if (!string.IsNullOrEmpty(d.splitInto)) Assert.IsTrue(EnemyDefs.TryGet(d.splitInto, out EnemyDef part) && part.minion && string.IsNullOrEmpty(part.splitInto), d.id + " splits forever");
                if (d.ethereal) Assert.IsTrue(HasParryableMelee(d), d.id + ": an ethereal enemy must be exposable by a parry");
            }
        }

        static bool HasParryableMelee(EnemyDef d)
        {
            foreach (MoveDef m in d.moves)
            {
                foreach (AttackDef a in m.steps)
                {
                    if (a.Parryable && !a.NoMeleeHit) return true;
                }
            }
            return false;
        }

        [Test]
        public void EveryEnemy_TeachesItsOwnTechnique()
        {
            var taught = new HashSet<string>();
            foreach (EnemyDef d in EnemyDefs.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(d.techniqueId), d.id + " has no technique to absorb");
                Assert.IsTrue(taught.Add(d.techniqueId), d.id + " shares its technique");
                RewardDef r = RewardRegistry.Create(d.techniqueId);
                Assert.IsNotNull(r, d.techniqueId);
                Assert.AreEqual(RewardKind.Technique, r.Kind, d.techniqueId);
                Assert.AreEqual(d.displayName, r.SourceEnemy, d.techniqueId + " names the wrong teacher");
                Assert.AreEqual(d.techniqueId, r.Id);
            }
            CollectionAssert.AreEquivalent(taught, RewardIds.Techniques, "every technique is taught by exactly one enemy");
            foreach (EnemyDef d in EnemyDefs.Minions) Assert.IsNull(d.techniqueId, d.id + ": minions teach nothing");
        }

        [Test]
        public void TechniqueStatCosts_AreReal()
        {
            foreach (string id in RewardIds.Techniques)
            {
                RewardDef r = RewardRegistry.Create(id);
                var before = new PlayerModifiers();
                var after = new PlayerModifiers();
                r.Apply(after);
                // Either a stat trade (Apply changes something) or a conditional cost spelled out in the text.
                bool statCost = after.maxHealthBonus < before.maxHealthBonus || after.damageMult < 1f || after.damageTakenMult > 1f ||
                                after.blockWearMult > 1f || after.dodgeRechargeMult > 1f || after.dodgeDistanceMult < 1f ||
                                after.moveSpeedMult < 1f || after.perfectParryPostureMult < 1f || after.perfectParryFrameBonus < 0 ||
                                after.normalAttackPostureMult < 1f || after.roomClearHealBonus < 0f;
                Assert.IsTrue(statCost || r.Cost.Contains("menos"), id + " has no real cost");
            }
        }

        [Test]
        public void Rooms_IntroduceTwoNewcomersEachAlone()
        {
            int met = 1;
            for (int depth = 1; depth <= 24; depth++)
            {
                List<string> newcomers = RoomGenerator.NewEnemiesAt(depth);
                Assert.That(newcomers.Count, Is.InRange(1, 2), "depth " + depth);
                met += newcomers.Count;
                RoomDef room = RoomGenerator.Generate(5, depth);
                for (int i = 0; i < newcomers.Count; i++)
                {
                    Assert.AreEqual(1, room.waves[i].Length);
                    Assert.AreEqual(newcomers[i], room.waves[i][0].id);
                }
                if (room.guardian)
                {
                    EnemySpawn boss = room.waves[room.waves.Length - 1][0];
                    Assert.IsTrue(boss.elite);
                    Assert.AreNotEqual("curandeira", boss.id, "a healer never leads a Guardian room");
                    Assert.AreNotEqual("olho", boss.id);
                }
            }
            Assert.AreEqual(EnemyDefs.All.Length, met, "the whole bestiary is met by depth 24");
        }
    }
}
