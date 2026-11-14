using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shura
{
    /// <summary>
    /// The controls screen: every command, its two keyboard/mouse keys and its gamepad button.
    /// Pick a cell and press the new key; if another command used it, the two swap. Delete (or
    /// the pad's Y / △) clears a cell; each device can go back to its defaults on its own.
    /// </summary>
    public sealed partial class GameUI
    {
        bool controlsOpen;
        int controlsTab;
        /// <summary>-1 = the tabs; 0..n-1 = commands; n = the footer.</summary>
        int controlsRow;
        int controlsCol;
        bool controlsListening;
        string controlsStatus;
        float controlsStatusTime;

        static readonly ControlAction[] ControlRows = (ControlAction[])Enum.GetValues(typeof(ControlAction));

        int ControlsFooterRow => ControlRows.Length;
        bool PadTab => controlsTab == 1;

        public void OpenControls()
        {
            controlsOpen = true;
            controlsTab = game.Controls.UsingGamepad ? 1 : 0;
            controlsRow = 0;
            controlsCol = 0;
            controlsListening = false;
            controlsStatus = null;
        }

        public void CloseControlsForShots() => controlsOpen = false;

        void CloseControls()
        {
            game.Controls.CancelRebind();
            controlsOpen = false;
            SaveControls();
        }

        void SaveControls()
        {
            game.Settings.bindings = game.Controls.SaveOverrides();
            if (!game.Launch.Automated) game.Settings.Save();
        }

        void SetControlsStatus(string text)
        {
            controlsStatus = text;
            controlsStatusTime = Time.unscaledTime;
        }

        void HandleControls(InputService input)
        {
            if (controlsListening || input.Rebinding) return;
            int n = ControlRows.Length;
            if (input.NavY != 0)
            {
                controlsRow = Mathf.Clamp(controlsRow - input.NavY, -1, ControlsFooterRow);
                if (controlsRow >= 0 && controlsRow < n) controlsCol = PadTab ? 0 : Mathf.Clamp(controlsCol, 0, 1);
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }
            if (input.NavX != 0)
            {
                if (controlsRow < 0)
                {
                    controlsTab = 1 - controlsTab;
                    controlsCol = 0;
                }
                else if (controlsRow == ControlsFooterRow || !PadTab)
                {
                    controlsCol = Mathf.Clamp(controlsCol + input.NavX, 0, 1);
                }
                game.Audio.Play(Sfx.UiMove, 0.5f, 1f, 0f);
            }

            bool clear = (Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame) ||
                         (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);
            if (clear && controlsRow >= 0 && controlsRow < n)
            {
                game.Controls.ClearSlot(ControlRows[controlsRow], CurrentSlot);
                SaveControls();
                SetControlsStatus(Strings.ControlsCleared);
                game.Audio.Play(Sfx.UiMove, 0.6f, 0.8f, 0f);
            }

            if (input.Submit) ActivateControls();
            else if (input.Cancel) CloseControls();
        }

        int CurrentSlot => PadTab ? 2 : controlsCol;

        void ActivateControls()
        {
            if (controlsRow < 0)
            {
                controlsTab = 1 - controlsTab;
                return;
            }
            if (controlsRow == ControlsFooterRow)
            {
                if (controlsCol == 0)
                {
                    game.Controls.ResetDevice(PadTab);
                    SaveControls();
                    SetControlsStatus(PadTab ? Strings.ControlsResetPad : Strings.ControlsResetKeyboard);
                    game.Audio.Play(Sfx.UiConfirm, 0.6f, 1f, 0f);
                }
                else CloseControls();
                return;
            }
            StartListening(ControlRows[controlsRow], CurrentSlot);
        }

        void StartListening(ControlAction c, int slot)
        {
            controlsListening = true;
            SetControlsStatus(null);
            game.Audio.Play(Sfx.UiConfirm, 0.6f, 1.2f, 0f);
            game.Controls.StartRebind(c, slot, ok =>
            {
                controlsListening = false;
                if (ok)
                {
                    SaveControls();
                    SetControlsStatus(string.Format(Strings.ControlsDone, game.Controls.ActionLabel(c), game.Controls.SlotName(c, slot)));
                    game.Audio.Play(Sfx.Unsheathe, 0.5f, 1.2f, 0f);
                }
                else SetControlsStatus(Strings.ControlsCanceled);
            });
        }

        /// <summary>A note beside a gamepad cell: tap/hold on the shared button, or the chord that also works.</summary>
        string PadNote(ControlAction c)
        {
            InputService ctl = game.Controls;
            GlyphStyle st = ctl.PadStyle;
            switch (c)
            {
                case ControlAction.Gourd: return Strings.ControlsNoteTap;
                case ControlAction.Bandage: return Strings.ControlsNoteHold;
                case ControlAction.Detonate: return string.Format(Strings.ControlsNoteChord, ctl.Glyph(ControlAction.Attack, st) + " + " + ctl.Glyph(ControlAction.Parry, st));
                case ControlAction.Kiai: return string.Format(Strings.ControlsNoteChord, ctl.Glyph(ControlAction.Parry, st) + " + " + ctl.Glyph(ControlAction.Dodge, st));
                case ControlAction.LockNext:
                case ControlAction.LockPrev: return Strings.ControlsNoteFlick;
                default: return null;
            }
        }

        /// <summary>
        /// A marca da trava de mira (pedido do diretor): nada de anéis sobre o corpo do alvo, que
        /// competem com o anel de aviso do golpe. Só dois colchetes finos nas pontas da barra de
        /// vida dele e um losango pequeno acima, sem pulsar.
        /// </summary>
        void DrawLockReticle(Vector2 world, float barHeight)
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            const float margin = 0.12f;
            Vector2 left = WorldToGui(cam, world + new Vector2(-Enemy.BarWidth * 0.5f - margin, barHeight));
            Vector2 right = WorldToGui(cam, world + new Vector2(Enemy.BarWidth * 0.5f + margin, barHeight));
            Color red = Pal.danger.WithAlpha(0.9f);
            const float h = 12f, w = 2f, tick = 5f;
            // [ à esquerda
            DrawRect(new Rect(left.x - w, left.y - h * 0.5f, w, h), red);
            DrawRect(new Rect(left.x - w, left.y - h * 0.5f, tick, w), red);
            DrawRect(new Rect(left.x - w, left.y + h * 0.5f - w, tick, w), red);
            // ] à direita
            DrawRect(new Rect(right.x, right.y - h * 0.5f, w, h), red);
            DrawRect(new Rect(right.x + w - tick, right.y - h * 0.5f, tick, w), red);
            DrawRect(new Rect(right.x + w - tick, right.y + h * 0.5f - w, tick, w), red);
            const float d = 9f;
            var top = new Vector2((left.x + right.x) * 0.5f, left.y - h * 0.5f - 9f);
            DrawTex(new Rect(top.x - d * 0.5f, top.y - d * 0.5f, d, d), ShapeLibrary.Get(Shape.Diamond).texture, red);
        }

        void DrawControlsScreen()
        {
            InputService ctl = game.Controls;
            var panel = new Rect(130f, 30f, 1660f, 1020f);
            kit.PaperPanel(panel);
            Heading(new Rect(panel.x, panel.y + 22f, panel.width, 90f), Strings.ControlsScreenTitle, UiKit.Ink, UiKit.Shu.WithAlpha(0.85f), 520f);
            Event e = Event.current;
            bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
            bool mouse = !game.Launch.Automated && !controlsListening;

            // Tabs.
            string[] tabs = { Strings.ControlsTabKeyboard, ctl.PadStyle == GlyphStyle.PlayStation ? Strings.ControlsTabPadPs : Strings.ControlsTabPadXbox };
            for (int t = 0; t < 2; t++)
            {
                var tr = new Rect(panel.center.x - 470f + t * 480f, panel.y + 122f, 460f, 52f);
                bool on = controlsTab == t;
                if (on) kit.Stroke(new Rect(tr.x - 10f, tr.y - 4f, tr.width + 20f, tr.height + 8f), (controlsRow < 0 ? UiKit.Shu : UiKit.Ink).WithAlpha(0.9f));
                FitLabel(tr, tabs[t], itemStyle, on ? UiKit.PaperText : UiKit.InkSoft);
                if (mouse && tr.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0)
                {
                    controlsTab = t;
                    controlsRow = Mathf.Max(controlsRow, 0);
                    controlsCol = 0;
                    e.Use();
                    return;
                }
            }

            // Column titles.
            float left = panel.x + 90f, nameW = 520f, cellW = 380f, gap = 24f;
            float y0 = panel.y + 192f, rowH = 34f;
            Label(new Rect(left, y0, nameW, 30f), Strings.ControlsColAction, capsStyle, UiKit.InkSoft);
            if (PadTab)
            {
                Label(new Rect(left + nameW, y0, cellW, 30f), Strings.ControlsColButton, capsStyle, UiKit.InkSoft);
                Label(new Rect(left + nameW + cellW + gap, y0, cellW + 120f, 30f), Strings.ControlsColNote, capsStyle, UiKit.InkSoft);
            }
            else
            {
                Label(new Rect(left + nameW, y0, cellW, 30f), Strings.ControlsColPrimary, capsStyle, UiKit.InkSoft);
                Label(new Rect(left + nameW + cellW + gap, y0, cellW, 30f), Strings.ControlsColSecondary, capsStyle, UiKit.InkSoft);
            }
            kit.Underline(new Rect(left, y0 + 30f, panel.width - 180f, 10f), UiKit.Ink.WithAlpha(0.4f));

            for (int i = 0; i < ControlRows.Length; i++)
            {
                ControlAction c = ControlRows[i];
                float y = y0 + 40f + i * rowH;
                bool rowSel = controlsRow == i;
                FitLabel(new Rect(left + 10f, y, nameW - 10f, rowH), ctl.ActionLabel(c), smallStyle, rowSel ? UiKit.Shu : UiKit.Ink);
                int cells = PadTab ? 1 : 2;
                for (int k = 0; k < cells; k++)
                {
                    int slot = PadTab ? 2 : k;
                    var cell = new Rect(left + nameW + k * (cellW + gap), y + 2f, cellW, rowH - 4f);
                    bool sel = rowSel && controlsCol == k;
                    if (sel) kit.Stroke(cell, UiKit.Shu.WithAlpha(controlsListening ? 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 10f) : 0.85f));
                    else kit.Dab(new Rect(cell.x, cell.y + 4f, cell.width, cell.height - 8f), UiKit.Ink.WithAlpha(0.06f));
                    string text = sel && controlsListening ? Strings.ControlsPress : ctl.SlotName(c, slot);
                    FitLabel(cell, text, smallStyleCenter, sel ? UiKit.PaperText : UiKit.Ink);
                    if (mouse && cell.Contains(e.mousePosition))
                    {
                        if (e.type == EventType.Repaint && mouseMoved)
                        {
                            controlsRow = i;
                            controlsCol = k;
                        }
                        if (e.type == EventType.MouseDown && e.button == 0)
                        {
                            controlsRow = i;
                            controlsCol = k;
                            e.Use();
                            StartListening(c, slot);
                            return;
                        }
                    }
                }
                if (PadTab)
                {
                    string note = PadNote(c);
                    if (note != null) FitLabel(new Rect(left + nameW + cellW + gap, y, cellW + 120f, rowH), note, smallStyle, UiKit.InkSoft);
                }
            }

            // Footer: defaults for this device, and back.
            float fy = y0 + 40f + ControlRows.Length * rowH + 14f;
            string[] foot = { PadTab ? Strings.ControlsResetPadButton : Strings.ControlsResetKeyboardButton, Strings.Back };
            for (int k = 0; k < 2; k++)
            {
                var br = new Rect(panel.center.x - 560f + k * 640f, fy, 480f, 56f);
                bool sel = controlsRow == ControlsFooterRow && controlsCol == k;
                if (sel) kit.Stroke(new Rect(br.x - 16f, br.y - 4f, br.width + 32f, br.height + 8f), UiKit.Shu.WithAlpha(0.9f));
                FitLabel(br, foot[k], itemStyle, sel ? UiKit.PaperText : UiKit.Ink);
                if (mouse && br.Contains(e.mousePosition))
                {
                    if (e.type == EventType.Repaint && mouseMoved)
                    {
                        controlsRow = ControlsFooterRow;
                        controlsCol = k;
                    }
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        controlsRow = ControlsFooterRow;
                        controlsCol = k;
                        e.Use();
                        ActivateControls();
                        return;
                    }
                }
            }

            string status = controlsListening
                ? string.Format(Strings.ControlsListening, ControlRows[Mathf.Clamp(controlsRow, 0, ControlRows.Length - 1)] is ControlAction ca ? ctl.ActionLabel(ca) : "")
                : controlsStatus != null && Time.unscaledTime - controlsStatusTime < 4f ? controlsStatus : (PadTab ? Strings.ControlsHelpPad : Strings.ControlsHelpKeyboard);
            FitLabel(new Rect(panel.x + 110f, fy + 66f, panel.width - 220f, 30f), status, smallStyleCenter, controlsListening ? UiKit.Shu : UiKit.InkSoft);
        }
    }
}
