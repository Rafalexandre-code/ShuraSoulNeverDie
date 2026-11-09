using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Entry point and game-flow state machine. Every system is created from here, so the
    /// scene is a single GameObject and nothing depends on hand-edited scene data.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Game : MonoBehaviour
    {
        public enum Mode
        {
            Title,
            MainMenu,
            Settings,
            Credits,
            Playing,
            Paused,
            Reward,
            GameOver,
            Altar,
            /// <summary>Weapon loadout before a run.</summary>
            Arsenal,
            /// <summary>Character, weapon wheel and worn items (from the reward screen or pause).</summary>
            Equipment,
            /// <summary>Online room: browser players join through the links shown here.</summary>
            Lobby,
            /// <summary>The route map between rooms: choose (or vote for) the next door.</summary>
            Route,
            /// <summary>Joining a friend's duel over Steam.</summary>
            SteamJoin,
        }

        const int RoomClearDelayFrames = 75;
        const int DeathDelayFrames = 80;
        const int HeadlessBotTicksPerFrame = 40;

        [Tooltip("Opcional. Crie em Create > Shura > Game Config e arraste aqui para ajustar valores no Inspector.")]
        [SerializeField] GameConfig config;

        readonly List<RewardDef> rewardChoices = new List<RewardDef>();
        int roomClearTimer = -1;
        int deathTimer = -1;
        Mode settingsReturnMode = Mode.MainMenu;
        Mode altarReturnMode = Mode.MainMenu;
        Mode equipmentReturnMode = Mode.Reward;
        string metaPath;
        float emberTimer;
        float tempo = 1f;
        float slowMoTimer;
        float slowMoScale = 1f;
        readonly HashSet<ReadingId> announcedReadings = new HashSet<ReadingId>();
        BotInput bot;
        AutoDriver autoDriver;

        public GameConfig Config { get; private set; }
        public GameSettings Settings { get; private set; }
        public InputService Controls { get; private set; }
        public SimClock Clock { get; } = new SimClock();
        public World World { get; private set; }
        public RunState Run { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public Vfx Vfx { get; private set; }
        public AudioService Audio { get; private set; }
        public GameUI UI { get; private set; }
        public FeedbackRouter Feedback { get; private set; }
        public FeedbackTap Tap { get; private set; }
        public NetSession Net { get; private set; }
        /// <summary>Online over the internet through Steam (Spacewar): hosting and joining.</summary>
        public SteamLink Steam { get; private set; }
        bool feelMuted;
        bool arsenalForLobby;

        /// <summary>While a teammate's moment plays: no hit-stop, shake, slow motion or flashes here.</summary>
        public bool FeelMuted
        {
            get => feelMuted;
            set
            {
                feelMuted = value;
                CameraRig.Muted = value;
            }
        }
        public MetaProgress Meta { get; private set; }
        public LaunchOptions Launch { get; private set; }
        public Mode CurrentMode { get; private set; }
        public IReadOnlyList<RewardDef> RewardChoices => rewardChoices;
        /// <summary>The host has picked; the room waits for teammates still choosing theirs.</summary>
        public bool WaitingForParty => CurrentMode == Mode.Reward && hostPicked;
        /// <summary>Reward cards the host still takes this room (a dispute gives two).</summary>
        public int HostPicksLeft { get; private set; } = 1;

        // ── Route map ─────────────────────────────────────────────────────────────────
        readonly List<RouteNode> routeOptions = new List<RouteNode>();
        /// <summary>Votes by voter (-1 the host, otherwise a browser player's id).</summary>
        readonly Dictionary<int, int> routeVotes = new Dictionary<int, int>();
        float routeStart;
        const float RouteSeconds = 25f;
        public IReadOnlyList<RouteNode> RouteOptions => routeOptions;
        public float RouteTimeLeft => Mathf.Max(0f, RouteSeconds - (Time.unscaledTime - routeStart));
        public int HostRouteVote => routeVotes.TryGetValue(-1, out int v) ? v : -1;

        public int RouteVotesFor(int option)
        {
            int n = 0;
            foreach (int v in routeVotes.Values)
            {
                if (v == option) n++;
            }
            return n;
        }
        /// <summary>Seconds left before teammates who have not chosen get their first card.</summary>
        public float PartyRewardTimeLeft => Mathf.Max(0f, RewardWaitSeconds - (Time.unscaledTime - rewardWaitStart));
        bool hostPicked;
        float rewardWaitStart;
        const float RewardWaitSeconds = 40f;

        /// <summary>
        /// Online with teammates, menus never stop the duel: the host's pause, equipment and
        /// settings sit over a world that keeps running (and can still hurt them).
        /// </summary>
        public bool WorldKeepsRunning =>
            Net != null && Net.Active && Run != null && World.PartySize > 1 &&
            (CurrentMode == Mode.Paused ||
             (CurrentMode == Mode.Equipment && equipmentReturnMode == Mode.Paused) ||
             (CurrentMode == Mode.Settings && settingsReturnMode == Mode.Paused));

        bool Simulating => CurrentMode == Mode.Playing || WorldKeepsRunning;
        public bool ShowHitboxes { get; set; }
        public IPlayerInputSource ActiveInput => bot != null ? (IPlayerInputSource)bot : Controls;
        /// <summary>Current game speed (base tempo raised by Flow, lowered by slow motion).</summary>
        public float EffectiveTempo => tempo * (slowMoTimer > 0f ? slowMoScale : 1f);

        int FlowLevel => Run != null ? Run.Flow.Level : 0;

        void Awake()
        {
            Launch = LaunchOptions.FromCommandLine();
            Config = config != null ? config : ScriptableObject.CreateInstance<GameConfig>();
            Settings = Launch.Automated ? new GameSettings() : GameSettings.Load();
            // Automated runs use a separate, fresh save so they never touch the player's progress.
            metaPath = MetaStore.DefaultPath(Launch.Automated);
            Meta = Launch.Automated ? new MetaProgress() : MetaStore.Load(metaPath);
            QualitySettings.vSyncCount = Launch.Headless ? 0 : 1;
            Application.runInBackground = Launch.Automated;

            Controls = new InputService();
            Controls.LoadOverrides(Settings.bindings);

            CameraRig = new GameObject("Camera").AddComponent<CameraRig>();
            CameraRig.Init(Config.feel, Settings, Config.palette.background);
            Controls.ScreenToWorld = CameraRig.ScreenToWorld;

            Vfx = new GameObject("Vfx").AddComponent<Vfx>();
            Vfx.Init();
            Audio = new GameObject("Audio").AddComponent<AudioService>();
            Audio.Init(Settings, Launch.Headless);
            UI = gameObject.AddComponent<GameUI>();
            UI.Init(this);
            gameObject.AddComponent<DebugOverlay>().Init(this);

            Feedback = new FeedbackRouter(this);
            Tap = new FeedbackTap(Feedback);
            World = new World(Config, Tap);
            Net = new NetSession(this, Tap);
            World.RoomClearedEvent += OnRoomCleared;
            World.PlayerDiedEvent += OnPlayerDied;

            Steam = new SteamLink();
            Steam.AllowSelf = Launch.SteamGuest;
            Steam.NoBrowser = Launch.NoBrowser;
            Steam.ListenLocal = Launch.Automated && Launch.Steam;
            if ((!Launch.Automated || Launch.Steam) && !Launch.Headless) Steam.Init();

            if (Launch.Bot) bot = new BotInput { EncounterLinger = Launch.ShotDir != null ? 150 : 0 };
            if (Launch.Automated) autoDriver = new AutoDriver(this);

            ApplySettings();
            ApplyHostIdentity();
            SetMode(Mode.Title);
        }

        void Update()
        {
            Controls.Poll(Config.player, CurrentMode == Mode.Playing && bot == null);

            switch (CurrentMode)
            {
                case Mode.Playing:
                    if (Controls.PausePressed && !Launch.Automated)
                    {
                        Pause();
                        break;
                    }
                    Simulate();
                    break;
                case Mode.Paused:
                    if (Controls.PausePressed) Resume();
                    break;
                case Mode.Reward:
                    if (Controls.EquipmentPressed && !Launch.Automated && !hostPicked) OpenEquipment();
                    if (hostPicked) TryFinishPartyRewards();
                    break;
                case Mode.Equipment:
                    if (Controls.EquipmentPressed) CloseEquipment();
                    break;
                case Mode.Route:
                    TryResolveRoute();
                    break;
            }

            // Online, the duel goes on behind the host's menus.
            if (WorldKeepsRunning) Simulate();

            Net.Update(Time.unscaledDeltaTime);
            Steam.Update();
            // A Steam invite accepted (or "+connect_lobby" at launch): straight to joining.
            if (Steam.TakeJoinRequest(out Steamworks.CSteamID invite))
            {
                if (CurrentMode == Mode.MainMenu || CurrentMode == Mode.Title || CurrentMode == Mode.SteamJoin)
                {
                    SetMode(Mode.SteamJoin);
                    Steam.Join(invite);
                }
            }

            autoDriver?.Update();
            UpdateMusic();
            if (!Launch.Headless && CurrentMode != Mode.Paused) SpawnEmbers(Time.unscaledDeltaTime);
        }

        void Simulate()
        {
            // Headless bots fast-forward, except when hosting (browser players live in real time).
            if (bot != null && Launch.Headless && !Net.Active)
            {
                Clock.Accumulate(SimClock.Dt * HeadlessBotTicksPerFrame, 10f);
            }
            else
            {
                // The duel breathes slower at Flow 0 and accelerates with every Flow level.
                float target = Config.feel.baseTempo + Config.feel.tempoPerFlowLevel * FlowLevel;
                tempo = Mathf.MoveTowards(tempo, target, Time.unscaledDeltaTime * 0.5f);
                if (slowMoTimer > 0f) slowMoTimer -= Time.unscaledDeltaTime;
                Clock.Accumulate(Time.unscaledDeltaTime * EffectiveTempo);
            }

            Mode startMode = CurrentMode;
            while (Clock.Step(out bool frozen))
            {
                // Frozen ticks are hit-stop: nothing moves and the input buffer does not age.
                if (frozen) continue;
                bot?.Think(World);
                World.Tick();
                ActiveInput.Buffer.Age();
                Net.AgeInputs();
                TickTimers();
                if (CurrentMode != startMode) break;
            }
        }

        void LateUpdate()
        {
            float realDt = Time.unscaledDeltaTime;
            bool frozen = Clock.HitstopFrames > 0 || !Simulating;
            World.Render(frozen ? 1f : Clock.Alpha, frozen ? 0f : realDt, ShowHitboxes);
            Vfx.Paused = CurrentMode == Mode.Paused && !WorldKeepsRunning;
            CameraRig.SetZoomOffset(Run != null ? FlowLevel * Config.feel.flowZoomPerLevel : 0f);
            // Outside a run the camera frames the whole arena behind the menus.
            PlayerController focus = World.CameraFocus;
            if (Run != null) CameraRig.Follow(focus.RenderPosition, focus.Facing, World.Bounds, realDt);
            else CameraRig.Follow(World.Bounds.center, Vector2.zero, World.Bounds, realDt);
            CameraRig.Tick(realDt);
        }

        void SetMode(Mode mode)
        {
            CurrentMode = mode;
            if (mode == Mode.Playing)
            {
                // Never catch up on time spent in menus.
                Clock.Reset();
                ActiveInput.Buffer.Clear();
            }
            UI.OnModeChanged(mode);
        }

        // ── Flow commands (called by UI and dev tools) ─────────────────────────────────

        public void GoToMainMenu()
        {
            SetMode(Mode.MainMenu);
        }

        public void StartRun()
        {
            // Restarting mid-run still banks what was earned: progress is never thrown away.
            if (Run != null && !Run.Banked) BankRun();
            ulong seed = Launch.Seed ?? (ulong)DateTime.UtcNow.Ticks;
            Run = new RunState(seed, Config.flow, Meta);
            string weaponA = Launch.Automated && Launch.WeaponA != null ? Launch.WeaponA : Settings.weaponA;
            string weaponB = Launch.Automated && Launch.WeaponB != null ? Launch.WeaponB : Settings.weaponB;
            Run.Weapons[0] = WeaponDefs.Get(weaponA);
            Run.Weapons[1] = WeaponDefs.Get(weaponB);
            if (Run.Modifiers.startFlowPoints > 0f) Run.Flow.Add(Run.Modifiers.startFlowPoints);
            roomClearTimer = -1;
            deathTimer = -1;
            rewardChoices.Clear();
            hostPicked = false;
            if (Net.Active) Net.OnRunStarted();
            World.BeginRun(Run, ActiveInput);
            int startDepth = Launch.Automated ? Mathf.Max(0, Launch.StartDepth) : 0;
            for (int i = 0; i < startDepth; i++) Run.Ascend();
            Run.RoomIndex = startDepth;
            World.Player.ApplyStats(true);
            RoomDef first = Run.GetRoom(startDepth);
            // Automated inspection: open on a chosen kind of door, event or weather.
            if (Launch.Automated && startDepth > 0 && Enum.TryParse(Launch.RoomKind ?? "", out RoomKind kind))
            {
                first = Run.Choose(new RouteNode { depth = startDepth, column = 1, kind = kind, eventId = Launch.EventId, hint = RewardHint.Relic,
                                                   challenge = kind == RoomKind.Challenge ? ChallengeKind.Timed : ChallengeKind.None });
            }
            if (Launch.Automated && Enum.TryParse(Launch.Weather ?? "", out Weather weather)) first.weather = weather;
            World.StartRoom(first, startDepth == 0);
            announcedReadings.Clear();
            tempo = Config.feel.baseTempo;
            slowMoTimer = 0f;
            Vfx.Clear();
            Vfx.ClearDecals();
            Vfx.StainBounds = World.Bounds;
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
        }

        public void OpenArsenal()
        {
            arsenalForLobby = CurrentMode == Mode.Lobby;
            SetMode(Mode.Arsenal);
        }

        // ── Online ────────────────────────────────────────────────────────────────────

        /// <summary>Opens the online room (starts the server) and shows its links.</summary>
        public void OpenLobby()
        {
            if (!Net.Active && !Net.Start(Launch.Port))
            {
                UI.Toast(string.Format(Strings.NetPortError, Launch.Port, Net.Error), Config.palette.danger);
                return;
            }
            SetMode(Mode.Lobby);
            // Over the internet too: a Steam lobby tunnels friends into this same room.
            Steam.Host(Net.Server);
        }

        public void CloseLobby()
        {
            Steam.StopHost();
            Net.Stop();
            SetMode(Mode.MainMenu);
        }

        /// <summary>Someone joined or left: refresh everyone's stats (shared buffs) and the HUD.</summary>
        public void OnPartyChanged()
        {
            if (Run == null) return;
            foreach (PlayerController p in World.Players) p.ApplyStats(false);
        }

        /// <summary>Saves the chosen pair and starts the run.</summary>
        public void ConfirmArsenal(string weaponA, string weaponB)
        {
            Settings.weaponA = weaponA;
            Settings.weaponB = weaponB;
            if (!Launch.Automated) Settings.Save();
            if (arsenalForLobby && Net.Active)
            {
                arsenalForLobby = false;
                SetMode(Mode.Lobby);
                return;
            }
            StartRun();
        }

        public void OpenEquipment()
        {
            if (Run == null) return;
            equipmentReturnMode = CurrentMode;
            SetMode(Mode.Equipment);
        }

        public void CloseEquipment()
        {
            SetMode(equipmentReturnMode);
        }

        public void EquipItem(Item item)
        {
            Run.Equip(item);
            World.Host.ApplyStats(false);
            Audio.Play(Sfx.Unsheathe, 0.5f, 1.3f, 0f);
        }

        public void UnequipItem(ItemSlot slot)
        {
            Run.Unequip(slot);
            World.Host.ApplyStats(false);
            Audio.Play(Sfx.UiMove, 0.6f, 0.8f, 0f);
        }

        public void Pause()
        {
            if (CurrentMode == Mode.Playing) SetMode(Mode.Paused);
        }

        public void Resume()
        {
            if (CurrentMode == Mode.Paused) SetMode(Mode.Playing);
        }

        /// <summary>Settings opened from the pause menu sit over the frozen game, not the title painting.</summary>
        public bool SettingsFromPause => settingsReturnMode == Mode.Paused;

        public void OpenSettings()
        {
            settingsReturnMode = CurrentMode;
            SetMode(Mode.Settings);
        }

        public void CloseSettings()
        {
            Settings.bindings = Controls.SaveOverrides();
            if (!Launch.Automated) Settings.Save();
            SetMode(settingsReturnMode);
        }

        public void OpenCredits()
        {
            SetMode(Mode.Credits);
        }

        public void QuitToMenu()
        {
            if (Run != null && !Run.Banked) BankRun();
            World.EndRun();
            Run = null;
            // Hosting: back to the online room, the party stays connected.
            SetMode(Net.Active ? Mode.Lobby : Mode.MainMenu);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ChooseReward(int index)
        {
            if (CurrentMode != Mode.Reward || index < 0 || index >= rewardChoices.Count) return;
            RewardDef reward = rewardChoices[index];
            // The host's own build: teammates choose theirs in their browsers.
            Run.Main.Grant(reward);
            World.Host.ApplyStats(false);
            if (reward.HealOnPick > 0f) World.Host.Heal(reward.HealOnPick);
            Color c = reward.Kind == RewardKind.Technique ? Config.palette.gold
                    : reward.Kind == RewardKind.Blessing ? Config.palette.blessing
                    : Config.palette.playerAccent;
            UI.Toast(string.Format(Strings.Obtained, reward.Name), c);
            Audio.Play(Sfx.UiConfirm, 0.8f, 1f, 0f);
            // A dispute: a second card from what is left.
            if (--HostPicksLeft > 0 && rewardChoices.Count > 1)
            {
                rewardChoices.RemoveAt(index);
                return;
            }
            rewardChoices.Clear();
            hostPicked = true;
            rewardWaitStart = Time.unscaledTime;
            TryFinishPartyRewards();
        }

        /// <summary>The next room opens once every duelist has chosen (or the wait ran out).</summary>
        void TryFinishPartyRewards()
        {
            if (!hostPicked) return;
            if (Net.Active && Net.ChoosingNames().Count > 0)
            {
                if (PartyRewardTimeLeft > 0f) return;
                Net.AutoPickRemaining();
            }
            hostPicked = false;
            OpenRoute();
        }

        // ── Route map ─────────────────────────────────────────────────────────────────

        /// <summary>After a room: the doors ahead. One door means no choice.</summary>
        void OpenRoute()
        {
            routeOptions.Clear();
            routeOptions.AddRange(Run.NextDoors());
            routeVotes.Clear();
            if (routeOptions.Count <= 1)
            {
                ChooseRoute(0);
                return;
            }
            routeStart = Time.unscaledTime;
            if (Net.Active) Net.OfferRoute(routeOptions);
            SetMode(Mode.Route);
            World.HintOnce(HintId.Route);
        }

        /// <summary>A vote (the host's is -1). Solo, the host's vote decides at once.</summary>
        public void VoteRoute(int voter, int option)
        {
            if (CurrentMode != Mode.Route || option < 0 || option >= routeOptions.Count) return;
            routeVotes[voter] = option;
            Audio.Play(Sfx.UiConfirm, 0.6f, 1.1f, 0f);
            TryResolveRoute();
        }

        void TryResolveRoute()
        {
            if (CurrentMode != Mode.Route) return;
            int voters = 1 + (Net.Active ? Net.JoinedCount : 0);
            bool everyone = routeVotes.Count >= voters;
            if (!everyone && RouteTimeLeft > 0f) return;
            // The most votes win; a tie goes to the host's door (or the first of the tied).
            int best = -1, bestVotes = -1;
            for (int i = 0; i < routeOptions.Count; i++)
            {
                int v = RouteVotesFor(i);
                bool hostTie = v == bestVotes && HostRouteVote == i;
                if (v > bestVotes || hostTie)
                {
                    best = i;
                    bestVotes = v;
                }
            }
            ChooseRoute(Mathf.Max(0, best));
        }

        void ChooseRoute(int index)
        {
            RouteNode node = routeOptions.Count > 0 ? routeOptions[Mathf.Clamp(index, 0, routeOptions.Count - 1)] : RouteMap.Default(Run.Seed, Run.RoomIndex + 1);
            Run.Choose(node);
            if (Net.Active) Net.OnRouteChosen();
            NextRoom();
        }

        /// <summary>The host's name and look, as the party sees them.</summary>
        public void ApplyHostIdentity()
        {
            World.Host.DisplayName = Settings.playerName;
            World.Host.SetArt(Settings.playerArt);
        }

        public void SetHostName(string name)
        {
            Settings.playerName = GameSettings.CleanName(name, Settings.playerName);
            if (!Launch.Automated) Settings.Save();
            ApplyHostIdentity();
        }

        public void CycleHostArt(int dir)
        {
            int n = PlayerController.PartyArt.Length;
            Settings.playerArt = ((Settings.playerArt + dir) % n + n) % n;
            if (!Launch.Automated) Settings.Save();
            ApplyHostIdentity();
        }

        public void OpenAltar()
        {
            altarReturnMode = CurrentMode;
            SetMode(Mode.Altar);
        }

        public void CloseAltar()
        {
            SetMode(altarReturnMode);
        }

        /// <summary>Buys the next level of an upgrade and saves immediately.</summary>
        public bool TryBuyUpgrade(MetaUpgradeDef def)
        {
            if (Meta.TryBuy(def))
            {
                SaveMeta();
                Audio.Play(Sfx.Purchase, 0.8f, 1f, 0f);
                UI.Toast(string.Format(Strings.UpgradeBought, def.name, Meta.GetLevel(def.id)), Config.palette.gold);
                UI.Flash(Config.palette.gold, 0.12f);
                return true;
            }
            Audio.Play(Sfx.Denied, 0.7f, 1f, 0f);
            if (!Meta.IsMaxed(def)) UI.Toast(Strings.NotEnoughEcos, Config.palette.danger);
            return false;
        }

        void BankRun()
        {
            if (Run == null || Run.Banked) return;
            Run.EcosBanked = MetaRules.EcosForRun(Run);
            Run.Banked = true;
            Meta.RecordRun(Run.EcosBanked, Run.RoomsCleared, false);
            SaveMeta();
        }

        void SaveMeta()
        {
            MetaStore.Save(Meta, metaPath);
        }

        void SpawnEmbers(float dt)
        {
            emberTimer += dt * Config.feel.embersPerSecond;
            while (emberTimer >= 1f)
            {
                emberTimer -= 1f;
                List<Vector2> flames = World.Decor.FlamePositions;
                if (flames.Count > 0 && UnityEngine.Random.value < 0.55f)
                {
                    Vector2 p = flames[UnityEngine.Random.Range(0, flames.Count)] + UnityEngine.Random.insideUnitCircle * 0.15f;
                    Vfx.Ember(p, ColorUtil.Hex("#F2A541"));
                }
                else
                {
                    Rect b = World.Bounds;
                    var p = new Vector2(UnityEngine.Random.Range(b.xMin, b.xMax), UnityEngine.Random.Range(b.yMin, b.yMax));
                    Vfx.Ember(p, Config.palette.gold.WithAlpha(0.55f));
                }
            }
        }

        public void ApplySettings()
        {
            AudioListener.volume = Settings.master;
            if (World != null) World.ShowTimingIndicator = Settings.timingIndicator;
            if (Vfx != null) Vfx.BloodEnabled = Settings.blood;
            if (!Application.isEditor && !Launch.Automated)
            {
                Screen.fullScreenMode = Settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }

        /// <summary>Briefly slows the whole simulation (real-time seconds). Never stacks to slower.</summary>
        public void SlowMo(float seconds, float scale)
        {
            if (Launch.Headless || FeelMuted) return;
            if (slowMoTimer > 0f && slowMoScale <= scale) slowMoTimer = Mathf.Max(slowMoTimer, seconds);
            else
            {
                slowMoTimer = seconds;
                slowMoScale = Mathf.Clamp(scale, 0.05f, 1f);
            }
        }

        public void DevForceGameOver()
        {
            BankRun();
            SetMode(Mode.GameOver);
        }

        void NextRoom()
        {
            // A party that fell as the room was won stands up at the next one: no late game over.
            deathTimer = -1;
            Run.RoomIndex++;
            RoomDef room = Run.GetRoom(Run.RoomIndex);
            World.StartRoom(room, false);
            Vfx.ClearDecals();
            Vfx.StainBounds = World.Bounds;
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
            AnnounceRoom(room);
        }

        /// <summary>Tells the player what changed: depth, new enemy, Guardian, and what the enemies learned.</summary>
        void AnnounceRoom(RoomDef room)
        {
            Palette pal = Config.palette;
            UI.Toast(string.Format(Strings.ToastDepth, room.depth + 1), pal.uiText);
            if (room.kind != RoomKind.Combat && room.kind != RoomKind.Guardian) UI.Toast(RouteMap.Describe(Run.CurrentNode ?? new RouteNode { kind = room.kind }), RouteMap.KindColor(room.kind));
            if (room.depth % Biomes.RoomsPerBiome == 0) UI.Toast(room.biome.name + ": " + Biomes.RuleText(room.biome.rule), pal.uiText);
            if (room.depth % 3 == 0) UI.Toast(Biomes.TimeName(room.time), pal.uiDim);
            if (room.weather != Weather.Clear) UI.Toast(Biomes.WeatherName(room.weather) + ": " + Biomes.WeatherText(room.weather), pal.playerAccent);
            if (room.corrupted) UI.Toast(Strings.ToastCorrupted, ColorUtil.Hex("#B06CFF"));
            foreach (string newcomer in RoomGenerator.NewEnemiesAt(room.depth))
            {
                EnemyDef def = EnemyDefs.Get(newcomer);
                string line = string.Format(Strings.ToastNewEnemy, def.displayName);
                UI.Toast(string.IsNullOrEmpty(def.lore) ? line : line + " — " + def.lore, pal.danger);
            }
            if (room.guardian) UI.Toast(Strings.ToastGuardian, pal.gold);
            foreach (ReadingId reading in World.Adaptation.Active())
            {
                if (announcedReadings.Add(reading)) UI.Toast(string.Format(Strings.ToastLearned, Strings.ReadingName(reading)), pal.adapted);
            }
        }

        void OnRoomCleared()
        {
            Run.RoomsCleared++;
            bool fight = !World.CurrentRoom.IsEncounter;
            foreach (PlayerController p in World.Players)
            {
                if (p.Build == null) continue;
                p.Build.TickCurse();
                if (fight) p.Build.Mon += 8 + Run.RoomIndex / 2;
            }
            if (Run.AllyRooms > 0) Run.AllyRooms--;
            // Ascension: the duelist grows with every room, so depth is a race, not a wall.
            Run.Ascend();
            float heal = Mathf.Max(0f, Config.player.healOnRoomClear + Run.Modifiers.roomClearHealBonus);
            foreach (PlayerController p in World.Players)
            {
                p.ApplyStats(false);
                p.Heal(heal);
            }
            Feedback.Emit(FxType.RoomClear, World.CameraFocus.Position, Vector2.up);
            UI.Toast(string.Format(Strings.ToastRoomClear, Mathf.RoundToInt(heal)), Config.palette.uiText);
            UI.Toast(string.Format(Strings.ToastRoomEcos, MetaRules.RoomEcos(Run.RoomsCleared - 1)), Config.palette.gold);
            UI.Toast(string.Format(Strings.ToastAscension, Run.Ascension), Config.palette.blessing);
            roomClearTimer = RoomClearDelayFrames;
        }

        void OnPlayerDied()
        {
            deathTimer = DeathDelayFrames;
        }

        void TickTimers()
        {
            if (roomClearTimer > 0 && --roomClearTimer == 0)
            {
                roomClearTimer = -1;
                AdvanceAfterRoom();
            }
            if (deathTimer > 0 && --deathTimer == 0)
            {
                deathTimer = -1;
                BankRun();
                SetMode(Mode.GameOver);
            }
        }

        void AdvanceAfterRoom()
        {
            // Encounters pay out inside the room: straight to the route map.
            if (World.CurrentRoom.IsEncounter)
            {
                OpenRoute();
                return;
            }
            // Endless: there is always a next room. Every duelist chooses their own reward.
            RoomDef room = World.CurrentRoom;
            int picks = room.kind == RoomKind.Dispute ? 2 : 1;
            HostPicksLeft = picks;
            rewardChoices.Clear();
            rewardChoices.AddRange(Run.Main.RollChoices(3 + picks - 1 + Run.Modifiers.extraRewardChoices, Run.RoomIndex + 1, room.hint));
            hostPicked = false;
            if (Net.Active) Net.OfferRewards(Run.RoomIndex + 1, room.hint, picks);
            if (rewardChoices.Count == 0)
            {
                hostPicked = true;
                rewardWaitStart = Time.unscaledTime;
            }
            SetMode(Mode.Reward);
        }

        void UpdateMusic()
        {
            bool inRun = CurrentMode == Mode.Playing || CurrentMode == Mode.Paused || CurrentMode == Mode.Reward || CurrentMode == Mode.Equipment || CurrentMode == Mode.Route;
            bool menu = CurrentMode == Mode.Title || CurrentMode == Mode.MainMenu ||
                        CurrentMode == Mode.Settings || CurrentMode == Mode.Credits || CurrentMode == Mode.Altar || CurrentMode == Mode.Arsenal;
            int flow = Run != null ? Run.Flow.Level : 0;
            bool combat = inRun && World.ActiveEnemyCount > 0;
            // Calm between waves and in menus; the fight mood in combat, a touch louder in high Flow.
            float fightLevel = combat ? (flow >= 2 ? 0.8f : 0.7f) : 0f;
            float calmLevel = combat ? 0f : CurrentMode == Mode.GameOver ? 0.45f : menu ? 0.55f : 0.6f;
            Audio.SetMusicMood(calmLevel, fightLevel);
            Audio.MusicMuffled = inRun && World.Player != null && (World.Player.InMushin || World.Standoff.Active);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Hosting keeps the duel running for everyone while this window is in the background.
            if (!hasFocus && CurrentMode == Mode.Playing && !Launch.Automated && !Net.Active) Pause();
        }

        // ── Joining over Steam ────────────────────────────────────────────────────────

        public void OpenSteamJoin()
        {
            SetMode(Mode.SteamJoin);
            Steam.RefreshLobbies();
        }

        public void CloseSteamJoin()
        {
            Steam.LeaveGuest();
            SetMode(Mode.MainMenu);
        }

        void OnApplicationQuit()
        {
            Net?.Stop();
            Steam?.Shutdown();
        }

        void OnDestroy()
        {
            Net?.Stop();
            Steam?.Shutdown();
            Controls?.Disable();
        }
    }
}
