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
        public const int CurrentVersion = 1;

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

        public bool CanBuy(MetaUpgradeDef def) => !IsMaxed(def) && ecos >= NextCost(def);

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
            version = CurrentVersion;
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
