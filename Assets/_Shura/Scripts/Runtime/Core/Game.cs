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
    public sealed partial class Game : MonoBehaviour
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
            /// <summary>A guest in a friend's duel: the host's game runs it, this window draws it and plays.</summary>
            Remote,
            /// <summary>The ruined temple between runs (design doc §7.1).</summary>
            Temple,
            /// <summary>A PvP duel with rollback netcode (design doc §9.6).</summary>
            Duel,
            /// <summary>A disciple fell: choose the next one among three.</summary>
            Heir,
            /// <summary>The ways to walk the road (design doc §7.8).</summary>
            Modes,
            /// <summary>The road ended: the top of the Tower, the end of the Pilgrimage.</summary>
            Victory,
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
        /// <summary>How far toward the locked target the camera leans (0 = on you, 1 = on it).</summary>
        const float LockFraming = 0.34f;
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
        /// <summary>This game as a guest in someone else's duel (native client, no browser).</summary>
        public RemoteSession Remote { get; private set; }
        /// <summary>The dojo: the guided first lesson (and replayable from the menu).</summary>
        public Tutorial Tutorial { get; private set; }
        float tutorialDoneTimer;
        bool feelMuted;
        bool arsenalForLobby;
        bool arsenalForRemote;

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

        /// <summary>Telemetria local (WP-01).</summary>
        public TelemetryService Telemetry { get; private set; }
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
            // WP-02: os números de afinação em dados (StreamingAssets/Balance.json), por cima dos padrões.
            Balance.Load(Config);
            Settings = Launch.Automated ? new GameSettings() : GameSettings.Load();
            // Automated runs use a separate, fresh save so they never touch the player's progress.
            metaPath = MetaStore.DefaultPath(Launch.Automated);
            Meta = Launch.Automated ? new MetaProgress() : MetaStore.Load(metaPath);
            QualitySettings.vSyncCount = Launch.Headless ? 0 : 1;
            Application.runInBackground = Launch.Automated;

            Controls = new InputService();
            Controls.LoadOverrides(Settings.bindings);
            Controls.RumbleEnabled = Settings.rumble;
            Controls.MouseLockAllowed = Settings.mouseLockOn;

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // WP-02: o painel de afinação só existe em build de desenvolvimento e no editor.
            gameObject.AddComponent<TuningPanel>().Init(this);
