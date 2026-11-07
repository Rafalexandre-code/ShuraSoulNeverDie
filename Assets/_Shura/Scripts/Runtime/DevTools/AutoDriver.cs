using System.IO;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Drives menus during automated runs and, in screenshot mode, captures a tour of every
    /// screen. Logs a run summary and quits, so a build can be verified without a human.
    /// </summary>
    public sealed class AutoDriver
    {
        const float ShotInterval = 3f;
        const float ShotTourSkipRoomAt = 26f;
        const float ShotTourPauseAt = 40f;
        const float ShotTourEndAt = 48f;
        const float MaxSeconds = 900f;

        readonly Game game;
        readonly bool shots;
        float elapsed;
        float modeTime;
        float nextShot = 3f;
        int shotIndex;
        bool shotPending;
        int shotFrame;
        bool skippedRoom;
        bool pauseTourDone;
        bool settingsShotDone;
        bool finished;
        Game.Mode lastMode;

        public AutoDriver(Game owner)
        {
            game = owner;
            shots = owner.Launch.ShotDir != null;
            if (shots) Directory.CreateDirectory(owner.Launch.ShotDir);
            lastMode = owner.CurrentMode;
            Debug.Log("[Shura][Auto] start bot=" + owner.Launch.Bot + " shots=" + shots);
        }

        public void Update()
        {
            if (finished) return;
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            if (game.CurrentMode != lastMode)
            {
                Debug.Log("[Shura][Auto] t=" + elapsed.ToString("0.0") + "s mode=" + game.CurrentMode +
                          (game.Run != null ? " room=" + (game.Run.RoomIndex + 1) : string.Empty));
                lastMode = game.CurrentMode;
                modeTime = 0f;
                shotPending = false;
            }
            modeTime += dt;
            float wait = shots ? 0.8f : 0f;

            switch (game.CurrentMode)
            {
                case Game.Mode.Title:
                    if (ShotThenAct("title", wait)) game.GoToMainMenu();
                    break;
                case Game.Mode.MainMenu:
                    if (ShotThenAct("menu", wait)) game.StartRun();
                    break;
                case Game.Mode.Reward:
                    if (ShotThenAct("reward", wait)) game.ChooseReward(PickReward());
                    break;
                case Game.Mode.Playing:
                    UpdatePlaying();
                    break;
                case Game.Mode.Paused:
                    if (!settingsShotDone)
                    {
                        if (ShotThenAct("pause", wait)) game.OpenSettings();
                    }
                    else
                    {
                        pauseTourDone = true;
                        game.Resume();
                    }
                    break;
                case Game.Mode.Settings:
                    if (ShotThenAct("settings", wait))
                    {
                        settingsShotDone = true;
                        game.CloseSettings();
                    }
                    break;
                case Game.Mode.GameOver:
                case Game.Mode.Victory:
                    if (ShotThenAct(game.CurrentMode.ToString().ToLowerInvariant(), shots ? 1.2f : 0f)) Finish("fim normal");
                    break;
            }

            if (!finished && elapsed > MaxSeconds) Finish("tempo esgotado");
        }

        void UpdatePlaying()
        {
            if (!shots) return;
            if (elapsed >= nextShot)
            {
                Shot("play");
                nextShot = elapsed + ShotInterval;
            }
            if (!skippedRoom && elapsed > ShotTourSkipRoomAt && game.Run != null && game.Run.RoomIndex == 0)
            {
                skippedRoom = true;
                game.World.DevSkipRoom();
            }
            if (!pauseTourDone && elapsed > ShotTourPauseAt) game.Pause();
            if (pauseTourDone && elapsed > ShotTourEndAt) game.DevForceGameOver();
        }

        /// <summary>Takes the screenshot first and acts two frames later, so the capture shows this screen.</summary>
        bool ShotThenAct(string name, float wait)
        {
            if (modeTime < wait) return false;
            if (!shots) return true;
            if (!shotPending)
            {
                Shot(name);
                shotPending = true;
                shotFrame = Time.frameCount;
                return false;
            }
            if (Time.frameCount - shotFrame < 2) return false;
            shotPending = false;
            return true;
        }

        void Shot(string name)
        {
            if (!shots) return;
            string path = Path.Combine(game.Launch.ShotDir, shotIndex.ToString("00") + "_" + name + ".png");
            shotIndex++;
            ScreenCapture.CaptureScreenshot(path);
        }

        int PickReward()
        {
            // Prefer techniques so automated runs exercise the absorbed-technique hooks.
            for (int i = 0; i < game.RewardChoices.Count; i++)
            {
                if (game.RewardChoices[i].Kind == RewardKind.Technique) return i;
            }
            return 0;
        }

        void Finish(string reason)
        {
            finished = true;
            var sb = new StringBuilder("[Shura][Auto] RESUMO (" + reason + ")");
            sb.Append(" modo=").Append(game.CurrentMode);
            RunState run = game.Run;
            if (run != null)
            {
                sb.Append(" salas=").Append(run.RoomsCleared).Append('/').Append(RoomDefs.Prototype.Length);
                sb.Append(" parries=").Append(run.PerfectParries);
                sb.Append(" esquivas=").Append(run.PerfectDodges);
                sb.Append(" contras=").Append(run.Counters);
                sb.Append(" finalizacoes=").Append(run.Finishers);
                sb.Append(" tecnicasAbsorvidas=").Append(run.TechniquesAbsorbed);
                sb.Append(" fluxo=").Append(run.Flow.Level);
                sb.Append(" vida=").Append(Mathf.CeilToInt(game.World.Player.Health));
                sb.Append(" tempoSim=").Append(run.Elapsed.ToString("0.0")).Append('s');
                sb.Append(" semente=").Append(run.Seed);
                sb.Append(" recompensas=[");
                for (int i = 0; i < run.Owned.Count; i++) sb.Append(i > 0 ? ", " : string.Empty).Append(run.Owned[i].Name);
                sb.Append(']');
            }
            Debug.Log(sb.ToString());
            Application.Quit(0);
        }
    }
}
