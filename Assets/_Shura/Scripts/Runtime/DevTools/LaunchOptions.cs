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
