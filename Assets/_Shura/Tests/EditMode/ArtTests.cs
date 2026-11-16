using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Shura.Tests
{
    /// <summary>WP-05: a Arte (um encaixe, 2 cargas) e o esquema de 7 teclas de combate.</summary>
    public class ArtTests
    {
        [TearDown]
        public void Restore() => Feature.Reset();

        [Test]
        public void TheSevenArts_HaveNamesRulesAndOrigins_AndIdsRoundTrip()
        {
            Assert.AreEqual(7, Arts.All.Length);
            var names = new HashSet<string>();
            foreach (ArtId a in Arts.All)
            {
                Assert.IsTrue(names.Add(Arts.Name(a)));
                Assert.IsNotEmpty(Arts.Rule(a));
                Assert.IsNotEmpty(Arts.Origin(a));
                Assert.AreEqual(a, Arts.Parse(Arts.Id(a)));
            }
            Assert.AreEqual(2, Arts.MaxCharges);
        }

        [Test]
        public void KiaiAndShuriken_AreOpenFromTheStart_TheRestComeFromTheTempleOrACard()
        {
            var meta = new MetaProgress();
            List<ArtId> open = Arts.Choosable(meta);
            CollectionAssert.AreEquivalent(new[] { ArtId.Kiai, ArtId.Shuriken }, open);
            meta.buildings.Add(Temple.Dojo);
            meta.buildings.Add(Temple.Library);
            CollectionAssert.AreEquivalent(new[] { ArtId.Kiai, ArtId.Shuriken, ArtId.Smoke, ArtId.Bell, ArtId.Chain }, Arts.Choosable(meta));
            Assert.IsTrue(Arts.FromCardOnly(ArtId.Rupture));
            Assert.IsTrue(Arts.FromCardOnly(ArtId.Soul));
        }

        [Test]
        public void AnArtCard_SwapsTheSlot_AndRefillsTheCharges_WithoutBecomingARelic()
        {
            var run = new RunState(7, new FlowTuning());
            PlayerBuild b = run.Main;
            b.ArtCharges = 0;
            int owned = b.Owned.Count;
            RewardDef card = RewardRegistry.Create("arte_alma:onryo");
            Assert.IsInstanceOf<ArtCard>(card);
            b.Acquire(card);
            Assert.AreEqual(ArtId.Soul, b.Art);
            Assert.AreEqual("onryo", b.SoulArtEnemy);
            Assert.AreEqual(Arts.MaxCharges, b.ArtCharges);
            Assert.AreEqual(owned, b.Owned.Count);
        }

        [Test]
        public void TheInnerSeal_BringsARuptureCard_ToTheNextRewards()
        {
            var run = new RunState(11, new FlowTuning());
            PlayerBuild b = run.Main;
            b.Acquire(RewardRegistry.Create(RewardIds.InnerSeal));
            Assert.AreEqual(1, b.PendingArtCards.Count);
            List<RewardDef> choices = b.RollChoices(3, 3);
            bool rupture = false;
            foreach (RewardDef r in choices) rupture |= r is ArtCard c && c.Art == ArtId.Rupture;
            Assert.IsTrue(rupture, "a carta da Ruptura entra na tela seguinte");
            Assert.AreEqual(0, b.PendingArtCards.Count);
        }

        [Test]
        public void TheCombatScheme_HasSevenActions_PlusTheLock()
        {
            var combat = new List<ControlAction>(InputService.NewScheme);
            combat.Remove(ControlAction.LockOn);
            combat.Remove(ControlAction.LockNext);
            combat.Remove(ControlAction.LockPrev);
            CollectionAssert.AreEquivalent(new[]
            {
                ControlAction.Attack, ControlAction.Parry, ControlAction.Dodge, ControlAction.Swap,
                ControlAction.Gourd, ControlAction.Tool, ControlAction.Interact,
            }, combat);
        }

        // Teclas que saíram no WP-05: G (detonar), V (kiai), B (alma), H (bandagem), T (2ª ferramenta), 1–4 (guardas).
        static readonly Regex RemovedKeys = new Regex(@"(?<![\p{L}\p{N}])[GVBHT](?![\p{L}\p{N}])|1[–-]4");

        [Test]
        public void NoKeyboardText_CitesTheRemovedKeys()
        {
            foreach (HintId h in System.Enum.GetValues(typeof(HintId)))
            {
                string text = Strings.HintText(h, false);
                if (text == null) continue;
                Assert.IsFalse(RemovedKeys.IsMatch(text), h + ": " + text);
            }
            foreach (Tutorial.Step s in System.Enum.GetValues(typeof(Tutorial.Step)))
            {
                if (!Tutorial.Teaches(s)) continue;
                string text = Strings.TutorialText(s, false, false);
                Assert.IsFalse(RemovedKeys.IsMatch(text), s + ": " + text);
            }
            foreach (string t in new[] { Strings.ReviveHold, Strings.CourtHint, Strings.HudSoulReady })
                Assert.IsFalse(RemovedKeys.IsMatch(t), t);
        }
    }
}
