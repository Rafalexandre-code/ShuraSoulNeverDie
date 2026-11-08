using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Pixel-art icons of the battle HUD, painted in code in the pack's style (16 px per tile,
    /// dark outline, one highlight): the healing gourd (hyōtan) full and empty, a bandage roll,
    /// spirit papers (ofuda), the Mon coin, the tools that have no sprite in the pack, and a
    /// paper doll whose regions light up with wounds. The browser client paints the same grids.
    /// </summary>
    public static class HudArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, string[] rows, Dictionary<char, Color> pal)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            s = PixelArt.FromGrid("hud_" + key, rows, pal, new Vector2(0.5f, 0.5f));
            Cache[key] = s;
            return s;
        }

        // ── The healing gourd ────────────────────────────────────────────────────────

        public static readonly string[] GourdRows =
        {
            ".....ccc.....",
            "....cCCCc....",
            ".....ooo.....",
            "....obbbo....",
            "...obhbbbo...",
            "...obhbbbo...",
            "...obbbbso...",
            "....obbso....",
            ".....rrr.....",
            "....rRrRr....",
            "....obbbo....",
            "...obhbbbo...",
            "..obhbbbbbo..",
            ".obhhbbbbbso.",
            ".obhbbbbbbso.",
            ".obbbbbbbsso.",
            "..obbbbbsso..",
            "...osssssoo..",
            "....ooooo....",
        };

        public static Sprite Gourd(bool full)
        {
            var pal = new Dictionary<char, Color>
            {
                { 'o', ColorUtil.Hex(full ? "#3A1F0E" : "#211B17") },
                { 'b', ColorUtil.Hex(full ? "#D9963A" : "#5E5146") },
                { 'h', ColorUtil.Hex(full ? "#F6D38A" : "#7A6C5E") },
                { 's', ColorUtil.Hex(full ? "#A8601F" : "#3D342C") },
                { 'r', ColorUtil.Hex(full ? "#B3261E" : "#5A2A26") },
                { 'R', ColorUtil.Hex(full ? "#E24B3B" : "#6E3530") },
                { 'c', ColorUtil.Hex("#6B4A2B") },
                { 'C', ColorUtil.Hex("#8A6A44") },
            };
            return Make(full ? "gourd_full" : "gourd_empty", GourdRows, pal);
        }

        // ── Bandage, papers, coin ────────────────────────────────────────────────────

        public static readonly string[] BandageRows =
        {
            "....oooooo....",
            "..oowwWWwwoo..",
            ".owWWwwwwWWwo.",
            "owWwwooooowWwo",
            "owWwo....owWwo",
            "owWwo....owWwo",
            "owWwwooooowWwo",
            ".owWWwwwwWWwo.",
            "..oowwwwwwoorr",
            "....oooooo.rRr",
            "...........rr.",
        };

        public static Sprite Bandage() => Make("bandage", BandageRows, new Dictionary<char, Color>
        {
            { 'o', ColorUtil.Hex("#5E5146") }, { 'w', ColorUtil.Hex("#E8DFCC") }, { 'W', ColorUtil.Hex("#FFFFFF") },
            { 'r', ColorUtil.Hex("#B3261E") }, { 'R', ColorUtil.Hex("#E24B3B") },
        });

        public static readonly string[] OfudaRows =
        {
            "oooooooo",
            "oppppppo",
            "oprrrrpo",
            "opppppPo",
            "oprprrpo",
            "oppprppo",
            "oprrrppo",
            "opppppPo",
            "opprrppo",
            "oprpprpo",
            "opprrppo",
            "oppppPpo",
            "opppppPo",
            "oooooooo",
        };

        public static Sprite Ofuda() => Make("ofuda", OfudaRows, new Dictionary<char, Color>
        {
            { 'o', ColorUtil.Hex("#8C7A5C") }, { 'p', ColorUtil.Hex("#F1E8D6") }, { 'P', ColorUtil.Hex("#D9CCB0") },
            { 'r', ColorUtil.Hex("#B3261E") },
        });

        public static readonly string[] CoinRows =
        {
            "...oooooo...",
            "..oggGGggo..",
            ".ogGGggggso.",
            "ogGgoooogsso",
            "ogGgo..ogsso",
            "oggo....oggo",
            "oggo....oggo",
            "osgo....osgo",
            "osggo..ogsgo",
            ".osgooooggo.",
            "..ossgggso..",
            "...oooooo...",
        };

        public static Sprite Coin() => Make("coin", CoinRows, new Dictionary<char, Color>
        {
            { 'o', ColorUtil.Hex("#5C3A0A") }, { 'g', ColorUtil.Hex("#D9A13A") }, { 'G', ColorUtil.Hex("#FBE29A") },
            { 's', ColorUtil.Hex("#A8701F") },
        });

        // ── Tools without a sprite in the pack ───────────────────────────────────────

        static readonly string[] BellRows =
        {
            ".....oo.....",
            "....oyyo....",
            "....oyyo....",
            "...oyYyyo...",
            "..oyYyyyyo..",
            "..oyYyyyyo..",
            "..oyYyyyso..",
            ".oyYyyyysso.",
            "oyYyyyyyysso",
            "oooooooooooo",
            ".....oo.....",
        };

        static readonly string[] HookRows =
        {
            "..ll..ll....",
            ".l..ll..l...",
            "..ll..ll.l..",
            ".........l..",
            "..........l.",
            "...oo.....l.",
            "..oSSo....l.",
            ".oSo.oo..l..",
            ".oSo..oSSo..",
            "..oSo.oSo...",
            "...oSSSo....",
            "....ooo.....",
        };

        static readonly string[] OilRows =
        {
            "....cc....",
            "....oo....",
            "...oggo...",
            "...oggo...",
            "..oggGgo..",
            ".oyyyyyyo.",
            "oyYyyyyyso",
            "oyYyyyyyso",
            "oyyyyyysso",
            ".osssssso.",
            "..oooooo..",
        };

        public static Sprite Tool(ToolKind kind)
        {
            switch (kind)
            {
                case ToolKind.Shuriken: return PixelArt.ProjectileSprite("fx_shuriken");
                case ToolKind.Smoke:
                {
                    Sprite[] strip = PixelArt.Strip("fx_smoke", 32, 32, new Vector2(0.5f, 0.5f));
                    return strip != null && strip.Length > 2 ? strip[2] : null;
                }
                case ToolKind.Bell:
                    return Make("bell", BellRows, new Dictionary<char, Color>
                    {
                        { 'o', ColorUtil.Hex("#3D2A0E") }, { 'y', ColorUtil.Hex("#C9902E") }, { 'Y', ColorUtil.Hex("#F2C14E") }, { 's', ColorUtil.Hex("#8A5A1A") },
                    });
                case ToolKind.Firecracker: return PixelArt.Image("item_bomb");
                case ToolKind.Oil:
                    return Make("oil", OilRows, new Dictionary<char, Color>
                    {
                        { 'o', ColorUtil.Hex("#2A1A10") }, { 'c', ColorUtil.Hex("#8A6A44") }, { 'g', ColorUtil.Hex("#B8C4C8") }, { 'G', ColorUtil.Hex("#FFFFFF") },
                        { 'y', ColorUtil.Hex("#E08A2B") }, { 'Y', ColorUtil.Hex("#FFD27A") }, { 's', ColorUtil.Hex("#A8501A") },
                    });
                default:
                    return Make("hook", HookRows, new Dictionary<char, Color>
                    {
                        { 'o', ColorUtil.Hex("#2A2A30") }, { 'S', ColorUtil.Hex("#C8C2B8") }, { 'l', ColorUtil.Hex("#8C8597") },
                    });
            }
        }

        // ── The paper doll ───────────────────────────────────────────────────────────

        public static readonly string[] DollRows =
        {
            "....HHHH....",
            "...HHHHHH...",
            "...HEHHEH...",
            "...HHHHHH...",
            "....HHHH....",
            ".....HH.....",
            "..AATTTTAA..",
            ".AAATTTTAAA.",
            ".AA.TTTT.AA.",
            ".AA.TTTT.AA.",
            ".AA.TTTT.AA.",
            ".AA.TTTT.AA.",
            "AA..TTTT..AA",
            "AA..TTTT..AA",
            "....LLLL....",
            "...LL..LL...",
            "...LL..LL...",
            "...LL..LL...",
            "...LL..LL...",
            "...LL..LL...",
            "..LLL..LLL..",
        };

        /// <summary>One region of the doll (H head, E eyes, T torso, A arms, L legs), white, to be tinted.</summary>
        public static Sprite DollPart(char region)
        {
            var rows = new string[DollRows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                char[] line = DollRows[i].ToCharArray();
                for (int k = 0; k < line.Length; k++)
                {
                    if (line[k] != region) line[k] = '.';
                }
                rows[i] = new string(line);
            }
            return Make("doll_" + region, rows, new Dictionary<char, Color> { { region, Color.white } });
        }
    }
}
