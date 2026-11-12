using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Permanent progress across runs: Ecos (currency earned by fighting) and upgrade levels.
    /// Every upgrade is capped, so progression softens the run without replacing skill.
    /// </summary>
    [Serializable]
    public sealed class MetaProgress
    {
        public const int CurrentVersion = 2;

        [Serializable]
        public sealed class UpgradeLevel
        {
            public string id;
            public int level;
        }

        public int version = CurrentVersion;
        public int ecos;
        public int totalEcosEarned;
        public int runs;
        public int victories;
        public int bestRoom;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        /// <summary>Weapon mastery: finishers dealt with each weapon (id → count), across every run.</summary>
        public List<UpgradeLevel> mastery = new List<UpgradeLevel>();

        // ── Phase 4: the Temple, the currencies, the lineage (design doc §7) ─────────────
        /// <summary>Aço Celeste: broken items and Guardians; rebuilds the Temple and feeds the Forge.</summary>
        public int steel;
        /// <summary>Fragmentos de Alma: techniques absorbed again; open the ends of the Altar's branches.</summary>
        public int fragments;
        /// <summary>Pétalas de Sakura: oaths and room challenges; spent in the Zen Garden.</summary>
        public int petals;
        /// <summary>−100 (Shura) … +100 (Bushidō), across every life (§8.3).</summary>
        public int karma;
        /// <summary>Temple constructions already rebuilt.</summary>
        public List<string> buildings = new List<string>();
        /// <summary>The Altar's rule nodes (the ends of each branch).</summary>
        public List<string> nodes = new List<string>();
        /// <summary>The oaths sworn in the Zen Garden for the next runs (id → level).</summary>
        public List<UpgradeLevel> oaths = new List<UpgradeLevel>();
        /// <summary>The seed typed in the Zen Garden ("" = a new one every run).</summary>
        public string seed = "";
        /// <summary>The disciple whose body the soul wears now.</summary>
        public Disciple disciple;
        /// <summary>The dead and the retired, oldest first.</summary>
        public List<Disciple> cemetery = new List<Disciple>();
        /// <summary>A disciple fell: the next one must be chosen among these before the road.</summary>
        public List<Disciple> heirs = new List<Disciple>();
        /// <summary>Enemies met (id → kills), for the Library's bestiary.</summary>
        public List<UpgradeLevel> bestiary = new List<UpgradeLevel>();
        /// <summary>Blade colours bought with petals, and the one worn ("" = the weapon's own).</summary>
        public List<string> colors = new List<string>();
        public string bladeColor = "";
        /// <summary>The garden's ornaments bought with petals.</summary>
        public List<string> garden = new List<string>();
        /// <summary>Most Calor carried by a run.</summary>
        public int bestHeat;

        // ── Phase 4, part 2: the Tea House, the scrolls, the modes ───────────────────
        /// <summary>Lifetime counters the guests' missions read (id → count).</summary>
        public List<UpgradeLevel> stats = new List<UpgradeLevel>();
        /// <summary>Personal missions already rewarded (guest ids).</summary>
        public List<string> missions = new List<string>();
        /// <summary>Each enemy's scroll of challenges (design doc §7.6).</summary>
        public List<EnemyScroll> scrolls = new List<EnemyScroll>();
        /// <summary>The daily challenge: the last day tried and every result kept.</summary>
        public string lastDaily = "";
        public List<DailyResult> dailyResults = new List<DailyResult>();
        /// <summary>Endings reached (ids).</summary>
        public List<string> endings = new List<string>();
        public int towerBest;
        public int pilgrimageBest;

        // ── Phase 5, part 1: rivals and the reading memory ──────────────────────────
        /// <summary>The rivals of the Shuradō who remember you (§5.1).</summary>
        public List<Rival> rivals = new List<Rival>();
        /// <summary>Rivals avenged, oldest first.</summary>
        public List<Rival> fallenRivals = new List<Rival>();
        /// <summary>"O Shura Lembra": how the enemies read you at the end of the last runs (§5.6).</summary>
        public List<ReadingMemory> readings = new List<ReadingMemory>();

        public int Stat(string id)
        {
            if (stats == null) return 0;
            foreach (UpgradeLevel u in stats)
            {
                if (u.id == id) return u.level;
            }
            return 0;
        }

        public void AddStat(string id, int amount)
        {
            if (amount == 0 || string.IsNullOrEmpty(id)) return;
            if (stats == null) stats = new List<UpgradeLevel>();
            foreach (UpgradeLevel u in stats)
            {
                if (u.id != id) continue;
                u.level = Mathf.Max(0, u.level + amount);
                return;
            }
            stats.Add(new UpgradeLevel { id = id, level = Mathf.Max(0, amount) });
        }

        public bool MissionDone(string guest) => missions != null && missions.Contains(guest);

        /// <summary>Enemies met at least once.</summary>
        public int KnownEnemies => bestiary != null ? bestiary.Count : 0;

        public EnemyScroll Scroll(string enemyId, bool create)
        {
            if (scrolls == null) scrolls = new List<EnemyScroll>();
            foreach (EnemyScroll s in scrolls)
            {
                if (s.id == enemyId) return s;
            }
            if (!create) return null;
            var scroll = new EnemyScroll { id = enemyId };
            scrolls.Add(scroll);
            return scroll;
        }

        public bool Built(string building) => buildings != null && buildings.Contains(building);
        public bool HasNode(string node) => nodes != null && nodes.Contains(node);

        public int OathLevel(string id)
        {
            if (oaths == null) return 0;
            foreach (UpgradeLevel u in oaths)
            {
                if (u.id == id) return u.level;
            }
            return 0;
        }

        public void SetOath(string id, int level)
        {
            if (oaths == null) oaths = new List<UpgradeLevel>();
            oaths.RemoveAll(u => u.id == id);
            if (level > 0) oaths.Add(new UpgradeLevel { id = id, level = level });
        }

        /// <summary>Kills of an enemy; −1 = never met.</summary>
        public int Kills(string enemyId)
        {
            if (bestiary == null) return -1;
            foreach (UpgradeLevel u in bestiary)
            {
                if (u.id == enemyId) return u.level;
            }
            return -1;
        }

        /// <summary>Seen (kills = 0) or one more kill.</summary>
        public void RecordEnemy(string enemyId, bool killed)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            if (bestiary == null) bestiary = new List<UpgradeLevel>();
            foreach (UpgradeLevel u in bestiary)
            {
                if (u.id != enemyId) continue;
                if (killed) u.level++;
                return;
            }
            bestiary.Add(new UpgradeLevel { id = enemyId, level = killed ? 1 : 0 });
        }

        public void AddKarma(int amount) => karma = Mathf.Clamp(karma + amount, -100, 100);

        public int MasteryFinishers(string weaponId)
        {
            if (mastery == null) return 0;
            foreach (UpgradeLevel u in mastery)
            {
                if (u.id == weaponId) return u.level;
            }
            return 0;
        }

        /// <summary>One more finisher with this weapon. Returns the new mastery level when it just went up, otherwise 0.</summary>
        public int AddMasteryFinisher(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return 0;
            if (mastery == null) mastery = new List<UpgradeLevel>();
            int before = MasteryFinishers(weaponId);
            SetMastery(weaponId, before + 1);
            int was = WeaponMastery.LevelFor(before), now = WeaponMastery.LevelFor(before + 1);
            return now > was ? now : 0;
        }

        void SetMastery(string weaponId, int count)
        {
            foreach (UpgradeLevel u in mastery)
            {
                if (u.id != weaponId) continue;
                u.level = count;
                return;
            }
            mastery.Add(new UpgradeLevel { id = weaponId, level = count });
        }

        public int GetLevel(string id)
        {
            foreach (UpgradeLevel u in upgrades)
            {
                if (u.id == id) return u.level;
            }
            return 0;
        }

        void SetLevel(string id, int level)
        {
            foreach (UpgradeLevel u in upgrades)
            {
                if (u.id != id) continue;
                u.level = level;
                return;
            }
            upgrades.Add(new UpgradeLevel { id = id, level = level });
        }

        public bool IsMaxed(MetaUpgradeDef def) => GetLevel(def.id) >= def.maxLevel;

        public int NextCost(MetaUpgradeDef def) => def.CostFor(GetLevel(def.id));

        /// <summary>The Altar is a tree with a point limit: one point per level, two per rule node.</summary>
        public bool CanBuy(MetaUpgradeDef def) => !IsMaxed(def) && ecos >= NextCost(def) && AltarTree.PointsFree(this) >= 1;

        /// <summary>Altar: all Ecos spent on levels and the fragments of the rule nodes come back.</summary>
        public void ResetAltar()
        {
            foreach (MetaUpgradeDef def in MetaUpgrades.All)
            {
                for (int l = 0; l < GetLevel(def.id); l++) ecos += def.CostFor(l);
            }
            upgrades.Clear();
            foreach (string n in nodes)
            {
                AltarNode node = AltarTree.Node(n);
                if (node == null) continue;
                ecos += node.ecos;
                fragments += node.fragments;
            }
            nodes.Clear();
        }

        internal void SetLevelForTests(string id, int level) => SetLevel(id, level);

        public bool TryBuy(MetaUpgradeDef def)
        {
            if (!CanBuy(def)) return false;
            ecos -= NextCost(def);
            SetLevel(def.id, GetLevel(def.id) + 1);
            return true;
        }

        /// <summary>The upgrade levels as a short code ("vigor:5,postura:4") for the online room.</summary>
        public string LevelsCode()
        {
            var parts = new List<string>();
            foreach (MetaUpgradeDef def in MetaUpgrades.All)
            {
                int level = GetLevel(def.id);
                if (level > 0) parts.Add(def.id + ":" + level);
            }
            if (mastery != null)
            {
                foreach (UpgradeLevel u in mastery)
                {
                    if (u.level > 0) parts.Add("m." + u.id + ":" + u.level);
                }
            }
            if (nodes != null)
            {
                foreach (string n in nodes) parts.Add("n." + n + ":1");
            }
            // The disciple's body comes along: age and traits.
            if (disciple != null)
            {
                parts.Add("age:" + disciple.age);
                if (disciple.traits != null)
                {
                    foreach (string t in disciple.traits) parts.Add("t." + t + ":1");
                }
            }
            return string.Join(",", parts);
        }

        /// <summary>A duelist's own Altar from their code (unknown ids dropped, levels capped).</summary>
        public static MetaProgress FromLevelsCode(string code)
        {
            var p = new MetaProgress();
            if (string.IsNullOrEmpty(code)) return p;
            foreach (string part in code.Split(','))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2 || !int.TryParse(kv[1], out int level)) continue;
                string id = kv[0].Trim();
                if (id.StartsWith("n."))
                {
                    string node = id.Substring(2);
                    if (AltarTree.Node(node) != null && !p.nodes.Contains(node)) p.nodes.Add(node);
                    continue;
                }
                if (id == "age")
                {
                    if (p.disciple == null) p.disciple = new Disciple { name = "?" };
                    p.disciple.age = Mathf.Clamp(level, 16, 90);
                    continue;
                }
                if (id.StartsWith("t."))
                {
                    if (p.disciple == null) p.disciple = new Disciple { name = "?", age = 25 };
                    string trait = id.Substring(2);
                    if (Lineage.Trait(trait) != null && !p.disciple.traits.Contains(trait) && p.disciple.traits.Count < 2) p.disciple.traits.Add(trait);
                    continue;
                }
                if (id.StartsWith("m."))
                {
                    id = id.Substring(2);
                    if (WeaponDefs.IsBlade(id) && p.MasteryFinishers(id) == 0) p.SetMastery(id, Mathf.Clamp(level, 0, 9999));
                    continue;
                }
                MetaUpgradeDef def = MetaUpgrades.Get(kv[0].Trim());
                if (def != null) p.SetLevel(def.id, Mathf.Clamp(level, 0, def.maxLevel));
            }
            return p;
        }

        public void ApplyTo(PlayerModifiers m)
        {
            foreach (MetaUpgradeDef def in MetaUpgrades.All) def.Apply(m, GetLevel(def.id));
            AltarTree.ApplyNodes(this, m);
            Lineage.Apply(this, m);
            Garden.Apply(this, m);
        }

        /// <summary>Banks a finished (or abandoned) run.</summary>
        public void RecordRun(int ecosEarned, int roomsCleared, bool victory)
        {
            ecos += Mathf.Max(0, ecosEarned);
            totalEcosEarned += Mathf.Max(0, ecosEarned);
            runs++;
            if (victory) victories++;
            bestRoom = Mathf.Max(bestRoom, roomsCleared);
        }

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>Throws on unreadable JSON; callers decide how to recover.</summary>
        public static MetaProgress FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new MetaProgress();
            MetaProgress p = JsonUtility.FromJson<MetaProgress>(json);
            if (p == null) throw new FormatException("Progresso vazio ou inválido.");
            p.Sanitize();
            return p;
        }

        void Sanitize()
        {
            ecos = Mathf.Max(0, ecos);
            totalEcosEarned = Mathf.Max(0, totalEcosEarned);
            runs = Mathf.Max(0, runs);
            victories = Mathf.Clamp(victories, 0, runs);
            bestRoom = Mathf.Max(0, bestRoom);

            // Drop unknown ids (removed upgrades), merge duplicates, clamp to current caps.
            var clean = new List<UpgradeLevel>();
            if (upgrades != null)
            {
                foreach (UpgradeLevel u in upgrades)
                {
                    if (u == null) continue;
                    MetaUpgradeDef def = MetaUpgrades.Get(u.id);
                    if (def == null || clean.Exists(c => c.id == u.id)) continue;
                    clean.Add(new UpgradeLevel { id = u.id, level = Mathf.Clamp(u.level, 0, def.maxLevel) });
                }
            }
            upgrades = clean;

            var cleanMastery = new List<UpgradeLevel>();
            if (mastery != null)
            {
                foreach (UpgradeLevel u in mastery)
                {
                    if (u == null || string.IsNullOrEmpty(u.id) || cleanMastery.Exists(c => c.id == u.id)) continue;
                    cleanMastery.Add(new UpgradeLevel { id = u.id, level = Mathf.Max(0, u.level) });
                }
            }
            mastery = cleanMastery;

            steel = Mathf.Max(0, steel);
            fragments = Mathf.Max(0, fragments);
            petals = Mathf.Max(0, petals);
            karma = Mathf.Clamp(karma, -100, 100);
            bestHeat = Mathf.Max(0, bestHeat);
            if (seed == null) seed = "";
            if (bladeColor == null) bladeColor = "";
            buildings = Distinct(buildings, b => Temple.Get(b) != null);
            nodes = Distinct(nodes, n => AltarTree.Node(n) != null);
            colors = Distinct(colors, c => Garden.Color(c) != null);
            garden = Distinct(garden, g => Garden.Ornament(g) != null);
            if (bladeColor != "" && !colors.Contains(bladeColor)) bladeColor = "";
            var cleanOaths = new List<UpgradeLevel>();
            if (oaths != null)
            {
                foreach (UpgradeLevel u in oaths)
                {
                    OathDef def = u != null ? Oaths.Get(u.id) : null;
                    if (def == null || def.sealedUntilRivals || cleanOaths.Exists(c => c.id == u.id)) continue;
                    int level = Mathf.Clamp(u.level, 0, def.maxLevel);
                    if (level > 0) cleanOaths.Add(new UpgradeLevel { id = u.id, level = level });
                }
            }
            oaths = cleanOaths;
            var cleanBestiary = new List<UpgradeLevel>();
            if (bestiary != null)
            {
                foreach (UpgradeLevel u in bestiary)
                {
                    if (u == null || string.IsNullOrEmpty(u.id) || cleanBestiary.Exists(c => c.id == u.id)) continue;
                    cleanBestiary.Add(new UpgradeLevel { id = u.id, level = Mathf.Max(0, u.level) });
                }
            }
            bestiary = cleanBestiary;
            if (cemetery == null) cemetery = new List<Disciple>();
            cemetery.RemoveAll(d => d == null);
            foreach (Disciple d in cemetery) d.Sanitize();
            if (heirs == null) heirs = new List<Disciple>();
            heirs.RemoveAll(d => d == null);
            foreach (Disciple d in heirs) d.Sanitize();
            // JsonUtility writes an empty object for a null field: a nameless disciple is none.
            if (disciple != null && string.IsNullOrEmpty(disciple.name)) disciple = null;
            disciple?.Sanitize();

            var cleanStats = new List<UpgradeLevel>();
            if (stats != null)
            {
                foreach (UpgradeLevel u in stats)
                {
                    if (u == null || string.IsNullOrEmpty(u.id) || cleanStats.Exists(c => c.id == u.id)) continue;
                    cleanStats.Add(new UpgradeLevel { id = u.id, level = Mathf.Max(0, u.level) });
                }
            }
            stats = cleanStats;
            missions = Distinct(missions, g => TeaGuests.Get(g) != null);
            endings = Distinct(endings, e => !string.IsNullOrEmpty(e));
            var cleanScrolls = new List<EnemyScroll>();
            if (scrolls != null)
            {
                foreach (EnemyScroll s in scrolls)
                {
                    if (s == null || string.IsNullOrEmpty(s.id) || cleanScrolls.Exists(c => c.id == s.id)) continue;
                    s.Sanitize();
                    cleanScrolls.Add(s);
                }
            }
            scrolls = cleanScrolls;
            if (lastDaily == null) lastDaily = "";
            if (dailyResults == null) dailyResults = new List<DailyResult>();
            dailyResults.RemoveAll(d => d == null || string.IsNullOrEmpty(d.date));
            towerBest = Mathf.Max(0, towerBest);
            pilgrimageBest = Mathf.Max(0, pilgrimageBest);
            if (rivals == null) rivals = new List<Rival>();
            rivals.RemoveAll(r => r == null || string.IsNullOrEmpty(r.id) || !EnemyDefs.TryGet(r.enemyId ?? "", out _));
            foreach (Rival r in rivals) r.Sanitize();
            if (fallenRivals == null) fallenRivals = new List<Rival>();
            fallenRivals.RemoveAll(r => r == null);
            if (readings == null) readings = new List<ReadingMemory>();
            readings.RemoveAll(r => r == null || r.values == null || r.values.Length != AdaptiveTraits.All.Length);
            while (readings.Count > ReadingMemory.Keep) readings.RemoveAt(0);
            version = CurrentVersion;
        }

        static List<string> Distinct(List<string> list, Predicate<string> valid)
        {
            var clean = new List<string>();
            if (list == null) return clean;
            foreach (string s in list)
            {
                if (!string.IsNullOrEmpty(s) && valid(s) && !clean.Contains(s)) clean.Add(s);
            }
            return clean;
        }
    }

    /// <summary>Atomic, corruption-tolerant persistence of MetaProgress.</summary>
    public static class MetaStore
    {
        public static string DefaultPath(bool automated)
        {
            return Path.Combine(Application.persistentDataPath, automated ? "progress_auto.json" : "progress.json");
        }

        public static MetaProgress Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new MetaProgress();
                return MetaProgress.FromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                // Keep the broken file for inspection instead of silently overwriting it.
                Debug.LogWarning("[Shura] Progresso ilegível, começando do zero (cópia em .corrupt): " + e.Message);
                try
                {
                    File.Copy(path, path + ".corrupt", true);
                }
                catch (Exception)
                {
                    // Best effort only.
                }
                return new MetaProgress();
            }
        }

        public static void Save(MetaProgress progress, string path)
        {
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, progress.ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Shura] Falha ao salvar progresso: " + e.Message);
            }
        }
    }
}
