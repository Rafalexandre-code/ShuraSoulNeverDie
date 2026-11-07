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
                    AddItem(Strings.Play, game.StartRun);
                    AddItem(Strings.Altar, game.OpenAltar);
                    AddItem(Strings.Settings, game.OpenSettings);
                    AddItem(Strings.Credits, game.OpenCredits);
                    AddItem(Strings.Quit, game.QuitGame);
                    break;
                case Game.Mode.Paused:
                    AddItem(Strings.Resume, game.Resume);
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
            Label(new Rect(260, 300, VW - 520, 420), Strings.CreditsBody, bodyCenterStyle, Pal.uiText);
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
                var panel = new Rect((VW - 720f) * 0.5f, 296f, 720f, 418f);
                DrawRect(panel, Pal.uiPanel);
                string[][] rows =
                {
                    new[] { Strings.StatRooms, run.RoomsCleared.ToString() },
                    new[] { Strings.StatRecord, game.Meta.bestRoom.ToString() },
                    new[] { Strings.StatParries, run.PerfectParries.ToString() },
                    new[] { Strings.StatDodges, run.PerfectDodges.ToString() },
                    new[] { Strings.StatCounters, run.Counters.ToString() },
                    new[] { Strings.StatFinishers, run.Finishers.ToString() },
                    new[] { Strings.StatTechniques, run.TechniquesAbsorbed.ToString() },
                    new[] { Strings.StatBestStreak, run.BestStreak.ToString() },
                    new[] { Strings.StatTime, FormatTime(run.Elapsed) },
                    new[] { Strings.StatSeed, run.Seed.ToString() },
                };
                float y = panel.y + 14;
                foreach (string[] row in rows)
                {
                    Label(new Rect(panel.x + 40, y, 420, 40), row[0], bodyStyle, Pal.uiDim);
                    Label(new Rect(panel.x + 400, y, 280, 40), row[1], bodyRightStyle, Pal.uiText);
                    y += 44;
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
            DrawOwned(run);
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

        void DrawOwned(RunState run)
        {
            int lines = run.Owned.Count + run.PendingTechniques.Count;
            if (lines == 0) return;
            // Top-left under the dodge pips, so it never collides with the hint box at the bottom.
            float h = 44f + lines * 32f;
            var panel = new Rect(36f, 214f, 520f, h);
            DrawRect(panel, Pal.uiPanel);
            Label(new Rect(panel.x + 18, panel.y + 8, 520, 30), Strings.HudOwned, smallStyle, Pal.uiDim);
            float y = panel.y + 40;
            foreach (RewardDef r in run.Owned)
            {
                bool tech = r.Kind == RewardKind.Technique;
                Label(new Rect(panel.x + 18, y, 530, 30), (tech ? Strings.TechniqueTag : Strings.RelicTag) + r.Name, smallStyle, tech ? Pal.gold : Pal.playerAccent);
                y += 32;
            }
            foreach (string id in run.PendingTechniques)
            {
                RewardDef preview = RewardRegistry.Create(id);
                Label(new Rect(panel.x + 18, y, 530, 30), string.Format(Strings.HudPending, preview != null ? preview.Name : id), smallStyle, Pal.gold.WithAlpha(0.7f));
                y += 32;
            }
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
