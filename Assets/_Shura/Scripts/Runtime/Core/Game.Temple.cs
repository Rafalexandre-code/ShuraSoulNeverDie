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

        public void OpenTemple()
        {
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
            Meta.runs = Mathf.Max(Meta.runs, 7);
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

            Run.Oaths = OathSet.From(Meta);
            Run.EcosMult *= Oaths.EcosMult(Run.Oaths.Heat);
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
            if (d.traits.Contains("hattori"))
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

        void BankLegacy(bool died)
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
            LastLegacy = Legacy.Bank(Meta, Run, facts, died);
        }
    }
}