#endif

            Feedback = new FeedbackRouter(this);
            Tap = new FeedbackTap(Feedback);
            World = new World(Config, Tap);
            Telemetry = new TelemetryService(this);
            Tap.Actor = () => World.ActorNetId;
            Net = new NetSession(this, Tap);
            World.RoomClearedEvent += OnRoomCleared;
            World.SlowMoRequested += seconds => SlowMo(seconds, 0.3f);
            World.MasteryRaised += (weapon, level) =>
            {
                ShowMasteryUp(weapon, level);
                SaveMeta();
            };
            World.PlayerDiedEvent += OnPlayerDied;

            Steam = new SteamLink();
            Steam.AllowSelf = Launch.SteamGuest;
            Steam.ListenLocal = Launch.Automated && Launch.Steam;
            if ((!Launch.Automated || Launch.Steam) && !Launch.Headless) Steam.Init();
            // Never named yet: the Steam name is the duelist's name (changeable in the room).
            if (Steam.Ready && !Launch.Automated && Settings.playerName == new GameSettings().playerName)
            {
                Settings.playerName = GameSettings.CleanName(Steamworks.SteamFriends.GetPersonaName(), Settings.playerName);
                Settings.Save();
            }
            Remote = new RemoteSession(this);
            Tutorial = new Tutorial(this);

            if (Launch.Bot) bot = new BotInput { EncounterLinger = Launch.ShotDir != null ? 150 : 0, ForcedKamae = Launch.Stance };
            if (Launch.Automated) autoDriver = new AutoDriver(this);

            ApplySettings();
            ApplyHostIdentity();
            SetMode(Mode.Title);
            // "-shuraTutorial" by hand (no bot): straight into the dojo, as a player would be.
            if (Launch.Tutorial && !Launch.Automated) StartTutorial();
        }

        void Update()
        {
            bool remotePlay = CurrentMode == Mode.Remote && Remote.Joined && !Remote.InputBlocked && (!Remote.Between || Remote.CourtWalk);
            bool court = CurrentMode == Mode.Duel || (CurrentMode == Mode.Lobby && !UI.CourtMenuOpen);
            Controls.Poll(Config.player, (CurrentMode == Mode.Playing || remotePlay || court) && bot == null);
            // Guests' input first (Steam tunnels feed the server): it acts in this very frame.
            Steam.Update();
            Net.Receive();
            if (Tutorial.Active && CurrentMode == Mode.Playing)
            {
                Tutorial.Update(Time.unscaledDeltaTime);
                if (Tutorial.Active && Tutorial.Current == Tutorial.Step.Done)
                {
                    tutorialDoneTimer += Time.unscaledDeltaTime;
                    if (tutorialDoneTimer > 5f || (tutorialDoneTimer > 1.2f && Controls.Submit)) EndTutorial(true);
                }
            }

            switch (CurrentMode)
            {
                case Mode.Playing:
                    // In the dojo, Esc twice leaves it (the pad's Start still pauses).
                    if (Tutorial.Active && !Launch.Automated && UnityEngine.InputSystem.Keyboard.current != null &&
                        UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        if (Tutorial.SkipArmed) SkipTutorial();
                        else Tutorial.ArmSkip();
                        break;
                    }
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
                case Mode.Duel:
                    TickDuel();
                    break;
            }
            // The courtyard lives while the party is between runs (and while two of them duel).
            if (Net.Active && (CurrentMode == Mode.Lobby || CurrentMode == Mode.Duel)) Net.TickCourt(Time.unscaledDeltaTime, HostCourtInput());

            // Online, the duel goes on behind the host's menus.
            if (WorldKeepsRunning) Simulate();

            // Right after the simulation: the snapshot leaves as fresh as it can be.
            Net.Update(Time.unscaledDeltaTime);
            Remote.Update(Time.unscaledDeltaTime);
            // A Steam invite accepted (or "+connect_lobby" at launch): straight to joining.
            if (Steam.TakeJoinRequest(out Steamworks.CSteamID invite))
            {
                if (CurrentMode == Mode.MainMenu || CurrentMode == Mode.Title || CurrentMode == Mode.SteamJoin)
                {
                    SetMode(Mode.SteamJoin);
                    JoinSteamLobby(invite);
                }
            }

            autoDriver?.Update();
            UpdateMusic();
            if (!Launch.Headless && CurrentMode != Mode.Paused) SpawnEmbers(Time.unscaledDeltaTime);
        }

        void Simulate()
        {
            // The dojo holds the world still while it waits for one button.
            if (Tutorial.Active && (Tutorial.Frozen || Tutorial.Touring || Tutorial.HoldWorld))
            {
                Clock.Reset();
                return;
            }
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

            // The day passes while the duel goes on (never in the dojo, never paused).
            if (Run != null && !World.TutorialRoom) Run.Clock.Advance(Time.unscaledDeltaTime);
            Mode startMode = CurrentMode;
            while (Clock.Step(out bool frozen))
            {
                // Frozen ticks are hit-stop: nothing moves and the input buffer does not age.
                if (frozen) continue;
                bot?.Think(World);
                World.Tick();
                if (Tutorial.Active)
                {
                    Tutorial.Tick();
                    if (Tutorial.Frozen) break;
                }
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
            if (Remote.Joined)
            {
                // A guest: the host's duel, posed for this frame; the camera follows this duelist.
                Remote.Render(realDt);
                List<object> me = Remote.Me;
                int flow = me != null ? MiniJson.I(me, 29) : 0;
                CameraRig.SetZoomOffset(Remote.Between ? 0f : flow * Config.feel.flowZoomPerLevel);
                List<object> env = Remote.Latest?.env;
                CameraRig.Post.SetMood(PostMood.ForHour(env != null ? MiniJson.F(env, 14, 9f) : 9f));
                if (Remote.HasOwn && !Remote.Between)
                {
                    Vector2? locked = Remote.LockPosition;
                    if (locked.HasValue && Settings.lockCamera) CameraRig.Follow(Vector2.Lerp(Remote.OwnPosition, locked.Value, LockFraming), Vector2.zero, World.Bounds, realDt);
                    else CameraRig.Follow(Remote.OwnPosition, Remote.Aim, World.Bounds, realDt);
                }
                else CameraRig.Follow(World.Bounds.center, Vector2.zero, World.Bounds, realDt);
                CameraRig.Tick(realDt);
                return;
            }
            CameraRig.SetZoomOffset(Run != null ? FlowLevel * Config.feel.flowZoomPerLevel : 0f);
            CameraRig.Post.SetMood(PostMood.ForHour(Run != null ? World.Hour : 9f));
            // Outside a run the camera frames the whole arena behind the menus.
            PlayerController focus = World.CameraFocus;
            if (Run != null && focus.LockTarget != null && Settings.lockCamera)
            {
                // Locked on: the camera frames both duelists, a little nearer to you.
                CameraRig.Follow(Vector2.Lerp(focus.RenderPosition, focus.LockTarget.RenderPosition, LockFraming), Vector2.zero, World.Bounds, realDt);
            }
            else if (Run != null) CameraRig.Follow(focus.RenderPosition, focus.Facing, World.Bounds, realDt);
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

        /// <summary>"Jogar": the very first time it leads through the dojo; after that, to the arsenal.</summary>
        public void Play()
        {
            if (!Settings.tutorialDone && Meta.runs == 0 && !Launch.Automated) StartTutorial();
            else if (Meta.heirs.Count > 0) OpenHeir(Mode.Arsenal);
            else OpenArsenal();
        }

        /// <summary>The dojo: one room, a master, and every lesson in order. Nothing here is banked.</summary>
        public void StartTutorial()
        {
            if (Run != null && !Run.Banked) BankRun();
            if (Net.Active)
            {
                Steam.StopHost();
                Net.Stop();
            }
            Run = new RunState(20261111UL, Config.flow, Meta);
            Run.Banked = true;
            World.ShowTimingIndicator = Settings.timingIndicator;
            Run.Weapons[0] = WeaponDefs.Get(Settings.Loadout(Settings.weaponA));
            Run.Weapons[1] = WeaponDefs.Get(Settings.Loadout(Settings.weaponB));
            roomClearTimer = -1;
            deathTimer = -1;
            rewardChoices.Clear();
            hostPicked = false;
            tutorialDoneTimer = 0f;
            Tutorial.Prepare();
            World.BeginRun(Run, ActiveInput);
            World.Player.ApplyStats(true);
            World.StartRoom(new RoomDef(Strings.TutorialRoomName, 0, Biomes.All[0], 22f, 13f, false, new EnemySpawn[0][]) { dojo = true, time = TimeOfDay.Night }, false);
            World.TutorialRoom = true;
            announcedReadings.Clear();
            tempo = Config.feel.baseTempo;
            slowMoTimer = 0f;
            Vfx.Clear();
            Vfx.ClearDecals();
            Vfx.StainBounds = World.Bounds;
            UI.ClearLog();
            Tutorial.Begin();
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
        }

        /// <summary>The last lesson is done: the dojo will not open by itself again.</summary>
        public void OnTutorialFinished()
        {
            tutorialDoneTimer = 0f;
            Settings.tutorialDone = true;
            if (!Launch.Automated) Settings.Save();
        }

        /// <summary>Leaves the dojo: to the arsenal after finishing it, otherwise to the menu.</summary>
        public void EndTutorial(bool finished)
        {
            Tutorial.Stop();
            World.EndRun();
            Run = null;
            Vfx.Clear();
            Vfx.ClearDecals();
            if (finished && !Launch.Automated) OpenArsenal();
            else SetMode(Mode.MainMenu);
        }

        /// <summary>Pause menu: never mind the dojo (it stays in the menu for later).</summary>
        public void SkipTutorial()
        {
            Settings.tutorialDone = true;
            if (!Launch.Automated) Settings.Save();
            EndTutorial(false);
        }

        public void SkipTutorialLesson()
        {
            Resume();
            Tutorial.SkipLesson();
        }

        public void StartRun()
        {
            Tutorial.Stop();
            // Restarting mid-run still banks what was earned: progress is never thrown away.
            if (Run != null && !Run.Banked) BankRun();
            RunMode mode = ModeForNextRun();
            Run = new RunState(SeedFor(mode), Config.flow, Meta) { Mode = mode };
            string weaponA = Launch.Automated && Launch.WeaponA != null ? Launch.WeaponA : Settings.Loadout(Settings.weaponA);
            string weaponB = Launch.Automated && Launch.WeaponB != null ? Launch.WeaponB : Settings.Loadout(Settings.weaponB);
            Run.Weapons[0] = WeaponDefs.Get(weaponA);
            Run.Weapons[1] = WeaponDefs.Get(weaponB);
            PrepareJourney();
            if (Run.Modifiers.startFlowPoints > 0f) Run.Flow.Add(Run.Modifiers.startFlowPoints);
            roomClearTimer = -1;
            deathTimer = -1;
            rewardChoices.Clear();
            hostPicked = false;
            if (Net.Active) Net.OnRunStarted();
            World.BeginRun(Run, ActiveInput);
            if (Net.Active) Net.AfterRunBegan();
            int startDepth = Launch.Automated ? Mathf.Max(0, Launch.StartDepth) : 0;
            for (int i = 0; i < startDepth; i++) Run.Ascend();
            Run.RoomIndex = startDepth;
            World.Player.ApplyStats(true);
            RoomDef first = Run.GetRoom(startDepth);
            // Automated inspection: open on a chosen kind of door, event or weather.
            if (Launch.Automated && startDepth > 0 && Enum.TryParse(Launch.RoomKind ?? "", out RoomKind kind))
            {
                // A rival's lair for the robot: one is made up on the spot (it remembers nobody real).
                string rivalId = null;
                if (kind == RoomKind.RivalLair)
                {
                    Rival r = Rivals.OnPlayerKilled(Meta, EnemyDefs.Get("kenkaku"), null, 0, "Haru", "Katana", null, 1, 90, Run.Seed);
                    Rivals.Roster.Add(r);
                    rivalId = r.id;
                }
                first = Run.Choose(new RouteNode { depth = startDepth, column = 1, kind = kind, eventId = Launch.EventId, hint = RewardHint.Relic,
                                                   challenge = kind == RoomKind.Challenge ? ChallengeKind.Timed : ChallengeKind.None, rivalId = rivalId });
            }
            if (Launch.Automated && Enum.TryParse(Launch.Weather ?? "", out Weather weather)) first.weather = weather;
            if (Launch.Automated && Launch.Stance >= 0) Run.Main.DefaultStance = (Kamae)Launch.Stance;
            if (Launch.Automated && Launch.Soul != null && RewardRegistry.Create(Launch.Soul) is TechniqueDef soulTech)
            {
                soulTech.Level = TechniqueDef.MaxLevel;
                soulTech.Branch = TechniqueDef.BranchEcho;
                Run.Main.Acquire(soulTech);
            }
            // The journey leaves at dawn; the day passes from here.
            Run.Clock.MomentReached += OnDayMoment;
            StampHour(first);
            World.StartRoom(first, startDepth == 0);
            Telemetry.BeginRun(Run);
            Telemetry.BeginRoom();
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
            arsenalForTemple = CurrentMode == Mode.Temple;
            arsenalForLobby = CurrentMode == Mode.Lobby;
            arsenalForRemote = CurrentMode == Mode.Remote;
            SetMode(Mode.Arsenal);
        }

        /// <summary>Back from the arsenal without choosing: to wherever it was opened from.</summary>
        public void CancelArsenal()
        {
            if (arsenalForRemote && Remote.Joined) SetMode(Mode.Remote);
            else if (arsenalForLobby && Net.Active) SetMode(Mode.Lobby);
            else if (arsenalForTemple) SetMode(Mode.Temple);
            else SetMode(Mode.MainMenu);
            arsenalForLobby = arsenalForRemote = arsenalForTemple = false;
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
            if (arsenalForRemote && Remote.Joined)
            {
                arsenalForRemote = false;
                SetMode(Mode.Remote);
                Remote.SendLoadout();
                return;
            }
            if (arsenalForLobby && Net.Active)
            {
                arsenalForLobby = false;
                SetMode(Mode.Lobby);
                return;
            }
            arsenalForTemple = false;
            // A disciple fell: the next body is chosen before the road.
            if (Meta.heirs.Count > 0 && !Launch.Automated)
            {
                OpenHeir(Mode.Playing);
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
        public bool SettingsFromPause => settingsReturnMode == Mode.Paused || settingsReturnMode == Mode.Remote;

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
            Tutorial.Stop();
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
            Telemetry.RewardPicked(reward);
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
            // Before the route map opens (§7.7) the road simply goes on through the first door.
            if (routeOptions.Count <= 1 || !Run.Has(Unlockable.RouteMap))
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
            Telemetry.RouteChosen(routeOptions, node);
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
            // A guest's Altar goes with them into the host's next run.
            if (altarReturnMode == Mode.Remote) Remote.SendLoadout();
        }

        /// <summary>A guest's run ended: what it was worth goes to this player's own Altar.</summary>
        void ShowMasteryUp(WeaponDef weapon, int level)
        {
            UI.Toast(string.Format(Strings.MasteryUp, weapon.name, WeaponMastery.Numeral(level), WeaponMastery.Unlock(level)), WeaponMastery.BladeColor(weapon));
        }

        /// <summary>An online guest finished an enemy: the mastery is theirs, kept in their own progress.</summary>
        public void RecordGuestFinisher(string weaponId)
        {
            int level = Meta.AddMasteryFinisher(weaponId);
            if (level <= 0) return;
            SaveMeta();
            WeaponDef w = WeaponDefs.Get(weaponId);
            if (w != null) ShowMasteryUp(w, level);
        }

        public void BankGuestRun(int ecos)
        {
            if (ecos <= 0) return;
            Meta.RecordRun(ecos, 0, false);
            SaveMeta();
            UI.Toast(string.Format(Strings.GuestEcosBanked, ecos, Meta.ecos), Config.palette.gold);
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

        /// <param name="died">The run ended in death (a restart or a quit does not bury the disciple).</param>
        /// <param name="victory">The road was finished (the Tower's top, the Pilgrimage's end).</param>
        void BankRun(bool died = false, bool victory = false)
        {
            if (Run == null || Run.Banked) return;
            Run.EcosBanked = MetaRules.EcosForRun(Run);
            Run.Banked = true;
            RecordPresence(died);
            if (died) Telemetry.Death();
            int runsBefore = Meta.runs;
            Meta.RecordRun(Run.EcosBanked, Run.RoomsCleared, victory);
            BankLegacy(died, victory);
            // Depois do legado: o Algoz (rival) já foi criado, e o fim da run sabe disso.
            Telemetry.EndRun(died ? "morte" : victory ? "vitoria" : "abandono");
            // A new layer of the game opened with this run (§7.7).
            if (LastLegacy != null && !Settings.unlockAll && !Launch.Automated) LastLegacy.unlocked = Unlocks.Names(Unlocks.Opened(runsBefore, Meta.runs));
            SaveMeta();
        }

        void SaveMeta()
        {
            MetaStore.Save(Meta, metaPath);
        }

        /// <summary>Save now (a friend's presence arrived online).</summary>
        public void PersistMeta() => SaveMeta();

        /// <summary>The ink message written where the duelist fell this run (edited on the death screen).</summary>
        public InkMessage LastInk { get; private set; }

        /// <summary>A fall or the end of a run: the spirit learns your habits; a fall leaves a stain and a message.</summary>
        void RecordPresence(bool died)
        {
            LastInk = null;
            if (World?.Host == null || World.TutorialRoom || Run.Mode == RunMode.Tower || Run.RoomsCleared <= 0 && !died) return;
            Presence.RecordSpirit(Meta, Settings.playerName, World.Host, Run.Profile);
            if (died) LastInk = Presence.RecordDeath(Meta, Settings.playerName, World.Depth, World.Host.Position, World.Host.LastHurtBy, World.Host.ArtIndex, World.HostTrail);
        }

        /// <summary>The death screen: the message changes words (← →) or its sentence (Enter).</summary>
        public void EditInk(int dir, bool template)
        {
            if (LastInk == null) return;
            if (template) LastInk.template = (LastInk.template + 1) % Presence.Templates.Length;
            else LastInk.word = (LastInk.word + (dir >= 0 ? 1 : Presence.Words.Length - 1)) % Presence.Words.Length;
            SaveMeta();
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
            if (World != null) World.ShowTimingIndicator = Settings.timingIndicator && (Run == null || Run.Mode != RunMode.Master);
            if (Vfx != null) Vfx.BloodEnabled = Settings.blood;
            if (!Application.isEditor && !Launch.Automated) ApplyScreen();
        }

        string appliedScreen;

        /// <summary>Full screen or window, at the chosen resolution (the monitor's own by default).</summary>
        void ApplyScreen()
        {
            FullScreenMode mode = Settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = Settings.resolutionWidth, h = Settings.resolutionHeight;
            if (w <= 0 || h <= 0)
            {
                if (Settings.fullscreen)
                {
                    w = Screen.currentResolution.width;
                    h = Screen.currentResolution.height;
                }
                else
                {
                    w = Screen.width;
                    h = Screen.height;
                }
            }
            string key = w + "x" + h + ":" + mode;
            if (key == appliedScreen) return;
            appliedScreen = key;
            Screen.SetResolution(w, h, mode);
        }

        /// <summary>Resolutions offered in the settings: the monitor's own sizes, from 1280×720 up.</summary>
        public List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (r.width >= 1280 && r.height >= 720 && !list.Contains(v)) list.Add(v);
            }
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
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
            BankRun(true);
            SetMode(Mode.GameOver);
        }

        /// <summary>A room takes the hour it is entered at (the full moon only rises at night).</summary>
        void StampHour(RoomDef room)
        {
            room.time = Run.Clock.Period;
            if (room.weather == Weather.FullMoon && room.time != TimeOfDay.Night) room.weather = Weather.Clear;
        }

        /// <summary>The day marks its passing: a line on screen, and a sound or a light.</summary>
        void OnDayMoment(int i)
        {
            if (Run == null || World.TutorialRoom) return;
            DayClock.Moment m = DayClock.Moments[i];
            UI.ShowAmbience(m.text);
            switch (i)
            {
                case 0: Audio.Play(Sfx.Glint, 0.35f, 1.6f, 0f); break;
                case 1:
                    Audio.Play(Sfx.Chime, 0.5f, 0.75f, 0f);
                    if (CameraRig.Post != null) CameraRig.Post.Flash = 0.45f;
                    break;
                case 3: Audio.Play(Sfx.Chime, 0.7f, 0.5f, 0f); break;
                case 5: Audio.Play(Sfx.Chime, 0.45f, 0.62f, 0f); break;
                case 6: Audio.Play(Sfx.Glint, 0.3f, 0.7f, 0f); break;
                case 7: Audio.Play(Sfx.Chime, 0.55f, 0.45f, 0f); break;
                case 8: Audio.Play(Sfx.Danger, 0.25f, 0.6f, 0f); break;
            }
        }

        void NextRoom()
        {
            // A party that fell as the room was won stands up at the next one: no late game over.
            deathTimer = -1;
            Run.RoomIndex++;
            RoomDef room = Run.GetRoom(Run.RoomIndex);
            StampHour(room);
            World.StartRoom(room, false);
            Telemetry.BeginRoom();
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
            float heal = Run.Modifiers.noRoomHeal ? 0f : Mathf.Max(0f, Config.player.healOnRoomClear + Run.Modifiers.roomClearHealBonus);
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
            // Nobody dies in the dojo: up again, and the lesson goes on.
            if (Tutorial.Active)
            {
                World.Host.Revive(1f);
                return;
            }
            deathTimer = DeathDelayFrames;
        }

        void TickTimers()
        {
            // The room waits for a blade being cleaned and sheathed (Chiburi and Nōtō).
            if (roomClearTimer == 1 && RitualInProgress()) return;
            if (roomClearTimer > 0 && --roomClearTimer == 0)
            {
                roomClearTimer = -1;
                AdvanceAfterRoom();
            }
            if (deathTimer > 0 && --deathTimer == 0)
            {
                deathTimer = -1;
                BankRun(true);
                SetMode(Mode.GameOver);
            }
        }

        void AdvanceAfterRoom()
        {
            // The Ashura fell: liberation, a new Ashura, the secret — or the wheel turns again (§8.6).
            if (!string.IsNullOrEmpty(Run.PendingEnding))
            {
                // You became the Ashura: your friends will meet you at the end of their runs for a week.
                if (Run.PendingEnding == GameModes.EndingShura) Presence.RecordAshura(Meta, Settings.playerName, World.Host);
                if (Run.PendingEnding != GameModes.EndingWheel || Run.Mode == RunMode.Pilgrimage)
                {
                    Victory();
                    return;
                }
                Meta.endings.Remove(GameModes.EndingWheel);
                Meta.endings.Add(GameModes.EndingWheel);
                UI.Toast(Strings.ToastWheelTurns, Config.palette.gold);
                Run.PendingEnding = null;
            }
            // The Tower's top, the Pilgrimage's last room: the road ends here.
            int goal = GameModes.Goal(Run.Mode);
            if (goal > 0 && Run.RoomsCleared >= goal)
            {
                Victory();
                return;
            }
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
            Telemetry.CloseRoom();
            Telemetry.RewardsOffered(rewardChoices);
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
            if (Remote.Joined)
            {
                RemoteSession.Snap s = Remote.Latest;
                bool fight = s != null && !Remote.Between && MiniJson.I(s.d, "cb") != 0;
                List<object> me = Remote.Me;
                int lvl = me != null ? MiniJson.I(me, 29) : 0;
                Audio.SetMusicMood(fight ? 0f : Remote.Between ? 0.55f : 0.6f, fight ? (lvl >= 2 ? 0.8f : 0.7f) : 0f);
                Audio.MusicMuffled = !Remote.Between && ((me != null && MiniJson.B(me, 44)) || (s != null && s.so != null && MiniJson.B(s.so, 0)));
                return;
            }
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
            Remote.LastError = null;
            SetMode(Mode.SteamJoin);
            Steam.RefreshLobbies();
        }

        public void CloseSteamJoin()
        {
            Remote.Leave(false);
            Steam.LeaveGuest();
            SetMode(Mode.MainMenu);
        }

        /// <summary>Into a friend's Steam lobby; this window becomes the game once the host welcomes us.</summary>
        public void JoinSteamLobby(Steamworks.CSteamID lobby)
        {
            Remote.Begin(new SteamRemoteLink(Steam));
            Steam.Join(lobby);
        }

        /// <summary>Straight to a host on the same network or Radmin VPN ("192.168.0.10" or "26.1.2.3:7777").</summary>
        public void JoinByAddress(string address)
        {
            address = (address ?? "").Trim();
            if (address.StartsWith("http://")) address = address.Substring(7);
            address = address.TrimEnd('/');
            if (address.Length == 0) return;
            if (address.IndexOf(':') < 0) address += ":" + NetSession.DefaultPort;
            if (CurrentMode != Mode.SteamJoin) SetMode(Mode.SteamJoin);
            Remote.Begin(new SocketRemoteLink(address));
        }

        /// <summary>The host welcomed this guest: the duel takes the window.</summary>
        public void OnRemoteJoined()
        {
            Vfx.Clear();
            Vfx.ClearDecals();
            SetMode(Mode.Remote);
            UI.OnRemoteJoined();
        }

        /// <summary>The duel is over for this guest (left, lost, or full): back to the menus.</summary>
        public void OnRemoteEnded()
        {
            arsenalForRemote = false;
            if (settingsReturnMode == Mode.Remote) settingsReturnMode = Mode.MainMenu;
            Vfx.Clear();
            Vfx.ClearDecals();
            if (CurrentMode != Mode.SteamJoin) SetMode(Mode.MainMenu);
        }

        public void LeaveRemote()
        {
            Remote.Leave();
        }

        void OnApplicationQuit()
        {
            Remote?.Leave(false);
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
