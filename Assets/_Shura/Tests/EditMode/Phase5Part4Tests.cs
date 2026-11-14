using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shura.Tests
{
    /// <summary>Phase 5, part 4: playing together (§9.2–9.5, 9.7).</summary>
    public class Phase5Part4Tests
    {
        static MetaProgress Fresh()
        {
            MetaProgress m = MetaProgress.FromJson(new MetaProgress().ToJson());
            Assert.IsNotNull(m.presence);
            return m;
        }

        static List<Vector2> Steps(int n)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < n; i++) list.Add(new Vector2(i * 0.5f, 1f));
            return list;
        }

        [Test]
        public void ADeath_LeavesAStainWithFiveSecondsOfSteps_AndAMessageTheDeathSuggests()
        {
            MetaProgress m = Fresh();
            InkMessage msg = Presence.RecordDeath(m, "Haru", 7, new Vector2(2f, 3f), "carrasco:corrente sala=Combat", 1, Steps(80));
            Assert.AreEqual(1, m.presence.myStains.Count);
            DeathStain s = m.presence.myStains[0];
            Assert.AreEqual(Presence.TrailPoints * 2, s.trail.Count, "only the last five seconds");
            Assert.AreEqual(7, s.depth);
            Assert.AreEqual("cuidado: a corrente", msg.Text);
            Assert.AreEqual("o chão", Presence.Words[Presence.WordFor("trap sala=Combat")]);
            for (int i = 0; i < 10; i++) Presence.RecordDeath(m, "Haru", i, Vector2.zero, "", 0, Steps(3));
            Assert.AreEqual(Presence.MaxStains, m.presence.myStains.Count);
            Assert.AreEqual(Presence.MaxMessages, m.presence.mine.Count);
        }

        [Test]
        public void APacket_TravelsAsJson_AndLeavesTheFriendsPresenceAtTheSameDepths()
        {
            MetaProgress haru = Fresh(), kenji = Fresh();
            InkMessage msg = Presence.RecordDeath(haru, "Haru", 12, new Vector2(1f, 1f), "arqueiro:flecha", 2, Steps(40));
            haru.presence.spirit = new SpiritProfile { name = "Haru", weaponA = "naginata", weaponB = "katana", art = 2, aggression = 0.9f };
            string json = Presence.ToJson(Presence.Pack(haru, "Haru"));
            PresencePacket pk = Presence.FromJson(json);
            Assert.IsNotNull(pk);

            Presence.Merge(kenji, pk, "Kenji");
            Assert.AreEqual(1, kenji.presence.friends.Count);
            Assert.AreEqual(1, Presence.MessagesAt(kenji, 12).Count);
            Assert.AreEqual(0, Presence.MessagesAt(kenji, 11).Count);
            Assert.AreEqual(1, Presence.StainsAt(kenji, 12, "Kenji").Count);
            Assert.AreEqual("naginata", Presence.SpiritFor(kenji, 5).weaponA);

            // Kenji approves it; the rating reaches Haru once, however often they meet.
            Presence.Rate(kenji, Presence.MessagesAt(kenji, 12)[0]);
            PresencePacket back = Presence.FromJson(Presence.ToJson(Presence.Pack(kenji, "Kenji")));
            Assert.AreEqual(1, Presence.Merge(haru, back, "Haru"));
            Assert.AreEqual(0, Presence.Merge(haru, back, "Haru"), "counted once");
            Assert.AreEqual(1, msg.rating);
            Assert.AreEqual(1, haru.presence.approvals);

            // A packet of your own (same name) is ignored; meeting again replaces the old one.
            Assert.AreEqual(0, Presence.Merge(kenji, Presence.Pack(kenji, "Kenji"), "Kenji"));
            Presence.Merge(kenji, pk, "Kenji");
            Assert.AreEqual(1, kenji.presence.friends.Count);
        }

        [Test]
        public void AFriendsRivals_MoveIn_AsCaptainsThatRememberWhoseTheyWere()
        {
            MetaProgress haru = Fresh(), kenji = Fresh();
            haru.rivals.Add(new Rival { id = "r1", enemyId = "ronin", name = "Kuroda", rank = Rivals.GeneralRank, ecos = 900, biome = 1 });
            haru.rivals.Add(new Rival { id = "r2", enemyId = "lanceiro", name = "Sato", rank = 1, ecos = 40 });
            haru.rivals.Add(new Rival { id = "r3", enemyId = "lanceiro", name = "Ito", rank = 0, ecos = 10 });
            Presence.Merge(kenji, Presence.FromJson(Presence.ToJson(Presence.Pack(haru, "Haru"))), "Kenji");
            Assert.AreEqual(Presence.SharedRivals, kenji.rivals.Count, "the two strongest");
            Rival k = Rivals.Find(kenji, "r1");
            Assert.AreEqual("Haru", k.foreign);
            Assert.IsFalse(k.General, "a lent General serves as a captain");
            Assert.LessOrEqual(k.ecos, 150);
            Assert.IsNull(Rivals.Find(kenji, "r3"));
            // Lent rivals are never lent on.
            MetaProgress third = Fresh();
            Presence.Merge(third, Presence.FromJson(Presence.ToJson(Presence.Pack(kenji, "Kenji"))), "Mei");
            Assert.AreEqual(0, third.rivals.Count);
        }

        [Test]
        public void TheNewAshura_ReignsForAWeek()
        {
            MetaProgress kenji = Fresh();
            var pk = new PresencePacket { author = "Haru", ashura = new AshuraMark { name = "Haru", weapon = "kanabo", since = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc).Ticks } };
            Presence.Merge(kenji, pk, "Kenji");
            Assert.IsNotNull(Presence.NewAshura(kenji, new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc)));
            Assert.IsNull(Presence.NewAshura(kenji, new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc)), "a week later the throne is empty again");
            EnemyDef combo = EnemyDefs.InnerAsura(WeaponDefs.Get("kanabo"), 0, "Haru");
            Assert.IsTrue(combo.moves.Length > 0);
        }

        [Test]
        public void AFriendsSpirit_FightsWithTheirBlade_AndTheirPace()
        {
            EnemyDef bold = World.SpiritDef(new SpiritProfile { name = "Haru", weaponA = "katana", aggression = 1f });
            EnemyDef calm = World.SpiritDef(new SpiritProfile { name = "Mei", weaponA = "tessen", aggression = 0f });
            Assert.AreEqual("Espírito de Haru", bold.displayName);
            Assert.Less(bold.idleMin, calm.idleMin, "the relentless wait less between blows");
            Assert.AreNotSame(EnemyDefs.InnerAsura(WeaponDefs.Katana, 0, "x"), bold);
        }

        [Test]
        public void Roles_GrowFromTheBlades()
        {
            Assert.AreEqual(PartyRole.Vanguard, World.RoleFor(new[] { WeaponDefs.Get("montante"), WeaponDefs.Katana }));
            Assert.AreEqual(PartyRole.Guardian, World.RoleFor(new[] { WeaponDefs.Get("tessen"), null }));
            Assert.AreEqual(PartyRole.Hunter, World.RoleFor(new[] { WeaponDefs.Katana, WeaponDefs.Get("wakizashi") }));
            Assert.AreEqual(PartyRole.Exorcist, World.RoleFor(new[] { WeaponDefs.Get("shakujo"), WeaponDefs.Katana }));
            Assert.AreEqual(PartyRole.None, World.RoleFor(new[] { WeaponDefs.Katana, WeaponDefs.Get("naginata") }));
            foreach (PartyRole r in new[] { PartyRole.Vanguard, PartyRole.Guardian, PartyRole.Hunter, PartyRole.Exorcist })
                Assert.IsNotEmpty(Strings.RoleName(r));
        }

        [Test]
        public void ThePactsPool_AndTheGroupsOath()
        {
            var f = new FlowMeter(new[] { 10, 20, 30 }, 40f);
            f.Set(55f);
            Assert.AreEqual(40f, f.Points, 1e-4f);
            f.Set(-3f);
            Assert.AreEqual(0f, f.Points, 1e-4f);

            List<string> oaths = NetSession.OathChoices();
            Assert.AreEqual("", oaths[0], "voting for nothing is a choice");
            Assert.Greater(oaths.Count, 5);
            Assert.AreEqual(Strings.GroupOathNone, NetSession.OathLabel("", 0));
            Assert.AreEqual(Oaths.Get(oaths[1]).name + " (2 votos)", NetSession.OathLabel(oaths[1], 2));
        }

        [Test]
        public void ThePresence_SurvivesTheSave()
        {
            MetaProgress m = Fresh();
            Presence.RecordDeath(m, "Haru", 3, Vector2.one, "", 0, Steps(10));
            m.presence.friends.Add(new PresencePacket { author = "Kenji" });
            MetaProgress back = MetaProgress.FromJson(m.ToJson());
            Assert.AreEqual(1, back.presence.myStains.Count);
            Assert.AreEqual(20, back.presence.myStains[0].trail.Count);
            Assert.AreEqual("Kenji", back.presence.friends[0].author);
        }
    }
}
