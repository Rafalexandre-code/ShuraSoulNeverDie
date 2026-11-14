using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The PvP duel on screen (§9.6), drawn only from the rollback state: the ensō of the arena,
    /// the two duelists with their blades, the closing ring of every blow (cyan to parry, red to
    /// dodge), both bars, the rounds, and the verdict with the ladder's change.
    /// </summary>
    public sealed partial class GameUI
    {
        const float DuelPx = 100f;
        readonly DuelAct[] duelPrevAct = new DuelAct[2];
        readonly int[] duelPrevEv = new int[2];

        Vector2 DuelCenter => new Vector2(VW * 0.5f, 600f);
        Vector2 DuelToCanvas(int x, int y) => new Vector2(DuelCenter.x + x / 1000f * DuelPx, DuelCenter.y - y / 1000f * DuelPx);

        void DrawDuel()
        {
            DuelSession d = game.Duel;
            if (d == null) return;
            DuelState s = d.Net.State;
            DrawCourtGround(false);
            DrawRect(FullScreen, Color.black.WithAlpha(0.45f));
            float ar = DuelSim.ArenaRadius / 1000f * DuelPx;
            Vector2 c = DuelCenter;
            DrawRect(new Rect(c.x - ar, c.y - ar, ar * 2f, ar * 2f), Color.clear);
            kit.Dab(new Rect(c.x - ar, c.y - ar * 0.98f, ar * 2f, ar * 1.96f), ColorUtil.Hex("#2A2320").WithAlpha(0.75f));
            kit.Enso(new Rect(c.x - ar - 14f, c.y - ar - 14f, ar * 2f + 28f, ar * 2f + 28f), UiKit.Shu.WithAlpha(0.85f), 30f);

            // The one further up is drawn first.
            bool aFirst = s.a.y >= s.b.y;
            DrawDuelist(aFirst ? s.a : s.b, aFirst ? d.A : d.B, aFirst ? s.b : s.a, aFirst ? d.B : d.A, s.frame, aFirst ? 0 : 1, d.Start.me);
            DrawDuelist(aFirst ? s.b : s.a, aFirst ? d.B : d.A, aFirst ? s.a : s.b, aFirst ? d.A : d.B, s.frame, aFirst ? 1 : 0, d.Start.me);
            DuelSounds(s.a, 0);
            DuelSounds(s.b, 1);

            DrawDuelBars(d, s);
            DrawDuelBanner(d, s);
            if (d.Outcome != DuelOutcome.None) DrawDuelVerdict(d);

            InputService ic = game.Controls;
            string hint = string.Format(Strings.DuelHint, ic.Glyph(ControlAction.Attack), ic.Glyph(ControlAction.Swap), ic.Glyph(ControlAction.Parry), ic.Glyph(ControlAction.Dodge));
            kit.InkPanel(new Rect(VW * 0.5f - 760f, 1020f, 1520f, 48f), 0.75f);
            FitLabel(new Rect(VW * 0.5f - 740f, 1028f, 1480f, 32f), hint, capsCenterStyle, UiKit.PaperText);
            Label(new Rect(VW - 520f, 980f, 500f, 28f),
                  string.Format(Strings.DuelNetLine, DuelRollback.Delay, d.Net.Rollbacks, (d.Net.Advantage - d.Net.RemoteAdvantage) / 2), capsRightStyle, UiKit.PaperDim.WithAlpha(0.6f));
        }

        void DrawDuelist(DuelFighter f, DuelSide side, DuelFighter o, DuelSide oside, int now, int index, int me)
        {
            Vector2 at = DuelToCanvas(f.x, f.y);
            float body = DuelSim.BodyRadius / 1000f * DuelPx;
            kit.Dab(new Rect(at.x - body * 1.1f, at.y - body * 0.35f, body * 2.2f, body * 0.7f), Color.black.WithAlpha(0.45f));

            // The closing ring over the one winding up: cyan to parry, red to dodge (Waki shows it late).
            if (f.act == DuelAct.Windup)
            {
                DuelSim.Blow(f, side, out DuelBlow b);
                float t = Mathf.Clamp01(f.frame / (float)Mathf.Max(1, b.windup));
                bool hidden = side.kamae == 3 && t < 0.4f;
                if (!hidden)
                {
                    float rr = Mathf.Lerp(2.6f, 1f, t) * body * 1.6f;
                    Color rc = f.red ? Pal.danger : new Color(0.45f, 0.9f, 1f);
                    kit.Enso(new Rect(at.x - rr, at.y - rr - body, rr * 2f, rr * 2f), rc.WithAlpha(0.5f + 0.5f * t), now * 4f);
                }
            }

            CharacterArt a = PixelArt.Character(PlayerController.PartyArt[Mathf.Clamp(side.art, 0, PlayerController.PartyArt.Length - 1)]);
            if (a != null)
            {
                ArtDir dir = Mathf.Abs(f.fx) > Mathf.Abs(f.fy) ? (f.fx > 0 ? ArtDir.Right : ArtDir.Left) : (f.fy > 0 ? ArtDir.Up : ArtDir.Down);
                bool striking = f.act == DuelAct.Windup || f.act == DuelAct.Active;
                Sprite sp = striking ? a.attack[(int)dir] : f.act == DuelAct.Free && (now / 8) % 2 == 0 ? a.idle[(int)dir] : a.walk[(int)dir][(now / 6) % a.walk[(int)dir].Length];
                Color tint = f.act == DuelAct.Dodge ? new Color(1f, 1f, 1f, 0.45f)
                           : f.act == DuelAct.Broken ? new Color(0.6f, 0.6f, 0.65f)
                           : f.act == DuelAct.Hurt ? new Color(1f, 0.55f, 0.55f) : Color.white;
                float h = 1.15f * DuelPx;
                float shake = f.act == DuelAct.Stagger ? Mathf.Sin(now * 1.7f) * 4f : 0f;
                DrawSpriteTinted(new Rect(at.x - h * 0.5f + shake, at.y - h * 0.92f, h, h), sp, tint);
            }
            DrawDuelBlade(f, side, at, now);
            if (f.act == DuelAct.Broken) Label(new Rect(at.x - 60f, at.y - 2.1f * DuelPx, 120f, 80f), "破", popupStyle, Pal.danger);
            if (f.guarding) kit.Enso(new Rect(at.x - body * 1.4f, at.y - body * 2.6f, body * 2.8f, body * 2.8f), new Color(0.45f, 0.9f, 1f, 0.5f), 0f);
            Label(new Rect(at.x - 200f, at.y + 8f, 400f, 26f), side.name, capsCenterStyle, index == me ? UiKit.ShuBright : UiKit.PaperText);

            // What just happened, for a moment.
            int age = now - f.evFrame;
            if (f.ev != DuelEvent.None && age >= 0 && age < 40)
            {
                string text = Strings.DuelEventName(f.ev);
                if (text.Length > 0)
                {
                    Color ec = f.ev == DuelEvent.Parried || f.ev == DuelEvent.Dodged ? Pal.gold : f.ev == DuelEvent.Blocked ? UiKit.PaperText : Pal.danger;
                    FitLabel(new Rect(at.x - 260f, at.y - 2.2f * DuelPx - age * 1.2f, 520f, 50f), text, popupStyle, ec.WithAlpha(1f - age / 40f));
                }
            }
        }

        void DrawDuelBlade(DuelFighter f, DuelSide side, Vector2 at, int now)
        {
            WeaponDef w = WeaponDefs.Get(side.weaponId);
            Sprite sp = PixelArt.Weapon(w.art);
            if (sp == null) return;
            float baseAngle = Mathf.Atan2(-(f.act == DuelAct.Windup || f.act == DuelAct.Active ? f.ay : f.fy), f.act == DuelAct.Windup || f.act == DuelAct.Active ? f.ax : f.fx) * Mathf.Rad2Deg;
            float swing = 0f;
            if (f.act == DuelAct.Windup) swing = -70f;
            else if (f.act == DuelAct.Active)
            {
                DuelSim.Blow(f, side, out DuelBlow b);
                swing = Mathf.Lerp(-70f, 70f, f.frame / (float)Mathf.Max(1, b.active));
            }
            else if (f.act == DuelAct.Recovery) swing = 70f;
            else if (f.guarding) swing = -95f;
            float len = Mathf.Max(0.8f, w.length) * DuelPx * 0.9f;
            Vector2 grip = at + new Vector2(0f, -0.45f * DuelPx);
            Matrix4x4 old = GUI.matrix;
            // Rotate in canvas space, then let the canvas's own scale apply (RotateAroundPivot would turn about the wrong point).
            GUI.matrix = old * Matrix4x4.TRS(grip, Quaternion.Euler(0f, 0f, baseAngle + swing), Vector3.one) * Matrix4x4.TRS(-grip, Quaternion.identity, Vector3.one);
            Rect tr = sp.textureRect;
            float hgt = len * tr.height / Mathf.Max(1f, tr.width);
            Color oldc = GUI.color;
            GUI.color = UiKit.Faded(f.red && f.act == DuelAct.Windup ? Color.Lerp(Color.white, Pal.danger, 0.5f) : Color.white);
            GUI.DrawTextureWithTexCoords(new Rect(grip.x, grip.y - hgt * 0.5f, len, hgt), sp.texture,
                new Rect(tr.x / sp.texture.width, tr.y / sp.texture.height, tr.width / sp.texture.width, tr.height / sp.texture.height));
            GUI.color = oldc;
            GUI.matrix = old;
        }

        void DuelSounds(DuelFighter f, int i)
        {
            if (f.act == DuelAct.Active && duelPrevAct[i] == DuelAct.Windup) game.Audio.Play(Sfx.Swing, 0.7f, f.red ? 0.75f : 1f);
            if (f.act == DuelAct.Dodge && duelPrevAct[i] != DuelAct.Dodge) game.Audio.Play(Sfx.Dodge, 0.6f);
            duelPrevAct[i] = f.act;
            if (f.evFrame == duelPrevEv[i] || f.ev == DuelEvent.None) return;
            duelPrevEv[i] = f.evFrame;
            switch (f.ev)
            {
                case DuelEvent.Hit: game.Audio.Play(Sfx.Hit, 0.9f); break;
                case DuelEvent.Blocked: game.Audio.Play(Sfx.Block, 0.8f); break;
                case DuelEvent.Parried: game.Audio.Play(Sfx.PerfectParry, 1f); break;
                case DuelEvent.Broken: game.Audio.Play(Sfx.PostureBreak, 1f); break;
                case DuelEvent.Finished: game.Audio.Play(Sfx.Finisher, 1f); break;
            }
        }

        void DrawDuelBars(DuelSession d, DuelState s)
        {
            DrawDuelSide(new Rect(40f, 24f, 640f, 130f), s.a, d.A, d.Start.me == 0, false);
            DrawDuelSide(new Rect(VW - 680f, 24f, 640f, 130f), s.b, d.B, d.Start.me == 1, true);
            int secs = Mathf.CeilToInt(s.roundTimer / 60f);
            kit.InkPanel(new Rect(VW * 0.5f - 110f, 20f, 220f, 110f), 0.85f);
            Label(new Rect(VW * 0.5f - 110f, 22f, 220f, 60f), secs.ToString(), cardKindStyle, UiKit.PaperText);
            Label(new Rect(VW * 0.5f - 110f, 82f, 220f, 30f), string.Format(Strings.DuelRound, s.round), capsCenterStyle, UiKit.PaperDim);
        }

        void DrawDuelSide(Rect r, DuelFighter f, DuelSide side, bool you, bool right)
        {
            kit.InkPanel(r, 0.85f);
            WeaponDef w = WeaponDefs.Get(side.weaponId);
            string title = side.name + (you ? Strings.DuelYou : "");
            Label(new Rect(r.x + 24f, r.y + 10f, r.width - 48f, 34f), title, right ? capsRightStyle : capsStyle, you ? UiKit.ShuBright : UiKit.PaperText);
            Label(new Rect(r.x + 24f, r.y + 40f, r.width - 48f, 26f), w.name + "  ·  " + Strings.KamaeName(side.kamae), right ? capsRightStyle : capsStyle, UiKit.PaperDim);
            kit.Bar(new Rect(r.x + 24f, r.y + 72f, r.width - 48f, 20f), Mathf.Clamp01(f.health / (float)DuelSim.Health), Pal.danger, Color.black.WithAlpha(0.6f));
            kit.Bar(new Rect(r.x + 24f, r.y + 98f, r.width - 48f, 10f), Mathf.Clamp01(f.posture / (float)DuelSim.PostureMax), Pal.gold, Color.black.WithAlpha(0.5f));
            for (int i = 0; i < DuelSim.RoundsToWin; i++)
            {
                float x = right ? r.x + 24f + i * 34f : r.xMax - 54f - i * 34f;
                kit.Dab(new Rect(x, r.y + 12f, 26f, 26f), i < f.rounds ? Pal.gold : Color.white.WithAlpha(0.15f));
            }
        }

        void DrawDuelBanner(DuelSession d, DuelState s)
        {
            if (s.pause <= 0 || d.Outcome != DuelOutcome.None) return;
            string text;
            if (s.lastRound < 0 || s.lastRound == 3) text = string.Format(Strings.DuelRound, s.round).ToUpperInvariant() + "  ·  " + Strings.DuelBegin;
            else if (s.lastRound == 2) text = Strings.DuelRoundDraw;
            else text = string.Format(Strings.DuelRoundTo, s.lastRound == 0 ? d.A.name : d.B.name);
            kit.InkPanel(new Rect(VW * 0.5f - 520f, 300f, 1040f, 110f), 0.85f);
            FitLabel(new Rect(VW * 0.5f - 500f, 310f, 1000f, 90f), text, cardKindStyle, UiKit.PaperText);
        }

        void DrawDuelVerdict(DuelSession d)
        {
            var r = new Rect(VW * 0.5f - 560f, 330f, 1120f, 330f);
            kit.PaperPanel(r);
            string title = d.Outcome == DuelOutcome.Won ? Strings.DuelWon : d.Outcome == DuelOutcome.Lost ? Strings.DuelLost
                         : d.Outcome == DuelOutcome.Draw ? Strings.DuelDraw : d.Outcome == DuelOutcome.Desync ? Strings.DuelDesync : Strings.DuelGone;
            Heading(new Rect(r.x, r.y + 26f, r.width, 90f), title, d.Outcome == DuelOutcome.Won ? UiKit.Shu : UiKit.Ink, UiKit.Shu.WithAlpha(0.8f), 380f);
            DuelResult res = game.LastDuel;
            string line;
            if (res != null)
            {
                PvpRecord rec = game.Meta.pvp;
                line = string.Format(Strings.DuelLadder, rec.rating, (res.change >= 0 ? "+" : "") + res.change, Pvp.RankTitle(rec.rating),
                                     Pvp.SeasonName(rec.season), rec.wins, rec.losses, rec.draws);
            }
            else line = d.Outcome == DuelOutcome.Desync ? Strings.DuelDesyncNote : Strings.DuelGoneNote;
            FitLabel(new Rect(r.x + 60f, r.y + 150f, r.width - 120f, 40f), line, bodyCenterStyle, UiKit.Ink);
            FitLabel(new Rect(r.x + 60f, r.y + 200f, r.width - 120f, 30f),
                     string.Format(Strings.DuelNetSummary, d.Net.Frame, d.Net.Rollbacks, d.Net.RolledBackFrames), capsCenterStyle, UiKit.InkSoft);
            if (d.SinceEnd > 2f) Label(new Rect(r.x, r.yMax - 64f, r.width, 30f), Strings.DuelBack, capsCenterStyle, UiKit.InkSoft);
        }
    }
}
