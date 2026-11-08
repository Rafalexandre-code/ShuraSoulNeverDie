using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The game's UI, hand-painted in sumi-e ink on washi paper (see UiKit), drawn with IMGUI
    /// on a virtual 1920x1080 canvas (letterboxed). Navigation is read in Update through
    /// InputService, so keyboard, mouse and gamepad all work; OnGUI only draws and handles
    /// mouse hover/click.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        const float VW = 1920f;
        const float VH = 1080f;
        const int MaxPopups = 24;
        const int MaxToasts = 4;

        struct MenuItem
        {
            public string label;
            public Func<string> value;
            public Action activate;
            public Action<int> adjust;
        }

        sealed class FloatingText
        {
            public string text;
            public Vector2 world;
            public Color color;
            public float age;
            public float life;
            public bool small;
        }

        readonly List<MenuItem> items = new List<MenuItem>();
        readonly List<FloatingText> popups = new List<FloatingText>();
        readonly List<FloatingText> toasts = new List<FloatingText>();

        Game game;
        readonly UiKit kit = new UiKit();
        Texture2D white;
        GUIStyle titleStyle, headerStyle, itemStyle, bodyStyle, bodyCenterStyle, smallStyle, smallRightStyle,
                 popupStyle, cardNameStyle, cardBodyStyle, hintStyle, toastStyle, roomStyle;
        bool stylesReady;
        float scale = 1f;
        Vector2 offset;
        int menuIndex;
        int modeChangedFrame = -1;
        Vector2 lastMouse;
        string hint;
        float hintTimer;
        float flashAlpha;
        Color flashColor = Color.white;
        float hurtAlpha;
        float fadeAlpha;
        Texture2D vignette;
        float ghostHp;
        float lastHp;
        float ghostDelay;
        float selAnim;
        int lastSel = -1;
        GUIStyle itemLeftStyle, ecosStyle, damageStyle, streakStyle;
        readonly List<string> arsenalPicks = new List<string>();
        int arsenalIndex;
        int equipIndex;
        int shownStreak;
        float streakPop;

        Palette Pal => game.Config.palette;

        public void Init(Game owner)
        {
            game = owner;
            white = Texture2D.whiteTexture;
            vignette = ShapeLibrary.VignetteTexture();
            kit.Load();
        }

        /// <summary>Screen fades in from black (room transitions).</summary>
        public void FadeFromBlack()
        {
            fadeAlpha = 1f;
        }

        /// <summary>Damage numbers grow and turn gold as Flow rises: you can see yourself getting stronger.</summary>
        public void DamageNumber(int value, Vector2 world, int flowLevel)
        {
            if (value <= 0) return;
            if (popups.Count >= MaxPopups) popups.RemoveAt(0);
            Vector2 jitter = new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(0f, 0.2f));
            Color c = flowLevel >= 2 ? Color.Lerp(Color.white, Pal.gold, 0.4f + 0.2f * flowLevel) : Color.white;
            popups.Add(new FloatingText { text = value.ToString(), world = world + jitter, color = c, life = 0.7f, small = flowLevel < 3 });
        }

        // ── API used by gameplay feedback ──────────────────────────────────────────────

        public void Popup(string text, Vector2 world, Color color)
        {
            if (popups.Count >= MaxPopups) popups.RemoveAt(0);
            popups.Add(new FloatingText { text = text, world = world, color = color, life = 0.9f });
        }

        public void Toast(string text, Color color)
        {
            toasts.Insert(0, new FloatingText { text = text, color = color, life = 2.4f });
            if (toasts.Count > MaxToasts) toasts.RemoveAt(toasts.Count - 1);
        }

        public void ShowHint(string text)
        {
            hint = text;
            hintTimer = 6.5f;
        }

        public void Flash(Color color, float strength)
        {
            if (game.FeelMuted) return;
            if (game.Settings.reduceFlashes) strength *= 0.2f;
            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, strength);
        }

        public void HurtVignette()
        {
            if (game.FeelMuted) return;
            hurtAlpha = game.Settings.reduceFlashes ? 0.15f : 0.45f;
        }

        public void OnModeChanged(Game.Mode mode)
        {
            menuIndex = 0;
            if (mode == Game.Mode.Arsenal)
            {
                arsenalPicks.Clear();
                arsenalPicks.Add(game.Settings.weaponA);
                arsenalPicks.Add(game.Settings.weaponB);
                arsenalIndex = 0;
            }
            if (mode == Game.Mode.Equipment) equipIndex = 0;
            modeChangedFrame = Time.frameCount;
            if (mode == Game.Mode.Title || mode == Game.Mode.MainMenu || mode == Game.Mode.GameOver)
            {
                hint = null;
                popups.Clear();
                toasts.Clear();
            }
        }

        // ── Input ──────────────────────────────────────────────────────────────────────

        void Update()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime;
            Age(popups, dt);
            Age(toasts, dt);
            if (game.CurrentMode == Game.Mode.Playing && hintTimer > 0f) hintTimer -= dt;
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3f);
            hurtAlpha = Mathf.MoveTowards(hurtAlpha, 0f, dt * 1.5f);
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, dt * 2.2f);
            int streak = game.Run != null ? game.Run.PerfectStreak : 0;
            if (streak > shownStreak) streakPop = 1f;
            shownStreak = streak;
            streakPop = Mathf.MoveTowards(streakPop, 0f, dt * 4f);
            UpdateGhostHealth(dt);
            UpdatePetals(dt);
            if (menuIndex != lastSel)
            {
                lastSel = menuIndex;
                selAnim = 0f;
            }
            selAnim = Mathf.MoveTowards(selAnim, 1f, dt * 6f);

            BuildMenu();
            // Ignore input on the frame the screen changed: the same press must not act twice.
            if (game.Launch.Automated || Time.frameCount == modeChangedFrame) return;

            InputService input = game.Controls;
            if (editingName) return;
            switch (game.CurrentMode)
            {
                case Game.Mode.Title:
                    if (input.AnyPressed)
                    {
                        game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
                        game.GoToMainMenu();
                    }
                    break;
                case Game.Mode.Reward:
                    HandleReward(input);
                    break;
                case Game.Mode.Arsenal:
                    HandleArsenal(input);
                    break;
                case Game.Mode.Equipment:
                    HandleEquipment(input);
                    break;
                case Game.Mode.Playing:
                    break;
                default:
                    HandleMenu(input);
                    break;
            }
        }

        /// <summary>A white chunk lingers where health was lost, then drains: damage reads at a glance.</summary>
        void UpdateGhostHealth(float dt)
        {
            PlayerController p = game.World != null ? game.World.Player : null;
            if (p == null) return;
            if (p.Health < lastHp) ghostDelay = 0.45f;
            lastHp = p.Health;
            if (p.Health >= ghostHp)
            {
                ghostHp = p.Health;
                return;
            }
            ghostDelay -= dt;
            if (ghostDelay <= 0f) ghostHp = Mathf.MoveTowards(ghostHp, p.Health, p.MaxHealth * 0.8f * dt);
        }

        static void Age(List<FloatingText> list, float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i].age += dt;
                if (list[i].age >= list[i].life) list.RemoveAt(i);
            }
        }

        void BuildMenu()
        {
            items.Clear();
            GameSettings s = game.Settings;
            switch (game.CurrentMode)
            {
                case Game.Mode.Lobby:
                    AddItem(Strings.LobbyStart, game.StartRun);
                    items.Add(new MenuItem
                    {
                        label = Strings.LobbyName,
                        value = () => editingName ? nameBuffer + (Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "|" : " ") : game.Settings.playerName,
                        activate = BeginNameEdit,
                    });
                    items.Add(new MenuItem
                    {
                        label = Strings.LobbyLook,
                        value = () => Strings.ArtName(game.Settings.playerArt),
                        activate = () => game.CycleHostArt(1),
                        adjust = dir => game.CycleHostArt(dir),
                    });
                    AddItem(Strings.LobbyWeapons, game.OpenArsenal);
                    AddItem(Strings.Altar, game.OpenAltar);
                    AddItem(Strings.LobbyClose, game.CloseLobby);
                    break;
                case Game.Mode.MainMenu:
                    AddItem(Strings.Play, game.OpenArsenal);
                    AddItem(Strings.PlayOnline, game.OpenLobby);
                    AddItem(Strings.Altar, game.OpenAltar);
                    AddItem(Strings.Settings, game.OpenSettings);
                    AddItem(Strings.Credits, game.OpenCredits);
                    AddItem(Strings.Quit, game.QuitGame);
                    break;
                case Game.Mode.Paused:
                    AddItem(Strings.Resume, game.Resume);
                    AddItem(Strings.Equipment, game.OpenEquipment);
                    AddItem(Strings.Settings, game.OpenSettings);
                    AddItem(Strings.RestartRun, game.StartRun);
                    AddItem(Strings.MainMenu, game.QuitToMenu);
                    break;
                case Game.Mode.Settings:
                    AddSlider(Strings.MasterVolume, () => s.master, v => s.master = v);
                    AddSlider(Strings.MusicVolume, () => s.music, v => s.music = v);
                    AddSlider(Strings.SfxVolume, () => s.sfx, v => s.sfx = v);
                    AddSlider(Strings.ScreenShake, () => s.shake, v => s.shake = v);
                    AddToggle(Strings.TimingIndicator, () => s.timingIndicator, v => s.timingIndicator = v);
                    AddToggle(Strings.Blood, () => s.blood, v => s.blood = v);
                    AddToggle(Strings.ReduceFlashes, () => s.reduceFlashes, v => s.reduceFlashes = v);
                    AddToggle(Strings.Fullscreen, () => s.fullscreen, v => s.fullscreen = v);
                    AddItem(Strings.ResetControls, () =>
                    {
                        game.Controls.ResetOverrides();
                        Toast(Strings.ControlsReset, Pal.uiText);
                    });
                    AddItem(Strings.Back, game.CloseSettings);
                    break;
                case Game.Mode.Credits:
                    AddItem(Strings.Back, game.GoToMainMenu);
                    break;
                case Game.Mode.GameOver:
                    AddItem(Strings.NewRun, game.StartRun);
                    AddItem(Strings.Arsenal, game.OpenArsenal);
                    AddItem(Strings.Altar, game.OpenAltar);
                    AddItem(Strings.MainMenu, game.QuitToMenu);
                    break;
                case Game.Mode.Altar:
                    foreach (MetaUpgradeDef def in MetaUpgrades.All)
                    {
                        MetaUpgradeDef captured = def;
                        AddItem(def.name, () => game.TryBuyUpgrade(captured));
                    }
                    AddItem(Strings.Back, game.CloseAltar);
                    break;
            }
            if (items.Count > 0) menuIndex = Mathf.Clamp(menuIndex, 0, items.Count - 1);
        }

        void AddItem(string label, Action activate)
        {
            items.Add(new MenuItem { label = label, activate = activate });
        }

        void AddSlider(string label, Func<float> get, Action<float> set)
        {
            items.Add(new MenuItem
            {
                label = label,
                value = () => Mathf.RoundToInt(get() * 100f) + "%",
                adjust = dir =>
                {
                    set(Mathf.Clamp01(Mathf.Round((get() + dir * 0.1f) * 10f) / 10f));
                    game.ApplySettings();
                },
                activate = () =>
                {
                    float v = Mathf.Round((get() + 0.1f) * 10f) / 10f;
                    set(v > 1.001f ? 0f : v);
                    game.ApplySettings();
                },
            });
        }

        void AddToggle(string label, Func<bool> get, Action<bool> set)
        {
            Action toggle = () =>
            {
                set(!get());
                game.ApplySettings();
            };
            items.Add(new MenuItem
            {
                label = label,
                value = () => get() ? Strings.On : Strings.Off,
                activate = toggle,
                adjust = _ => toggle(),
            });
        }

        void HandleMenu(InputService input)
        {
            if (items.Count == 0) return;
            if (input.NavY != 0)
            {
                menuIndex = (menuIndex - input.NavY + items.Count) % items.Count;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.NavX != 0 && items[menuIndex].adjust != null)
            {
                items[menuIndex].adjust(input.NavX);
                game.Audio.Play(Sfx.UiMove, 0.5f, 1.1f, 0f);
            }
            if (input.Submit) Activate(menuIndex);
            else if (input.Cancel) Back();
        }

        // ── Host name (typed in the lobby) ─────────────────────────────────────────────

        bool editingName;
        string nameBuffer = string.Empty;

        void BeginNameEdit()
        {
            editingName = true;
            nameBuffer = game.Settings.playerName;
        }

        /// <summary>Keyboard text for the name field (IMGUI key events, in OnGUI).</summary>
        void HandleNameTyping()
        {
            if (!editingName) return;
            if (game.CurrentMode != Game.Mode.Lobby)
            {
                editingName = false;
                return;
            }
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                game.SetHostName(nameBuffer);
                editingName = false;
                modeChangedFrame = Time.frameCount;
                game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                editingName = false;
                modeChangedFrame = Time.frameCount;
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (nameBuffer.Length > 0) nameBuffer = nameBuffer.Substring(0, nameBuffer.Length - 1);
            }
            else if (e.character != '\0' && !char.IsControl(e.character) && nameBuffer.Length < 16)
            {
                nameBuffer += e.character;
            }
            e.Use();
        }

        void HandleReward(InputService input)
        {
            if (game.WaitingForParty) return;
            int count = game.RewardChoices.Count;
            if (count == 0) return;
            menuIndex = Mathf.Clamp(menuIndex, 0, count - 1);
            if (input.NavX != 0)
            {
                menuIndex = (menuIndex + input.NavX + count) % count;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) game.ChooseReward(menuIndex);
        }

        void Activate(int index)
        {
            if (index < 0 || index >= items.Count) return;
            game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
            items[index].activate?.Invoke();
        }

        void Back()
        {
            switch (game.CurrentMode)
            {
                case Game.Mode.Settings: game.CloseSettings(); break;
                case Game.Mode.Credits: game.GoToMainMenu(); break;
                case Game.Mode.Paused: game.Resume(); break;
                case Game.Mode.Altar: game.CloseAltar(); break;
                case Game.Mode.Equipment: game.CloseEquipment(); break;
                case Game.Mode.Lobby: game.CloseLobby(); break;
            }
        }

        // ── Drawing ────────────────────────────────────────────────────────────────────

        struct PetalFx
        {
            public Vector2 pos;
            public Vector2 vel;
            public float rot;
            public float spin;
            public float size;
            public float phase;
        }

        readonly List<PetalFx> petals = new List<PetalFx>();
        const int PetalCount = 28;

        bool PaintingScreen
        {
            get
            {
                Game.Mode m = game.CurrentMode;
                return m == Game.Mode.Title || m == Game.Mode.MainMenu || m == Game.Mode.Credits || m == Game.Mode.Arsenal ||
                       m == Game.Mode.Altar || m == Game.Mode.Lobby || (m == Game.Mode.Settings && !game.SettingsFromPause);
            }
        }

        /// <summary>Sakura petals drift across the painted screens.</summary>
        void UpdatePetals(float dt)
        {
            if (!PaintingScreen)
            {
                petals.Clear();
                return;
            }
            bool fresh = petals.Count == 0;
            while (petals.Count < PetalCount)
            {
                petals.Add(new PetalFx
                {
                    pos = new Vector2(UnityEngine.Random.Range(-300f, VW), fresh ? UnityEngine.Random.Range(-60f, VH) : UnityEngine.Random.Range(-90f, -30f)),
                    vel = new Vector2(UnityEngine.Random.Range(25f, 70f), UnityEngine.Random.Range(35f, 85f)),
                    rot = UnityEngine.Random.Range(0f, 360f),
                    spin = UnityEngine.Random.Range(-120f, 120f),
                    size = UnityEngine.Random.Range(13f, 26f),
                    phase = UnityEngine.Random.Range(0f, 10f),
                });
            }
            for (int i = petals.Count - 1; i >= 0; i--)
            {
                PetalFx p = petals[i];
                p.phase += dt;
                p.pos += (p.vel + new Vector2(Mathf.Sin(p.phase * 1.3f) * 30f, 0f)) * dt;
                p.rot += p.spin * dt;
                petals[i] = p;
                if (p.pos.y > VH + 40f || p.pos.x > VW + 60f) petals.RemoveAt(i);
            }
        }

        void DrawPetals()
        {
            foreach (PetalFx p in petals)
            {
                float flutter = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(p.phase * 2.1f));
                kit.Petal(new Rect(p.pos.x, p.pos.y, p.size, p.size * flutter), p.rot, 0.9f);
            }
        }

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            scale = Mathf.Min(Screen.width / VW, Screen.height / VH);
            offset = new Vector2((Screen.width - VW * scale) * 0.5f, (Screen.height - VH * scale) * 0.5f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            HandleNameTyping();
            Game.Mode mode = game.CurrentMode;
            bool inRun = mode == Game.Mode.Playing || mode == Game.Mode.Paused || mode == Game.Mode.Reward;
            if (PaintingScreen)
            {
                kit.Painting(FullScreen, Mathf.Sin(Time.unscaledTime * 0.05f) * 12f);
                DrawPetals();
            }
            else
            {
                DrawTex(FullScreen, vignette, new Color(1f, 1f, 1f, 0.85f));
            }
            if (inRun && game.Run != null && game.Run.Flow.Level >= game.Run.Flow.MaxLevel)
            {
                // Full Flow: the screen edges burn gold.
                float pulse = 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
                DrawTex(FullScreen, vignette, Pal.gold.WithAlpha(pulse));
            }
            if (mode == Game.Mode.Playing || mode == Game.Mode.Paused)
            {
                DrawHud();
                DrawPopups();
            }

            switch (mode)
            {
                case Game.Mode.Title: DrawTitle(); break;
                case Game.Mode.MainMenu: DrawMainMenu(); break;
                case Game.Mode.Settings: DrawSettings(); break;
                case Game.Mode.Credits: DrawCredits(); break;
                case Game.Mode.Paused: DrawPause(); break;
                case Game.Mode.Reward: DrawReward(); break;
                case Game.Mode.GameOver: DrawEnd(Strings.DeathTitle, Strings.DeathSubtitle); break;
                case Game.Mode.Altar: DrawAltar(); break;
                case Game.Mode.Arsenal: DrawArsenal(); break;
                case Game.Mode.Equipment: DrawEquipment(); break;
                case Game.Mode.Lobby: DrawLobby(); break;
            }

            // The reward screen has its own text blocks; room-clear toasts were already seen in play.
            if (mode != Game.Mode.Reward) DrawToasts();
            if (mode == Game.Mode.Playing) DrawHint();
            DrawOverlays();
            if (fadeAlpha > 0.001f) DrawRect(FullScreen, Color.black.WithAlpha(fadeAlpha));
            if (Event.current.type == EventType.Repaint) lastMouse = Event.current.mousePosition;
            GUI.matrix = Matrix4x4.identity;
        }

        Rect FullScreen => new Rect(-offset.x / scale, -offset.y / scale, Screen.width / scale, Screen.height / scale);

        /// <summary>The game frozen behind a screen: darkened, with an ink wash creeping in from the edges.</summary>
        void DimGame(float amount)
        {
            DrawRect(FullScreen, Color.black.WithAlpha(0.45f * amount));
            kit.InkVignette(FullScreen, new Color(0.05f, 0.04f, 0.04f, 0.95f * amount));
        }

        /// <summary>A brush heading with a dry stroke beneath it.</summary>
        void Heading(Rect r, string text, Color color, Color underline, float underlineWidth = 420f)
        {
            Label(r, UiKit.Sentence(text), headerStyle, color);
            kit.Underline(new Rect(r.center.x - underlineWidth * 0.5f, r.yMax - 6f, underlineWidth, 18f), underline);
        }

        // ── Title & menus ─────────────────────────────────────────────────────────────

        void DrawTitle()
        {
            float t = Time.unscaledTime;
            var logo = new Rect(110f, 70f, 900f, 458f);
            kit.Logo(logo, UiKit.Ink);
            kit.Seal(new Rect(930f, 420f, 128f, 128f), 0.95f);
            Label(new Rect(110f, 520f, 900f, 80f), Strings.Title, latinTitleStyle, UiKit.Ink);
            Label(new Rect(110f, 600f, 900f, 40f), Strings.Tagline, taglineStyle, UiKit.InkSoft);

            // "Press any button" sits on a breathing ink stroke.
            float breath = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 1.8f));
            var stroke = new Rect(VW * 0.5f - 380f, 850f, 760f, 120f);
            kit.Stroke(stroke, UiKit.Ink.WithAlpha(0.88f * breath));
            Label(new Rect(stroke.x, stroke.y + 6f, stroke.width, stroke.height - 12f), Strings.PressAnyButton, itemStyle, UiKit.PaperText.WithAlpha(0.6f + 0.4f * breath));
            Label(new Rect(VW - 460, VH - 56, 420, 40), Strings.BuildLabel, smallRightStyle, UiKit.PaperText.WithAlpha(0.7f));
        }

        void DrawMainMenu()
        {
            kit.Logo(new Rect(90f, 40f, 640f, 326f), UiKit.Ink);
            kit.Seal(new Rect(660f, 280f, 92f, 92f), 0.95f);
            var panel = new Rect(110f, 395f, 620f, 590f);
            kit.PaperPanel(panel);
            Label(new Rect(panel.x, panel.y + 44f, panel.width, 34f), Strings.Tagline, taglineSmallStyle, UiKit.InkSoft);
            DrawMenuList(panel.y + 104f, 76f, 520f, panel.center.x, true);
            Label(new Rect(VW - 460, VH - 56, 420, 40), Strings.BuildLabel, smallRightStyle, UiKit.PaperText.WithAlpha(0.7f));
        }

        /// <summary>
        /// Menu entries. The selected one is painted over with a vermilion stroke (it sweeps in
        /// when the selection moves) and its text turns to paper.
        /// </summary>
        void DrawMenuList(float top, float itemHeight, float width, float centerX, bool onPaper)
        {
            float x = centerX - width * 0.5f;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, top + i * itemHeight, width, itemHeight - 8f);
                bool hover = !game.Launch.Automated && r.Contains(e.mousePosition);
                if (hover && e.type == EventType.Repaint && mouseMoved) menuIndex = i;
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    menuIndex = i;
                    e.Use();
                    Activate(i);
                    return;
                }

                bool selected = i == menuIndex;
                MenuItem item = items[i];
                string text = item.label;
                if (item.value != null) text += selected ? "     ‹  " + item.value() + "  ›" : "     " + item.value();
                if (selected)
                {
                    float textW = itemStyle.CalcSize(new GUIContent(text)).x;
                    float sw = Mathf.Min(width + 60f, textW + 170f);
                    var stroke = new Rect(centerX - sw * 0.5f, r.center.y - itemHeight * 0.62f, sw, itemHeight * 1.24f);
                    kit.Stroke(stroke, UiKit.Shu.WithAlpha(0.95f), CombatMath.EaseOutCubic(selAnim));
                }
                Color c = selected ? UiKit.PaperText : onPaper ? UiKit.Ink : UiKit.PaperText;
                FitLabel(r, text, itemStyle, c);
            }
        }

        // ── Online ────────────────────────────────────────────────────────────────────

        /// <summary>The online room: links to share (Radmin first), who is in, and the run's start.</summary>
        void DrawLobby()
        {
            NetSession net = game.Net;
            kit.Logo(new Rect(70f, 30f, 420f, 214f), UiKit.Ink);
            var left = new Rect(80f, 250f, 640f, 780f);
            kit.PaperPanel(left);
            Heading(new Rect(left.x, left.y + 36f, left.width, 90f), Strings.LobbyTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 360f);
            DrawMenuList(left.y + 160f, 64f, 560f, left.center.x, true);
            Label(new Rect(left.x + 60f, left.yMax - 200f, left.width - 120f, 150f), Strings.LobbyRadminHint, cardBodyStyle, UiKit.InkSoft);

            var right = new Rect(780f, 60f, 1080f, 970f);
            kit.PaperPanel(right);
            Label(new Rect(right.x + 70f, right.y + 50f, right.width - 140f, 60f), Strings.LobbyLinks, bodyStyle, UiKit.Ink);
            float y = right.y + 120f;
            for (int i = 0; i < net.Urls.Count && i < 4; i++)
            {
                bool radmin = net.UrlLabels[i] == "Radmin VPN";
                var row = new Rect(right.x + 60f, y, right.width - 120f, 70f);
                if (radmin) kit.Stroke(new Rect(row.x - 20f, row.y - 6f, row.width + 40f, row.height + 12f), UiKit.Shu.WithAlpha(0.9f));
                Label(new Rect(row.x + 10f, row.y, 260f, row.height), net.UrlLabels[i], capsStyle, radmin ? UiKit.PaperText : UiKit.Shu);
                Label(new Rect(row.x + 230f, row.y, row.width - 240f, row.height), net.Urls[i], urlStyle, radmin ? UiKit.PaperText : UiKit.Ink);
                y += 80f;
            }

            y += 20f;
            kit.Underline(new Rect(right.x + 60f, y, right.width - 120f, 16f), UiKit.Ink.WithAlpha(0.45f));
            Label(new Rect(right.x + 70f, y + 20f, 600f, 40f), Strings.LobbyPlayers + "  ·  " + game.World.Players.Count + " / " + PartyScaling.MaxPlayers, capsStyle, UiKit.Shu);
            y += 66f;
            DrawLobbyPlayer(new Rect(right.x + 60f, y, right.width - 120f, 64f), PlayerController.PartyArt[game.Settings.playerArt],
                            string.Format(Strings.LobbyHostName, game.Settings.playerName),
                            WeaponDefs.Get(game.Settings.weaponA).name + "  ·  " + WeaponDefs.Get(game.Settings.weaponB).name, -1f);
            y += 70f;
            int shown = 0;
            foreach (NetSession.Remote r in net.Remotes)
            {
                if (!r.joined) continue;
                DrawLobbyPlayer(new Rect(right.x + 60f, y, right.width - 120f, 64f), PlayerController.PartyArt[r.art], r.name + (r.ready ? "  ✓" : ""),
                                WeaponDefs.Get(r.weaponA).name + "  ·  " + WeaponDefs.Get(r.weaponB).name, r.rtt);
                y += 70f;
                shown++;
            }
            if (shown == 0) Label(new Rect(right.x + 80f, y + 6f, right.width - 160f, 40f), Strings.LobbyEmpty, taglineStyle, UiKit.InkSoft);
        }

        void DrawLobbyPlayer(Rect r, string art, string name, string weapons, float rtt)
        {
            CharacterArt a = PixelArt.Character(art);
            if (a != null) DrawSprite(new Rect(r.x, r.y, r.height, r.height), a.walk[(int)ArtDir.Down][Mathf.FloorToInt(Time.unscaledTime * 5f) % a.walk[0].Length]);
            Label(new Rect(r.x + r.height + 20f, r.y, 420f, r.height), name, weaponHudStyle, UiKit.Ink);
            Label(new Rect(r.x + r.height + 380f, r.y, 400f, r.height), weapons, smallStyle, UiKit.InkSoft);
            if (rtt >= 0f) Label(new Rect(r.xMax - 140f, r.y, 140f, r.height), Mathf.RoundToInt(rtt * 1000f) + " ms", smallRightStyle, rtt < 0.12f ? UiKit.Ai : UiKit.Shu);
        }

        /// <summary>Draws one sprite of a sheet (IMGUI draws whole textures, so via UVs).</summary>
        static void DrawSprite(Rect r, Sprite s)
        {
            Rect tr = s.textureRect;
            Texture tex = s.texture;
            float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
            var fit = new Rect(r.center.x - tr.width * k * 0.5f, r.center.y - tr.height * k * 0.5f, tr.width * k, tr.height * k);
            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(fit, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            GUI.color = old;
        }

        /// <summary>
        /// Co-op HUD: teammates' health under the Flow panel, "hold E" over a fallen ally,
        /// and the host's own "you fell" notice while the party fights on.
        /// </summary>
        void DrawParty()
        {
            World w = game.World;
            if (w.Players.Count < 2) return;
            Camera cam = game.CameraRig.Cam;

            var panel = new Rect(VW - 470f, 240f, 460f, 34f + (w.Players.Count - 1) * 40f);
            kit.InkPanel(new Rect(panel.x, panel.y - 10f, panel.width, panel.height + 20f), 0.8f);
            Label(new Rect(panel.x + 40f, panel.y, panel.width - 80f, 26f), string.Format(Strings.OnlineBadge, w.Players.Count), capsRightStyle, Pal.gold);
            float y = panel.y + 30f;
            foreach (PlayerController p in w.Players)
            {
                if (p == w.Host) continue;
                Label(new Rect(panel.x + 40f, y, 170f, 32f), p.DisplayName, smallStyle, p.IsAlive ? UiKit.PaperText : UiKit.Shu);
                if (p.IsAlive) kit.Bar(new Rect(panel.x + 200f, y + 4f, 220f, 22f), p.Health / p.MaxHealth, UiKit.ShuBright, Color.black.WithAlpha(0.7f));
                else kit.Bar(new Rect(panel.x + 200f, y + 4f, 220f, 22f), w.ReviveProgress(p), Pal.gold, Color.black.WithAlpha(0.7f));
                y += 40f;
            }

            if (cam == null) return;
            // Every fallen teammate shows its revive progress where it lies.
            foreach (PlayerController p in w.Players)
            {
                if (p.IsAlive) continue;
                Vector2 g = WorldToGui(cam, p.RenderPosition + Vector2.up * 1.3f);
                float t = w.ReviveProgress(p);
                kit.Enso(new Rect(g.x - 34f, g.y - 34f, 68f, 68f), UiKit.PaperText.WithAlpha(0.35f), Time.unscaledTime * 40f);
                if (t > 0f) kit.Bar(new Rect(g.x - 60f, g.y + 34f, 120f, 18f), t, Pal.gold, Color.black.WithAlpha(0.7f));
            }

            PlayerController host = w.Host;
            PlayerController fallen = w.FallenNear(host);
            if (fallen != null)
            {
                string text = string.Format(game.Controls.UsingGamepad ? Strings.ReviveHoldPad : Strings.ReviveHold, fallen.DisplayName);
                float tw = toastStyle.CalcSize(new GUIContent(text)).x;
                kit.Stroke(new Rect(VW * 0.5f - tw * 0.5f - 80f, VH - 250f, tw + 160f, 70f), UiKit.Shu.WithAlpha(0.92f));
                Label(new Rect(0f, VH - 240f, VW, 48f), text, toastStyle, UiKit.PaperText);
            }
            if (!host.IsAlive && w.AnyPlayerAlive)
            {
                kit.InkPanel(new Rect(VW * 0.5f - 560f, VH - 260f, 1120f, 110f), 0.9f);
                Label(new Rect(VW * 0.5f - 520f, VH - 252f, 1040f, 90f), Strings.YouFell, hintStyleCenter, UiKit.PaperText);
            }
        }

        Vector2 WorldToGui(Camera cam, Vector2 world)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            return new Vector2((sp.x - offset.x) / scale, (Screen.height - sp.y - offset.y) / scale);
        }

        void DrawSettings()
        {
            if (game.SettingsFromPause) DimGame(1f);
            var panel = new Rect(300f, 28f, 900f, 1024f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 40f, panel.width, 90f), Strings.SettingsTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            DrawMenuList(panel.y + 160f, 72f, 760f, panel.center.x, true);
            DrawControlsPanel(new Rect(1250f, 300f, 600f, 340f), false);
        }

        void DrawCredits()
        {
            var panel = new Rect(300f, 80f, 1320f, 920f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 50f, panel.width, 90f), Strings.CreditsTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            Label(new Rect(panel.x + 100f, panel.y + 190f, panel.width - 200f, 520f), Strings.CreditsBody, bodyCenterStyle, UiKit.Ink);
            kit.Seal(new Rect(panel.xMax - 190f, panel.yMax - 200f, 110f, 110f), 0.9f);
            DrawMenuList(panel.yMax - 170f, 86f, 420f, panel.center.x, true);
        }

        /// <summary>The host chose; every teammate chooses their own card in their browser.</summary>
        void DrawPartyChoosing()
        {
            var panel = new Rect(VW * 0.5f - 520f, 300f, 1040f, 420f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 40f, panel.width, 90f), Strings.PartyChoosingTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 520f);
            List<string> names = game.Net.ChoosingNames();
            Label(new Rect(panel.x + 60f, panel.y + 170f, panel.width - 120f, 120f), string.Join("   ·   ", names), bodyCenterStyle, UiKit.Ink);
            Label(new Rect(panel.x, panel.yMax - 100f, panel.width, 40f),
                  string.Format(Strings.PartyChoosingTimer, Mathf.CeilToInt(game.PartyRewardTimeLeft)), smallStyleCenter, UiKit.InkSoft);
        }

        void DrawPause()
        {
            // Online the duel goes on behind this menu: a lighter veil, and a warning.
            bool live = game.WorldKeepsRunning;
            DimGame(live ? 0.45f : 1f);
            var panel = new Rect(560f, 90f, 800f, 900f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 40f, panel.width, 100f), live ? Strings.PausedLive : Strings.Paused, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            if (live) Label(new Rect(panel.x, panel.y + 128f, panel.width, 34f), Strings.PausedLiveHint, smallStyleCenter, UiKit.Shu);
            RoomDef room = game.World != null ? game.World.CurrentRoom : null;
            if (room != null && game.Run != null)
            {
                Label(new Rect(panel.x, panel.y + 160f, panel.width, 34f), string.Format(Strings.HudRoom, game.Run.RoomIndex + 1, room.name), smallStyleCenter, UiKit.InkSoft);
            }
            DrawMenuList(panel.y + 215f, 80f, 560f, panel.center.x, true);
            DrawControlsPanel(new Rect(panel.x + 90f, panel.y + 640f, panel.width - 180f, 230f), true);
            kit.Seal(new Rect(panel.xMax - 120f, panel.y + 40f, 76f, 76f), 0.85f);
        }

        void DrawControlsPanel(Rect r, bool onPaper)
        {
            Color title = onPaper ? UiKit.Shu : Pal.gold;
            Color key = onPaper ? UiKit.InkSoft : UiKit.PaperDim;
            Color value = onPaper ? UiKit.Ink : UiKit.PaperText;
            if (onPaper) kit.Underline(new Rect(r.x, r.y - 8f, r.width, 16f), UiKit.Ink.WithAlpha(0.5f));
            else kit.InkPanel(new Rect(r.x - 30f, r.y - 24f, r.width + 60f, r.height + 48f), 0.92f);
            Label(new Rect(r.x + 10, r.y + 10, r.width - 20, 32), Strings.ControlsTitle, smallStyle, title);
            float y = r.y + 46;
            foreach (KeyValuePair<string, string> b in game.Controls.DescribeBindings())
            {
                Label(new Rect(r.x + 10, y, 200, 30), b.Key, smallStyle, key);
                Label(new Rect(r.x + 200, y, r.width - 210, 30), b.Value, smallStyle, value);
                y += 30;
            }
        }

        // ── Altar ─────────────────────────────────────────────────────────────────────

        void DrawAltar()
        {
            MetaProgress meta = game.Meta;
            var panel = new Rect(210f, 16f, 1500f, 1048f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 34f, panel.width, 90f), Strings.AltarTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f));
            Label(new Rect(panel.x, panel.y + 130f, panel.width, 36f), Strings.AltarSubtitle, bodyCenterStyle, UiKit.InkSoft);
            Label(new Rect(panel.x, panel.y + 168f, panel.width, 64f), string.Format(Strings.EcosBalance, meta.ecos), ecosStyle, UiKit.GoldInk);

            const float width = 1340f, rowH = 62f;
            float top = panel.y + 250f;
            float x = panel.center.x - width * 0.5f;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            MetaUpgradeDef[] defs = MetaUpgrades.All;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, top + i * rowH, width, rowH - 8f);
                bool hover = !game.Launch.Automated && r.Contains(e.mousePosition);
                if (hover && e.type == EventType.Repaint && mouseMoved) menuIndex = i;
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    menuIndex = i;
                    e.Use();
                    Activate(i);
                    return;
                }

                bool selected = i == menuIndex;
                if (selected)
                {
                    kit.Stroke(new Rect(r.x - 30f, r.y - 10f, i >= defs.Length ? r.width : 470f, r.height + 20f), UiKit.Shu.WithAlpha(0.92f), CombatMath.EaseOutCubic(selAnim));
                }
                Color nameColor = selected ? UiKit.PaperText : UiKit.Ink;

                if (i >= defs.Length)
                {
                    Label(r, items[i].label, itemStyle, nameColor);
                    continue;
                }

                MetaUpgradeDef def = defs[i];
                int level = meta.GetLevel(def.id);
                bool maxed = meta.IsMaxed(def);
                Label(new Rect(r.x + 10f, r.y, 420f, r.height), def.name, itemLeftStyle, nameColor);

                for (int j = 0; j < def.maxLevel; j++)
                {
                    var pr = new Rect(r.x + 470f + j * 34f, r.y + r.height * 0.5f - 15f, 30f, 30f);
                    kit.Dab(pr, j < level ? UiKit.Shu : UiKit.Ink.WithAlpha(0.16f));
                }

                string effect = level > 0 ? def.EffectAt(level) : "—";
                if (!maxed) effect += "   ›   " + def.EffectAt(level + 1);
                Label(new Rect(r.x + 680f, r.y, 450f, r.height), effect, smallStyle, UiKit.Ink);

                string cost = maxed ? Strings.Max : string.Format(Strings.CostLabel, meta.NextCost(def));
                Color costColor = maxed ? UiKit.GoldInk : meta.CanBuy(def) ? UiKit.Ink : UiKit.Shu;
                Label(new Rect(r.xMax - 220f, r.y, 210f, r.height), cost, bodyRightStyle, costColor);
            }

            float by = top + items.Count * rowH + 6f;
            kit.Underline(new Rect(panel.center.x - 500f, by, 1000f, 16f), UiKit.Ink.WithAlpha(0.45f));
            if (menuIndex < defs.Length)
            {
                Label(new Rect(panel.x + 80f, by + 18f, panel.width - 160f, 36), defs[menuIndex].description, bodyCenterStyle, UiKit.Ink);
            }
            Label(new Rect(panel.x, by + 60f, panel.width, 30), string.Format(Strings.AltarStats, meta.runs, meta.bestRoom), smallStyleCenter, UiKit.InkSoft);
            Label(new Rect(panel.x, panel.yMax - 70f, panel.width, 32), Strings.AltarControls, smallStyleCenter, UiKit.InkSoft);
        }

        // ── Reward ────────────────────────────────────────────────────────────────────

        void DrawReward()
        {
            DimGame(1.2f);
            if (game.WaitingForParty)
            {
                DrawPartyChoosing();
                return;
            }
            Heading(new Rect(0, 50, VW, 100), Strings.RewardTitle, UiKit.PaperText, UiKit.Shu, 620f);
            Label(new Rect(0, 160, VW, 40), Strings.RewardSubtitle, bodyCenterStyle, UiKit.PaperDim);

            IReadOnlyList<RewardDef> choices = game.RewardChoices;
            const float gap = 46f;
            int n = Mathf.Max(1, choices.Count);
            float cardW = Mathf.Min(470f, (VW - 160f - gap * (n - 1)) / n);
            float bodyW = cardW - 80f;
            // Every card is as tall as the wordiest one, so the row stays even.
            float cardH = 440f;
            for (int i = 0; i < choices.Count; i++) cardH = Mathf.Max(cardH, RewardCardHeight(choices[i], cardW, bodyW));
            float startX = (VW - (choices.Count * cardW + (choices.Count - 1) * gap)) * 0.5f;
            float top = Mathf.Min(236f, 790f - cardH);
            Event e = Event.current;
            for (int i = 0; i < choices.Count; i++)
            {
                RewardDef r = choices[i];
                bool selected = i == menuIndex;
                float lift = selected ? -18f * CombatMath.EaseOutCubic(selAnim) : 0f;
                var rect = new Rect(startX + i * (cardW + gap), top + lift, cardW, cardH);

                if (!game.Launch.Automated && rect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.Repaint && (e.mousePosition - lastMouse).sqrMagnitude > 1f) menuIndex = i;
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        game.ChooseReward(i);
                        return;
                    }
                }

                bool technique = r.Kind == RewardKind.Technique;
                bool blessing = r.Kind == RewardKind.Blessing;
                Color accent = technique ? UiKit.GoldInk : blessing ? UiKit.Ai : UiKit.Shu;
                // Talisman card: paper, a coloured band across the top, a seal when chosen.
                kit.PaperPanel(rect, selected ? 1f : 0.93f);
                kit.Stroke(new Rect(rect.x + 30f, rect.y + 28f, rect.width - 60f, 64f), accent.WithAlpha(0.92f));
                Label(new Rect(rect.x, rect.y + 32f, cardW, 54f), technique ? Strings.Technique : blessing ? Strings.Blessing : Strings.Relic, cardKindStyle, UiKit.PaperText);
                float nameH = cardNameStyle.CalcHeight(new GUIContent(r.Name), cardW - 60f);
                Label(new Rect(rect.x + 30, rect.y + 100, cardW - 60, nameH), r.Name, cardNameStyle, UiKit.Ink);
                float y = rect.y + 104 + nameH;
                if (r.SourceEnemy != null)
                {
                    Label(new Rect(rect.x, y, cardW, 30), string.Format(Strings.AbsorbedFrom, r.SourceEnemy), smallStyleCenter, UiKit.InkSoft);
                    y += 32;
                }
                y += 10;
                kit.Underline(new Rect(rect.x + 50f, y, cardW - 100f, 14f), UiKit.Ink.WithAlpha(0.35f));
                y += 18;
                float gainH = cardBodyStyle.CalcHeight(new GUIContent(r.Gain), bodyW);
                Label(new Rect(rect.x + 40, y, bodyW, 28), Strings.Gain, capsStyle, accent);
                Label(new Rect(rect.x + 40, y + 28, bodyW, gainH), r.Gain, cardBodyStyle, UiKit.Ink);
                y += 28 + gainH + 16;
                float costH = cardBodyStyle.CalcHeight(new GUIContent(r.Cost), bodyW);
                Label(new Rect(rect.x + 40, y, bodyW, 28), Strings.Cost, capsStyle, UiKit.Shu);
                Label(new Rect(rect.x + 40, y + 28, bodyW, costH), r.Cost, cardBodyStyle, UiKit.Ink);
                if (selected) kit.Seal(new Rect(rect.xMax - 64f, rect.y - 22f, 78f, 78f), 0.95f * CombatMath.EaseOutCubic(selAnim));
            }
            Label(new Rect(0, top + cardH + 12f, VW, 34f), Strings.EquipmentOpen + "   ·   " + string.Format(Strings.EquipmentBagCount, game.Run.Bag.Count, RunState.BagCapacity), smallStyleCenter, Pal.gold);
            DrawReading(new Rect(300f, 840f, VW - 600f, 160f));
            Label(new Rect(0, VH - 52f, VW, 40), Strings.RewardControls, bodyCenterStyle, UiKit.PaperDim);
        }

        float RewardCardHeight(RewardDef r, float cardW, float bodyW)
        {
            float h = 104f + cardNameStyle.CalcHeight(new GUIContent(r.Name), cardW - 60f);
            if (r.SourceEnemy != null) h += 32f;
            h += 28f;
            h += 28f + cardBodyStyle.CalcHeight(new GUIContent(r.Gain), bodyW) + 16f;
            h += 28f + cardBodyStyle.CalcHeight(new GUIContent(r.Cost), bodyW) + 40f;
            return h;
        }

        /// <summary>Single-line text that shrinks its font until it fits the rect's width.</summary>
        void FitLabel(Rect r, string text, GUIStyle style, Color color)
        {
            int size = style.fontSize;
            bool wrap = style.wordWrap;
            style.wordWrap = false;
            var content = new GUIContent(text);
            while (style.fontSize > 16 && style.CalcSize(content).x > r.width) style.fontSize -= 2;
            Label(r, text, style, color);
            style.fontSize = size;
            style.wordWrap = wrap;
        }

        /// <summary>What the enemies have learned about you: shown between rooms so adaptation is never hidden.</summary>
        void DrawReading(Rect r)
        {
            kit.InkPanel(new Rect(r.x - 30f, r.y - 14f, r.width + 60f, r.height + 28f), 0.95f);
            Label(new Rect(r.x + 30f, r.y + 10f, r.width - 60f, 32f), Strings.ReadingTitle, smallStyle, Pal.adapted);
            List<ReadingId> active = game.World.Adaptation.Active();
            if (active.Count == 0)
            {
                Label(new Rect(r.x + 30f, r.y + 50f, r.width - 60f, 32f), Strings.ReadingNone, cardBodyStyle, UiKit.PaperDim);
                return;
            }
            float y = r.y + 46f;
            foreach (ReadingId id in active)
            {
                Label(new Rect(r.x + 30f, y, 220f, 30f), Strings.ReadingName(id), smallStyle, Pal.adapted);
                Label(new Rect(r.x + 250f, y, r.width - 280f, 30f), Strings.ReadingText(id), cardBodyStyle, UiKit.PaperText);
                y += 30f;
            }
        }

        GUIStyle smallStyleCenter;

        // ── Death ─────────────────────────────────────────────────────────────────────

        void DrawEnd(string title, string subtitle)
        {
            DimGame(1.15f);
            // A vermilion splash behind the verdict.
            kit.Splat(1, new Rect(VW * 0.5f - 330f, 0f, 660f, 360f), UiKit.Shu.WithAlpha(0.85f));
            Label(new Rect(0, 90, VW, 130), UiKit.Sentence(title), deathTitleStyle, UiKit.PaperText);
            Label(new Rect(0, 222, VW, 40), subtitle, bodyCenterStyle, UiKit.PaperDim);

            RunState run = game.Run;
            if (run != null)
            {
                var panel = new Rect((VW - 760f) * 0.5f, 280f, 760f, 470f);
                kit.PaperPanel(panel);
                string[][] rows =
                {
                    new[] { Strings.StatRooms, run.RoomsCleared.ToString() },
                    new[] { Strings.StatRecord, game.Meta.bestRoom.ToString() },
                    new[] { Strings.StatParries, run.PerfectParries.ToString() },
                    new[] { Strings.StatSwaps, run.SwapParries.ToString() },
                    new[] { Strings.StatDodges, run.PerfectDodges.ToString() },
                    new[] { Strings.StatCounters, run.Counters.ToString() },
                    new[] { Strings.StatFinishers, run.Finishers.ToString() },
                    new[] { Strings.StatTechniques, run.TechniquesAbsorbed.ToString() },
                    new[] { Strings.StatBestStreak, run.BestStreak.ToString() },
                    new[] { Strings.StatTime, FormatTime(run.Elapsed) },
                    new[] { Strings.StatSeed, run.Seed.ToString() },
                };
                float y = panel.y + 34;
                foreach (string[] row in rows)
                {
                    Label(new Rect(panel.x + 70, y, 420, 36), row[0], bodyStyle, UiKit.InkSoft);
                    Label(new Rect(panel.x + 400, y, 290, 36), row[1], bodyRightStyle, UiKit.Ink);
                    y += 36;
                }
                Label(new Rect(0, 766f, VW, 44f), string.Format(Strings.EcosEarned, run.EcosBanked, game.Meta.ecos), ecosSmallStyle, Pal.gold);
            }
            DrawMenuList(818f, 64f, 560f, VW * 0.5f, false);
        }

        GUIStyle bodyRightStyle;

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ── HUD ───────────────────────────────────────────────────────────────────────

        void DrawHud()
        {
            RunState run = game.Run;
            PlayerController p = game.World.Player;
            if (run == null || p == null) return;

            const float x = 52f, y = 30f, w = 440f;
            kit.InkPanel(new Rect(10f, 2f, 560f, 270f), 0.82f);

            Label(new Rect(x, y, w, 26), Strings.HudHealth, capsStyle, UiKit.PaperDim);
            var hpRect = new Rect(x - 8f, y + 24, w + 16f, 40);
            float hpRatio = Mathf.Clamp01(p.Health / p.MaxHealth);
            float ghostRatio = Mathf.Clamp01(ghostHp / p.MaxHealth);
            kit.Bar(hpRect, hpRatio, UiKit.ShuBright, Color.black.WithAlpha(0.7f), ghostRatio, UiKit.PaperText.WithAlpha(0.8f));
            Label(new Rect(x, y, w, 26), Mathf.CeilToInt(p.Health) + " / " + Mathf.RoundToInt(p.MaxHealth), smallRightStyle, UiKit.PaperText);

            float pr = p.Posture.Ratio;
            Color pc = Color.Lerp(Pal.postureLow, Pal.postureHigh, pr);
            if (pr > 0.8f) pc = Color.Lerp(pc, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f));
            Label(new Rect(x, y + 70, w, 26), Strings.HudPosture, capsStyle, UiKit.PaperDim);
            kit.Bar(new Rect(x - 8f, y + 92, w + 16f, 28), pr, pc, Color.black.WithAlpha(0.7f));

            Label(new Rect(x, y + 126, w, 26), Strings.HudDodge, capsStyle, UiKit.PaperDim);
            for (int i = 0; i < p.MaxDodgeCharges; i++)
            {
                var r = new Rect(x + 140f + i * 40f, y + 120, 36f, 36f);
                float fill = Mathf.Clamp01(p.DodgeCharges - i);
                kit.Dab(r, Color.black.WithAlpha(0.6f));
                if (fill > 0f) kit.Dab(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Pal.playerAccent.WithAlpha(fill >= 1f ? 1f : 0.35f));
            }

            RoomDef room = game.World.CurrentRoom;
            if (room != null)
            {
                Color rc = room.guardian ? Pal.gold : UiKit.PaperText;
                Label(new Rect(0, 8, VW, 60), string.Format(Strings.HudRoom, run.RoomIndex + 1, room.name), roomStyle, rc);
                kit.Underline(new Rect(VW * 0.5f - 230f, 62f, 460f, 16f), rc.WithAlpha(0.55f));
                int wave = Mathf.Max(1, game.World.WaveIndex + 1);
                Label(new Rect(0, 76, VW, 30), string.Format(Strings.HudWave, wave, room.waves.Length), smallStyleCenter, UiKit.PaperDim);
                List<ReadingId> reading = game.World.Adaptation.Active();
                if (reading.Count > 0)
                {
                    var names = new string[reading.Count];
                    for (int i = 0; i < reading.Count; i++) names[i] = Strings.ReadingName(reading[i]);
                    Label(new Rect(0, 104, VW, 30), string.Format(Strings.HudReading, string.Join("  ·  ", names)), smallStyleCenter, Pal.adapted);
                }
            }

            DrawFlow(run.Flow);
            DrawWeaponsHud(run, x, y + 168f);
            DrawParty();
        }

        void DrawFlow(FlowMeter flow)
        {
            const float w = 360f;
            float fx = VW - 52f - w, fy = 30f;
            kit.InkPanel(new Rect(VW - 470f, 2f, 460f, 230f), 0.82f);
            int level = flow.Level;
            Label(new Rect(fx, fy, w, 26), Strings.HudFlow, capsRightStyle, level > 0 ? Pal.gold : UiKit.PaperDim);
            // Each Flow level is an ensō; lit ones are gold with a filled heart.
            for (int i = 0; i < flow.MaxLevel; i++)
            {
                float cx = fx + w - 34f - (flow.MaxLevel - 1 - i) * 76f;
                var r = new Rect(cx - 34f, fy + 30f, 68f, 68f);
                bool lit = level > i;
                float spin = lit ? Time.unscaledTime * 20f + i * 40f : i * 50f;
                kit.Enso(r, lit ? Pal.gold : UiKit.PaperText.WithAlpha(0.28f), spin);
                if (lit) kit.Dab(new Rect(r.center.x - 13f, r.center.y - 13f, 26f, 26f), Pal.gold);
            }
            kit.Bar(new Rect(fx + w - 230f, fy + 104f, 230f, 22f), flow.ProgressInLevel, Pal.gold, Color.black.WithAlpha(0.6f));
            Label(new Rect(fx, fy + 128f, w, 40f), string.Format(Strings.HudEcos, MetaRules.EcosForRun(game.Run)), ecosHudStyle, Pal.gold.WithAlpha(0.9f));

            int streak = game.Run.PerfectStreak;
            if (streak >= 2)
            {
                // Pops on every new perfect action; brighter the longer it runs.
                // Font-size pop (pivot-based GUI scaling misplaces under the letterbox matrix).
                Color sc = Color.Lerp(UiKit.PaperText, Pal.gold, Mathf.Clamp01(streak / 10f));
                streakStyle.fontSize = Mathf.RoundToInt(34f * (1f + 0.4f * streakPop));
                Label(new Rect(fx - 40f, fy + 210f, w + 40f, 56f), string.Format(Strings.HudStreak, streak), streakStyle, sc);
            }
        }

        // ── HUD weapons ───────────────────────────────────────────────────────────────

        void DrawWeaponsHud(RunState run, float x, float y)
        {
            DrawWeaponArt(run.CurrentWeapon, new Rect(x - 6f, y + 4f, 70f, 30f), Color.white);
            Label(new Rect(x + 76f, y - 4f, 400f, 40f), run.CurrentWeapon.name, weaponHudStyle, UiKit.PaperText);
            Label(new Rect(x + 76f, y + 32f, 400f, 26f), "‹›  " + run.OtherWeapon.name + "  (" + (game.Controls.UsingGamepad ? "Y" : "Q") + ")", smallStyle, UiKit.PaperDim);
        }

        // ── Arsenal ───────────────────────────────────────────────────────────────────

        const int ArsenalStartIndex = 6;
        const int ArsenalBackIndex = 7;

        void HandleArsenal(InputService input)
        {
            if (input.NavX != 0 && arsenalIndex < 6)
            {
                int row = arsenalIndex / 3;
                arsenalIndex = row * 3 + (arsenalIndex % 3 + input.NavX + 3) % 3;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.NavY != 0)
            {
                arsenalIndex = input.NavY < 0
                    ? (arsenalIndex < 3 ? arsenalIndex + 3 : arsenalIndex < 6 ? ArsenalStartIndex : ArsenalBackIndex)
                    : (arsenalIndex == ArsenalBackIndex ? ArsenalStartIndex : arsenalIndex == ArsenalStartIndex ? 4 : arsenalIndex >= 3 ? arsenalIndex - 3 : arsenalIndex);
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) ActivateArsenal(arsenalIndex);
            else if (input.Cancel) game.GoToMainMenu();
        }

        void ActivateArsenal(int index)
        {
            if (index == ArsenalBackIndex)
            {
                game.GoToMainMenu();
                return;
            }
            if (index == ArsenalStartIndex)
            {
                if (arsenalPicks.Count == 2) game.ConfirmArsenal(arsenalPicks[0], arsenalPicks[1]);
                else game.Audio.Play(Sfx.Denied, 0.6f, 1f, 0f);
                return;
            }
            string id = WeaponDefs.All[index].id;
            if (arsenalPicks.Contains(id)) arsenalPicks.Remove(id);
            else if (arsenalPicks.Count < 2) arsenalPicks.Add(id);
            else arsenalPicks[1] = id;
            game.Audio.Play(Sfx.Unsheathe, 0.5f, 1.2f, 0f);
        }

        void DrawArsenal()
        {
            DrawRect(FullScreen, UiKit.Ink.WithAlpha(0.42f));
            Heading(new Rect(0, 10, VW, 100), Strings.ArsenalTitle, UiKit.PaperText, UiKit.Shu.WithAlpha(0.9f));
            Label(new Rect(0, 112, VW, 40), Strings.ArsenalSubtitle, taglineStyle, UiKit.PaperText);

            const float cardW = 560f, cardH = 322f, gap = 26f;
            float startX = (VW - (3 * cardW + 2 * gap)) * 0.5f;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int i = 0; i < WeaponDefs.All.Length; i++)
            {
                WeaponDef w = WeaponDefs.All[i];
                bool selected = i == arsenalIndex;
                float lift = selected ? -10f * CombatMath.EaseOutCubic(selAnim) : 0f;
                var r = new Rect(startX + (i % 3) * (cardW + gap), 166f + (i / 3) * (cardH + gap) + lift, cardW, cardH);
                if (ArsenalMouse(r, i, e, mouseMoved)) return;
                int pick = arsenalPicks.IndexOf(w.id);
                kit.PaperPanel(r, selected || pick >= 0 ? 1f : 0.92f);

                // The weapon lies on an ink swatch; chosen weapons get a vermilion one.
                var swatch = new Rect(r.x + 26f, r.y + 34f, 240f, 86f);
                kit.Stroke(swatch, (pick >= 0 ? UiKit.Shu : UiKit.Ink).WithAlpha(selected ? 0.95f : 0.82f));
                DrawWeaponArt(w, new Rect(swatch.x + 30f, swatch.y + 18f, 180f, 50f), Color.white);
                if (w.dual) DrawWeaponArt(w, new Rect(swatch.x + 50f, swatch.y + 30f, 180f, 50f), new Color(0.85f, 0.85f, 0.85f, 1f));
                FitLabel(new Rect(r.x + 280f, r.y + 34f, cardW - 370f, 86f), w.name, cardNameLeftStyle, pick >= 0 ? UiKit.Shu : UiKit.Ink);
                if (pick >= 0)
                {
                    var dab = new Rect(r.xMax - 92f, r.y + 22f, 60f, 60f);
                    kit.Dab(dab, UiKit.Shu);
                    Label(dab, (pick + 1).ToString(), pickStyle, UiKit.PaperText);
                }
                Label(new Rect(r.x + 36f, r.y + 126f, cardW - 72f, 70f), w.description, cardBodyStyle, UiKit.Ink);
                Label(new Rect(r.x + 36f, r.y + 204f, cardW - 72f, 28f), string.Format(Strings.WeaponCombo, w.combo.Length), smallStyle, UiKit.InkSoft);
                Label(new Rect(r.x + 36f, r.y + 232f, cardW - 72f, 28f), string.Format(Strings.WeaponSwapStrike, w.swapStrike.name), smallStyle, UiKit.Ai);
                Label(new Rect(r.x + 36f, r.y + 260f, cardW - 72f, 28f), string.Format(Strings.WeaponFinisher, w.finisherName), smallStyle, UiKit.Shu);
                if (selected) kit.Underline(new Rect(r.x + 40f, r.yMax - 34f, cardW - 80f, 16f), UiKit.Shu.WithAlpha(0.8f));
            }

            var start = new Rect((VW - 560f) * 0.5f, 872f, 560f, 88f);
            var back = new Rect((VW - 400f) * 0.5f, 964f, 400f, 68f);
            if (ArsenalMouse(start, ArsenalStartIndex, e, mouseMoved) || ArsenalMouse(back, ArsenalBackIndex, e, mouseMoved)) return;
            bool ready = arsenalPicks.Count == 2;
            bool onStart = arsenalIndex == ArsenalStartIndex;
            bool onBack = arsenalIndex == ArsenalBackIndex;
            kit.Stroke(new Rect(start.x - 40f, start.y - 10f, start.width + 80f, start.height + 20f), (onStart && ready ? UiKit.Shu : UiKit.Ink).WithAlpha(ready ? 0.95f : 0.5f));
            Label(start, ready ? Strings.ArsenalStart : Strings.ArsenalNeedTwo, itemStyle, ready ? UiKit.PaperText : UiKit.PaperDim);
            if (onBack) kit.Stroke(new Rect(back.x - 30f, back.y - 6f, back.width + 60f, back.height + 12f), UiKit.Shu.WithAlpha(0.95f), CombatMath.EaseOutCubic(selAnim));
            Label(back, Strings.Back, itemStyle, UiKit.PaperText);
        }

        bool ArsenalMouse(Rect r, int index, Event e, bool mouseMoved)
        {
            if (game.Launch.Automated || !r.Contains(e.mousePosition)) return false;
            if (e.type == EventType.Repaint && mouseMoved) arsenalIndex = index;
            if (e.type != EventType.MouseDown || e.button != 0) return false;
            arsenalIndex = index;
            e.Use();
            ActivateArsenal(index);
            return true;
        }

        // ── Equipment ─────────────────────────────────────────────────────────────────

        int EquipmentEntries => 4 + game.Run.Bag.Count;

        void HandleEquipment(InputService input)
        {
            if (game.Run == null) return;
            int n = EquipmentEntries;
            if (input.NavY != 0)
            {
                equipIndex = (equipIndex - input.NavY + n) % n;
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.Submit) ActivateEquipment(equipIndex);
            else if (input.Cancel) game.CloseEquipment();
            equipIndex = Mathf.Clamp(equipIndex, 0, EquipmentEntries - 1);
        }

        void ActivateEquipment(int index)
        {
            RunState run = game.Run;
            if (index < 4) game.UnequipItem((ItemSlot)index);
            else if (index - 4 < run.Bag.Count) game.EquipItem(run.Bag[index - 4]);
        }

        Item EquipmentItemAt(int index)
        {
            RunState run = game.Run;
            if (index < 4) return run.Equipped[index];
            return index - 4 < run.Bag.Count ? run.Bag[index - 4] : null;
        }

        void DrawEquipment()
        {
            RunState run = game.Run;
            if (run == null) return;
            DimGame(1.2f);
            Heading(new Rect(0, 12, VW, 96), Strings.EquipmentTitle, UiKit.PaperText, UiKit.Shu, 520f);

            DrawCharacterPanel(run, new Rect(40f, 120f, 640f, 860f));

            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            var slotsPanel = new Rect(700f, 120f, 580f, 340f);
            kit.PaperPanel(slotsPanel);
            Label(new Rect(slotsPanel.x + 50f, slotsPanel.y + 34f, 400f, 30f), Strings.EquipmentSlots, capsStyle, UiKit.Shu);
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(slotsPanel.x + 40f, slotsPanel.y + 70f + i * 62f, slotsPanel.width - 80f, 58f);
                if (EquipmentMouse(r, i, e, mouseMoved)) return;
                DrawItemRow(r, run.Equipped[i], ItemGenerator.SlotName((ItemSlot)i), i == equipIndex);
            }

            var bagPanel = new Rect(700f, 476f, 580f, 504f);
            kit.PaperPanel(bagPanel);
            Label(new Rect(bagPanel.x + 50f, bagPanel.y + 34f, 500f, 30f), Strings.EquipmentBag + "   " + string.Format(Strings.EquipmentBagCount, run.Bag.Count, RunState.BagCapacity), capsStyle, UiKit.Shu);
            for (int i = 0; i < run.Bag.Count; i++)
            {
                var r = new Rect(bagPanel.x + 40f, bagPanel.y + 70f + i * 34f, bagPanel.width - 80f, 32f);
                if (EquipmentMouse(r, 4 + i, e, mouseMoved)) return;
                DrawItemRow(r, run.Bag[i], null, 4 + i == equipIndex);
            }

            DrawItemTooltip(EquipmentItemAt(equipIndex), new Rect(1300f, 120f, 580f, 860f));
            Label(new Rect(0, VH - 52f, VW, 36f), Strings.EquipmentControls, smallStyleCenter, UiKit.PaperDim);
        }

        bool EquipmentMouse(Rect r, int index, Event e, bool mouseMoved)
        {
            if (game.Launch.Automated || !r.Contains(e.mousePosition)) return false;
            if (e.type == EventType.Repaint && mouseMoved) equipIndex = index;
            if (e.type != EventType.MouseDown || e.button != 0) return false;
            equipIndex = index;
            e.Use();
            ActivateEquipment(index);
            return true;
        }

        void DrawItemRow(Rect r, Item item, string emptyLabel, bool selected)
        {
            if (selected) kit.Stroke(new Rect(r.x - 24f, r.y - 8f, r.width + 48f, r.height + 16f), UiKit.Shu.WithAlpha(0.92f), CombatMath.EaseOutCubic(selAnim));
            float icon = Mathf.Min(r.height - 6f, 46f);
            var iconRect = new Rect(r.x + 8f, r.y + (r.height - icon) * 0.5f, icon, icon);
            var textRect = new Rect(r.x + icon + 22f, r.y, r.width - icon - 30f, r.height);
            if (item == null)
            {
                kit.Enso(iconRect, (selected ? UiKit.PaperText : UiKit.Ink).WithAlpha(0.35f));
                Label(textRect, (emptyLabel ?? string.Empty) + "  " + Strings.EquipmentEmpty, smallStyle, selected ? UiKit.PaperText : UiKit.InkSoft);
                return;
            }
            DrawItemIcon(iconRect, item);
            Label(textRect, item.Name, smallStyle, selected ? UiKit.PaperText : UiKit.OnPaper(Rarities.Color(item.rarity)));
        }

        /// <summary>Pixel-art item on a rarity-coloured ink dab.</summary>
        void DrawItemIcon(Rect r, Item item)
        {
            Color rc = Rarities.Color(item.rarity);
            kit.Dab(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), rc.WithAlpha(0.55f + 0.03f * (int)item.rarity));
            Sprite icon = PixelArt.ItemIcon(item.slot, item.element, item.rarity);
            if (icon != null) DrawTex(r, icon.texture, Color.white);
            else DrawTex(r, ShapeLibrary.Get(ItemGenerator.IconShape(item.slot)).texture, Elements.Color(item.element));
            if (item.rarity >= Rarity.Mythic)
            {
                float spin = Time.unscaledTime * 40f;
                kit.Enso(new Rect(r.x - 10f, r.y - 10f, r.width + 20f, r.height + 20f), rc.WithAlpha(0.8f), spin);
            }
        }

        void DrawItemTooltip(Item item, Rect r)
        {
            kit.PaperPanel(r);
            if (item == null)
            {
                Label(new Rect(r.x + 60f, r.y + 70f, r.width - 120f, 120f), Strings.EquipmentPickHint, cardBodyStyle, UiKit.InkSoft);
                kit.Enso(new Rect(r.center.x - 110f, r.y + 260f, 220f, 220f), UiKit.Ink.WithAlpha(0.15f));
                return;
            }
            Color rc = Rarities.Color(item.rarity);
            Color rcInk = UiKit.OnPaper(rc);
            kit.Enso(new Rect(r.center.x - 100f, r.y + 36f, 200f, 200f), rcInk.WithAlpha(0.8f));
            DrawItemIcon(new Rect(r.center.x - 56f, r.y + 80f, 112f, 112f), item);
            Label(new Rect(r.x + 40f, r.y + 244f, r.width - 80f, 60f), item.Name, cardNameStyle, UiKit.Ink);
            Label(new Rect(r.x + 40f, r.y + 306f, r.width - 80f, 30f), string.Format(Strings.Rarity, Rarities.Name(item.rarity)), smallStyleCenter, rcInk);
            Label(new Rect(r.x + 40f, r.y + 336f, r.width - 80f, 30f), string.Format(Strings.ElementLine, ItemGenerator.SlotName(item.slot), Elements.Name(item.element)), smallStyleCenter, UiKit.OnPaper(Elements.Color(item.element)));
            kit.Underline(new Rect(r.x + 60f, r.y + 378f, r.width - 120f, 16f), UiKit.Ink.WithAlpha(0.45f));
            Label(new Rect(r.x + 60f, r.y + 400f, r.width - 120f, 30f), Strings.Gain, capsStyle, UiKit.Shu);
            Label(new Rect(r.x + 60f, r.y + 434f, r.width - 120f, 380f), item.Gain, cardBodyStyle, UiKit.Ink);
        }

        void DrawCharacterPanel(RunState run, Rect r)
        {
            kit.PaperPanel(r);
            PlayerController p = game.World.Player;

            // The duelist inside an ensō, its two blades above and below.
            var centre = new Vector2(r.center.x, r.y + 330f);
            Label(new Rect(r.x, r.y + 34f, r.width, 30f), Strings.EquipmentWheel, capsCenterStyle, UiKit.Shu);
            kit.Enso(new Rect(centre.x - 200f, centre.y - 200f, 400f, 400f), UiKit.Ink.WithAlpha(0.9f), Mathf.Sin(Time.unscaledTime * 0.4f) * 6f);
            kit.Dab(new Rect(centre.x - 120f, centre.y - 120f, 240f, 240f), Pal.gold.WithAlpha(0.10f + 0.04f * run.Flow.Level));
            DrawCharacterArt(new Rect(centre.x - 80f, centre.y - 80f, 160f, 160f));
            for (int i = 0; i < 4; i++)
            {
                Item item = run.Equipped[i];
                if (item == null) continue;
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                var pos = centre + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 118f;
                DrawItemIcon(new Rect(pos.x - 22f, pos.y - 22f, 44f, 44f), item);
            }
            DrawWheelWeapon(run.CurrentWeapon, new Rect(centre.x - 170f, centre.y - 262f, 340f, 56f), true);
            DrawWheelWeapon(run.OtherWeapon, new Rect(centre.x - 170f, centre.y + 208f, 340f, 56f), false);

            Label(new Rect(r.x + 50f, r.y + 600f, r.width - 100f, 30f), Strings.EquipmentStats, capsStyle, UiKit.Shu);
            PlayerModifiers m = run.Modifiers;
            string[][] stats =
            {
                new[] { "Vida", Mathf.CeilToInt(p.Health) + " / " + Mathf.RoundToInt(p.MaxHealth) },
                new[] { "Postura máxima", Mathf.RoundToInt(p.Posture.Max).ToString() },
                new[] { "Dano", "x" + p.DamageDealtMult.ToString("0.00") },
                new[] { "Crítico · Roubo de vida", Mathf.RoundToInt(m.critChance * 100f) + "% · " + Mathf.RoundToInt(m.lifesteal * 100f) + "%" },
                new[] { "Janela de parry", p.PerfectParryFrames + " frames" },
                new[] { "Ascensão · Técnicas", run.Ascension + " · " + run.Owned.Count },
            };
            for (int i = 0; i < stats.Length; i++)
            {
                float y = r.y + 634f + i * 30f;
                Label(new Rect(r.x + 50f, y, 320f, 30f), stats[i][0], smallStyle, UiKit.InkSoft);
                Label(new Rect(r.x + 330f, y, r.width - 380f, 30f), stats[i][1], smallRightStyle, UiKit.Ink);
            }
            string owned = string.Empty;
            foreach (RewardDef d in run.Owned) owned += (owned.Length > 0 ? "  ·  " : string.Empty) + d.Name;
            if (owned.Length > 0) Label(new Rect(r.x + 50f, r.y + 818f, r.width - 100f, 40f), owned, cardBodyStyle, UiKit.InkSoft);
        }

        /// <summary>A texture scaled to fit (whole pixels stay square) and centred in the rect.</summary>
        void DrawFitted(Rect r, Texture tex, Color color)
        {
            float k = Mathf.Min(r.width / tex.width, r.height / tex.height);
            var fit = new Rect(r.center.x - tex.width * k * 0.5f, r.center.y - tex.height * k * 0.5f, tex.width * k, tex.height * k);
            DrawTex(fit, tex, color);
        }

        void DrawWeaponArt(WeaponDef w, Rect r, Color color)
        {
            Sprite s = PlayerController.WeaponSprite(w);
            // Pixel-art weapons have their own small texture; the old shapes share big ones.
            if (s.texture.width <= 64) DrawFitted(r, s.texture, color);
            else DrawTex(r, s.texture, w.color * color);
        }

        /// <summary>The duelist walking toward the viewer, large, for the equipment screen.</summary>
        void DrawCharacterArt(Rect r)
        {
            CharacterArt art = PixelArt.Character(PlayerController.PlayerArt);
            if (art == null)
            {
                DrawTex(r, ShapeLibrary.Get(Shape.Circle).texture, Pal.player);
                return;
            }
            // Cut from the sheet (IMGUI draws whole textures, so use UVs).
            Sprite[] walk = art.walk[(int)ArtDir.Down];
            Sprite frame = walk[Mathf.FloorToInt(Time.unscaledTime * 6f) % walk.Length];
            Rect tr = frame.textureRect;
            Texture tex = frame.texture;
            float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
            var fit = new Rect(r.center.x - tr.width * k * 0.5f, r.center.y - tr.height * k * 0.5f, tr.width * k, tr.height * k);
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            Color old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(fit, tex, uv);
            GUI.color = old;
        }

        void DrawWheelWeapon(WeaponDef w, Rect r, bool active)
        {
            if (active) kit.Stroke(new Rect(r.x - 20f, r.y - 8f, r.width + 40f, r.height + 16f), UiKit.Ink.WithAlpha(0.9f));
            DrawWeaponArt(w, new Rect(r.x + 10f, r.y + 10f, 100f, 36f), active ? Color.white : new Color(0.55f, 0.5f, 0.45f, 1f));
            Label(new Rect(r.x + 118f, r.y, r.width - 118f, r.height), w.name, weaponHudStyle, active ? UiKit.PaperText : UiKit.InkSoft);
        }

        void DrawPopups()
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            foreach (FloatingText p in popups)
            {
                float t = p.age / p.life;
                Vector2 world = p.world + Vector2.up * (0.5f + t * 1.1f);
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
                float gx = (sp.x - offset.x) / scale;
                float gy = (Screen.height - sp.y - offset.y) / scale;
                float alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                Label(new Rect(gx - 260f, gy - 24f, 520f, 48f), p.text, p.small ? damageStyle : popupStyle, p.color.WithAlpha(alpha));
            }
        }

        void DrawToasts()
        {
            // During play toasts sit under the room title; on full screens they move to the
            // bottom so they never cover the screen's own heading. Each rides an ink stroke.
            bool playing = game.CurrentMode == Game.Mode.Playing;
            for (int i = 0; i < toasts.Count; i++)
            {
                FloatingText t = toasts[i];
                float alpha = Mathf.Clamp01((t.life - t.age) / 0.5f) * Mathf.Clamp01(t.age / 0.15f);
                float y = playing ? 150f + i * 54f : VH - 120f - i * 54f;
                float tw = toastStyle.CalcSize(new GUIContent(t.text)).x;
                float sw = Mathf.Min(VW - 100f, tw + 160f);
                kit.Stroke(new Rect(VW * 0.5f - sw * 0.5f, y - 10f, sw, 68f), UiKit.Ink.WithAlpha(0.8f * alpha), Mathf.Clamp01(t.age / 0.2f));
                Label(new Rect(0, y, VW, 48), t.text, toastStyle, t.color.WithAlpha(alpha));
            }
        }

        void DrawHint()
        {
            if (string.IsNullOrEmpty(hint) || hintTimer <= 0f) return;
            float alpha = Mathf.Clamp01(hintTimer / 0.6f);
            var r = new Rect(230f, VH - 190f, VW - 460f, 150f);
            kit.PaperPanel(r, alpha);
            kit.Seal(new Rect(r.x + 44f, r.y + 39f, 72f, 72f), 0.9f * alpha);
            Label(new Rect(r.x + 140, r.y + 22, r.width - 200, r.height - 44), hint, hintStyle, UiKit.Ink.WithAlpha(alpha));
        }

        void DrawOverlays()
        {
            if (hurtAlpha > 0.001f) kit.InkVignette(FullScreen, Pal.danger.WithAlpha(Mathf.Clamp01(hurtAlpha * 1.6f)));
            if (flashAlpha > 0.001f) DrawRect(FullScreen, flashColor.WithAlpha(flashAlpha));
        }

        // ── Primitives ─────────────────────────────────────────────────────────────────

        static void DrawTex(Rect r, Texture tex, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = prev;
        }

        void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = prev;
        }

        /// <summary>Light text gets a soft ink shadow over the game; dark ink on paper needs none.</summary>
        static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = style.normal.textColor;
            float luminance = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
            if (luminance > 0.62f)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * color.a);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = prev;
        }

        GUIStyle latinTitleStyle, taglineStyle, taglineSmallStyle, cardKindStyle, cardNameLeftStyle, pickStyle, deathTitleStyle,
                 ecosSmallStyle, ecosHudStyle, capsStyle, capsRightStyle, capsCenterStyle, weaponHudStyle, urlStyle, hintStyleCenter;

        void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            Font brush = kit.Brush;
            Font text = kit.Text;
            Font bold = kit.TextBold;
            titleStyle = MakeStyle(150, TextAnchor.MiddleCenter, brush);
            latinTitleStyle = MakeStyle(78, TextAnchor.MiddleCenter, brush);
            taglineStyle = MakeStyle(30, TextAnchor.MiddleCenter, text, FontStyle.Italic);
            taglineSmallStyle = MakeStyle(22, TextAnchor.MiddleCenter, text, FontStyle.Italic);
            headerStyle = MakeStyle(76, TextAnchor.MiddleCenter, brush);
            deathTitleStyle = MakeStyle(118, TextAnchor.MiddleCenter, brush);
            itemStyle = MakeStyle(36, TextAnchor.MiddleCenter, bold);
            itemLeftStyle = MakeStyle(32, TextAnchor.MiddleLeft, bold);
            bodyStyle = MakeStyle(28, TextAnchor.MiddleLeft, text);
            bodyRightStyle = MakeStyle(28, TextAnchor.MiddleRight, bold);
            bodyCenterStyle = MakeStyle(28, TextAnchor.UpperCenter, text);
            smallStyle = MakeStyle(22, TextAnchor.MiddleLeft, bold);
            smallRightStyle = MakeStyle(22, TextAnchor.MiddleRight, bold);
            smallStyleCenter = MakeStyle(22, TextAnchor.MiddleCenter, bold);
            capsStyle = MakeStyle(19, TextAnchor.MiddleLeft, bold);
            capsRightStyle = MakeStyle(19, TextAnchor.MiddleRight, bold);
            capsCenterStyle = MakeStyle(19, TextAnchor.MiddleCenter, bold);
            popupStyle = MakeStyle(36, TextAnchor.MiddleCenter, brush);
            cardKindStyle = MakeStyle(34, TextAnchor.MiddleCenter, brush);
            cardNameStyle = MakeStyle(42, TextAnchor.UpperCenter, brush);
            cardNameLeftStyle = MakeStyle(40, TextAnchor.MiddleLeft, brush);
            pickStyle = MakeStyle(34, TextAnchor.MiddleCenter, brush);
            cardBodyStyle = MakeStyle(24, TextAnchor.UpperLeft, text);
            hintStyle = MakeStyle(27, TextAnchor.MiddleLeft, text);
            toastStyle = MakeStyle(30, TextAnchor.MiddleCenter, bold);
            roomStyle = MakeStyle(44, TextAnchor.MiddleCenter, brush);
            ecosStyle = MakeStyle(52, TextAnchor.MiddleCenter, brush);
            ecosSmallStyle = MakeStyle(36, TextAnchor.MiddleCenter, brush);
            ecosHudStyle = MakeStyle(30, TextAnchor.MiddleRight, brush);
            damageStyle = MakeStyle(30, TextAnchor.MiddleCenter, brush);
            streakStyle = MakeStyle(34, TextAnchor.MiddleRight, brush);
            weaponHudStyle = MakeStyle(32, TextAnchor.MiddleLeft, brush);
            urlStyle = MakeStyle(36, TextAnchor.MiddleLeft, bold);
            hintStyleCenter = MakeStyle(27, TextAnchor.MiddleCenter, text);
        }

        static GUIStyle MakeStyle(int size, TextAnchor anchor, Font font, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                fontStyle = font != null ? fontStyle : FontStyle.Bold,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
            };
            if (font != null) style.font = font;
            return style;
        }
    }
}
