using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>WP-04: quatro anéis (a tríade + a finta), o roxo da Troca Perfeita, os 10 Elos e a IA que não pune o gancho.</summary>
    public class RingsAndLinksTests
    {
        [TearDown]
        public void Restore() => Feature.Reset();

        static AttackDef Cursed() => new AttackDef { kind = AttackKind.Normal, cursed = true, windup = 20, active = 4, recovery = 20, shape = HitShape.Arc(1.6f, 110f) };

        // ── Os quatro anéis ──────────────────────────────────────────────────────────

        [Test]
        public void TheSoloGame_HasFourRings_EachWithItsOwnPatternColorSoundAndAnswer()
        {
            Assert.AreEqual(4, Rings.Core.Length);
            var patterns = new HashSet<Sprite>();
            var sounds = new HashSet<string>();
            var cues = new HashSet<string>();
            var answers = new HashSet<string>();
            foreach (RingKind k in Rings.Core)
            {
                patterns.Add(ShapeLibrary.RingPattern((int)k));
                RingSound s = Rings.Sound(k);
                sounds.Add(s.id);
                cues.Add(s.sfx + "@" + s.pitch);
                answers.Add(Rings.Answer(k));
            }
            Assert.AreEqual(4, patterns.Count, "forma");
            Assert.AreEqual(4, sounds.Count, "id de som");
            Assert.AreEqual(4, cues.Count, "o som provisório também é distinto");
            Assert.AreEqual(4, answers.Count, "cada anel pede uma resposta");
        }

        [Test]
        public void ACursedBlow_ShowsCyan_AndBlockingDoesNotCurse_UnlessTheGreenRingIsBack()
        {
            Assert.AreEqual(RingKind.Parry, Rings.Of(Cursed(), false));
            Assert.IsFalse(Rings.Curses(Cursed()));
            Feature.Set("CursedRing", true);
            Assert.AreEqual(RingKind.Cursed, Rings.Of(Cursed(), false));
            Assert.IsTrue(Rings.Curses(Cursed()));
        }

        [Test]
        public void AHeldBlow_IsJustACyanRingThatClosesLater_UnlessTheDoubleTypeIsBack()
        {
            var a = new AttackDef { kind = AttackKind.Normal, windup = 20, active = 4, recovery = 20, shape = HitShape.Arc(1.6f, 110f) };
            Assert.AreEqual(RingKind.Parry, Rings.Of(a, true));
            Feature.Set("DoubleRingType", true);
            Assert.AreEqual(RingKind.Double, Rings.Of(a, true));
        }

        [Test]
        public void ThePurpleRing_IsTheSwap_ParryGoesThrough_DodgeOnlyAvoids_NoTalisman()
        {
            var spirit = new AttackDef { kind = AttackKind.Spirit, windup = 20, active = 4, recovery = 20, shape = HitShape.Arc(1.6f, 110f) };
            Assert.AreEqual(RingKind.Spirit, Rings.Of(spirit, false));
            var d = new DefenderState { facing = Vector2.right, toAttacker = Vector2.right, parryArcDegrees = 120f, counterDot = 0.5f };
            d.swapWindow = true;
            Assert.AreEqual(HitOutcome.SwapParry, HitResolver.Resolve(spirit, d));
            d.swapWindow = false;
            d.parryWindow = true;
            d.guarding = true;
            Assert.AreEqual(HitOutcome.Hit, HitResolver.Resolve(spirit, d), "aparar atravessa a lâmina");
            d.parryWindow = d.guarding = false;
            d.dodgeIFrames = true;
            Assert.AreEqual(HitOutcome.Evaded, HitResolver.Resolve(spirit, d), "esquiva evita sem ganho");
            Assert.IsFalse(Feature.LegacySpiritAnswers, "a exceção do Talismã saiu");
        }

        // ── Distribuição por ato ─────────────────────────────────────────────────────

        static IEnumerable<AttackDef> AttacksOf(EnemyDef def)
        {
            foreach (MoveDef m in def.moves)
            {
                foreach (AttackDef a in m.steps) yield return a;
            }
            if (!string.IsNullOrEmpty(def.summonId) && EnemyDefs.TryGet(def.summonId, out EnemyDef minion))
            {
                foreach (MoveDef m in minion.moves)
                {
                    foreach (AttackDef a in m.steps) yield return a;
                }
            }
        }

        [Test]
        public void NoEnemyOfActOne_UsesPurpleOrFeint()
        {
            int actOne = 0;
            foreach (EnemyDef def in EnemyDefs.All)
            {
                if (RoomGenerator.UnlockDepth(def.id) >= Biomes.RoomsPerBiome) continue;
                actOne++;
                foreach (AttackDef a in AttacksOf(def))
                {
                    RingKind k = Rings.Of(a, false);
                    Assert.AreNotEqual(RingKind.Spirit, k, def.id + "/" + a.id + " é roxo no Ato I");
                    Assert.AreNotEqual(RingKind.Feint, k, def.id + "/" + a.id + " é finta no Ato I");
                }
            }
            Assert.Greater(actOne, 5);
            // O Senhor do primeiro bioma e o evento da Kitsune também respeitam o Ato I.
            BossDef lord = BossDefs.ForDepth(Biomes.RoomsPerBiome - 1);
            Assert.IsNotNull(lord);
            foreach (MoveDef[] phase in lord.phases)
            {
                foreach (MoveDef m in phase)
                {
                    foreach (AttackDef a in m.steps) Assert.AreNotEqual(RingKind.Spirit, Rings.Of(a, false), lord.id + "/" + a.id);
                }
            }
            Assert.GreaterOrEqual(RunEvents.MinDepth(RunEvents.Kitsune), Biomes.RoomsPerBiome);
        }

        [Test]
        public void TheKitsuneEvent_NeverComesInActOne()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                for (int depth = 0; depth < Biomes.RoomsPerBiome; depth++)
                {
                    foreach (RouteNode n in RouteMap.Layer(seed, depth))
                    {
                        if (n.kind == RoomKind.Event) Assert.AreNotEqual(RunEvents.Kitsune, n.eventId, "semente " + seed + " prof. " + depth);
                    }
                }
            }
        }

        // ── Os 10 Elos ───────────────────────────────────────────────────────────────

        [Test]
        public void EveryWeapon_HasItsOwnLink_WithANameARuleAndADistinctEffect()
        {
            var names = new HashSet<string>();
            var effects = new HashSet<string>();
            foreach (WeaponDef w in WeaponDefs.All)
            {
                EloRule r = EloRules.For(w.id);
                Assert.AreEqual(w.id, r.weapon, "cada arma tem o seu Elo");
                Assert.IsNotEmpty(r.rule);
                Assert.IsTrue(names.Add(r.name), "nome repetido: " + r.name);
                PlayerAttackDef plain = EloRules.Build(w.swapStrike, null, "x");
                PlayerAttackDef elo = EloRules.Build(w.swapStrike, r, r.name);
                string sig = elo.damage / plain.damage + "|" + elo.posture / plain.posture + "|" + elo.shape.range / plain.shape.range + "|" +
                             (elo.hits - plain.hits) + "|" + elo.shape.type + elo.shape.arcDegrees + "|" + (elo.pull - plain.pull) + "|" + r.windup + "|" + r.burst;
                Assert.IsTrue(effects.Add(r.burst + sig), "Elo igual a outro: " + r.name);
                bool changes = elo.damage != plain.damage || elo.posture != plain.posture || elo.hits != plain.hits ||
                               elo.shape.range != plain.shape.range || elo.shape.arcDegrees != plain.shape.arcDegrees ||
                               elo.pull != plain.pull || elo.windup != plain.windup || r.burst != EloBurst.None;
                Assert.IsTrue(changes, r.name + " não muda nada");
            }
            Assert.AreEqual(10, names.Count);
        }

        [Test]
        public void TheLink_DoesNotChangeTheWeaponsOwnSwapStrike()
        {
            PlayerAttackDef s = WeaponDefs.Katana.swapStrike;
            int hits = s.hits;
            float range = s.shape.range;
            EloRules.Build(s, EloRules.For("katana"), "Fio Duplo");
            EloRules.Build(s, EloRules.For("naginata"), "Varrida");
            Assert.AreEqual(hits, s.hits);
            Assert.AreEqual(range, s.shape.range);
        }

        // ── A IA não pune a Troca Perfeita ───────────────────────────────────────────

        [Test]
        public void TheSwapReading_IsGone_UnlessTheExtendedReadsAreBack()
        {
            var profile = new PlayerProfile();
            for (int i = 0; i < 600; i++) profile.Tick(0.1f, true);
            for (int i = 0; i < 60; i++) profile.OnSwapParry();
            Assert.AreEqual(0f, profile.Traits(1f).swap);
            Feature.Set("AiReadsExtended", true);
            Assert.Greater(profile.Traits(1f).swap, 0.3f);
        }
    }
}
