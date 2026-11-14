using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// An enemy that killed you and remembers it (design doc §5.1). It took a name, part of
    /// your Ecos, something from your build and a scar where you cut it, and it comes back
    /// in future runs, in its own lair on the route map.
    /// </summary>
    [Serializable]
    public sealed class Rival
    {
        public string id;
        public string enemyId;
        /// <summary>"Kuroda, o Ronin Errante que Partiu Haru".</summary>
        public string name;
        public string surname;
        /// <summary>0..2: Captain (one to three stars); 3: General of a biome.</summary>
        public int rank;
        public int biome;
        public int kills = 1;
        /// <summary>The Ecos it took from you; vengeance returns them with 50% more.</summary>
        public int ecos;
        /// <summary>What it stole from your build: "arma" or "tecnica", and its name.</summary>
        public string trait = "";
        public string traitName = "";
        /// <summary>Where you wounded it (a WoundRegion name), and what it costs it now.</summary>
        public string scar = "";
        public string victim = "";
        /// <summary>A friend's rival lent by the online presence (§9.5): their name, empty for your own.</summary>
        public string foreign = "";
        /// <summary>A General's bodyguards lost to fallen captains, and its fury.</summary>
        public int guardsLost;
        public bool furious;

        public bool General => rank >= Rivals.GeneralRank;

        public void Sanitize()
        {
            rank = Mathf.Clamp(rank, 0, Rivals.GeneralRank);
            kills = Mathf.Max(1, kills);
            ecos = Mathf.Max(0, ecos);
            guardsLost = Mathf.Clamp(guardsLost, 0, 2);
            if (trait == null) trait = "";
            if (traitName == null) traitName = "";
            if (scar == null) scar = "";
            if (victim == null) victim = "";
            if (name == null) name = "";
            if (surname == null) surname = "";
            if (foreign == null) foreign = "";
        }
    }

    /// <summary>What a vengeance returns.</summary>
    public struct Vengeance
    {
        public int ecos;
        public string technique;
        public bool general;
    }

    public static class Rivals
    {
        public const int GeneralRank = 3;
        public const int MaxCaptains = 15;
        public const int MaxGenerals = 5;
        /// <summary>A fifth of a fallen run's Ecos goes with its killer.</summary>
        public const float EcosTaken = 0.2f;
        public const float VengeanceMult = 1.5f;

        static readonly string[] Surnames =
        {
            "Kuroda", "Akechi", "Saitō", "Hōjō", "Ōtomo", "Kikkawa", "Sasaki", "Matsudaira", "Yagyū", "Murakami",
            "Tachibana", "Gamō", "Niwa", "Ōuchi", "Amago", "Kobayakawa", "Uesugi", "Satake", "Nanbu", "Mogami",
        };

        static readonly string[] Verbs = { "que Partiu", "que Calou", "que Enterrou", "que Quebrou", "que Derrubou" };

        /// <summary>The rivals the route map draws lairs for (set when a run begins).</summary>
        public static readonly List<Rival> Roster = new List<Rival>();

        public static string RankName(Rival r) => r.General ? "General" : "Capitão " + new string('★', r.rank + 1);

        public static Rival Find(MetaProgress meta, string id)
        {
            if (meta?.rivals == null || string.IsNullOrEmpty(id)) return null;
            foreach (Rival r in meta.rivals)
            {
                if (r.id == id) return r;
            }
            return null;
        }

        static int Hash(string s, ulong salt)
        {
            ulong h = 1469598103934665603UL ^ salt;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return (int)(h & 0x7FFFFFFF);
        }

        /// <summary>
        /// A duelist fell. Their killer becomes a rival (or, if it already was one, rises in
        /// rank); it takes part of the run's Ecos, a trait from the build and a scar.
        /// Returns the rival, or null when the blow came from nobody who could remember.
        /// </summary>
        public static Rival OnPlayerKilled(MetaProgress meta, EnemyDef killer, string killerRivalId, int biome, string victim,
                                           string weaponName, string techniqueName, int scarMask, int ecosLost, ulong seed)
        {
            if (meta == null) return null;
            Rival known = Find(meta, killerRivalId);
            if (known != null)
            {
                known.kills++;
                known.ecos += ecosLost;
                known.victim = victim;
                Promote(meta, known);
                return known;
            }
            if (killer == null || killer.minion) return null;
            int captains = 0;
            foreach (Rival r in meta.rivals)
            {
                if (!r.General) captains++;
            }
            // A full roster: the weakest captain is forgotten to make room.
            if (captains >= MaxCaptains)
            {
                Rival weakest = null;
                foreach (Rival r in meta.rivals)
                {
                    if (!r.General && (weakest == null || r.kills < weakest.kills)) weakest = r;
                }
                if (weakest != null) meta.rivals.Remove(weakest);
            }
            int h = Hash(killer.id + victim, seed);
            string surname = Surnames[h % Surnames.Length];
            foreach (Rival r in meta.rivals)
            {
                if (r.surname == surname) surname = Surnames[(h / 3 + meta.rivals.Count) % Surnames.Length];
            }
            var rival = new Rival
            {
                id = killer.id + "-" + (seed & 0xFFFFFF).ToString("x") + "-" + meta.runs,
                enemyId = killer.id,
                surname = surname,
                name = surname + ", o " + killer.displayName + " " + Verbs[(h / 7) % Verbs.Length] + " " + victim,
                biome = Mathf.Max(0, biome),
                ecos = ecosLost,
                victim = victim,
                trait = string.IsNullOrEmpty(techniqueName) ? "arma" : "tecnica",
                traitName = string.IsNullOrEmpty(techniqueName) ? (weaponName ?? "Katana") : techniqueName,
                scar = ScarFrom(scarMask, h),
            };
            meta.rivals.Add(rival);
            return rival;
        }

        static string ScarFrom(int mask, int h)
        {
            WoundRegion[] all = { WoundRegion.Arm, WoundRegion.Leg, WoundRegion.Torso, WoundRegion.Eyes };
            foreach (WoundRegion w in all)
            {
                if ((mask & (1 << (int)w)) != 0) return w.ToString();
            }
            return all[(h / 11) % all.Length].ToString();
        }

        /// <summary>One more kill: a star, and at three stars the General's seat of its biome (if free).</summary>
        public static void Promote(MetaProgress meta, Rival r)
        {
            if (r.General) return;
            if (r.rank < GeneralRank - 1)
            {
                r.rank++;
                return;
            }
            foreach (Rival g in meta.rivals)
            {
                if (g != r && g.General && g.biome == r.biome) return;
            }
            r.rank = GeneralRank;
            r.name = r.surname + ", General " + Biomes.All[Mathf.Clamp(r.biome, 0, Biomes.All.Length - 1)].suffix;
        }

        /// <summary>
        /// Vengeance: the rival falls for good. Its Ecos come back with half again, its
        /// technique is yours, and a fallen captain unsettles its General.
        /// </summary>
        public static Vengeance Defeat(MetaProgress meta, string rivalId)
        {
            Rival r = Find(meta, rivalId);
            if (r == null) return default;
            meta.rivals.Remove(r);
            meta.fallenRivals.Add(r);
            if (!r.General)
            {
                foreach (Rival g in meta.rivals)
                {
                    if (!g.General || g.biome != r.biome) continue;
                    g.guardsLost = Mathf.Min(2, g.guardsLost + 1);
                    g.furious = true;
                }
            }
            EnemyDef def = EnemyDefs.TryGet(r.enemyId, out EnemyDef d) ? d : null;
            return new Vengeance { ecos = Mathf.RoundToInt(r.ecos * VengeanceMult), technique = def?.techniqueId, general = r.General };
        }

        // ── In the arena ─────────────────────────────────────────────────────────────

        /// <summary>The oath "Rival Eterno" adds a rank.</summary>
        public static int EffectiveRank(Rival r, bool eternal) => Mathf.Min(GeneralRank + 1, r.rank + (eternal ? 1 : 0));

        public static float HealthMult(int rank) => 1.3f + 0.2f * rank;
        public static float PostureMult(int rank) => 1.15f + 0.1f * rank;
        public static float DamageMult(int rank) => 1.08f + 0.05f * rank;

        /// <summary>A General keeps two bodyguards, less those its fallen captains cost it.</summary>
        public static int Guards(Rival r) => r.General ? Mathf.Max(0, 2 - r.guardsLost) : 0;

        public static string TraitText(Rival r)
        {
            return r.trait == "tecnica"
                ? "Roubou " + r.traitName + ": postura +25%."
                : "Aprendeu com a sua " + r.traitName + ": dano +15%, golpes mais rápidos.";
        }

        public static string ScarText(Rival r)
        {
            switch (r.scar)
            {
                case "Arm": return "Cicatriz no braço: golpes 20% mais lentos.";
                case "Leg": return "Cicatriz na perna: não salta nem investe.";
                case "Eyes": return "Cicatriz no olho: erra a mira dos disparos.";
                default: return "Cicatriz no peito: postura −20%.";
            }
        }

        static readonly string[] Taunts =
        {
            "{0} hesitou exatamente assim. Lembro de cada respiração.",
            "Seus Ecos ainda estão comigo. Venha buscá-los.",
            "Voltou? A roda gosta de você. Eu também.",
            "Esta cicatriz é sua. Hoje eu devolvo o presente.",
            "Aprendi a sua {1}. Quer ver como ficou?",
            "Quantos corpos a sua alma ainda tem para me dar?",
        };

        static readonly string[] GeneralTaunts =
        {
            "Este bioma é meu. Você é só mais um que veio morrer nele.",
            "Meus capitães caíram? Então venho eu.",
            "Sou o que a sua derrota construiu.",
        };

        public static string Taunt(Rival r, int salt)
        {
            string[] pool = r.General ? GeneralTaunts : Taunts;
            int h = Hash(r.id, (ulong)salt);
            return string.Format(pool[h % pool.Length], r.victim, r.traitName.ToLowerInvariant());
        }

        // ── On the route map ─────────────────────────────────────────────────────────

        /// <summary>One door in five (from depth 3) may hide a rival's lair, if any rival lives.</summary>
        public static Rival LairAt(ulong seed, int depth, int biome, out int door)
        {
            door = -1;
            if (Roster.Count == 0 || depth < 3) return null;
            ulong h = RoomGenerator.Mix(seed, depth + 9100);
            if (h % 100UL >= 20UL) return null;
            door = (int)(h / 100UL % 3UL);
            // The General of this biome first, then any captain.
            foreach (Rival r in Roster)
            {
                if (r.General && r.biome == biome) return r;
            }
            var captains = new List<Rival>();
            foreach (Rival r in Roster)
            {
                if (!r.General) captains.Add(r);
            }
            if (captains.Count == 0) return null;
            return captains[(int)(h / 300UL % (ulong)captains.Count)];
        }
    }
}
