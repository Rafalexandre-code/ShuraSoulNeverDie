using System;
using System.Collections.Generic;
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
        public const int CurrentVersion = 4;
        const string PrefsKey = "shura.settings";

        public int version = CurrentVersion;
        public float master = 0.8f;
        public float music = 0.7f;
        public float sfx = 0.9f;
        public float shake = 1f;
        public bool reduceFlashes;
        /// <summary>The gamepad shakes on parries, wounds and finishers.</summary>
        public bool rumble = true;
        /// <summary>Locked on, the camera frames you and the target together.</summary>
        public bool lockCamera = true;
        /// <summary>Bloom, colour grade, vignette and grain.</summary>
        public bool postFx = true;
        /// <summary>Keyboard and mouse: lock on with Z (on) or aim with the mouse as always (off).</summary>
        public bool mouseLockOn;
        public bool fullscreen = true;
        /// <summary>Size of the battle HUD (1 = the original layout, drawn for 1080p).</summary>
        public float uiScale = 0.8f;
        /// <summary>How much of the arena the camera shows (1 = default; more = farther, smaller fighters).</summary>
        public float view = 1.25f;
        /// <summary>Tutorial hints during play.</summary>
        public bool hints = true;
        /// <summary>WP-01: telemetria local (arquivos .jsonl no próprio computador). Ligada por padrão; o jogador é avisado.</summary>
        public bool telemetry = true;
        public bool telemetryNoticeShown;
        /// <summary>Experimento do diretor: a trilha de ritmo embaixo da tela (BPM). Ligada para o teste.</summary>
        public bool rhythmTrack = true;
        /// <summary>Chosen screen resolution (0 = the monitor's own).</summary>
        public int resolutionWidth;
        public int resolutionHeight;
        public bool timingIndicator = true;
        public bool blood = true;
        /// <summary>false = Ninja Adventure tracks; true = "Samurai Nights" in five layers.</summary>
        public bool samuraiNights;
        public string weaponA = "katana";
        public string weaponB = "montante";
        /// <summary>Altar "Segunda Troca": the third blade, sheathed ("" = none).</summary>
        public string weaponC = "";
        /// <summary>Every layer of the game open from the start (§7.7), for experienced players.</summary>
        public bool unlockAll;
        /// <summary>"O Shura Lembra" (§5.6): each run starts with what the last five runs taught the enemies.</summary>
        public bool shuraRemembers;
        /// <summary>The forge: one "blade+grip+tsuba" per blade that was changed.</summary>
        public List<string> forge = new List<string>();

        /// <summary>The weapon actually carried for a blade: forged if the forge changed it.</summary>
        public string Loadout(string blade)
        {
            if (forge != null)
            {
                foreach (string f in forge)
                {
                    WeaponForge.Split(f, out string b, out _, out _);
                    if (b == blade) return f;
                }
            }
            return blade;
        }

        public void SetForge(string blade, string grip, string tsuba)
        {
            if (forge == null) forge = new List<string>();
            forge.RemoveAll(f => { WeaponForge.Split(f, out string b, out _, out _); return b == blade; });
            string id = WeaponForge.Compose(blade, grip, tsuba);
            if (id != blade) forge.Add(id);
        }
        public string bindings = string.Empty;
        /// <summary>Name shown to the party when hosting (and in the lobby).</summary>
        public string playerName = "Anfitrião";
        /// <summary>Which samurai the host plays (index into PlayerController.PartyArt).</summary>
        public int playerArt;
        /// <summary>The last host typed in "Entrar por IP".</summary>
        public string lastAddress = string.Empty;
        /// <summary>The dojo was finished (or skipped): "Jogar" goes straight to the arsenal.</summary>
        public bool tutorialDone;

        public const float MinUiScale = 0.5f;
        public const float MaxUiScale = 1.3f;
        public const float MinView = 0.9f;
        public const float MaxView = 1.7f;

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
            // v4 added the HUD size, the camera's field of view, hints and the resolution.
            if (version < 4)
            {
                uiScale = defaults.uiScale;
                view = defaults.view;
                hints = true;
            }
            uiScale = Mathf.Clamp(float.IsNaN(uiScale) ? defaults.uiScale : uiScale, MinUiScale, MaxUiScale);
            view = Mathf.Clamp(float.IsNaN(view) ? defaults.view : view, MinView, MaxView);
            if (resolutionWidth < 640 || resolutionHeight < 360) resolutionWidth = resolutionHeight = 0;
            if (!WeaponDefs.IsBlade(weaponA)) weaponA = "katana";
            if (forge == null) forge = new List<string>();
            forge.RemoveAll(f => !WeaponDefs.Exists(f));
            if (!WeaponDefs.IsBlade(weaponB) || weaponB == weaponA) weaponB = weaponA == "montante" ? "katana" : "montante";
            playerName = CleanName(playerName, defaults.playerName);
            playerArt = Mathf.Clamp(playerArt, 0, PlayerController.PartyArt.Length - 1);
            version = CurrentVersion;
        }

        /// <summary>A shareable name: printable, trimmed, at most 16 characters.</summary>
        public static string CleanName(string name, string fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (!char.IsControl(c) && c != '"' && c != '\\') sb.Append(c);
            }
            string clean = sb.ToString().Trim();
            if (clean.Length > 16) clean = clean.Substring(0, 16);
            return clean.Length == 0 ? fallback : clean;
        }

        static float Clean(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }
    }
}
