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
            Victory,
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
            if (bot != null && Launch.Headless) Clock.Accumulate(SimClock.Dt * HeadlessBotTicksPerFrame, 10f);
            else Clock.Accumulate(Time.unscaledDeltaTime);

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
            if (Run != null && !Run.Banked) BankRun(false);
            ulong seed = Launch.Seed ?? (ulong)DateTime.UtcNow.Ticks;
            Run = new RunState(seed, Config.flow, Meta);
            if (Run.Modifiers.startFlowPoints > 0f) Run.Flow.Add(Run.Modifiers.startFlowPoints);
            roomClearTimer = -1;
            deathTimer = -1;
            rewardChoices.Clear();
            World.BeginRun(Run, ActiveInput);
            World.StartRoom(0);
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
            if (Run != null && !Run.Banked) BankRun(false);
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
            Color c = reward.Kind == RewardKind.Technique ? Config.palette.gold : Config.palette.playerAccent;
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

        void BankRun(bool victory)
        {
            if (Run == null || Run.Banked) return;
            Run.EcosBanked = MetaRules.EcosForRun(Run, victory);
            Run.Banked = true;
            Meta.RecordRun(Run.EcosBanked, Run.RoomsCleared, victory);
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

        public void DevForceGameOver()
        {
            BankRun(false);
            SetMode(Mode.GameOver);
        }

        void NextRoom()
        {
            Run.RoomIndex++;
            World.StartRoom(Run.RoomIndex);
            SetMode(Mode.Playing);
            UI.FadeFromBlack();
        }

        void OnRoomCleared()
        {
            Run.RoomsCleared++;
            float heal = Config.player.healOnRoomClear + Run.Modifiers.roomClearHealBonus;
            World.Player.Heal(heal);
            Feedback.Emit(FxType.RoomClear, World.Player.Position, Vector2.up);
            UI.Toast(string.Format(Strings.ToastRoomClear, Mathf.RoundToInt(heal)), Config.palette.uiText);
            UI.Toast(string.Format(Strings.ToastRoomEcos, MetaRules.RoomEcos(Run.RoomsCleared - 1)), Config.palette.gold);
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
                BankRun(false);
                SetMode(Mode.GameOver);
            }
        }

        void AdvanceAfterRoom()
        {
            if (Run.RoomIndex + 1 >= RoomDefs.Prototype.Length)
            {
                BankRun(true);
                SetMode(Mode.Victory);
                return;
            }
            rewardChoices.Clear();
            rewardChoices.AddRange(Run.RollChoices(3 + Run.Modifiers.extraRewardChoices));
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
            float choir = inRun ? (flow >= 3 ? 0.6f : 0f) : (menu || CurrentMode == Mode.Victory ? 0.35f : 0f);
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
