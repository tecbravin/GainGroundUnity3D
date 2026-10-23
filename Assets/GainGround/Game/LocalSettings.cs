using System;
using System.IO;
using UnityEngine;

namespace GainGround.Game
{
    public enum GameDifficulty { Normal, Easy, Hard }

    [Serializable]
    public sealed class LocalSettings
    {
        public PlayerBindings Player1 = PlayerBindings.Defaults(0);
        public PlayerBindings Player2 = PlayerBindings.Defaults(1);
        public int BGM = 100;
        public int SFX = 100;
        public bool ClassicMode = true;
        public GameDifficulty Difficulty = GameDifficulty.Normal;
        public int BindingDefaultsVersion;
        const string BrowserSettingsKey = "GainGround.LocalSettings";

        public static string FilePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath("localSettings.json");
#else
                return Path.Combine(Application.persistentDataPath, "localSettings.json");
#endif
            }
        }
        public LocalSettings Copy() => new LocalSettings { BGM = BGM, SFX = SFX, ClassicMode = ClassicMode, Difficulty = Difficulty, BindingDefaultsVersion = BindingDefaultsVersion, Player1 = Player1.Copy(), Player2 = Player2.Copy() };
        public void Clamp() { Player1 = Player1 ?? PlayerBindings.Defaults(0); Player2 = Player2 ?? PlayerBindings.Defaults(1); Player1.Validate(0); Player2.Validate(1); BGM = Mathf.Clamp(BGM, 0, 100); SFX = Mathf.Clamp(SFX, 0, 100); if (!Enum.IsDefined(typeof(GameDifficulty), Difficulty)) Difficulty = GameDifficulty.Normal; }

        void UpgradeBindingDefaults()
        {
            if (BindingDefaultsVersion != 0) return;
            var actions = Player1.actions;
            if (actions[(int)GameAction.Fire].keyboard == KeyCode.Z &&
                actions[(int)GameAction.Special].keyboard == KeyCode.X &&
                actions[(int)GameAction.Dash].keyboard == KeyCode.C)
            {
                actions[(int)GameAction.Fire].keyboard = KeyCode.Q;
                actions[(int)GameAction.Special].keyboard = KeyCode.E;
                actions[(int)GameAction.Dash].keyboard = KeyCode.F;
            }
            BindingDefaultsVersion = 1;
        }

        public static LocalSettings Load(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var browserSettings = new LocalSettings();
            var stored = BrowserStorage.Read(BrowserSettingsKey);
            if (!string.IsNullOrEmpty(stored))
                JsonUtility.FromJsonOverwrite(stored, browserSettings);
            browserSettings.Clamp();
            browserSettings.UpgradeBindingDefaults();
            return browserSettings;
#else
            if (!File.Exists(path))
            {
                var defaults = new LocalSettings();
                defaults.Save(path);
                return defaults;
            }
            // Overwrite a default instance so missing fields retain their default values.
            var result = new LocalSettings();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), result);
            result.Clamp();
            result.UpgradeBindingDefaults();
            return result;
#endif
        }
        public void Save(string path)
        {
            var clean = Copy(); clean.Clamp(); clean.BindingDefaultsVersion = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
            BrowserStorage.Write(BrowserSettingsKey, JsonUtility.ToJson(clean));
#else
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(clean, true));
            // Replace only after the complete new file has been written.
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
#endif
        }
    }
}
