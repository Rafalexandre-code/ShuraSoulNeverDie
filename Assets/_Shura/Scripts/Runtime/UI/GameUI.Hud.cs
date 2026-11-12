using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The battle HUD. Top left, the duelist's card: their own character, large, in the pose of
    /// the guard they hold (and swinging, dodging, drinking as they do), the four guards as
    /// small figures, health, posture, breath and Shura, and a paper doll of their wounds.
    /// Bottom left, the belt: both blades, the gourd's sips as gourds, bandages, the tool and
    /// its spirit papers, the purse. Bottom right, the log: everything that used to shout in the
    /// middle of the screen (items, new enemies, rewards) is written there instead.
    /// </summary>
    public sealed partial class GameUI
    {
        // ── The log ──────────────────────────────────────────────────────────────────

        sealed class LogEntry
        {
            public string text;
            public Color color;
            public float born;
            public string stamp;
        }

        readonly List<LogEntry> log = new List<LogEntry>();
        const int LogKeep = 40;
        const int LogShown = 8;

        /// <summary>During a run, notices go to the log; on menus they still appear as toasts.</summary>
        bool LogsNotices => (game.Run != null &&
                             (game.CurrentMode == Game.Mode.Playing || game.CurrentMode == Game.Mode.Paused || game.CurrentMode == Game.Mode.Reward ||
                              game.CurrentMode == Game.Mode.Route || game.CurrentMode == Game.Mode.Equipment)) ||
                            (game.Remote.Joined && !game.Remote.Between);

        /// <summary>The log starts folded into a small tab; opened, it shows the last lines.</summary>
        bool logOpen;
        int logUnread;

        void UpdateLogToggle()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            bool hudUp = game.CurrentMode == Game.Mode.Playing || (game.CurrentMode == Game.Mode.Remote && game.Remote.Joined && !game.Remote.Between);
            if (!hudUp || editingName || editingIp) return;
            if (game.Controls.LogTogglePressed) ToggleLog();
        }

        void ToggleLog()
        {
            logOpen = !logOpen;
            if (logOpen) logUnread = 0;
            game.Audio.Play(Sfx.UiMove, 0.4f, logOpen ? 1.1f : 0.9f, 0f);
        }

        public void Log(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!logOpen) logUnread++;
            // The same line twice in a row folds into one.
            if (log.Count > 0 && log[log.Count - 1].text == text && Time.unscaledTime - log[log.Count - 1].born < 3f)
            {
                log[log.Count - 1].born = Time.unscaledTime;
                return;
            }
            float t = game.Run != null ? game.Run.Elapsed : Time.unscaledTime - logEpoch;
            int s = Mathf.FloorToInt(t);
            log.Add(new LogEntry { text = text, color = color, born = Time.unscaledTime, stamp = (s / 60).ToString("00") + ":" + (s % 60).ToString("00") });
            if (log.Count > LogKeep) log.RemoveAt(0);
        }

        public void ClearLog()
        {
            log.Clear();
            logEpoch = Time.unscaledTime;
        }

        GUIStyle logStyle;

        void DrawLog()
        {
            if (log.Count == 0) return;
            if (!logOpen)
            {
                DrawLogTab();
                return;
            }
            if (logStyle == null)
            {
                logStyle = new GUIStyle(smallStyle) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
            }
            const float w = 560f, maxH = 330f, textX = 108f;
            float textW = w - textX - 26f;
            // Newest at the bottom; long lines wrap; as many as fit.
            var shown = new List<LogEntry>();
            var heights = new List<float>();
            float total = 0f;
            for (int i = log.Count - 1; i >= 0 && shown.Count < LogShown; i--)
            {
                float lh = Mathf.Max(28f, logStyle.CalcHeight(new GUIContent(log[i].text), textW) + 4f);
                if (total + lh > maxH && shown.Count > 0) break;
                shown.Insert(0, log[i]);
                heights.Insert(0, lh);
                total += lh;
            }
            float h = 48f + total;
            var r = new Rect(VW - w - 10f, VH - h - 10f, w, h);
            Panel(6, r);
            if (LogClicked(new Rect(r.x, r.y, r.width, 44f))) return;
            float newest = Time.unscaledTime - log[log.Count - 1].born;
            // A solid backing so the lines read over bright floors, then the brush panel.
            DrawRect(new Rect(r.x + 14f, r.y + 6f, r.width - 14f, r.height - 6f), new Color(0.05f, 0.04f, 0.05f, 0.62f));
            DrawRect(new Rect(r.x + 14f, r.y + 6f, r.width - 14f, 2f), Pal.gold.WithAlpha(0.35f));
            kit.InkPanel(r, Mathf.Lerp(0.92f, 0.7f, Mathf.Clamp01((newest - 6f) / 4f)));
            Label(new Rect(r.x + 32f, r.y + 12f, 200f, 24f), Strings.HudLog, capsStyle, Pal.gold.WithAlpha(0.75f));
            Label(new Rect(r.xMax - 260f, r.y + 12f, 240f, 24f), Strings.HudLogFold, capsRightStyle, UiKit.PaperDim.WithAlpha(0.7f));
            float y = r.y + 40f;
            for (int i = 0; i < shown.Count; i++)
            {
                LogEntry e = shown[i];
                float age = Time.unscaledTime - e.born;
                float a = age < 12f ? 1f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((age - 12f) / 4f));
                float slide = age < 0.25f ? (1f - age / 0.25f) * 40f : 0f;
                Label(new Rect(r.x + 28f + slide, y + 2f, 66f, 24f), e.stamp, capsStyle, UiKit.PaperDim.WithAlpha(0.55f * a));
                kit.Dab(new Rect(r.x + 88f + slide, y + 7f, 12f, 12f), e.color.WithAlpha(a));
                Label(new Rect(r.x + textX + slide, y, textW, heights[i]), e.text, logStyle, Color.Lerp(UiKit.PaperText, e.color, 0.3f).WithAlpha(a));
                y += heights[i];
            }
            UiKit.Fade = 1f;
        }

        /// <summary>Folded: one small tab in the corner, with how many lines are new.</summary>
        void DrawLogTab()
        {
            var r = new Rect(VW - 300f, VH - 54f, 290f, 46f);
            hudMarks[HudPart.Log] = new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 10f);
            Panel(6, r);
            if (LogClicked(r))
            {
                UiKit.Fade = 1f;
                return;
            }
            DrawRect(new Rect(r.x + 6f, r.y + 4f, r.width - 6f, r.height - 4f), new Color(0.05f, 0.04f, 0.05f, 0.72f));
            DrawRect(new Rect(r.x + 6f, r.y + 4f, r.width - 6f, 2f), Pal.gold.WithAlpha(0.45f));
            kit.InkPanel(r, 0.8f);
            bool fresh = logUnread > 0;
            float pulse = fresh ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f) : 0.6f;
            Label(new Rect(r.x + 26f, r.y + 11f, 170f, 24f), Strings.HudLog, capsStyle, Pal.gold.WithAlpha(pulse));
            string right = (fresh ? "+" + logUnread + "  " : "") + (game.Controls.UsingGamepad ? (game.Controls.PadStyle == GlyphStyle.PlayStation ? "segure Create" : "segure View") : "C");
            Label(new Rect(r.x + 120f, r.y + 11f, r.width - 140f, 24f), right, capsRightStyle, fresh ? Pal.gold : UiKit.PaperDim);
            UiKit.Fade = 1f;
        }

        bool LogClicked(Rect r)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use();
            ToggleLog();
            return true;
        }

        // ── Keeping the fight in view ────────────────────────────────────────────────

        readonly List<Rect> bodies = new List<Rect>();
        readonly Dictionary<int, float> panelFade = new Dictionary<int, float>();

        Rect CardRect => new Rect(8f, 4f, 632f, 330f);
        Rect BeltRect => new Rect(8f, VH - 150f, 900f, 142f);
        Rect TitleRect => new Rect(VW * 0.5f - 470f, 0f, 940f, 200f);
        Rect FlowRect => new Rect(VW - 470f, 2f, 460f, 280f);
        Rect PartyRect(int players) => new Rect(VW - 470f, 230f, 460f, 34f + Mathf.Max(0, players - 1) * 40f);

        /// <summary>Where every duelist and enemy stands on screen this frame (GUI space, head to feet).</summary>
        void CollectBodies()
        {
            bodies.Clear();
            Camera cam = game.CameraRig.Cam;
            if (cam == null) return;
            if (game.Remote.Joined)
            {
                foreach (RemoteView.Puppet p in game.Remote.View.Players) AddBody(cam, p.pos, p.top + 0.3f);
                foreach (RemoteView.Puppet p in game.Remote.View.Enemies)
                {
                    if (p.row != null && !MiniJson.B(p.row, 32)) AddBody(cam, p.pos, p.top + 0.3f);
                }
                return;
            }
            World w = game.World;
            foreach (PlayerController p in w.Players)
            {
                if (p.gameObject.activeSelf) AddBody(cam, p.RenderPosition, 1.4f);
            }
            foreach (Enemy e in w.Enemies)
            {
                if (!e.IsGone) AddBody(cam, e.RenderPosition, e.Radius * 2.6f + 0.6f);
            }
        }

        void AddBody(Camera cam, Vector2 at, float height)
        {
            Vector2 feet = WorldToGui(cam, at + Vector2.down * 0.35f);
            Vector2 head = WorldToGui(cam, at + Vector2.up * height);
            float h = Mathf.Max(20f, feet.y - head.y);
            float w = Mathf.Max(40f, h * 0.7f);
            bodies.Add(new Rect(feet.x - w * 0.5f, head.y, w, h));
        }

        /// <summary>Draws the next HUD panel see-through while anyone stands under it (eased, so it never flickers).</summary>
        void Panel(int id, Rect r, float seeThrough = 0.18f)
        {
            bool covered = false;
            foreach (Rect b in bodies)
            {
                if (b.Overlaps(r))
                {
                    covered = true;
                    break;
                }
            }
            if (!panelFade.TryGetValue(id, out float f)) f = 1f;
            if (Event.current.type == EventType.Repaint) f = Mathf.MoveTowards(f, covered ? seeThrough : 1f, Time.unscaledDeltaTime * 5f);
            panelFade[id] = f;
            UiKit.Fade = f;
        }

        // ── What the HUD shows ───────────────────────────────────────────────────────

        /// <summary>
        /// One duelist as the HUD reads them: filled from the local player, or from the host's
        /// snapshot when this game is a guest in a friend's duel. The drawing never touches
        /// the simulation, so both look exactly the same.
        /// </summary>
        public sealed class HudData
        {
            public string name = "";
            public int art;
            public bool alive = true;
            public float health = 1f, maxHealth = 1f, posture, stamina = 1f;
            public bool exhausted, asura, mushin, moving, cycle, muramasa;
            public float shura;
            public int shuraLevel;
            public Kamae stance = Kamae.Chudan;
            public float dodge;
            public int maxDodge;
            public int charge;
            public string curse;
            public int curseRooms;
            /// <summary>The soul a level-III technique keeps: its name, ready this room, seconds left in it.</summary>
            public string soulName;
            public bool soulReady;
            public int soulSeconds;
            public int woundMask;
            public PlayerController.State state;
            /// <summary>The blade in hand.</summary>
            public WeaponDef weapon = WeaponDefs.Katana;
            /// <summary>The two blades as the belt shows them, and which one is in hand.</summary>
            public readonly WeaponDef[] belt = { WeaponDefs.Katana, WeaponDefs.Montante };
            public int activeSlot;
            public int flow;
            public int sips, maxSips, bandages, papers, mon;
            public ToolKind tool;
            public bool hasBuild = true;

            public bool HasWound(WoundRegion r) => (woundMask & (1 << (int)r)) != 0;

            public void From(PlayerController p)
            {
                PlayerBuild b = p.Build;
                name = p.DisplayName;
                art = p.ArtIndex;
                alive = p.IsAlive;
                health = p.Health;
                maxHealth = Mathf.Max(1f, p.MaxHealth);
                posture = p.Posture.Ratio;
                stamina = p.Stamina / PlayerController.MaxStamina;
                exhausted = p.Exhausted;
                asura = p.InAsura;
                mushin = p.InMushin;
                moving = p.Velocity.sqrMagnitude > 0.5f;
                cycle = p.CycleFramesLeft > 0;
                shura = b != null ? b.Shura / ShuraRules.Max : 0f;
                shuraLevel = p.ShuraLevel;
                stance = p.Stance;
                dodge = p.DodgeCharges;
                maxDodge = p.MaxDodgeCharges;
                charge = p.Charge;
                woundMask = p.WoundMask;
                state = p.CurrentState;
                weapon = p.Weapon;
                hasBuild = b != null;
                flow = b != null ? b.Flow.Level : 0;
                curse = b != null && b.CurseRooms > 0 ? RunCurses.Name(b.Curse) : null;
                curseRooms = b != null ? b.CurseRooms : 0;
                muramasa = b != null && b.Muramasa;
                EnemyDef soul = p.InSoul ? p.SoulForm : p.SoulAvailable;
                soulName = soul?.displayName;
                soulReady = p.SoulReady;
                soulSeconds = p.InSoul ? Mathf.CeilToInt(p.SoulFramesLeft / (float)SimClock.TickRate) : 0;
                sips = p.Sips;
                maxSips = p.MaxSips;
                if (b == null) return;
                belt[0] = b.Weapons[0] ?? WeaponDefs.Katana;
                belt[1] = b.Weapons[1] ?? WeaponDefs.Montante;
                activeSlot = b.ActiveWeapon;
                bandages = b.Bandages;
                papers = b.SpiritPapers;
                mon = b.Mon;
                tool = b.Tool;
            }

            /// <summary>A row of the host's snapshot ("P"), as NetSession writes it.</summary>
            public void FromRow(List<object> r)
            {
                name = MiniJson.S(r, 1);
                art = MiniJson.I(r, 33);
                alive = MiniJson.B(r, 21);
                maxHealth = Mathf.Max(1f, MiniJson.F(r, 27, 1f));
                health = MiniJson.F(r, 26);
                posture = MiniJson.F(r, 18);
                dodge = MiniJson.F(r, 19);
                maxDodge = MiniJson.I(r, 20);
                flow = MiniJson.I(r, 29);
                stamina = MiniJson.F(r, 34, 1f);
                exhausted = MiniJson.B(r, 35);
                shura = MiniJson.F(r, 36);
                shuraLevel = MiniJson.I(r, 37);
                stance = (Kamae)Mathf.Clamp(MiniJson.I(r, 38), 0, 3);
                sips = MiniJson.I(r, 39);
                maxSips = MiniJson.I(r, 40);
                tool = (ToolKind)Mathf.Clamp(MiniJson.I(r, 41), 0, PlayerController.ToolNames.Length - 1);
                papers = MiniJson.I(r, 42);
                woundMask = MiniJson.I(r, 43);
                mushin = MiniJson.B(r, 44);
                asura = MiniJson.B(r, 45);
                charge = MiniJson.I(r, 52);
                bandages = MiniJson.I(r, 53);
                mon = MiniJson.I(r, 54);
                string c = MiniJson.S(r, 55);
                curse = string.IsNullOrEmpty(c) ? null : c;
                curseRooms = MiniJson.I(r, 56);
                state = (PlayerController.State)MiniJson.I(r, 59);
                moving = MiniJson.B(r, 60);
                string a = MiniJson.S(r, 61), o = MiniJson.S(r, 62);
                weapon = WeaponDefs.Exists(a) ? WeaponDefs.Get(a) : WeaponDefs.Katana;
                belt[0] = weapon;
                belt[1] = WeaponDefs.Exists(o) ? WeaponDefs.Get(o) : WeaponDefs.Montante;
                activeSlot = 0;
                cycle = MiniJson.B(r, 63);
                muramasa = MiniJson.B(r, 64);
                string sn = MiniJson.S(r, 65);
                soulName = string.IsNullOrEmpty(sn) ? null : sn;
                soulReady = MiniJson.B(r, 66);
                soulSeconds = MiniJson.I(r, 67);
                hasBuild = true;
            }
        }

        // ── The duelist's card ───────────────────────────────────────────────────────

        static readonly string[] KamaeKanji = { "上段", "中段", "下段", "脇構" };
        /// <summary>Blade angle of each guard, GUI degrees clockwise from facing right.</summary>
        static readonly float[] KamaeAngle = { -112f, -18f, 38f, 158f };
        /// <summary>Where the hands hold the grip, as a fraction of the body sprite (x from left, y from top).</summary>
        static readonly Vector2[] KamaeHands = { new Vector2(0.48f, 0.2f), new Vector2(0.66f, 0.58f), new Vector2(0.62f, 0.66f), new Vector2(0.34f, 0.66f) };

        /// <summary>The parts of the HUD, for the dojo's tour of the screen.</summary>
        public enum HudPart
        {
            None, Portrait, Stances, Health, Posture, Breath, Shura, Dodge, Wounds, Status, Flow, Ecos,
            Weapons, Gourd, Bandage, Tool, Mon, Log,
        }

        /// <summary>Where each part of the HUD was drawn this frame (HUD canvas coordinates).</summary>
        readonly Dictionary<HudPart, Rect> hudMarks = new Dictionary<HudPart, Rect>();

        void DrawDuelistCard(HudData p)
        {
            var card = new Rect(8f, 4f, 632f, 330f);
            kit.InkPanel(card, 0.86f);

            // Medallion with the duelist in their guard.
            var med = new Rect(30f, 26f, 196f, 196f);
            int flow = p.flow;
            Color ring = p.asura ? Pal.shu : flow > 0 ? Pal.gold : UiKit.PaperDim.WithAlpha(0.55f);
            kit.Dab(new Rect(med.x + 8f, med.y + 8f, med.width - 16f, med.height - 16f), new Color(0.05f, 0.04f, 0.06f, 0.85f));
            DrawPortrait(new Rect(med.x + 18f, med.y + 10f, med.width - 36f, med.height - 28f), p);
            kit.Enso(new Rect(med.x - 6f, med.y - 6f, med.width + 12f, med.height + 12f), ring, Time.unscaledTime * (6f + 10f * flow));
            int k = (int)p.stance;
            Label(new Rect(med.x - 10f, med.yMax - 10f, med.width + 20f, 34f), KamaeKanji[k] + "  " + PlayerController.KamaeName(p.stance), capsCenterStyle, Pal.playerAccent);
            DrawStanceRow(new Rect(med.x - 10f, med.yMax + 26f, med.width + 20f, 64f), p);
            hudMarks[HudPart.Portrait] = new Rect(med.x - 8f, med.y - 8f, med.width + 16f, med.height + 44f);
            hudMarks[HudPart.Stances] = new Rect(med.x - 14f, med.yMax + 22f, med.width + 28f, 72f);

            // Name and the four meters.
            float x = 252f, w = 300f;
            hudMarks[HudPart.Health] = new Rect(x - 12f, 58f, w + 24f, 70f);
            hudMarks[HudPart.Posture] = new Rect(x - 12f, 124f, w + 24f, 54f);
            hudMarks[HudPart.Breath] = new Rect(x - 10f, 178f, (w - 20f) * 0.5f + 16f, 46f);
            hudMarks[HudPart.Shura] = new Rect(x + (w - 20f) * 0.5f + 10f, 178f, (w - 20f) * 0.5f + 16f, 46f);
            hudMarks[HudPart.Dodge] = new Rect(x - 10f, 224f, w + 20f, 38f);
            hudMarks[HudPart.Status] = new Rect(x - 10f, 262f, w + 60f, 38f);
            hudMarks[HudPart.Wounds] = new Rect(card.xMax - 72f, 60f, 62f, 102f);
            Label(new Rect(x, 16f, w + 60f, 44f), p.name, weaponHudStyle, UiKit.PaperText);
            DrawRect(new Rect(x, 56f, w, 2f), Pal.gold.WithAlpha(0.3f));
            float lowHp = p.health / p.maxHealth < 0.3f ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            Icon(new Rect(x - 2f, 61f, 26f * lowHp, 26f * lowHp), HudArt.Heart(), Color.white);
            Label(new Rect(x + 30f, 62f, w, 24f), Strings.HudHealth, capsStyle, UiKit.PaperDim);
            Label(new Rect(x, 62f, w, 24f), Mathf.CeilToInt(p.health) + " / " + Mathf.RoundToInt(p.maxHealth), smallRightStyle, UiKit.PaperText);
            float hpRatio = Mathf.Clamp01(p.health / p.maxHealth);
            float ghostRatio = Mathf.Clamp01(ghostHp / p.maxHealth);
            Color hpColor = hpRatio < 0.3f ? Color.Lerp(UiKit.ShuBright, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f)) : UiKit.ShuBright;
            kit.Bar(new Rect(x - 8f, 86f, w + 16f, 38f), hpRatio, hpColor, Color.black.WithAlpha(0.7f), ghostRatio, UiKit.PaperText.WithAlpha(0.8f));

            float pr = p.posture;
            Color pc = Color.Lerp(Pal.postureLow, Pal.postureHigh, pr);
            if (pr > 0.8f) pc = Color.Lerp(pc, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f));
            Icon(new Rect(x - 2f, 127f, 26f, 26f), HudArt.Tsuba(), pr > 0.8f ? pc : Color.white);
            Label(new Rect(x + 30f, 128f, w, 24f), Strings.HudPosture, capsStyle, UiKit.PaperDim);
            kit.Bar(new Rect(x - 8f, 152f, w + 16f, 22f), pr, pc, Color.black.WithAlpha(0.7f));

            // Breath and Shura side by side.
            float half = (w - 20f) * 0.5f;
            float st = p.stamina;
            Color sc = p.exhausted ? Color.Lerp(Pal.danger, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f)) : Pal.breath;
            Icon(new Rect(x - 2f, 181f, 24f, 24f), HudArt.Wind(), p.exhausted ? Pal.danger : Color.white);
            Label(new Rect(x + 28f, 182f, half, 24f), Strings.HudBreath, capsStyle, p.exhausted ? Pal.danger : UiKit.PaperDim);
            kit.Bar(new Rect(x - 6f, 206f, half + 6f, 14f), st, sc, Color.black.WithAlpha(0.7f));
            float sh = p.shura;
            int lvl = p.shuraLevel;
            float sx = x + half + 20f;
            float rage = lvl > 0 ? 1f + 0.08f * lvl * Mathf.Sin(Time.unscaledTime * (4f + 3f * lvl)) : 1f;
            Icon(new Rect(sx - 2f, 181f, 24f * rage, 24f * rage), HudArt.Oni(), lvl > 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            FitLabel(new Rect(sx + 28f, 182f, half - 28f, 24f), lvl > 0 ? Strings.HudShura + " · " + ShuraRules.LevelName(lvl) : Strings.HudShura, capsStyle, lvl > 0 ? Pal.shu : UiKit.PaperDim);
            Color shc = p.asura ? Color.Lerp(Pal.shu, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : Pal.shu;
            kit.Bar(new Rect(sx - 6f, 206f, half + 6f, 14f), sh, shc, Color.black.WithAlpha(0.7f));

            // Dodges: wind marks.
            Icon(new Rect(x - 2f, 231f, 24f, 24f), HudArt.Feather(), Color.white);
            Label(new Rect(x + 28f, 232f, 140f, 24f), Strings.HudDodge, capsStyle, UiKit.PaperDim);
            for (int i = 0; i < p.maxDodge; i++)
            {
                var r = new Rect(x + 130f + i * 36f, 228f, 30f, 30f);
                float fill = Mathf.Clamp01(p.dodge - i);
                kit.Enso(r, Pal.playerAccent.WithAlpha(fill >= 1f ? 0.95f : 0.3f), i * 70f);
                if (fill > 0f) kit.Dab(new Rect(r.x + 8f, r.y + 8f, 14f, 14f), Pal.playerAccent.WithAlpha(fill >= 1f ? 1f : 0.35f));
            }

            // Combo charge, the Links' cycle and the curse under the meters.
            string status = null;
            Color statusColor = UiKit.PaperText;
            if (p.cycle)
            {
                status = Strings.PopupCycle;
                statusColor = Pal.gold;
            }
            else if (p.charge > 0)
            {
                status = string.Format(Strings.PopupCharge, p.charge);
                statusColor = p.weapon.trail;
            }
            if (p.curse != null && p.curseRooms > 0)
            {
                status = (status != null ? status + "  ·  " : "") + p.curse + " (" + p.curseRooms + ")";
                statusColor = ColorUtil.Hex("#C08CFF");
            }
            if (p.muramasa) status = (status != null ? status + "  ·  " : "") + "Muramasa";
            if (p.soulName != null && (p.soulReady || p.soulSeconds > 0))
            {
                string soul = p.soulSeconds > 0 ? string.Format(Strings.HudSoulActive, p.soulName, p.soulSeconds) : string.Format(Strings.HudSoulReady, p.soulName);
                status = (status != null ? status + "  ·  " : "") + soul;
                if (p.soulSeconds > 0) statusColor = ColorUtil.Hex("#B07CFF");
            }
            if (status != null) FitLabel(new Rect(x, 266f, w + 50f, 30f), status, smallStyle, statusColor);

            DrawWoundDoll(new Rect(card.xMax - 66f, 66f, 50f, 90f), p);
        }

        /// <summary>The duelist themself, in the pose of their guard, or swinging, dodging, drinking, down.</summary>
        void DrawPortrait(Rect r, HudData p)
        {
            CharacterArt art = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(p.art, 0, PlayerController.PartyArt.Length - 1)]);
            if (art == null) return;
            const int right = (int)ArtDir.Right;
            PlayerController.State s = p.state;
            Sprite body;
            bool attacking = s == PlayerController.State.Attack || s == PlayerController.State.SwapStrike || s == PlayerController.State.Finisher;
            if (s == PlayerController.State.Dead) body = art.dead ?? art.idle[right];
            else if (attacking) body = art.attack[right];
            else if (s == PlayerController.State.Dodge || p.moving)
            {
                Sprite[] walk = art.walk[right];
                body = walk[Mathf.FloorToInt(Time.unscaledTime * (s == PlayerController.State.Dodge ? 14f : 8f)) % walk.Length];
            }
            else body = art.idle[right];

            // Scale so the whole figure (head to feet) fills three quarters of the medallion.
            float px = 0.62f * r.height / Mathf.Max(6f, art.visibleHeight * PixelArt.PixelsPerUnit);
            float size = 16f * px;
            float feet = body.pivot.y * px;
            var cell = new Rect(r.center.x - size * 0.5f, r.yMax - 4f - size + feet, size, size);
            float bob = s == PlayerController.State.Free ? Mathf.Sin(Time.unscaledTime * 2.4f) * 3f : 0f;
            cell.y += bob;
            // Ground shadow.
            kit.Dab(new Rect(cell.x + size * 0.2f, cell.yMax - size * 0.1f, size * 0.6f, size * 0.12f), new Color(0f, 0f, 0f, 0.55f));

            // The guard's blade (swinging forward while attacking).
            int k = (int)p.stance;
            float angle = attacking ? Mathf.Lerp(-80f, 40f, Mathf.PingPong(Time.unscaledTime * 6f, 1f)) : KamaeAngle[k];
            if (s == PlayerController.State.Parry || s == PlayerController.State.Swap) angle = -60f;
            Vector2 hands = attacking ? new Vector2(0.7f, 0.5f) : KamaeHands[k];
            var grip = new Vector2(cell.x + hands.x * size, cell.y + hands.y * size);
            bool behind = k == (int)Kamae.Waki && !attacking;
            bool drawBlade = s != PlayerController.State.Dead && s != PlayerController.State.Drink;
            if (drawBlade && behind) DrawHeldWeapon(p.weapon, grip, angle, size / 16f);

            Color tint = Color.white;
            if (s == PlayerController.State.Hurt) tint = Color.Lerp(Color.white, Pal.danger, 0.6f);
            if (p.asura) tint = Color.Lerp(tint, Pal.shu, 0.45f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f));
            if (p.mushin || s == PlayerController.State.Parry)
            {
                Sprite white = s == PlayerController.State.Parry ? art.idleWhite[right] : art.idleWhite[right];
                float g = 1.08f;
                DrawSpriteTinted(new Rect(cell.center.x - size * g * 0.5f, cell.center.y - size * g * 0.5f, size * g, size * g), white,
                                 (s == PlayerController.State.Parry ? Pal.playerAccent : Pal.gold).WithAlpha(0.5f));
            }
            DrawSpriteTinted(cell, body, tint);
            if (drawBlade && !behind) DrawHeldWeapon(p.weapon, grip, angle, size / 16f);
            if (s == PlayerController.State.Drink)
            {
                Sprite gourd = HudArt.Gourd(true);
                DrawSpriteTinted(new Rect(grip.x - 10f, cell.y + size * 0.2f, 34f, 48f), gourd, Color.white);
            }
        }

        /// <summary>A pixel weapon held at the grip, pointing at a GUI angle (clockwise from right).</summary>
        void DrawHeldWeapon(WeaponDef w, Vector2 grip, float angle, float pixel)
        {
            Sprite s = PlayerController.WeaponSprite(w);
            if (s == null || s.texture.width > 64) return;
            Rect tr = s.textureRect;
            float gripX = s.pivot.x;
            float len = tr.width * pixel, thick = tr.height * pixel;
            var rect = new Rect(grip.x - gripX * pixel, grip.y - thick * 0.5f, len, thick);
            UiKit.Rotated(grip, angle, () => DrawSpriteTinted(rect, s, Color.white, false));
        }

        void Icon(Rect r, Sprite s, Color c) => DrawSpriteTinted(new Rect(r.center.x - r.width * 0.5f, r.center.y - r.height * 0.5f, r.width, r.height), s, c);

        void DrawSpriteTinted(Rect r, Sprite s, Color c, bool fit = true)
        {
            if (s == null) return;
            Rect tr = s.textureRect;
            Texture tex = s.texture;
            Rect dst = r;
            if (fit)
            {
                float k = Mathf.Min(r.width / tr.width, r.height / tr.height);
                dst = new Rect(r.center.x - tr.width * k * 0.5f, r.center.y - tr.height * k * 0.5f, tr.width * k, tr.height * k);
            }
            Color old = GUI.color;
            GUI.color = UiKit.Faded(c);
            GUI.DrawTextureWithTexCoords(dst, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            GUI.color = old;
        }

        /// <summary>The four guards as small figures of the duelist; the one held is lit.</summary>
        void DrawStanceRow(Rect r, HudData p)
        {
            CharacterArt art = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(p.art, 0, PlayerController.PartyArt.Length - 1)]);
            if (art == null) return;
            float cw = r.width / 4f;
            bool pad = game.Controls.UsingGamepad;
            string[] keys = pad ? new[] { "↑", "→", "↓", "←" } : new[] { "1", "2", "3", "4" };
            for (int i = 0; i < 4; i++)
            {
                bool on = (int)p.stance == i;
                Color c = on ? Pal.gold : UiKit.PaperDim.WithAlpha(0.4f);
                var cell = new Rect(r.x + i * cw + (cw - 44f) * 0.5f, r.y, 44f, 44f);
                if (on) kit.Dab(new Rect(cell.x - 6f, cell.y - 4f, 56f, 52f), Pal.gold.WithAlpha(0.18f));
                var grip = new Vector2(cell.x + KamaeHands[i].x * 44f, cell.y + KamaeHands[i].y * 44f);
                // A thin blade line at the guard's angle.
                if (i == (int)Kamae.Waki) DrawBladeLine(grip, KamaeAngle[i], 26f, c);
                DrawSpriteTinted(cell, art.idleWhite[(int)ArtDir.Right], c);
                if (i != (int)Kamae.Waki) DrawBladeLine(grip, KamaeAngle[i], 26f, c);
                Label(new Rect(cell.x - 10f, cell.yMax - 2f, 64f, 22f), keys[i], capsCenterStyle, c);
            }
        }

        void DrawBladeLine(Vector2 grip, float angle, float length, Color c)
        {
            UiKit.Rotated(grip, angle, () => DrawRect(new Rect(grip.x - 4f, grip.y - 1.5f, length, 3f), c));
        }

        /// <summary>A paper doll: the regions lit red are wounded (the eyes, when blinded).</summary>
        void DrawWoundDoll(Rect r, HudData p)
        {
            Color ok = UiKit.PaperDim.WithAlpha(0.45f);
            Color hurt = Color.Lerp(Pal.danger, Color.white, 0.15f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
            DrawSpriteTinted(r, HudArt.DollPart('H'), ok);
            DrawSpriteTinted(r, HudArt.DollPart('T'), p.HasWound(WoundRegion.Torso) ? hurt : ok);
            DrawSpriteTinted(r, HudArt.DollPart('A'), p.HasWound(WoundRegion.Arm) ? hurt : ok);
            DrawSpriteTinted(r, HudArt.DollPart('L'), p.HasWound(WoundRegion.Leg) ? hurt : ok);
            DrawSpriteTinted(r, HudArt.DollPart('E'), p.HasWound(WoundRegion.Eyes) ? hurt : new Color(0.1f, 0.1f, 0.12f, 0.9f));
            if (p.woundMask != 0) Label(new Rect(r.x - 30f, r.yMax + 2f, r.width + 60f, 22f), Strings.HudWounded, capsCenterStyle, Pal.danger);
        }

        // ── The belt ─────────────────────────────────────────────────────────────────

        void DrawBelt(HudData p)
        {
            if (!p.hasBuild) return;
            bool pad = game.Controls.UsingGamepad;
            var belt = new Rect(8f, VH - 150f, 900f, 142f);
            kit.InkPanel(belt, 0.86f);
            float x = belt.x + 34f, y = belt.y + 18f;

            // Two blades: the one in hand lit, the other with its swap key.
            for (int i = 0; i < 2; i++)
            {
                WeaponDef w = p.belt[i] ?? WeaponDefs.Katana;
                bool active = i == p.activeSlot;
                var slot = new Rect(x, y, 118f, 106f);
                Slot(slot, active ? Pal.gold : UiKit.PaperDim.WithAlpha(0.35f), active);
                DrawWeaponArt(w, new Rect(slot.x + 12f, slot.y + 10f, slot.width - 24f, 54f), active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                FitLabel(new Rect(slot.x + 4f, slot.y + 66f, slot.width - 8f, 24f), w.HudName, smallStyleCenter, active ? UiKit.PaperText : UiKit.PaperDim);
                if (active && p.charge > 0)
                {
                    for (int c = 0; c < 3; c++) kit.Dab(new Rect(slot.x + 34f + c * 18f, slot.yMax - 16f, 12f, 12f), (c < p.charge ? w.trail : Color.black).WithAlpha(c < p.charge ? 1f : 0.5f));
                }
                if (!active) Key(new Rect(slot.xMax - 30f, slot.y - 8f, 34f, 26f), game.Controls.Glyph(ControlAction.Swap));
                x += slot.width + 12f;
            }
            x += 14f;

            // The gourd: one hyōtan per sip.
            int sips = p.sips, max = p.maxSips;
            hudMarks[HudPart.Weapons] = new Rect(belt.x + 26f, y - 12f, x - belt.x - 34f, 122f);
            var gourds = new Rect(x, y, 30f + max * 40f, 106f);
            hudMarks[HudPart.Gourd] = new Rect(gourds.x - 6f, gourds.y - 12f, gourds.width + 12f, gourds.height + 18f);
            Slot(gourds, Pal.blessing.WithAlpha(0.4f), false);
            for (int i = 0; i < max; i++)
            {
                bool full = i < sips;
                float wob = full && p.state == PlayerController.State.Drink && i == sips - 1 ? Mathf.Sin(Time.unscaledTime * 20f) * 3f : 0f;
                DrawSpriteTinted(new Rect(gourds.x + 15f + i * 40f, gourds.y + 12f + wob, 38f, 56f), HudArt.Gourd(full), Color.white);
            }
            Label(new Rect(gourds.x, gourds.y + 72f, gourds.width, 26f), Strings.HudGourd, capsCenterStyle, sips > 0 ? Pal.blessing : UiKit.PaperDim);
            Key(new Rect(gourds.xMax - 30f, gourds.y - 8f, 34f, 26f), game.Controls.Glyph(ControlAction.Gourd));
            x = gourds.xMax + 12f;

            // Bandages.
            var band = new Rect(x, y, 96f, 106f);
            hudMarks[HudPart.Bandage] = new Rect(band.x - 6f, band.y - 12f, band.width + 12f, band.height + 18f);
            Slot(band, UiKit.PaperDim.WithAlpha(0.35f), false);
            DrawSpriteTinted(new Rect(band.x + 22f, band.y + 14f, 52f, 44f), HudArt.Bandage(), p.bandages > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f));
            Label(new Rect(band.x, band.y + 62f, band.width, 30f), "× " + p.bandages, cardKindStyleSmall, p.bandages > 0 ? UiKit.PaperText : UiKit.PaperDim);
            Key(new Rect(band.xMax - 30f, band.y - 8f, 34f, 26f), game.Controls.Glyph(ControlAction.Bandage));
            x = band.xMax + 12f;

            // The tool and its spirit papers.
            var tool = new Rect(x, y, 196f, 106f);
            hudMarks[HudPart.Tool] = new Rect(tool.x - 6f, tool.y - 12f, tool.width + 12f, tool.height + 18f);
            Slot(tool, Pal.gold.WithAlpha(0.35f), false);
            DrawSpriteTinted(new Rect(tool.x + 14f, tool.y + 12f, 56f, 56f), HudArt.Tool(p.tool), Color.white);
            FitLabel(new Rect(tool.x + 6f, tool.y + 72f, 110f, 26f), PlayerController.ToolNames[(int)p.tool], smallStyleCenter, UiKit.PaperText);
            DrawSpriteTinted(new Rect(tool.x + 122f, tool.y + 14f, 30f, 52f), HudArt.Ofuda(), p.papers > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f));
            Label(new Rect(tool.x + 112f, tool.y + 66f, 80f, 30f), "× " + p.papers, cardKindStyleSmall, p.papers > 0 ? UiKit.PaperText : Pal.danger);
            Key(new Rect(tool.xMax - 66f, tool.y - 8f, 34f, 26f), game.Controls.Glyph(ControlAction.ToolCycle));
            Key(new Rect(tool.xMax - 30f, tool.y - 8f, 34f, 26f), game.Controls.Glyph(ControlAction.Tool));
            x = tool.xMax + 18f;

            // The purse.
            hudMarks[HudPart.Mon] = new Rect(x - 28f, y - 10f, 96f, 116f);
            DrawSpriteTinted(new Rect(x, y + 20f, 40f, 40f), HudArt.Coin(), Color.white);
            Label(new Rect(x - 20f, y + 64f, 80f, 30f), p.mon.ToString(), cardKindStyleSmall, Pal.gold);
            Label(new Rect(x - 20f, y - 6f, 80f, 24f), "MON", capsCenterStyle, UiKit.PaperDim);
        }

        void Slot(Rect r, Color edge, bool lit)
        {
            DrawRect(r, new Color(0f, 0f, 0f, lit ? 0.55f : 0.4f));
            DrawRect(new Rect(r.x, r.y, r.width, 2f), edge);
            DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), edge);
            DrawRect(new Rect(r.x, r.y, 2f, r.height), edge);
            DrawRect(new Rect(r.xMax - 2f, r.y, 2f, r.height), edge);
            if (lit) DrawRect(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Pal.gold.WithAlpha(0.06f));
        }

        void Key(Rect r, string key)
        {
            DrawRect(r, UiKit.PaperText.WithAlpha(0.92f));
            DrawRect(new Rect(r.x, r.yMax - 3f, r.width, 3f), UiKit.InkSoft);
            Label(r, key, capsCenterStyle, UiKit.Ink);
        }
    }
}
