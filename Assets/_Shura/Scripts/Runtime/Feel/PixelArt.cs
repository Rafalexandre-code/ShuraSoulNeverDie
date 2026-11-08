using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Facing used to pick a character's frames.</summary>
    public enum ArtDir
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>
    /// A character in four directions: walk loop, idle, strike pose and a fallen frame, plus
    /// white silhouettes of each for hit flashes and wind-up glows.
    /// </summary>
    public sealed class CharacterArt
    {
        public Sprite[][] walk = new Sprite[4][];
        public Sprite[][] walkWhite = new Sprite[4][];
        public Sprite[] idle = new Sprite[4];
        public Sprite[] idleWhite = new Sprite[4];
        public Sprite[] attack = new Sprite[4];
        public Sprite[] attackWhite = new Sprite[4];
        public Sprite dead;
        public Sprite deadWhite;
        /// <summary>Height of the visible pixels in world units at scale 1 (empty rows excluded).</summary>
        public float visibleHeight;

        public static ArtDir DirOf(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y) * 0.9f) return facing.x < 0f ? ArtDir.Left : ArtDir.Right;
            return facing.y > 0f ? ArtDir.Up : ArtDir.Down;
        }
    }

    /// <summary>
    /// Pixel art for every entity, from Pixel-Boy &amp; AAA's "Ninja Adventure" pack (CC0) in
    /// Resources/Art/NA: samurai, ninjas, monks and oni in four directions, their weapons,
    /// temple floors, walls, banners and lanterns. A few props the pack lacks (gore chunks,
    /// chain links, item icons) are hand-authored here as pixel grids in the same style. One
    /// world unit = 16 art pixels everywhere.
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 16;
        const string Root = "Art/NA/";
        const int Cell = 16;

        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, CharacterArt> Characters = new Dictionary<string, CharacterArt>();

        // ── Loading ─────────────────────────────────────────────────────────────────────

        /// <summary>Readable, point-filtered texture, or null when missing.</summary>
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

        /// <summary>
        /// A region of a sheet, in tiles counted from the sheet's top-left (as the pack's
        /// previews are laid out). Pivot is relative to the region.
        /// </summary>
        public static Sprite Region(string sheet, int tileX, int tileY, int tilesW, int tilesH, Vector2 pivot)
        {
            string key = sheet + "#" + tileX + "," + tileY + "," + tilesW + "," + tilesH + "@" + pivot.x.ToString("0.##") + "," + pivot.y.ToString("0.##");
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture(sheet);
            Sprite s = null;
            if (tex != null)
            {
                var rect = new Rect(tileX * Cell, tex.height - (tileY + tilesH) * Cell, tilesW * Cell, tilesH * Cell);
                s = UnityEngine.Sprite.Create(tex, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            Sprites[key] = s;
            return s;
        }

        /// <summary>A whole small image (bomb, talisman) as one centred sprite.</summary>
        public static Sprite Image(string name, float pixelsPerUnit = PixelsPerUnit)
        {
            string key = name + "#image@" + pixelsPerUnit;
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture(name);
            Sprite s = tex != null
                ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect)
                : null;
            Sprites[key] = s;
            return s;
        }

        /// <summary>
        /// A thrown weapon drawn pointing along +X: shuriken sheets hold two 16 px frames (the
        /// first is used, the projectile spins), kunai are a single image.
        /// </summary>
        public static Sprite ProjectileSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!name.Contains("shuriken")) return Image(name, 18f);
            string key = name + "#projectile";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture(name);
            Sprite s = tex != null
                ? UnityEngine.Sprite.Create(tex, new Rect(0, tex.height - Cell, Cell, Cell), new Vector2(0.5f, 0.5f), 22f, 0, SpriteMeshType.FullRect)
                : null;
            Sprites[key] = s;
            return s;
        }

        /// <summary>A horizontal strip of animation frames of any size (effects sheets).</summary>
        public static Sprite[] Strip(string sheet, int frameW, int frameH, Vector2 pivot)
        {
            Texture2D tex = Texture(sheet);
            if (tex == null) return new Sprite[0];
            int count = Mathf.Max(1, tex.width / frameW);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = UnityEngine.Sprite.Create(tex, new Rect(i * frameW, tex.height - frameH, frameW, frameH), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return frames;
        }

        // ── Characters ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads "{id}_walk" (4 columns = down, up, left, right; 4 rows = frames), "{id}_idle"
        /// and "{id}_attack" (one frame per direction) and "{id}_dead". Pivot on the feet.
        /// </summary>
        public static CharacterArt Character(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Characters.TryGetValue(id, out CharacterArt cached)) return cached;

            Texture2D walk = Texture(id + "_walk");
            Texture2D idle = Texture(id + "_idle");
            if (walk == null || idle == null)
            {
                Characters[id] = null;
                return null;
            }
            Texture2D attack = Texture(id + "_attack") ?? idle;
            Texture2D dead = Texture(id + "_dead");

            int bottom = EmptyRowsBottom(idle, new RectInt(0, 0, Cell, Cell));
            int top = EmptyRowsTop(idle, new RectInt(0, 0, Cell, Cell));
            var pivot = new Vector2(0.5f, bottom / (float)Cell);
            var art = new CharacterArt { visibleHeight = (Cell - top - bottom) / (float)PixelsPerUnit };
            Texture2D walkWhite = Silhouette(walk);
            Texture2D idleWhite = Silhouette(idle);
            Texture2D attackWhite = Silhouette(attack);
            int rows = walk.height / Cell;
            for (int d = 0; d < 4; d++)
            {
                art.walk[d] = new Sprite[rows];
                art.walkWhite[d] = new Sprite[rows];
                for (int f = 0; f < rows; f++)
                {
                    art.walk[d][f] = Cut(walk, d, f, pivot);
                    art.walkWhite[d][f] = Cut(walkWhite, d, f, pivot);
                }
                art.idle[d] = Cut(idle, d, 0, pivot);
                art.idleWhite[d] = Cut(idleWhite, d, 0, pivot);
                art.attack[d] = Cut(attack, d, 0, pivot);
                art.attackWhite[d] = Cut(attackWhite, d, 0, pivot);
            }
            if (dead != null)
            {
                art.dead = Cut(dead, 0, 0, pivot);
                art.deadWhite = Cut(Silhouette(dead), 0, 0, pivot);
            }
            Characters[id] = art;
            return art;
        }

        /// <summary>Cell (column, row-from-top) of a 16 px sheet.</summary>
        static Sprite Cut(Texture2D sheet, int column, int row, Vector2 pivot)
        {
            var rect = new Rect(column * Cell, sheet.height - (row + 1) * Cell, Cell, Cell);
            return UnityEngine.Sprite.Create(sheet, rect, pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>The sheet's shape in pure white: drawn on top and faded for flashes and glows.</summary>
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

        static int EmptyRowsBottom(Texture2D t, RectInt r)
        {
            if (!t.isReadable) return 0;
            for (int y = r.yMin; y < r.yMax; y++)
            {
                for (int x = r.xMin; x < r.xMax; x++) if (t.GetPixel(x, y).a > 0.03f) return y - r.yMin;
            }
            return 0;
        }

        static int EmptyRowsTop(Texture2D t, RectInt r)
        {
            if (!t.isReadable) return 0;
            for (int y = r.yMax - 1; y >= r.yMin; y--)
            {
                for (int x = r.xMin; x < r.xMax; x++) if (t.GetPixel(x, y).a > 0.03f) return r.yMax - 1 - y;
            }
            return 0;
        }

        // ── Weapons ─────────────────────────────────────────────────────────────────────

        /// <summary>Where the hand holds each weapon, as a fraction of its length from the pommel.</summary>
        static float Grip(string name)
        {
            switch (name)
            {
                case "weapon_Lance":
                case "weapon_Lance2":
                case "weapon_Stick":
                    return 0.3f;
                case "weapon_Club":
                case "weapon_Hammer":
                case "weapon_AxeTool":
                    return 0.25f;
                default:
                    return 0.18f;
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
            Texture2D rotated = Rotate(Texture(name), true, key);
            Sprite s = rotated != null
                ? UnityEngine.Sprite.Create(rotated, new Rect(0, 0, rotated.width, rotated.height), new Vector2(Grip(name), 0.5f), rotated.width, 0, SpriteMeshType.FullRect)
                : null;
            Sprites[key] = s;
            return s;
        }

        /// <summary>
        /// Held, not swung from a hilt (bows): centred, 1 unit tall, turned so the belly of the
        /// bow faces +X (the pack draws it lying down, belly toward the bottom).
        /// </summary>
        public static Sprite WeaponCentered(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = name + "#centered";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D rotated = Rotate(Texture(name), false, key);
            Sprite s = rotated != null
                ? UnityEngine.Sprite.Create(rotated, new Rect(0, 0, rotated.width, rotated.height), new Vector2(0.5f, 0.5f), rotated.height, 0, SpriteMeshType.FullRect)
                : null;
            Sprites[key] = s;
            return s;
        }

        /// <summary>
        /// Arrows are drawn on the diagonal (tip up-right). The sprite keeps that and its pivot
        /// on the tail; rotate it by <see cref="ArrowAngleOffset"/> so the tip leads.
        /// </summary>
        public static Sprite Arrow()
        {
            const string key = "weapon_Arrow#arrow";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;
            Texture2D tex = Texture("weapon_Arrow");
            // Diagonal length = 1 unit.
            Sprite s = tex != null
                ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.06f, 0.06f), tex.width * 1.3f, 0, SpriteMeshType.FullRect)
                : null;
            Sprites[key] = s;
            return s;
        }

        public const float ArrowAngleOffset = -45f;

        /// <summary>Quarter turn of a sprite's pixels: clockwise (up → right) or counter-clockwise (down → right).</summary>
        static Texture2D Rotate(Texture2D src, bool clockwise, string name)
        {
            if (src == null || !src.isReadable) return null;
            int w = src.width;
            int h = src.height;
            Texture2D tex = NewTexture(h, w, name);
            Color32[] from = src.GetPixels32();
            var to = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int nx = clockwise ? y : h - 1 - y;
                    int ny = clockwise ? w - 1 - x : x;
                    to[ny * h + nx] = from[y * w + x];
                }
            }
            tex.SetPixels32(to);
            tex.Apply(false, false);
            return tex;
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
