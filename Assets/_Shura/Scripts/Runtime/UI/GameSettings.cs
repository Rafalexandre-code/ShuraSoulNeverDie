using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Player settings persisted as versioned JSON. Missing, old or corrupt data falls back to
    /// defaults field by field instead of failing.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 3;
        const string PrefsKey = "shura.settings";

        public int version = CurrentVersion;
        public float master = 0.8f;
        public float music = 0.7f;
        public float sfx = 0.9f;
        public float shake = 1f;
        public bool reduceFlashes;
        public bool fullscreen = true;
        public bool timingIndicator = true;
        public bool blood = true;
        public string weaponA = "katana";
        public string weaponB = "montante";
        public string bindings = string.Empty;

        public static GameSettings Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (string.IsNullOrEmpty(json)) return new GameSettings();
                GameSettings loaded = JsonUtility.FromJson<GameSettings>(json);
                if (loaded == null) return new GameSettings();
                loaded.Sanitize();
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Shura] Configurações corrompidas, usando padrão: " + e.Message);
                return new GameSettings();
            }
        }

        public void Save()
        {
            version = CurrentVersion;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        void Sanitize()
        {
            var defaults = new GameSettings();
            master = Clean(master, defaults.master);
            music = Clean(music, defaults.music);
            sfx = Clean(sfx, defaults.sfx);
            shake = Clean(shake, defaults.shake);
            if (bindings == null) bindings = string.Empty;
            // v2 added the timing indicator: older saves get it switched on.
            if (version < 2) timingIndicator = true;
            // v3 added blood and the weapon loadout.
            if (version < 3) blood = true;
            if (!WeaponDefs.Exists(weaponA)) weaponA = "katana";
            if (!WeaponDefs.Exists(weaponB) || weaponB == weaponA) weaponB = weaponA == "montante" ? "katana" : "montante";
            version = CurrentVersion;
        }

        static float Clean(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }
    }
}
