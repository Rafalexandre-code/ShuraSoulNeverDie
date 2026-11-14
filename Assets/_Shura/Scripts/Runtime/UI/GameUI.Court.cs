using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The Temple courtyard as the online lobby (§9.7): drawn in ink over the dojo's stone, the
    /// same for the host and the guests. Esc (or Start) opens the room's menu over it.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>The room's menu is open over the courtyard (else the duelist walks).</summary>
        public bool CourtMenuOpen { get; set; }

        struct CourtFigure
        {
            public int id;
            public string name;
            public int art;
            public Vector2 pos, facing;
            public bool moving, sitting, challenging, dueling, ready, you;
            public int swing, guard, kamae;
        }

        readonly List<CourtFigure> figures = new List<CourtFigure>();
        Texture2D courtFloor;
        Rect courtFloorWorld;

        // ── Input ────────────────────────────────────────────────────────────────────

        void HandleCourt(InputService input)
        {
            bool tab = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame;
            if (input.PausePressed || input.Cancel || tab)
            {
                CourtMenuOpen = true;
                game.Audio.Play(Sfx.UiConfirm, 0.5f, 1f, 0f);
            }
        }

        // ── Drawing ──────────────────────────────────────────────────────────────────

        float CourtScale => Mathf.Min((VW - 140f) / (Courtyard.HalfW * 2f), 860f / (Courtyard.HalfH * 2f));
        Vector2 CourtCenter => new Vector2(VW * 0.5f, 590f);

        Vector2 CourtToCanvas(Vector2 p) => new Vector2(CourtCenter.x + p.x * CourtScale, CourtCenter.y - p.y * CourtScale);

        void EnsureCourtFloor()
        {
            if (courtFloor != null) return;
            var gen = new GameObject("CourtFloor");
            try
            {
                var layout = new ArenaLayout(18, 10, ArenaShape.Hall);
                ArenaDecor d = ArenaBuilder.Build(gen.transform, game.Config, layout, Biomes.All[0], false, true);
                courtFloor = d.FloorTexture;
                courtFloorWorld = new Rect(layout.Origin.x, layout.Origin.y, layout.W, layout.H);
            }
            finally
            {
                Object.Destroy(gen);
            }
        }

        void DrawCourtGround(bool spots = true)
        {
            DrawRect(FullScreen, ColorUtil.Hex("#141012"));
            EnsureCourtFloor();
            if (courtFloor != null)
            {
                Vector2 a = CourtToCanvas(new Vector2(courtFloorWorld.xMin, courtFloorWorld.yMax));
                Vector2 b = CourtToCanvas(new Vector2(courtFloorWorld.xMax, courtFloorWorld.yMin));
                Color old = GUI.color;
                GUI.color = UiKit.Faded(new Color(0.78f, 0.74f, 0.7f));
                GUI.DrawTexture(new Rect(a.x, a.y, b.x - a.x, b.y - a.y), courtFloor);
                GUI.color = old;
            }
            kit.InkVignette(FullScreen, Color.black.WithAlpha(0.55f));
            if (!spots) return;
            float s = CourtScale;

            // The duel circle: a great ensō and the kanji for a decisive duel.
            Vector2 c = CourtToCanvas(Courtyard.DuelCircle);
            float r = Courtyard.CircleRadius * s;
            kit.Enso(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), UiKit.Shu.WithAlpha(0.8f), 20f);
            Label(new Rect(c.x - 60f, c.y - 50f, 120f, 100f), "決", popupStyle, UiKit.Shu.WithAlpha(0.35f));
            Label(new Rect(c.x - 200f, c.y + r - 8f, 400f, 30f), Strings.CourtCircle, capsCenterStyle, UiKit.PaperDim);

            // The torii: two posts, two beams, in vermilion.
            Vector2 t = CourtToCanvas(Courtyard.Torii);
            Color ver = ColorUtil.Hex("#C8402B");
            DrawRect(new Rect(t.x - 0.9f * s, t.y - 1.7f * s, 1.8f * s, 0.22f * s), ver);
            DrawRect(new Rect(t.x - 0.75f * s, t.y - 1.35f * s, 1.5f * s, 0.14f * s), ver);
            DrawRect(new Rect(t.x - 0.62f * s, t.y - 1.6f * s, 0.16f * s, 1.9f * s), ver);
            DrawRect(new Rect(t.x + 0.46f * s, t.y - 1.6f * s, 0.16f * s, 1.9f * s), ver);
            Label(new Rect(t.x - 200f, t.y + 0.5f * s, 400f, 30f), Strings.CourtTorii, capsCenterStyle, UiKit.PaperDim);

            // The tea house: a tatami and a kettle's steam.
            Vector2 h = CourtToCanvas(Courtyard.TeaHouse);
            DrawRect(new Rect(h.x - 1.1f * s, h.y - 0.6f * s, 2.2f * s, 1.2f * s), ColorUtil.Hex("#7C8A4E").WithAlpha(0.85f));
            DrawRect(new Rect(h.x - 1.1f * s, h.y - 0.6f * s, 2.2f * s, 0.08f * s), ColorUtil.Hex("#3B2A1A"));
            DrawRect(new Rect(h.x - 1.1f * s, h.y + 0.52f * s, 2.2f * s, 0.08f * s), ColorUtil.Hex("#3B2A1A"));
            for (int i = 0; i < 3; i++)
            {
                float k = Mathf.Repeat(Time.unscaledTime * 0.4f + i / 3f, 1f);
                kit.Dab(new Rect(h.x - 8f + Mathf.Sin(k * 6f + i) * 10f, h.y - 30f - k * 60f, 22f, 22f), Color.white.WithAlpha(0.25f * (1f - k)));
            }
            Label(new Rect(h.x - 200f, h.y + 0.65f * s, 400f, 30f), Strings.CourtTea, capsCenterStyle, UiKit.PaperDim);

            // The oath stone.
            Vector2 o = CourtToCanvas(Courtyard.OathStone);
            DrawRect(new Rect(o.x - 0.45f * s, o.y - 0.9f * s, 0.9f * s, 1.2f * s), ColorUtil.Hex("#4A4744"));
            DrawRect(new Rect(o.x - 0.45f * s, o.y - 0.9f * s, 0.9f * s, 0.1f * s), ColorUtil.Hex("#6A6560"));
            Label(new Rect(o.x - 60f, o.y - 0.75f * s, 120f, 0.9f * s), "誓", popupStyle, UiKit.PaperText.WithAlpha(0.8f));
            Label(new Rect(o.x - 200f, o.y + 0.38f * s, 400f, 30f), Strings.CourtStone, capsCenterStyle, UiKit.PaperDim);
        }

        void DrawCourtFigure(CourtFigure f)
        {
            float s = CourtScale;
            Vector2 at = CourtToCanvas(f.pos + (f.sitting ? Vector2.down * 0.15f : Vector2.zero));
            CharacterArt a = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(f.art, 0, PlayerController.PartyArt.Length - 1)]);
            if (f.challenging)
            {
                float rr = 0.75f * s;
                kit.Enso(new Rect(at.x - rr, at.y - rr * 0.5f, rr * 2f, rr), UiKit.ShuBright.WithAlpha(0.9f), Time.unscaledTime * 60f);
            }
            kit.Dab(new Rect(at.x - 0.42f * s, at.y - 0.1f * s, 0.84f * s, 0.26f * s), Color.black.WithAlpha(0.35f));
            if (a != null)
            {
                ArtDir dir = Mathf.Abs(f.facing.x) > Mathf.Abs(f.facing.y) ? (f.facing.x > 0 ? ArtDir.Right : ArtDir.Left) : (f.facing.y > 0 ? ArtDir.Up : ArtDir.Down);
                Sprite[] walk = a.walk[(int)dir];
                Sprite sp = f.swing > 0 ? a.attack[(int)dir] : f.moving ? walk[Mathf.FloorToInt(Time.unscaledTime * 8f + f.id) % walk.Length] : a.idle[(int)dir];
                float h = 1.35f * s;
                Color tint = f.dueling ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                DrawSpriteTinted(new Rect(at.x - h * 0.5f, at.y - h * 0.95f, h, h), sp, tint);
                if (f.guard > 0) kit.Enso(new Rect(at.x - 0.55f * s, at.y - 1.05f * s, 1.1f * s, 1.1f * s), UiKit.Ai.WithAlpha(0.7f), 0f);
            }
            Color nameColor = f.you ? UiKit.ShuBright : UiKit.PaperText;
            Label(new Rect(at.x - 160f, at.y - 1.55f * s, 320f, 28f), f.name, capsCenterStyle, nameColor);
            string tag = f.dueling ? Strings.CourtDueling : f.sitting ? Strings.CourtSitting : f.ready ? Strings.SlotReady : "";
            if (tag.Length > 0) Label(new Rect(at.x - 160f, at.y + 0.2f * s, 320f, 24f), tag, capsCenterStyle, f.dueling ? UiKit.ShuBright : UiKit.PaperDim);
        }

        void DrawCourtPopup(string text, Vector2 at, int color, float age)
        {
            Color c = color == 1 ? new Color(0.55f, 0.75f, 1f) : color == 2 ? UiKit.ShuBright : color == 3 ? Pal.gold : UiKit.PaperText;
            Vector2 p = CourtToCanvas(at) + Vector2.up * (-age * 40f);
            float alpha = Mathf.Clamp01(1.6f - age);
            FitLabel(new Rect(p.x - 300f, p.y - 30f, 600f, 44f), text, capsCenterStyle, c.WithAlpha(alpha));
        }

        /// <summary>The top band: where you are, the party line, your season, and how to move.</summary>
        void DrawCourtFrame(string partyLine, string you)
        {
            kit.InkPanel(new Rect(VW * 0.5f - 640f, 14f, 1280f, 112f), 0.82f);
            Label(new Rect(0, 18f, VW, 50f), Strings.CourtTitle, cardKindStyle, UiKit.PaperText);
            FitLabel(new Rect(VW * 0.5f - 600f, 66f, 1200f, 26f), partyLine, capsCenterStyle, UiKit.PaperDim);
            PvpRecord r = Pvp.Current(game.Meta, System.DateTime.UtcNow);
            FitLabel(new Rect(VW * 0.5f - 600f, 92f, 1200f, 26f),
                     string.Format(Strings.CourtSeason, Pvp.SeasonName(r.season), r.rating, Pvp.RankTitle(r.rating), r.wins, r.losses), capsCenterStyle, Pal.gold);
            InputService c = game.Controls;
            string hint = string.Format(Strings.CourtHint, c.Glyph(ControlAction.Attack), c.Glyph(ControlAction.Parry), c.Glyph(ControlAction.Interact));
            kit.InkPanel(new Rect(VW * 0.5f - 760f, 1020f, 1520f, 48f), 0.75f);
            FitLabel(new Rect(VW * 0.5f - 740f, 1028f, 1480f, 32f), hint, capsCenterStyle, UiKit.PaperText);
            if (!string.IsNullOrEmpty(you)) FitLabel(new Rect(VW * 0.5f - 740f, 986f, 1480f, 30f), you, capsCenterStyle, UiKit.PaperDim);
        }

        string NearSpotHint(Vector2 pos, bool challenging)
        {
            if (Courtyard.In(pos, Courtyard.DuelCircle, Courtyard.CircleRadius)) return challenging ? Strings.CourtAtCircleWaiting : Strings.CourtAtCircle;
            if (Courtyard.In(pos, Courtyard.TeaHouse, Courtyard.SpotRadius)) return Strings.CourtAtTea;
            if (Courtyard.In(pos, Courtyard.OathStone, Courtyard.SpotRadius)) return Strings.CourtAtStone;
            if (Courtyard.In(pos, Courtyard.Torii, Courtyard.SpotRadius)) return game.Remote.Joined ? Strings.CourtAtToriiGuest : Strings.CourtHostStarts;
            return "";
        }

        /// <summary>The host's courtyard, straight from the simulation.</summary>
        void DrawCourtHost()
        {
            NetSession net = game.Net;
            DrawCourtGround();
            figures.Clear();
            Vector2 myPos = Vector2.zero;
            bool myChallenge = false;
            foreach (Courtyard.Walker w in net.Court.Walkers)
            {
                bool ready = w.id == 0;
                foreach (NetSession.Remote r in net.Remotes)
                {
                    if (r.id == w.id) ready = r.ready;
                }
                if (w.id == 0)
                {
                    myPos = w.pos;
                    myChallenge = w.challenging;
                }
                figures.Add(new CourtFigure
                {
                    id = w.id, name = w.name, art = w.art, pos = w.pos, facing = w.facing, moving = w.moving, sitting = w.sitting,
                    challenging = w.challenging, dueling = w.dueling, ready = ready, you = w.id == 0, swing = w.swing, guard = w.guard, kamae = w.kamae,
                });
            }
            DrawFigures();
            foreach (Courtyard.Popup p in net.Court.Popups) DrawCourtPopup(p.text, p.at, p.color, p.age);
            DrawCourtFrame(net.LobbyPartyLine(), NearSpotHint(myPos, myChallenge));
        }

        /// <summary>A guest's courtyard, from the host's messages (positions eased between them).</summary>
        void DrawCourtGuest()
        {
            RemoteSession r = Remote;
            DrawCourtGround();
            figures.Clear();
            Vector2 myPos = Vector2.zero;
            bool myChallenge = false;
            float k = r.CourtBlend;
            foreach (object o in r.CourtWalkers)
            {
                if (!(o is List<object> w)) continue;
                int id = MiniJson.I(w, 0);
                var pos = new Vector2(MiniJson.F(w, 3), MiniJson.F(w, 4));
                if (r.CourtPrev != null)
                {
                    foreach (object po in r.CourtPrev)
                    {
                        if (po is List<object> pw && MiniJson.I(pw, 0) == id) pos = Vector2.Lerp(new Vector2(MiniJson.F(pw, 3), MiniJson.F(pw, 4)), pos, k);
                    }
                }
                bool me = id == r.MyId;
                if (me)
                {
                    myPos = pos;
                    myChallenge = MiniJson.B(w, 12);
                }
                figures.Add(new CourtFigure
                {
                    id = id, name = MiniJson.S(w, 1), art = MiniJson.I(w, 2), pos = pos, facing = new Vector2(MiniJson.F(w, 5), MiniJson.F(w, 6)),
                    moving = MiniJson.B(w, 7), swing = MiniJson.I(w, 8), guard = MiniJson.I(w, 9), sitting = MiniJson.B(w, 10), kamae = MiniJson.I(w, 11),
                    challenging = MiniJson.B(w, 12), dueling = MiniJson.B(w, 13), ready = MiniJson.B(w, 14), you = me,
                });
            }
            DrawFigures();
            if (r.CourtPopups != null)
            {
                foreach (object o in r.CourtPopups)
                {
                    if (o is List<object> p) DrawCourtPopup(MiniJson.S(p, 0), new Vector2(MiniJson.F(p, 1), MiniJson.F(p, 2)), MiniJson.I(p, 3), MiniJson.F(p, 4) + r.CourtAge);
                }
            }
            DrawCourtFrame(r.LobbyPartyLine(), NearSpotHint(myPos, myChallenge));
        }

        void DrawFigures()
        {
            // Back to front: the ones higher in the yard first.
            figures.Sort((a, b) => b.pos.y.CompareTo(a.pos.y));
            foreach (CourtFigure f in figures) DrawCourtFigure(f);
        }
    }
}
