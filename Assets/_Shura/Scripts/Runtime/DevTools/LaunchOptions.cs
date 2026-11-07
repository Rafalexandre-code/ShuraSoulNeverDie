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
