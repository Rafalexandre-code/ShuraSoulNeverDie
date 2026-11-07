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
        public MetaProgress Meta { get; private set; }
        public LaunchOptions Launch { get; private set; }
        public Mode CurrentMode { get; private set; }
        public IReadOnlyList<RewardDef> RewardChoices => rewardChoices;
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
            World = new World(Config, Feedback);
            World.RoomClearedEvent += OnRoomCleared;
            World.PlayerDiedEvent += OnPlayerDied;

            if (Launch.Bot) bot = new BotInput();
            if (Launch.Automated) autoDriver = new AutoDriver(this);

            ApplySettings();
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
            }

            autoDriver?.Update();
            UpdateMusic();
            if (!Launch.Headless && CurrentMode != Mode.Paused) SpawnEmbers(Time.unscaledDeltaTime);
        }

        void Simulate()
        {
            if (bot != null && Launch.Headless)
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

            while (Clock.Step(out bool frozen))
            {
                // Frozen ticks are hit-stop: nothing moves and the input buffer does not age.
                if (frozen) continue;
                bot?.Think(World);
                World.Tick();
                ActiveInput.Buffer.Age();
                TickTimers();
                if (CurrentMode != Mode.Playing) break;
            }
        }

        void LateUpdate()
        {
            float realDt = Time.unscaledDeltaTime;
            bool frozen = Clock.HitstopFrames > 0 || CurrentMode != Mode.Playing;
            World.Render(frozen ? 1f : Clock.Alpha, frozen ? 0f : realDt, ShowHitboxes);
            Vfx.Paused = CurrentMode == Mode.Paused;
            CameraRig.SetZoomOffset(Run != null ? FlowLevel * Config.feel.flowZoomPerLevel : 0f);
            // Outside a run the camera frames the whole arena behind the menus.
            if (Run != null) CameraRig.Follow(World.Player.RenderPosition, World.Player.Facing, World.Bounds, realDt);
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
            if (Run.Modifiers.startFlowPoints > 0f) Run.Flow.Add(Run.Modifiers.startFlowPoints);
            roomClearTimer = -1;
            deathTimer = -1;
            rewardChoices.Clear();
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
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
        }

        public void Pause()
        {
            if (CurrentMode == Mode.Playing) SetMode(Mode.Paused);
        }

        public void Resume()
        {
            if (CurrentMode == Mode.Paused) SetMode(Mode.Playing);
        }

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
            SetMode(Mode.MainMenu);
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
            Run.Acquire(reward);
            World.Player.ApplyStats(false);
            if (reward.HealOnPick > 0f) World.Player.Heal(reward.HealOnPick);
            Color c = reward.Kind == RewardKind.Technique ? Config.palette.gold
                    : reward.Kind == RewardKind.Blessing ? Config.palette.blessing
                    : Config.palette.playerAccent;
            UI.Toast(string.Format(Strings.Obtained, reward.Name), c);
            Audio.Play(Sfx.UiConfirm, 0.8f, 1f, 0f);
            rewardChoices.Clear();
            NextRoom();
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
            if (!Application.isEditor && !Launch.Automated)
            {
                Screen.fullScreenMode = Settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }

        /// <summary>Briefly slows the whole simulation (real-time seconds). Never stacks to slower.</summary>
        public void SlowMo(float seconds, float scale)
        {
            if (Launch.Headless) return;
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
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
            AnnounceRoom(room);
        }

        /// <summary>Tells the player what changed: depth, new enemy, Guardian, and what the enemies learned.</summary>
        void AnnounceRoom(RoomDef room)
        {
            Palette pal = Config.palette;
            UI.Toast(string.Format(Strings.ToastDepth, room.depth + 1), pal.uiText);
            string newcomer = RoomGenerator.NewEnemyAt(room.depth);
            if (newcomer != null) UI.Toast(string.Format(Strings.ToastNewEnemy, EnemyDefs.Get(newcomer).displayName), pal.danger);
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
            World.Player.ApplyStats(false);
            float heal = Config.player.healOnRoomClear + Run.Modifiers.roomClearHealBonus;
            World.Player.Heal(heal);
            Feedback.Emit(FxType.RoomClear, World.Player.Position, Vector2.up);
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
            // Endless: there is always a next room.
            rewardChoices.Clear();
            rewardChoices.AddRange(Run.RollChoices(3 + Run.Modifiers.extraRewardChoices, Run.RoomIndex + 1));
            if (rewardChoices.Count == 0)
            {
                NextRoom();
                return;
            }
            SetMode(Mode.Reward);
        }

        void UpdateMusic()
        {
            bool inRun = CurrentMode == Mode.Playing || CurrentMode == Mode.Paused || CurrentMode == Mode.Reward;
            bool menu = CurrentMode == Mode.Title || CurrentMode == Mode.MainMenu ||
                        CurrentMode == Mode.Settings || CurrentMode == Mode.Credits || CurrentMode == Mode.Altar;
            int flow = Run != null ? Run.Flow.Level : 0;
            bool combat = inRun && World.ActiveEnemyCount > 0;

            float drone = CurrentMode == Mode.GameOver ? 0.35f : 0.55f;
            float drums = combat ? 0.8f : inRun ? 0.15f : 0f;
            float strings = inRun && flow >= 2 ? 0.55f : 0f;
            float choir = inRun ? (flow >= 3 ? 0.6f : 0f) : (menu ? 0.35f : 0f);
            Audio.SetMusicTargets(drone, drums, strings, choir);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && CurrentMode == Mode.Playing && !Launch.Automated) Pause();
        }

        void OnDestroy()
        {
            Controls?.Disable();
        }
    }
}
