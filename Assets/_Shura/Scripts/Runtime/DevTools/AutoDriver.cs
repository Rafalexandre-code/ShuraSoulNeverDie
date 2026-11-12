using System.Collections.Generic;
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
        bool altarShotDone;
        int templeShot;
        bool heirShotDone;
        bool modesShotDone;
        bool equipmentShotDone;
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
                    if (ShotThenAct("menu", wait))
                    {
                        if (tutorialStarted) Finish("dojo concluído");
                        else if (game.Launch.Tutorial)
                        {
                            tutorialStarted = true;
                            game.StartTutorial();
                        }
                        else if (remoteDone) Finish("o anfitrião encerrou");
                        else if (game.Launch.JoinAddress != null) game.JoinByAddress(game.Launch.JoinAddress);
                        else if (game.Launch.SteamGuest) game.OpenSteamJoin();
                        else if (game.Launch.Host) game.OpenLobby();
                        else if (shots && !creditsShotDone) game.OpenCredits();
                        else if (shots) game.OpenArsenal();
                        else game.StartRun();
                    }
                    break;
                case Game.Mode.Lobby:
                    // Screenshot tour: the in-game Steam invite list once, then the room as usual.
                    if (shots && game.Steam.Ready && !inviteShotDone && modeTime > 3f)
                    {
                        game.UI.DevInvites(true);
                        if (ShotThenAct("lobby_invites", wait))
                        {
                            inviteShotDone = true;
                            game.UI.DevInvites(false);
                        }
                        break;
                    }
                    // Online test: start once the expected browser players are in (or give up waiting).
                    if (game.Net.JoinedCount >= game.Launch.HostWait || modeTime > 40f)
                    {
                        if (ShotThenAct("lobby", wait))
                        {
                            Debug.Log("[Shura][Auto] começando online com " + game.World.Players.Count + " jogadores");
                            game.StartRun();
                        }
                    }
                    break;
                case Game.Mode.Credits:
                    if (ShotThenAct("credits", wait))
                    {
                        creditsShotDone = true;
                        game.GoToMainMenu();
                    }
                    break;
                case Game.Mode.Arsenal:
                    if (!arsenalShotDone)
                    {
                        if (!ShotThenAct("arsenal", wait)) break;
                        arsenalShotDone = true;
                        if (!shots)
                        {
                            game.StartRun();
                            break;
                        }
                        // The forge's tour: a Nagamaki (katana blade on a naginata grip) with the Moon tsuba.
                        game.Settings.SetForge(game.Settings.weaponA, "naginata", "lua");
                        game.UI.OpenForgeForShots();
                        break;
                    }
                    if (ShotThenAct("forge", 0f))
                    {
                        game.UI.CloseForgeForShots();
                        game.StartRun();
                    }
                    break;
                case Game.Mode.SteamJoin:
                    // Test guest: refresh until a duel shows up, join it, then just keep the tunnel open.
                    if (remoteDone && !game.Remote.Active)
                    {
                        Finish("o anfitrião encerrou");
                        break;
                    }
                    if (game.Remote.Active || steamJoinAsked || game.Launch.JoinAddress != null) break;
                    if (game.Steam.Lobbies.Count > 0)
                    {
                        steamJoinAsked = true;
                        Debug.Log("[Shura][Auto] entrando na sala Steam de " + game.Steam.Lobbies[0].host);
                        game.JoinSteamLobby(game.Steam.Lobbies[0].id);
                    }
                    else if (!game.Steam.Searching && modeTime > 3f)
                    {
                        modeTime = 0f;
                        game.Steam.RefreshLobbies();
                    }
                    break;
                case Game.Mode.Remote:
                    UpdateRemote();
                    break;
                case Game.Mode.Route:
                    if (ShotThenAct("route", wait) && game.RouteOptions.Count > 0)
                    {
                        // Walk every kind of door over a run: rotate through the options.
                        game.VoteRoute(-1, routePick++ % game.RouteOptions.Count);
                    }
                    break;
                case Game.Mode.Reward:
                    if (ShotThenAct("reward", wait))
                    {
                        if (shots && !equipmentShotDone) game.OpenEquipment();
                        else game.ChooseReward(PickReward());
                    }
                    break;
                case Game.Mode.Equipment:
                    if (ShotThenAct("equipment", wait))
                    {
                        equipmentShotDone = true;
                        game.CloseEquipment();
                    }
                    break;
                case Game.Mode.Playing:
                    if (game.Tutorial.Active) UpdateTutorial();
                    else UpdatePlaying();
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
                    if (!controlsShotDone)
                    {
                        if (!ShotThenAct("settings", wait)) break;
                        controlsShotDone = true;
                        game.UI.OpenControls();
                        break;
                    }
                    if (ShotThenAct("controls", 0f))
                    {
                        settingsShotDone = true;
                        game.UI.CloseControlsForShots();
                        game.CloseSettings();
                    }
                    break;
                case Game.Mode.GameOver:
                    if (ShotThenAct(game.CurrentMode.ToString().ToLowerInvariant(), shots ? 1.2f : 0f))
                    {
                        if (shots && templeShot > 0 && !modesShotDone) game.OpenModes();
                        else if (shots && templeShot == 0)
                        {
                            // The temple's tour: everything rebuilt, a few oaths and tombs to look at.
                            game.DevPrepareTempleShots();
                            game.OpenTemple();
                        }
                        else Finish("fim normal");
                    }
                    break;
                case Game.Mode.Temple:
                    if (!altarShotDone)
                    {
                        string[] views = { "templo", "templo_dojo", "templo_biblioteca", "templo_jardim", "templo_cemiterio", "templo_cha" };
                        if (templeShot < views.Length)
                        {
                            game.UI.ShowTempleViewForShots(templeShot);
                            if (ShotThenAct(views[templeShot], wait)) templeShot++;
                            break;
                        }
                        game.UI.ShowTempleViewForShots(0);
                        game.OpenAltar();
                        break;
                    }
                    if (!heirShotDone && game.Meta.heirs.Count > 0)
                    {
                        game.OpenHeir(Game.Mode.GameOver);
                        break;
                    }
                    if (!modesShotDone)
                    {
                        game.OpenModes();
                        break;
                    }
                    Finish("fim normal");
                    break;
                case Game.Mode.Modes:
                    if (ShotThenAct("modos", wait))
                    {
                        modesShotDone = true;
                        Finish("fim normal");
                    }
                    break;
                case Game.Mode.Altar:
                    if (ShotThenAct("altar", wait))
                    {
                        altarShotDone = true;
                        game.CloseAltar();
                    }
                    break;
                case Game.Mode.Heir:
                    if (ShotThenAct("herdeiro", wait))
                    {
                        heirShotDone = true;
                        game.ChooseHeir(0);
                    }
                    break;
            }

            if (!finished && elapsed > MaxSeconds) Finish("tempo esgotado");
        }

        bool tutorialStarted;
        int tutorialShotStep = -1;
        bool tutorialFreezeShot;

        /// <summary>The dojo, played by the test duelist: one picture of every lesson and of every frozen moment.</summary>
        void UpdateTutorial()
        {
            Tutorial t = game.Tutorial;
            if (!shots) return;
            int step = (int)t.Current;
            // The prologue: the title, the walk, and a picture of every line.
            if (t.Current == Tutorial.Step.Prologue)
            {
                int key = t.ProloguePhase * 100 + (t.ProloguePhase == 2 ? t.LineIndex : 0);
                bool ready = t.ProloguePhase == 2 ? t.LineTime > 1.2f : t.PhaseTime > (t.ProloguePhase == 0 ? 1.6f : 2.5f);
                if (key != prologueShot && ready)
                {
                    prologueShot = key;
                    Shot("prologo_" + key.ToString("000"));
                }
                return;
            }
            // The HUD tour: one picture per page.
            if (t.Touring && t.TourIndex != tourShotPage && t.TourTime > 0.45f)
            {
                tourShotPage = t.TourIndex;
                Shot("dojo_hud_" + t.TourIndex.ToString("00"));
                return;
            }
            if (t.Frozen && !tutorialFreezeShot)
            {
                tutorialFreezeShot = true;
                Shot("dojo_" + step.ToString("00") + "_" + t.Current + "_agora");
            }
            if (!t.Frozen) tutorialFreezeShot = false;
            if (step != tutorialShotStep && t.StepTime > 1.6f && t.Flash <= 0f && !t.Frozen)
            {
                tutorialShotStep = step;
                Shot("dojo_" + step.ToString("00") + "_" + t.Current);
            }
        }

        bool remoteDone;
        int tourShotPage = -1;
        int prologueShot = -1;
        bool creditsShotDone;
        bool inviteShotDone;
        float remoteShotTimer;
        int remoteShots;
        float remoteLogTimer;

        /// <summary>A native test guest: it plays by itself (RemoteSession.AutoPlay); this takes pictures and logs.</summary>
        void UpdateRemote()
        {
            RemoteSession r = game.Remote;
            remoteDone = true;
            remoteLogTimer += Time.unscaledDeltaTime;
            if (remoteLogTimer > 5f)
            {
                remoteLogTimer = 0f;
                System.Collections.Generic.List<object> me = r.Me;
                Debug.Log("[Shura][Auto][Remoto] modo=" + r.Mode + " ping=" + Mathf.RoundToInt(r.Ping * 1000f) + "ms atraso=" + Mathf.RoundToInt(r.InterpolationDelayMs) +
                          "ms jogadores=" + (r.Latest?.P?.Count ?? 0) + " inimigos=" + (r.Latest?.E?.Count ?? 0) +
                          (me != null ? " vida=" + MiniJson.I(me, 26) + " pos=" + r.OwnPosition.ToString("0.0") : ""));
            }
            if (!shots) return;
            remoteShotTimer += Time.unscaledDeltaTime;
            if (remoteShotTimer < 3f || remoteShots >= 30) return;
            remoteShotTimer = 0f;
            Shot("remote_" + r.Mode + "_" + remoteShots++);
        }

        int burstFrames;
        int burstStride;
        int burstCounter;
        bool swingBurstDone;
        bool finisherBurstDone;

        /// <summary>Consecutive-frame captures of a swing and a finisher, to judge animation fluidity.</summary>
        void UpdateBursts()
        {
            PlayerController p = game.World.Player;
            if (!swingBurstDone && p.CurrentState == PlayerController.State.Attack && p.StateFrame <= 1)
            {
                swingBurstDone = true;
                burstFrames = 12;
                burstStride = 1;
                burstCounter = 0;
            }
            if (!finisherBurstDone && p.CurrentState == PlayerController.State.Finisher && p.StateFrame <= 1)
            {
                finisherBurstDone = true;
                burstFrames = 14;
                burstStride = 3;
                burstCounter = 0;
            }
            if (burstFrames <= 0) return;
            if (burstCounter++ % burstStride != 0) return;
            Shot(burstStride == 1 ? "swing" : "finisher");
            burstFrames--;
        }

        float nextBeat = 20f;

        static string DescribeOfferings(World w, PlayerController p)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Offering o in w.Offerings)
            {
                sb.Append(o.Title).Append('@').Append(o.Position).Append(o.AvailableTo(p) ? "" : "(x)").Append(o.Spent ? "(gasto)" : "")
                  .Append(" p=").Append(o.MaxProgress.ToString("0.0")).Append("; ");
            }
            return sb.ToString();
        }
        int routePick;
        bool steamJoinAsked;

        void UpdatePlaying()
        {
            if (shots) UpdateBursts();
            // The soul form, twice: as it takes the body and halfway through.
            PlayerController host = game.World != null ? game.World.Host : null;
            // The tour shows the target lock too.
            if (shots && host != null) host.DevForceLock = true;
            if (shots && host != null && host.InSoul && soulShots < 2 && host.SoulFramesLeft < PlayerController.SoulFrames - 20 - soulShots * 200)
            {
                Shot("soul");
                soulShots++;
            }
            // Endless runs: an automated run stops at the target depth.
            if (!shots && game.Run != null && game.Run.RoomsCleared >= game.Launch.TargetDepth)
            {
                Finish("profundidade alvo");
                return;
            }
            if (!shots)
            {
                // A heartbeat every 20 s of real time, so a stuck run shows where it stands.
                if (elapsed >= nextBeat)
                {
                    nextBeat = elapsed + 20f;
                    World w = game.World;
                    PlayerController p = w.Player;
                    var sb = new System.Text.StringBuilder();
                    foreach (Enemy e in w.Enemies)
                    {
                        sb.Append(e.Def.id).Append(':').Append(e.CurrentState).Append(e.HeldInSpawn ? "(held)" : "")
                          .Append(e.IsIntangible ? "(int)" : "").Append(" hp=").Append(e.Health.ToString("0")).Append(' ');
                    }
                    Debug.Log("[Shura][Auto] pulso t=" + elapsed.ToString("0") + "s sala=" + (game.Run.RoomIndex + 1) + " onda=" + w.WaveIndex +
                              " jogador=" + p.CurrentState + " vida=" + p.Health.ToString("0") + " folego=" + p.Stamina.ToString("0") +
                              " iai=" + w.Standoff.Active + " inimigos=[" + sb + "]" + " tipo=" + w.CurrentRoom.kind + " ativos=" + w.ActiveEnemyCount +
                              " pos=" + p.Position + " ofertas=[" + DescribeOfferings(w, p) + "]");
                }
                return;
            }
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
        bool arsenalShotDone;
        bool controlsShotDone;
        int soulShots;

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
                sb.Append(" profundidade=").Append(run.RoomsCleared);
                sb.Append(" morte=[").Append(game.World.Host.LastHurtBy).Append(']');
                sb.Append(" ascensao=").Append(run.Ascension);
                sb.Append(" guardioes=").Append(run.ElitesKilled);
                sb.Append(" sequenciaMax=").Append(run.BestStreak);
                sb.Append(" trocasPerfeitas=").Append(run.SwapParries);
                sb.Append(" armas=").Append(run.Weapons[0].id).Append('+').Append(run.Weapons[1].id);
                int equipped = 0;
                Rarity best = Rarity.Common;
                foreach (Item item in run.Equipped)
                {
                    if (item == null) continue;
                    equipped++;
                    if (item.rarity > best) best = item.rarity;
                }
                sb.Append(" itens=").Append(equipped).Append(" equipados/").Append(run.Bag.Count).Append(" mochila, melhor=").Append(Rarities.Name(best));
                sb.Append(" leitura=[");
                List<ReadingId> reading = game.World.Adaptation.Active();
                for (int i = 0; i < reading.Count; i++) sb.Append(i > 0 ? ", " : string.Empty).Append(Strings.ReadingName(reading[i]));
                sb.Append("] salas=[");
                for (int i = 0; i <= run.RoomIndex; i++) sb.Append(i > 0 ? ", " : string.Empty).Append(run.GetRoom(i).name);
                sb.Append(']');
                sb.Append(" parries=").Append(run.PerfectParries);
                sb.Append(" esquivas=").Append(run.PerfectDodges);
                sb.Append(" contras=").Append(run.Counters);
                sb.Append(" finalizacoes=").Append(run.Finishers);
                sb.Append(" tecnicasAbsorvidas=").Append(run.TechniquesAbsorbed);
                sb.Append(" fluxo=").Append(run.Flow.Level);
                sb.Append(" vida=").Append(Mathf.CeilToInt(game.World.Player.Health));
                sb.Append(" tempoSim=").Append(run.Elapsed.ToString("0.0")).Append('s');
                sb.Append(" semente=").Append(run.Seed);
                sb.Append(" jogadores=").Append(game.World.Players.Count);
                int alive = 0;
                foreach (PlayerController pl in game.World.Players) if (pl.IsAlive) alive++;
                sb.Append(" vivos=").Append(alive);
                sb.Append(" ecos=").Append(MetaRules.EcosForRun(run)).Append(" (total ").Append(game.Meta.ecos).Append(')');
                sb.Append(" recompensas=[");
                for (int i = 0; i < run.Owned.Count; i++) sb.Append(i > 0 ? ", " : string.Empty).Append(run.Owned[i].Name);
                sb.Append(']');
            }
            Debug.Log(sb.ToString());
            Application.Quit(0);
        }
    }
}
