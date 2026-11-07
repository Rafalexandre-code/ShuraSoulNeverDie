using System;
using System.Collections.Generic;
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

        /// <summary>Normalized aim direction, or zero when the source has no explicit aim.</summary>
        Vector2 AimDirection(Vector2 playerWorldPosition);
    }

    /// <summary>
    /// Actions are built in code (not an .inputactions asset) so they are easy to review and
    /// remap; overrides are persisted as JSON in the settings.
    /// </summary>
    public sealed class InputService : IPlayerInputSource
    {
        const float NavFirstRepeat = 0.38f;
        const float NavRepeat = 0.11f;
        const float StickDeadzoneSqr = 0.12f;

        readonly InputActionMap gameplay = new InputActionMap("Gameplay");
        readonly InputActionMap ui = new InputActionMap("UI");
        readonly InputAction move, aim, attack, parry, dodge, swap, pause;
        readonly InputAction navigate, submit, cancel;

        int lastNavX, lastNavY;
        float navTimer;

        public InputBuffer Buffer { get; } = new InputBuffer();
        public bool UsingGamepad { get; private set; }
        public bool PausePressed { get; private set; }
        public bool Submit { get; private set; }
        public bool Cancel { get; private set; }
        public bool AnyPressed { get; private set; }
        /// <summary>Tab / gamepad Select: opens the Equipment screen.</summary>
        public bool EquipmentPressed { get; private set; }
        public int NavX { get; private set; }
        public int NavY { get; private set; }

        /// <summary>Screen → world conversion, provided by the camera.</summary>
        public Func<Vector2, Vector2> ScreenToWorld { get; set; }

        public InputService()
        {
            move = gameplay.AddAction("Mover", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            aim = gameplay.AddAction("Mirar", InputActionType.Value, "<Gamepad>/rightStick");

            attack = gameplay.AddAction("Atacar", InputActionType.Button);
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Keyboard>/j");
            attack.AddBinding("<Gamepad>/rightShoulder");

            parry = gameplay.AddAction("Aparar", InputActionType.Button);
            parry.AddBinding("<Mouse>/rightButton");
            parry.AddBinding("<Keyboard>/k");
            parry.AddBinding("<Gamepad>/leftShoulder");

            dodge = gameplay.AddAction("Esquivar", InputActionType.Button);
            dodge.AddBinding("<Keyboard>/space");
            dodge.AddBinding("<Keyboard>/l");
            dodge.AddBinding("<Gamepad>/buttonSouth");

            swap = gameplay.AddAction("Trocar arma", InputActionType.Button);
            swap.AddBinding("<Keyboard>/q");
            swap.AddBinding("<Mouse>/middleButton");
            swap.AddBinding("<Gamepad>/buttonNorth");

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

        public bool ParryHeld => parry.IsPressed();

        public Vector2 AimDirection(Vector2 playerWorldPosition)
        {
            if (UsingGamepad)
            {
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
            DetectDevice();
            PausePressed = pause.WasPressedThisFrame();
            Submit = submit.WasPressedThisFrame();
            Cancel = cancel.WasPressedThisFrame();
            AnyPressed = AnyButtonPressed();
            EquipmentPressed = (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ||
                               (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            UpdateNavigation();

            if (!gameplayActive) return;
            if (attack.WasPressedThisFrame()) Buffer.Press(BufferedAction.Attack, tuning.actionBufferFrames);
            if (dodge.WasPressedThisFrame()) Buffer.Press(BufferedAction.Dodge, tuning.actionBufferFrames);
            if (parry.WasPressedThisFrame()) Buffer.Press(BufferedAction.Parry, tuning.parryBufferFrames);
            if (swap.WasPressedThisFrame()) Buffer.Press(BufferedAction.Swap, tuning.parryBufferFrames);
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
                    gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame;
                if (gamepadActivity) UsingGamepad = true;
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

        public string SaveOverrides()
        {
            return gameplay.SaveBindingOverridesAsJson();
        }

        public void LoadOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                gameplay.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                // Corrupt or outdated overrides must never lock the player out of the controls.
                Debug.LogWarning("[Shura] Remapeamento salvo inválido, usando padrão: " + e.Message);
                gameplay.RemoveAllBindingOverrides();
            }
        }

        public void ResetOverrides()
        {
            gameplay.RemoveAllBindingOverrides();
        }

        /// <summary>Human-readable current bindings, for the controls panel.</summary>
        public List<KeyValuePair<string, string>> DescribeBindings()
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (InputAction action in new[] { move, attack, parry, dodge, swap, pause })
            {
                string display;
                try
                {
                    display = action.GetBindingDisplayString();
                }
                catch (Exception)
                {
                    display = "?";
                }
                list.Add(new KeyValuePair<string, string>(action.name, display));
            }
            return list;
        }

        public void Disable()
        {
            gameplay.Disable();
            ui.Disable();
        }
    }
}
