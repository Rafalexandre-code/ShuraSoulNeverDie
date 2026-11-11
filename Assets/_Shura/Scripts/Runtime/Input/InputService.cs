using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shura
{
    /// <summary>What the player controller reads. Implemented by real input and by the test bot.</summary>
    public interface IPlayerInputSource
    {
        Vector2 Move { get; }
        bool ParryHeld { get; }
        InputBuffer Buffer { get; }

        /// <summary>Held over a fallen teammate to bring them back (E / gamepad X).</summary>
        bool InteractHeld { get; }

        /// <summary>The attack button is held (the Iai standoff draws on release).</summary>
        bool AttackHeld { get; }

        /// <summary>A guard stance (Kamae 0..3) asked for since the last call, or -1.</summary>
        int TakeKamae();

        /// <summary>Normalized aim direction, or zero when the source has no explicit aim.</summary>
        Vector2 AimDirection(Vector2 playerWorldPosition);
    }

    /// <summary>Every command a player can remap (the order of the controls screen).</summary>
    public enum ControlAction
    {
        Attack,
        Parry,
        Dodge,
        Swap,
        Interact,
        LockOn,
        LockNext,
        LockPrev,
        Detonate,
        Kiai,
        Soul,
        Tool,
        ToolCycle,
        Gourd,
        Bandage,
        Kamae1,
        Kamae2,
        Kamae3,
        Kamae4,
    }

    /// <summary>The look of the buttons on screen: the keyboard, an Xbox pad or a PlayStation pad.</summary>
    public enum GlyphStyle
    {
        Keyboard,
        Xbox,
        PlayStation,
    }

    /// <summary>
    /// Actions are built in code (not an .inputactions asset) so they are easy to review and
    /// remap. Every remappable action has two keyboard/mouse slots and one gamepad slot; the
    /// player's changes are saved per slot ("Atacar#1=&lt;Keyboard&gt;/u").
    /// </summary>
    public sealed class InputService : IPlayerInputSource
    {
        public const string KeyboardGroup = "Teclado";
        public const string GamepadGroup = "Controle";

        const float NavFirstRepeat = 0.38f;
        const float NavRepeat = 0.11f;
        const float StickDeadzoneSqr = 0.12f;
        const float FlickOn = 0.78f;
        const float FlickOff = 0.32f;
        const float SelectHoldSeconds = 0.45f;

        /// <summary>The one the game is played with (for the HUD's button names).</summary>
        public static InputService Active { get; private set; }

        readonly InputActionMap gameplay = new InputActionMap("Gameplay");
        readonly InputActionMap ui = new InputActionMap("UI");
        readonly InputAction move, aim, pause;
        readonly InputAction[] actions = new InputAction[Enum.GetValues(typeof(ControlAction)).Length];
        readonly InputAction navigate, submit, cancel;

        int lastNavX, lastNavY;
        float navTimer;
        bool stickFlickArmed = true;
        float selectHeld = -1f;
        float rumbleLeft;

        public InputBuffer Buffer { get; } = new InputBuffer();
        public bool UsingGamepad { get; private set; }
        public GlyphStyle PadStyle { get; private set; } = GlyphStyle.Xbox;
        public bool PausePressed { get; private set; }
        public bool Submit { get; private set; }
        public bool Cancel { get; private set; }
        public bool AnyPressed { get; private set; }
        /// <summary>Tab / a tap of the gamepad's View (Select) button: opens the Equipment screen.</summary>
        public bool EquipmentPressed { get; private set; }
        /// <summary>C / holding the gamepad's View (Select) button: the combat log.</summary>
        public bool LogTogglePressed { get; private set; }
        public int NavX { get; private set; }
        public int NavY { get; private set; }
        /// <summary>
        /// Every gameplay press ever made, per action (attack, parry, dodge, swap, detonate, kiai,
        /// tool, tool cycle, gourd, bandage, soul): a guest sends these counters to the host, so a
        /// lost packet can never lose a press.
        /// </summary>
        public readonly int[] PressCounts = new int[11];

        // ── Target lock (read by the duelist, or by an online guest's own session) ─────────
        public int LockToggles { get; private set; }
        public int LockFlicks { get; private set; }
        public Vector2 LastFlick { get; private set; }
        public int LockCycleNext { get; private set; }
        public int LockCyclePrev { get; private set; }
        /// <summary>Set by whoever holds the lock: while locked, the right stick switches targets instead of aiming.</summary>
        public bool LockEngaged { get; set; }

        /// <summary>Keyboard and mouse may lock on (from the settings); on a gamepad the lock is always on.</summary>
        public bool MouseLockAllowed { get; set; }
        /// <summary>Gamepad rumble on/off (from the settings).</summary>
        public bool RumbleEnabled { get; set; } = true;
        /// <summary>A remap is listening for a key: the menus must not react to it.</summary>
        public bool Rebinding => rebind != null;

        /// <summary>Screen → world conversion, provided by the camera.</summary>
        public Func<Vector2, Vector2> ScreenToWorld { get; set; }

        InputAction A(ControlAction c) => actions[(int)c];

        InputAction Add(ControlAction c, string name, string key1, string key2, string pad, string padInteraction = null)
        {
            InputAction a = gameplay.AddAction(name, InputActionType.Button);
            a.AddBinding(key1 ?? string.Empty).WithGroup(KeyboardGroup);
            a.AddBinding(key2 ?? string.Empty).WithGroup(KeyboardGroup);
            var padBinding = a.AddBinding(pad ?? string.Empty).WithGroup(GamepadGroup);
            if (padInteraction != null) padBinding.WithInteraction(padInteraction);
            actions[(int)c] = a;
            return a;
        }

        public InputService()
        {
            Active = this;
            move = gameplay.AddAction("Mover", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            aim = gameplay.AddAction("Mirar", InputActionType.Value, "<Gamepad>/rightStick");

            // Souls-like layout on a pad: RB attack, LB parry, A dodge, Y swap, X interact,
            // R3 lock, L3 soul, B tap = gourd / hold = bandage, RT tool, LT next tool, d-pad guards.
            Add(ControlAction.Attack, "Atacar", "<Mouse>/leftButton", "<Keyboard>/j", "<Gamepad>/rightShoulder");
            Add(ControlAction.Parry, "Aparar", "<Mouse>/rightButton", "<Keyboard>/k", "<Gamepad>/leftShoulder");
            Add(ControlAction.Dodge, "Esquivar", "<Keyboard>/space", "<Keyboard>/l", "<Gamepad>/buttonSouth");
            Add(ControlAction.Swap, "Trocar arma", "<Keyboard>/q", "<Mouse>/middleButton", "<Gamepad>/buttonNorth");
            Add(ControlAction.Interact, "Interagir / reviver", "<Keyboard>/e", null, "<Gamepad>/buttonWest");
            Add(ControlAction.LockOn, "Travar mira", "<Keyboard>/z", "<Mouse>/forwardButton", "<Gamepad>/rightStickPress");
            Add(ControlAction.LockNext, "Próximo alvo", "<Mouse>/scroll/up", "<Keyboard>/x", null);
            Add(ControlAction.LockPrev, "Alvo anterior", "<Mouse>/scroll/down", null, null);
            Add(ControlAction.Detonate, "Detonar selos", "<Keyboard>/g", null, null);
            Add(ControlAction.Kiai, "Kiai", "<Keyboard>/v", null, null);
            Add(ControlAction.Soul, "Alma", "<Keyboard>/b", null, "<Gamepad>/leftStickPress");
            Add(ControlAction.Tool, "Ferramenta", "<Keyboard>/r", null, "<Gamepad>/rightTrigger");
            Add(ControlAction.ToolCycle, "Trocar ferramenta", "<Keyboard>/t", null, "<Gamepad>/leftTrigger");
            Add(ControlAction.Gourd, "Cabaça", "<Keyboard>/f", null, "<Gamepad>/buttonEast", "tap(duration=0.3)");
            Add(ControlAction.Bandage, "Bandagem", "<Keyboard>/h", null, "<Gamepad>/buttonEast", "hold(duration=0.45)");
            Add(ControlAction.Kamae1, "Guarda Jōdan", "<Keyboard>/1", null, "<Gamepad>/dpad/up");
            Add(ControlAction.Kamae2, "Guarda Chūdan", "<Keyboard>/2", null, "<Gamepad>/dpad/right");
            Add(ControlAction.Kamae3, "Guarda Gedan", "<Keyboard>/3", null, "<Gamepad>/dpad/down");
            Add(ControlAction.Kamae4, "Guarda Waki", "<Keyboard>/4", null, "<Gamepad>/dpad/left");

            pause = gameplay.AddAction("Pausar", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            navigate = ui.AddAction("Navegar", InputActionType.Value);
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            navigate.AddBinding("<Gamepad>/dpad");
            navigate.AddBinding("<Gamepad>/leftStick");

            submit = ui.AddAction("Confirmar", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            cancel = ui.AddAction("Voltar", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Keyboard>/backspace");
            cancel.AddBinding("<Gamepad>/buttonEast");

            gameplay.Enable();
            ui.Enable();
        }

        public Vector2 Move
        {
            get
            {
                Vector2 v = move.ReadValue<Vector2>();
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public bool ParryHeld => A(ControlAction.Parry).IsPressed();

        public bool InteractHeld => A(ControlAction.Interact).IsPressed();

        public bool AttackHeld => A(ControlAction.Attack).IsPressed();

        int pendingKamae = -1;

        public int TakeKamae()
        {
            int k = pendingKamae;
            pendingKamae = -1;
            return k;
        }

        public Vector2 AimDirection(Vector2 playerWorldPosition)
        {
            if (UsingGamepad)
            {
                // Locked on, the right stick picks targets; it does not aim.
                if (LockEngaged) return Vector2.zero;
                Vector2 stick = aim.ReadValue<Vector2>();
                return stick.sqrMagnitude > StickDeadzoneSqr ? stick.normalized : Vector2.zero;
            }

            if (Mouse.current == null || ScreenToWorld == null) return Vector2.zero;
            Vector2 world = ScreenToWorld(Mouse.current.position.ReadValue());
            Vector2 d = world - playerWorldPosition;
            return d.sqrMagnitude > 0.04f ? d.normalized : Vector2.zero;
        }

        /// <summary>Call once per rendered frame, before the simulation steps.</summary>
        public void Poll(PlayerTuning tuning, bool gameplayActive)
        {
            TickRumble();
            if (rebind != null)
            {
                // Listening for a new key: nothing else may react to it.
                PausePressed = Submit = Cancel = AnyPressed = EquipmentPressed = LogTogglePressed = false;
                NavX = NavY = 0;
                return;
            }
            DetectDevice();
            PausePressed = pause.WasPressedThisFrame();
            Submit = submit.WasPressedThisFrame();
            Cancel = cancel.WasPressedThisFrame();
            AnyPressed = AnyButtonPressed();
            UpdateViewButton();
            UpdateNavigation();

            if (!gameplayActive) return;
            CountPresses();
            Press(ControlAction.Attack, BufferedAction.Attack, tuning.actionBufferFrames);
            Press(ControlAction.Dodge, BufferedAction.Dodge, tuning.actionBufferFrames);
            Press(ControlAction.Parry, BufferedAction.Parry, tuning.parryBufferFrames);
            Press(ControlAction.Swap, BufferedAction.Swap, tuning.parryBufferFrames);
            Press(ControlAction.Detonate, BufferedAction.Detonate, tuning.actionBufferFrames);
            Press(ControlAction.Kiai, BufferedAction.Kiai, tuning.actionBufferFrames);
            Press(ControlAction.Tool, BufferedAction.Tool, tuning.actionBufferFrames);
            Press(ControlAction.ToolCycle, BufferedAction.ToolCycle, tuning.actionBufferFrames);
            Press(ControlAction.Gourd, BufferedAction.Gourd, tuning.actionBufferFrames);
            Press(ControlAction.Bandage, BufferedAction.Bandage, tuning.actionBufferFrames);
            Press(ControlAction.Soul, BufferedAction.Soul, tuning.actionBufferFrames);
            for (int k = 0; k < 4; k++)
            {
                if (A(ControlAction.Kamae1 + k).WasPerformedThisFrame()) pendingKamae = k;
            }
            if (A(ControlAction.LockOn).WasPerformedThisFrame()) LockToggles++;
            if (A(ControlAction.LockNext).WasPerformedThisFrame()) LockCycleNext++;
            if (A(ControlAction.LockPrev).WasPerformedThisFrame()) LockCyclePrev++;
            UpdateFlick();
        }

        void Press(ControlAction c, BufferedAction b, int frames)
        {
            if (A(c).WasPerformedThisFrame()) Buffer.Press(b, frames);
        }

        void CountPresses()
        {
            ControlAction[] order =
            {
                ControlAction.Attack, ControlAction.Parry, ControlAction.Dodge, ControlAction.Swap, ControlAction.Detonate, ControlAction.Kiai,
                ControlAction.Tool, ControlAction.ToolCycle, ControlAction.Gourd, ControlAction.Bandage, ControlAction.Soul,
            };
            for (int i = 0; i < order.Length; i++)
            {
                if (A(order[i]).WasPerformedThisFrame()) PressCounts[i]++;
            }
        }

        /// <summary>A quick push of the right stick past the edge, then back: one target switch.</summary>
        void UpdateFlick()
        {
            if (!UsingGamepad)
            {
                stickFlickArmed = true;
                return;
            }
            Vector2 s = aim.ReadValue<Vector2>();
            float m = s.magnitude;
            if (stickFlickArmed && m >= FlickOn)
            {
                stickFlickArmed = false;
                LastFlick = s / m;
                LockFlicks++;
            }
            else if (!stickFlickArmed && m <= FlickOff)
            {
                stickFlickArmed = true;
            }
        }

        /// <summary>The View (Select) button: a tap opens the equipment, holding it opens the log.</summary>
        void UpdateViewButton()
        {
            Keyboard kb = Keyboard.current;
            EquipmentPressed = kb != null && kb.tabKey.wasPressedThisFrame;
            LogTogglePressed = kb != null && kb.cKey.wasPressedThisFrame;
            Gamepad gp = Gamepad.current;
            if (gp == null) return;
            if (gp.selectButton.wasPressedThisFrame) selectHeld = 0f;
            if (selectHeld < 0f) return;
            if (gp.selectButton.isPressed)
            {
                float before = selectHeld;
                selectHeld += Time.unscaledDeltaTime;
                if (before < SelectHoldSeconds && selectHeld >= SelectHoldSeconds) LogTogglePressed = true;
                return;
            }
            if (selectHeld < SelectHoldSeconds) EquipmentPressed = true;
            selectHeld = -1f;
        }

        void DetectDevice()
        {
            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                bool gamepadActivity =
                    gp.leftStick.ReadValue().sqrMagnitude > 0.05f ||
                    gp.rightStick.ReadValue().sqrMagnitude > 0.05f ||
                    gp.dpad.ReadValue().sqrMagnitude > 0.1f ||
                    gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                    gp.buttonWest.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame ||
                    gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
                    gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame ||
                    gp.leftStickButton.wasPressedThisFrame || gp.rightStickButton.wasPressedThisFrame ||
                    gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame;
                if (gamepadActivity)
                {
                    UsingGamepad = true;
                    PadStyle = IsPlayStation(gp) ? GlyphStyle.PlayStation : GlyphStyle.Xbox;
                }
            }

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            bool desktopActivity =
                (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f ||
                                   mouse.leftButton.wasPressedThisFrame ||
                                   mouse.rightButton.wasPressedThisFrame)) ||
                (kb != null && kb.anyKey.wasPressedThisFrame);
            if (desktopActivity) UsingGamepad = false;
        }

        static bool IsPlayStation(Gamepad gp)
        {
            string n = gp.GetType().Name + " " + gp.layout + " " + gp.description.product;
            return n.IndexOf("DualShock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("PlayStation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool AnyButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            Gamepad gp = Gamepad.current;
            return gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                                  gp.buttonWest.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame ||
                                  gp.startButton.wasPressedThisFrame);
        }

        void UpdateNavigation()
        {
            Vector2 v = navigate.ReadValue<Vector2>();
            int x = v.x > 0.5f ? 1 : v.x < -0.5f ? -1 : 0;
            int y = v.y > 0.5f ? 1 : v.y < -0.5f ? -1 : 0;
            NavX = 0;
            NavY = 0;
            if (x != lastNavX || y != lastNavY)
            {
                NavX = x;
                NavY = y;
                navTimer = NavFirstRepeat;
            }
            else if (x != 0 || y != 0)
            {
                navTimer -= Time.unscaledDeltaTime;
                if (navTimer <= 0f)
                {
                    NavX = x;
                    NavY = y;
                    navTimer = NavRepeat;
                }
            }
            lastNavX = x;
            lastNavY = y;
        }

        // ── Rumble ────────────────────────────────────────────────────────────────────

        /// <summary>Shakes the pad in the hands (low = heavy motor, high = sharp motor).</summary>
        public void Rumble(float low, float high, float seconds)
        {
            if (!RumbleEnabled || !UsingGamepad) return;
            Gamepad gp = Gamepad.current;
            if (gp == null) return;
            try
            {
                gp.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
                rumbleLeft = Mathf.Max(rumbleLeft, seconds);
            }
            catch (Exception)
            {
                // Pads without motors.
            }
        }

        void TickRumble()
        {
            if (rumbleLeft <= 0f) return;
            rumbleLeft -= Time.unscaledDeltaTime;
            if (rumbleLeft > 0f) return;
            StopRumble();
        }

        public void StopRumble()
        {
            rumbleLeft = 0f;
            try
            {
                Gamepad.current?.SetMotorSpeeds(0f, 0f);
            }
            catch (Exception)
            {
                // Pads without motors.
            }
        }

        // ── Button names ──────────────────────────────────────────────────────────────

        /// <summary>The button for a command on the device in use: "Q", "RB", "R1", "✕"...</summary>
        public string Glyph(ControlAction c) => Glyph(c, UsingGamepad ? PadStyle : GlyphStyle.Keyboard);

        public string Glyph(ControlAction c, GlyphStyle style)
        {
            InputAction a = A(c);
            int start = style == GlyphStyle.Keyboard ? 0 : 2;
            int end = style == GlyphStyle.Keyboard ? 1 : 2;
            for (int i = start; i <= end; i++)
            {
                string path = a.bindings[i].effectivePath;
                if (!string.IsNullOrEmpty(path)) return PathName(path, style);
            }
            return "—";
        }

        /// <summary>The name of one slot of a command (the controls screen's cells).</summary>
        public string SlotName(ControlAction c, int slot)
        {
            string path = A(c).bindings[slot].effectivePath;
            if (string.IsNullOrEmpty(path)) return "—";
            return PathName(path, slot == 2 ? PadStyle : GlyphStyle.Keyboard);
        }

        public string ActionLabel(ControlAction c) => A(c).name;

        public static string PathName(string path, GlyphStyle style)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            string p = path.ToLowerInvariant();
            bool ps = style == GlyphStyle.PlayStation;
            if (p.StartsWith("<gamepad>") || p.StartsWith("<xinputcontroller>") || p.StartsWith("<dualshockgamepad>"))
            {
                string c = p.Substring(p.IndexOf('/') + 1);
                switch (c)
                {
                    case "buttonsouth": return ps ? "✕" : "A";
                    case "buttoneast": return ps ? "○" : "B";
                    case "buttonwest": return ps ? "□" : "X";
                    case "buttonnorth": return ps ? "△" : "Y";
                    case "leftshoulder": return ps ? "L1" : "LB";
                    case "rightshoulder": return ps ? "R1" : "RB";
                    case "lefttrigger": return ps ? "L2" : "LT";
                    case "righttrigger": return ps ? "R2" : "RT";
                    case "leftstickpress": return "L3";
                    case "rightstickpress": return "R3";
                    case "start": return ps ? "Options" : "Menu";
                    case "select": return ps ? "Create" : "View";
                    case "dpad/up": return "↑";
                    case "dpad/down": return "↓";
                    case "dpad/left": return "←";
                    case "dpad/right": return "→";
                    case "leftstick": return ps ? "L" : "LS";
                    case "rightstick": return ps ? "R" : "RS";
                    default: return c.ToUpperInvariant();
                }
            }
            if (p.StartsWith("<mouse>"))
            {
                string c = p.Substring(p.IndexOf('/') + 1);
                switch (c)
                {
                    case "leftbutton": return "Mouse esq.";
                    case "rightbutton": return "Mouse dir.";
                    case "middlebutton": return "Mouse meio";
                    case "forwardbutton": return "Mouse 5";
                    case "backbutton": return "Mouse 4";
                    case "scroll/up": return "Roda ↑";
                    case "scroll/down": return "Roda ↓";
                    default: return c;
                }
            }
            if (p.StartsWith("<keyboard>"))
            {
                string c = p.Substring(p.IndexOf('/') + 1);
                switch (c)
                {
                    case "space": return "Espaço";
                    case "leftshift": return "Shift";
                    case "rightshift": return "Shift dir.";
                    case "leftctrl": return "Ctrl";
                    case "rightctrl": return "Ctrl dir.";
                    case "leftalt": return "Alt";
                    case "tab": return "Tab";
                    case "enter": return "Enter";
                    case "backspace": return "Backspace";
                    case "capslock": return "Caps";
                    case "uparrow": return "↑";
                    case "downarrow": return "↓";
                    case "leftarrow": return "←";
                    case "rightarrow": return "→";
                    default:
                        if (c.Length == 1) return c.ToUpperInvariant();
                        try
                        {
                            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                        }
                        catch (Exception)
                        {
                            return c;
                        }
                }
            }
            try
            {
                return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            catch (Exception)
            {
                return path;
            }
        }

        // ── Remapping ─────────────────────────────────────────────────────────────────

        InputActionRebindingExtensions.RebindingOperation rebind;

        /// <summary>
        /// Listens for the next key/button for one slot (0, 1 keyboard/mouse; 2 gamepad). If the
        /// new control already belongs to another command on the same device, the two swap.
        /// </summary>
        public void StartRebind(ControlAction c, int slot, Action<bool> done)
        {
            CancelRebind();
            InputAction action = A(c);
            string oldPath = action.bindings[slot].effectivePath;
            bool pad = slot == 2;
            gameplay.Disable();
            var op = action.PerformInteractiveRebinding(slot)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Gamepad>/start")
                .WithControlsExcluding("<Gamepad>/select")
                .WithControlsExcluding("<Gamepad>/leftStick")
                .WithControlsExcluding("<Gamepad>/rightStick")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithTimeout(6f)
                .OnMatchWaitForAnother(0.08f);
            if (pad) op.WithControlsHavingToMatchPath("<Gamepad>");
            else op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            op.OnComplete(o =>
            {
                string newPath = action.bindings[slot].effectivePath;
                ResolveConflict(c, slot, newPath, oldPath);
                FinishRebind();
                done?.Invoke(true);
            });
            op.OnCancel(o =>
            {
                FinishRebind();
                done?.Invoke(false);
            });
            rebind = op;
            op.Start();
        }

        public void CancelRebind()
        {
            if (rebind == null) return;
            rebind.Cancel();
        }

        void FinishRebind()
        {
            rebind?.Dispose();
            rebind = null;
            gameplay.Enable();
        }

        /// <summary>Two commands never share a control on the same device: the other one takes the old key.</summary>
        void ResolveConflict(ControlAction changed, int slot, string newPath, string oldPath)
        {
            if (string.IsNullOrEmpty(newPath)) return;
            bool pad = slot == 2;
            for (int i = 0; i < actions.Length; i++)
            {
                InputAction a = actions[i];
                for (int s = pad ? 2 : 0; s <= (pad ? 2 : 1); s++)
                {
                    if (i == (int)changed && s == slot) continue;
                    // Gourd and bandage share the pad's B on purpose (tap / hold).
                    if (pad && IsGourdBandagePair((ControlAction)i, changed)) continue;
                    if (!string.Equals(a.bindings[s].effectivePath, newPath, StringComparison.OrdinalIgnoreCase)) continue;
                    a.ApplyBindingOverride(s, oldPath ?? string.Empty);
                }
            }
        }

        static bool IsGourdBandagePair(ControlAction a, ControlAction b) =>
            (a == ControlAction.Gourd && b == ControlAction.Bandage) || (a == ControlAction.Bandage && b == ControlAction.Gourd);

        /// <summary>Clears one slot (the "—" of a professional controls screen).</summary>
        public void ClearSlot(ControlAction c, int slot) => A(c).ApplyBindingOverride(slot, string.Empty);

        public void ResetSlot(ControlAction c, int slot) => A(c).RemoveBindingOverride(slot);

        /// <summary>Back to the defaults for one device (keyboard and mouse, or the gamepad).</summary>
        public void ResetDevice(bool pad)
        {
            foreach (InputAction a in actions)
            {
                for (int s = pad ? 2 : 0; s <= (pad ? 2 : 1); s++) a.RemoveBindingOverride(s);
            }
        }

        /// <summary>"Atacar#1=&lt;Keyboard&gt;/u" per changed slot, one per line.</summary>
        public string SaveOverrides()
        {
            var sb = new StringBuilder();
            foreach (InputAction a in actions)
            {
                for (int s = 0; s < 3; s++)
                {
                    InputBinding b = a.bindings[s];
                    if (b.overridePath == null) continue;
                    sb.Append(a.name).Append('#').Append(s).Append('=').Append(b.overridePath).Append('\n');
                }
            }
            return sb.ToString();
        }

        public void LoadOverrides(string data)
        {
            if (string.IsNullOrEmpty(data) || data.TrimStart().StartsWith("{")) return;
            try
            {
                foreach (string line in data.Split('\n'))
                {
                    int hash = line.IndexOf('#'), eq = line.IndexOf('=');
                    if (hash <= 0 || eq <= hash) continue;
                    string name = line.Substring(0, hash);
                    if (!int.TryParse(line.Substring(hash + 1, eq - hash - 1), out int slot) || slot < 0 || slot > 2) continue;
                    InputAction a = gameplay.FindAction(name);
                    if (a == null || Array.IndexOf(actions, a) < 0) continue;
                    a.ApplyBindingOverride(slot, line.Substring(eq + 1).Trim());
                }
            }
            catch (Exception e)
            {
                // Corrupt saved controls must never lock the player out.
                Debug.LogWarning("[Shura] Remapeamento salvo inválido, usando padrão: " + e.Message);
                ResetOverrides();
            }
        }

        public void ResetOverrides()
        {
            foreach (InputAction a in actions) a.RemoveAllBindingOverrides();
        }

        /// <summary>Human-readable current bindings, for the controls panel.</summary>
        public List<KeyValuePair<string, string>> DescribeBindings()
        {
            var list = new List<KeyValuePair<string, string>>();
            GlyphStyle style = UsingGamepad ? PadStyle : GlyphStyle.Keyboard;
            list.Add(new KeyValuePair<string, string>("Mover", style == GlyphStyle.Keyboard ? "WASD · setas" : (style == GlyphStyle.PlayStation ? "L" : "LS")));
            foreach (ControlAction c in new[]
                     {
                         ControlAction.Attack, ControlAction.Parry, ControlAction.Dodge, ControlAction.Swap, ControlAction.LockOn,
                         ControlAction.Detonate, ControlAction.Kiai, ControlAction.Soul, ControlAction.Tool, ControlAction.Gourd, ControlAction.Bandage,
                     })
            {
                string g = Glyph(c, style);
                if (style != GlyphStyle.Keyboard && g == "—")
                {
                    if (c == ControlAction.Detonate) g = Glyph(ControlAction.Attack, style) + "+" + Glyph(ControlAction.Parry, style);
                    else if (c == ControlAction.Kiai) g = Glyph(ControlAction.Parry, style) + "+" + Glyph(ControlAction.Dodge, style);
                }
                if (c == ControlAction.Bandage && style != GlyphStyle.Keyboard) g += " (segurar)";
                list.Add(new KeyValuePair<string, string>(A(c).name, g));
            }
            list.Add(new KeyValuePair<string, string>("Pausar", style == GlyphStyle.Keyboard ? "Esc" : (style == GlyphStyle.PlayStation ? "Options" : "Menu")));
            return list;
        }

        public void Disable()
        {
            StopRumble();
            CancelRebind();
            gameplay.Disable();
            ui.Disable();
        }
    }
}
