using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Translates gameplay moments into hit-stop, shake, particles, sound and UI. All feel
    /// numbers come from FeelTuning, so impact can be tuned without touching combat code.
    /// </summary>
    public sealed class FeedbackRouter : IFeedback
    {
        readonly Game game;

        public FeedbackRouter(Game owner)
        {
            game = owner;
        }

        FeelTuning Feel => game.Config.feel;
        Palette Pal => game.Config.palette;

        public void Emit(FxType type, Vector2 pos, Vector2 dir, float magnitude = 1f, Color? color = null)
        {
            Vfx vfx = game.Vfx;
            AudioService audio = game.Audio;
            CameraRig cam = game.CameraRig;
            SimClock clock = game.Clock;
            GameUI ui = game.UI;
            Color col = color ?? Color.white;

            switch (type)
            {
                case FxType.PlayerSwing:
                    audio.Play(Sfx.Swing, 0.55f, 1.05f);
                    break;

                case FxType.EnemySwing:
                    audio.Play(Sfx.Swing, 0.45f, magnitude > 1f ? 0.75f : 0.88f);
                    break;

                case FxType.HitEnemy:
                    clock.AddHitstop(Feel.hitstopHit);
                    cam.Shake(Feel.shakeHit);
                    vfx.Sparks(pos, dir, 7, Color.white, 7f);
                    vfx.Sparks(pos, dir, 4, col, 5f);
                    vfx.Ring(pos, 0.1f, 0.6f, Color.white.WithAlpha(0.7f), 0.15f);
                    audio.Play(Sfx.Hit, 0.8f);
                    ui.DamageNumber(Mathf.RoundToInt(magnitude), pos);
                    break;

                case FxType.TimingCue:
                    if (!game.Settings.timingIndicator) break;
                    audio.Play(Sfx.Glint, magnitude > 0.5f ? 0.55f : 0.45f, magnitude > 0.5f ? 0.8f : 1f, 0f);
                    break;

                case FxType.Footstep:
                    vfx.Footstep(pos, dir);
                    break;

                case FxType.EnemyBlocked:
                    clock.AddHitstop(Feel.hitstopBlock);
                    cam.Shake(Feel.shakeBlock * 0.7f);
                    vfx.Sparks(pos, dir, 6, Pal.spark, 6f);
                    audio.Play(Sfx.Block, 0.7f, 1.1f);
                    ui.Popup(Strings.PopupBlocked, pos, Pal.uiDim);
                    break;

                case FxType.PlayerBlock:
                    clock.AddHitstop(Feel.hitstopBlock);
                    cam.Shake(Feel.shakeBlock);
                    vfx.Sparks(pos, dir, 8, Pal.spark, 6f);
                    audio.Play(Sfx.Block, 0.8f);
                    break;

                case FxType.PerfectParry:
                    clock.AddHitstop(Feel.hitstopPerfectParry);
                    cam.Shake(Feel.shakePerfectParry);
                    cam.Punch(Feel.zoomPunchPerfectParry);
                    vfx.Sparks(pos, dir, 18, Pal.spark, 11f, 70f);
                    vfx.Sparks(pos, -dir, 8, Color.white, 8f, 50f);
                    vfx.Ring(pos, 0.15f, 1.4f, Pal.playerAccent, 0.25f);
                    audio.Play(Sfx.PerfectParry, 1f);
                    ui.Popup(Strings.PopupPerfect, pos, Pal.playerAccent);
                    ui.Flash(Color.white, 0.12f);
                    break;

                case FxType.PerfectDodge:
                    vfx.Ring(pos, 0.3f, 1.2f, Pal.playerAccent.WithAlpha(0.7f), 0.3f);
                    audio.Play(Sfx.Chime, 0.6f, 1.2f);
                    ui.Popup(Strings.PopupDodge, pos, Pal.playerAccent);
                    break;

                case FxType.CounterThrust:
                    clock.AddHitstop(Feel.hitstopCounter);
                    cam.Shake(Feel.shakeCounter);
                    cam.Punch(Feel.zoomPunchPerfectParry * 1.4f);
                    vfx.Sparks(pos, dir, 22, Pal.spark, 12f, 40f);
                    vfx.Ring(pos, 0.2f, 1.8f, Pal.playerAccent, 0.3f);
                    audio.Play(Sfx.PerfectParry, 1f, 0.8f);
                    audio.Play(Sfx.Hit, 0.8f);
                    ui.Popup(Strings.PopupCounter, pos, Pal.playerAccent);
                    ui.Flash(Color.white, 0.15f);
                    break;

                case FxType.PostureBreak:
                    clock.AddHitstop(Feel.hitstopPostureBreak);
                    cam.Shake(Feel.shakePostureBreak);
                    vfx.Ring(pos, magnitude * 1.2f, magnitude * 4f, Pal.gold, 0.4f);
                    vfx.Burst(pos, 14, Pal.gold, 6f);
                    audio.Play(Sfx.PostureBreak, 1f);
                    ui.Popup(Strings.PopupBreak, pos + Vector2.up * 0.6f, Pal.gold);
                    break;

                case FxType.Finisher:
                    clock.AddHitstop(Feel.hitstopFinisher);
                    cam.Shake(Feel.shakeFinisher);
                    cam.Punch(Feel.zoomPunchFinisher);
                    vfx.Sparks(pos, dir, 30, col, 13f, 80f);
                    vfx.Sparks(pos, dir, 14, Pal.gold, 10f, 30f);
                    vfx.Ring(pos, 0.3f, 3f, Color.white, 0.35f);
                    audio.Play(Sfx.Finisher, 1f);
                    ui.Popup(Strings.PopupFinisher, pos + Vector2.up * 0.6f, Color.white);
                    ui.Flash(Color.white, 0.25f);
                    break;

                case FxType.PlayerHurt:
                    clock.AddHitstop(Feel.hitstopHurt);
                    cam.Shake(Feel.shakeHurt);
                    vfx.Sparks(pos, dir, 12, Pal.danger, 8f);
                    audio.Play(Sfx.Hurt, 0.9f);
                    ui.HurtVignette();
                    break;

                case FxType.GuardBreak:
                    clock.AddHitstop(Feel.hitstopPostureBreak);
                    cam.Shake(Feel.shakePostureBreak);
                    vfx.Ring(pos, 0.4f, 2f, Pal.postureHigh, 0.35f);
                    audio.Play(Sfx.PostureBreak, 0.9f, 0.8f);
                    ui.Popup(Strings.PopupGuardBroken, pos + Vector2.up * 0.7f, Pal.postureHigh);
                    break;

                case FxType.PlayerDeath:
                    cam.Shake(0.7f);
                    vfx.Burst(pos, 24, Pal.player, 5f);
                    audio.Play(Sfx.Death, 1f, 0.7f);
                    break;

                case FxType.EnemyDeath:
                    vfx.Burst(pos, 16, col, 4.5f);
                    vfx.Rising(pos, magnitude, col, 18);
                    vfx.Ring(pos, magnitude, magnitude * 3f, col.WithAlpha(0.6f), 0.5f);
                    audio.Play(Sfx.Death, 0.6f);
                    break;

                case FxType.DangerCue:
                    vfx.Ring(pos, 0.3f, 1.4f, Pal.danger, 0.3f);
                    audio.Play(Sfx.Danger, 0.9f, 1f, 0f);
                    break;

                case FxType.Dodge:
                    vfx.Dust(pos, dir);
                    audio.Play(Sfx.Dodge, 0.5f);
                    break;

                case FxType.Afterimage:
                    vfx.Afterimage(pos, magnitude, col);
                    break;

                case FxType.FlowUp:
                    vfx.Ring(pos, 0.4f, 2.4f, Pal.gold, 0.45f);
                    audio.Play(Sfx.FlowUp, 0.8f, 1f + 0.12f * (magnitude - 1f), 0f);
                    ui.Toast(string.Format(Strings.ToastFlowUp, Mathf.RoundToInt(magnitude)), Pal.gold);
                    break;

                case FxType.FlowDown:
                    audio.Play(Sfx.FlowDown, 0.6f);
                    break;

                case FxType.TechniqueAbsorbed:
                    vfx.Stream(pos, game.World.Player.Position, 26, Pal.gold);
                    audio.Play(Sfx.Absorb, 1f);
                    break;

                case FxType.Mastered:
                    vfx.Ring(pos, 0.3f, 1.6f, Pal.gold, 0.4f);
                    audio.Play(Sfx.Chime, 0.8f);
                    break;

                case FxType.Spawn:
                    vfx.Ring(pos, magnitude * 3f, magnitude * 1.2f, col.WithAlpha(0.8f), 0.6f);
                    vfx.Rising(pos, magnitude, col, 22);
                    audio.Play(Sfx.Spawn, 0.5f);
                    break;

                case FxType.RoomClear:
                    audio.Play(Sfx.FlowUp, 0.7f, 0.8f, 0f);
                    break;

                case FxType.Burst:
                    cam.Shake(Feel.shakeHit);
                    vfx.Ring(pos, 0.2f, magnitude, Pal.playerAccent, 0.3f);
                    vfx.Burst(pos, 14, Pal.playerAccent, 6f);
                    audio.Play(Sfx.Hit, 0.7f, 1.3f);
                    break;

                case FxType.MirrorThrust:
                    vfx.Line(pos, dir, magnitude, 0.5f, Pal.playerAccent, 0.25f);
                    audio.Play(Sfx.Swing, 0.8f, 1.5f);
                    break;
            }
        }

        public void Popup(string text, Vector2 worldPosition, Color color)
        {
            game.UI.Popup(text, worldPosition, color);
        }

        public void Toast(string text, Color color)
        {
            game.UI.Toast(text, color);
        }

        public void Hint(HintId hint)
        {
            game.UI.ShowHint(Strings.HintText(hint, game.Controls.UsingGamepad));
        }
    }
}
