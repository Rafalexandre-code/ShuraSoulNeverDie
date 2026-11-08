using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Translates gameplay moments into hit-stop, shake, particles, sound and UI. All feel
    /// numbers come from FeelTuning; the impact of the player's own blows grows with Flow,
    /// so a run in full Flow literally hits harder on screen and in the ears.
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
        int FlowLevel => game.Run != null ? game.Run.Flow.Level : 0;
        /// <summary>1 at Flow 0, growing with each level.</summary>
        float Impact => 1f + game.Config.flow.impactPerLevel * FlowLevel;
        int ImpactHitstop => FlowLevel;

        readonly SimClock mutedClock = new SimClock();

        /// <summary>
        /// In co-op, a teammate's parry must not freeze or shake the host's screen: their
        /// moments keep particles and (quieter) sound; hit-stop, shake, slow motion and screen
        /// flashes belong to whoever is at this computer.
        /// </summary>
        public void Emit(FxType type, Vector2 pos, Vector2 dir, float magnitude = 1f, Color? color = null)
        {
            bool remote = game.World != null && game.World.ActorIsRemote;
            if (!remote)
            {
                EmitCore(type, pos, dir, magnitude, color, game.Clock);
                return;
            }
            game.FeelMuted = true;
            game.Audio.VolumeScale = 0.55f;
            try
            {
                EmitCore(type, pos, dir, magnitude, color, mutedClock);
            }
            finally
            {
                game.FeelMuted = false;
                game.Audio.VolumeScale = 1f;
                mutedClock.Reset();
            }
        }

        void EmitCore(FxType type, Vector2 pos, Vector2 dir, float magnitude, Color? color, SimClock clock)
        {
            Vfx vfx = game.Vfx;
            AudioService audio = game.Audio;
            CameraRig cam = game.CameraRig;
            GameUI ui = game.UI;
            Color col = color ?? Color.white;
            float impact = Impact;
            int sparks = Mathf.RoundToInt(impact * 1f);

            switch (type)
            {
                case FxType.PlayerSwing:
                    // magnitude: the weapon's swing pitch (heavy weapons whoosh low and long).
                    audio.Play(magnitude < 0.95f ? Sfx.SwingHeavy : Sfx.Swing, 0.5f + 0.08f * FlowLevel, Mathf.Max(0.8f, magnitude) * (1.05f + 0.05f * FlowLevel));
                    if (game.Run != null && game.World.Player.Weapon.chain) audio.Play(Sfx.Chain, 0.4f);
                    break;

                case FxType.WeaponSwap:
                    audio.Play(Sfx.Unsheathe, 0.55f, 1.1f);
                    vfx.Ring(pos, 0.2f, 0.9f, col.WithAlpha(0.6f), 0.2f);
                    break;

                case FxType.SwapParry:
                    // Troca Perfeita: the biggest parry in the game.
                    clock.AddHitstop(Feel.hitstopPerfectParry + 3 + ImpactHitstop);
                    cam.Shake(Feel.shakePerfectParry * 1.4f * impact);
                    cam.Punch(Feel.zoomPunchPerfectParry * 1.6f);
                    game.SlowMo(0.3f, 0.35f);
                    vfx.ClashStar(pos, 2.2f * impact, Pal.gold);
                    vfx.Sparks(pos, dir, 26 * sparks, Pal.spark, 13f, 80f);
                    vfx.Ring(pos, 0.2f, 2.4f, Pal.gold, 0.35f);
                    vfx.Ring(pos, 0.1f, 1.4f, col, 0.25f);
                    audio.Play(Sfx.Clash, 1f, 0.95f);
                    audio.Play(Sfx.Unsheathe, 0.9f, 1f);
                    audio.Play(Sfx.PerfectParry, 0.7f, 1.15f);
                    ui.Popup(Strings.PopupSwapParry, pos + Vector2.up * 0.4f, Pal.gold);
                    ui.Flash(Pal.gold, 0.2f);
                    break;

                case FxType.SwapStrike:
                    cam.Punch(0.2f);
                    audio.Play(Sfx.SwingHeavy, 0.9f, Mathf.Max(0.85f, magnitude * 0.95f));
                    vfx.Ring(pos, 0.3f, 2.2f, col.WithAlpha(0.8f), 0.3f);
                    break;

                case FxType.ThrustTrail:
                    vfx.Line(pos, dir, magnitude, 0.35f, col, 0.16f);
                    break;

                case FxType.SlamImpact:
                    cam.Shake(Feel.shakeHit * 1.5f * impact);
                    vfx.Ring(pos, 0.2f, magnitude, Color.white.WithAlpha(0.7f), 0.3f);
                    vfx.Dust(pos, dir);
                    vfx.Dust(pos, -dir);
                    audio.Play(Sfx.Crunch, 0.5f, 1.3f);
                    break;

                case FxType.FinisherStart:
                    cam.Punch(0.25f);
                    audio.Play(Sfx.Unsheathe, 0.6f, 0.8f);
                    break;

                case FxType.FinisherHit:
                    clock.AddHitstop(Mathf.RoundToInt(3f + 5f * magnitude));
                    cam.Shake(0.15f + 0.25f * magnitude);
                    vfx.Sparks(pos, dir, 6, Color.white, 7f);
                    audio.Play(Sfx.Slice, 0.6f + 0.4f * magnitude, 1f);
                    break;

                case FxType.Crit:
                    clock.AddHitstop(3);
                    cam.Shake(Feel.shakeHit);
                    ui.Popup(Strings.PopupCrit, pos + Vector2.up * 0.5f, Pal.gold);
                    vfx.Blood(pos, dir, 14, 9f, 30f, col);
                    audio.Play(Sfx.Slice, 0.7f, 1.2f);
                    break;

                case FxType.DotTick:
                    ui.DamageNumber(Mathf.RoundToInt(magnitude), pos, 0);
                    vfx.Rising(pos, 0.35f, col, 4);
                    break;

                case FxType.Lightning:
                    vfx.Line(pos, dir, magnitude, 0.18f, col, 0.18f);
                    vfx.Sparks(pos + dir * magnitude, dir, 8, col, 7f, 180f);
                    audio.Play(Sfx.Glint, 0.6f, 0.6f);
                    break;

                case FxType.ItemDrop:
                {
                    int tier = Mathf.RoundToInt(magnitude);
                    vfx.Ring(pos, 0.2f, 1.2f + 0.2f * tier, col, 0.5f);
                    vfx.Rising(pos, 0.4f, col, 8 + 3 * tier);
                    audio.Play(Sfx.Chime, 0.5f + 0.05f * tier, 0.8f + 0.06f * tier, 0f);
                    if (tier >= (int)Rarity.Legendary)
                    {
                        cam.Punch(0.15f);
                        audio.Play(Sfx.Absorb, 0.6f, 1f + 0.05f * tier, 0f);
                    }
                    break;
                }

                case FxType.EnemySwing:
                    // magnitude: 1 normal, 1.5 dangerous, 2 bow release.
                    if (magnitude > 1f && magnitude < 2f) audio.Play(Sfx.SwingHeavy, 0.55f, 0.85f);
                    else audio.Play(Sfx.Swing, 0.45f, magnitude >= 2f ? 1.6f : 0.9f);
                    break;

                case FxType.HitEnemy:
                    clock.AddHitstop(Feel.hitstopHit + ImpactHitstop);
                    cam.Shake(Feel.shakeHit * impact);
                    vfx.Sparks(pos, dir, 5 * sparks, Color.white, 7f * impact);
                    vfx.Blood(pos, dir, 8 + 5 * sparks, 6.5f * impact, 35f, col);
                    vfx.Ring(pos, 0.1f, 0.6f * impact, Color.white.WithAlpha(0.7f), 0.15f);
                    audio.Play(Sfx.Hit, 0.75f + 0.08f * FlowLevel, 1f - 0.04f * FlowLevel);
                    audio.Play(Sfx.Slice, 0.35f, 1.1f);
                    ui.DamageNumber(Mathf.RoundToInt(magnitude), pos, FlowLevel);
                    break;

                case FxType.EnemyBlocked:
                    // The enemy's guard deflected the player's swing: steel on steel, your blade bounces.
                    clock.AddHitstop(Feel.hitstopDeflected);
                    cam.Shake(Feel.shakeDeflected);
                    vfx.ClashStar(pos, 1.1f, Pal.spark);
                    vfx.Sparks(pos, dir, 10, Pal.spark, 7f, 70f);
                    audio.Play(Sfx.Clash, 0.85f, 0.9f);
                    ui.Popup(Strings.PopupBlocked, pos, Pal.uiDim);
                    break;

                case FxType.PlayerBlock:
                    clock.AddHitstop(Feel.hitstopBlock);
                    cam.Shake(Feel.shakeBlock);
                    vfx.ClashStar(pos, 0.9f, Pal.spark);
                    vfx.Sparks(pos, dir, 8, Pal.spark, 6f);
                    audio.Play(Sfx.Clash, 0.75f, 0.82f);
                    audio.Play(Sfx.Block, 0.5f);
                    break;

                case FxType.PerfectParry:
                    clock.AddHitstop(Feel.hitstopPerfectParry + ImpactHitstop);
                    cam.Shake(Feel.shakePerfectParry * impact);
                    cam.Punch(Feel.zoomPunchPerfectParry * impact);
                    vfx.ClashStar(pos, 1.6f * impact, Pal.playerAccent);
                    vfx.Sparks(pos, dir, 18 * sparks, Pal.spark, 11f * impact, 70f);
                    vfx.Sparks(pos, -dir, 8 * sparks, Color.white, 8f, 50f);
                    vfx.Ring(pos, 0.15f, 1.4f * impact, Pal.playerAccent, 0.25f);
                    audio.Play(Sfx.Clash, 1f, 1.05f);
                    audio.Play(Sfx.PerfectParry, 0.7f, 1f + 0.04f * FlowLevel);
                    ui.Popup(Strings.PopupPerfect, pos, Pal.playerAccent);
                    ui.Flash(Color.white, 0.12f);
                    break;

                case FxType.PerfectDodge:
                    vfx.Ring(pos, 0.3f, 1.2f, Pal.playerAccent.WithAlpha(0.7f), 0.3f);
                    audio.Play(Sfx.Chime, 0.6f, 1.2f);
                    ui.Popup(Strings.PopupDodge, pos, Pal.playerAccent);
                    break;

                case FxType.CounterThrust:
                    clock.AddHitstop(Feel.hitstopCounter + ImpactHitstop);
                    cam.Shake(Feel.shakeCounter * impact);
                    cam.Punch(Feel.zoomPunchPerfectParry * 1.4f * impact);
                    vfx.ClashStar(pos, 1.8f * impact, Pal.playerAccent);
                    vfx.Sparks(pos, dir, 22 * sparks, Pal.spark, 12f, 40f);
                    vfx.Ring(pos, 0.2f, 1.8f, Pal.playerAccent, 0.3f);
                    audio.Play(Sfx.Clash, 1f, 0.75f);
                    audio.Play(Sfx.Hit, 0.8f);
                    ui.Popup(Strings.PopupCounter, pos, Pal.playerAccent);
                    ui.Flash(Color.white, 0.15f);
                    break;

                case FxType.PostureBreak:
                    clock.AddHitstop(Feel.hitstopPostureBreak + ImpactHitstop);
                    cam.Shake(Feel.shakePostureBreak * impact);
                    game.SlowMo(Feel.slowMoBreakSeconds, Feel.slowMoBreakScale);
                    vfx.Ring(pos, magnitude * 1.2f, magnitude * 4f, Pal.gold, 0.4f);
                    vfx.Burst(pos, 14 * sparks, Pal.gold, 6f);
                    audio.Play(Sfx.PostureBreak, 1f);
                    ui.Popup(Strings.PopupBreak, pos + Vector2.up * 0.6f, Pal.gold);
                    break;

                case FxType.Finisher:
                {
                    bool lastKill = magnitude >= 2f;
                    clock.AddHitstop(Feel.hitstopFinisher + ImpactHitstop);
                    cam.Shake(Feel.shakeFinisher * impact * (lastKill ? 1.3f : 1f));
                    cam.Punch(Feel.zoomPunchFinisher * impact * (lastKill ? 1.5f : 1f));
                    if (lastKill) game.SlowMo(Feel.slowMoLastKillSeconds, Feel.slowMoLastKillScale);
                    vfx.ClashStar(pos, 2.2f * impact, Pal.gold);
                    vfx.Sparks(pos, dir, 14 * sparks, Pal.gold, 10f, 30f);
                    vfx.Ring(pos, 0.3f, 3f * impact, Color.white, 0.35f);
                    audio.Play(Sfx.Finisher, 1f);
                    audio.Play(Sfx.Splatter, 0.9f);
                    ui.Flash(lastKill ? Color.white : col, lastKill ? 0.4f : 0.2f);
                    break;
                }

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
                    game.SlowMo(0.8f, 0.3f);
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

                case FxType.TimingCue:
                    if (!game.Settings.timingIndicator) break;
                    audio.Play(Sfx.Glint, magnitude > 0.5f ? 0.55f : 0.45f, magnitude > 0.5f ? 0.8f : 1f, 0f);
                    break;

                case FxType.Dodge:
                    vfx.Dust(pos, dir);
                    audio.Play(Sfx.Dodge, 0.5f);
                    break;

                case FxType.Footstep:
                    vfx.Footstep(pos, dir);
                    break;

                case FxType.Afterimage:
                    vfx.Afterimage(pos, magnitude, col);
                    break;

                case FxType.ShadowStep:
                    vfx.Rising(pos, magnitude, col, 16);
                    vfx.Ring(pos, magnitude * 2.5f, magnitude, col.WithAlpha(0.8f), 0.25f);
                    audio.Play(Sfx.Dodge, 0.8f, 0.6f);
                    break;

                case FxType.FlowUp:
                    vfx.Ring(pos, 0.4f, 2.4f + magnitude, Pal.gold, 0.45f);
                    vfx.Burst(pos, 10 + 6 * Mathf.RoundToInt(magnitude), Pal.gold, 5f);
                    cam.Punch(0.12f * magnitude);
                    audio.Play(Sfx.FlowUp, 0.8f, 1f + 0.12f * (magnitude - 1f), 0f);
                    ui.Toast(string.Format(Strings.ToastFlowUp, Mathf.RoundToInt(magnitude)), Pal.gold);
                    ui.Flash(Pal.gold, 0.12f);
                    break;

                case FxType.FlowDown:
                    audio.Play(Sfx.FlowDown, 0.6f);
                    break;

                case FxType.StreakMilestone:
                    vfx.Ring(pos, 0.3f, 2.2f, Pal.gold, 0.35f);
                    audio.Play(Sfx.Chime, 0.7f, 1f + Mathf.Min(0.6f, magnitude * 0.02f), 0f);
                    ui.Toast(string.Format(Strings.ToastStreak, Mathf.RoundToInt(magnitude)), Pal.gold);
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
                    cam.Shake(Feel.shakeHit * impact);
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

        public void Blood(Vector2 position, Vector2 direction, float amount, Color color)
        {
            int count = Mathf.RoundToInt(amount);
            game.Vfx.Blood(position, direction, count, 5f + amount * 0.08f, 45f, color);
            if (amount >= 12f) game.Vfx.BloodPool(position + direction * 0.4f, 0.25f + amount * 0.012f, color);
        }

        /// <summary>The body's fate: every weapon kills differently, and the floor remembers.</summary>
        public void Gore(FinisherStyle style, Vector2 pos, Vector2 dir, Sprite bodySprite, Color bodyColor, Color blood, float radius)
        {
            Vfx vfx = game.Vfx;
            AudioService audio = game.Audio;
            CameraRig cam = game.CameraRig;
            Vector2 perp = CombatMath.Perpendicular(dir);
            // The body pieces are the enemy's own pixel art, stained with its blood.
            Color flesh = Color.Lerp(Color.white, blood, 0.3f);
            float size = radius * 2f;
            // Body sprites are drawn 1:1 in the arena; their pieces keep that size.
            const float bodySize = 1f;

            switch (style)
            {
                case FinisherStyle.CleanCut:
                    // Split along the cut: two halves slide apart, a geyser between them.
                    vfx.Gib(bodySprite, pos + perp * radius * 0.4f, perp * 3.2f + dir * 0.8f, bodySize, 0.5f, flesh, blood);
                    vfx.Gib(bodySprite, pos - perp * radius * 0.4f, -perp * 3.2f + dir * 0.8f, bodySize, 0.5f, flesh, blood);
                    vfx.Blood(pos, perp, 40, 9f, 25f, blood);
                    vfx.Blood(pos, -perp, 40, 9f, 25f, blood);
                    vfx.BloodPool(pos, size * 1.6f, blood);
                    vfx.BloodStreak(pos, perp, 2.2f, blood);
                    vfx.BloodStreak(pos, -perp, 2.2f, blood);
                    audio.Play(Sfx.Slice, 1f, 0.8f);
                    audio.Play(Sfx.SheathClick, 0.9f, 1f, 0f);
                    break;

                case FinisherStyle.Execution:
                    // Crushed into the floor: chunks scatter, a wide pool spreads.
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 v = Random.insideUnitCircle.normalized * Random.Range(2f, 5f);
                        vfx.Gib(PixelArt.GoreChunk(i), pos, v, radius * Random.Range(0.6f, 0.95f), 1f, Color.Lerp(Color.white, blood, 0.2f), blood);
                    }
                    vfx.Blood(pos, dir, 90, 10f, 180f, blood);
                    vfx.BloodPool(pos, size * 2.4f, blood);
                    vfx.Ring(pos, 0.4f, 3.5f, Color.white.WithAlpha(0.8f), 0.35f);
                    cam.Shake(0.5f);
                    audio.Play(Sfx.Crunch, 1f, 0.8f);
                    break;

                case FinisherStyle.ThousandCuts:
                    vfx.Gib(bodySprite, pos, dir * 1.2f, bodySize, 1f, flesh * 0.8f, blood);
                    vfx.Blood(pos, dir, 70, 8f, 180f, blood);
                    vfx.BloodPool(pos, size * 1.8f, blood);
                    audio.Play(Sfx.Slice, 1f, 1.2f);
                    break;

                case FinisherStyle.Impale:
                    // Flung off the polearm: the body tumbles away painting a long smear.
                    vfx.Gib(bodySprite, pos, dir * 9f, bodySize, 1f, flesh, blood);
                    vfx.Blood(pos, dir, 60, 11f, 20f, blood);
                    vfx.BloodStreak(pos, dir, 3.5f, blood);
                    vfx.BloodPool(pos + dir * 3.5f, size * 1.4f, blood);
                    break;

                case FinisherStyle.Reap:
                    vfx.Gib(bodySprite, pos, dir * 10f, bodySize, 1f, flesh, blood);
                    vfx.Blood(pos, dir, 70, 10f, 180f, blood);
                    vfx.Ring(pos, 0.3f, 2.6f, blood.WithAlpha(0.6f), 0.3f);
                    vfx.BloodStreak(pos, dir, 3f, blood);
                    audio.Play(Sfx.Chain, 0.8f, 0.8f);
                    break;

                case FinisherStyle.HeartBreak:
                    // A fist straight through: everything behind the enemy turns red.
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 v = CombatMath.RotateTowards(dir, perp * (i % 2 == 0 ? 1f : -1f), Random.Range(0f, 35f)) * Random.Range(5f, 9f);
                        vfx.Gib(PixelArt.GoreChunk(i + 1), pos, v, radius * Random.Range(0.55f, 0.85f), 1f, Color.Lerp(Color.white, blood, 0.2f), blood);
                    }
                    vfx.Gib(bodySprite, pos, dir * 6f, bodySize, 1f, flesh * 0.8f, blood);
                    vfx.Blood(pos, dir, 100, 13f, 30f, blood);
                    vfx.BloodPool(pos + dir * 1.5f, size * 2f, blood);
                    audio.Play(Sfx.Crunch, 1f, 1f);
                    break;
            }
        }

        float nextStep;

        public void Footstep(Vector2 position)
        {
            if (Time.unscaledTime < nextStep) return;
            nextStep = Time.unscaledTime + 0.12f;
            game.Audio.Play(Sfx.Step, 0.32f, 1f, 0.1f);
        }

        public void ItemCollected(Item item, bool equipped, Vector2 position)
        {
            Color rc = Rarities.Color(item.rarity);
            game.Audio.Play(Sfx.Pickup, 0.6f + 0.04f * (int)item.rarity, 1f + 0.05f * (int)item.rarity, 0f);
            game.Vfx.Ring(position, 0.3f, 1.4f, rc, 0.3f);
            string text = string.Format(equipped ? Strings.ToastItemEquipped : Strings.ToastItemBag, item.Name, Rarities.Name(item.rarity));
            game.UI.Toast(text, rc);
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
