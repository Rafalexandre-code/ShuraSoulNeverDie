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

            if (Launch.Bot) bot = new BotInput();
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
            }

            // Online, the duel goes on behind the host's menus.
            if (WorldKeepsRunning) Simulate();

            Net.Update(Time.unscaledDeltaTime);

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
            World.StartRoom(Run.GetRoom(startDepth), startDepth == 0);
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
        }

        public void CloseLobby()
        {
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
            Run.Acquire(reward);
            World.Host.ApplyStats(false);
            if (reward.HealOnPick > 0f) World.Host.Heal(reward.HealOnPick);
            Color c = reward.Kind == RewardKind.Technique ? Config.palette.gold
                    : reward.Kind == RewardKind.Blessing ? Config.palette.blessing
                    : Config.palette.playerAccent;
            UI.Toast(string.Format(Strings.Obtained, reward.Name), c);
            Audio.Play(Sfx.UiConfirm, 0.8f, 1f, 0f);
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
            // Endless: there is always a next room. Every duelist chooses their own reward.
            rewardChoices.Clear();
            rewardChoices.AddRange(Run.RollChoices(3 + Run.Modifiers.extraRewardChoices, Run.RoomIndex + 1));
            hostPicked = false;
            if (Net.Active) Net.OfferRewards(Run.RoomIndex + 1);
            if (rewardChoices.Count == 0)
            {
                hostPicked = true;
                rewardWaitStart = Time.unscaledTime;
            }
            SetMode(Mode.Reward);
        }

        void UpdateMusic()
        {
            bool inRun = CurrentMode == Mode.Playing || CurrentMode == Mode.Paused || CurrentMode == Mode.Reward || CurrentMode == Mode.Equipment;
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

        void OnApplicationQuit()
        {
            Net?.Stop();
        }

        void OnDestroy()
        {
            Net?.Stop();
            Controls?.Disable();
        }
    }
}
