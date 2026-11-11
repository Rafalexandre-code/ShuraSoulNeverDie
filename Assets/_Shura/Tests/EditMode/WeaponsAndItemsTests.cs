using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    public class WeaponsAndItemsTests
    {
        static AttackDef Normal() => new AttackDef { kind = AttackKind.Normal, windup = 10, active = 4, shape = HitShape.Arc(1.5f, 100f) };

        [Test]
        public void SwapWindow_IsASwapParry_ButHasNoBlockFallback()
        {
            var d = new DefenderState { facing = Vector2.right, toAttacker = Vector2.right, parryArcDegrees = 120f, swapWindow = true };
            Assert.AreEqual(HitOutcome.SwapParry, HitResolver.Resolve(Normal(), d));

            d.swapWindow = false;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Normal(), d), "A missed swap is a clean hit: no guard to fall back on");

            var behind = new DefenderState { facing = Vector2.right, toAttacker = Vector2.left, parryArcDegrees = 120f, swapWindow = true };
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(Normal(), behind), "Swap-parry is directional like a parry");

            var danger = new AttackDef { kind = AttackKind.Sweep, windup = 30, active = 6, shape = HitShape.Circle(2.6f) };
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(danger, d), "Dangerous attacks cannot be swap-parried either");
        }

        [Test]
        public void Arsenal_SixDistinctWeapons_WithCompleteData()
        {
            Assert.AreEqual(10, WeaponDefs.All.Length);
            var ids = new HashSet<string>();
            var finishers = new HashSet<string>();
            foreach (WeaponDef w in WeaponDefs.All)
            {
                Assert.IsTrue(ids.Add(w.id), "Duplicate weapon id " + w.id);
                Assert.IsTrue(finishers.Add(w.finisherName), w.id + " must have its own finisher");
                Assert.IsFalse(string.IsNullOrEmpty(w.finisherName), w.id);
                Assert.Greater(w.combo.Length, 0, w.id);
                Assert.IsNotNull(w.swapStrike, w.id);
                Assert.IsFalse(string.IsNullOrEmpty(w.swapStrike.name), w.id + " swap strike needs a name");
                foreach (PlayerAttackDef step in w.combo)
                {
                    Assert.Greater(step.windup, 0, w.id);
                    Assert.Greater(step.active, 0, w.id);
                    Assert.GreaterOrEqual(step.hits, 1, w.id);
                    Assert.Greater(step.damage, 0f, w.id);
                }
            }
            Assert.AreSame(WeaponDefs.Katana, WeaponDefs.Get("nao_existe"), "Unknown ids fall back to the katana");
        }

        [Test]
        public void Rarity_IsRareAtFirst_AndRisesWithDepthAndGuardians()
        {
            var rng = new SeededRandom(11);
            float shallow = 0f, deep = 0f, guardian = 0f;
            const int samples = 4000;
            for (int i = 0; i < samples; i++)
            {
                shallow += (int)ItemGenerator.RollRarity(rng, 0, false);
                deep += (int)ItemGenerator.RollRarity(rng, 25, false);
                guardian += (int)ItemGenerator.RollRarity(rng, 25, true);
            }
            Assert.Less(shallow / samples, 1.5f, "Early loot is mostly common");
            Assert.Greater(deep, shallow);
            Assert.Greater(guardian, deep);
        }

        [Test]
        public void Items_HaveRarityAffixCounts_AndTenRarities()
        {
            Assert.AreEqual(10, Rarities.Count);
            var rng = new SeededRandom(5);
            for (int i = 0; i < 300; i++)
            {
                Item item = ItemGenerator.Roll(rng, i % 40, i % 7 == 0);
                Assert.AreEqual(Rarities.AffixCount(item.rarity), item.affixes.Count, item.Name);
                Assert.IsFalse(string.IsNullOrEmpty(item.Gain), item.Name);
                var stats = new HashSet<AffixStat>();
                foreach (Affix a in item.affixes) Assert.IsTrue(stats.Add(a.stat), item.Name + " repeats an affix");
            }
        }

        [Test]
        public void Items_EquipFromBag_SwapsAndRecomputes()
        {
            var run = new RunState(1, new FlowTuning());
            var first = new Item(ItemSlot.Ring, Rarity.Common, Element.None, "Anel");
            first.affixes.Add(new Affix { stat = AffixStat.Damage, value = 0.1f });
            var second = new Item(ItemSlot.Ring, Rarity.Rare, Element.None, "Aro");
            second.affixes.Add(new Affix { stat = AffixStat.Crit, value = 0.2f });

            Assert.IsTrue(run.AddItem(first), "Empty slot: worn at once");
            Assert.AreEqual(1.1f, run.Modifiers.damageMult, 1e-3f);
            Assert.IsFalse(run.AddItem(second), "Occupied slot: goes to the bag");
            Assert.AreEqual(0f, run.Modifiers.critChance, 1e-3f);

            run.Equip(second);
            Assert.AreSame(second, run.Equipped[(int)ItemSlot.Ring]);
            Assert.Contains(first, run.Bag);
            Assert.AreEqual(1f, run.Modifiers.damageMult, 1e-3f);
            Assert.AreEqual(0.2f, run.Modifiers.critChance, 1e-3f);
            Assert.Contains(second, run.Effects, "Worn items take part in combat hooks");
        }

        [Test]
        public void Bag_NeverExceedsCapacity()
        {
            var run = new RunState(1, new FlowTuning());
            for (int i = 0; i < 30; i++) run.AddItem(new Item(ItemSlot.Amulet, Rarity.Common, Element.Fire, "Amuleto"));
            Assert.LessOrEqual(run.Bag.Count, RunState.BagCapacity);
        }
    }
}
