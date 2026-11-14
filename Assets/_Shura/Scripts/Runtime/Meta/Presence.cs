using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>An ink message left on the floor, built from a fixed vocabulary (§9.5).</summary>
    [Serializable]
    public sealed class InkMessage
    {
        public string id = "";
        public string author = "";
        public int depth;
        public float x, y;
        public int template;
        public int word;
        /// <summary>How many readers found it good (only the author's own copy counts).</summary>
        public int rating;

        public string Text => Presence.Phrase(template, word);
    }

    /// <summary>A bloodstain: where a duelist fell, and the last five seconds of their steps.</summary>
    [Serializable]
    public sealed class DeathStain
    {
        public string id = "";
        public string author = "";
        public int depth;
        public float x, y;
        public string cause = "";
        public int art;
        /// <summary>x, y pairs, six a second, oldest first.</summary>
        public List<float> trail = new List<float>();
    }

    /// <summary>What a friend's spirit fights with: their blades, their look and their habits.</summary>
    [Serializable]
    public sealed class SpiritProfile
    {
        public string name = "";
        public string weaponA = "katana";
        public string weaponB = "montante";
        public int art;
        public string technique = "";
        /// <summary>0 (patient, parries a lot) .. 1 (relentless).</summary>
        public float aggression = 0.5f;
    }

    /// <summary>A duelist who became the new Ashura: the final lord of their friends' runs for a week.</summary>
    [Serializable]
    public sealed class AshuraMark
    {
        public string name = "";
        public string weapon = "katana";
        public int art;
        public string technique = "";
        /// <summary>UTC ticks of the day it happened.</summary>
        public long since;
    }

    /// <summary>Everything one duelist shares with the friends they play with.</summary>
    [Serializable]
    public sealed class PresencePacket
    {
        public string author = "";
        public List<InkMessage> messages = new List<InkMessage>();
        public List<DeathStain> stains = new List<DeathStain>();
        public SpiritProfile spirit;
        public AshuraMark ashura;
        public List<Rival> rivals = new List<Rival>();
        /// <summary>Ids of other people's messages this duelist found good.</summary>
        public List<string> ratings = new List<string>();
        public long when;

        public void Sanitize()
        {
            author = GameSettings.CleanName(author, "Ronin");
            messages = messages ?? new List<InkMessage>();
            stains = stains ?? new List<DeathStain>();
            rivals = rivals ?? new List<Rival>();
            ratings = ratings ?? new List<string>();
            messages.RemoveAll(m => m == null);
            stains.RemoveAll(s => s == null || s.trail == null);
            rivals.RemoveAll(r => r == null);
            foreach (InkMessage m in messages)
            {
                m.template = Mathf.Clamp(m.template, 0, Presence.Templates.Length - 1);
                m.word = Mathf.Clamp(m.word, 0, Presence.Words.Length - 1);
            }
            foreach (DeathStain s in stains)
            {
                if (s.trail.Count > Presence.TrailPoints * 2) s.trail.RemoveRange(0, s.trail.Count - Presence.TrailPoints * 2);
                if (s.trail.Count % 2 == 1) s.trail.RemoveAt(0);
            }
            foreach (Rival r in rivals) r.Sanitize();
            if (messages.Count > Presence.MaxMessages) messages.RemoveRange(0, messages.Count - Presence.MaxMessages);
            if (stains.Count > Presence.MaxStains) stains.RemoveRange(0, stains.Count - Presence.MaxStains);
            if (rivals.Count > Presence.SharedRivals) rivals.RemoveRange(Presence.SharedRivals, rivals.Count - Presence.SharedRivals);
        }
    }

    /// <summary>The asynchronous presence kept in the save: yours, and what your friends left.</summary>
    [Serializable]
    public sealed class PresenceData
    {
        public List<InkMessage> mine = new List<InkMessage>();
        public List<DeathStain> myStains = new List<DeathStain>();
        public List<PresencePacket> friends = new List<PresencePacket>();
        /// <summary>Messages of others you found good, until they reach their author.</summary>
        public List<string> rated = new List<string>();
        /// <summary>Ratings already counted here ("reader|message"), so a reconnection counts nothing twice.</summary>
        public List<string> counted = new List<string>();
        public SpiritProfile spirit;
        public AshuraMark ashura;
        public int approvals;

        public void Sanitize()
        {
            mine = mine ?? new List<InkMessage>();
            myStains = myStains ?? new List<DeathStain>();
            friends = friends ?? new List<PresencePacket>();
            rated = rated ?? new List<string>();
            counted = counted ?? new List<string>();
            mine.RemoveAll(m => m == null);
            myStains.RemoveAll(s => s == null || s.trail == null);
            friends.RemoveAll(f => f == null);
            foreach (PresencePacket f in friends) f.Sanitize();
            Presence.Trim(mine, Presence.MaxMessages);
            Presence.Trim(myStains, Presence.MaxStains);
            Presence.Trim(friends, Presence.MaxFriends);
            Presence.Trim(rated, 40);
            Presence.Trim(counted, 300);
            approvals = Mathf.Max(0, approvals);
        }
    }

    /// <summary>
    /// Presence without a server (design doc §9.5): what you leave behind — ink messages,
    /// bloodstains, your spirit, your rivals, the new Ashura you became — travels to a friend
    /// whenever you play together, and stays in their save for their own runs (and theirs in yours).
    /// </summary>
    public static class Presence
    {
        public const int MaxMessages = 8;
        public const int MaxStains = 4;
        public const int MaxFriends = 8;
        public const int SharedRivals = 2;
        /// <summary>Five seconds of steps, six a second.</summary>
        public const int TrailPoints = 30;
        public const int TrailEvery = 10;
        public const int AshuraDays = 7;
        public const float SpiritChance = 0.25f;

        public static readonly string[] Templates =
        {
            "cuidado: {0}", "tente {0}", "{0} adiante", "não confie em {0}", "aqui, {0}", "lembre-se: {0}",
        };

        public static readonly string[] Words =
        {
            "agarrão", "kamae baixa", "esquiva", "aparar", "fogo", "emboscada", "golpe vermelho", "flechas",
            "paciência", "a corrente", "teias", "a lança", "pelas costas", "o chão", "fôlego", "coragem",
        };

        public static string Phrase(int template, int word)
        {
            string t = Templates[Mathf.Clamp(template, 0, Templates.Length - 1)];
            return string.Format(t, Words[Mathf.Clamp(word, 0, Words.Length - 1)]);
        }

        /// <summary>The word a death suggests ("trap" → o chão, a grab → agarrão...).</summary>
        public static int WordFor(string cause)
        {
            string c = (cause ?? "").ToLowerInvariant();
            if (c.Contains("trap")) return 13;
            if (c.Contains("agarr") || c.Contains("abraco") || c.Contains("grab")) return 0;
            if (c.Contains("corrente") || c.Contains("chain")) return 9;
            if (c.Contains("teia") || c.Contains("aranha") || c.Contains("jorogumo")) return 10;
            if (c.Contains("flecha") || c.Contains("arqueiro") || c.Contains("arrow")) return 7;
            if (c.Contains("fogo") || c.Contains("kagutsuchi") || c.Contains("chama")) return 4;
            if (c.Contains("lanc") || c.Contains("yari")) return 11;
            if (c.Contains("sombra") || c.Contains("kunoichi") || c.Contains("ninja")) return 12;
            return 6;
        }

        public static void Trim<T>(List<T> list, int max)
        {
            if (list != null && list.Count > max) list.RemoveRange(0, list.Count - max);
        }

        static string NewId(string author, int salt)
        {
            ulong h = 1469598103934665603UL ^ (ulong)DateTime.UtcNow.Ticks ^ (ulong)(uint)salt;
            foreach (char c in author ?? "") { h ^= c; h *= 1099511628211UL; }
            return (author ?? "?") + ":" + (h % 0xFFFFFFUL).ToString("x6");
        }

        // ── Your own ─────────────────────────────────────────────────────────────────

        /// <summary>You fell: a bloodstain with your last steps, and an ink message (the death suggests the word).</summary>
        public static InkMessage RecordDeath(MetaProgress meta, string author, int depth, Vector2 at, string cause, int art, IList<Vector2> trail)
        {
            if (meta == null) return null;
            PresenceData p = meta.presence;
            var stain = new DeathStain { id = NewId(author, depth), author = author, depth = depth, x = at.x, y = at.y, cause = cause ?? "", art = art };
            if (trail != null)
            {
                int start = Mathf.Max(0, trail.Count - TrailPoints);
                for (int i = start; i < trail.Count; i++)
                {
                    stain.trail.Add(Round(trail[i].x));
                    stain.trail.Add(Round(trail[i].y));
                }
            }
            p.myStains.Add(stain);
            Trim(p.myStains, MaxStains);
            var msg = new InkMessage
            {
                id = NewId(author, depth * 7 + 1), author = author, depth = depth,
                x = Round(at.x + 0.6f), y = Round(at.y - 0.4f), template = 0, word = WordFor(cause),
            };
            p.mine.Add(msg);
            Trim(p.mine, MaxMessages);
            return msg;
        }

        static float Round(float v) => Mathf.Round(v * 10f) / 10f;

        /// <summary>The run is banked: the spirit your friends will meet learns your latest way of fighting.</summary>
        public static void RecordSpirit(MetaProgress meta, string name, PlayerController host, PlayerProfile profile)
        {
            if (meta == null || host?.Build == null) return;
            var s = new SpiritProfile { name = name, art = host.ArtIndex };
            WeaponDef[] w = host.Build.Weapons;
            if (w[0] != null) s.weaponA = w[0].id;
            if (w[1] != null) s.weaponB = w[1].id;
            TechniqueDef best = null;
            foreach (RewardDef r in host.Build.Owned)
            {
                if (r is TechniqueDef t && (best == null || t.Level > best.Level)) best = t;
            }
            s.technique = best != null ? best.Id : "";
            if (profile != null)
            {
                float act = profile.AttacksPerSecond, wait = profile.PerfectsPerSecond * 2f + profile.DodgesPerSecond;
                s.aggression = act + wait > 0.01f ? Mathf.Clamp01(act / (act + wait)) : 0.5f;
            }
            meta.presence.spirit = s;
        }

        /// <summary>The "new Ashura" ending: you are your friends' final lord for a week.</summary>
        public static void RecordAshura(MetaProgress meta, string name, PlayerController host)
        {
            if (meta == null || host == null) return;
            meta.presence.ashura = new AshuraMark
            {
                name = name, weapon = host.Weapon.id, art = host.ArtIndex,
                technique = meta.presence.spirit?.technique ?? "", since = DateTime.UtcNow.Date.Ticks,
            };
        }

        // ── Shared with friends ──────────────────────────────────────────────────────

        public static PresencePacket Pack(MetaProgress meta, string author)
        {
            var pk = new PresencePacket { author = author, when = DateTime.UtcNow.Ticks };
            if (meta == null) return pk;
            PresenceData p = meta.presence;
            pk.messages.AddRange(p.mine);
            pk.stains.AddRange(p.myStains);
            pk.spirit = p.spirit;
            pk.ashura = p.ashura;
            pk.ratings.AddRange(p.rated);
            // The strongest of your own rivals (never one a friend lent you).
            var own = new List<Rival>();
            foreach (Rival r in meta.rivals)
            {
                if (string.IsNullOrEmpty(r.foreign)) own.Add(r);
            }
            own.Sort((a, b) => b.rank != a.rank ? b.rank.CompareTo(a.rank) : b.kills.CompareTo(a.kills));
            for (int i = 0; i < own.Count && i < SharedRivals; i++) pk.rivals.Add(own[i]);
            return pk;
        }

        public static string ToJson(PresencePacket pk) => pk == null ? "" : JsonUtility.ToJson(pk);

        public static PresencePacket FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var pk = JsonUtility.FromJson<PresencePacket>(json);
                pk?.Sanitize();
                return pk;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// A friend's packet arrives: it replaces what they left before; their ratings of your
        /// messages count once; up to two of their rivals move into your roster as captains.
        /// Returns how many of your messages they approved now.
        /// </summary>
        public static int Merge(MetaProgress meta, PresencePacket pk, string me)
        {
            if (meta == null || pk == null || string.IsNullOrEmpty(pk.author) || pk.author == me) return 0;
            PresenceData p = meta.presence;
            p.friends.RemoveAll(f => f.author == pk.author);
            p.friends.Add(pk);
            Trim(p.friends, MaxFriends);

            int approved = 0;
            foreach (string id in pk.ratings)
            {
                InkMessage mine = p.mine.Find(m => m.id == id);
                string key = pk.author + "|" + id;
                if (mine == null || p.counted.Contains(key)) continue;
                p.counted.Add(key);
                mine.rating++;
                p.approvals++;
                approved++;
            }
            Trim(p.counted, 300);
            // Ratings you gave to this friend's messages have reached them.
            p.rated.RemoveAll(id => id.StartsWith(pk.author + ":", StringComparison.Ordinal));

            foreach (Rival r in pk.rivals) ImportRival(meta, r, pk.author);
            return approved;
        }

        static void ImportRival(MetaProgress meta, Rival r, string friend)
        {
            if (r == null || string.IsNullOrEmpty(r.id) || Rivals.Find(meta, r.id) != null) return;
            int foreign = 0, captains = 0;
            foreach (Rival x in meta.rivals)
            {
                if (!string.IsNullOrEmpty(x.foreign)) foreign++;
                if (!x.General) captains++;
            }
            if (foreign >= SharedRivals * 2 || captains >= Rivals.MaxCaptains) return;
            var copy = JsonUtility.FromJson<Rival>(JsonUtility.ToJson(r));
            copy.foreign = friend;
            copy.rank = Mathf.Min(copy.rank, Rivals.GeneralRank - 1);
            copy.ecos = Mathf.Min(copy.ecos, 150);
            copy.guardsLost = 0;
            copy.furious = false;
            copy.Sanitize();
            meta.rivals.Add(copy);
        }

        public static void Rate(MetaProgress meta, InkMessage m)
        {
            if (meta == null || m == null || meta.presence.rated.Contains(m.id)) return;
            meta.presence.rated.Add(m.id);
            Trim(meta.presence.rated, 40);
        }

        public static bool Rated(MetaProgress meta, InkMessage m) => meta != null && m != null && meta.presence.rated.Contains(m.id);

        // ── In a run ─────────────────────────────────────────────────────────────────

        /// <summary>Friends' messages at this depth (two at most, the best rated first).</summary>
        public static List<InkMessage> MessagesAt(MetaProgress meta, int depth)
        {
            var list = new List<InkMessage>();
            if (meta == null) return list;
            foreach (PresencePacket f in meta.presence.friends)
            {
                foreach (InkMessage m in f.messages)
                {
                    if (m.depth == depth) list.Add(m);
                }
            }
            list.Sort((a, b) => b.rating.CompareTo(a.rating));
            if (list.Count > 2) list.RemoveRange(2, list.Count - 2);
            return list;
        }

        /// <summary>Bloodstains at this depth: your friends' falls, and your own lineage's (two at most).</summary>
        public static List<DeathStain> StainsAt(MetaProgress meta, int depth, string currentRunAuthor)
        {
            var list = new List<DeathStain>();
            if (meta == null) return list;
            foreach (PresencePacket f in meta.presence.friends)
            {
                foreach (DeathStain s in f.stains)
                {
                    if (s.depth == depth) list.Add(s);
                }
            }
            foreach (DeathStain s in meta.presence.myStains)
            {
                if (s.depth == depth && s.trail.Count >= 2) list.Add(s);
            }
            if (list.Count > 2) list.RemoveRange(2, list.Count - 2);
            return list;
        }

        /// <summary>A friend's spirit for this run (the friend picked by the run's seed), or null.</summary>
        public static SpiritProfile SpiritFor(MetaProgress meta, ulong seed)
        {
            if (meta == null) return null;
            var spirits = new List<SpiritProfile>();
            foreach (PresencePacket f in meta.presence.friends)
            {
                if (f.spirit != null && !string.IsNullOrEmpty(f.spirit.name) && WeaponDefs.Exists(f.spirit.weaponA)) spirits.Add(f.spirit);
            }
            if (spirits.Count == 0) return null;
            return spirits[(int)(seed % (ulong)spirits.Count)];
        }

        /// <summary>The newest friend who became the Ashura less than a week ago, or null.</summary>
        public static AshuraMark NewAshura(MetaProgress meta, DateTime nowUtc)
        {
            if (meta == null) return null;
            AshuraMark best = null;
            foreach (PresencePacket f in meta.presence.friends)
            {
                AshuraMark a = f.ashura;
                if (a == null || a.since <= 0) continue;
                double days = (nowUtc.Date - new DateTime(a.since, DateTimeKind.Utc)).TotalDays;
                if (days < 0 || days >= AshuraDays) continue;
                if (best == null || a.since > best.since) best = a;
            }
            return best;
        }
    }
}
