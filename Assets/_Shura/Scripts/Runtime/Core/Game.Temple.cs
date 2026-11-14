using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Phase 4 side of the game (design doc §7): the ruined temple between runs, the oaths
    /// carried into a journey, the lineage of disciples and what each journey leaves behind.
    /// </summary>
    public sealed partial class Game
    {
        Mode templeReturnMode = Mode.MainMenu;
        bool arsenalForTemple;
        Action afterHeir;

        /// <summary>What the last banked journey left (currencies, Karma, the disciple's fate).</summary>
        public LegacyReport LastLegacy { get; private set; }

        // ── The temple ─────────────────────────────────────────────────────────────────

        /// <summary>The temple opens with the fourth run (§7.7), or at once for whoever opened everything.</summary>
        public bool TempleOpen => Settings.unlockAll || Launch.Automated || Unlocks.Open(Meta.runs, Feature.Temple);

        public void OpenTemple()
        {
            if (!TempleOpen)
            {
                Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                UI.Toast(string.Format(Strings.TempleLocked, Unlocks.RunsFor(Feature.Temple)), Config.palette.uiText);
                return;
            }
            if (CurrentMode != Mode.Temple && CurrentMode != Mode.Altar && CurrentMode != Mode.Arsenal && CurrentMode != Mode.Heir)
                templeReturnMode = CurrentMode;
            SetMode(Mode.Temple);
        }

        public void CloseTemple()
        {
            SetMode(templeReturnMode == Mode.GameOver && Run == null ? Mode.MainMenu : templeReturnMode);
        }

        bool Spend(bool ok, string toast, Color color)
        {
            if (ok)
            {
                SaveMeta();
                Audio.Play(Sfx.Purchase, 0.8f, 1f, 0f);
                UI.Toast(toast, color);
                UI.Flash(color, 0.12f);
                return true;
            }
            Audio.Play(Sfx.Denied, 0.7f, 1f, 0f);
            return false;
        }

        public bool TryBuild(BuildingDef b)
        {
            bool ok = Temple.TryBuild(Meta, b);
            if (!ok && !b.comingSoon && !Temple.IsBuilt(Meta, b)) UI.Toast(Strings.TempleCannotBuild, Config.palette.danger);
            return Spend(ok, string.Format(Strings.TempleBuilt, b.name), Config.palette.gold);
        }

        public bool TryOpenNode(AltarBranch b)
        {
            bool ok = AltarTree.TryOpen(Meta, b);
            if (!ok && !Meta.HasNode(b.node.id)) UI.Toast(Strings.AltarNodeLocked, Config.palette.danger);
            return Spend(ok, string.Format(Strings.AltarNodeOpened, b.node.name), b.color);
        }

        /// <summary>A guest's mission was fulfilled: the reward is theirs to give.</summary>
        public bool ClaimMission(GuestDef g)
        {
            bool ok = TeaGuests.TryClaim(Meta, g);
            if (!ok) return false;
            return Spend(true, string.Format(Strings.TeaClaimed, g.name), g.color);
        }

        public void ResetAltar()
        {
            Meta.ResetAltar();
            SaveMeta();
            Audio.Play(Sfx.UiConfirm, 0.7f, 0.8f, 0f);
            UI.Toast(Strings.AltarReset, Config.palette.gold);
        }

        public void SetOath(OathDef o, int level)
        {
            if (o.sealedUntilRivals)
            {
                Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                return;
            }
            level = Mathf.Clamp(level, 0, o.maxLevel);
            if (level == Meta.OathLevel(o.id)) return;
            Meta.SetOath(o.id, level);
            SaveMeta();
            Audio.Play(level > 0 ? Sfx.Unsheathe : Sfx.UiMove, 0.5f, 0.8f + 0.1f * level, 0f);
        }

        public bool TryBuyColor(BladeColorDef c)
        {
            if (Meta.colors.Contains(c.id))
            {
                // Owned: wear it, or take it off.
                Meta.bladeColor = Meta.bladeColor == c.id ? "" : c.id;
                SaveMeta();
                Run?.RecomputeModifiers();
                Audio.Play(Sfx.Unsheathe, 0.5f, 1.2f, 0f);
                return true;
            }
            return Spend(Garden.TryBuyColor(Meta, c), string.Format(Strings.GardenBought, c.name), c.color);
        }

        public bool TryBuyOrnament(OrnamentDef o)
        {
            if (Meta.garden.Contains(o.id)) return false;
            return Spend(Garden.TryBuyOrnament(Meta, o), string.Format(Strings.GardenBought, o.name), Config.palette.gold);
        }

        public void SetGardenSeed(string seed)
        {
            Meta.seed = (seed ?? "").Trim();
            if (Meta.seed.Length > 24) Meta.seed = Meta.seed.Substring(0, 24);
            SaveMeta();
        }

        /// <summary>The seed typed in the garden: a number as it is, any other text hashed.</summary>
        ulong? GardenSeed()
        {
            if (Launch.Automated || string.IsNullOrEmpty(Meta.seed)) return null;
            if (ulong.TryParse(Meta.seed, out ulong n)) return n;
            ulong h = 1469598103934665603UL;
            foreach (char c in Meta.seed)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>Screenshot tour only: a temple with everything rebuilt, oaths sworn, two tombs.</summary>
        public void DevPrepareTempleShots()
        {
            if (!Launch.Automated) return;
            Meta.ecos += 2000;
            Meta.steel += 12;
            Meta.fragments += 6;
            Meta.petals += 40;
            Meta.runs = Mathf.Max(Meta.runs, 12);
            foreach (BuildingDef b in Temple.All)
            {
                if (!b.comingSoon && !Meta.Built(b.id)) Meta.buildings.Add(b.id);
            }
            Meta.SetOath(Oaths.Hunger, 1);
            Meta.SetOath(Oaths.SilentRing, 2);
            Meta.colors.Add("sakura");
            Meta.bladeColor = "sakura";
            Meta.garden.Add("bonsai");
            Meta.garden.Add("lanterna");
            Meta.AddKarma(25);
            // The Tea House: one mission ready, one done; a known enemy with a finished scroll.
            Meta.AddStat(TeaGuests.StatFoxes, 2);
            Meta.AddStat(TeaGuests.StatParries, 90);
            if (!Meta.missions.Contains(TeaGuests.Warashi)) Meta.missions.Add(TeaGuests.Warashi);
            if (Meta.bestiary.Count > 0)
            {
                EnemyScroll s = Meta.Scroll(Meta.bestiary[0].id, true);
                s.signatureParries = 30;
                s.weapons.AddRange(new[] { "katana", "naginata", "tessen" });
                s.cleanKills = 1;
                s.masterAnswers = 1;
                s.done = true;
            }
            GameModes.RecordDaily(Meta, DateTime.Now.AddDays(-1), 12, 640);
            // Two rivals on the wall: a General and a captain who took Ecos.
            if (Meta.rivals.Count == 0)
            {
                Rival g = Rivals.OnPlayerKilled(Meta, EnemyDefs.Get("kenkaku"), null, 0, "Haru", "Katana", null, 1, 120, 11);
                if (g != null)
                {
                    g.rank = Rivals.GeneralRank;
                    g.name = g.surname + ", General " + Biomes.All[0].suffix;
                    g.kills = 4;
                }
                Rivals.OnPlayerKilled(Meta, EnemyDefs.Get("ronin"), null, 1, "Ren", "Naginata", "Lâmina Paciente", 2, 60, 29);
            }
            GameModes.RecordDaily(Meta, DateTime.Now.AddDays(-2), 7, 310);
            Meta.cemetery.Insert(0, new Disciple
            {
                name = "Kenji", age = 72, generation = 1, retired = true, journeys = 10, bestRoom = 31, weapon = "naginata",
                traits = new System.Collections.Generic.List<string> { "paciente" },
            });
        }

        // ── The lineage ────────────────────────────────────────────────────────────────

        /// <summary>A disciple fell: the next body is chosen first, then the way goes on.</summary>
        public void OpenHeir(Mode next)
        {
            if (next == Mode.Arsenal) afterHeir = OpenArsenal;
            else if (next == Mode.Playing) afterHeir = StartRun;
            else afterHeir = () => SetMode(next);
            SetMode(Mode.Heir);
        }

        public void ChooseHeir(int index)
        {
            if (!Legacy.ChooseHeir(Meta, index)) return;
            SaveMeta();
            Audio.Play(Sfx.UiConfirm, 0.8f, 0.9f, 0f);
            UI.Toast(string.Format(Strings.HeirChosen, Meta.disciple.name), Config.palette.gold);
            Action next = afterHeir ?? GoToMainMenu;
            afterHeir = null;
            next();
        }

        /// <summary>"Nova run" after a death: the heir comes first.</summary>
        public void NewRunFromEnd()
        {
            if (Meta.heirs.Count > 0) OpenHeir(Mode.Playing);
            else StartRun();
        }

        /// <summary>Oaths, the disciple's body and inheritance, the third weapon: before the first room.</summary>
        void PrepareJourney()
        {
            // An heir nobody chose (the robot, a restart from the pause menu): the first one walks on.
            if (Meta.heirs.Count > 0) Legacy.ChooseHeir(Meta, 0);
            if (Meta.disciple == null) Meta.disciple = Lineage.First();
            Disciple d = Meta.disciple;
            PlayerBuild b = Run.Main;

            Run.UnlockRuns = Settings.unlockAll || Launch.Automated ? int.MaxValue : Meta.runs;
            // The road's events lean with the soul's Karma; Tsubaki's friends meet more honour duels.
            KarmaRules.RoadKarma = Meta.karma;
            KarmaRules.HonorDuels = Meta.MissionDone(TeaGuests.Tsubaki);
            World.ShowTimingIndicator = Settings.timingIndicator && Run.Mode != RunMode.Master;
            Run.Oaths = Run.Has(Feature.Oaths) ? OathSet.From(Meta) : new OathSet();
            // The rivals the route map can draw lairs for; the reading the Shura remembers.
            Rivals.Roster.Clear();
            if (Run.Has(Feature.Rivals) && Run.Mode != RunMode.Tower) Rivals.Roster.AddRange(Meta.rivals);
            if (Settings.shuraRemembers && !Launch.Automated) Run.Profile.Baseline = ReadingMemory.Average(Meta.readings);
            Run.EcosMult *= Oaths.EcosMult(Run.Oaths.Heat) * GameModes.EcosMult(Run.Mode);
            World.ExtraEliteAffixes = Run.Oaths.Level(Oaths.DemonBell);
            if (Run.Oaths.Level(Oaths.SingleBlade) > 0) b.Weapons[1] = b.Weapons[0];
            else if (Meta.HasNode(AltarTree.SecondSwap)) b.Reserve = ThirdWeapon(b);
            if (Run.Oaths.Level(Oaths.EarlyKalpa) > 0)
            {
                Run.Kalpa = 2;
                var offer = KalpaRules.Offer(Run, new SeededRandom(Run.Seed ^ 0x4B414C5041UL));
                if (offer.Count > 0) Run.KalpaRules.Add(offer[0]);
            }

            // The predecessor's best technique lives on in the heir.
            if (!string.IsNullOrEmpty(d.technique))
            {
                RewardDef t = RewardRegistry.Create(d.technique);
                if (t != null) b.Acquire(t);
            }
            if (d.traits.Contains("hattori") && Run.Mode != RunMode.Tower)
            {
                var rng = new SeededRandom(Run.Seed ^ 0x48415454UL);
                for (int s = 0; s < 2; s++)
                {
                    Item item = ItemGenerator.RollAt(rng, Rarity.Uncommon, (ItemSlot)s);
                    item.clan = Clan.Hattori;
                    b.AddItem(item);
                }
            }
            Run.RecomputeModifiers();
        }

        /// <summary>The third blade chosen in the arsenal (if it differs from the two in hand).</summary>
        WeaponDef ThirdWeapon(PlayerBuild b)
        {
            string id = Settings.weaponC;
            if (string.IsNullOrEmpty(id) || WeaponDefs.Get(id) == null) return null;
            WeaponDef w = WeaponDefs.Get(Settings.Loadout(id));
            if (w == null || w.id == b.Weapons[0]?.id || w.id == b.Weapons[1]?.id) return null;
            return w;
        }

        void BankLegacy(bool died, bool victory = false)
        {
            LastLegacy = null;
            if (Run == null || World == null) return;
            RoomDef room = World.CurrentRoom;
            var facts = new Lineage.Facts
            {
                biome = room?.biome?.name,
                time = World.TimeOfDay,
                weather = room != null ? room.weather : Weather.Clear,
                killer = World.Host != null ? World.Host.LastKillerName : null,
                weapon = Run.CurrentWeapon?.name,
                depth = Run.RoomIndex,
                karma = Run.Karma,
                seed = Run.Seed,
            };
            LastLegacy = Legacy.Bank(Meta, Run, facts, died, Run.Has(Feature.Lineage));
            BankRival(died);
            Meta.readings.Add(ReadingMemory.Of(World.Adaptation));
            while (Meta.readings.Count > ReadingMemory.Keep) Meta.readings.RemoveAt(0);
            switch (Run.Mode)
            {
                case RunMode.Daily:
                    GameModes.RecordDaily(Meta, DateTime.Now, Run.RoomsCleared, Run.EcosBanked);
                    break;
                case RunMode.Tower:
                    Meta.towerBest = Mathf.Max(Meta.towerBest, Run.RoomsCleared);
                    if (victory) LastLegacy.ending = Run.PendingEnding ?? "torre";
                    break;
                case RunMode.Pilgrimage:
                    Meta.pilgrimageBest = Mathf.Max(Meta.pilgrimageBest, Run.RoomsCleared);
                    if (victory) LastLegacy.ending = Run.PendingEnding ?? GameModes.EndingFor(KarmaRules.Of(Run));
                    break;
                default:
                    if (victory && !string.IsNullOrEmpty(Run.PendingEnding)) LastLegacy.ending = Run.PendingEnding;
                    break;
            }
            if (!string.IsNullOrEmpty(LastLegacy.ending) && !Meta.endings.Contains(LastLegacy.ending)) Meta.endings.Add(LastLegacy.ending);
        }

        /// <summary>
        /// Whoever killed the duelist remembers (§5.1): a new rival (or a known one rising in
        /// rank), with a fifth of the run's Ecos, a trait from the build and a scar.
        /// </summary>
        void BankRival(bool died)
        {
            if (!died || Run.Mode == RunMode.Tower || !Run.Has(Feature.Rivals) || World.Host == null) return;
            Enemy killer = World.Host.LastKillerEnemy;
            if (killer == null || killer.Def == null || killer.IsAlly || killer.AsuraOwner != null) return;
            string technique = null;
            string best = Legacy.MostUsedTechnique(Run.Main);
            if (!string.IsNullOrEmpty(best)) technique = RewardRegistry.Create(best)?.Name;
            int lost = Mathf.RoundToInt(Run.EcosBanked * Rivals.EcosTaken);
            Rival before = Rivals.Find(Meta, killer.RivalId);
            int rankBefore = before != null ? before.rank : -1;
            string victim = LastLegacy?.disciple?.name ?? "Haru";
            int biome = System.Array.IndexOf(Biomes.All, World.CurrentRoom != null ? World.CurrentRoom.biome : Biomes.All[0]);
            Rival r = Rivals.OnPlayerKilled(Meta, killer.Def, killer.RivalId, biome, victim, Run.CurrentWeapon?.name, technique,
                                            killer.WoundMask, lost, Run.Seed ^ (ulong)Run.RoomIndex);
            if (r == null) return;
            Meta.ecos = Mathf.Max(0, Meta.ecos - lost);
            LastLegacy.rival = r.name;
            LastLegacy.rivalEcos = lost;
            LastLegacy.rivalPromoted = rankBefore >= 0 && r.rank > rankBefore;
        }

        // ── The ways to walk the road (§7.8) ───────────────────────────────────────────

        RunMode pendingMode = RunMode.Journey;

        /// <summary>The mode the next run will be walked in (chosen in "Modos de jogo").</summary>
        public RunMode PendingMode => pendingMode;

        /// <summary>The Master mode is Zatō's gift; the rest are open from the start.</summary>
        public bool ModeOpen(RunMode m) => m != RunMode.Master || Meta.MissionDone(TeaGuests.Zato) || Launch.Automated;

        public void OpenModes() => SetMode(Mode.Modes);

        public void ChooseMode(RunMode m)
        {
            if (!ModeOpen(m))
            {
                Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                UI.Toast(Strings.ModeLocked, Config.palette.uiText);
                return;
            }
            if (m == RunMode.Daily && GameModes.DailyTried(Meta, DateTime.Now) && !Launch.Automated)
            {
                Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                UI.Toast(Strings.DailyDone, Config.palette.uiText);
                return;
            }
            pendingMode = m;
            if (Meta.heirs.Count > 0) OpenHeir(Mode.Arsenal);
            else OpenArsenal();
        }

        RunMode ModeForNextRun()
        {
            RunMode m = pendingMode;
            if (Launch.Automated && Launch.Mode != null && Enum.TryParse(Launch.Mode, true, out RunMode forced)) m = forced;
            // One try a day: a restart of the daily road becomes an ordinary journey.
            if (m == RunMode.Daily && GameModes.DailyTried(Meta, DateTime.Now) && !Launch.Automated) m = RunMode.Journey;
            if (m == RunMode.Master && !ModeOpen(m)) m = RunMode.Journey;
            return m;
        }

        ulong SeedFor(RunMode m)
        {
            if (m == RunMode.Daily) return GameModes.DailySeed(DateTime.Now);
            if (m == RunMode.Pilgrimage) return GameModes.PilgrimageSeed;
            return Launch.Seed ?? GardenSeed() ?? (ulong)DateTime.UtcNow.Ticks;
        }

        bool RitualInProgress()
        {
            foreach (PlayerController p in World.Players)
            {
                if (p.ChiburiHolding) return true;
            }
            return false;
        }

        /// <summary>The road was walked to its end.</summary>
        void Victory()
        {
            BankRun(false, true);
            SetMode(Mode.Victory);
        }
    }
}
