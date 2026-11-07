using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Idle/run/hit frames of one character, plus white silhouettes for hit flashes.</summary>
    public sealed class CharacterArt
    {
        public Sprite[] idle;
        public Sprite[] run;
        public Sprite[] hit;
        public Sprite[] idleWhite;
        public Sprite[] runWhite;
        public Sprite[] hitWhite;
        /// <summary>Height of the visible pixels in world units at scale 1 (empty rows excluded).</summary>
        public float visibleHeight;

        public bool HasHit => hit != null && hit.Length > 0;
    }

    /// <summary>
    /// Pixel art for every entity. Characters, weapons and tiles come from 0x72's
    /// "16x16 DungeonTileset II" (CC0) in Resources/Art/0x72. A few props the pack lacks
    /// (brazier, flames, gore chunks, chain, item icons) are hand-authored here as pixel grids
    /// in the same palette. One world unit = 16 art pixels, so characters, tiles and props share
    /// one pixel density.
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 16;
        const string Root = "Art/0x72/";

        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, CharacterArt> Characters = new Dictionary<string, CharacterArt>();

        // ── Loading ─────────────────────────────────────────────────────────────────────

        /// <summary>Readable, point-filtered texture of one frame, or null when missing.</summary>
        public static Texture2D Texture(string name)
        {
            if (Textures.TryGetValue(name, out Texture2D cached)) return cached;
            var tex = Resources.Load<Texture2D>(Root + name);
            if (tex != null)
            {
                tex = MakeReadable(tex);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            Textures[name] = tex;
            return tex;
        }

        /// <summary>The import rules make these textures readable; this is the safety net.</summary>
        static Texture2D MakeReadable(Texture2D tex)
        {
            if (tex.isReadable) return tex;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return tex;
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false) { name = tex.name };
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        public static Sprite Sprite(string name, Vector2 pivot)
        {
            string key = name + "@" + pivot.x.ToString("0.###") + "," + pivot.y.ToString("0.###");
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture(name);
            Sprite s = tex != null ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect) : null;
            Sprites[key] = s;
            return s;
        }

        // ── Characters ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads "{id}_idle_anim_f0..", "{id}_run_anim_f0..", "{id}_hit_anim_f0" (creatures with a
        /// single loop use "{id}_anim_f0.." for both). Pivot sits on the feet.
        /// </summary>
        public static CharacterArt Character(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Characters.TryGetValue(id, out CharacterArt cached)) return cached;

            List<Texture2D> idle = Frames(id + "_idle_anim_f");
            List<Texture2D> run = Frames(id + "_run_anim_f");
            List<Texture2D> hit = Frames(id + "_hit_anim_f");
            if (idle.Count == 0) idle = Frames(id + "_anim_f");
            if (run.Count == 0) run = idle;
            if (idle.Count == 0)
            {
                Characters[id] = null;
                return null;
            }

            Texture2D first = idle[0];
            int bottom = EmptyRowsBottom(first);
            int top = EmptyRowsTop(first);
            var pivot = new Vector2(0.5f, bottom / (float)first.height);
            var art = new CharacterArt
            {
                idle = ToSprites(idle, pivot, false),
                run = ToSprites(run, pivot, false),
                hit = ToSprites(hit, pivot, false),
                idleWhite = ToSprites(idle, pivot, true),
                runWhite = ToSprites(run, pivot, true),
                hitWhite = ToSprites(hit, pivot, true),
                visibleHeight = (first.height - top - bottom) / (float)PixelsPerUnit,
            };
            Characters[id] = art;
            return art;
        }

        static List<Texture2D> Frames(string prefix)
        {
            var list = new List<Texture2D>();
            for (int i = 0; i < 8; i++)
            {
                Texture2D t = Texture(prefix + i);
                if (t == null) break;
                list.Add(t);
            }
            return list;
        }

        static Sprite[] ToSprites(List<Texture2D> frames, Vector2 pivot, bool white)
        {
            var result = new Sprite[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                Texture2D t = white ? Silhouette(frames[i]) : frames[i];
                result[i] = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return result;
        }

        /// <summary>The frame's shape in pure white: drawn on top and faded for hit flashes and wind-up heat.</summary>
        static Texture2D Silhouette(Texture2D source)
        {
            string key = source.name + "#white";
            if (Textures.TryGetValue(key, out Texture2D cached)) return cached;
            var tex = NewTexture(source.width, source.height, key);
            if (source.isReadable)
            {
                Color32[] px = source.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a > 8 ? (byte)255 : (byte)0);
                tex.SetPixels32(px);
                tex.Apply(false, false);
            }
            Textures[key] = tex;
            return tex;
        }

        static int EmptyRowsBottom(Texture2D t)
        {
            if (!t.isReadable) return 0;
            Color32[] px = t.GetPixels32();
            for (int y = 0; y < t.height; y++)
            {
                for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 8) return y;
            }
            return 0;
        }

        static int EmptyRowsTop(Texture2D t)
        {
            if (!t.isReadable) return 0;
            Color32[] px = t.GetPixels32();
            for (int y = t.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < t.width; x++) if (px[y * t.width + x].a > 8) return t.height - 1 - y;
            }
            return 0;
        }

        // ── Weapons ─────────────────────────────────────────────────────────────────────

        /// <summary>Where the hand holds each weapon, as a fraction of its length from the pommel.</summary>
        static float Grip(string name)
        {
            switch (name)
            {
                case "weapon_spear":
                case "weapon_red_magic_staff":
                case "weapon_green_magic_staff":
                    return 0.28f;
                case "weapon_waraxe":
                case "weapon_axe":
                case "weapon_mace":
                case "weapon_hammer":
                case "weapon_big_hammer":
                case "weapon_baton_with_spikes":
                case "weapon_double_axe":
                    return 0.22f;
                case "weapon_cleaver":
                case "weapon_knife":
                    return 0.25f;
                case "weapon_arrow":
                    return 0f;
                default:
                    return 0.12f;
            }
        }

        /// <summary>
        /// A weapon laid along +X with its pivot in the hand and its full length = 1 unit, the
        /// convention the weapon rig expects. The pack draws weapons pointing up, so the pixels
        /// are rotated a quarter turn clockwise.
        /// </summary>
        public static Sprite Weapon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = name + "#weapon";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D src = Texture(name);
            Sprite s = null;
            if (src != null && src.isReadable)
            {
                int w = src.width;
                int h = src.height;
                Texture2D tex = NewTexture(h, w, key);
                Color32[] from = src.GetPixels32();
                var to = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Up becomes right; the blade's left edge becomes its top edge.
                        int nx = y;
                        int ny = w - 1 - x;
                        to[ny * h + nx] = from[y * w + x];
                    }
                }
                tex.SetPixels32(to);
                tex.Apply(false, false);
                s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, h, w), new Vector2(Grip(name), 0.5f), h, 0, SpriteMeshType.FullRect);
            }
            Sprites[key] = s;
            return s;
        }

        /// <summary>Held, not swung from a hilt (bows): centred, as drawn, 1 unit tall.</summary>
        public static Sprite WeaponCentered(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = name + "#centered";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture(name);
            Sprite s = tex != null ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height, 0, SpriteMeshType.FullRect) : null;
            Sprites[key] = s;
            return s;
        }

        // ── Hand-authored pixel props ───────────────────────────────────────────────────

        static readonly Color Outline = ColorUtil.Hex("#1A1420");

        /// <summary>Builds a sprite from rows of characters (top row first); '.' is transparent.</summary>
        public static Sprite FromGrid(string key, string[] rows, Dictionary<char, Color> palette, Vector2 pivot, float ppu = PixelsPerUnit, SpriteMeshType mesh = SpriteMeshType.FullRect)
        {
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            int h = rows.Length;
            int w = 0;
            foreach (string r in rows) w = Mathf.Max(w, r.Length);
            Texture2D tex = NewTexture(w, h, key);
            var px = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                string line = rows[row];
                int y = h - 1 - row;
                for (int x = 0; x < w; x++)
                {
                    char c = x < line.Length ? line[x] : '.';
                    px[y * w + x] = c != '.' && palette.TryGetValue(c, out Color col) ? (Color32)col : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Sprite s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, mesh);
            Sprites[key] = s;
            return s;
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
        }

        static readonly string[] BrazierRows =
        {
            ".oooooooooooo.",
            "obbbbbbbbbbbbo",
            "oddddddddddddo",
            ".ommmmmmmmmmo.",
            ".odmmmhmmmmdo.",
            "..odmmmmmmdo..",
            "...oddddddo...",
            "....oommoo....",
            ".....omdo.....",
            "....ommmdo....",
            "...omo..odo...",
            "..omo....odo..",
            "..oo......oo..",
        };

        /// <summary>Iron fire bowl on a tripod; the flames sit on top.</summary>
        public static Sprite Brazier()
        {
            var pal = new Dictionary<char, Color>
            {
                { 'o', Outline }, { 'b', ColorUtil.Hex("#3A2A22") }, { 'd', ColorUtil.Hex("#3B3644") },
                { 'm', ColorUtil.Hex("#5C5566") }, { 'h', ColorUtil.Hex("#8A8396") },
            };
            return FromGrid("brazier", BrazierRows, pal, new Vector2(0.5f, 0.15f));
        }

        static readonly string[][] FlameRows =
        {
            new[]
            {
                "....o.....",
                "...ofo....",
                "...off.o..",
                "..offfofo.",
                "..offyffo.",
                ".offyyyfo.",
                ".ofyywyfo.",
                ".ofywwyfo.",
                "..ofyyfo..",
                "...oooo...",
            },
            new[]
            {
                ".....o....",
                "....ofo...",
                ".o..off...",
                "ofo.offo..",
                "offoffyfo.",
                ".ofyyyyfo.",
                ".ofywwyfo.",
                ".ofywwyfo.",
                "..ofyyfo..",
                "...oooo...",
            },
            new[]
            {
                "...o......",
                "..ofo.....",
                "..offo..o.",
                ".offfo.ofo",
                ".offyfoffo",
                ".ofyyyyfo.",
                ".ofywwyfo.",
                ".ofywwyfo.",
                "..ofyyfo..",
                "...oooo...",
            },
            new[]
            {
                "..........",
                "....oo....",
                "...offo...",
                "..offfo...",
                "..offyfo..",
                ".offyyyfo.",
                ".ofywwyfo.",
                ".ofywwyfo.",
                "..ofyyfo..",
                "...oooo...",
            },
        };

        /// <summary>Four flickering flame frames in the biome's fire colour.</summary>
        public static Sprite[] Flames(Color flame)
        {
            var pal = new Dictionary<char, Color>
            {
                { 'o', Color.Lerp(flame, Color.black, 0.55f) }, { 'f', flame },
                { 'y', Color.Lerp(flame, ColorUtil.Hex("#FFE9A0"), 0.6f) }, { 'w', ColorUtil.Hex("#FFF8E6") },
            };
            string tint = ColorUtility.ToHtmlStringRGB(flame);
            var frames = new Sprite[FlameRows.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = FromGrid("flame_" + tint + "_" + i, FlameRows[i], pal, new Vector2(0.5f, 0f));
            return frames;
        }

        static readonly string[][] GoreRows =
        {
            new[] { ".oo..", "oflo.", "offfo", ".ofdo", "..oo." },
            new[] { "..oo.", ".olfo", "offdo", "ofdo.", ".oo.." },
            new[] { ".ooo.", "oflfo", "obbfo", ".obo.", "..o.." },
            new[] { "oo...", "ofo..", "offoo", ".ofdo", "..oo." },
        };

        /// <summary>Small chunks for finishers; the body sprite itself is used for the big pieces.</summary>
        public static Sprite GoreChunk(int variant)
        {
            var pal = new Dictionary<char, Color>
            {
                { 'o', ColorUtil.Hex("#2A0A10") }, { 'f', ColorUtil.Hex("#9C2A35") }, { 'l', ColorUtil.Hex("#D9707A") },
                { 'd', ColorUtil.Hex("#5E1520") }, { 'b', ColorUtil.Hex("#E8D8C8") },
            };
            int v = ((variant % GoreRows.Length) + GoreRows.Length) % GoreRows.Length;
            // 5 px drawn 1 unit wide: the gib code scales it by the chunk size.
            return FromGrid("gore_" + v, GoreRows[v], pal, new Vector2(0.5f, 0.5f), 5f);
        }

        /// <summary>One chain link, tiled along the kusarigama's chain.</summary>
        public static Sprite ChainLink()
        {
            var pal = new Dictionary<char, Color>
            {
                { 'o', Outline }, { 'm', ColorUtil.Hex("#8C8A86") }, { 'h', ColorUtil.Hex("#C9C6C0") },
            };
            string[] rows = { ".oo.oo", "ohmomh", ".oo.oo" };
            return FromGrid("chain_link", rows, pal, new Vector2(0f, 0.5f), PixelsPerUnit * 1.5f, SpriteMeshType.FullRect);
        }

        static readonly Dictionary<ItemSlot, string[]> ItemRows = new Dictionary<ItemSlot, string[]>
        {
            {
                ItemSlot.Amulet, new[]
                {
                    "..o.......o.",
                    "..oc.....co.",
                    "...oc...co..",
                    "....oc.co...",
                    ".....ooo....",
                    "....ommmo...",
                    "...omgggmo..",
                    "..omgwgggmo.",
                    "..omgggggmo.",
                    "...omgggmo..",
                    "....ommmo...",
                    ".....ooo....",
                }
            },
            {
                ItemSlot.Ring, new[]
                {
                    "....oooo....",
                    "...ogwggo...",
                    "...ogggg o..",
                    "....oggo....",
                    "...ommmmo...",
                    "..omhoohmo..",
                    ".omo....omo.",
                    ".omo....omo.",
                    ".omo....omo.",
                    "..omoooomo..",
                    "...ommmmo...",
                    "....oooo....",
                }
            },
            {
                ItemSlot.Bracer, new[]
                {
                    "............",
                    ".oooooooooo.",
                    ".ohhhhhhhho.",
                    ".ommmmmmmmo.",
                    ".omcmoomcmo.",
                    ".omcogwocmo.",
                    ".omcoggocmo.",
                    ".omcmoomcmo.",
                    ".ommmmmmmmo.",
                    ".occcccccco.",
                    ".oooooooooo.",
                    "............",
                }
            },
            {
                ItemSlot.Talisman, new[]
                {
                    "...oooooo...",
                    "...ocpppo...",
                    "...opppco...",
                    "...opoopo...",
                    "...ooggoo...",
                    "...ogwggo...",
                    "...ogggg o..",
                    "...opoopo...",
                    "...opppco...",
                    "...ocpppo...",
                    "...oooooo...",
                    ".....oo.....",
                }
            },
        };

        /// <summary>Item art: the slot's object in rarity-tinted metal with an element-coloured stone.</summary>
        public static Sprite ItemIcon(ItemSlot slot, Element element, Rarity rarity)
        {
            string key = "item_" + slot + "_" + element + "_" + (int)rarity;
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Color metal = Color.Lerp(ColorUtil.Hex("#8C8A86"), Rarities.Color(rarity), 0.55f);
            Color gem = element == Element.None ? Rarities.Color(rarity) : Elements.Color(element);
            var pal = new Dictionary<char, Color>
            {
                { 'o', Outline }, { 'm', metal }, { 'h', Color.Lerp(metal, Color.white, 0.45f) },
                { 'c', Color.Lerp(metal, Color.black, 0.35f) }, { 'g', gem }, { 'w', Color.Lerp(gem, Color.white, 0.75f) },
                { 'p', ColorUtil.Hex("#E9DFC8") },
            };
            string[] rows = ItemRows.TryGetValue(slot, out string[] r) ? r : ItemRows[ItemSlot.Talisman];
            // Typos in grids (spaces) read as transparent.
            var clean = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++) clean[i] = rows[i].Replace(' ', '.');
            return FromGrid(key, clean, pal, new Vector2(0.5f, 0.5f), 12f);
        }
    }
}
