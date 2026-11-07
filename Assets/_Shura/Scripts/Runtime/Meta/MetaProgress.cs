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
