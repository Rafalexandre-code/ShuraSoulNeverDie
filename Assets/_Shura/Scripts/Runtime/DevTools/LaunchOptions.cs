using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Command-line switches for automated verification of a built player:
    ///   -shuraBot            a bot plays a full run (headless with -batchmode -nographics) and logs a summary
    ///   -shuraShots &lt;dir&gt;   the bot plays in real time while screenshots are saved to dir
    ///   -shuraSeed &lt;n&gt;      fixed run seed
    /// </summary>
    public sealed class LaunchOptions
    {
        public bool Bot;
        public string ShotDir;
        public ulong? Seed;
        public bool Headless;
        /// <summary>Automated runs end after clearing this many rooms (the game itself is endless).</summary>
        public int TargetDepth = 15;
        /// <summary>Automated runs only: start deep (with matching Ascension) to inspect later content.</summary>
        public int StartDepth;
        /// <summary>Automated runs only: "-shuraWeapons katana,montante".</summary>
        public string WeaponA;
        public string WeaponB;
        /// <summary>Automated runs only: "-shuraRoomKind Merchant" opens on that kind of door; "-shuraEvent 3" picks the event.</summary>
        public string RoomKind;
        public int EventId = -1;
        /// <summary>Automated runs only: "-shuraWeather Storm" forces the first room's weather.</summary>
        public string Weather;
        /// <summary>"-shuraSteam": automated runs also start Steam (online tests).</summary>
        public bool Steam;
        /// <summary>"-shuraSteamGuest": a test guest that joins the first Steam duel it finds (even its own account's).</summary>
        public bool SteamGuest;
        /// <summary>"-shuraNoBrowser": kept for old scripts (guests no longer use a browser).</summary>
        public bool NoBrowser;
        /// <summary>"-shuraJoin host[:port]": a test guest that joins that host directly (native client over WebSocket).</summary>
        public string JoinAddress;
        /// <summary>"-shuraStance N": automated runs hold this guard (0 Jōdan, 1 Chūdan, 2 Gedan, 3 Waki).</summary>
        public int Stance = -1;
        /// <summary>"-shuraSoul tecId": automated runs start with this technique at level III (its soul ready).</summary>
        public string Soul;
        /// <summary>"-shuraTutorial [N]": automated runs play the dojo (from lesson N, 0-based).</summary>
        public bool Tutorial;
        public int TutorialStep = -1;

        /// <summary>"-shuraHost N": open the online room and start once N browser players joined (or after 40 s).</summary>
        public bool Host;
        public int HostWait;
        public int Port = NetSession.DefaultPort;

        public bool Automated => Bot || ShotDir != null;

        public static LaunchOptions FromCommandLine()
        {
            var options = new LaunchOptions { Headless = Application.isBatchMode };
            if (Application.isEditor) return options;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-shuraBot":
                        options.Bot = true;
                        break;
                    case "-shuraShots":
                        if (i + 1 < args.Length) options.ShotDir = args[++i];
                        options.Bot = true;
                        break;
                    case "-shuraRoomKind":
                        if (i + 1 < args.Length) options.RoomKind = args[++i];
                        break;
                    case "-shuraEvent":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int ev)) options.EventId = ev;
                        i++;
                        break;
                    case "-shuraSteam":
                        options.Steam = true;
                        break;
                    case "-shuraSteamGuest":
                        options.SteamGuest = true;
                        options.Steam = true;
                        break;
                    case "-shuraNoBrowser":
                        options.NoBrowser = true;
                        break;
                    case "-shuraJoin":
                        if (i + 1 < args.Length) options.JoinAddress = args[++i];
                        break;
                    case "-shuraTutorial":
                        options.Tutorial = true;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int lesson))
                        {
                            options.TutorialStep = lesson;
                            i++;
                        }
                        break;
                    case "-shuraSoul":
                        if (i + 1 < args.Length) options.Soul = args[++i];
                        break;
                    case "-shuraStance":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int stance)) options.Stance = Mathf.Clamp(stance, 0, 3);
                        i++;
                        break;
                    case "-shuraWeather":
                        if (i + 1 < args.Length) options.Weather = args[++i];
                        break;
                    case "-shuraStartDepth":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int start)) options.StartDepth = start;
                        i++;
                        break;
                    case "-shuraWeapons":
                        if (i + 1 < args.Length)
                        {
                            string[] pair = args[i + 1].Split(',');
                            options.WeaponA = pair[0];
                            if (pair.Length > 1) options.WeaponB = pair[1];
                        }
                        i++;
                        break;
                    case "-shuraDepth":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int depth)) options.TargetDepth = depth;
                        i++;
                        break;
                    case "-shuraHost":
                        options.Host = true;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int wait))
                        {
                            options.HostWait = wait;
                            i++;
                        }
                        break;
                    case "-shuraPort":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port)) options.Port = port;
                        i++;
                        break;
                    case "-shuraSeed":
                        if (i + 1 < args.Length && ulong.TryParse(args[i + 1], out ulong seed)) options.Seed = seed;
                        i++;
                        break;
                }
            }
            return options;
        }
    }
}
