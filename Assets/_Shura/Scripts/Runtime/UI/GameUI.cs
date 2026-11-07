using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Prototype UI drawn with IMGUI on a virtual 1920x1080 canvas (letterboxed). Navigation
    /// is read in Update through InputService, so keyboard, mouse and gamepad all work; OnGUI
    /// only draws and handles mouse hover/click. Replaced by the final UI in the polish milestone.
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
            if (game.Settings.reduceFlashes) strength *= 0.2f;
            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, strength);
        }

        public void HurtVignette()
        {
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
                case Game.Mode.MainMenu:
                    AddItem(Strings.Play, game.OpenArsenal);
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

        void HandleReward(InputService input)
        {
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
            }
        }

        // ── Drawing ────────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            scale = Mathf.Min(Screen.width / VW, Screen.height / VH);
            offset = new Vector2((Screen.width - VW * scale) * 0.5f, (Screen.height - VH * scale) * 0.5f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            Game.Mode mode = game.CurrentMode;
            bool inRun = mode == Game.Mode.Playing || mode == Game.Mode.Paused || mode == Game.Mode.Reward;
            DrawTex(FullScreen, vignette, new Color(1f, 1f, 1f, 0.85f));
            if (inRun && game.Run != null && game.Run.Flow.Level >= game.Run.Flow.MaxLevel)
            {
                // Full Flow: the screen edges burn gold.
                float pulse = 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
                DrawTex(FullScreen, vignette, Pal.gold.WithAlpha(pulse));
            }
            if (inRun)
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
                case Game.Mode.GameOver: DrawEnd(Strings.DeathTitle, Strings.DeathSubtitle, Pal.danger); break;
                case Game.Mode.Altar: DrawAltar(); break;
                case Game.Mode.Arsenal: DrawArsenal(); break;
                case Game.Mode.Equipment: DrawEquipment(); break;
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

        void DrawTitle()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.55f));
            DrawTitleGlow(360f);
            Label(new Rect(0, 250, VW, 220), Strings.Title, titleStyle, Pal.uiText);
            Label(new Rect(0, 480, VW, 50), Strings.Tagline, bodyCenterStyle, Pal.gold);
            float blink = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            Label(new Rect(0, 760, VW, 50), Strings.PressAnyButton, itemStyle, Pal.uiText.WithAlpha(blink));
            Label(new Rect(VW - 460, VH - 60, 420, 40), Strings.BuildLabel, smallRightStyle, Pal.uiDim);
        }

        void DrawMainMenu()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.65f));
            DrawTitleGlow(250f);
            Label(new Rect(0, 150, VW, 200), Strings.Title, titleStyle, Pal.uiText);
            DrawMenuList(470f, 72f, 640f);
            Label(new Rect(VW - 460, VH - 60, 420, 40), Strings.BuildLabel, smallRightStyle, Pal.uiDim);
        }

        void DrawTitleGlow(float centerY)
        {
            Texture glow = ShapeLibrary.Get(Shape.SoftCircle).texture;
            float pulse = 0.14f + 0.05f * Mathf.Sin(Time.unscaledTime * 1.6f);
            DrawTex(new Rect(VW * 0.5f - 520f, centerY - 260f, 1040f, 520f), glow, Pal.gold.WithAlpha(pulse));
        }

        void DrawAltar()
        {
            MetaProgress meta = game.Meta;
            DrawRect(FullScreen, Pal.background.WithAlpha(0.88f));
            Label(new Rect(0, 60, VW, 90), Strings.AltarTitle, headerStyle, Pal.gold);
            Label(new Rect(0, 150, VW, 40), Strings.AltarSubtitle, bodyCenterStyle, Pal.uiDim);
            Label(new Rect(0, 200, VW, 64), string.Format(Strings.EcosBalance, meta.ecos), ecosStyle, Pal.gold);

            const float width = 1340f, rowH = 64f, top = 290f;
            float x = (VW - width) * 0.5f;
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
                    DrawRect(new Rect(r.x, r.y, r.width * CombatMath.EaseOutCubic(selAnim), r.height), Pal.uiText.WithAlpha(0.08f));
                    DrawRect(new Rect(r.x, r.y, 6f, r.height), Pal.gold);
                }

                if (i >= defs.Length)
                {
                    Label(r, items[i].label, itemStyle, selected ? Pal.gold : Pal.uiText);
                    continue;
                }

                MetaUpgradeDef def = defs[i];
                int level = meta.GetLevel(def.id);
                bool maxed = meta.IsMaxed(def);
                Label(new Rect(r.x + 30f, r.y, 400f, r.height), def.name, itemLeftStyle, selected ? Pal.gold : Pal.uiText);

                Texture pip = ShapeLibrary.Get(Shape.Diamond).texture;
                Texture pipEmpty = ShapeLibrary.Get(Shape.DiamondOutline).texture;
                for (int j = 0; j < def.maxLevel; j++)
                {
                    var pr = new Rect(r.x + 450f + j * 34f, r.y + r.height * 0.5f - 13f, 26f, 26f);
                    DrawTex(pr, j < level ? pip : pipEmpty, j < level ? Pal.gold : Pal.uiDim.WithAlpha(0.7f));
                }

                string effect = level > 0 ? def.EffectAt(level) : "—";
                if (!maxed) effect += "   >   " + def.EffectAt(level + 1);
                Label(new Rect(r.x + 650f, r.y, 480f, r.height), effect, smallStyle, Pal.uiText);

                string cost = maxed ? Strings.Max : string.Format(Strings.CostLabel, meta.NextCost(def));
                Color costColor = maxed ? Pal.gold : meta.CanBuy(def) ? Pal.uiText : Pal.danger;
                Label(new Rect(r.xMax - 230f, r.y, 200f, r.height), cost, bodyRightStyle, costColor);
            }

            float by = top + items.Count * rowH + 10f;
            if (menuIndex < defs.Length)
            {
                Label(new Rect(0, by, VW, 36), defs[menuIndex].description, bodyCenterStyle, Pal.uiText);
            }
            Label(new Rect(0, by + 44f, VW, 30), string.Format(Strings.AltarStats, meta.runs, meta.bestRoom), smallStyleCenter, Pal.uiDim);
            Label(new Rect(0, VH - 60f, VW, 32), Strings.AltarControls, smallStyleCenter, Pal.uiDim);
        }

        void DrawPause()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.7f));
            Label(new Rect(0, 170, VW, 90), Strings.Paused, headerStyle, Pal.uiText);
            DrawMenuList(330f, 72f, 640f);
            DrawControlsPanel(new Rect(560, 690, 800, 250));
        }

        void DrawSettings()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.8f));
            Label(new Rect(0, 100, VW, 90), Strings.SettingsTitle, headerStyle, Pal.uiText);
            DrawMenuList(230f, 64f, 900f);
            DrawControlsPanel(new Rect(560, 770, 800, 250));
        }

        void DrawCredits()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.85f));
            Label(new Rect(0, 140, VW, 90), Strings.CreditsTitle, headerStyle, Pal.uiText);
            Label(new Rect(160, 260, VW - 320, 520), Strings.CreditsBody, bodyCenterStyle, Pal.uiText);
            DrawMenuList(820f, 72f, 500f);
        }

        void DrawControlsPanel(Rect r)
        {
            DrawRect(r, Pal.uiPanel);
            Label(new Rect(r.x + 24, r.y + 14, r.width - 48, 32), Strings.ControlsTitle, smallStyle, Pal.gold);
            float y = r.y + 54;
            foreach (KeyValuePair<string, string> b in game.Controls.DescribeBindings())
            {
                Label(new Rect(r.x + 24, y, 200, 30), b.Key, smallStyle, Pal.uiDim);
                Label(new Rect(r.x + 220, y, r.width - 244, 30), b.Value, smallStyle, Pal.uiText);
                y += 36;
            }
        }

        void DrawReward()
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.75f));
            Label(new Rect(0, 110, VW, 90), Strings.RewardTitle, headerStyle, Pal.uiText);
            Label(new Rect(0, 200, VW, 44), Strings.RewardSubtitle, bodyCenterStyle, Pal.uiDim);

            IReadOnlyList<RewardDef> choices = game.RewardChoices;
            const float cardH = 470f, gap = 40f;
            int n = Mathf.Max(1, choices.Count);
            float cardW = Mathf.Min(480f, (VW - 160f - gap * (n - 1)) / n);
            float startX = (VW - (choices.Count * cardW + (choices.Count - 1) * gap)) * 0.5f;
            Event e = Event.current;
            for (int i = 0; i < choices.Count; i++)
            {
                RewardDef r = choices[i];
                bool selected = i == menuIndex;
                var rect = new Rect(startX + i * (cardW + gap), selected ? 285f : 300f, cardW, cardH);

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
                Color accent = technique ? Pal.gold : blessing ? Pal.blessing : Pal.playerAccent;
                DrawRect(rect, Pal.uiPanel);
                if (selected) DrawFrame(rect, accent, 4f);

                Label(new Rect(rect.x, rect.y + 24, cardW, 34), technique ? Strings.Technique : blessing ? Strings.Blessing : Strings.Relic, smallStyleCenter, accent);
                Label(new Rect(rect.x + 20, rect.y + 62, cardW - 40, 100), r.Name, cardNameStyle, Pal.uiText);
                float y = rect.y + 150;
                if (r.SourceEnemy != null)
                {
                    Label(new Rect(rect.x, y, cardW, 30), string.Format(Strings.AbsorbedFrom, r.SourceEnemy), smallStyleCenter, Pal.uiDim);
                }
                y += 50;
                Label(new Rect(rect.x + 30, y, cardW - 60, 30), Strings.Gain, smallStyle, accent);
                Label(new Rect(rect.x + 30, y + 32, cardW - 60, 110), r.Gain, cardBodyStyle, Pal.uiText);
                y += 150;
                Label(new Rect(rect.x + 30, y, cardW - 60, 30), Strings.Cost, smallStyle, Pal.danger);
                Label(new Rect(rect.x + 30, y + 32, cardW - 60, 100), r.Cost, cardBodyStyle, Pal.uiText);
            }
            DrawReading(new Rect(260f, 800f, VW - 520f, 200f));
            Label(new Rect(0, 760f, VW, 34f), Strings.EquipmentOpen + "   ·   " + string.Format(Strings.EquipmentBagCount, game.Run.Bag.Count, RunState.BagCapacity), smallStyleCenter, Pal.gold);
            Label(new Rect(0, VH - 56f, VW, 40), Strings.RewardControls, bodyCenterStyle, Pal.uiDim);
        }

        /// <summary>What the enemies have learned about you: shown between rooms so adaptation is never hidden.</summary>
        void DrawReading(Rect r)
        {
            DrawRect(r, Pal.uiPanel);
            DrawRect(new Rect(r.x, r.y, 6f, r.height), Pal.adapted);
            Label(new Rect(r.x + 30f, r.y + 10f, r.width - 60f, 32f), Strings.ReadingTitle, smallStyle, Pal.adapted);
            List<ReadingId> active = game.World.Adaptation.Active();
            if (active.Count == 0)
            {
                Label(new Rect(r.x + 30f, r.y + 50f, r.width - 60f, 32f), Strings.ReadingNone, cardBodyStyle, Pal.uiDim);
                return;
            }
            float y = r.y + 46f;
            foreach (ReadingId id in active)
            {
                Label(new Rect(r.x + 30f, y, 220f, 30f), Strings.ReadingName(id), smallStyle, Pal.adapted);
                Label(new Rect(r.x + 250f, y, r.width - 280f, 30f), Strings.ReadingText(id), cardBodyStyle, Pal.uiText);
                y += 30f;
            }
        }

        GUIStyle smallStyleCenter;

        void DrawEnd(string title, string subtitle, Color titleColor)
        {
            DrawRect(FullScreen, Pal.background.WithAlpha(0.8f));
            Label(new Rect(0, 130, VW, 100), title, headerStyle, titleColor);
            Label(new Rect(0, 235, VW, 44), subtitle, bodyCenterStyle, Pal.uiDim);

            RunState run = game.Run;
            if (run != null)
            {
                var panel = new Rect((VW - 720f) * 0.5f, 290f, 720f, 424f);
                DrawRect(panel, Pal.uiPanel);
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
                float y = panel.y + 10;
                foreach (string[] row in rows)
                {
                    Label(new Rect(panel.x + 40, y, 420, 36), row[0], bodyStyle, Pal.uiDim);
                    Label(new Rect(panel.x + 400, y, 280, 36), row[1], bodyRightStyle, Pal.uiText);
                    y += 37;
                }
                Label(new Rect(0, 716f, VW, 40f), string.Format(Strings.EcosEarned, run.EcosBanked, game.Meta.ecos), toastStyle, Pal.gold);
            }
            DrawMenuList(770f, 70f, 560f);
        }

        GUIStyle bodyRightStyle;

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        void DrawMenuList(float top, float itemHeight, float width)
        {
            float x = (VW - width) * 0.5f;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, top + i * itemHeight, width, itemHeight - 10f);
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
                float slide = selected ? CombatMath.EaseOutCubic(selAnim) : 0f;
                if (selected)
                {
                    float hw = r.width * slide;
                    DrawRect(new Rect(r.center.x - hw * 0.5f, r.y, hw, r.height), Pal.uiText.WithAlpha(0.08f));
                    DrawRect(new Rect(r.x, r.y, 6f, r.height), Pal.gold.WithAlpha(slide));
                }
                MenuItem item = items[i];
                string text = item.label;
                if (item.value != null) text += selected ? "     <  " + item.value() + "  >" : "     " + item.value();
                Label(new Rect(r.x + 10f * slide, r.y, r.width, r.height), text, itemStyle, selected ? Pal.gold : Pal.uiText);
            }
        }

        void DrawHud()
        {
            RunState run = game.Run;
            PlayerController p = game.World.Player;
            if (run == null || p == null) return;

            const float x = 48f, y = 36f, w = 460f;
            Label(new Rect(x, y, w, 28), Strings.HudHealth, smallStyle, Pal.uiDim);
            var hpRect = new Rect(x, y + 30, w, 26);
            Bar(hpRect, p.Health / p.MaxHealth, Pal.health);
            float hpRatio = Mathf.Clamp01(p.Health / p.MaxHealth);
            float ghostRatio = Mathf.Clamp01(ghostHp / p.MaxHealth);
            if (ghostRatio > hpRatio)
            {
                DrawRect(new Rect(hpRect.x + hpRect.width * hpRatio, hpRect.y, hpRect.width * (ghostRatio - hpRatio), hpRect.height), Color.white.WithAlpha(0.75f));
            }
            Label(new Rect(x, y + 30, w - 12, 26), Mathf.CeilToInt(p.Health) + " / " + Mathf.RoundToInt(p.MaxHealth), smallRightStyle, Pal.uiText);

            float pr = p.Posture.Ratio;
            Color pc = Color.Lerp(Pal.postureLow, Pal.postureHigh, pr);
            if (pr > 0.8f) pc = Color.Lerp(pc, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f));
            Label(new Rect(x, y + 64, w, 28), Strings.HudPosture, smallStyle, Pal.uiDim);
            Bar(new Rect(x, y + 94, w, 14), pr, pc);

            Label(new Rect(x, y + 116, w, 28), Strings.HudDodge, smallStyle, Pal.uiDim);
            for (int i = 0; i < p.MaxDodgeCharges; i++)
            {
                var r = new Rect(x + i * 48f, y + 148, 40f, 12f);
                DrawRect(r, new Color(0f, 0f, 0f, 0.6f));
                float fill = Mathf.Clamp01(p.DodgeCharges - i);
                DrawRect(new Rect(r.x, r.y, r.width * fill, r.height), fill >= 1f ? Pal.playerAccent : Pal.playerAccent.WithAlpha(0.4f));
            }

            RoomDef room = game.World.CurrentRoom;
            if (room != null)
            {
                Label(new Rect(0, 30, VW, 44), string.Format(Strings.HudRoom, run.RoomIndex + 1, room.name), roomStyle, room.guardian ? Pal.gold : Pal.uiText);
                int wave = Mathf.Max(1, game.World.WaveIndex + 1);
                Label(new Rect(0, 74, VW, 32), string.Format(Strings.HudWave, wave, room.waves.Length), smallStyleCenter, Pal.uiDim);
                List<ReadingId> reading = game.World.Adaptation.Active();
                if (reading.Count > 0)
                {
                    var names = new string[reading.Count];
                    for (int i = 0; i < reading.Count; i++) names[i] = Strings.ReadingName(reading[i]);
                    Label(new Rect(0, 102, VW, 30), string.Format(Strings.HudReading, string.Join("  ·  ", names)), smallStyleCenter, Pal.adapted);
                }
            }

            DrawFlow(run.Flow);
            DrawWeaponsHud(run, x, y + 170f);
        }

        void DrawFlow(FlowMeter flow)
        {
            const float w = 360f;
            float fx = VW - 48f - w, fy = 36f;
            int level = flow.Level;
            Label(new Rect(fx, fy, w, 30), Strings.HudFlow, smallRightStyle, level > 0 ? Pal.gold : Pal.uiDim);
            // Textured diamonds: GUIUtility.RotateAroundPivot misplaces the pivot under a scaled GUI.matrix.
            Texture diamond = ShapeLibrary.Get(Shape.Diamond).texture;
            Texture outline = ShapeLibrary.Get(Shape.DiamondOutline).texture;
            for (int i = 0; i < flow.MaxLevel; i++)
            {
                float cx = fx + w - 26f - (flow.MaxLevel - 1 - i) * 58f;
                var r = new Rect(cx - 24f, fy + 38f, 48f, 48f);
                bool lit = level > i;
                DrawTex(r, lit ? diamond : outline, lit ? Pal.gold : Pal.uiDim.WithAlpha(0.6f));
            }
            Bar(new Rect(fx + w - 190f, fy + 98f, 190f, 8f), flow.ProgressInLevel, Pal.gold);
            Label(new Rect(fx, fy + 114f, w, 30f), string.Format(Strings.HudEcos, MetaRules.EcosForRun(game.Run)), smallRightStyle, Pal.gold.WithAlpha(0.85f));

            int streak = game.Run.PerfectStreak;
            if (streak >= 2)
            {
                // Pops on every new perfect action; brighter the longer it runs.
                // Font-size pop (pivot-based GUI scaling misplaces under the letterbox matrix).
                Color sc = Color.Lerp(Pal.uiText, Pal.gold, Mathf.Clamp01(streak / 10f));
                streakStyle.fontSize = Mathf.RoundToInt(30f * (1f + 0.4f * streakPop));
                Label(new Rect(fx, fy + 146f, w, 50f), string.Format(Strings.HudStreak, streak), streakStyle, sc);
            }
        }

        // ── HUD weapons ───────────────────────────────────────────────────────────────

        void DrawWeaponsHud(RunState run, float x, float y)
        {
            Label(new Rect(x, y, 600f, 30f), run.CurrentWeapon.name, smallStyle, Pal.uiText);
            Label(new Rect(x, y + 26f, 600f, 26f), "<>  " + run.OtherWeapon.name + "  (" + (game.Controls.UsingGamepad ? "Y" : "Q") + ")", smallStyle, Pal.uiDim);
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
            DrawRect(FullScreen, Pal.background.WithAlpha(0.9f));
            Label(new Rect(0, 50, VW, 90), Strings.ArsenalTitle, headerStyle, Pal.uiText);
            Label(new Rect(0, 140, VW, 40), Strings.ArsenalSubtitle, bodyCenterStyle, Pal.gold);

            const float cardW = 540f, cardH = 300f, gap = 30f;
            float startX = (VW - (3 * cardW + 2 * gap)) * 0.5f;
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            for (int i = 0; i < WeaponDefs.All.Length; i++)
            {
                WeaponDef w = WeaponDefs.All[i];
                var r = new Rect(startX + (i % 3) * (cardW + gap), 200f + (i / 3) * (cardH + gap), cardW, cardH);
                if (ArsenalMouse(r, i, e, mouseMoved)) return;
                int pick = arsenalPicks.IndexOf(w.id);
                bool selected = i == arsenalIndex;
                DrawRect(r, Pal.uiPanel);
                if (pick >= 0) DrawFrame(r, Pal.gold, 3f);
                if (selected) DrawFrame(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), Pal.uiText, 2f);

                DrawWeaponArt(w, new Rect(r.x + 30f, r.y + 30f, 200f, 50f), Color.white);
                if (w.dual) DrawWeaponArt(w, new Rect(r.x + 30f, r.y + 70f, 200f, 50f), new Color(0.85f, 0.85f, 0.85f, 1f));
                Label(new Rect(r.x + 250f, r.y + 20f, cardW - 270f, 44f), w.name, cardNameStyle, pick >= 0 ? Pal.gold : Pal.uiText);
                if (pick >= 0) Label(new Rect(r.x + 6f, r.y + 2f, 40f, 40f), (pick + 1).ToString(), toastStyle, Pal.gold);
                Label(new Rect(r.x + 30f, r.y + 120f, cardW - 60f, 60f), w.description, cardBodyStyle, Pal.uiText);
                Label(new Rect(r.x + 30f, r.y + 190f, cardW - 60f, 28f), string.Format(Strings.WeaponCombo, w.combo.Length), smallStyle, Pal.uiDim);
                Label(new Rect(r.x + 30f, r.y + 218f, cardW - 60f, 28f), string.Format(Strings.WeaponSwapStrike, w.swapStrike.name), smallStyle, Pal.playerAccent);
                Label(new Rect(r.x + 30f, r.y + 246f, cardW - 60f, 28f), string.Format(Strings.WeaponFinisher, w.finisherName), smallStyle, Pal.danger);
            }

            var start = new Rect((VW - 500f) * 0.5f, 880f, 500f, 64f);
            var back = new Rect((VW - 500f) * 0.5f, 950f, 500f, 60f);
            if (ArsenalMouse(start, ArsenalStartIndex, e, mouseMoved) || ArsenalMouse(back, ArsenalBackIndex, e, mouseMoved)) return;
            bool ready = arsenalPicks.Count == 2;
            if (arsenalIndex == ArsenalStartIndex) DrawRect(start, Pal.uiText.WithAlpha(0.1f));
            if (arsenalIndex == ArsenalBackIndex) DrawRect(back, Pal.uiText.WithAlpha(0.1f));
            Label(start, ready ? Strings.ArsenalStart : Strings.ArsenalNeedTwo, itemStyle, ready ? (arsenalIndex == ArsenalStartIndex ? Pal.gold : Pal.uiText) : Pal.uiDim);
            Label(back, Strings.Back, itemStyle, arsenalIndex == ArsenalBackIndex ? Pal.gold : Pal.uiText);
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
            DrawRect(FullScreen, Pal.background.WithAlpha(0.94f));
            Label(new Rect(0, 30, VW, 80), Strings.EquipmentTitle, headerStyle, Pal.uiText);

            DrawCharacterPanel(run, new Rect(60f, 130f, 600f, 820f));

            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            var slotsPanel = new Rect(700f, 130f, 560f, 300f);
            DrawRect(slotsPanel, Pal.uiPanel);
            Label(new Rect(slotsPanel.x + 20f, slotsPanel.y + 10f, 400f, 30f), Strings.EquipmentSlots, smallStyle, Pal.gold);
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(slotsPanel.x + 10f, slotsPanel.y + 48f + i * 62f, slotsPanel.width - 20f, 56f);
                if (EquipmentMouse(r, i, e, mouseMoved)) return;
                DrawItemRow(r, run.Equipped[i], ItemGenerator.SlotName((ItemSlot)i), i == equipIndex);
            }

            var bagPanel = new Rect(700f, 450f, 560f, 500f);
            DrawRect(bagPanel, Pal.uiPanel);
            Label(new Rect(bagPanel.x + 20f, bagPanel.y + 10f, 500f, 30f), Strings.EquipmentBag + "   " + string.Format(Strings.EquipmentBagCount, run.Bag.Count, RunState.BagCapacity), smallStyle, Pal.gold);
            for (int i = 0; i < run.Bag.Count; i++)
            {
                var r = new Rect(bagPanel.x + 10f, bagPanel.y + 44f + i * 37f, bagPanel.width - 20f, 34f);
                if (EquipmentMouse(r, 4 + i, e, mouseMoved)) return;
                DrawItemRow(r, run.Bag[i], null, 4 + i == equipIndex);
            }

            DrawItemTooltip(EquipmentItemAt(equipIndex), new Rect(1290f, 130f, 570f, 820f));
            Label(new Rect(0, VH - 56f, VW, 36f), Strings.EquipmentControls, smallStyleCenter, Pal.uiDim);
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
            if (selected) DrawRect(r, Pal.uiText.WithAlpha(0.1f));
            float icon = Mathf.Min(r.height - 8f, 44f);
            var iconRect = new Rect(r.x + 8f, r.y + (r.height - icon) * 0.5f, icon, icon);
            if (item == null)
            {
                DrawTex(iconRect, ShapeLibrary.Get(Shape.Ring).texture, Pal.uiDim.WithAlpha(0.5f));
                Label(new Rect(r.x + icon + 20f, r.y, r.width - icon - 30f, r.height), (emptyLabel ?? string.Empty) + "  " + Strings.EquipmentEmpty, smallStyle, Pal.uiDim);
                return;
            }
            DrawItemIcon(iconRect, item);
            Label(new Rect(r.x + icon + 20f, r.y, r.width - icon - 30f, r.height), item.Name, smallStyle, Rarities.Color(item.rarity));
        }

        /// <summary>Pixel-art item: the slot's object in rarity metal with an element stone, on a rarity glow.</summary>
        void DrawItemIcon(Rect r, Item item)
        {
            Color rc = Rarities.Color(item.rarity);
            DrawTex(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), ShapeLibrary.Get(Shape.SoftCircle).texture, rc.WithAlpha(0.35f + 0.04f * (int)item.rarity));
            Sprite icon = PixelArt.ItemIcon(item.slot, item.element, item.rarity);
            if (icon != null) DrawTex(r, icon.texture, Color.white);
            else DrawTex(r, ShapeLibrary.Get(ItemGenerator.IconShape(item.slot)).texture, Elements.Color(item.element));
            if (item.rarity >= Rarity.Mythic)
            {
                float spin = Mathf.Repeat(Time.unscaledTime * 0.5f, 1f);
                DrawTex(new Rect(r.x - 6f + 4f * spin, r.y - 6f, r.width + 12f, r.height + 12f), ShapeLibrary.Get(Shape.Star).texture, rc.WithAlpha(0.6f));
            }
        }

        void DrawItemTooltip(Item item, Rect r)
        {
            DrawRect(r, Pal.uiPanel);
            if (item == null)
            {
                Label(new Rect(r.x + 30f, r.y + 40f, r.width - 60f, 80f), Strings.EquipmentPickHint, cardBodyStyle, Pal.uiDim);
                return;
            }
            Color rc = Rarities.Color(item.rarity);
            DrawFrame(r, rc, 3f);
            DrawItemIcon(new Rect(r.center.x - 60f, r.y + 30f, 120f, 120f), item);
            Label(new Rect(r.x + 20f, r.y + 170f, r.width - 40f, 50f), item.Name, cardNameStyle, rc);
            Label(new Rect(r.x + 20f, r.y + 222f, r.width - 40f, 30f), string.Format(Strings.Rarity, Rarities.Name(item.rarity)), smallStyleCenter, rc);
            Label(new Rect(r.x + 20f, r.y + 252f, r.width - 40f, 30f), string.Format(Strings.ElementLine, ItemGenerator.SlotName(item.slot), Elements.Name(item.element)), smallStyleCenter, Elements.Color(item.element));
            Label(new Rect(r.x + 30f, r.y + 300f, r.width - 60f, 30f), Strings.Gain, smallStyle, Pal.gold);
            Label(new Rect(r.x + 30f, r.y + 334f, r.width - 60f, 420f), item.Gain, cardBodyStyle, Pal.uiText);
        }

        void DrawCharacterPanel(RunState run, Rect r)
        {
            DrawRect(r, Pal.uiPanel);
            PlayerController p = game.World.Player;

            // The duelist at the centre of its weapon wheel.
            var centre = new Vector2(r.center.x, r.y + 300f);
            Label(new Rect(r.x, r.y + 10f, r.width, 30f), Strings.EquipmentWheel, smallStyleCenter, Pal.gold);
            DrawTex(new Rect(centre.x - 190f, centre.y - 190f, 380f, 380f), ShapeLibrary.Get(Shape.Ring).texture, Pal.uiDim.WithAlpha(0.5f));
            DrawTex(new Rect(centre.x - 120f, centre.y - 120f, 240f, 240f), ShapeLibrary.Get(Shape.SoftCircle).texture, Pal.gold.WithAlpha(0.12f + 0.04f * run.Flow.Level));
            DrawCharacterArt(new Rect(centre.x - 60f, centre.y - 100f, 120f, 210f));
            for (int i = 0; i < 4; i++)
            {
                Item item = run.Equipped[i];
                if (item == null) continue;
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                var pos = centre + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 95f;
                DrawItemIcon(new Rect(pos.x - 20f, pos.y - 20f, 40f, 40f), item);
            }
            DrawWheelWeapon(run.CurrentWeapon, new Rect(centre.x - 130f, centre.y - 235f, 260f, 60f), true);
            DrawWheelWeapon(run.OtherWeapon, new Rect(centre.x - 130f, centre.y + 175f, 260f, 60f), false);

            Label(new Rect(r.x + 30f, r.y + 560f, r.width - 60f, 30f), Strings.EquipmentStats, smallStyle, Pal.gold);
            PlayerModifiers m = run.Modifiers;
            string[] stats =
            {
                "Vida  " + Mathf.CeilToInt(p.Health) + " / " + Mathf.RoundToInt(p.MaxHealth),
                "Postura máxima  " + Mathf.RoundToInt(p.Posture.Max),
                "Dano  x" + p.DamageDealtMult.ToString("0.00"),
                "Crítico  " + Mathf.RoundToInt(m.critChance * 100f) + "%     Roubo de vida  " + Mathf.RoundToInt(m.lifesteal * 100f) + "%",
                "Janela de parry  " + p.PerfectParryFrames + " frames",
                "Ascensão  " + run.Ascension + "     Técnicas e relíquias  " + run.Owned.Count,
            };
            for (int i = 0; i < stats.Length; i++)
            {
                Label(new Rect(r.x + 30f, r.y + 594f + i * 30f, r.width - 60f, 32f), stats[i], smallStyle, Pal.uiText);
            }
            string owned = string.Empty;
            foreach (RewardDef d in run.Owned) owned += (owned.Length > 0 ? "  ·  " : string.Empty) + d.Name;
            if (owned.Length > 0) Label(new Rect(r.x + 30f, r.y + 776f, r.width - 60f, 50f), owned, cardBodyStyle, Pal.uiDim);
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
            if (s.texture.width >= s.texture.height * 2) DrawFitted(r, s.texture, color);
            else DrawTex(r, s.texture, w.color * color);
        }

        /// <summary>The duelist's idle loop, large, for the equipment screen.</summary>
        void DrawCharacterArt(Rect r)
        {
            CharacterArt art = PixelArt.Character(PlayerController.PlayerArt);
            if (art == null)
            {
                DrawTex(r, ShapeLibrary.Get(Shape.Circle).texture, Pal.player);
                return;
            }
            Sprite frame = art.idle[Mathf.FloorToInt(Time.unscaledTime * 6f) % art.idle.Length];
            DrawFitted(r, frame.texture, Color.white);
        }

        void DrawWheelWeapon(WeaponDef w, Rect r, bool active)
        {
            if (active) DrawRect(r, Pal.gold.WithAlpha(0.12f));
            DrawWeaponArt(w, new Rect(r.x - 20f, r.y + 8f, 110f, 30f), active ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f));
            Label(new Rect(r.x + 100f, r.y, r.width + 40f, 40f), w.name, smallStyle, active ? Pal.gold : Pal.uiDim);
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
                Label(new Rect(gx - 260f, gy - 22f, 520f, 44f), p.text, p.small ? damageStyle : popupStyle, p.color.WithAlpha(alpha));
            }
        }

        void DrawToasts()
        {
            // During play toasts sit under the room title; on full screens they move to the
            // bottom so they never cover the screen's own heading.
            bool playing = game.CurrentMode == Game.Mode.Playing;
            for (int i = 0; i < toasts.Count; i++)
            {
                FloatingText t = toasts[i];
                float alpha = Mathf.Clamp01((t.life - t.age) / 0.5f) * Mathf.Clamp01(t.age / 0.15f);
                float y = playing ? 150f + i * 50f : VH - 110f - i * 50f;
                Label(new Rect(0, y, VW, 48), t.text, toastStyle, t.color.WithAlpha(alpha));
            }
        }

        void DrawHint()
        {
            if (string.IsNullOrEmpty(hint) || hintTimer <= 0f) return;
            float alpha = Mathf.Clamp01(hintTimer / 0.6f);
            var r = new Rect(240f, VH - 160f, VW - 480f, 104f);
            DrawRect(r, Pal.uiPanel.WithAlpha(Pal.uiPanel.a * alpha));
            DrawRect(new Rect(r.x, r.y, 6f, r.height), Pal.gold.WithAlpha(alpha));
            Label(new Rect(r.x + 30, r.y + 10, r.width - 60, r.height - 20), hint, hintStyle, Pal.uiText.WithAlpha(alpha));
        }

        void DrawOverlays()
        {
            if (hurtAlpha > 0.001f)
            {
                Rect fs = FullScreen;
                for (int k = 0; k < 6; k++)
                {
                    float inset = k * 22f;
                    float a = hurtAlpha * (1f - k / 6f) * 0.5f;
                    DrawFrame(new Rect(fs.x + inset, fs.y + inset, fs.width - inset * 2f, fs.height - inset * 2f), Pal.danger.WithAlpha(a), 22f);
                }
            }
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

        void DrawFrame(Rect r, Color c, float thickness)
        {
            DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
            DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            DrawRect(new Rect(r.x, r.y + thickness, thickness, r.height - thickness * 2f), c);
            DrawRect(new Rect(r.xMax - thickness, r.y + thickness, thickness, r.height - thickness * 2f), c);
        }

        void Bar(Rect r, float ratio, Color fill)
        {
            DrawRect(r, new Color(0f, 0f, 0f, 0.6f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), fill);
        }

        static void Label(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f * color.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = prev;
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            titleStyle = MakeStyle(150, TextAnchor.MiddleCenter, true);
            headerStyle = MakeStyle(64, TextAnchor.MiddleCenter, true);
            itemStyle = MakeStyle(38, TextAnchor.MiddleCenter, false);
            bodyStyle = MakeStyle(30, TextAnchor.MiddleLeft, false);
            bodyRightStyle = MakeStyle(30, TextAnchor.MiddleRight, true);
            bodyCenterStyle = MakeStyle(30, TextAnchor.UpperCenter, false);
            smallStyle = MakeStyle(22, TextAnchor.MiddleLeft, true);
            smallRightStyle = MakeStyle(22, TextAnchor.MiddleRight, true);
            smallStyleCenter = MakeStyle(22, TextAnchor.MiddleCenter, true);
            popupStyle = MakeStyle(30, TextAnchor.MiddleCenter, true);
            cardNameStyle = MakeStyle(40, TextAnchor.UpperCenter, true);
            cardBodyStyle = MakeStyle(25, TextAnchor.UpperLeft, false);
            hintStyle = MakeStyle(27, TextAnchor.MiddleCenter, false);
            toastStyle = MakeStyle(34, TextAnchor.MiddleCenter, true);
            roomStyle = MakeStyle(30, TextAnchor.MiddleCenter, true);
            itemLeftStyle = MakeStyle(34, TextAnchor.MiddleLeft, false);
            ecosStyle = MakeStyle(48, TextAnchor.MiddleCenter, true);
            damageStyle = MakeStyle(24, TextAnchor.MiddleCenter, true);
            streakStyle = MakeStyle(30, TextAnchor.MiddleRight, true);
        }

        static GUIStyle MakeStyle(int size, TextAnchor anchor, bool bold)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true,
                richText = false,
                clipping = TextClipping.Overflow,
            };
        }
    }
}
